using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using XLua;
using Object = UnityEngine.Object;

namespace LuaObjectBind
{
    public class FieldBindHandlerAttribute: Attribute
    {
    }
    
    public interface IFieldBindHandler
    {
        //todo 以后有时间写个代码生成器
        #region 各种类型的Handle和Get
        /// <summary>
        /// 执行InternalHandle字符串相关逻辑。
        /// </summary>
        void InternalHandleString(Object obj, FieldBindEnum type, string value);
        /// <summary>
        /// 执行InternalHandle布尔值相关逻辑。
        /// </summary>
        void InternalHandleBool(Object obj, FieldBindEnum type, bool value);
        /// <summary>
        /// 执行InternalHandle整数相关逻辑。
        /// </summary>
        void InternalHandleInt(Object obj, FieldBindEnum type, int value);
        /// <summary>
        /// 执行InternalHandle浮点数相关逻辑。
        /// </summary>
        void InternalHandleFloat(Object obj, FieldBindEnum type, float value);
        /// <summary>
        /// 执行InternalHandleVector2相关逻辑。
        /// </summary>
        void InternalHandleVector2(Object obj, FieldBindEnum type, Vector2 value);
        /// <summary>
        /// 执行InternalHandleVector3相关逻辑。
        /// </summary>
        void InternalHandleVector3(Object obj, FieldBindEnum type, Vector3 value);
        /// <summary>
        /// 执行InternalHandleLua表相关逻辑。
        /// </summary>
        void InternalHandleLuaTable(Object obj, FieldBindEnum type, LuaTable value);
        
        /// <summary>
        /// 执行Internal获取字符串相关逻辑。
        /// </summary>
        string InternalGetString(Object obj, FieldBindEnum type);
        /// <summary>
        /// 执行Internal获取布尔值相关逻辑。
        /// </summary>
        bool InternalGetBool(Object obj, FieldBindEnum type);
        /// <summary>
        /// 执行Internal获取整数相关逻辑。
        /// </summary>
        int InternalGetInt(Object obj, FieldBindEnum type);
        /// <summary>
        /// 执行Internal获取浮点数相关逻辑。
        /// </summary>
        float InternalGetFloat(Object obj, FieldBindEnum type);
        /// <summary>
        /// 执行Internal获取Vector2相关逻辑。
        /// </summary>
        Vector2 InternalGetVector2(Object obj, FieldBindEnum type);
        /// <summary>
        /// 执行Internal获取Vector3相关逻辑。
        /// </summary>
        Vector3 InternalGetVector3(Object obj, FieldBindEnum type);
        /// <summary>
        /// 执行Internal获取Lua表相关逻辑。
        /// </summary>
        LuaTable InternalGetLuaTable(Object obj, FieldBindEnum type);
        #endregion
        /// <summary>
        /// 获取类型。
        /// </summary>
        Type GetType();
    }
    
    [FieldBindHandler]
    public abstract class AFieldBindHandler<T>: IFieldBindHandler where T : Object
    {
        //todo 以后有时间写个代码生成器
        #region 各种类型的Handle和Get
        /// <summary>
        /// 将字符串值写入绑定对象。
        /// </summary>
        protected virtual void HandleString(T obj, FieldBindEnum type, string value)
        {
        }

        /// <summary>
        /// 执行内部Handle字符串相关逻辑。
        /// </summary>
        public void InternalHandleString(Object obj, FieldBindEnum type, string value)
        {
            HandleString(obj as T, type, value);
        }
        
        /// <summary>
        /// 获取字符串。
        /// </summary>
        protected virtual string GetString(T obj, FieldBindEnum type)
        {
            return string.Empty;
        }

        /// <summary>
        /// 执行内部获取字符串相关逻辑。
        /// </summary>
        public string InternalGetString(Object obj, FieldBindEnum type)
        {
            return GetString(obj as T, type);
        }

        /// <summary>
        /// 处理布尔值。
        /// </summary>
        protected virtual void HandleBool(T obj, FieldBindEnum type, bool value)
        {
        }
        /// <summary>
        /// 执行内部Handle布尔值相关逻辑。
        /// </summary>
        public void InternalHandleBool(Object obj, FieldBindEnum type, bool value)
        {
            HandleBool(obj as T, type, value);
        }
        
        /// <summary>
        /// 获取布尔值。
        /// </summary>
        protected virtual bool GetBool(T obj, FieldBindEnum type)
        {
            return false;
        }
        /// <summary>
        /// 执行内部获取布尔值相关逻辑。
        /// </summary>
        public bool InternalGetBool(Object obj, FieldBindEnum type)
        {
            return GetBool(obj as T, type);
        }

        /// <summary>
        /// 处理整数。
        /// </summary>
        protected virtual void HandleInt(T obj, FieldBindEnum type, int value)
        {
        }
        /// <summary>
        /// 执行内部Handle整数相关逻辑。
        /// </summary>
        public void InternalHandleInt(Object obj, FieldBindEnum type, int value)
        {
            HandleInt(obj as T, type, value);
        }
        /// <summary>
        /// 获取整数。
        /// </summary>
        protected virtual int GetInt(T obj, FieldBindEnum type)
        {
            return 0;
        }
        /// <summary>
        /// 执行内部获取整数相关逻辑。
        /// </summary>
        public int InternalGetInt(Object obj, FieldBindEnum type)
        {
            return GetInt(obj as T, type);
        }
        
        /// <summary>
        /// 处理浮点数。
        /// </summary>
        protected virtual void HandleFloat(T obj, FieldBindEnum type, float value)
        {
        }
        /// <summary>
        /// 执行内部Handle浮点数相关逻辑。
        /// </summary>
        public void InternalHandleFloat(Object obj, FieldBindEnum type, float value)
        {
            HandleFloat(obj as T, type, value);
        }
        /// <summary>
        /// 获取浮点数。
        /// </summary>
        protected virtual float GetFloat(T obj, FieldBindEnum type)
        {
            return 0;
        }
        /// <summary>
        /// 执行内部获取浮点数相关逻辑。
        /// </summary>
        public float InternalGetFloat(Object obj, FieldBindEnum type)
        {
            return GetFloat(obj as T, type);
        }
        /// <summary>
        /// 处理Vector2。
        /// </summary>
        protected virtual void HandleVector2(T obj, FieldBindEnum type, Vector2 value)
        {
        }
        /// <summary>
        /// 执行内部HandleVector2相关逻辑。
        /// </summary>
        public void InternalHandleVector2(Object obj, FieldBindEnum type, Vector2 value)
        {
            HandleVector2(obj as T, type, value);
        }
        /// <summary>
        /// 获取Vector2。
        /// </summary>
        protected virtual Vector2 GetVector2(T obj, FieldBindEnum type)
        {
            return Vector2.zero;
        }
        /// <summary>
        /// 执行内部获取Vector2相关逻辑。
        /// </summary>
        public Vector2 InternalGetVector2(Object obj, FieldBindEnum type)
        {
            return GetVector2(obj as T, type);
        }
        /// <summary>
        /// 处理Vector3。
        /// </summary>
        protected virtual void HandleVector3(T obj, FieldBindEnum type, Vector3 value)
        {
        }
        /// <summary>
        /// 执行内部HandleVector3相关逻辑。
        /// </summary>
        public void InternalHandleVector3(Object obj, FieldBindEnum type, Vector3 value)
        {
            HandleVector3(obj as T, type, value);
        }
        /// <summary>
        /// 获取Vector3。
        /// </summary>
        protected virtual Vector3 GetVector3(T obj, FieldBindEnum type)
        {
            return Vector3.zero;
        }
        /// <summary>
        /// 执行内部获取Vector3相关逻辑。
        /// </summary>
        public Vector3 InternalGetVector3(Object obj, FieldBindEnum type)
        {
            return GetVector3(obj as T, type);
        }
        
        /// <summary>
        /// 处理Lua表。
        /// </summary>
        protected virtual void HandleLuaTable(T obj, FieldBindEnum type, LuaTable value)
        {
        }
        /// <summary>
        /// 执行内部HandleLuaTable相关逻辑。
        /// </summary>
        public void InternalHandleLuaTable(Object obj, FieldBindEnum type, LuaTable value)
        {
            HandleLuaTable(obj as T, type, value);
        }
        /// <summary>
        /// 获取Lua表。
        /// </summary>
        protected virtual LuaTable GetLuaTable(T obj, FieldBindEnum type)
        {
            return null;
        }
        /// <summary>
        /// 执行内部获取LuaTable相关逻辑。
        /// </summary>
        public LuaTable InternalGetLuaTable(Object obj, FieldBindEnum type)
        {
            return GetLuaTable(obj as T, type);
        }
        #endregion
        
        /// <summary>
        /// 获取类型。
        /// </summary>
        public virtual Type GetType()
        {
            return typeof (T);
        }
    }
    
    public class FieldBindDispatcher: Singleton<FieldBindDispatcher>
    {
        private readonly Dictionary<Type, IFieldBindHandler> FieldBindHandlers = new();
        private readonly Dictionary<FieldBindEnum, FieldBindTypeEnum> BindTypeMap = new();

        /// <summary>
        /// 初始化当前实例。
        /// </summary>
        public void Init()
        {
            Assembly assembly = typeof(FieldBindDispatcher).Assembly;
            var types = assembly.GetTypes();
            
            foreach (Type type in types)
            {
                if (type.IsAbstract)
                {
                    continue;
                }
                
                object[] objects = type.GetCustomAttributes(typeof(FieldBindHandlerAttribute), true);

                foreach (object o in objects)
                {
                    Register(type);
                    break;
                }
            }
            
            InitFieldTypeMap();
        }
        
        /// <summary>
        /// 注册当前实例。
        /// </summary>
        private void Register(Type type)
        {
            object obj = Activator.CreateInstance(type);

            IFieldBindHandler iFieldBindHandler = obj as IFieldBindHandler;
            if (iFieldBindHandler == null)
            {
                throw new Exception($"FieldBind handler not inherit IMActorHandler abstract class: {obj.GetType().FullName}");
            }

            Type objType = iFieldBindHandler.GetType();
            FieldBindHandlers.TryAdd(objType, iFieldBindHandler);
        }

        /// <summary>
        /// 执行初始化字段类型映射相关逻辑。
        /// </summary>
        private void InitFieldTypeMap()
        {
            //遍历所有的FiledBindEnum和它的FieldBindType属性
            foreach (FieldBindEnum fieldBindEnum in Enum.GetValues(typeof(FieldBindEnum)))
            {
                FieldInfo fieldInfo = fieldBindEnum.GetType().GetField(fieldBindEnum.ToString());
                if (fieldInfo == null)
                {
                    continue;
                }
                
                object[] attributes = fieldInfo.GetCustomAttributes(typeof(FieldBindTypeAttribute), true);
                if (attributes.Length > 0)
                {
                    FieldBindTypeAttribute filedBindTypeAttribute = attributes[0] as FieldBindTypeAttribute;
                    if (filedBindTypeAttribute != null)
                    {
                        BindTypeMap.Add(fieldBindEnum, filedBindTypeAttribute.FieldBindType);
                    }
                }
            }
        }
        
        /// <summary>
        /// 获取Field绑定类型。
        /// </summary>
        public FieldBindTypeEnum GetFieldBindType(FieldBindEnum fieldBindEnum)
        {
            if (BindTypeMap.TryGetValue(fieldBindEnum, out FieldBindTypeEnum fieldBindType))
            {
                return fieldBindType;
            }
            return FieldBindTypeEnum.None;
        }
        
        #region 设置FieldBind值
        /// <summary>
        /// 处理字符串。
        /// </summary>
        public void HandleString(Object obj, FieldBindEnum type, string value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }
            iFieldBindHandler.InternalHandleString(obj, type, value);
        }

        /// <summary>
        /// 处理布尔值。
        /// </summary>
        public void HandleBool(Object obj, FieldBindEnum type, bool value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleBool(obj, type, value);
        }
        
        /// <summary>
        /// 处理整数。
        /// </summary>
        public void HandleInt(Object obj, FieldBindEnum type, int value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleInt(obj, type, value);
        }
        
        /// <summary>
        /// 处理浮点数。
        /// </summary>
        public void HandleFloat(Object obj, FieldBindEnum type, float value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleFloat(obj, type, value);
        }
        
        /// <summary>
        /// 处理Vector2。
        /// </summary>
        public void HandleVector2(Object obj, FieldBindEnum type, Vector2 value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleVector2(obj, type, value);
        }
        
        /// <summary>
        /// 处理Vector2。
        /// </summary>
        public void HandleVector2(Object obj, FieldBindEnum type, float x, float y)
        {
            HandleVector2(obj, type, new Vector2(x, y));
        }
        
        /// <summary>
        /// 处理Vector3。
        /// </summary>
        public void HandleVector3(Object obj, FieldBindEnum type, Vector3 value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleVector3(obj, type, value);
        }
        
        /// <summary>
        /// 处理Vector3。
        /// </summary>
        public void HandleVector3(Object obj, FieldBindEnum type, float x, float y, float z)
        {
            HandleVector3(obj, type, new Vector3(x, y, z));
        }
        
        /// <summary>
        /// 处理Lua表。
        /// </summary>
        public void HandleLuaTable(Object obj, FieldBindEnum type, LuaTable value)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            iFieldBindHandler.InternalHandleLuaTable(obj, type, value);
        }
        #endregion
        
        #region 获取FieldBind值
        /// <summary>
        /// 获取字符串。
        /// </summary>
        public string GetString(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetString(obj, type);
        }
        
        /// <summary>
        /// 获取布尔值。
        /// </summary>
        public bool GetBool(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetBool(obj, type);
        }
        
        /// <summary>
        /// 获取整数。
        /// </summary>
        public int GetInt(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetInt(obj, type);
        }
        
        /// <summary>
        /// 获取浮点数。
        /// </summary>
        public float GetFloat(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetFloat(obj, type);
        }
        
        /// <summary>
        /// 获取Vector2。
        /// </summary>
        public Vector2 GetVector2(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetVector2(obj, type);
        }
        
        /// <summary>
        /// 获取Vector3。
        /// </summary>
        public Vector3 GetVector3(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetVector3(obj, type);
        }
        
        /// <summary>
        /// 获取Lua表。
        /// </summary>
        public object GetLuaTable(Object obj, FieldBindEnum type)
        {
            if (!FieldBindHandlers.TryGetValue(obj.GetType(), out IFieldBindHandler iFieldBindHandler))
            {
                throw new Exception($"not found FieldBind handler: {obj.GetType().FullName}");
            }

            return iFieldBindHandler.InternalGetLuaTable(obj, type);
        }
        #endregion

        // Domain Reload Support
#if UNITY_EDITOR
        /// <summary>
        /// 响应EnterPlay模式事件。
        /// </summary>
        [UnityEditor.InitializeOnEnterPlayMode]
        static void OnEnterPlayMode(UnityEditor.EnterPlayModeOptions options)
        {
            ResetHandler(options);
        }
#endif
    }
}