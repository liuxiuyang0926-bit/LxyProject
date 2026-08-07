using UnityEngine;

namespace LxyDemo.UIFramework
{
    [DefaultExecutionOrder(-9000)]
    public sealed class UIManagerBootstrap : MonoBehaviour
    {
        [SerializeField]
        private UIManagerSettings settings;

        [SerializeField]
        private UILayerRoot layerRoot;

        private void Awake()
        {
            UIManager.Instance.Initialize(settings, layerRoot);
        }
    }
}
