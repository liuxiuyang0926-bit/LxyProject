using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.RuntimeData;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Battle.TurnBased.Authoring
{
    [Serializable]
    public sealed class BattleLogicDataAuthoring
    {
        [LabelText("数据名称"), Required,
         Tooltip("供策划和生成代码识别的稳定英文名，例如 damage_rate。")]
        public string name = "new_value";
        [LabelText("数据 Key"), Min(1),
         Tooltip("技能逻辑内部唯一且发布后不可复用。建议参数用 1001 段、技能变量用 2001 段、临时变量用 3001 段。")]
        public int key = 1001;
        [LabelText("数据种类")]
        public BattleLogicDataKind kind = BattleLogicDataKind.Parameter;
        [LabelText("值类型")]
        public BattleLogicDataValueType valueType = BattleLogicDataValueType.Integer;
        [LabelText("变量作用域"), ShowIf(nameof(IsRuntimeVariable)),
         Tooltip("单次触发：一条规则执行结束后清空；技能实例：同一单位的该技能整场保留。")]
        public BattleLogicVariableScope scope = BattleLogicVariableScope.Invocation;
        [LabelText("默认值"),
         Tooltip("万分比：10000=100%；布尔：0=false、1=true；标识符：填写配置 ID。")]
        public long defaultValue;
        [LabelText("策划说明"), TextArea(1, 3)]
        public string description;

        private bool IsRuntimeVariable => kind == BattleLogicDataKind.RuntimeVariable;

        internal CompiledBattleLogicData Compile() =>
            new CompiledBattleLogicData(
                new Game.Battle.TurnBased.Domain.BattleVariableKey(key),
                name?.Trim(),
                kind,
                valueType,
                kind == BattleLogicDataKind.Parameter
                    ? BattleLogicVariableScope.Invocation
                    : scope,
                defaultValue);
    }

    [Serializable]
    public sealed class BattleLogicOutputAuthoring
    {
        [LabelText("数据 Key"), Tooltip("表现轨道引用的稳定数据键，例如 damage_result。")]
        public string key = "damage_result";
        [LabelText("事件类型")]
        public BattleEventType eventType = BattleEventType.DamageResolved;
        [LabelText("说明"), TextArea(1, 3)]
        public string description = "伤害、暴击、闪避和死亡结果";
    }

    [Serializable]
    public sealed class BattleLogicTrackAuthoring
    {
        [LabelText("轨道名称")]
        public string trackName = "主逻辑轨";
        [LabelText("执行组"), Tooltip("同名组中的多个 Parallel 轨道表达并行逻辑分支。")]
        public string executionGroup = "主流程";
        [LabelText("执行方式")]
        public BattleAuthoringExecutionMode executionMode =
            BattleAuthoringExecutionMode.Sequence;
        [LabelText("规则列表"), ListDrawerSettings(
            ListElementLabelName = "operationName",
            DraggableItems = true,
            ShowItemCount = true,
            AlwaysAddDefaultValue = true)]
        public List<BattleRuleAuthoring> rules = new List<BattleRuleAuthoring>();
    }

    [Serializable]
    public sealed class BattleExpressionClipAuthoring
    {
        [Tooltip("表现编辑器和运行日志中显示的操作标记。")]
        public string operationName = "表现操作";
        [Tooltip("绑定逻辑资产声明的数据键；留空表示不读取逻辑结果。")]
        public string logicOutputKey;
        public BattleExpressionClipType type = BattleExpressionClipType.Animation;
        public BattleExpressionSubject subject = BattleExpressionSubject.Caster;
        [Min(0)] public int startFrame;
        [Min(0)] public int durationFrames;
        [Range(1, 18), Tooltip(
            "仅用于编辑器阵容演示。实际战斗始终移动到技能逻辑选出的动态主目标。")]
        public int previewTargetSlot = 10;
        public string resourceKey;
        [Tooltip("第二资源 Key，例如弹道目标挂点、特效动画或受击特效。")]
        public string secondaryResourceKey;
        [Tooltip("模型挂点名称，例如 Head、Hit 或 Weapon。留空使用模型根节点。")]
        public string anchorKey;
        [Tooltip("镜头或空间操作使用的目标锚点。")]
        public BattleExpressionAnchor anchor = BattleExpressionAnchor.PrimaryTarget;
        public Vector3 offset;
        [Tooltip("弹道终点等双端操作使用的目标相对偏移。")]
        public Vector3 targetOffset;
        public Color color = Color.white;
        public float intensity = 1f;
        [Tooltip("开关类表现的目标状态。")]
        public bool state = true;
        [Tooltip("动作、音频或特效是否循环。")]
        public bool loop;
        [Tooltip("形态序号、样式编号等离散参数。")]
        public int option;
        [Min(0f), Tooltip("震动频率或每秒脉冲次数。")]
        public float frequency = 8f;

        public int EndFrame => startFrame + durationFrames;

        internal CompiledBattleExpressionClip Compile(
            string parallelGroup,
            int authoringOrder)
        {
            return new CompiledBattleExpressionClip(
                type,
                subject,
                startFrame,
                durationFrames,
                resourceKey,
                new BattleExpressionVector(offset.x, offset.y, offset.z),
                new BattleExpressionColor(color.r, color.g, color.b, color.a),
                intensity,
                operationName,
                parallelGroup,
                logicOutputKey,
                previewTargetSlot,
                secondaryResourceKey,
                anchorKey,
                anchor,
                new BattleExpressionVector(
                    targetOffset.x,
                    targetOffset.y,
                    targetOffset.z),
                state,
                loop,
                option,
                frequency,
                authoringOrder);
        }
    }

    [Serializable]
    public sealed class BattleExpressionTrackAuthoring
    {
        [LabelText("轨道名称")]
        public string trackName = "施法者轨道";
        [LabelText("并行组"), Tooltip("同名并行组中的轨道从各自的绝对帧同时执行。")]
        public string parallelGroup = "主并行组";
        [LabelText("轨内方式"), Tooltip("顺序轨不允许帧区间重叠；并行轨允许同轨操作重叠。")]
        public BattleAuthoringExecutionMode executionMode =
            BattleAuthoringExecutionMode.Sequence;
        [LabelText("帧操作列表"), ListDrawerSettings(
            ListElementLabelName = "operationName",
            DraggableItems = true,
            ShowItemCount = true,
            AlwaysAddDefaultValue = true)]
        public List<BattleExpressionClipAuthoring> operations =
            new List<BattleExpressionClipAuthoring>();
    }
}
