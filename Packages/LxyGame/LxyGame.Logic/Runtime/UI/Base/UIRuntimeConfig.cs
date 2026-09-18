namespace LxyDemo.UIFramework
{
    /// <summary>
    /// UI 运行时统一开关。该配置属于 Game.Logic 程序集，不显示在
    /// Inspector；修改后重新构建 Game.Logic.dll 即可随热更新发布。
    /// </summary>
    public static class UIRuntimeConfig
    {
        /// <summary>
        /// true：启动 C# UI 和 XLua；false：只启动 C# UI。
        /// </summary>
        public static bool EnableLuaRuntime { get; set; } = true;

        /// <summary>
        /// EnableLuaRuntime 为 true 时执行的 Lua 入口模块。
        /// </summary>
        public static string LuaMainModule { get; set; } = "Main";

        /// <summary>
        /// Lua 入口没有主动打开首界面时，由 C# UIManager 兜底打开登录面板。
        /// Lua 项目如果已经在 Main 中打开 UILogin，不会重复创建。
        /// </summary>
        public static bool OpenLoginPanelAfterStartup { get; set; } = true;

        /// <summary>
        /// 首个业务场景名称。
        /// </summary>
        public static string LoginSceneName { get; set; } = "Login";

        /// <summary>
        /// 登录面板的 UIManager 标识。
        /// </summary>
        public static string LoginPanelId { get; set; } = "UILogin";

        /// <summary>
        /// 登录面板的 YooAsset Location。
        /// </summary>
        public static string LoginPanelLocation { get; set; } =
            "Assets/GameResources/Prefabs/UIRes/Login/UILogin.prefab";
    }
}
