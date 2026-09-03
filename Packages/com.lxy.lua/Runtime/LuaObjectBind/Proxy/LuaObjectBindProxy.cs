using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace LuaObjectBind
{
    public class LuaObjectBindProxy
    {
        private static LuaEnv luaEnv;

        /// <summary>
        /// 获取LuaEnv。
        /// </summary>
        public static LuaEnv GetLuaEnv()
        {
            return luaEnv;
        }

        /// <summary>
        /// 设置LuaEnv。
        /// </summary>
        public static void SetLuaEnv(LuaEnv configuredLuaEnv)
        {
            luaEnv = configuredLuaEnv;
        }

        /// <summary>
        /// 清空LuaEnv。
        /// </summary>
        public static void ClearLuaEnv(LuaEnv configuredLuaEnv)
        {
            if (ReferenceEquals(luaEnv, configuredLuaEnv))
            {
                luaEnv = null;
            }
        }

        /// <summary>
        /// 获取Lua路径。
        /// </summary>
        public static string GetLuaPath()
        {
            string path = Application.dataPath;
            path = path.Substring(0, path.LastIndexOf("/Assets"));
            path += "/Lua";
            return path;
        }

        /// <summary>
        /// 获取精灵图路径。
        /// </summary>
        public static string GetSpritePath(Sprite sprite)
        {
            return sprite.name;
        }

        /// <summary>
        /// 设置精灵图By路径。
        /// </summary>
        public static void SetSpriteByPath(Image image, string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            image.sprite = sprite;
        }

        /// <summary>
        /// 获取Lua对象绑定编辑器Proxy路径。
        /// </summary>
        public static string GetLuaObjectBindEditorProxyPath()
        {
            return System.IO.Path.GetFullPath(
                System.IO.Path.Combine(
                    Application.dataPath,
                    "../Packages/com.lxy.lua/Editor/" +
                    "LuaObjectBind/Editor/" +
                    "LuaObjectBindEditorProxy.cs"));
        }
    }
}
