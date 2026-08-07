using System;
using UnityEngine;
using XLua;
using Object = UnityEngine.Object;
using System.Collections.Generic;
#if USE_UNI_LUA
using LuaAPI = UniLua.Lua;
using RealStatePtr = UniLua.ILuaState;
using LuaCSFunction = UniLua.CSharpFunctionDelegate;
#else
using LuaAPI = XLua.LuaDLL.Lua;
using RealStatePtr = System.IntPtr;
#endif

namespace LuaObjectBind
{
    public enum UIObjectBinderScriptType
    {
        CSharp = 0,
        Lua = 1,
    }

    public enum UIObjectBinderLayer
    {
        Auto = 0,
        Bottom = 10,
        Stack = 20,
        Popup = 30,
        Guide = 40,
        Top = 50,
        Loading = 60,
        Tips = 70,
        Debug = 80,
    }

    [Serializable]
    public sealed class UIScriptGenerationSettings
    {
        [Tooltip("创建工具写入的默认脚本类型，可在 Prefab 上手动修改。")]
        public UIObjectBinderScriptType scriptType =
            UIObjectBinderScriptType.Lua;

        [Tooltip("UIManager 打开、关闭界面时使用的唯一 ID。")]
        public string panelId;

        [Tooltip("C# 业务 View 类名；Base 类会自动使用“类名 + Base”。")]
        public string csharpClassName;

        [Tooltip("C# 命名空间。")]
        public string csharpNamespace = "LxyDemo.GameUI";

        [Tooltip("C# Main/Base 脚本输出目录，必须位于 Assets 下。")]
        public string csharpOutputFolder = "Assets/Scripts/GameUI";

        [Tooltip("Lua require 路径，例如 UI.Login.UILogin。")]
        public string luaModuleName;

        [Tooltip("Lua 脚本最终输出目录，相对于项目根目录。")]
        public string luaOutputFolder = "Lua/UI";

        [Tooltip("C# UIManager 使用的挂载层级；Lua 层级仍以 UIDefine 为准。")]
        public UIObjectBinderLayer uiLayer =
            UIObjectBinderLayer.Auto;
    }

    public class ObjectBinder : MonoBehaviour
    {
        [Header("UI Script Generation")]
        public UIScriptGenerationSettings uiScriptGeneration =
            new UIScriptGenerationSettings();
        
        public LuaFileReference lua = new LuaFileReference();
        public BindValueCollection bindValues =
            new BindValueCollection();
        public FieldBindValueCollection fieldBindValues =
            new FieldBindValueCollection();
        public PathBindValueCollection pathBindValues =
            new PathBindValueCollection();
        public StaticTextBindValueCollection staticTextBindValues =
            new StaticTextBindValueCollection();
        public StateControlBindValueCollection stateControlBindValues =
            new StateControlBindValueCollection();
        private WeakReference<Object>[] _bindObjects;
        private WeakReference<Object>[] _fieldBindObjects;
        private FieldBindEnum[] _fieldBindTypes;
        private WeakReference<StateControl.Runtime.StateController>[] _stateControllers;
        private string[] _stateGroupNames;

        // 当前Binder绑定的静态对象，Prefab内部的ObjectBinder对象

        // string, Object(ObjectBinder类型)
        public BinderElementCollection binderElements =
            new BinderElementCollection();

        /// <summary>
        /// 标记为引导节点的 bindValue key 列表，用于引导系统运行时查找
        /// </summary>
        [HideInInspector]
        public List<string> guideNodeKeys = new List<string>();

        private WeakReference<Object>[] _binderElementObjects;

        public string LuaClassName
        {
            get { return lua.ClassName; }
        }

        private int _binderId;
        public int BinderId
        {
            get { return _binderId; }
            private set { _binderId = value; }
        }

        // 对应LuaEnv中的table
        public LuaTable BindedTable { get; private set; }
        private const string WIDGET_TABLE_NAME = "Widget";
        public void Init(LuaTable table)
        {
            if (bindValues == null || fieldBindValues == null || pathBindValues == null || stateControlBindValues == null)
            {
                Debug.LogError("BindValues, FieldBindValues, PathBindValues, or StateControlBindValues is null.");
                return;
            }
            
            // 初始化 staticTextBindValues 如果为 null
            if (staticTextBindValues == null)
            {
                staticTextBindValues = new StaticTextBindValueCollection();
            }

            LuaEnv env = LuaObjectBindProxy.GetLuaEnv();
            if (env == null)
                return;

            // 创建WidgetTable
            LuaTable widget = table.Get<LuaTable>(WIDGET_TABLE_NAME);
            if (widget == null)
            {
                widget = env.NewTable();
                table.Set(WIDGET_TABLE_NAME, widget);
            }

            BindedTable = table;
            
            LuaTable objectIdMap = env.NewTable();
            _binderId = ObjectRefManager.RecordObject(this);
            widget.Set("__objectBinderId", _binderId);
            _bindObjects = new WeakReference<Object>[bindValues.Binds.Count];
            for (int i = 0; i < bindValues.Binds.Count; i++)
            {
                var bindValue = bindValues.Binds[i];
                if (bindValue == null)
                    continue;
                var bindValueName = bindValue.Name;
                if (string.IsNullOrEmpty(bindValueName))
                    continue;
                Object value = bindValue.GetValue<Object>();
                if (value == null)
                    continue;

                objectIdMap.Set(bindValueName, i);
                _bindObjects[i] = new WeakReference<Object>(value);
            }
            widget.Set("__objectIdMap", objectIdMap);

            // 新增：处理 binderLogic
            if (binderElements != null && binderElements.Binds != null)
            {
                LuaTable elementMap = env.NewTable();
                _binderElementObjects = new WeakReference<Object>[binderElements.Binds.Count];
                for (int i = 0; i < binderElements.Binds.Count; i++)
                {
                    var element = binderElements.Binds[i];
                    if (element == null)
                        continue;
                    var elementName = element.Name;
                    if (string.IsNullOrEmpty(elementName))
                        continue;
                    Object value = element.Value;
                    if (value == null)
                        continue;

                    elementMap.Set(elementName, i);
                    _binderElementObjects[i] = new WeakReference<Object>(value);
                }
                widget.Set("__elementMap", elementMap);
            }

            LuaTable fieldMap = env.NewTable();
            _fieldBindObjects = new WeakReference<Object>[fieldBindValues.Binds.Count];
            _fieldBindTypes = new FieldBindEnum[fieldBindValues.Binds.Count];
            for (int i = 0; i < fieldBindValues.Binds.Count; i++)
            {
                var fieldBindValue = fieldBindValues.Binds[i];
                if (fieldBindValue == null)
                    continue;
                var fieldBindValueName = fieldBindValue.Name;
                if (string.IsNullOrEmpty(fieldBindValueName))
                    continue;
                Object value = fieldBindValue.GetValue<Object>();
                if (value == null)
                    continue;

                fieldMap.Set(fieldBindValueName, i);
                _fieldBindObjects[i] = new WeakReference<Object>(value);
                _fieldBindTypes[i] = fieldBindValue.FieldBindType;
            }
            widget.Set("__fieldMap", fieldMap);

            // 处理 pathBindValues
            if (pathBindValues != null && pathBindValues.Binds != null)
            {
                LuaTable pathMap = env.NewTable();
                for (int i = 0; i < pathBindValues.Binds.Count; i++)
                {
                    var pathBindValue = pathBindValues.Binds[i];
                    if (pathBindValue == null)
                        continue;
                    var pathBindValueName = pathBindValue.Name;
                    if (string.IsNullOrEmpty(pathBindValueName))
                        continue;
                    string path = pathBindValue.GetValue<string>();
                    if (string.IsNullOrEmpty(path))
                        continue;

                    pathMap.Set(pathBindValueName, path);
                }
                widget.Set("__pathMap", pathMap);
            }
            
            // 处理 stateControlBindValues
            if (stateControlBindValues != null && stateControlBindValues.Binds != null)
            {
                LuaTable stateControlMap = env.NewTable();
                _stateControllers = new WeakReference<StateControl.Runtime.StateController>[stateControlBindValues.Binds.Count];
                _stateGroupNames = new string[stateControlBindValues.Binds.Count];
                for (int i = 0; i < stateControlBindValues.Binds.Count; i++)
                {
                    var stateControlBindValue = stateControlBindValues.Binds[i];
                    if (stateControlBindValue == null)
                        continue;
                    var stateControlBindValueName = stateControlBindValue.Name;
                    if (string.IsNullOrEmpty(stateControlBindValueName))
                        continue;
                    StateControl.Runtime.StateController controller = stateControlBindValue.GetValue<StateControl.Runtime.StateController>();
                    if (controller == null)
                        continue;

                    stateControlMap.Set(stateControlBindValueName, i);
                    _stateControllers[i] = new WeakReference<StateControl.Runtime.StateController>(controller);
                    _stateGroupNames[i] = stateControlBindValue.StateGroupName;
                }
                widget.Set("__stateControlMap", stateControlMap);
            }

            // 处理 staticTextBindValues - 直接设置到 widget 表中，可通过 self.Widget.xxx 访问
            if (staticTextBindValues != null && staticTextBindValues.Binds != null)
            {
                for (int i = 0; i < staticTextBindValues.Binds.Count; i++)
                {
                    var staticTextBindValue = staticTextBindValues.Binds[i];
                    if (staticTextBindValue == null)
                        continue;
                    var staticTextName = staticTextBindValue.Name;
                    if (string.IsNullOrEmpty(staticTextName))
                        continue;
                    string text = staticTextBindValue.GetValue<string>();
                    
                    // 直接设置到 widget 表，可以通过 self.Widget.xxx 访问
                    widget.Set(staticTextName, text ?? string.Empty);
                }
            }
        }

        public static Object GetObject(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._bindObjects == null || binder._bindObjects.Length <= keyId || binder._bindObjects[keyId] == null)
            {
                return null;
            }

            if (binder._bindObjects[keyId].TryGetTarget(out Object target))
            {
                return target;
            }
            return null;
        }

        // 获取当前ObjectBind的静态Logic对象
        public static ObjectBinder GetBinderLogicElement(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._binderElementObjects == null || binder._binderElementObjects.Length <= keyId || binder._binderElementObjects[keyId] == null)
            {
                return null;
            }
            if (binder._binderElementObjects[keyId].TryGetTarget(out Object target))
            {
                return target as ObjectBinder;
            }
            return null;
        }

        public static string GetPath(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder.pathBindValues == null || binder.pathBindValues.Binds == null || binder.pathBindValues.Binds.Count <= keyId)
                return string.Empty;

            var pathBindValue = binder.pathBindValues.Binds[keyId];
            if (pathBindValue == null)
                return string.Empty;

            return pathBindValue.GetValue<string>();
        }

        private void OnEnable()
        {
            if (BindedTable != null)
            {
                var onEnableFunc = BindedTable.Get<LuaFunction>("OnEnable");
                if (onEnableFunc != null)
                {
                    onEnableFunc.Call(BindedTable); // 以冒号方式传self
                }
            }
        }

        private void OnDisable()
        {
            if (BindedTable != null)
            {
                var onDisableFunc = BindedTable.Get<LuaFunction>("OnDisable");
                if (onDisableFunc != null)
                {
                    onDisableFunc.Call(BindedTable);
                }
            }
        }

        public void Release()
        {
            _bindObjects = null;
            _fieldBindObjects = null;
            _binderElementObjects = null;
            _stateControllers = null;
            _stateGroupNames = null;
            if (_binderId != 0)
            {
                ObjectRefManager.RemoveObject(_binderId);
                _binderId = 0;
            }
            BindedTable?.Dispose();
            BindedTable = null;
        }

        public void OnDestroy()
        {
            Release();
        }

        public static FieldBindEnum GetFieldBindEnum(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindTypes == null || binder._fieldBindTypes.Length <= keyId)
                return FieldBindEnum.None;
            return binder._fieldBindTypes[keyId];
        }

        public static FieldBindTypeEnum GetFieldBindTypeEnum(int binderId, int keyId)
        {
            FieldBindEnum fieldBindEnum = GetFieldBindEnum(binderId, keyId);
            return FieldBindDispatcher.Instance.GetFieldBindType(fieldBindEnum);
        }

        //todo 以后有时间写个代码生成器
        #region 各种类的Get
        [LuaRawFunction]
        public static int GetValue(IntPtr L)
        {
            var binderId = LuaAPI.xlua_tointeger(L, 1);
            var keyId = LuaAPI.xlua_tointeger(L, 2);
            var bindType = GetFieldBindTypeEnum(binderId, keyId);
            switch (bindType)
            {
                case FieldBindTypeEnum.Bool:
                    LuaAPI.lua_pushboolean(L, GetBool(binderId, keyId));
                    break;
                case FieldBindTypeEnum.Int:
                    LuaAPI.xlua_pushinteger(L, GetInt(binderId, keyId));
                    break;
                case FieldBindTypeEnum.Float:
                    LuaAPI.lua_pushnumber(L, GetFloat(binderId, keyId));
                    break;
                case FieldBindTypeEnum.String:
                    LuaAPI.lua_pushstring(L, GetString(binderId, keyId));
                    break;
                // case FieldBindTypeEnum.Vector2:
                // {
                //     // var translator = ObjectTranslatorPool.Instance.Find(L);
                //     // translator.PushUnityEngineVector2(L, GetVector2(binderId, keyId));
                //     // break;
                // }
                // case FieldBindTypeEnum.Vector3:
                // {
                //     // var translator = ObjectTranslatorPool.Instance.Find(L);
                //     // translator.PushUnityEngineVector3(L, GetVector3(binderId, keyId));
                //     // break;
                // }
                case FieldBindTypeEnum.None:
                case FieldBindTypeEnum.LuaTable:
                default:
                    LuaAPI.lua_pushnil(L);
                    break;
            }
            return 1;
        }

        public static string GetString(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return String.Empty;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                return FieldBindDispatcher.Instance.GetString(target, fieldBindValue);
            }
            return String.Empty;
        }

        public static bool GetBool(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return false;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                return FieldBindDispatcher.Instance.GetBool(target, fieldBindValue);
            }
            return false;
        }

        public static int GetInt(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return 0;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                return FieldBindDispatcher.Instance.GetInt(target, fieldBindValue);
            }
            return 0;
        }

        public static float GetFloat(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return 0;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                return FieldBindDispatcher.Instance.GetFloat(target, fieldBindValue);
            }
            return 0;
        }

        public static Vector2 GetVector2(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return Vector2.zero;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                return FieldBindDispatcher.Instance.GetVector2(target, fieldBindValue);
            }
            return Vector2.zero;
        }

        public static Vector3 GetVector3(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return Vector3.zero;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                return FieldBindDispatcher.Instance.GetVector3(target, fieldBindValue);
            }
            return Vector3.zero;
        }

        #endregion

        #region Handle
        [LuaRawFunction]
        public static int SetValue(IntPtr L)
        {
            var binderId = LuaAPI.xlua_tointeger(L, 1);
            var keyId = LuaAPI.xlua_tointeger(L, 2);
            var bindType = GetFieldBindTypeEnum(binderId, keyId);
            switch (bindType)
            {
                case FieldBindTypeEnum.String:
                    HandleString(binderId, keyId, LuaAPI.lua_tostring(L, 3));
                    break;
                case FieldBindTypeEnum.Int:
                    HandleInt(binderId, keyId, LuaAPI.xlua_tointeger(L, 3));
                    break;
                case FieldBindTypeEnum.Float:
                    HandleFloat(binderId, keyId, (float)LuaAPI.lua_tonumber(L, 3));
                    break;
                case FieldBindTypeEnum.Bool:
                    HandleBool(binderId, keyId, LuaAPI.lua_toboolean(L, 3));
                    break;
                case FieldBindTypeEnum.Vector2:
                {
                    var translator = ObjectTranslatorPool.Instance.Find(L);
                    translator.Get(L, 3, out Vector2 value);
                    HandleVector2(binderId, keyId, value);
                    break;
                }
                case FieldBindTypeEnum.Vector3:
                {
                    var translator = ObjectTranslatorPool.Instance.Find(L);
                    translator.Get(L, 3, out Vector3 value);
                    HandleVector3(binderId, keyId, value);
                    break;
                }
                case FieldBindTypeEnum.LuaTable:
                {
                    var translator = ObjectTranslatorPool.Instance.Find(L);
                    var value = (LuaTable)translator.GetObject(L, 3, typeof(LuaTable));
                    HandleLuaTable(binderId, keyId, value);
                    break;
                }
                case FieldBindTypeEnum.None:
                default:
                    LuaAPI.lua_pushboolean(L, false);
                    return 1;
            }
            LuaAPI.lua_pushboolean(L, true);
            return 1;
        }

        public static void HandleString(int binderId, int keyId, string value)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                FieldBindDispatcher.Instance.HandleString(target, fieldBindValue, value);
            }
        }

        public static void HandleBool(int binderId, int keyId, bool value)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                FieldBindDispatcher.Instance.HandleBool(target, fieldBindValue, value);
            }
        }

        public static void HandleInt(int binderId, int keyId, int value)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                FieldBindDispatcher.Instance.HandleInt(target, fieldBindValue, value);
            }
        }

        public static void HandleFloat(int binderId, int keyId, float value)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                FieldBindDispatcher.Instance.HandleFloat(target, fieldBindValue, value);
            }
        }

        public static void HandleVector2(int binderId, int keyId, Vector2 value)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                FieldBindDispatcher.Instance.HandleVector2(target, fieldBindValue, value);
            }
        }

        public static void HandleVector3(int binderId, int keyId, Vector3 value)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                FieldBindDispatcher.Instance.HandleVector3(target, fieldBindValue, value);
            }
        }

        #endregion
        
        #region StateControl

        public static StateControl.Runtime.StateController GetStateController(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._stateControllers == null || binder._stateControllers.Length <= keyId || binder._stateControllers[keyId] == null)
                return null;

            if (binder._stateControllers[keyId].TryGetTarget(out StateControl.Runtime.StateController controller))
            {
                return controller;
            }
            return null;
        }

        public static string GetStateGroupName(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._stateGroupNames == null || binder._stateGroupNames.Length <= keyId)
                return string.Empty;

            return binder._stateGroupNames[keyId];
        }

        public static void ChangeState(int binderId, int keyId, int stateIndex)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._stateControllers == null || binder._stateControllers.Length <= keyId || binder._stateControllers[keyId] == null)
                return;

            if (binder._stateControllers[keyId].TryGetTarget(out StateControl.Runtime.StateController controller))
            {
                string stateGroupName = binder._stateGroupNames[keyId];
                if (!string.IsNullOrEmpty(stateGroupName))
                {
                    controller.ChangeState(stateGroupName, stateIndex);
                }
            }
        }

        public static void ChangeStateByName(int binderId, int keyId, string stateName)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._stateControllers == null || binder._stateControllers.Length <= keyId || binder._stateControllers[keyId] == null)
                return;

            if (binder._stateControllers[keyId].TryGetTarget(out StateControl.Runtime.StateController controller))
            {
                string stateGroupName = binder._stateGroupNames[keyId];
                if (!string.IsNullOrEmpty(stateGroupName))
                {
                    controller.ChangeStateByName(stateGroupName, stateName);
                }
            }
        }

        public static int GetStateValue(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._stateControllers == null || binder._stateControllers.Length <= keyId || binder._stateControllers[keyId] == null)
                return -1;

            if (binder._stateControllers[keyId].TryGetTarget(out StateControl.Runtime.StateController controller))
            {
                string stateGroupName = binder._stateGroupNames[keyId];
                if (!string.IsNullOrEmpty(stateGroupName))
                {
                    return controller.GetStateValue(stateGroupName);
                }
            }
            return -1;
        }

        #endregion
        
        public static object GetLuaTable(int binderId, int keyId)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return null;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                return FieldBindDispatcher.Instance.GetLuaTable(target, fieldBindValue);
            }
            return null;
        }

        public static void HandleLuaTable(int binderId, int keyId, LuaTable value)
        {
            ObjectBinder binder = ObjectRefManager.GetObject(binderId) as ObjectBinder;
            if (binder == null || binder._fieldBindObjects == null || binder._fieldBindObjects.Length <= keyId || binder._fieldBindObjects[keyId] == null)
                return;
            if (binder._fieldBindObjects[keyId].TryGetTarget(out Object target))
            {
                FieldBindEnum fieldBindValue = binder._fieldBindTypes[keyId];
                FieldBindDispatcher.Instance.HandleLuaTable(target, fieldBindValue, value);
            }
        }
    }
}
