using UnityEngine;

namespace LxyDemo.UIFramework
{
    [DefaultExecutionOrder(-9000)]
    public sealed class UIManagerBootstrap : MonoBehaviour
    {
        /// <summary>
        /// 在 Inspector 中指定的 UI 管理器运行设置。
        /// </summary>
        [SerializeField]
        private UIManagerSettings settings;

        /// <summary>
        /// 在 Inspector 中指定的 UI 层级根节点；为空时由 UIManager 自动创建。
        /// </summary>
        [SerializeField]
        private UILayerRoot layerRoot;

        /// <summary>
        /// 初始化组件的运行时状态。
        /// </summary>
        private void Awake()
        {
            UIManager.Instance.Initialize(settings, layerRoot);
        }
    }
}
