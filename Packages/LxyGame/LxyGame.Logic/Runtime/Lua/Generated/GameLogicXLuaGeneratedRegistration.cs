using LxyDemo.SceneManagement;
using XLua;
using XLua.CSObjectWrap;

namespace LxyDemo.UIFramework
{
    /// <summary>
    /// Registers Game.Logic-owned xLua wrappers before the first LuaEnv is
    /// created. This keeps XLua.Runtime independent from hot-update logic.
    /// </summary>
    internal static class GameLogicXLuaGeneratedRegistration
    {
        private static bool isRegistered;

        public static void EnsureRegistered()
        {
            if (isRegistered)
            {
                return;
            }

            LuaEnv.AddIniter(Initialize);
            isRegistered = true;
        }

        private static void Initialize(
            LuaEnv luaEnv,
            ObjectTranslator translator)
        {
            translator.DelayWrapLoader(
                typeof(GameSceneManager),
                LxyDemoSceneManagementGameSceneManagerWrap.__Register);
        }
    }
}
