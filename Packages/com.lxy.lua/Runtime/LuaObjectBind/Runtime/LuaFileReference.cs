using System.IO;
using UnityEngine;

namespace LuaObjectBind
{
    [System.Serializable]
    public class LuaFileReference
    {
        // 实际存储的是UI/XX/YY.lua
        [SerializeField]
        protected string filePath;

        public string FilePath
        {
            get { return filePath; }
            set
            {
                filePath = value;
            }
        }

        // Require Class Name UI.XX.YY
        public string ClassName
        {
            get
            {
                if (string.IsNullOrEmpty(filePath))
                    return string.Empty;
                string className = filePath.Replace("Lua/", "");
                className = className.Replace("\\", ".");
                className = className.Replace("/", ".");
                className = className.Replace("_Auto", "");
                className = className.Replace(".lua", "");
                return className;
            }
        }
    }
}