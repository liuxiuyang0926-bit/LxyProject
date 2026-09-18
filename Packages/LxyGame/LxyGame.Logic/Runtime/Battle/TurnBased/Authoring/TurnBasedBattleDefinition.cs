using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Battle.TurnBased.Authoring
{
    public enum BattleUnitControl
    {
        Player = 0,
        Ai = 1,
    }

    public enum BattleSkillTargetMode
    {
        SingleEnemy = 0,
        AllEnemies = 1,
        Self = 2,
        LowestHpAlly = 3,
    }

    public enum BattleAuthoringExecutionMode
    {
        Sequence = 0,
        Parallel = 1,
    }

    [Serializable]
    public sealed class BattleTargetAuthoring
    {
        [LabelText("目标类型")]
        public TargetSelectorType type = TargetSelectorType.SelectedTargets;
        [LabelText("数量上限"), Min(0), Tooltip("0 表示不限制数量。")]
        public int count;
        [LabelText("包含死亡单位")]
        public bool includeDead;
        [LabelText("配置 ID 过滤"), Tooltip("大于 0 时，只保留指定配置 ID 的单位。")]
        [Min(0)] public int configIdFilter;

        internal CompiledTargetSelector Compile() =>
            new CompiledTargetSelector(type, count, includeDead, configIdFilter);
    }

    [Serializable]
    public sealed class BattleValueOperandAuthoring
    {
        [LabelText("倍率来源")]
        public ValueSourceType source = ValueSourceType.LogicParameter;
        [LabelText("固定倍率"), ShowIf(nameof(UsesConstant))]
        public long constant = BattleNumeric.BasisPointOne;
        [LabelText("倍率属性"), ShowIf(nameof(UsesAttribute))]
        public AttributeType attribute = AttributeType.Hp;
        [LabelText("引用 ID"), ShowIf(nameof(UsesReferenceId)),
         Tooltip("逻辑参数/变量填写数据 Key；Buff 层数填写 Buff ID。")]
        public int referenceId;

        private bool UsesConstant => source == ValueSourceType.Constant;
        private bool UsesAttribute => source == ValueSourceType.OwnerAttribute ||
            source == ValueSourceType.AttackerAttribute ||
            source == ValueSourceType.TargetAttribute;
        private bool UsesReferenceId => source == ValueSourceType.BuffStack ||
            source == ValueSourceType.BattleCounter ||
            source == ValueSourceType.LogicParameter ||
            source == ValueSourceType.InvocationVariable ||
            source == ValueSourceType.SkillVariable;
    }

    [Serializable]
    public sealed class BattleValueAuthoring
    {
        [LabelText("数值来源")]
        public ValueSourceType source = ValueSourceType.Constant;
        [LabelText("固定值"), ShowIf(nameof(UsesConstant))]
        public long constant;
        [LabelText("属性"), ShowIf(nameof(UsesAttribute))]
        public AttributeType attribute = AttributeType.Hp;
        [LabelText("引用 ID"), ShowIf(nameof(UsesReferenceId)),
         Tooltip("BuffStack 填 Buff ID；BattleCounter 填全局变量 ID；逻辑参数/变量填逻辑数据 Key。")]
        public int referenceId;
        [LabelText("倍率（万分比）"), Min(0), ShowIf(nameof(UsesStaticScale)),
         Tooltip("10000 = 100%，13500 = 135%。")]
        public int scaleBasisPoint = BattleNumeric.BasisPointOne;
        [LabelText("使用动态倍率"),
         Tooltip("开启后倍率可读取策划参数或运行时变量，适合按技能等级配置伤害系数。")]
        public bool useDynamicScale;
        [LabelText("动态倍率"), ShowIf(nameof(useDynamicScale)), InlineProperty]
        public BattleValueOperandAuthoring dynamicScale =
            new BattleValueOperandAuthoring();
        [LabelText("最终偏移")]
        public long offset;
        [LabelText("启用下限")]
        public bool hasMinimum;
        [LabelText("最小值"), ShowIf(nameof(hasMinimum))]
        public long minimum;
        [LabelText("启用上限")]
        public bool hasMaximum;
        [LabelText("最大值"), ShowIf(nameof(hasMaximum))]
        public long maximum;

        private bool UsesConstant => source == ValueSourceType.Constant;
        private bool UsesStaticScale => !useDynamicScale;
        private bool UsesAttribute => source == ValueSourceType.OwnerAttribute ||
            source == ValueSourceType.AttackerAttribute ||
            source == ValueSourceType.TargetAttribute;
        private bool UsesReferenceId => source == ValueSourceType.BuffStack ||
            source == ValueSourceType.BattleCounter ||
            source == ValueSourceType.LogicParameter ||
            source == ValueSourceType.InvocationVariable ||
            source == ValueSourceType.SkillVariable;

        internal CompiledValue Compile() =>
            new CompiledValue(
                source,
                constant,
                attribute,
                referenceId,
                scaleBasisPoint,
                offset,
                hasMinimum,
                minimum,
                hasMaximum,
                maximum,
                useDynamicScale,
                dynamicScale?.source ?? ValueSourceType.Constant,
                dynamicScale?.constant ?? BattleNumeric.BasisPointOne,
                dynamicScale?.attribute ?? AttributeType.Hp,
                dynamicScale?.referenceId ?? 0);
    }

    [Serializable]
    public sealed class BattleConditionAuthoring
    {
        [LabelText("条件类型")]
        public CompiledConditionType type = CompiledConditionType.Always;
        [LabelText("条件目标"), ShowIf(nameof(UsesTarget))]
        public BattleTargetAuthoring target = new BattleTargetAuthoring();
        [LabelText("比较方式"), ShowIf(nameof(UsesComparison))]
        public ComparisonOperator comparison = ComparisonOperator.Equal;
        [LabelText("比较值"), ShowIf(nameof(UsesValue))]
        public long value;
        [LabelText("左侧数值"), ShowIf(nameof(UsesLogicValues)), InlineProperty]
        public BattleValueAuthoring leftValue = new BattleValueAuthoring();
        [LabelText("右侧数值"), ShowIf(nameof(UsesLogicValues)), InlineProperty]
        public BattleValueAuthoring rightValue = new BattleValueAuthoring();
        [LabelText("比较属性"), ShowIf(nameof(UsesAttribute))]
        public AttributeType attribute = AttributeType.Hp;
        [LabelText("引用 ID"), ShowIf(nameof(UsesReferenceId)),
         Tooltip("HasBuff 填 Buff ID；计数器条件填变量 ID。")]
        public int referenceId;
        [LabelText("条件取反"), HideIf(nameof(IsAlways))]
        public bool negate;

        private bool IsAlways => type == CompiledConditionType.Always;
        private bool UsesTarget => type != CompiledConditionType.Always &&
            type != CompiledConditionType.CounterCompareCurrentRound &&
            type != CompiledConditionType.LogicValueCompare;
        private bool UsesComparison => type == CompiledConditionType.ConfigIdCompare ||
            type == CompiledConditionType.AttributeCompare ||
            type == CompiledConditionType.HpPercentCompare ||
            type == CompiledConditionType.EventValueCompare ||
            type == CompiledConditionType.CounterCompare ||
            type == CompiledConditionType.RoundCompare ||
            type == CompiledConditionType.TargetCountCompare ||
            type == CompiledConditionType.CounterCompareCurrentRound ||
            type == CompiledConditionType.LogicValueCompare;
        private bool UsesValue => type == CompiledConditionType.ConfigIdCompare ||
            type == CompiledConditionType.AttributeCompare ||
            type == CompiledConditionType.HpPercentCompare ||
            type == CompiledConditionType.EventValueCompare ||
            type == CompiledConditionType.CounterCompare ||
            type == CompiledConditionType.RoundCompare ||
            type == CompiledConditionType.TargetCountCompare;
        private bool UsesAttribute => type == CompiledConditionType.AttributeCompare;
        private bool UsesLogicValues => type == CompiledConditionType.LogicValueCompare;
        private bool UsesReferenceId => type == CompiledConditionType.HasBuff ||
            type == CompiledConditionType.CounterCompare ||
            type == CompiledConditionType.CounterCompareCurrentRound;

        internal CompiledCondition Compile() =>
            new CompiledCondition(
                type,
                (target ?? new BattleTargetAuthoring()).Compile(),
                comparison,
                value,
                attribute,
                referenceId,
                negate,
                (leftValue ?? new BattleValueAuthoring()).Compile(),
                (rightValue ?? new BattleValueAuthoring()).Compile());
    }

    [Serializable]
    public sealed class BattleActionAuthoring
    {
        [LabelText("操作名称"), Tooltip("编辑器中用于识别、检索和评审此操作的名称。")]
        public string operationName = "逻辑操作";
        [LabelText("执行组"), Tooltip("同一并行组中的操作表示并行语义；运行时仍按确定性顺序结算。")]
        public string executionGroup = "主流程";
        [LabelText("执行方式")]
        public BattleAuthoringExecutionMode executionMode =
            BattleAuthoringExecutionMode.Sequence;
        [LabelText("操作类型")]
        public CompiledActionType type = CompiledActionType.Damage;
        [LabelText("操作目标"), ShowIf(nameof(UsesTarget))]
        public BattleTargetAuthoring target = new BattleTargetAuthoring();
        [LabelText("数值表达式"), ShowIf(nameof(UsesValue))]
        public BattleValueAuthoring value = new BattleValueAuthoring();
        [LabelText("修改属性"), ShowIf(nameof(UsesAttribute))]
        public AttributeType attribute = AttributeType.Hp;
        [LabelText("动态引用 ID"), ShowIf(nameof(SupportsDynamicReference)),
         Tooltip("启用后可从策划参数或变量读取 Buff ID；关闭时使用固定引用 ID。")]
        public bool useDynamicReference;
        [LabelText("引用 ID 数值"), ShowIf(nameof(UsesDynamicReference)), InlineProperty]
        public BattleValueAuthoring referenceValue = new BattleValueAuthoring();
        [LabelText("引用 ID"), ShowIf(nameof(UsesFixedReferenceId)),
         Tooltip("添加/移除 Buff 填 Buff ID；计数器和变量操作填变量 ID。")]
        public int referenceId;
        [LabelText("伤害选项"), ShowIf(nameof(IsDamage))]
        public CompiledActionFlags flags;

        private bool IsDamage => type == CompiledActionType.Damage;
        private bool SupportsDynamicReference =>
            type == CompiledActionType.AddBuff || type == CompiledActionType.RemoveBuff;
        private bool UsesDynamicReference => SupportsDynamicReference && useDynamicReference;
        private bool UsesTarget => type != CompiledActionType.AddCounter &&
            type != CompiledActionType.SetVariable &&
            type != CompiledActionType.EndTurn &&
            type != CompiledActionType.SetInvocationVariable &&
            type != CompiledActionType.AddInvocationVariable &&
            type != CompiledActionType.SetSkillVariable &&
            type != CompiledActionType.AddSkillVariable;
        private bool UsesValue => type == CompiledActionType.Damage ||
            type == CompiledActionType.Heal ||
            type == CompiledActionType.ModifyAttribute ||
            type == CompiledActionType.ModifyResource ||
            type == CompiledActionType.Revive ||
            type == CompiledActionType.MovePosition ||
            type == CompiledActionType.AddCounter ||
            type == CompiledActionType.SetVariable ||
            type == CompiledActionType.SetInvocationVariable ||
            type == CompiledActionType.AddInvocationVariable ||
            type == CompiledActionType.SetSkillVariable ||
            type == CompiledActionType.AddSkillVariable;
        private bool UsesAttribute => type == CompiledActionType.ModifyAttribute ||
            type == CompiledActionType.ModifyResource;
        private bool UsesReferenceId => type == CompiledActionType.AddBuff ||
            type == CompiledActionType.RemoveBuff ||
            type == CompiledActionType.AddCounter ||
            type == CompiledActionType.SetVariable ||
            type == CompiledActionType.SetInvocationVariable ||
            type == CompiledActionType.AddInvocationVariable ||
            type == CompiledActionType.SetSkillVariable ||
            type == CompiledActionType.AddSkillVariable;
        private bool UsesFixedReferenceId => UsesReferenceId && !UsesDynamicReference;

        internal CompiledAction Compile() =>
            new CompiledAction(
                type,
                (target ?? new BattleTargetAuthoring()).Compile(),
                (value ?? new BattleValueAuthoring()).Compile(),
                attribute,
                referenceId,
                flags,
                useDynamicReference,
                (referenceValue ?? new BattleValueAuthoring()).Compile());
    }

    [Serializable]
    public sealed class BattleRuleAuthoring
    {
        [LabelText("规则名称"), Tooltip("编辑器中显示的规则名称。")]
        public string operationName = "规则";
        [LabelText("执行组"), Tooltip("同名组可表达 Lua Parallel/并行分支。")]
        public string executionGroup = "主流程";
        [LabelText("执行方式")]
        public BattleAuthoringExecutionMode executionMode =
            BattleAuthoringExecutionMode.Sequence;
        [LabelText("规则 ID"), Min(1)] public int id = 1;
        [LabelText("触发事件")]
        public BattleEventType trigger = BattleEventType.SkillCast;
        [LabelText("执行阶段")]
        public BattleEventPhase phase = BattleEventPhase.Main;
        [LabelText("优先级"), Tooltip("数字越小越先执行。")]
        public int priority;
        [LabelText("拥有者必须存活")]
        public bool ownerMustBeAlive = true;
        [LabelText("触发条件（全部满足）"), ListDrawerSettings(
            ListElementLabelName = "type",
            DraggableItems = true,
            ShowItemCount = true,
            AlwaysAddDefaultValue = true)]
        public List<BattleConditionAuthoring> conditions =
            new List<BattleConditionAuthoring>();
        [LabelText("逻辑操作（按列表顺序）"), ListDrawerSettings(
            ListElementLabelName = "operationName",
            DraggableItems = true,
            ShowItemCount = true,
            AlwaysAddDefaultValue = true)]
        public List<BattleActionAuthoring> actions =
            new List<BattleActionAuthoring>();

        internal CompiledRule Compile()
        {
            var compiledConditions =
                new CompiledCondition[conditions?.Count ?? 0];
            for (int index = 0; index < compiledConditions.Length; index++)
            {
                compiledConditions[index] = conditions[index].Compile();
            }

            var compiledActions = new CompiledAction[actions?.Count ?? 0];
            for (int index = 0; index < compiledActions.Length; index++)
            {
                compiledActions[index] = actions[index].Compile();
            }

            return new CompiledRule(
                new RuleId(id),
                trigger,
                phase,
                priority,
                compiledConditions,
                compiledActions,
                ownerMustBeAlive);
        }
    }

    [Serializable]
    public sealed class BattleSkillAuthoring
    {
        [LabelText("技能 ID"), Min(1)] public int id = 1001;
        [LabelText("显示名称")]
        public string displayName = "普通攻击";
        [LabelText("消耗属性")]
        public AttributeType costAttribute = AttributeType.Energy;
        [LabelText("消耗数值"), Min(0)] public long cost;
        [LabelText("目标模式")]
        public BattleSkillTargetMode targetMode =
            BattleSkillTargetMode.SingleEnemy;
        [LabelText("主动技能"), Tooltip("关闭后作为被动技能注册规则，但不会出现在行动按钮与 AI 选技中。")]
        public bool activeCommand = true;
        [LabelText("旧版规则 ID（独立技能无需配置）"),
         Tooltip("独立技能资产会直接使用 Logic 中的规则 ID。")]
        public List<int> ruleIds = new List<int>();

        [Header("表现")]
        [LabelText("备用动作名称")]
        public string animationName = "attack_1";
        [LabelText("备用表现时长"), Min(0.05f)]
        public float presentationDuration = 0.65f;
        [LabelText("技能按钮颜色")]
        public Color buttonColor = new Color(0.18f, 0.48f, 0.78f, 1f);

        internal CompiledSkill Compile(
            RuleId[] compiledRuleIdsOverride = null,
            string expressionKeyOverride = null,
            string logicIdOverride = null,
            CompiledBattleLogicData[] logicDataOverride = null)
        {
            RuleId[] compiledRuleIds = compiledRuleIdsOverride;
            if (compiledRuleIds == null)
            {
                compiledRuleIds = new RuleId[ruleIds?.Count ?? 0];
                for (int index = 0; index < compiledRuleIds.Length; index++)
                {
                    compiledRuleIds[index] = new RuleId(ruleIds[index]);
                }
            }

            return new CompiledSkill(
                new SkillId(id),
                costAttribute,
                cost,
                compiledRuleIds,
                expressionKeyOverride ?? animationName,
                logicIdOverride ?? string.Empty,
                logicDataOverride);
        }
    }

    [Serializable]
    public sealed class BattleBuffAuthoring
    {
        [Min(1)] public int id = 2001;
        public string displayName = "Buff";
        [Min(1)] public int durationRounds = 1;
        [Min(1)] public int maxStack = 1;
        public BuffStackRule stackRule = BuffStackRule.RefreshDuration;
        public int tags;
        public List<int> ruleIds = new List<int>();

        internal CompiledBuff Compile()
        {
            var compiledRuleIds = new RuleId[ruleIds?.Count ?? 0];
            for (int index = 0; index < compiledRuleIds.Length; index++)
            {
                compiledRuleIds[index] = new RuleId(ruleIds[index]);
            }

            return new CompiledBuff(
                new BuffId(id),
                durationRounds,
                maxStack,
                stackRule,
                compiledRuleIds,
                tags);
        }
    }

    [Serializable]
    public sealed class BattleUnitAuthoring
    {
        [Min(1)] public int unitId = 1;
        [Min(1)] public int configId = 101;
        public string displayName = "武将";
        public BattleCamp camp = BattleCamp.Attacker;
        public BattleUnitControl control = BattleUnitControl.Player;
        public int position;

        [Header("属性")]
        [Min(1)] public long maxHp = 1000;
        [Min(0)] public long attack = 100;
        [Min(0)] public long defense = 10;
        [Min(1)] public long speed = 100;
        [Range(0, BattleNumeric.BasisPointOne)] public int critRate = 1500;
        [Min(0)] public int critDamage = BattleNumeric.DefaultCritDamage;
        [Range(0, BattleNumeric.BasisPointOne)] public int hitRate =
            BattleNumeric.BasisPointOne;
        [Range(0, BattleNumeric.BasisPointOne)] public int dodgeRate;
        [Min(0)] public long energy = 100;
        public List<int> skillIds = new List<int>();

        [Header("表现")]
        public GameObject viewPrefab;
        public Vector3 viewPosition;
        public Vector3 viewScale = Vector3.one;
        public bool flipX;
        public string idleAnimation = "idle_1";
        public string hitAnimation = "hit_1";
        public string deathAnimation = "die_1";
        public string victoryAnimation = "win_1";

        internal BattleUnit Compile()
        {
            var unit = new BattleUnit(
                new UnitId(unitId),
                configId,
                camp,
                position);
            unit.InitializeHealth(maxHp);
            unit.InitializeAttribute(AttributeType.Attack, attack);
            unit.InitializeAttribute(AttributeType.Defense, defense);
            unit.InitializeAttribute(AttributeType.Speed, speed);
            unit.InitializeAttribute(AttributeType.CritRate, critRate);
            unit.InitializeAttribute(AttributeType.CritDamage, critDamage);
            unit.InitializeAttribute(AttributeType.HitRate, hitRate);
            unit.InitializeAttribute(AttributeType.DodgeRate, dodgeRate);
            unit.InitializeAttribute(AttributeType.Energy, energy);

            for (int index = 0; index < skillIds.Count; index++)
            {
                unit.Skills.Add(new SkillInstance(
                    new SkillId(skillIds[index]),
                    unit.Id));
            }

            return unit;
        }
    }

    [Serializable]
    public sealed class BattlePresentationAuthoring
    {
        [Min(0f)] public float battleStartDuration = 0.45f;
        [Min(0f)] public float turnBannerDuration = 0.25f;
        [Min(0f)] public float hitDuration = 0.38f;
        [Min(0f)] public float deathDuration = 0.8f;
        [Min(0f)] public float aiThinkingDuration = 0.55f;
        [Min(0f)] public float commandGapDuration = 0.2f;
        public Color attackerAccent = new Color(0.15f, 0.75f, 1f, 1f);
        public Color defenderAccent = new Color(1f, 0.32f, 0.25f, 1f);
    }

    public sealed class TurnBasedBattleRuntimeBuild
    {
        public TurnBasedBattleRuntimeBuild(
            CompiledBattleDatabase database,
            BattleUnit[] units)
        {
            Database = database;
            Units = units;
        }

        public CompiledBattleDatabase Database { get; }
        public BattleUnit[] Units { get; }
    }

    [CreateAssetMenu(
        fileName = "TurnBasedBattleDefinition",
        menuName = "LxyDemo/战斗/回合制战斗定义")]
    public sealed class TurnBasedBattleDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "回合制战斗";
        [SerializeField] private string logicVersion = "1.0.0";
        [SerializeField] private string runtimeDataVersion = "1";
        [SerializeField] private int randomSeed = 20260907;
        [SerializeField] private List<BattleUnitAuthoring> units =
            new List<BattleUnitAuthoring>();
        [SerializeField] private List<BattleSkillAuthoring> skills =
            new List<BattleSkillAuthoring>();
        [SerializeField, Tooltip(
            "非空时，以这些独立技能资产作为编译输入；内嵌技能和规则仅作旧数据兼容。")]
        private List<TurnBasedSkillAsset> generatedSkillAssets =
            new List<TurnBasedSkillAsset>();
        [SerializeField] private List<BattleRuleAuthoring> rules =
            new List<BattleRuleAuthoring>();
        [SerializeField] private List<BattleBuffAuthoring> buffs =
            new List<BattleBuffAuthoring>();
        [SerializeField] private BattlePresentationAuthoring presentation =
            new BattlePresentationAuthoring();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;
        public int RandomSeed => randomSeed;
        public IReadOnlyList<BattleUnitAuthoring> Units => units;
        public IReadOnlyList<BattleSkillAuthoring> Skills => skills;
        public IReadOnlyList<TurnBasedSkillAsset> GeneratedSkillAssets =>
            generatedSkillAssets;
        public bool UsesGeneratedSkillAssets => generatedSkillAssets != null &&
            generatedSkillAssets.Count > 0;
        public BattlePresentationAuthoring Presentation => presentation;

        public TurnBasedBattleRuntimeBuild Compile()
        {
            EnsureDefaults();
            ValidateOrThrow();

            CompileSkills(
                out CompiledSkill[] compiledSkills,
                out CompiledRule[] compiledRules,
                out CompiledBattleExpression[] compiledExpressions);

            var compiledBuffs = new CompiledBuff[buffs.Count];
            for (int index = 0; index < buffs.Count; index++)
            {
                compiledBuffs[index] = buffs[index].Compile();
            }

            var compiledUnits = new BattleUnit[units.Count];
            for (int index = 0; index < units.Count; index++)
            {
                compiledUnits[index] = units[index].Compile();
            }

            var database = new CompiledBattleDatabase(
                logicVersion,
                runtimeDataVersion,
                CalculateLogicHash(),
                compiledSkills,
                compiledRules,
                compiledBuffs,
                compiledExpressions);
            return new TurnBasedBattleRuntimeBuild(database, compiledUnits);
        }

        public bool TryValidate(out string error)
        {
            try
            {
                EnsureDefaults();
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

        public BattleUnitAuthoring FindUnit(int unitId)
        {
            for (int index = 0; index < units.Count; index++)
            {
                if (units[index] != null && units[index].unitId == unitId)
                {
                    return units[index];
                }
            }

            return null;
        }

        public BattleSkillAuthoring FindSkill(int skillId)
        {
            if (UsesGeneratedSkillAssets)
            {
                for (int index = 0; index < generatedSkillAssets.Count; index++)
                {
                    TurnBasedSkillAsset asset = generatedSkillAssets[index];
                    if (asset != null && asset.Skill != null &&
                        asset.Skill.id == skillId)
                    {
                        return asset.Skill;
                    }
                }

                return null;
            }

            for (int index = 0; index < skills.Count; index++)
            {
                if (skills[index] != null && skills[index].id == skillId)
                {
                    return skills[index];
                }
            }

            return null;
        }

        public void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = "回合制战斗";
            }
            if (string.IsNullOrWhiteSpace(logicVersion))
            {
                logicVersion = "1.0.0";
            }
            if (string.IsNullOrWhiteSpace(runtimeDataVersion))
            {
                runtimeDataVersion = "1";
            }
            units = units ?? new List<BattleUnitAuthoring>();
            skills = skills ?? new List<BattleSkillAuthoring>();
            generatedSkillAssets = generatedSkillAssets ??
                new List<TurnBasedSkillAsset>();
            rules = rules ?? new List<BattleRuleAuthoring>();
            buffs = buffs ?? new List<BattleBuffAuthoring>();
            presentation = presentation ?? new BattlePresentationAuthoring();
        }

        public void ResetToDefault(
            GameObject attackerPrefab,
            GameObject defenderPrefab)
        {
            displayName = "虎牢关演武";
            logicVersion = "1.0.0";
            runtimeDataVersion = "1";
            randomSeed = 20260907;
            units = new List<BattleUnitAuthoring>
            {
                new BattleUnitAuthoring
                {
                    unitId = 1,
                    configId = 101,
                    displayName = "关羽",
                    camp = BattleCamp.Attacker,
                    control = BattleUnitControl.Player,
                    position = 0,
                    maxHp = 1200,
                    attack = 190,
                    defense = 30,
                    speed = 110,
                    critRate = 2200,
                    critDamage = 16500,
                    hitRate = 10000,
                    dodgeRate = 500,
                    energy = 100,
                    skillIds = new List<int> { 1001, 1002 },
                    viewPrefab = attackerPrefab,
                    viewPosition = new Vector3(-3.25f, -1.25f, 0f),
                    viewScale = new Vector3(0.9f, 0.9f, 0.9f),
                    idleAnimation = "idle_1",
                    hitAnimation = "hit_1",
                    deathAnimation = "die_1",
                    victoryAnimation = "win_1",
                },
                new BattleUnitAuthoring
                {
                    unitId = 2,
                    configId = 202,
                    displayName = "锤将",
                    camp = BattleCamp.Defender,
                    control = BattleUnitControl.Ai,
                    position = 1,
                    maxHp = 1050,
                    attack = 155,
                    defense = 25,
                    speed = 90,
                    critRate = 1200,
                    critDamage = 15000,
                    hitRate = 10000,
                    dodgeRate = 300,
                    energy = 100,
                    skillIds = new List<int> { 1001 },
                    viewPrefab = defenderPrefab,
                    viewPosition = new Vector3(3.25f, -1.25f, 0f),
                    viewScale = Vector3.one,
                    flipX = true,
                    idleAnimation = "idle_1",
                    hitAnimation = "hit_1",
                    deathAnimation = "die_1",
                    victoryAnimation = "win_1",
                },
            };

            skills = new List<BattleSkillAuthoring>
            {
                new BattleSkillAuthoring
                {
                    id = 1001,
                    displayName = "普通攻击",
                    targetMode = BattleSkillTargetMode.SingleEnemy,
                    ruleIds = new List<int> { 1 },
                    animationName = "attack_1",
                    presentationDuration = 0.65f,
                    buttonColor = new Color(0.16f, 0.48f, 0.76f, 1f),
                },
                new BattleSkillAuthoring
                {
                    id = 1002,
                    displayName = "青龙斩",
                    targetMode = BattleSkillTargetMode.SingleEnemy,
                    ruleIds = new List<int> { 2 },
                    animationName = "skill_1",
                    presentationDuration = 0.9f,
                    buttonColor = new Color(0.72f, 0.42f, 0.12f, 1f),
                },
            };

            generatedSkillAssets = new List<TurnBasedSkillAsset>();

            rules = new List<BattleRuleAuthoring>
            {
                CreateDamageRule(1, BattleNumeric.BasisPointOne, 0),
                CreateDamageRule(
                    2,
                    13500,
                    10,
                    CompiledActionFlags.CanCritical |
                    CompiledActionFlags.CanMiss),
            };
            buffs = new List<BattleBuffAuthoring>();
            presentation = new BattlePresentationAuthoring();
        }

        public void SetGeneratedSkillAssets(
            IReadOnlyList<TurnBasedSkillAsset> assets,
            bool attachPassiveSkillsToAttackers = true)
        {
            generatedSkillAssets = assets == null
                ? new List<TurnBasedSkillAsset>()
                : new List<TurnBasedSkillAsset>(assets);

            if (!UsesGeneratedSkillAssets)
            {
                return;
            }

            var activeIds = new List<int>();
            var passiveIds = new List<int>();
            for (int index = 0; index < generatedSkillAssets.Count; index++)
            {
                BattleSkillAuthoring skill = generatedSkillAssets[index]?.Skill;
                if (skill == null)
                {
                    continue;
                }

                (skill.activeCommand ? activeIds : passiveIds).Add(skill.id);
            }

            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                BattleUnitAuthoring unit = units[unitIndex];
                var next = new List<int>();
                for (int skillIndex = 0; skillIndex < unit.skillIds.Count; skillIndex++)
                {
                    int id = unit.skillIds[skillIndex];
                    if (activeIds.Contains(id) && !next.Contains(id))
                    {
                        next.Add(id);
                    }
                }

                if (next.Count == 0 && activeIds.Count > 0)
                {
                    next.Add(activeIds[0]);
                }

                if (attachPassiveSkillsToAttackers &&
                    unit.camp == BattleCamp.Attacker)
                {
                    for (int index = 0; index < passiveIds.Count; index++)
                    {
                        if (!next.Contains(passiveIds[index]))
                        {
                            next.Add(passiveIds[index]);
                        }
                    }
                }

                unit.skillIds = next;
            }
        }

        private void CompileSkills(
            out CompiledSkill[] compiledSkills,
            out CompiledRule[] compiledRules,
            out CompiledBattleExpression[] compiledExpressions)
        {
            if (!UsesGeneratedSkillAssets)
            {
                compiledRules = new CompiledRule[rules.Count];
                for (int index = 0; index < rules.Count; index++)
                {
                    compiledRules[index] = rules[index].Compile();
                }

                compiledSkills = new CompiledSkill[skills.Count];
                for (int index = 0; index < skills.Count; index++)
                {
                    compiledSkills[index] = skills[index].Compile();
                }

                compiledExpressions = Array.Empty<CompiledBattleExpression>();
                return;
            }

            var skillList = new List<CompiledSkill>(generatedSkillAssets.Count);
            var ruleList = new List<CompiledRule>();
            var expressionList = new List<CompiledBattleExpression>();
            var skillIds = new HashSet<int>();
            var ruleIds = new HashSet<int>();
            var expressionIds = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < generatedSkillAssets.Count; index++)
            {
                TurnBasedSkillAsset asset = generatedSkillAssets[index] ??
                    throw new InvalidOperationException($"独立技能资产第 {index} 项为空。");
                CompiledSkill skill = asset.Compile();
                if (!skillIds.Add(skill.Id.Value))
                {
                    throw new InvalidOperationException($"独立技能 ID 重复：{skill.Id}");
                }
                skillList.Add(skill);

                CompiledRule[] assetRules = asset.Logic.Compile();
                for (int ruleIndex = 0; ruleIndex < assetRules.Length; ruleIndex++)
                {
                    if (!ruleIds.Add(assetRules[ruleIndex].Id.Value))
                    {
                        throw new InvalidOperationException(
                            $"独立技能规则 ID 重复：{assetRules[ruleIndex].Id}");
                    }
                    ruleList.Add(assetRules[ruleIndex]);
                }

                if (asset.Expression != null &&
                    expressionIds.Add(asset.Expression.ExpressionId))
                {
                    expressionList.Add(asset.Expression.Compile());
                }
            }

            compiledSkills = skillList.ToArray();
            compiledRules = ruleList.ToArray();
            compiledExpressions = expressionList.ToArray();
        }

        private static BattleRuleAuthoring CreateDamageRule(
            int id,
            int scaleBasisPoint,
            long offset,
            CompiledActionFlags flags =
                CompiledActionFlags.CanCritical |
                CompiledActionFlags.CanMiss)
        {
            return new BattleRuleAuthoring
            {
                id = id,
                trigger = BattleEventType.SkillCast,
                phase = BattleEventPhase.Main,
                actions = new List<BattleActionAuthoring>
                {
                    new BattleActionAuthoring
                    {
                        type = CompiledActionType.Damage,
                        target = new BattleTargetAuthoring
                        {
                            type = TargetSelectorType.SelectedTargets,
                            count = 0,
                        },
                        value = new BattleValueAuthoring
                        {
                            source = ValueSourceType.OwnerAttribute,
                            attribute = AttributeType.Attack,
                            scaleBasisPoint = scaleBasisPoint,
                            offset = offset,
                            hasMinimum = true,
                            minimum = 1,
                        },
                        flags = flags,
                    },
                },
            };
        }

        private void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(logicVersion) ||
                string.IsNullOrWhiteSpace(runtimeDataVersion))
            {
                throw new InvalidOperationException("逻辑版本与运行时数据版本不能为空。");
            }

            if (units == null || units.Count < 2)
            {
                throw new InvalidOperationException("回合制战斗至少需要两个单位。");
            }

            ValidateUnits();
            ValidateRules();
            ValidateSkills();
            ValidateBuffs();
        }

        private void ValidateUnits()
        {
            var ids = new HashSet<int>();
            var positions = new HashSet<int>();
            bool hasAttacker = false;
            bool hasDefender = false;
            for (int index = 0; index < units.Count; index++)
            {
                BattleUnitAuthoring unit = units[index] ??
                    throw new InvalidOperationException($"单位配置第 {index} 项为空。");
                if (unit.unitId <= 0 || !ids.Add(unit.unitId))
                {
                    throw new InvalidOperationException($"单位 ID 无效或重复：{unit.unitId}");
                }

                if (!positions.Add(unit.position))
                {
                    throw new InvalidOperationException($"单位站位重复：{unit.position}");
                }

                if (unit.configId <= 0 || unit.camp == BattleCamp.Neutral ||
                    unit.maxHp <= 0 || unit.speed <= 0)
                {
                    throw new InvalidOperationException($"单位 {unit.unitId} 的基础数据无效。");
                }

                if (unit.skillIds == null || unit.skillIds.Count == 0)
                {
                    throw new InvalidOperationException($"单位 {unit.unitId} 至少需要一个技能。");
                }

                var unitSkills = new HashSet<int>();
                for (int skillIndex = 0; skillIndex < unit.skillIds.Count; skillIndex++)
                {
                    if (unit.skillIds[skillIndex] <= 0 ||
                        !unitSkills.Add(unit.skillIds[skillIndex]))
                    {
                        throw new InvalidOperationException(
                            $"单位 {unit.unitId} 的技能 ID 无效或重复。");
                    }
                }

                hasAttacker |= unit.camp == BattleCamp.Attacker;
                hasDefender |= unit.camp == BattleCamp.Defender;
            }

            if (!hasAttacker || !hasDefender)
            {
                throw new InvalidOperationException("战斗配置必须同时包含进攻方与防守方。");
            }
        }

        private void ValidateRules()
        {
            if (UsesGeneratedSkillAssets)
            {
                var externalRuleIds = new HashSet<int>();
                for (int assetIndex = 0;
                     assetIndex < generatedSkillAssets.Count;
                     assetIndex++)
                {
                    TurnBasedSkillAsset asset = generatedSkillAssets[assetIndex] ??
                        throw new InvalidOperationException(
                            $"独立技能资产第 {assetIndex} 项为空。");
                    if (!asset.TryValidate(out string error))
                    {
                        throw new InvalidOperationException(error);
                    }

                    IReadOnlyList<BattleRuleAuthoring> assetRules = asset.Logic.Rules;
                    for (int ruleIndex = 0; ruleIndex < assetRules.Count; ruleIndex++)
                    {
                        if (!externalRuleIds.Add(assetRules[ruleIndex].id))
                        {
                            throw new InvalidOperationException(
                                $"独立技能规则 ID 重复：{assetRules[ruleIndex].id}");
                        }
                    }
                }
                return;
            }

            if (rules == null)
            {
                throw new InvalidOperationException("规则列表不能为空。");
            }

            var ids = new HashSet<int>();
            for (int index = 0; index < rules.Count; index++)
            {
                BattleRuleAuthoring rule = rules[index] ??
                    throw new InvalidOperationException($"规则配置第 {index} 项为空。");
                if (rule.id <= 0 || !ids.Add(rule.id) ||
                    rule.trigger == BattleEventType.None)
                {
                    throw new InvalidOperationException($"规则 ID 或触发器无效：{rule.id}");
                }

                if (rule.actions == null || rule.actions.Count == 0)
                {
                    throw new InvalidOperationException($"规则 {rule.id} 至少需要一个 Action。");
                }

                for (int actionIndex = 0; actionIndex < rule.actions.Count; actionIndex++)
                {
                    BattleActionAuthoring action = rule.actions[actionIndex] ??
                        throw new InvalidOperationException($"规则 {rule.id} 包含空 Action。");
                    ValidateTarget(action.target, $"规则 {rule.id} Action");
                    ValidateValue(action.value, $"规则 {rule.id} Action");
                }

                if (rule.conditions == null)
                {
                    continue;
                }

                for (int conditionIndex = 0;
                     conditionIndex < rule.conditions.Count;
                     conditionIndex++)
                {
                    BattleConditionAuthoring condition = rule.conditions[conditionIndex] ??
                        throw new InvalidOperationException($"规则 {rule.id} 包含空 Condition。");
                    ValidateTarget(condition.target, $"规则 {rule.id} Condition");
                }
            }
        }

        private void ValidateSkills()
        {
            if (UsesGeneratedSkillAssets)
            {
                ValidateGeneratedSkills();
                return;
            }

            if (skills == null || skills.Count == 0)
            {
                throw new InvalidOperationException("技能列表不能为空。");
            }

            var ruleIds = new HashSet<int>();
            for (int index = 0; index < rules.Count; index++)
            {
                ruleIds.Add(rules[index].id);
            }

            var skillIds = new HashSet<int>();
            for (int index = 0; index < skills.Count; index++)
            {
                BattleSkillAuthoring skill = skills[index] ??
                    throw new InvalidOperationException($"技能配置第 {index} 项为空。");
                if (skill.id <= 0 || !skillIds.Add(skill.id) || skill.cost < 0)
                {
                    throw new InvalidOperationException($"技能 ID、消耗无效或重复：{skill.id}");
                }

                if (skill.ruleIds == null || skill.ruleIds.Count == 0)
                {
                    throw new InvalidOperationException($"技能 {skill.id} 至少需要一条规则。");
                }

                var localRules = new HashSet<int>();
                for (int ruleIndex = 0; ruleIndex < skill.ruleIds.Count; ruleIndex++)
                {
                    int ruleId = skill.ruleIds[ruleIndex];
                    if (!ruleIds.Contains(ruleId) || !localRules.Add(ruleId))
                    {
                        throw new InvalidOperationException(
                            $"技能 {skill.id} 引用了不存在或重复的规则 {ruleId}。");
                    }
                }
            }

            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                for (int skillIndex = 0;
                     skillIndex < units[unitIndex].skillIds.Count;
                     skillIndex++)
                {
                    int skillId = units[unitIndex].skillIds[skillIndex];
                    if (!skillIds.Contains(skillId))
                    {
                        throw new InvalidOperationException(
                            $"单位 {units[unitIndex].unitId} 引用了不存在的技能 {skillId}。");
                    }
                }
            }
        }

        private void ValidateBuffs()
        {
            if (buffs == null)
            {
                throw new InvalidOperationException("Buff 列表不能为空。");
            }

            var ruleIds = new HashSet<int>();
            if (UsesGeneratedSkillAssets)
            {
                for (int assetIndex = 0;
                     assetIndex < generatedSkillAssets.Count;
                     assetIndex++)
                {
                    IReadOnlyList<BattleRuleAuthoring> assetRules =
                        generatedSkillAssets[assetIndex].Logic.Rules;
                    for (int ruleIndex = 0; ruleIndex < assetRules.Count; ruleIndex++)
                    {
                        ruleIds.Add(assetRules[ruleIndex].id);
                    }
                }
            }
            else
            {
                for (int index = 0; index < rules.Count; index++)
                {
                    ruleIds.Add(rules[index].id);
                }
            }

            var ids = new HashSet<int>();
            for (int index = 0; index < buffs.Count; index++)
            {
                BattleBuffAuthoring buff = buffs[index] ??
                    throw new InvalidOperationException($"Buff 配置第 {index} 项为空。");
                if (buff.id <= 0 || !ids.Add(buff.id) ||
                    buff.durationRounds <= 0 || buff.maxStack <= 0)
                {
                    throw new InvalidOperationException($"Buff 数据无效或 ID 重复：{buff.id}");
                }

                for (int ruleIndex = 0;
                     ruleIndex < (buff.ruleIds?.Count ?? 0);
                     ruleIndex++)
                {
                    if (!ruleIds.Contains(buff.ruleIds[ruleIndex]))
                    {
                        throw new InvalidOperationException(
                            $"Buff {buff.id} 引用了不存在的规则 {buff.ruleIds[ruleIndex]}。");
                    }
                }
            }
        }

        private void ValidateGeneratedSkills()
        {
            var skillIds = new HashSet<int>();
            bool hasActiveSkill = false;
            for (int index = 0; index < generatedSkillAssets.Count; index++)
            {
                TurnBasedSkillAsset asset = generatedSkillAssets[index] ??
                    throw new InvalidOperationException(
                        $"独立技能资产第 {index} 项为空。");
                if (!asset.TryValidate(out string error))
                {
                    throw new InvalidOperationException(error);
                }

                BattleSkillAuthoring skill = asset.Skill;
                if (!skillIds.Add(skill.id))
                {
                    throw new InvalidOperationException(
                        $"独立技能 ID 重复：{skill.id}");
                }
                hasActiveSkill |= skill.activeCommand;
            }

            if (!hasActiveSkill)
            {
                throw new InvalidOperationException("战斗至少需要一个可主动释放的技能。");
            }

            for (int unitIndex = 0; unitIndex < units.Count; unitIndex++)
            {
                BattleUnitAuthoring unit = units[unitIndex];
                bool hasUnitActiveSkill = false;
                for (int skillIndex = 0; skillIndex < unit.skillIds.Count; skillIndex++)
                {
                    int skillId = unit.skillIds[skillIndex];
                    if (!skillIds.Contains(skillId))
                    {
                        throw new InvalidOperationException(
                            $"单位 {unit.unitId} 引用了不存在的独立技能 {skillId}。");
                    }

                    BattleSkillAuthoring skill = FindSkill(skillId);
                    hasUnitActiveSkill |= skill != null && skill.activeCommand;
                }

                if (!hasUnitActiveSkill)
                {
                    throw new InvalidOperationException(
                        $"单位 {unit.unitId} 至少需要一个可主动释放的技能。");
                }
            }
        }

        private static void ValidateTarget(
            BattleTargetAuthoring target,
            string owner)
        {
            if (target == null || target.count < 0)
            {
                throw new InvalidOperationException($"{owner} 的目标选择器无效。");
            }
        }

        private static void ValidateValue(
            BattleValueAuthoring value,
            string owner)
        {
            if (value == null || value.scaleBasisPoint < 0 ||
                value.useDynamicScale && value.dynamicScale == null ||
                value.useDynamicScale &&
                value.dynamicScale.source == ValueSourceType.Constant &&
                value.dynamicScale.constant < 0 ||
                value.hasMinimum && value.hasMaximum &&
                value.minimum > value.maximum)
            {
                throw new InvalidOperationException($"{owner} 的数值表达式无效。");
            }
        }

        private ulong CalculateLogicHash()
        {
            var hash = new StableAuthoringHash();
            hash.Add(logicVersion);
            hash.Add(runtimeDataVersion);
            hash.Add(randomSeed);

            hash.Add(units.Count);
            for (int index = 0; index < units.Count; index++)
            {
                BattleUnitAuthoring unit = units[index];
                hash.Add(unit.unitId);
                hash.Add(unit.configId);
                hash.Add((int)unit.camp);
                hash.Add(unit.position);
                hash.Add(unit.maxHp);
                hash.Add(unit.attack);
                hash.Add(unit.defense);
                hash.Add(unit.speed);
                hash.Add(unit.critRate);
                hash.Add(unit.critDamage);
                hash.Add(unit.hitRate);
                hash.Add(unit.dodgeRate);
                hash.Add(unit.energy);
                hash.Add(unit.skillIds.Count);
                for (int skillIndex = 0; skillIndex < unit.skillIds.Count; skillIndex++)
                {
                    hash.Add(unit.skillIds[skillIndex]);
                }
            }

            hash.Add(skills.Count);
            for (int index = 0; index < skills.Count; index++)
            {
                BattleSkillAuthoring skill = skills[index];
                hash.Add(skill.id);
                hash.Add((int)skill.costAttribute);
                hash.Add(skill.cost);
                hash.Add((int)skill.targetMode);
                hash.Add(skill.ruleIds.Count);
                for (int ruleIndex = 0; ruleIndex < skill.ruleIds.Count; ruleIndex++)
                {
                    hash.Add(skill.ruleIds[ruleIndex]);
                }
            }

            hash.Add(generatedSkillAssets.Count);
            for (int index = 0; index < generatedSkillAssets.Count; index++)
            {
                TurnBasedSkillAsset asset = generatedSkillAssets[index];
                if (asset == null || asset.Skill == null)
                {
                    continue;
                }

                BattleSkillAuthoring skill = asset.Skill;
                hash.Add(skill.id);
                hash.Add((int)skill.costAttribute);
                hash.Add(skill.cost);
                hash.Add((int)skill.targetMode);
                if (asset.Logic != null)
                {
                    hash.Add(asset.Logic.LogicId);
                    hash.Add(asset.Logic.DataDefinitions.Count);
                    for (int dataIndex = 0;
                         dataIndex < asset.Logic.DataDefinitions.Count;
                         dataIndex++)
                    {
                        BattleLogicDataAuthoring data =
                            asset.Logic.DataDefinitions[dataIndex];
                        hash.Add(data.key);
                        hash.Add(data.name);
                        hash.Add((int)data.kind);
                        hash.Add((int)data.valueType);
                        hash.Add((int)(data.kind == BattleLogicDataKind.Parameter
                            ? BattleLogicVariableScope.Invocation
                            : data.scope));
                        hash.Add(data.defaultValue);
                    }
                    hash.Add(asset.Logic.Rules.Count);
                    for (int ruleIndex = 0;
                         ruleIndex < asset.Logic.Rules.Count;
                         ruleIndex++)
                    {
                        AddRuleHash(ref hash, asset.Logic.Rules[ruleIndex]);
                    }
                }
            }

            hash.Add(rules.Count);
            for (int index = 0; index < rules.Count; index++)
            {
                AddRuleHash(ref hash, rules[index]);
            }

            hash.Add(buffs.Count);
            for (int index = 0; index < buffs.Count; index++)
            {
                BattleBuffAuthoring buff = buffs[index];
                hash.Add(buff.id);
                hash.Add(buff.durationRounds);
                hash.Add(buff.maxStack);
                hash.Add((int)buff.stackRule);
                hash.Add(buff.tags);
                hash.Add(buff.ruleIds.Count);
                for (int ruleIndex = 0; ruleIndex < buff.ruleIds.Count; ruleIndex++)
                {
                    hash.Add(buff.ruleIds[ruleIndex]);
                }
            }

            return hash.Value;
        }

        private static void AddRuleHash(
            ref StableAuthoringHash hash,
            BattleRuleAuthoring rule)
        {
            hash.Add(rule.id);
            hash.Add((int)rule.trigger);
            hash.Add((int)rule.phase);
            hash.Add(rule.priority);
            hash.Add(rule.ownerMustBeAlive);
            int conditionCount = rule.conditions?.Count ?? 0;
            hash.Add(conditionCount);
            for (int index = 0; index < conditionCount; index++)
            {
                BattleConditionAuthoring condition = rule.conditions[index];
                hash.Add((int)condition.type);
                AddTargetHash(ref hash, condition.target);
                hash.Add((int)condition.comparison);
                hash.Add(condition.value);
                hash.Add((int)condition.attribute);
                hash.Add(condition.referenceId);
                hash.Add(condition.negate);
                if (condition.type == CompiledConditionType.LogicValueCompare)
                {
                    AddValueHash(ref hash, condition.leftValue);
                    AddValueHash(ref hash, condition.rightValue);
                }
            }

            int actionCount = rule.actions?.Count ?? 0;
            hash.Add(actionCount);
            for (int index = 0; index < actionCount; index++)
            {
                BattleActionAuthoring action = rule.actions[index];
                hash.Add((int)action.type);
                AddTargetHash(ref hash, action.target);
                AddValueHash(ref hash, action.value);
                hash.Add((int)action.attribute);
                hash.Add(action.referenceId);
                hash.Add((int)action.flags);
                hash.Add(action.useDynamicReference);
                if (action.useDynamicReference)
                {
                    AddValueHash(ref hash, action.referenceValue);
                }
            }
        }

        private static void AddTargetHash(
            ref StableAuthoringHash hash,
            BattleTargetAuthoring target)
        {
            hash.Add((int)target.type);
            hash.Add(target.count);
            hash.Add(target.includeDead);
            hash.Add(target.configIdFilter);
        }

        private static void AddValueHash(
            ref StableAuthoringHash hash,
            BattleValueAuthoring value)
        {
            hash.Add((int)value.source);
            hash.Add(value.constant);
            hash.Add((int)value.attribute);
            hash.Add(value.referenceId);
            hash.Add(value.scaleBasisPoint);
            hash.Add(value.offset);
            hash.Add(value.hasMinimum);
            hash.Add(value.minimum);
            hash.Add(value.hasMaximum);
            hash.Add(value.maximum);
            hash.Add(value.useDynamicScale);
            if (value.useDynamicScale && value.dynamicScale != null)
            {
                hash.Add((int)value.dynamicScale.source);
                hash.Add(value.dynamicScale.constant);
                hash.Add((int)value.dynamicScale.attribute);
                hash.Add(value.dynamicScale.referenceId);
            }
        }

        private struct StableAuthoringHash
        {
            private const ulong Offset = 14695981039346656037UL;
            private const ulong Prime = 1099511628211UL;
            private ulong value;

            public ulong Value => value == 0 ? Offset : value;

            public void Add(bool item) => Add(item ? 1L : 0L);
            public void Add(int item) => Add((long)item);

            public void Add(long item)
            {
                EnsureInitialized();
                unchecked
                {
                    ulong bits = (ulong)item;
                    for (int index = 0; index < sizeof(long); index++)
                    {
                        value ^= (byte)(bits >> (index * 8));
                        value *= Prime;
                    }
                }
            }

            public void Add(string item)
            {
                EnsureInitialized();
                string safe = item ?? string.Empty;
                Add(safe.Length);
                unchecked
                {
                    for (int index = 0; index < safe.Length; index++)
                    {
                        char character = safe[index];
                        value ^= (byte)character;
                        value *= Prime;
                        value ^= (byte)(character >> 8);
                        value *= Prime;
                    }
                }
            }

            private void EnsureInitialized()
            {
                if (value == 0)
                {
                    value = Offset;
                }
            }
        }
    }
}
