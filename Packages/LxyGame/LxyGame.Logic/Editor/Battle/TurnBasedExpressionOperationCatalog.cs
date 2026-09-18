using System.Collections.Generic;
using Game.Battle.TurnBased.RuntimeData;
using UnityEngine;

namespace Game.Battle.Editor
{
    internal readonly struct TurnBasedExpressionOperationDescriptor
    {
        public TurnBasedExpressionOperationDescriptor(
            BattleExpressionClipType type,
            string menuPath,
            string displayName,
            string description,
            int defaultDurationFrames,
            float defaultIntensity = 1f,
            float defaultFrequency = 8f)
        {
            Type = type;
            MenuPath = menuPath;
            DisplayName = displayName;
            Description = description;
            DefaultDurationFrames = Mathf.Max(0, defaultDurationFrames);
            DefaultIntensity = defaultIntensity;
            DefaultFrequency = Mathf.Max(0f, defaultFrequency);
        }

        public BattleExpressionClipType Type { get; }
        public string MenuPath { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int DefaultDurationFrames { get; }
        public float DefaultIntensity { get; }
        public float DefaultFrequency { get; }
    }

    internal static class TurnBasedExpressionOperationCatalog
    {
        private static readonly TurnBasedExpressionOperationDescriptor[] Items =
        {
            Entry(BattleExpressionClipType.Animation, "角色/播放动作", "播放动作",
                "在执行对象的 Animator 或 Spine 动画状态机中播放动作。", 10),
            Entry(BattleExpressionClipType.MoveToTarget, "角色/移动到逻辑目标", "移动到逻辑目标",
                "运行时追踪技能逻辑选出的主目标；编辑器站位只用于演示。", 10),
            Entry(BattleExpressionClipType.MoveHome, "角色/返回原站位", "返回原站位",
                "从当前位置平滑返回战斗开始时记录的站位。", 10),
            Entry(BattleExpressionClipType.SwapPosition, "角色/与目标交换位置", "交换位置",
                "施法者与逻辑主目标同时交换表现位置，不修改权威战斗站位。", 12),
            Entry(BattleExpressionClipType.Summon, "角色/召唤入场", "召唤入场",
                "显示已由战斗逻辑创建的单位并播放入场动作。", 10),
            Entry(BattleExpressionClipType.HitReaction, "角色/触发受击组合", "受击组合",
                "组合播放受击动作、受击特效与音效，具体结果仍来自逻辑事件。", 8),
            Entry(BattleExpressionClipType.Hit, "结算/伤害与飘字", "伤害与飘字",
                "消费伤害事件并展示伤害、暴击、闪避和受击抖动。", 12),
            Entry(BattleExpressionClipType.CheckDead, "结算/死亡表现", "死亡表现",
                "仅在逻辑事件已判定目标死亡时播放死亡表现。", 20),
            Entry(BattleExpressionClipType.LeaveField, "角色/离场", "离场",
                "隐藏执行对象的模型和角色界面。", 0),
            Entry(BattleExpressionClipType.SetUnitVisible, "角色/显示或隐藏模型", "模型显隐",
                "设置执行对象模型的显示状态。", 0),
            Entry(BattleExpressionClipType.SetHudVisible, "角色/显示或隐藏血条", "血条显隐",
                "设置执行对象名称、血条和回合标记的显示状态。", 0),
            Entry(BattleExpressionClipType.SetShadowVisible, "角色/显示或隐藏阴影", "阴影显隐",
                "设置执行对象配置的战斗阴影节点显示状态。", 0),
            Entry(BattleExpressionClipType.SetPetVisible, "角色/显示或隐藏宠物", "宠物显隐",
                "设置执行对象配置的宠物节点显示状态。", 0),
            Entry(BattleExpressionClipType.SetSkin, "角色/切换皮肤", "切换皮肤",
                "切换 Spine 模型皮肤；资源 Key 填写皮肤名称。", 0),
            Entry(BattleExpressionClipType.SwitchForm, "角色/切换形态", "切换形态",
                "切换 UnitView 中预先配置的形态节点。", 0),
            Entry(BattleExpressionClipType.ChangeModel, "角色/替换模型", "替换模型",
                "请求项目表现扩展按资源 Key 替换模型。", 0),
            Entry(BattleExpressionClipType.UnitShake, "角色/人物抖动", "人物抖动",
                "按方向、强度和频率驱动纯表现位移。", 8, 0.15f, 8f),
            Entry(BattleExpressionClipType.ColorFlash, "角色/颜色闪烁", "颜色闪烁",
                "使用 MaterialPropertyBlock 对模型做颜色脉冲。", 5),
            Entry(BattleExpressionClipType.Effect, "特效/挂点特效", "挂点特效",
                "在模型根节点或指定挂点播放特效资源。", 10),
            Entry(BattleExpressionClipType.ProjectileEffect, "特效/弹道特效", "弹道特效",
                "从施法者挂点移动到逻辑主目标挂点。", 12),
            Entry(BattleExpressionClipType.EffectAnimation, "特效/控制特效动画", "特效动画",
                "控制已存在的指定特效播放动画或触发器。", 0),
            Entry(BattleExpressionClipType.RemoveEffect, "特效/移除特效", "移除特效",
                "按资源 Key 结束并回收常驻特效。", 0),
            Entry(BattleExpressionClipType.ToggleLoopEffect, "特效/开关常驻特效", "常驻特效开关",
                "开启或关闭绑定在执行对象上的常驻特效。", 0),
            Entry(BattleExpressionClipType.Audio, "音频/播放音效", "播放音效",
                "在执行对象位置播放一次或循环音效。", 0),
            Entry(BattleExpressionClipType.BackgroundAudio, "音频/播放背景音乐", "播放背景音乐",
                "切换战斗背景音乐；循环状态由开关参数控制。", 0),
            Entry(BattleExpressionClipType.CameraFocus, "镜头/聚焦目标", "镜头聚焦",
                "把战斗镜头平滑移动到选定锚点并调整正交大小。", 12, 4.7f),
            Entry(BattleExpressionClipType.CameraShake, "镜头/震屏", "镜头震屏",
                "按方向、强度和频率产生可重复的镜头震动。", 8, 0.18f, 12f),
            Entry(BattleExpressionClipType.DarkenOthers, "场景/压暗非目标", "压暗非目标",
                "保留执行对象和主目标高亮，压暗其他单位。", 0, 0.7f),
            Entry(BattleExpressionClipType.ClearDark, "场景/清除压暗", "清除压暗",
                "恢复所有单位的颜色和界面显示。", 0),
            Entry(BattleExpressionClipType.SetBackgroundVisible, "场景/显示或隐藏背景", "背景显隐",
                "通过场景表现绑定控制战斗背景显示状态。", 0),
            Entry(BattleExpressionClipType.RefreshGrid, "场景/刷新格子表现", "刷新格子表现",
                "通知项目表现扩展刷新逻辑目标所在格子的视觉状态。", 0),
            Entry(BattleExpressionClipType.BuffText, "飘字/Buff 飘字", "Buff 飘字",
                "消费逻辑输出并显示 Buff、护盾或状态文本。", 12),
            Entry(BattleExpressionClipType.FloatingTip, "飘字/战斗提示", "战斗提示",
                "在执行对象上方显示即时战斗提示。", 30),
            Entry(BattleExpressionClipType.BubbleTip, "飘字/气泡对话", "气泡对话",
                "在执行对象上方显示一段气泡文本。", 45),
            Entry(BattleExpressionClipType.PresentationTimeScale, "时间/表现速度", "表现速度",
                "只调整当前表现播放器的帧推进速度，不修改权威逻辑或 Unity 全局时间。", 15, 0.5f),
        };

        private static readonly GUIContent[] PopupContents = BuildPopupContents();

        public static IReadOnlyList<TurnBasedExpressionOperationDescriptor> All => Items;
        public static GUIContent[] Options => PopupContents;

        public static int IndexOf(BattleExpressionClipType type)
        {
            for (int index = 0; index < Items.Length; index++)
            {
                if (Items[index].Type == type)
                {
                    return index;
                }
            }
            return 0;
        }

        public static TurnBasedExpressionOperationDescriptor Get(
            BattleExpressionClipType type)
        {
            return Items[IndexOf(type)];
        }

        public static string GetDisplayName(BattleExpressionClipType type)
        {
            return Get(type).DisplayName;
        }

        private static TurnBasedExpressionOperationDescriptor Entry(
            BattleExpressionClipType type,
            string menuPath,
            string displayName,
            string description,
            int duration,
            float intensity = 1f,
            float frequency = 8f)
        {
            return new TurnBasedExpressionOperationDescriptor(
                type,
                menuPath,
                displayName,
                description,
                duration,
                intensity,
                frequency);
        }

        private static GUIContent[] BuildPopupContents()
        {
            var result = new GUIContent[Items.Length];
            for (int index = 0; index < Items.Length; index++)
            {
                TurnBasedExpressionOperationDescriptor item = Items[index];
                result[index] = new GUIContent(item.MenuPath, item.Description);
            }
            return result;
        }
    }
}
