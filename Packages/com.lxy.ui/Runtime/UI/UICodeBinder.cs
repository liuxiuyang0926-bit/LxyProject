using System;
using System.Collections.Generic;
using LuaObjectBind;
using UnityEngine;

namespace LxyDemo.UIFramework
{
    [Serializable]
    public sealed class UICodeBinding
    {
        [Tooltip("生成到 Auto C# / Lua 文件中的字段名。")]
        /// <summary>
        /// 公开的字段名称数据。
        /// </summary>
        public string fieldName;

        [Tooltip("需要绑定的 GameObject 或 Component。")]
        /// <summary>
        /// 公开的目标数据。
        /// </summary>
        public UnityEngine.Object target;

        [Tooltip("找不到目标时是否抛出异常。")]
        /// <summary>
        /// 公开的required数据。
        /// </summary>
        public bool required = true;

        [Tooltip("为 Button、Toggle、Slider、InputField 等生成默认事件绑定。")]
        /// <summary>
        /// 公开的绑定默认值事件数据。
        /// </summary>
        public bool bindDefaultEvent;
    }

    /// <summary>
    /// 仅保存编辑器代码生成元数据。运行时字段绑定由生成的 Auto C# / Lua 完成。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UICodeBinder : MonoBehaviour
    {
        [SerializeField]
        private string panelId;

        [SerializeField]
        private string codeNamespace;

        [SerializeField]
        private string logicClassName;

        [SerializeField]
        private string mainScriptPath;

        [SerializeField]
        private string autoScriptPath;

        [SerializeField]
        private UIScriptType scriptType =
            UIScriptType.CSharp;

        [SerializeField]
        private string luaModuleName;

        [SerializeField]
        [Tooltip("UIManager 会把实例挂到 UICanvasRoot 中对应的 UILayer 节点。")]
        private UILayer uiLayer = UILayer.Auto;

        [SerializeField]
        private List<UICodeBinding> bindings =
            new List<UICodeBinding>();

        /// <summary>
        /// 向调用方提供面板标识。
        /// </summary>
        public string PanelId => panelId;
        /// <summary>
        /// 向调用方提供CodeNamespace。
        /// </summary>
        public string CodeNamespace => codeNamespace;
        /// <summary>
        /// 向调用方提供LogicClass名称。
        /// </summary>
        public string LogicClassName => logicClassName;
        /// <summary>
        /// 向调用方提供MainScript路径。
        /// </summary>
        public string MainScriptPath => mainScriptPath;
        /// <summary>
        /// 向调用方提供AutoScript路径。
        /// </summary>
        public string AutoScriptPath => autoScriptPath;
        /// <summary>
        /// 向调用方提供Script类型。
        /// </summary>
        public UIScriptType ScriptType => scriptType;
        /// <summary>
        /// 向调用方提供LuaModule名称。
        /// </summary>
        public string LuaModuleName => luaModuleName;
        /// <summary>
        /// 向调用方提供层级。
        /// </summary>
        public UILayer Layer => uiLayer;
        /// <summary>
        /// 向调用方提供层级值。
        /// </summary>
        public int LayerValue => (int)uiLayer;
        /// <summary>
        /// 向调用方提供Bindings。
        /// </summary>
        public List<UICodeBinding> Bindings => bindings;

#if UNITY_EDITOR
        /// <summary>
        /// 执行配置生成器相关逻辑。
        /// </summary>
        public void ConfigureGenerator(
            string configuredPanelId,
            string configuredNamespace,
            string configuredClassName,
            string configuredMainScriptPath,
            string configuredAutoScriptPath,
            UILayer configuredLayer,
            UIScriptType configuredScriptType,
            string configuredLuaModuleName)
        {
            panelId = configuredPanelId;
            codeNamespace = configuredNamespace;
            logicClassName = configuredClassName;
            mainScriptPath = configuredMainScriptPath;
            autoScriptPath = configuredAutoScriptPath;
            uiLayer = configuredLayer;
            scriptType = configuredScriptType;
            luaModuleName = configuredLuaModuleName;
        }
#endif
    }

    public static class UIBindingUtility
    {
        public static T GetBoundObject<T>(
            ObjectBinder binder,
            string bindingName,
            string panelId,
            bool required)
            where T : UnityEngine.Object
        {
            if (binder == null)
            {
                if (required)
                {
                    throw new MissingComponentException(
                        $"面板 {panelId} 缺少 ObjectBinder。");
                }

                return null;
            }

            UnityEngine.Object value =
                binder.bindValues?.Get(bindingName)
                    as UnityEngine.Object;
            T result = value as T;
            if (result == null && required)
            {
                string actualType = value == null
                    ? "null"
                    : value.GetType().FullName;
                throw new MissingReferenceException(
                    $"面板 {panelId} 的 ObjectBinder 绑定 " +
                    $"{bindingName} 无效；期望 {typeof(T).FullName}，" +
                    $"实际 {actualType}。");
            }

            return result;
        }

        /// <summary>
        /// 获取BoundLogic对象。
        /// </summary>
        public static ObjectBinder GetBoundLogicObject(
            ObjectBinder binder,
            string bindingName,
            string panelId,
            bool required)
        {
            if (binder == null)
            {
                if (required)
                {
                    throw new MissingComponentException(
                        $"面板 {panelId} 缺少 ObjectBinder。");
                }

                return null;
            }

            ObjectBinder result =
                binder.binderElements?.Get(bindingName);
            if (result == null && required)
            {
                throw new MissingReferenceException(
                    $"面板 {panelId} 的 ObjectBinder Logic 绑定 " +
                    $"{bindingName} 无效；期望 " +
                    $"{typeof(ObjectBinder).FullName}。");
            }

            return result;
        }

        public static T FindComponent<T>(
            GameObject root,
            string path,
            string panelId,
            bool required)
            where T : Component
        {
            Transform target = FindTransform(
                root,
                path,
                panelId,
                required);
            if (target == null)
            {
                return null;
            }

            T component = target.GetComponent<T>();
            if (component == null && required)
            {
                throw new MissingComponentException(
                    $"面板 {panelId} 的节点 {path} 缺少组件 " +
                    typeof(T).FullName);
            }

            return component;
        }

        /// <summary>
        /// 查找游戏对象。
        /// </summary>
        public static GameObject FindGameObject(
            GameObject root,
            string path,
            string panelId,
            bool required)
        {
            Transform target = FindTransform(
                root,
                path,
                panelId,
                required);
            return target == null ? null : target.gameObject;
        }

        /// <summary>
        /// 查找变换。
        /// </summary>
        private static Transform FindTransform(
            GameObject root,
            string path,
            string panelId,
            bool required)
        {
            if (root == null)
            {
                if (required)
                {
                    throw new MissingReferenceException(
                        $"面板 {panelId} 的根 GameObject 为空。");
                }

                return null;
            }

            Transform target =
                string.IsNullOrEmpty(path) || path == "."
                    ? root.transform
                    : root.transform.Find(path);
            if (target == null && required)
            {
                throw new MissingReferenceException(
                    $"面板 {panelId} 中找不到绑定路径：{path}");
            }

            return target;
        }
    }
}
