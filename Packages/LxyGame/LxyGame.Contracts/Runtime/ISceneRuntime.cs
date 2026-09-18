using System;
using UnityEngine;

namespace Game.Contracts
{
    /// <summary>
    /// Stable scene-service contract implemented by Game.Logic after its
    /// HybridCLR assembly has been loaded.
    /// </summary>
    public interface ISceneRuntime
    {
        string ActiveSceneName { get; }
        string LastError { get; }

        event Action<string, float> SceneLoadProgressChanged;

        Coroutine LoadSceneAsync(string sceneName);
    }
}
