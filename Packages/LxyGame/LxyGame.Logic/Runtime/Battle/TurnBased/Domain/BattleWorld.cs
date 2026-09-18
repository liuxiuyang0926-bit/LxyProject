using System;
using System.Collections.Generic;

namespace Game.Battle.TurnBased.Domain
{
    public readonly struct TurnItem
    {
        public TurnItem(UnitId unit, long timelineValue, long speed)
        {
            Unit = unit;
            TimelineValue = timelineValue;
            Speed = speed;
        }

        public UnitId Unit { get; }
        public long TimelineValue { get; }
        public long Speed { get; }
    }

    public sealed class TurnTimeline
    {
        private const long ActionBarDistance = 100000000L;
        private readonly List<TurnItem> items = new List<TurnItem>();

        public UnitId CurrentActor => items.Count == 0 ? UnitId.None : items[0].Unit;
        public IReadOnlyList<TurnItem> Items => items;

        internal void Rebuild(BattleWorld world)
        {
            items.Clear();
            IReadOnlyList<UnitId> unitIds = world.UnitIds;
            for (int index = 0; index < unitIds.Count; index++)
            {
                BattleUnit unit = world.GetUnit(unitIds[index]);
                if (!unit.IsDead)
                {
                    items.Add(new TurnItem(
                        unit.Id,
                        0,
                        System.Math.Max(1, unit.Attributes.Get(AttributeType.Speed))));
                }
            }

            StableSort();
        }

        internal void Advance(BattleWorld world)
        {
            UnitId actor = CurrentActor;
            for (int index = items.Count - 1; index >= 0; index--)
            {
                if (!world.TryGetUnit(items[index].Unit, out BattleUnit unit) || unit.IsDead)
                {
                    items.RemoveAt(index);
                }
            }

            for (int index = 0; index < items.Count; index++)
            {
                if (items[index].Unit != actor)
                {
                    continue;
                }

                long speed = System.Math.Max(1, world.GetUnit(actor).Attributes.Get(AttributeType.Speed));
                long nextValue = checked(items[index].TimelineValue + ActionBarDistance / speed);
                items[index] = new TurnItem(actor, nextValue, speed);
                break;
            }

            StableSort();
        }

        internal void Add(BattleUnit unit)
        {
            if (unit == null || unit.IsDead)
            {
                return;
            }

            items.Add(new TurnItem(
                unit.Id,
                items.Count == 0 ? 0 : items[items.Count - 1].TimelineValue,
                System.Math.Max(1, unit.Attributes.Get(AttributeType.Speed))));
            StableSort();
        }

        private void StableSort()
        {
            items.Sort(Compare);
        }

        private static int Compare(TurnItem left, TurnItem right)
        {
            int value = left.TimelineValue.CompareTo(right.TimelineValue);
            if (value != 0)
            {
                return value;
            }

            value = right.Speed.CompareTo(left.Speed);
            return value != 0 ? value : left.Unit.CompareTo(right.Unit);
        }
    }

    public sealed class GridState
    {
        private readonly SortedDictionary<int, UnitId> occupants =
            new SortedDictionary<int, UnitId>();

        public bool IsOccupied(int position) => occupants.ContainsKey(position);

        public bool TryGetOccupant(int position, out UnitId unit) =>
            occupants.TryGetValue(position, out unit);

        internal void Place(UnitId unit, int position)
        {
            if (occupants.TryGetValue(position, out UnitId current) && current != unit)
            {
                throw new InvalidOperationException($"战斗位置已被占用：{position}");
            }

            Remove(unit);
            occupants[position] = unit;
        }

        internal void Remove(UnitId unit)
        {
            int foundPosition = int.MinValue;
            foreach (KeyValuePair<int, UnitId> pair in occupants)
            {
                if (pair.Value == unit)
                {
                    foundPosition = pair.Key;
                    break;
                }
            }

            if (foundPosition != int.MinValue)
            {
                occupants.Remove(foundPosition);
            }
        }
    }

    public sealed class WaveState
    {
        public int CurrentWave { get; internal set; } = 1;
        public int TotalWaves { get; internal set; } = 1;
    }

    public sealed class BattleCounters
    {
        private readonly SortedDictionary<BattleVariableKey, long> values =
            new SortedDictionary<BattleVariableKey, long>();

        public IEnumerable<KeyValuePair<BattleVariableKey, long>> Items => values;

        public long Get(BattleVariableKey key) =>
            values.TryGetValue(key, out long value) ? value : 0;

        internal void Set(BattleVariableKey key, long value)
        {
            if (!key.IsValid)
            {
                throw new ArgumentException("战斗变量 Key 无效。", nameof(key));
            }

            values[key] = value;
        }

        internal long Add(BattleVariableKey key, long delta)
        {
            long result = checked(Get(key) + delta);
            Set(key, result);
            return result;
        }
    }

    public sealed class BattleWorld
    {
        private readonly Dictionary<UnitId, BattleUnit> units =
            new Dictionary<UnitId, BattleUnit>();
        private readonly List<UnitId> unitIds = new List<UnitId>();

        public BattleWorld()
        {
            TurnTimeline = new TurnTimeline();
            Grid = new GridState();
            Wave = new WaveState();
            Counters = new BattleCounters();
        }

        public TurnTimeline TurnTimeline { get; }
        public GridState Grid { get; }
        public WaveState Wave { get; }
        public BattleCounters Counters { get; }
        public IReadOnlyList<UnitId> UnitIds => unitIds;

        public void AddUnit(BattleUnit unit)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(nameof(unit));
            }

            if (unit.Attributes.Get(AttributeType.MaxHp) <= 0)
            {
                throw new InvalidOperationException($"单位 {unit.Id} 尚未初始化生命值。");
            }

            if (units.ContainsKey(unit.Id))
            {
                throw new InvalidOperationException($"单位 ID 重复：{unit.Id}");
            }

            Grid.Place(unit.Id, unit.Position);
            units.Add(unit.Id, unit);
            unitIds.Add(unit.Id);
            unitIds.Sort();
        }

        public BattleUnit GetUnit(UnitId id)
        {
            if (!units.TryGetValue(id, out BattleUnit unit))
            {
                throw new KeyNotFoundException($"战斗单位不存在：{id}");
            }

            return unit;
        }

        public bool TryGetUnit(UnitId id, out BattleUnit unit) =>
            units.TryGetValue(id, out unit);

        public int CountAlive(BattleCamp camp)
        {
            int count = 0;
            for (int index = 0; index < unitIds.Count; index++)
            {
                BattleUnit unit = units[unitIds[index]];
                if (!unit.IsDead && unit.Camp == camp)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountAlive()
        {
            int count = 0;
            for (int index = 0; index < unitIds.Count; index++)
            {
                if (!units[unitIds[index]].IsDead)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
