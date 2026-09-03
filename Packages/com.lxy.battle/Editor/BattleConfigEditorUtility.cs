using System.IO;
using Game.Battle.Config;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    internal static class BattleConfigEditorUtility
    {
        /// <summary>
        /// 公开的数据库资源路径数据。
        /// </summary>
        public const string DatabaseAssetPath =
            "Assets/GameResources/Battle/Config/" +
            "BattleConfigDatabase.asset";

        /// <summary>
        /// 加载Or创建数据库。
        /// </summary>
        public static BattleConfigDatabase LoadOrCreateDatabase()
        {
            BattleConfigDatabase database =
                AssetDatabase.LoadAssetAtPath<BattleConfigDatabase>(
                    DatabaseAssetPath);
            if (database != null)
            {
                return database;
            }

            EnsureAssetFolder(
                Path.GetDirectoryName(DatabaseAssetPath)
                    ?.Replace('\\', '/'));
            database = ScriptableObject.CreateInstance<
                BattleConfigDatabase>();
            database.ResetToDefaults();
            AssetDatabase.CreateAsset(database, DatabaseAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[Battle] 已创建默认战斗配置：" +
                DatabaseAssetPath,
                database);
            return database;
        }

        /// <summary>
        /// 执行保存相关逻辑。
        /// </summary>
        public static void Save(Object target)
        {
            if (target == null)
            {
                return;
            }

            EditorUtility.SetDirty(target);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 确保资源目录。
        /// </summary>
        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) ||
                AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent =
                Path.GetDirectoryName(folder)?.Replace('\\', '/');
            EnsureAssetFolder(parent);
            string name = Path.GetFileName(folder);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
