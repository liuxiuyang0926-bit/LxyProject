using System;
using Game.Battle.TurnBased.Authoring;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public static class TurnBasedSkillAssetGenerator
    {
        public const string Root =
            "Assets/GameResources/Battle/TurnBased/Skills";
        public const string DefinitionPath =
            "Assets/GameResources/Battle/TurnBased/DefaultTurnBasedBattle.asset";
        public const string NormalLogicPath = Root + "/Logic/skill_logic_1001.asset";
        public const string HeavyLogicPath = Root + "/Logic/skill_logic_1002.asset";
        public const string AchievementLogicPath = Root +
            "/Logic/skill_logic_ach_tiaozhan_level_jinnang_3.asset";
        public const string Expression101011Path = Root +
            "/Expression/skill_101011.asset";
        public const string NormalSkillPath = Root +
            "/Definitions/skill_1001.asset";
        public const string HeavySkillPath = Root +
            "/Definitions/skill_1002.asset";
        public const string AchievementSkillPath = Root +
            "/Definitions/skill_ach_tiaozhan_level_jinnang_3.asset";

        [MenuItem("工具/战斗/生成参考技能逻辑与表现资产", false, 3)]
        public static void GenerateReferenceAssetsFromMenu()
        {
            TurnBasedSkillAsset[] skills = EnsureReferenceAssets(false);
            TurnBasedBattleDefinition definition =
                AssetDatabase.LoadAssetAtPath<TurnBasedBattleDefinition>(
                    DefinitionPath);
            if (definition != null)
            {
                definition.SetGeneratedSkillAssets(skills);
                EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = skills[0];
            EditorGUIUtility.PingObject(skills[0]);
            Debug.Log(
                definition == null
                    ? "[TurnBasedBattle] 已生成参考逻辑/表现 ScriptableObject。"
                    : "[TurnBasedBattle] 已生成并挂接参考逻辑/表现 ScriptableObject。",
                definition != null ? definition : skills[0]);
        }

        public static TurnBasedSkillAsset[] EnsureReferenceAssets(bool resetExisting)
        {
            EnsureFolder(Root + "/Logic");
            EnsureFolder(Root + "/Expression");
            EnsureFolder(Root + "/Definitions");

            TurnBasedSkillLogicAsset normalLogic = LoadOrCreate<TurnBasedSkillLogicAsset>(
                NormalLogicPath,
                resetExisting,
                asset => asset.ResetToDamageSample("skill_logic_1001", 1, 10000, 0));
            TurnBasedSkillLogicAsset heavyLogic = LoadOrCreate<TurnBasedSkillLogicAsset>(
                HeavyLogicPath,
                resetExisting,
                asset => asset.ResetToDamageSample("skill_logic_1002", 2, 13500, 10));
            TurnBasedSkillLogicAsset achievementLogic = LoadOrCreate<TurnBasedSkillLogicAsset>(
                AchievementLogicPath,
                resetExisting,
                asset => asset.ResetToJinnangAchievementSample());
            TurnBasedSkillExpressionAsset expression = LoadOrCreate<TurnBasedSkillExpressionAsset>(
                Expression101011Path,
                resetExisting,
                asset => asset.ResetToSkill101011Sample(normalLogic));

            TurnBasedSkillAsset normal = LoadOrCreate<TurnBasedSkillAsset>(
                NormalSkillPath,
                resetExisting,
                asset => asset.Configure(
                    1001,
                    "普通攻击",
                    BattleSkillTargetMode.SingleEnemy,
                    normalLogic,
                    expression,
                    true,
                    "attack_1",
                    0.65f,
                    new Color(0.16f, 0.48f, 0.76f, 1f)));
            TurnBasedSkillAsset heavy = LoadOrCreate<TurnBasedSkillAsset>(
                HeavySkillPath,
                resetExisting,
                asset => asset.Configure(
                    1002,
                    "青龙斩",
                    BattleSkillTargetMode.SingleEnemy,
                    heavyLogic,
                    expression,
                    true,
                    "skill_1",
                    0.9f,
                    new Color(0.72f, 0.42f, 0.12f, 1f)));
            TurnBasedSkillAsset achievement = LoadOrCreate<TurnBasedSkillAsset>(
                AchievementSkillPath,
                resetExisting,
                asset => asset.Configure(
                    190003,
                    "锦囊挑战：一轮内击杀全部孙尚香",
                    BattleSkillTargetMode.Self,
                    achievementLogic,
                    null,
                    false,
                    string.Empty,
                    0.05f,
                    new Color(0.35f, 0.35f, 0.35f, 1f)));

            AssetDatabase.SaveAssets();
            return new[] { normal, heavy, achievement };
        }

        private static T LoadOrCreate<T>(
            string path,
            bool resetExisting,
            Action<T> initialize)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = asset == null;
            if (created)
            {
                asset = ScriptableObject.CreateInstance<T>();
                initialize(asset);
                AssetDatabase.CreateAsset(asset, path);
            }

            if (resetExisting && !created)
            {
                initialize(asset);
            }
            if (created || resetExisting)
            {
                EditorUtility.SetDirty(asset);
            }
            return asset;
        }

        private static void EnsureFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }
    }
}
