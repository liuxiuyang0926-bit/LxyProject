using System;
using System.Collections.Generic;
using Game.Battle.Core;
using Game.Battle.Core.Math;
using UnityEngine;

namespace Game.Battle.Config
{
    [Serializable]
    public sealed class BattleSkillFrameOperationConfig
    {
        [Min(0)] public int frame = 4;
        /// <summary>
        /// 公开的操作类型数据。
        /// </summary>
        public BattleSkillOperationType operationType =
            BattleSkillOperationType.DamageBox;
        /// <summary>
        /// 公开的hit形状数据。
        /// </summary>
        public BattleCollisionShape hitShape =
            BattleCollisionShape.Aabb;
        [Min(0)] public int damage = 100;
        [Tooltip("0 表示命中时不附加 Buff。")]
        [Min(0)] public int buffId = 2001;
        [Tooltip("伤害盒中心相对施法者的本地 X，10000 = 1 米。")]
        /// <summary>
        /// 公开的偏移X原始值数据。
        /// </summary>
        public long offsetXRaw = 17500;
        [Tooltip("伤害盒中心相对施法者的本地 Y，10000 = 1 米。")]
        /// <summary>
        /// 公开的偏移Y原始值数据。
        /// </summary>
        public long offsetYRaw;
        [Tooltip("伤害盒半宽，10000 = 1 米。")]
        [Min(1)] public long halfWidthRaw = 17500;
        [Tooltip("伤害盒半高，10000 = 1 米。")]
        [Min(1)] public long halfHeightRaw = 10000;
        [Tooltip("OBB 相对角色朝向的角度，10000 = 1 度。")]
        /// <summary>
        /// 公开的旋转角度原始值数据。
        /// </summary>
        public long rotationDegreesRaw;
        [Tooltip("施法者本地位移 X，仅位移操作使用。")]
        /// <summary>
        /// 公开的displacementX原始值数据。
        /// </summary>
        public long displacementXRaw;
        [Tooltip("施法者本地位移 Y，仅位移操作使用。")]
        /// <summary>
        /// 公开的displacementY原始值数据。
        /// </summary>
        public long displacementYRaw;

        /// <summary>
        /// 执行转换为定义相关逻辑。
        /// </summary>
        public BattleSkillFrameOperationDefinition ToDefinition(
            int order)
        {
            return new BattleSkillFrameOperationDefinition(
                frame,
                order,
                operationType,
                hitShape,
                damage,
                buffId,
                new FPVector2(
                    FP.FromRaw(offsetXRaw),
                    FP.FromRaw(offsetYRaw)),
                new FPVector2(
                    FP.FromRaw(halfWidthRaw),
                    FP.FromRaw(halfHeightRaw)),
                FP.FromRaw(rotationDegreesRaw),
                new FPVector2(
                    FP.FromRaw(displacementXRaw),
                    FP.FromRaw(displacementYRaw)));
        }
    }

    [Serializable]
    public sealed class BattleSkillConfig
    {
        [Min(1)] public int id = 1001;
        /// <summary>
        /// 公开的display名称数据。
        /// </summary>
        public string displayName = "普通攻击";
        [Min(1)] public int totalFrames = 12;
        [Min(0)] public int cooldownFrames = 20;
        /// <summary>
        /// 公开的帧操作数据。
        /// </summary>
        public List<BattleSkillFrameOperationConfig> frameOperations =
            new List<BattleSkillFrameOperationConfig>
            {
                new BattleSkillFrameOperationConfig(),
            };

        // 兼容旧配置资源；存在新帧操作后不再读取这些字段。
        [HideInInspector] public int hitFrame = 4;
        [HideInInspector] public int damage = 100;
        [HideInInspector] public long rangeRaw = 35000;
        [HideInInspector] public int buffId = 2001;

        /// <summary>
        /// 执行转换为定义相关逻辑。
        /// </summary>
        public BattleSkillDefinition ToDefinition()
        {
            List<BattleSkillFrameOperationConfig> source =
                frameOperations;
            if (source == null || source.Count == 0)
            {
                source = new List<BattleSkillFrameOperationConfig>
                {
                    new BattleSkillFrameOperationConfig
                    {
                        frame = hitFrame,
                        damage = damage,
                        buffId = buffId,
                        offsetXRaw = rangeRaw / 2L,
                        halfWidthRaw = rangeRaw / 2L,
                    },
                };
            }

            var definitions =
                new BattleSkillFrameOperationDefinition[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                if (source[index] == null)
                {
                    throw new InvalidOperationException(
                        $"技能 {id} 的第 {index} 个帧操作为空。");
                }

                definitions[index] = source[index].ToDefinition(index);
            }

            return new BattleSkillDefinition(
                id,
                displayName,
                totalFrames,
                cooldownFrames,
                definitions);
        }
    }

    [Serializable]
    public sealed class BattleBuffConfig
    {
        [Min(1)] public int id = 2001;
        /// <summary>
        /// 公开的display名称数据。
        /// </summary>
        public string displayName = "灼烧";
        /// <summary>
        /// 公开的类型数据。
        /// </summary>
        public BattleBuffType type = BattleBuffType.DamageOverTime;
        [Min(1)] public int durationFrames = 60;
        [Tooltip("周期伤害/治疗的间隔帧；移速 Buff 可以为 0。")]
        [Min(0)] public int intervalFrames = 20;
        [Tooltip("伤害/治疗数值，或移速百分比。")]
        /// <summary>
        /// 公开的值数据。
        /// </summary>
        public int value = 20;
        /// <summary>
        /// 公开的栈模式数据。
        /// </summary>
        public BattleBuffStackMode stackMode =
            BattleBuffStackMode.RefreshDuration;
        [Min(1)] public int maxStacks = 1;

        /// <summary>
        /// 执行转换为定义相关逻辑。
        /// </summary>
        public BattleBuffDefinition ToDefinition()
        {
            return new BattleBuffDefinition(
                id,
                displayName,
                type,
                durationFrames,
                intervalFrames,
                value,
                stackMode,
                maxStacks);
        }
    }

    [CreateAssetMenu(
        fileName = "BattleConfigDatabase",
        menuName = "LxyDemo/Battle/Config Database")]
    public sealed class BattleConfigDatabase : ScriptableObject
    {
        [SerializeField]
        private List<BattleSkillConfig> skills =
            new List<BattleSkillConfig>();

        [SerializeField]
        private List<BattleBuffConfig> buffs =
            new List<BattleBuffConfig>();

        /// <summary>
        /// 向调用方提供Skills。
        /// </summary>
        public IReadOnlyList<BattleSkillConfig> Skills => skills;
        /// <summary>
        /// 向调用方提供Buffs。
        /// </summary>
        public IReadOnlyList<BattleBuffConfig> Buffs => buffs;

        /// <summary>
        /// 构建目录。
        /// </summary>
        public BattleConfigCatalog BuildCatalog()
        {
            var catalog = new BattleConfigCatalog();
            for (int index = 0; index < buffs.Count; index++)
            {
                catalog.AddBuff(buffs[index].ToDefinition());
            }

            for (int index = 0; index < skills.Count; index++)
            {
                catalog.AddSkill(skills[index].ToDefinition());
            }

            return catalog;
        }

        /// <summary>
        /// 尝试构建目录，并返回是否成功。
        /// </summary>
        public bool TryBuildCatalog(
            out BattleConfigCatalog catalog,
            out string error)
        {
            try
            {
                catalog = BuildCatalog();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                catalog = null;
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// 重置ToDefaults。
        /// </summary>
        public void ResetToDefaults()
        {
            skills.Clear();
            buffs.Clear();
            buffs.Add(new BattleBuffConfig());
            skills.Add(new BattleSkillConfig());
        }

        /// <summary>
        /// 添加默认值技能。
        /// </summary>
        public void AddDefaultSkill()
        {
            int id = 1001;
            while (skills.Exists(item => item != null && item.id == id))
            {
                id++;
            }

            skills.Add(new BattleSkillConfig { id = id });
        }

        /// <summary>
        /// 添加默认值增益。
        /// </summary>
        public void AddDefaultBuff()
        {
            int id = 2001;
            while (buffs.Exists(item => item != null && item.id == id))
            {
                id++;
            }

            buffs.Add(new BattleBuffConfig { id = id });
        }

        /// <summary>
        /// 移除技能At。
        /// </summary>
        public void RemoveSkillAt(int index)
        {
            if (index >= 0 && index < skills.Count)
            {
                skills.RemoveAt(index);
            }
        }

        /// <summary>
        /// 移除增益At。
        /// </summary>
        public void RemoveBuffAt(int index)
        {
            if (index >= 0 && index < buffs.Count)
            {
                buffs.RemoveAt(index);
            }
        }

        /// <summary>
        /// 创建默认值目录。
        /// </summary>
        public static BattleConfigCatalog CreateDefaultCatalog()
        {
            var catalog = new BattleConfigCatalog();
            catalog.AddBuff(new BattleBuffConfig().ToDefinition());
            catalog.AddSkill(new BattleSkillConfig().ToDefinition());
            return catalog;
        }
    }
}
