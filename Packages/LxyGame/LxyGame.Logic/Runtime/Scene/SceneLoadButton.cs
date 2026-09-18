using UnityEngine;
using UnityEngine.UI;

namespace LxyDemo.SceneManagement
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LxyDemo/Scene/Scene Load Button")]
    public sealed class SceneLoadButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private string targetSceneName = "Battle";
        [SerializeField] private bool disableWhileLoading = true;

        private GameSceneManager subscribedManager;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(LoadTargetScene);
            }

            SubscribeSceneRuntime();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(LoadTargetScene);
            }

            UnsubscribeSceneRuntime();
        }

        public void LoadTargetScene()
        {
            if (string.IsNullOrWhiteSpace(targetSceneName))
            {
                Debug.LogError("[SceneLoadButton] 目标场景名称为空。", this);
                return;
            }

            GameSceneManager manager = GameSceneManager.Instance;
            SubscribeSceneRuntime();
            if (disableWhileLoading && button != null)
            {
                button.interactable = false;
            }

            if (manager.LoadSceneAsync(targetSceneName) == null &&
                !string.Equals(
                    manager.ActiveSceneName,
                    targetSceneName,
                    System.StringComparison.Ordinal))
            {
                RestoreButton(targetSceneName);
            }
        }

        private void SubscribeSceneRuntime()
        {
            if (subscribedManager != null)
            {
                return;
            }

            GameSceneManager manager = GameSceneManager.Instance;
            manager.SceneLoadCompleted += RestoreButton;
            manager.SceneLoadFailed += OnSceneLoadFailed;
            subscribedManager = manager;
        }

        private void UnsubscribeSceneRuntime()
        {
            if (subscribedManager == null)
            {
                return;
            }

            subscribedManager.SceneLoadCompleted -= RestoreButton;
            subscribedManager.SceneLoadFailed -= OnSceneLoadFailed;
            subscribedManager = null;
        }

        private void OnSceneLoadFailed(string sceneName, string error)
        {
            RestoreButton(sceneName);
        }

        private void RestoreButton(string sceneName)
        {
            if (button != null)
            {
                button.interactable = true;
            }
        }
    }
}
