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
        public string fieldName;

        [Tooltip("需要绑定的 GameObject 或 Component。")]
        public UnityEngine.Object target;

        [Tooltip("找不到目标时是否抛出异常。")]
        public bool required = true;

        [Tooltip("为 Button、Toggle、Slider、InputField 等生成默认事件绑定。")]
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

        public string PanelId => panelId;
        public string CodeNamespace => codeNamespace;
        public string LogicClassName => logicClassName;
        public string MainScriptPath => mainScriptPath;
        public string AutoScriptPath => autoScriptPath;
        public UIScriptType ScriptType => scriptType;
        public string LuaModuleName => luaModuleName;
        public UILayer Layer => uiLayer;
        public int LayerValue => (int)uiLayer;
        public List<UICodeBinding> Bindings => bindings;

#if UNITY_EDITOR
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
