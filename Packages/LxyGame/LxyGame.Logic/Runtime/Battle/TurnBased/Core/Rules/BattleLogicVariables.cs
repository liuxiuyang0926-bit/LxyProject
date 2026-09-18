using System;
using System.Collections.Generic;
using Game.Battle.Core;
using Game.Battle.TurnBased.Domain;

namespace Game.Battle.TurnBased.Core.Rules
{
    internal readonly struct SkillVariableAddress :
        IComparable<SkillVariableAddress>,
        IEquatable<SkillVariableAddress>
    {
        public SkillVariableAddress(long sourceInstanceId, BattleVariableKey key)
        {
            if (sourceInstanceId <= 0 || !key.IsValid)
            {
                throw new ArgumentException("技能变量地址无效。");
            }

            SourceInstanceId = sourceInstanceId;
            Key = key;
        }

        public long SourceInstanceId { get; }
        public BattleVariableKey Key { get; }

        public int CompareTo(SkillVariableAddress other)
        {
            int result = SourceInstanceId.CompareTo(other.SourceInstanceId);
            return result != 0 ? result : Key.CompareTo(other.Key);
        }

        public bool Equals(SkillVariableAddress other) =>
            SourceInstanceId == other.SourceInstanceId && Key == other.Key;

        public override bool Equals(object obj) =>
            obj is SkillVariableAddress other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (SourceInstanceId.GetHashCode() * 397) ^ Key.GetHashCode();
            }
        }
    }

    /// <summary>
    /// 技能实例级持久变量。地址由运行时技能实例 ID 与策划变量 Key 共同组成，
    /// 使用稳定有序容器，保证哈希、回放和跨端执行顺序一致。
    /// </summary>
    public sealed class BattleLogicVariableStore
    {
        private readonly SortedDictionary<SkillVariableAddress, long> skillValues =
            new SortedDictionary<SkillVariableAddress, long>();

        public long GetSkill(
            long sourceInstanceId,
            BattleVariableKey key,
            long defaultValue = 0)
        {
            var address = new SkillVariableAddress(sourceInstanceId, key);
            return skillValues.TryGetValue(address, out long value)
                ? value
                : defaultValue;
        }

        public void SetSkill(long sourceInstanceId, BattleVariableKey key, long value)
        {
            skillValues[new SkillVariableAddress(sourceInstanceId, key)] = value;
        }

        public long AddSkill(
            long sourceInstanceId,
            BattleVariableKey key,
            long delta,
            long defaultValue = 0)
        {
            long value = checked(GetSkill(sourceInstanceId, key, defaultValue) + delta);
            SetSkill(sourceInstanceId, key, value);
            return value;
        }

        internal void AddToHash(ref BattleHash hash)
        {
            hash.Add(skillValues.Count);
            foreach (KeyValuePair<SkillVariableAddress, long> pair in skillValues)
            {
                hash.Add(pair.Key.SourceInstanceId);
                hash.Add(pair.Key.Key.Value);
                hash.Add(pair.Value);
            }
        }

        internal void Clear() => skillValues.Clear();
    }

    /// <summary>
    /// 一次规则触发期间有效的临时变量。RuleEngine 复用此缓冲区，避免每次触发分配。
    /// </summary>
    internal sealed class BattleInvocationVariableBuffer
    {
        private readonly SortedDictionary<BattleVariableKey, long> values =
            new SortedDictionary<BattleVariableKey, long>();

        public long Get(BattleVariableKey key, long defaultValue = 0)
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("单次触发变量 Key 无效。", nameof(key));
            }

            return values.TryGetValue(key, out long value) ? value : defaultValue;
        }

        public void Set(BattleVariableKey key, long value)
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("单次触发变量 Key 无效。", nameof(key));
            }

            values[key] = value;
        }

        public long Add(BattleVariableKey key, long delta, long defaultValue = 0)
        {
            long value = checked(Get(key, defaultValue) + delta);
            Set(key, value);
            return value;
        }

        public void Clear() => values.Clear();
    }
}
