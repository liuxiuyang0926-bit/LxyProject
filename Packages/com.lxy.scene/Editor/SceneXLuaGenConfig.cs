using System;
using System.Collections.Generic;
using XLua;

namespace LxyDemo.SceneManagement
{
    /// <summary>
    /// Lua 场景系统所需的 xLua 类型与委托桥配置。
    /// 生成 xLua 代码后同样可用于 IL2CPP 真机环境。
    /// </summary>
    public static class SceneXLuaGenConfig
    {
        [LuaCallCSharp]
        /// <summary>
        /// 公开的LuaCallCSharp数据。
        /// </summary>
        public static readonly List<Type> LuaCallCSharp =
            new List<Type>
            {
                typeof(GameSceneManager)
            };

        [CSharpCallLua]
        /// <summary>
        /// 公开的CSharpCallLua数据。
        /// </summary>
        public static readonly List<Type> CSharpCallLua =
            new List<Type>
            {
                typeof(Action<float>),
                typeof(Action<bool, string>)
            };
    }
}
