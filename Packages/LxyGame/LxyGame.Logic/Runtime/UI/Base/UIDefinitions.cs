using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace LxyDemo.UIFramework
{
    public enum UIScriptType
    {
        CSharp = 0,
        Lua = 1
    }

    public enum UIPanelCloseType
    {
        Destroy,
        Hide
    }

    public enum UILayer
    {
        Auto = 0,
        Bottom = 10,
        Stack = 20,
        Popup = 30,
        Guide = 40,
        Top = 50,
        Loading = 60,
        Tips = 70,
        Debug = 80
    }

    public enum UIPanelState
    {
        None,
        Creating,
        Loading,
        Visible,
        Hidden,
        Closing,
        Destroyed,
        Failed
    }

    public enum UIFrameworkErrorCode
    {
        InvalidArgument,
        PanelNotRegistered,
        DuplicatePanelId,
        InvalidStateTransition,
        LogicCreationFailed,
        AssetLoadFailed,
        BindFailed,
        ShowFailed,
        OperationCancelled,
        PreloadTimeout,
        ManagerShuttingDown
    }

    [Serializable]
    public sealed class UIPanelConfig
    {
        [Tooltip("全局唯一的面板 ID，业务代码使用它打开和关闭界面。")]
        [SerializeField]
        private string id;

        [Tooltip("Prefab 所属的 YooAsset ResourcePackage 名称。")]
        [FormerlySerializedAs("bundleName")]
        [SerializeField]
        private string packageName = "DefaultPackage";

        [Tooltip("YooAsset 资源定位地址，可以填写 Address 或完整 Assets 路径。")]
        [FormerlySerializedAs("assetName")]
        [SerializeField]
        private string location;

        [Tooltip("UIPanelLogic 的程序集限定类型名。由 C# UI 生成器自动填写。")]
        [SerializeField]
        private string logicTypeName;

        [Tooltip("开启后不进入导航栈，适用于弹窗、提示、浮层。")]
        [SerializeField]
        private bool ignoreStack;

        [Tooltip("Auto 会让栈界面进入 Stack 层，让 IgnoreStack 界面进入 Popup 层。")]
        [SerializeField]
        private UILayer layer = UILayer.Auto;

        [Tooltip("关闭时销毁实例，或仅隐藏并保留实例与 YooAsset 句柄。")]
        [SerializeField]
        private UIPanelCloseType closeType =
            UIPanelCloseType.Destroy;

        [Tooltip("显示栈界面时，自动关闭非 Debug 层的弹窗。")]
        [SerializeField]
        private bool closePopupsWhenShown = true;

        [Tooltip("切换 Active Scene 时自动销毁。")]
        [SerializeField]
        private bool autoDestroyWhenSceneChanged = true;

        [Tooltip("创建背景遮罩。默认实现为半透明遮罩，可替换为项目自己的模糊服务。")]
        [SerializeField]
        private bool blurMode;

        /// <summary>
        /// 是否允许点击背景遮罩时关闭当前面板。
        /// </summary>
        [Tooltip("点击背景遮罩时关闭当前面板。")]
        [SerializeField]
        private bool blurCloseOnClick;

        [SerializeField]
        private Color backdropColor =
            new Color(0f, 0f, 0f, 0.65f);

        public string Id
        {
            get => id;
            set => id = value;
        }

        public string PackageName
        {
            get => packageName;
            set => packageName = value;
        }

        public string Location
        {
            get => location;
            set => location = value;
        }

        public string LogicTypeName
        {
            get => logicTypeName;
            set => logicTypeName = value;
        }

        public bool IgnoreStack
        {
            get => ignoreStack;
            set => ignoreStack = value;
        }

        public UILayer Layer
        {
            get => layer;
            set => layer = value;
        }

        public UIPanelCloseType CloseType
        {
            get => closeType;
            set => closeType = value;
        }

        public bool ClosePopupsWhenShown
        {
            get => closePopupsWhenShown;
            set => closePopupsWhenShown = value;
        }

        public bool AutoDestroyWhenSceneChanged
        {
            get => autoDestroyWhenSceneChanged;
            set => autoDestroyWhenSceneChanged = value;
        }

        public bool BlurMode
        {
            get => blurMode;
            set => blurMode = value;
        }

        public bool BlurCloseOnClick
        {
            get => blurCloseOnClick;
            set => blurCloseOnClick = value;
        }

        public Color BackdropColor
        {
            get => backdropColor;
            set => backdropColor = value;
        }

        /// <summary>
        /// 向调用方提供Effective层级。
        /// </summary>
        public UILayer EffectiveLayer =>
            layer == UILayer.Auto
                ? (ignoreStack ? UILayer.Popup : UILayer.Stack)
                : layer;

        /// <summary>
        /// 执行校验相关逻辑。
        /// </summary>
        public void Validate()
        {
            id = Normalize(id);
            packageName = Normalize(packageName);
            location = Normalize(location);
            logicTypeName = string.IsNullOrWhiteSpace(logicTypeName)
                ? string.Empty
                : logicTypeName.Trim();

            if (id.Length == 0)
            {
                throw new UIFrameworkException(
                    UIFrameworkErrorCode.InvalidArgument,
                    "UIPanelConfig.Id 不能为空。");
            }

            if (packageName.Length == 0)
            {
                throw new UIFrameworkException(
                    UIFrameworkErrorCode.InvalidArgument,
                    $"面板 {id} 的 PackageName 不能为空。",
                    id);
            }

            if (location.Length == 0)
            {
                throw new UIFrameworkException(
                    UIFrameworkErrorCode.InvalidArgument,
                    $"面板 {id} 的 Location 不能为空。",
                    id);
            }
        }

        /// <summary>
        /// 规范化当前数据。
        /// </summary>
        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/');
        }
    }

    [Serializable]
    public sealed class UIFrameworkException : Exception
    {
        /// <summary>
        /// 向调用方提供错误Code。
        /// </summary>
        public UIFrameworkErrorCode ErrorCode { get; }
        /// <summary>
        /// 向调用方提供面板标识。
        /// </summary>
        public string PanelId { get; }

        /// <summary>
        /// 创建UIFrameworkException实例。
        /// </summary>
        public UIFrameworkException(
            UIFrameworkErrorCode errorCode,
            string message,
            string panelId = null,
            Exception innerException = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            PanelId = panelId;
        }
    }

    public readonly struct UIPanelRuntimeSnapshot
    {
        /// <summary>
        /// 创建UI面板运行时快照实例。
        /// </summary>
        public UIPanelRuntimeSnapshot(
            string panelId,
            UIPanelState state,
            bool isLoaded,
            bool isVisible,
            bool isInStack,
            string pendingCloseTarget,
            long requestId)
        {
            PanelId = panelId;
            State = state;
            IsLoaded = isLoaded;
            IsVisible = isVisible;
            IsInStack = isInStack;
            PendingCloseTarget = pendingCloseTarget;
            RequestId = requestId;
        }

        /// <summary>
        /// 向调用方提供面板标识。
        /// </summary>
        public string PanelId { get; }
        /// <summary>
        /// 向调用方提供状态。
        /// </summary>
        public UIPanelState State { get; }
        /// <summary>
        /// 指示当前对象是否已加载。
        /// </summary>
        public bool IsLoaded { get; }
        /// <summary>
        /// 指示Visible是否成立。
        /// </summary>
        public bool IsVisible { get; }
        /// <summary>
        /// 指示In栈是否成立。
        /// </summary>
        public bool IsInStack { get; }
        /// <summary>
        /// 向调用方提供PendingCloseTarget。
        /// </summary>
        public string PendingCloseTarget { get; }
        /// <summary>
        /// 向调用方提供Request标识。
        /// </summary>
        public long RequestId { get; }
    }

    public readonly struct UIStackSnapshot
    {
        /// <summary>
        /// 创建UI栈快照实例。
        /// </summary>
        public UIStackSnapshot(string panelId, object userData)
        {
            PanelId = panelId;
            UserData = userData;
        }

        /// <summary>
        /// 向调用方提供面板标识。
        /// </summary>
        public string PanelId { get; }
        /// <summary>
        /// 向调用方提供User数据。
        /// </summary>
        public object UserData { get; }
    }
}
