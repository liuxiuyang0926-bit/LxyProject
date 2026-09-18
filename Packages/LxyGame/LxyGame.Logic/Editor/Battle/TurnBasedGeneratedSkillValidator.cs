using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Application;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.Core.Commands;
using Game.Battle.TurnBased.Core.Session;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public static class TurnBasedGeneratedSkillValidator
    {
        private static readonly BattleVariableKey AchievementComplete =
            new BattleVariableKey(9030302);

        [MenuItem("工具/战斗/运行技能资产闭环自检", false, 5)]
        public static void Validate()
        {
            TurnBasedBattleDefinition definition =
                AssetDatabase.LoadAssetAtPath<TurnBasedBattleDefinition>(
                    TurnBasedSkillAssetGenerator.DefinitionPath);
            if (definition == null)
            {
                throw new InvalidOperationException(
                    "缺少默认回合制战斗定义：" +
                    TurnBasedSkillAssetGenerator.DefinitionPath);
            }
            TurnBasedBattleRuntimeBuild build = definition.Compile();
            ValidateOperationCatalog();
            ValidateExpression(build.Database);
            ValidateJinnangAchievement(build.Database, definition.RandomSeed);
            Debug.Log(
                "[TurnBasedBattle] 技能资产闭环自检通过：SO 编译、领域事件、" +
                "锦囊同轮击杀和表现时间轴均正常。",
                definition);
        }

        private static void ValidateOperationCatalog()
        {
            Array values = Enum.GetValues(typeof(BattleExpressionClipType));
            IReadOnlyList<TurnBasedExpressionOperationDescriptor> descriptors =
                TurnBasedExpressionOperationCatalog.All;
            if (descriptors.Count != values.Length)
            {
                throw new InvalidOperationException(
                    $"表现操作下拉目录不完整：枚举 {values.Length} 项，目录 " +
                    $"{descriptors.Count} 项。");
            }

            var unique = new HashSet<BattleExpressionClipType>();
            for (int index = 0; index < descriptors.Count; index++)
            {
                TurnBasedExpressionOperationDescriptor descriptor = descriptors[index];
                if (!unique.Add(descriptor.Type) ||
                    string.IsNullOrWhiteSpace(descriptor.MenuPath) ||
                    string.IsNullOrWhiteSpace(descriptor.DisplayName))
                {
                    throw new InvalidOperationException(
                        $"表现操作下拉目录第 {index + 1} 项无效或重复。");
                }
            }
            foreach (BattleExpressionClipType value in values)
            {
                if (!unique.Contains(value))
                {
                    throw new InvalidOperationException(
                        $"表现操作 {value} 没有配置编辑器下拉项。");
                }
            }
        }

        private static void ValidateExpression(CompiledBattleDatabase database)
        {
            if (!database.TryGetSkill(new SkillId(1001), out CompiledSkill skill) ||
                !database.TryGetExpression(
                    skill.ExpressionKey,
                    out CompiledBattleExpression expression) ||
                expression.Id != "skill_101011" ||
                expression.FramesPerSecond != 30 ||
                expression.Clips.Length == 0)
            {
                throw new InvalidOperationException(
                    "skill_101011 没有正确编译并绑定到普通攻击。");
            }
        }

        private static void ValidateJinnangAchievement(
            CompiledBattleDatabase database,
            int seed)
        {
            BattleUnit firstAttacker = CreateUnit(
                1,
                101,
                BattleCamp.Attacker,
                0,
                1000,
                100,
                200);
            firstAttacker.Skills.Add(new SkillInstance(
                new SkillId(1001),
                firstAttacker.Id));
            firstAttacker.Skills.Add(new SkillInstance(
                new SkillId(190003),
                firstAttacker.Id));

            BattleUnit secondAttacker = CreateUnit(
                2,
                102,
                BattleCamp.Attacker,
                1,
                1000,
                100,
                190);
            secondAttacker.Skills.Add(new SkillInstance(
                new SkillId(1001),
                secondAttacker.Id));

            BattleUnit firstTarget = CreateUnit(
                3,
                308,
                BattleCamp.Defender,
                2,
                10,
                1,
                80);
            firstTarget.Skills.Add(new SkillInstance(
                new SkillId(1001),
                firstTarget.Id));

            BattleUnit secondTarget = CreateUnit(
                4,
                308,
                BattleCamp.Defender,
                3,
                10,
                1,
                70);
            secondTarget.Skills.Add(new SkillInstance(
                new SkillId(1001),
                secondTarget.Id));

            using (BattleSession session = new BattleApplication(database).CreateSession(
                       new[]
                       {
                           firstAttacker,
                           secondAttacker,
                           firstTarget,
                           secondTarget,
                       },
                       seed))
            {
                RequireSuccess(session.Step(new StartBattleCommand(1)));
                RequireSuccess(session.Step(new UseSkillCommand(
                    2,
                    firstAttacker.Id,
                    new SkillId(1001),
                    new[] { firstTarget.Id })));
                RequireSuccess(session.Step(new EndTurnCommand(
                    3,
                    firstAttacker.Id)));
                if (session.Context.Flow.CurrentActor != secondAttacker.Id)
                {
                    throw new InvalidOperationException(
                        "锦囊自检的第二名攻击者没有在同一轮获得行动权。");
                }

                RequireSuccess(session.Step(new UseSkillCommand(
                    4,
                    secondAttacker.Id,
                    new SkillId(1001),
                    new[] { secondTarget.Id })));
                if (session.Context.World.Counters.Get(AchievementComplete) != 1)
                {
                    throw new InvalidOperationException(
                        "同一轮击杀全部 ConfigId=308 后，锦囊挑战没有完成。");
                }
            }
        }

        private static BattleUnit CreateUnit(
            int id,
            int configId,
            BattleCamp camp,
            int position,
            long hp,
            long attack,
            long speed)
        {
            var unit = new BattleUnit(new UnitId(id), configId, camp, position);
            unit.InitializeHealth(hp);
            unit.InitializeAttribute(AttributeType.Attack, attack);
            unit.InitializeAttribute(AttributeType.Defense, 0);
            unit.InitializeAttribute(AttributeType.Speed, speed);
            unit.InitializeAttribute(AttributeType.Energy, 100);
            return unit;
        }

        private static void RequireSuccess(BattleStepResult result)
        {
            if (!result.Success)
            {
                throw new InvalidOperationException(
                    $"战斗步骤失败：{result.AbortReason} / {result.Error}");
            }
        }
    }
}
