using System.Collections.Generic;
using UnityEngine;

namespace LxyDemo.UIFramework
{
    [CreateAssetMenu(
        fileName = "UIManagerSettings",
        menuName = "LxyDemo/UI/UI Manager Settings")]
    public sealed class UIManagerSettings : ScriptableObject
    {
        [SerializeField]
        private List<UIPanelConfig> panels =
            new List<UIPanelConfig>();

        [Tooltip("Active Scene 改变时自动销毁配置为 AutoDestroyWhenSceneChanged 的面板。")]
        [SerializeField]
        private bool closePanelsOnActiveSceneChanged = true;

        [Tooltip("UIRoot 和 UIManager 跨场景保留。")]
        [SerializeField]
        private bool dontDestroyOnLoad = true;

        [Tooltip("框架状态迁移和栈变化日志。")]
        [SerializeField]
        private bool verboseLogging;

        /// <summary>
        /// 向调用方提供Panels。
        /// </summary>
        public IReadOnlyList<UIPanelConfig> Panels => panels;
        /// <summary>
        /// 向调用方提供ClosePanelsOnActive场景Changed。
        /// </summary>
        public bool ClosePanelsOnActiveSceneChanged =>
            closePanelsOnActiveSceneChanged;
        /// <summary>
        /// 向调用方提供DontDestroyOnLoad。
        /// </summary>
        public bool DontDestroyOnLoad => dontDestroyOnLoad;
        /// <summary>
        /// 向调用方提供VerboseLogging。
        /// </summary>
        public bool VerboseLogging => verboseLogging;

#if UNITY_EDITOR
        /// <summary>
        /// 添加OrUpdate面板From编辑器。
        /// </summary>
        public void AddOrUpdatePanelFromEditor(
            UIPanelConfig panelConfig)
        {
            if (panelConfig == null)
            {
                return;
            }

            panelConfig.Validate();
            int existingIndex = panels.FindIndex(
                item => item != null &&
                        item.Id == panelConfig.Id);
            if (existingIndex >= 0)
            {
                panels[existingIndex] = panelConfig;
            }
            else
            {
                panels.Add(panelConfig);
            }

            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
