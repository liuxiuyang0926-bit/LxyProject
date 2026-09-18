using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Battle.TurnBased.Authoring
{
    [CreateAssetMenu(
        fileName = "TurnBasedSkillLogic",
        menuName = "LxyDemo/战斗/回合制技能逻辑")]
    public sealed partial class TurnBasedSkillLogicAsset
    {
        [SerializeField, TitleGroup("基础信息"), LabelText("逻辑名称"), Required]
        private string logicId = "skill_logic_new";
        [SerializeField, TitleGroup("基础信息"), LabelText("说明"), TextArea(2, 5)]
        private string description;
        [SerializeField, TitleGroup("表现数据契约"), LabelText("输出列表"),
         ListDrawerSettings(
             ListElementLabelName = "key",
             DraggableItems = true,
             ShowItemCount = true,
             AlwaysAddDefaultValue = true)]
        private List<BattleLogicOutputAuthoring> outputs =
            new List<BattleLogicOutputAuthoring>();
        [SerializeField, TitleGroup("临时数据与参数"), LabelText("数据定义"),
         ListDrawerSettings(
             ListElementLabelName = "name",
             DraggableItems = true,
             ShowItemCount = true,
             AlwaysAddDefaultValue = true)]
        private List<BattleLogicDataAuthoring> dataDefinitions =
            new List<BattleLogicDataAuthoring>();
        [SerializeField, TitleGroup("逻辑轨道"), LabelText("轨道列表"),
         ListDrawerSettings(
             ListElementLabelName = "trackName",
             DraggableItems = true,
             ShowItemCount = true,
             AlwaysAddDefaultValue = true)]
        private List<BattleLogicTrackAuthoring> tracks =
            new List<BattleLogicTrackAuthoring>();
        [SerializeField, HideInInspector] private List<BattleRuleAuthoring> rules =
            new List<BattleRuleAuthoring>();

        public string LogicId => logicId;
        public string Description => description;
        public IReadOnlyList<BattleLogicOutputAuthoring> Outputs => outputs;
        public IReadOnlyList<BattleLogicDataAuthoring> DataDefinitions =>
            dataDefinitions ?? (dataDefinitions = new List<BattleLogicDataAuthoring>());
        public IReadOnlyList<BattleLogicTrackAuthoring> Tracks => tracks;
        public IReadOnlyList<BattleRuleAuthoring> Rules => GetAuthoringRules();
        public bool UsesLegacyLayout => (tracks == null || tracks.Count == 0) &&
            rules != null && rules.Count > 0;

        public void AddDataDefinition(BattleLogicDataAuthoring definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            dataDefinitions = dataDefinitions ?? new List<BattleLogicDataAuthoring>();
            dataDefinitions.Add(definition);
        }

        public void UpgradeLegacyLayout()
        {
            if (!UsesLegacyLayout)
            {
                return;
            }
            tracks = new List<BattleLogicTrackAuthoring>
            {
                new BattleLogicTrackAuthoring
                {
                    trackName = "迁移主逻辑轨",
                    executionGroup = "主流程",
                    rules = new List<BattleRuleAuthoring>(rules),
                },
            };
            rules.Clear();
        }
        public CompiledRule[] Compile()
        {
            ValidateOrThrow();
            List<BattleRuleAuthoring> sourceRules = GetAuthoringRules();
            var result = new CompiledRule[sourceRules.Count];
            for (int index = 0; index < sourceRules.Count; index++)
            {
                result[index] = sourceRules[index].Compile();
            }
            return result;
        }

        public CompiledBattleLogicData[] CompileDataDefinitions()
        {
            dataDefinitions = dataDefinitions ?? new List<BattleLogicDataAuthoring>();
            var result = new CompiledBattleLogicData[dataDefinitions.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = dataDefinitions[index].Compile();
            }
            Array.Sort(result, (left, right) => left.Key.CompareTo(right.Key));
            return result;
        }

        public bool TryValidate(out string error)
        {
            try
            {
                ValidateOrThrow();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void ResetToDamageSample(
            string id,
            int ruleId,
            int scaleBasisPoint,
            long offset)
        {
            logicId = id;
            description = "编辑器生成的主动技能伤害规则。";
            outputs = new List<BattleLogicOutputAuthoring>
            {
                new BattleLogicOutputAuthoring(),
            };
            dataDefinitions = new List<BattleLogicDataAuthoring>
            {
                new BattleLogicDataAuthoring
                {
                    name = "damage_rate",
                    key = 1001,
                    kind = BattleLogicDataKind.Parameter,
                    valueType = BattleLogicDataValueType.BasisPoint,
                    defaultValue = scaleBasisPoint,
                    description = "基础伤害倍率，10000 表示 100%。",
                },
                new BattleLogicDataAuthoring
                {
                    name = "cast_count",
                    key = 2001,
                    kind = BattleLogicDataKind.RuntimeVariable,
                    valueType = BattleLogicDataValueType.Integer,
                    scope = BattleLogicVariableScope.Skill,
                    description = "此技能在本场战斗中的释放次数。",
                },
                new BattleLogicDataAuthoring
                {
                    name = "temp_value",
                    key = 3001,
                    kind = BattleLogicDataKind.RuntimeVariable,
                    valueType = BattleLogicDataValueType.Integer,
                    scope = BattleLogicVariableScope.Invocation,
                    description = "当前规则触发期间的临时计算值。",
                },
            };
            tracks = new List<BattleLogicTrackAuthoring>
            {
                new BattleLogicTrackAuthoring
                {
                    trackName = "伤害结算",
                    executionGroup = "技能主流程",
                    rules = new List<BattleRuleAuthoring>
                    {
                        new BattleRuleAuthoring
                        {
                            operationName = "技能命中时结算伤害",
                            id = ruleId,
                            trigger = BattleEventType.SkillCast,
                            phase = BattleEventPhase.Main,
                            ownerMustBeAlive = true,
                            actions = new List<BattleActionAuthoring>
                            {
                                new BattleActionAuthoring
                                {
                                    operationName = "计算本次技能伤害",
                                    type = CompiledActionType.SetInvocationVariable,
                                    referenceId = 3001,
                                    value = new BattleValueAuthoring
                                    {
                                        source = ValueSourceType.OwnerAttribute,
                                        attribute = AttributeType.Attack,
                                        useDynamicScale = true,
                                        dynamicScale = new BattleValueOperandAuthoring
                                        {
                                            source = ValueSourceType.LogicParameter,
                                            referenceId = 1001,
                                        },
                                    },
                                },
                                new BattleActionAuthoring
                                {
                                    operationName = "对选中目标造成伤害",
                                    type = CompiledActionType.Damage,
                                    target = new BattleTargetAuthoring
                                    {
                                        type = TargetSelectorType.SelectedTargets,
                                        count = 0,
                                    },
                                    value = new BattleValueAuthoring
                                    {
                                        source = ValueSourceType.InvocationVariable,
                                        referenceId = 3001,
                                        scaleBasisPoint = BattleNumeric.BasisPointOne,
                                        offset = offset,
                                        hasMinimum = true,
                                        minimum = 1,
                                    },
                                    flags = CompiledActionFlags.CanCritical |
                                        CompiledActionFlags.CanMiss,
                                },
                                new BattleActionAuthoring
                                {
                                    operationName = "累计技能释放次数",
                                    type = CompiledActionType.AddSkillVariable,
                                    referenceId = 2001,
                                    value = new BattleValueAuthoring
                                    {
                                        source = ValueSourceType.Constant,
                                        constant = 1,
                                    },
                                },
                            },
                        },
                    },
                },
            };
            rules = new List<BattleRuleAuthoring>();
        }

        public void ResetToJinnangAchievementSample()
        {
            const int rememberRoundRuleId = 303001;
            const int completeRuleId = 303002;
            const int targetConfigId = 308;
            const int rememberedRoundVariable = 9030301;
            const int completionVariable = 9030302;

            logicId = "skill_logic_ach_tiaozhan_level_jinnang_3";
            description =
                "来自参考 Lua：同一轮内击杀全部孙尚香（ConfigId=308）。" +
                "第一个目标死亡时记录轮次，最后一个目标死亡且轮次相同时完成。";
            outputs = new List<BattleLogicOutputAuthoring>
            {
                new BattleLogicOutputAuthoring
                {
                    key = "achievement_complete",
                    eventType = BattleEventType.UnitDead,
                    description = "同一轮内击杀全部目标后的完成状态。",
                },
            };
            dataDefinitions = new List<BattleLogicDataAuthoring>();

            BattleTargetAuthoring deadEventTarget = new BattleTargetAuthoring
            {
                type = TargetSelectorType.EventTarget,
                count = 1,
                includeDead = true,
            };
            BattleTargetAuthoring aliveSunShangXiang = new BattleTargetAuthoring
            {
                type = TargetSelectorType.AllEnemies,
                count = 0,
                includeDead = false,
                configIdFilter = targetConfigId,
            };

            tracks = new List<BattleLogicTrackAuthoring>
            {
                new BattleLogicTrackAuthoring
                {
                    trackName = "击杀轮次记录",
                    executionGroup = "死亡事件并行检查",
                    executionMode = BattleAuthoringExecutionMode.Parallel,
                    rules = new List<BattleRuleAuthoring>
                    {
                        new BattleRuleAuthoring
                        {
                            operationName = "记录首次目标死亡轮次",
                            executionGroup = "死亡事件并行检查",
                            executionMode = BattleAuthoringExecutionMode.Parallel,
                            id = rememberRoundRuleId,
                            trigger = BattleEventType.UnitDead,
                            phase = BattleEventPhase.After,
                            priority = 0,
                            ownerMustBeAlive = false,
                            conditions = new List<BattleConditionAuthoring>
                            {
                                Condition(CompiledConditionType.IsEnemy, deadEventTarget),
                                Condition(CompiledConditionType.IsDead, deadEventTarget),
                                Condition(
                                    CompiledConditionType.TargetCountCompare,
                                    aliveSunShangXiang,
                                    ComparisonOperator.Greater,
                                    0),
                                CounterCondition(rememberedRoundVariable, ComparisonOperator.Equal, 0),
                            },
                            actions = new List<BattleActionAuthoring>
                            {
                                SetVariable(rememberedRoundVariable, ValueSourceType.CurrentRound),
                            },
                        },
                    },
                },
                new BattleLogicTrackAuthoring
                {
                    trackName = "完成条件检查",
                    executionGroup = "死亡事件并行检查",
                    executionMode = BattleAuthoringExecutionMode.Parallel,
                    rules = new List<BattleRuleAuthoring>(),
                },
            };
            tracks[1].rules = new List<BattleRuleAuthoring>
            {
                new BattleRuleAuthoring
                {
                    operationName = "最后一个目标死亡时完成挑战",
                    executionGroup = "死亡事件并行检查",
                    executionMode = BattleAuthoringExecutionMode.Parallel,
                    id = completeRuleId,
                    trigger = BattleEventType.UnitDead,
                    phase = BattleEventPhase.After,
                    priority = 10,
                    ownerMustBeAlive = false,
                    conditions = new List<BattleConditionAuthoring>
                    {
                        Condition(CompiledConditionType.IsEnemy, deadEventTarget),
                        Condition(CompiledConditionType.IsDead, deadEventTarget),
                        Condition(
                            CompiledConditionType.TargetCountCompare,
                            aliveSunShangXiang,
                            ComparisonOperator.Equal,
                            0),
                        CounterCondition(rememberedRoundVariable, ComparisonOperator.Greater, 0),
                        new BattleConditionAuthoring
                        {
                            type = CompiledConditionType.CounterCompareCurrentRound,
                            target = new BattleTargetAuthoring
                            {
                                type = TargetSelectorType.Self,
                            },
                            comparison = ComparisonOperator.Equal,
                            referenceId = rememberedRoundVariable,
                        },
                    },
                    actions = new List<BattleActionAuthoring>
                    {
                        SetVariable(completionVariable, ValueSourceType.Constant, 1),
                    },
                },
            };
            rules = new List<BattleRuleAuthoring>();
        }

        private static BattleConditionAuthoring Condition(
            CompiledConditionType type,
            BattleTargetAuthoring target,
            ComparisonOperator comparison = ComparisonOperator.Equal,
            long value = 0)
        {
            return new BattleConditionAuthoring
            {
                type = type,
                target = CloneTarget(target),
                comparison = comparison,
                value = value,
            };
        }

        private static BattleConditionAuthoring CounterCondition(
            int variable,
            ComparisonOperator comparison,
            long value)
        {
            return new BattleConditionAuthoring
            {
                type = CompiledConditionType.CounterCompare,
                target = new BattleTargetAuthoring
                {
                    type = TargetSelectorType.Self,
                },
                comparison = comparison,
                value = value,
                referenceId = variable,
            };
        }

        private static BattleActionAuthoring SetVariable(
            int variable,
            ValueSourceType source,
            long constant = 0)
        {
            return new BattleActionAuthoring
            {
                type = CompiledActionType.SetVariable,
                target = new BattleTargetAuthoring
                {
                    type = TargetSelectorType.Self,
                },
                value = new BattleValueAuthoring
                {
                    source = source,
                    constant = constant,
                },
                referenceId = variable,
            };
        }

        private static BattleTargetAuthoring CloneTarget(BattleTargetAuthoring source)
        {
            return new BattleTargetAuthoring
            {
                type = source.type,
                count = source.count,
                includeDead = source.includeDead,
                configIdFilter = source.configIdFilter,
            };
        }

        private void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(logicId))
            {
                throw new InvalidOperationException("技能逻辑 ID 不能为空。");
            }

            dataDefinitions = dataDefinitions ?? new List<BattleLogicDataAuthoring>();
            var logicDataByKey = new Dictionary<int, BattleLogicDataAuthoring>();
            var logicDataNames = new HashSet<string>(StringComparer.Ordinal);
            for (int dataIndex = 0; dataIndex < dataDefinitions.Count; dataIndex++)
            {
                BattleLogicDataAuthoring data = dataDefinitions[dataIndex] ??
                    throw new InvalidOperationException(
                        $"技能逻辑 {logicId} 第 {dataIndex + 1} 个数据定义为空。");
                string dataName = data.name?.Trim();
                if (data.key <= 0 || string.IsNullOrWhiteSpace(dataName) ||
                    logicDataByKey.ContainsKey(data.key) ||
                    !logicDataNames.Add(dataName))
                {
                    throw new InvalidOperationException(
                        $"技能逻辑 {logicId} 的数据 Key/名称为空或重复：{data.key}/{dataName}");
                }

                logicDataByKey.Add(data.key, data);

                if (data.valueType == BattleLogicDataValueType.Boolean &&
                    data.defaultValue != 0 && data.defaultValue != 1)
                {
                    throw new InvalidOperationException(
                        $"逻辑数据 {dataName} 是布尔值，默认值只能是 0 或 1。");
                }
            }

            List<BattleRuleAuthoring> sourceRules = GetAuthoringRules();
            if (sourceRules.Count == 0)
            {
                throw new InvalidOperationException($"技能逻辑 {logicId} 至少需要一条规则。");
            }

            var ids = new HashSet<int>();
            for (int index = 0; index < sourceRules.Count; index++)
            {
                BattleRuleAuthoring rule = sourceRules[index] ??
                    throw new InvalidOperationException($"技能逻辑 {logicId} 第 {index} 条规则为空。");
                if (rule.id <= 0 || !ids.Add(rule.id) ||
                    rule.trigger == BattleEventType.None)
                {
                    throw new InvalidOperationException($"技能逻辑 {logicId} 的规则 ID 无效或重复：{rule.id}");
                }

                if (rule.actions == null || rule.actions.Count == 0)
                {
                    throw new InvalidOperationException($"规则 {rule.id} 至少需要一个 Action。");
                }

                for (int actionIndex = 0;
                     actionIndex < rule.actions.Count;
                     actionIndex++)
                {
                    BattleActionAuthoring action = rule.actions[actionIndex] ??
                        throw new InvalidOperationException(
                            $"规则 {rule.id} 的 Action {actionIndex} 为空。");
                    if (action.target != null &&
                        (action.target.count < 0 || action.target.configIdFilter < 0))
                    {
                        throw new InvalidOperationException(
                            $"规则 {rule.id} 的 Action {actionIndex} 目标选择器无效。");
                    }
                    if (action.value != null &&
                        (action.value.scaleBasisPoint < 0 ||
                         action.value.useDynamicScale && action.value.dynamicScale == null ||
                         action.value.useDynamicScale &&
                         action.value.dynamicScale.source == ValueSourceType.Constant &&
                         action.value.dynamicScale.constant < 0 ||
                         action.value.hasMinimum && action.value.hasMaximum &&
                         action.value.minimum > action.value.maximum))
                    {
                        throw new InvalidOperationException(
                            $"规则 {rule.id} 的 Action {actionIndex} 数值表达式无效。");
                    }
                    ValidateValueReference(
                        action.value,
                        logicDataByKey,
                        $"规则 {rule.id} 的 Action {actionIndex}");
                    ValidateVariableActionReference(
                        action,
                        logicDataByKey,
                        $"规则 {rule.id} 的 Action {actionIndex}");
                    ValidateActionReference(
                        action,
                        logicDataByKey,
                        $"规则 {rule.id} 的 Action {actionIndex}");
                }

                for (int conditionIndex = 0;
                     conditionIndex < (rule.conditions?.Count ?? 0);
                     conditionIndex++)
                {
                    BattleConditionAuthoring condition =
                        rule.conditions[conditionIndex] ??
                        throw new InvalidOperationException(
                            $"规则 {rule.id} 的 Condition {conditionIndex} 为空。");
                    if (condition.target != null &&
                        (condition.target.count < 0 ||
                         condition.target.configIdFilter < 0))
                    {
                        throw new InvalidOperationException(
                            $"规则 {rule.id} 的 Condition {conditionIndex} 目标选择器无效。");
                    }
                    if (condition.type == CompiledConditionType.LogicValueCompare)
                    {
                        ValidateValueShape(
                            condition.leftValue,
                            $"规则 {rule.id} 的 Condition {conditionIndex} 左值");
                        ValidateValueShape(
                            condition.rightValue,
                            $"规则 {rule.id} 的 Condition {conditionIndex} 右值");
                        ValidateValueReference(
                            condition.leftValue,
                            logicDataByKey,
                            $"规则 {rule.id} 的 Condition {conditionIndex} 左值");
                        ValidateValueReference(
                            condition.rightValue,
                            logicDataByKey,
                            $"规则 {rule.id} 的 Condition {conditionIndex} 右值");
                    }
                }
            }

            outputs = outputs ?? new List<BattleLogicOutputAuthoring>();
            var outputKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < outputs.Count; index++)
            {
                BattleLogicOutputAuthoring output = outputs[index];
                if (output == null || string.IsNullOrWhiteSpace(output.key) ||
                    !outputKeys.Add(output.key))
                {
                    throw new InvalidOperationException(
                        $"技能逻辑 {logicId} 的表现数据键为空或重复。");
                }
            }
        }

        private static void ValidateValueShape(BattleValueAuthoring value, string owner)
        {
            if (value == null || value.scaleBasisPoint < 0 ||
                value.useDynamicScale && value.dynamicScale == null ||
                value.useDynamicScale &&
                value.dynamicScale.source == ValueSourceType.Constant &&
                value.dynamicScale.constant < 0 ||
                value.hasMinimum && value.hasMaximum && value.minimum > value.maximum)
            {
                throw new InvalidOperationException($"{owner} 的数值表达式无效。");
            }
        }

        private static void ValidateValueReference(
            BattleValueAuthoring value,
            IReadOnlyDictionary<int, BattleLogicDataAuthoring> definitions,
            string owner)
        {
            if (value == null)
            {
                return;
            }

            if (value.useDynamicScale)
            {
                ValidateOperandReference(
                    value.dynamicScale,
                    definitions,
                    owner + " 动态倍率");
            }

            BattleLogicDataKind kind;
            BattleLogicVariableScope? scope;
            switch (value.source)
            {
                case ValueSourceType.LogicParameter:
                    kind = BattleLogicDataKind.Parameter;
                    scope = null;
                    break;
                case ValueSourceType.InvocationVariable:
                    kind = BattleLogicDataKind.RuntimeVariable;
                    scope = BattleLogicVariableScope.Invocation;
                    break;
                case ValueSourceType.SkillVariable:
                    kind = BattleLogicDataKind.RuntimeVariable;
                    scope = BattleLogicVariableScope.Skill;
                    break;
                default:
                    return;
            }

            ValidateLogicDataReference(value.referenceId, kind, scope, definitions, owner);
        }

        private static void ValidateOperandReference(
            BattleValueOperandAuthoring value,
            IReadOnlyDictionary<int, BattleLogicDataAuthoring> definitions,
            string owner)
        {
            if (value == null)
            {
                throw new InvalidOperationException($"{owner} 不能为空。");
            }

            BattleLogicDataKind kind;
            BattleLogicVariableScope? scope;
            switch (value.source)
            {
                case ValueSourceType.LogicParameter:
                    kind = BattleLogicDataKind.Parameter;
                    scope = null;
                    break;
                case ValueSourceType.InvocationVariable:
                    kind = BattleLogicDataKind.RuntimeVariable;
                    scope = BattleLogicVariableScope.Invocation;
                    break;
                case ValueSourceType.SkillVariable:
                    kind = BattleLogicDataKind.RuntimeVariable;
                    scope = BattleLogicVariableScope.Skill;
                    break;
                default:
                    return;
            }

            ValidateLogicDataReference(value.referenceId, kind, scope, definitions, owner);
        }

        private static void ValidateVariableActionReference(
            BattleActionAuthoring action,
            IReadOnlyDictionary<int, BattleLogicDataAuthoring> definitions,
            string owner)
        {
            BattleLogicVariableScope scope;
            switch (action.type)
            {
                case CompiledActionType.SetInvocationVariable:
                case CompiledActionType.AddInvocationVariable:
                    scope = BattleLogicVariableScope.Invocation;
                    break;
                case CompiledActionType.SetSkillVariable:
                case CompiledActionType.AddSkillVariable:
                    scope = BattleLogicVariableScope.Skill;
                    break;
                default:
                    return;
            }

            ValidateLogicDataReference(
                action.referenceId,
                BattleLogicDataKind.RuntimeVariable,
                scope,
                definitions,
                owner);
        }

        private static void ValidateActionReference(
            BattleActionAuthoring action,
            IReadOnlyDictionary<int, BattleLogicDataAuthoring> definitions,
            string owner)
        {
            bool isBuff = action.type == CompiledActionType.AddBuff ||
                action.type == CompiledActionType.RemoveBuff;
            if (isBuff && action.useDynamicReference)
            {
                ValidateValueShape(action.referenceValue, owner + " 动态引用");
                ValidateValueReference(action.referenceValue, definitions, owner + " 动态引用");
                return;
            }

            bool requiresReference = isBuff ||
                action.type == CompiledActionType.AddCounter ||
                action.type == CompiledActionType.SetVariable;
            if (requiresReference && action.referenceId <= 0)
            {
                throw new InvalidOperationException($"{owner} 的引用 ID 必须大于 0。");
            }
        }

        private static void ValidateLogicDataReference(
            int key,
            BattleLogicDataKind kind,
            BattleLogicVariableScope? scope,
            IReadOnlyDictionary<int, BattleLogicDataAuthoring> definitions,
            string owner)
        {
            if (!definitions.TryGetValue(key, out BattleLogicDataAuthoring definition))
            {
                throw new InvalidOperationException($"{owner} 引用了未声明的逻辑数据 Key：{key}。");
            }

            if (definition.kind != kind ||
                scope.HasValue && definition.scope != scope.Value)
            {
                throw new InvalidOperationException(
                    $"{owner} 引用的逻辑数据 {definition.name} 类型或作用域不匹配。");
            }
        }

        private List<BattleRuleAuthoring> GetAuthoringRules()
        {
            tracks = tracks ?? new List<BattleLogicTrackAuthoring>();
            if (tracks.Count == 0)
            {
                return rules ?? (rules = new List<BattleRuleAuthoring>());
            }

            var result = new List<BattleRuleAuthoring>();
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                BattleLogicTrackAuthoring track = tracks[trackIndex];
                if (track?.rules == null)
                {
                    continue;
                }
                result.AddRange(track.rules);
            }
            return result;
        }
    }

    [CreateAssetMenu(
        fileName = "TurnBasedSkillExpression",
        menuName = "LxyDemo/战斗/回合制技能表现")]
    public sealed partial class TurnBasedSkillExpressionAsset
    {
        [SerializeField, TitleGroup("基础信息"), LabelText("表现名称"), Required]
        private string expressionId = "skill_expression_new";
        [SerializeField, TitleGroup("基础信息"), LabelText("逻辑数据源"),
         Tooltip("表现只读取该逻辑资产声明的数据契约。")]
        private TurnBasedSkillLogicAsset logicSource;
        [SerializeField, TitleGroup("时间轴"), LabelText("每秒帧数"), Min(1)]
        private int framesPerSecond = 30;
        [SerializeField, TitleGroup("时间轴"), LabelText("自动计算总帧数")]
        private bool autoDuration = true;
        [SerializeField, TitleGroup("时间轴"), LabelText("总帧数"), Min(1),
         HideIf(nameof(autoDuration))]
        private int durationFrames = 30;
        [SerializeField, TitleGroup("基础信息"), LabelText("说明"), TextArea(2, 5)]
        private string description;
        [SerializeField, TitleGroup("表现轨道"), LabelText("轨道列表"),
         ListDrawerSettings(
             ListElementLabelName = "trackName",
             DraggableItems = true,
             ShowItemCount = true,
             AlwaysAddDefaultValue = true)]
        private List<BattleExpressionTrackAuthoring> tracks =
            new List<BattleExpressionTrackAuthoring>();
        [SerializeField, HideInInspector] private List<BattleExpressionClipAuthoring> clips =
            new List<BattleExpressionClipAuthoring>();

        public string ExpressionId => expressionId;
        public TurnBasedSkillLogicAsset LogicSource => logicSource;
        public int FramesPerSecond => framesPerSecond;
        public int DurationFrames => CalculateDurationFrames();
        public string Description => description;
        public IReadOnlyList<BattleExpressionTrackAuthoring> Tracks => tracks;
        public IReadOnlyList<BattleExpressionClipAuthoring> Clips =>
            GetAuthoringClips();
        public bool UsesLegacyLayout => (tracks == null || tracks.Count == 0) &&
            clips != null && clips.Count > 0;

        public void UpgradeLegacyLayout()
        {
            if (!UsesLegacyLayout)
            {
                return;
            }
            tracks = new List<BattleExpressionTrackAuthoring>
            {
                new BattleExpressionTrackAuthoring
                {
                    trackName = "迁移主表现轨",
                    parallelGroup = "主并行组",
                    executionMode = BattleAuthoringExecutionMode.Parallel,
                    operations = new List<BattleExpressionClipAuthoring>(clips),
                },
            };
            clips.Clear();
        }

        public CompiledBattleExpression Compile()
        {
            ValidateOrThrow();
            List<ClipWithGroup> sourceClips = GetClipsWithGroups();
            var result = new CompiledBattleExpressionClip[sourceClips.Count];
            for (int index = 0; index < sourceClips.Count; index++)
            {
                result[index] = sourceClips[index].Clip.Compile(
                    sourceClips[index].Group,
                    index);
            }
            Array.Sort(result, CompareClips);
            return new CompiledBattleExpression(
                expressionId,
                framesPerSecond,
                CalculateDurationFrames(),
                result);
        }

        public bool TryValidate(out string error)
        {
            try
            {
                ValidateOrThrow();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        public void ResetToEmpty(string id)
        {
            expressionId = string.IsNullOrWhiteSpace(id)
                ? "skill_expression_new"
                : id.Trim();
            logicSource = null;
            framesPerSecond = 30;
            autoDuration = true;
            durationFrames = 30;
            description = string.Empty;
            tracks = new List<BattleExpressionTrackAuthoring>();
            clips = new List<BattleExpressionClipAuthoring>();
        }

        public void ResetToSkill101011Sample(
            TurnBasedSkillLogicAsset logicSourceAsset = null)
        {
            expressionId = "skill_101011";
            logicSource = logicSourceAsset;
            framesPerSecond = 30;
            autoDuration = true;
            durationFrames = 61;
            description =
                "来自 skill_101011.lua：播放 attack_1，6~15 帧突进，" +
                "18 帧命中表现，21/24 帧声音与特效，41~51 帧归位。";
            tracks = new List<BattleExpressionTrackAuthoring>
            {
                new BattleExpressionTrackAuthoring
                {
                    trackName = "攻击方动作与位移",
                    parallelGroup = "主并行组",
                    operations = new List<BattleExpressionClipAuthoring>
                    {
                        Clip("播放攻击动作", BattleExpressionClipType.Animation, 0, 0, "attack_1"),
                        Clip("突进到目标", BattleExpressionClipType.MoveToTarget, 6, 9,
                            offset: new Vector3(-2.5f, 0f, -1f)),
                        Clip("施法声音", BattleExpressionClipType.Audio, 21, 0, "1010111"),
                        Clip("攻击方特效", BattleExpressionClipType.Effect, 24, 10, "1010111"),
                        Clip("返回站位", BattleExpressionClipType.MoveHome, 41, 10),
                    },
                },
                new BattleExpressionTrackAuthoring
                {
                    trackName = "目标受击表现",
                    parallelGroup = "主并行组",
                    executionMode = BattleAuthoringExecutionMode.Parallel,
                    operations = new List<BattleExpressionClipAuthoring>
                    {
                        Clip("伤害飘字", BattleExpressionClipType.Hit, 18, 12,
                            subject: BattleExpressionSubject.PrimaryTarget,
                            logicOutputKey: "damage_result"),
                        Clip("通用锐器受击", BattleExpressionClipType.Effect, 18, 8, "9002",
                            BattleExpressionSubject.PrimaryTarget,
                            color: new Color(1f, 0.2f, 0.12f, 1f),
                            logicOutputKey: "damage_result"),
                        Clip("命中声音", BattleExpressionClipType.Audio, 18, 0, "2",
                            BattleExpressionSubject.PrimaryTarget,
                            logicOutputKey: "damage_result"),
                        Clip("目标红闪", BattleExpressionClipType.ColorFlash, 18, 4,
                            subject: BattleExpressionSubject.PrimaryTarget,
                            color: new Color(1f, 0.15f, 0.12f, 1f),
                            intensity: 1f,
                            logicOutputKey: "damage_result"),
                        Clip("死亡检查", BattleExpressionClipType.CheckDead, 30, 24,
                            subject: BattleExpressionSubject.PrimaryTarget,
                            logicOutputKey: "damage_result"),
                    },
                },
            };
            clips = new List<BattleExpressionClipAuthoring>();
        }

        private static BattleExpressionClipAuthoring Clip(
            string operationName,
            BattleExpressionClipType type,
            int startFrame,
            int durationFrames,
            string key = "",
            BattleExpressionSubject subject = BattleExpressionSubject.Caster,
            Vector3 offset = default,
            Color color = default,
            float intensity = 1f,
            string logicOutputKey = "")
        {
            return new BattleExpressionClipAuthoring
            {
                operationName = operationName,
                logicOutputKey = logicOutputKey,
                type = type,
                subject = subject,
                startFrame = startFrame,
                durationFrames = durationFrames,
                resourceKey = key,
                offset = offset,
                color = color == default ? Color.white : color,
                intensity = intensity,
            };
        }

        private void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(expressionId))
            {
                throw new InvalidOperationException("技能表现 ID 不能为空。");
            }

            int effectiveDuration = CalculateDurationFrames();
            if (framesPerSecond <= 0 || effectiveDuration <= 0)
            {
                throw new InvalidOperationException("技能表现帧率与总帧数必须大于零。");
            }

            List<ClipWithGroup> sourceClips = GetClipsWithGroups();
            var outputKeys = new HashSet<string>(StringComparer.Ordinal);
            if (logicSource != null)
            {
                for (int index = 0; index < logicSource.Outputs.Count; index++)
                {
                    BattleLogicOutputAuthoring output = logicSource.Outputs[index];
                    if (output != null && !string.IsNullOrWhiteSpace(output.key))
                    {
                        outputKeys.Add(output.key);
                    }
                }
            }

            for (int index = 0; index < sourceClips.Count; index++)
            {
                BattleExpressionClipAuthoring clip = sourceClips[index].Clip ??
                    throw new InvalidOperationException($"技能表现 {expressionId} 第 {index} 个片段为空。");
                if (clip.startFrame < 0 || clip.durationFrames < 0 ||
                    clip.EndFrame > effectiveDuration)
                {
                    throw new InvalidOperationException(
                        $"技能表现 {expressionId} 的片段 {index} 超出时间轴范围。");
                }
                ValidateExpressionClip(clip, index);
                if (!string.IsNullOrWhiteSpace(clip.logicOutputKey) &&
                    (logicSource == null || !outputKeys.Contains(clip.logicOutputKey)))
                {
                    throw new InvalidOperationException(
                        $"表现操作 {clip.operationName} 引用的逻辑数据键不存在：{clip.logicOutputKey}");
                }
            }

            tracks = tracks ?? new List<BattleExpressionTrackAuthoring>();
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                BattleExpressionTrackAuthoring track = tracks[trackIndex];
                if (track == null || track.executionMode == BattleAuthoringExecutionMode.Parallel)
                {
                    continue;
                }

                var ordered = new List<BattleExpressionClipAuthoring>(
                    track.operations ?? new List<BattleExpressionClipAuthoring>());
                ordered.Sort((left, right) => left.startFrame.CompareTo(right.startFrame));
                for (int index = 1; index < ordered.Count; index++)
                {
                    if (ordered[index].startFrame < ordered[index - 1].EndFrame)
                    {
                        throw new InvalidOperationException(
                            $"顺序轨道 {track.trackName} 的操作帧区间发生重叠；" +
                            "需要重叠时请将轨道执行方式改为 Parallel。");
                    }
                }
            }
        }

        private void ValidateExpressionClip(
            BattleExpressionClipAuthoring clip,
            int index)
        {
            string marker = string.IsNullOrWhiteSpace(clip.operationName)
                ? $"第 {index + 1} 个操作"
                : clip.operationName;
            if (!Enum.IsDefined(typeof(BattleExpressionClipType), clip.type))
            {
                throw new InvalidOperationException(
                    $"技能表现 {expressionId} 的 {marker} 使用了未知操作类型：{clip.type}。");
            }
            if (!IsFinite(clip.intensity) || !IsFinite(clip.frequency) ||
                !IsFinite(clip.offset) || !IsFinite(clip.targetOffset) ||
                !IsFinite(clip.color))
            {
                throw new InvalidOperationException(
                    $"技能表现 {expressionId} 的 {marker} 包含 NaN 或 Infinity 参数。");
            }
            if (clip.previewTargetSlot < 1 || clip.previewTargetSlot > 18)
            {
                throw new InvalidOperationException(
                    $"技能表现 {expressionId} 的 {marker} 演示目标位置必须在 1~18 之间。");
            }
            if (RequiresResourceKey(clip.type) &&
                string.IsNullOrWhiteSpace(clip.resourceKey))
            {
                throw new InvalidOperationException(
                    $"技能表现 {expressionId} 的 {marker} 缺少资源或动作 Key。");
            }
            if (RequiresPositiveDuration(clip.type) && clip.durationFrames <= 0)
            {
                throw new InvalidOperationException(
                    $"技能表现 {expressionId} 的 {marker} 必须设置大于 0 的持续帧数。");
            }
            if ((clip.type == BattleExpressionClipType.CameraFocus ||
                 clip.type == BattleExpressionClipType.PresentationTimeScale) &&
                clip.intensity <= 0f)
            {
                throw new InvalidOperationException(
                    $"技能表现 {expressionId} 的 {marker} 强度参数必须大于 0。");
            }
        }

        private static bool RequiresResourceKey(BattleExpressionClipType type)
        {
            switch (type)
            {
                case BattleExpressionClipType.Animation:
                case BattleExpressionClipType.Effect:
                case BattleExpressionClipType.Audio:
                case BattleExpressionClipType.ProjectileEffect:
                case BattleExpressionClipType.EffectAnimation:
                case BattleExpressionClipType.RemoveEffect:
                case BattleExpressionClipType.SetSkin:
                case BattleExpressionClipType.ToggleLoopEffect:
                case BattleExpressionClipType.ChangeModel:
                case BattleExpressionClipType.BackgroundAudio:
                    return true;
                default:
                    return false;
            }
        }

        private static bool RequiresPositiveDuration(BattleExpressionClipType type)
        {
            switch (type)
            {
                case BattleExpressionClipType.MoveToTarget:
                case BattleExpressionClipType.MoveHome:
                case BattleExpressionClipType.SwapPosition:
                case BattleExpressionClipType.Hit:
                case BattleExpressionClipType.ColorFlash:
                case BattleExpressionClipType.UnitShake:
                case BattleExpressionClipType.CameraFocus:
                case BattleExpressionClipType.CameraShake:
                case BattleExpressionClipType.ProjectileEffect:
                case BattleExpressionClipType.PresentationTimeScale:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Color value)
        {
            return IsFinite(value.r) && IsFinite(value.g) &&
                IsFinite(value.b) && IsFinite(value.a);
        }

        private int CalculateDurationFrames()
        {
            if (!autoDuration)
            {
                return Mathf.Max(1, durationFrames);
            }

            int maximum = 1;
            List<BattleExpressionClipAuthoring> sourceClips = GetAuthoringClips();
            for (int index = 0; index < sourceClips.Count; index++)
            {
                if (sourceClips[index] != null)
                {
                    maximum = Mathf.Max(maximum, sourceClips[index].EndFrame);
                }
            }
            return maximum;
        }

        private List<BattleExpressionClipAuthoring> GetAuthoringClips()
        {
            List<ClipWithGroup> grouped = GetClipsWithGroups();
            var result = new List<BattleExpressionClipAuthoring>(grouped.Count);
            for (int index = 0; index < grouped.Count; index++)
            {
                result.Add(grouped[index].Clip);
            }
            return result;
        }

        private List<ClipWithGroup> GetClipsWithGroups()
        {
            tracks = tracks ?? new List<BattleExpressionTrackAuthoring>();
            var result = new List<ClipWithGroup>();
            if (tracks.Count == 0)
            {
                clips = clips ?? new List<BattleExpressionClipAuthoring>();
                for (int index = 0; index < clips.Count; index++)
                {
                    result.Add(new ClipWithGroup(clips[index], "旧版主轨"));
                }
                return result;
            }

            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                BattleExpressionTrackAuthoring track = tracks[trackIndex];
                if (track?.operations == null)
                {
                    continue;
                }
                for (int operationIndex = 0;
                     operationIndex < track.operations.Count;
                     operationIndex++)
                {
                    result.Add(new ClipWithGroup(
                        track.operations[operationIndex],
                        track.parallelGroup));
                }
            }
            return result;
        }

        private readonly struct ClipWithGroup
        {
            public ClipWithGroup(BattleExpressionClipAuthoring clip, string group)
            {
                Clip = clip;
                Group = group ?? string.Empty;
            }

            public BattleExpressionClipAuthoring Clip { get; }
            public string Group { get; }
        }

        private static int CompareClips(
            CompiledBattleExpressionClip left,
            CompiledBattleExpressionClip right)
        {
            int frame = left.StartFrame.CompareTo(right.StartFrame);
            return frame != 0
                ? frame
                : left.AuthoringOrder.CompareTo(right.AuthoringOrder);
        }
    }

    [CreateAssetMenu(
        fileName = "TurnBasedSkill",
        menuName = "LxyDemo/战斗/回合制技能")]
    public sealed partial class TurnBasedSkillAsset
    {
        [SerializeField, TitleGroup("技能配置"), HideLabel, InlineProperty]
        private BattleSkillAuthoring skill = new BattleSkillAuthoring();
        [SerializeField, TitleGroup("资产引用"), LabelText("技能逻辑"), Required]
        private TurnBasedSkillLogicAsset logic;
        [SerializeField, TitleGroup("资产引用"), LabelText("技能表现")]
        private TurnBasedSkillExpressionAsset expression;

        public BattleSkillAuthoring Skill => skill;
        public TurnBasedSkillLogicAsset Logic => logic;
        public TurnBasedSkillExpressionAsset Expression => expression;

        public CompiledSkill Compile()
        {
            ValidateOrThrow();
            CompiledRule[] rules = logic.Compile();
            var ids = new RuleId[rules.Length];
            for (int index = 0; index < rules.Length; index++)
            {
                ids[index] = rules[index].Id;
            }

            return skill.Compile(
                ids,
                expression == null ? string.Empty : expression.ExpressionId,
                logic.LogicId,
                logic.CompileDataDefinitions());
        }

        public void Configure(
            int id,
            string displayName,
            BattleSkillTargetMode targetMode,
            TurnBasedSkillLogicAsset logicAsset,
            TurnBasedSkillExpressionAsset expressionAsset,
            bool activeCommand,
            string fallbackAnimation,
            float fallbackDuration,
            Color buttonColor,
            long cost = 0)
        {
            skill = new BattleSkillAuthoring
            {
                id = id,
                displayName = displayName,
                costAttribute = AttributeType.Energy,
                cost = cost,
                targetMode = targetMode,
                activeCommand = activeCommand,
                animationName = fallbackAnimation,
                presentationDuration = fallbackDuration,
                buttonColor = buttonColor,
            };
            logic = logicAsset;
            expression = expressionAsset;
        }

        public bool TryValidate(out string error)
        {
            try
            {
                ValidateOrThrow();
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private void ValidateOrThrow()
        {
            if (skill == null || skill.id <= 0 || skill.cost < 0)
            {
                throw new InvalidOperationException($"技能资产 {name} 的基础数据无效。");
            }

            if (logic == null)
            {
                throw new InvalidOperationException($"技能资产 {name} 缺少逻辑资产。");
            }

            if (!logic.TryValidate(out string logicError))
            {
                throw new InvalidOperationException(logicError);
            }

            if (expression != null && !expression.TryValidate(out string expressionError))
            {
                throw new InvalidOperationException(expressionError);
            }

            if (expression != null)
            {
                var providedKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < logic.Outputs.Count; index++)
                {
                    BattleLogicOutputAuthoring output = logic.Outputs[index];
                    if (output != null && !string.IsNullOrWhiteSpace(output.key))
                    {
                        providedKeys.Add(output.key);
                    }
                }

                for (int index = 0; index < expression.Clips.Count; index++)
                {
                    string requiredKey = expression.Clips[index]?.logicOutputKey;
                    if (!string.IsNullOrWhiteSpace(requiredKey) &&
                        !providedKeys.Contains(requiredKey))
                    {
                        throw new InvalidOperationException(
                            $"技能 {skill.id} 的逻辑 {logic.LogicId} 未提供表现所需数据：" +
                            requiredKey);
                    }
                }
            }
        }
    }
}
