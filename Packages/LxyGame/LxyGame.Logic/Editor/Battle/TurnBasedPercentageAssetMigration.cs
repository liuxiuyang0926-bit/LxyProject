using System;
using System.IO;
using System.Text.RegularExpressions;
using Game.Battle.TurnBased.Authoring;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    /// <summary>在主线程完成旧资产转换，避免序列化回调与嵌套字段恢复顺序冲突。</summary>
    [InitializeOnLoad]
    public sealed class TurnBasedPercentageAssetMigration : AssetPostprocessor
    {
        private static bool scheduled;
        static TurnBasedPercentageAssetMigration() => Schedule();

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted,
            string[] moved, string[] movedFrom)
        {
            foreach (var path in imported)
                if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase)) { Schedule(); break; }
        }

        private static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += MigrateAll;
        }

        public static void MigrateAll()
        {
            scheduled = false;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { Schedule(); return; }
            int count = 0;
            foreach (var filter in new[] { "t:TurnBasedSkillLogicAsset", "t:TurnBasedBattleDefinition" })
                foreach (var guid in AssetDatabase.FindAssets(filter, new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string yaml = File.ReadAllText(path);
                    if (!yaml.StartsWith("%YAML", StringComparison.Ordinal))
                        throw new InvalidOperationException($"请先将战斗配置 {path} 序列化为文本，再进行百分比迁移。");
                    if (Regex.IsMatch(yaml, @"(?m)^  percentageVersion: [1-9]\d*\s*$")) continue;
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    // 以磁盘版本为准：旧文件缺失字段时，Unity 可能保留热重载对象的新字段默认值。
                    var serialized = new SerializedObject(asset);
                    serialized.FindProperty("percentageVersion").intValue = 0;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    bool changed = asset is TurnBasedSkillLogicAsset logic
                        ? logic.UpgradeLegacyPercentages()
                        : ((TurnBasedBattleDefinition)asset).UpgradeLegacyPercentages();
                    if (!changed) continue;
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                    count++;
                }
            if (count > 0) Debug.Log($"[TurnBasedBattle] 已将 {count} 个战斗配置迁移为实际百分数；旧技能数值保持不变。");
        }
    }
}
