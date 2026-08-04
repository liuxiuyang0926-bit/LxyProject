using System.Collections;
using LxyDemo.SceneManagement;
using UnityEngine;

namespace LxyDemo
{
    /// <summary>
    /// 游戏唯一启动入口。负责全局初始化，并在初始化完成后
    /// 请求 GameSceneManager 进入第一个业务场景。
    /// </summary>
    [DefaultExecutionOrder(-19000)]
    [DisallowMultipleComponent]
    public class GameMain : MonoBehaviour
    {
        [Header("首个业务场景")]
        [SerializeField]
        private bool loadFirstSceneOnStart = true;

        [SerializeField]
        private string firstSceneName = "Login";

        public bool IsInitializing { get; private set; }

        public bool IsInitialized { get; private set; }

        public string LastError { get; private set; }

        protected virtual IEnumerator Start()
        {
            IsInitializing = true;
            LastError = null;

            yield return InitializeGameAsync();
            if (!string.IsNullOrEmpty(LastError))
            {
                IsInitializing = false;
                yield break;
            }

            IsInitialized = true;
            IsInitializing = false;
            Debug.Log("[GameMain] 游戏基础初始化完成。", this);

            if (!loadFirstSceneOnStart)
            {
                yield break;
            }

            GameSceneManager manager =
                GameSceneManager.Instance;
            if (manager == null)
            {
                Fail("Start 场景中缺少 GameSceneManager。");
                yield break;
            }

            string targetScene = firstSceneName?.Trim();
            if (string.IsNullOrEmpty(targetScene))
            {
                Fail("首个业务场景名称不能为空。");
                yield break;
            }

            Debug.Log(
                $"[GameMain] 请求进入首个业务场景：{targetScene}",
                this);
            manager.LoadSceneAsync(targetScene);
        }

        /// <summary>
        /// Start 场景需要执行的基础初始化入口。
        /// 后续可在子类中接入配置、存档、网络等初始化流程。
        /// </summary>
        protected virtual IEnumerator InitializeGameAsync()
        {
            yield break;
        }

        private void Fail(string message)
        {
            LastError = message;
            IsInitialized = false;
            IsInitializing = false;
            Debug.LogError("[GameMain] " + message, this);
        }
    }
}
