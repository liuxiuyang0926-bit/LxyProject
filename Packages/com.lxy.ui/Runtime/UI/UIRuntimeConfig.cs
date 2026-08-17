namespace LxyDemo.UIFramework
{
    /// <summary>
    /// UI 运行时统一开关。该配置属于 Game.UI 程序集，不显示在
    /// Inspector；修改后重新构建 Game.UI.dll 即可随热更新发布。
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
    }
}
