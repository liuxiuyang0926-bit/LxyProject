using UnityEngine;
using UnityEngine.UI;
using XLua;

namespace LuaObjectBind
{
    public class LuaObjectBindProxy
    {
        private static LuaEnv luaEnv;

        public static LuaEnv GetLuaEnv()
        {
            return luaEnv;
        }

        public static void SetLuaEnv(LuaEnv configuredLuaEnv)
        {
            luaEnv = configuredLuaEnv;
        }

        public static void ClearLuaEnv(LuaEnv configuredLuaEnv)
        {
            if (ReferenceEquals(luaEnv, configuredLuaEnv))
            {
                luaEnv = null;
            }
        }

        public static string GetLuaPath()
        {
            string path = Application.dataPath;
            path = path.Substring(0, path.LastIndexOf("/Assets"));
            path += "/Lua";
            return path;
        }

        public static string GetSpritePath(Sprite sprite)
        {
            return sprite.name;
        }

        public static void SetSpriteByPath(Image image, string path)
        {
            Sprite sprite = Resources.Load<Sprite>(path);
            image.sprite = sprite;
        }

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
