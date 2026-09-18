using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.TurnBased.Authoring
{
    public enum BattleFormationUnitSource
    {
        None = 0,
        Hero = 1,
        Monster = 2,
    }

    [Serializable]
    public sealed class TurnBasedFormationSlotAuthoring
    {
        [SerializeField, Range(0, 17)] private int slotIndex;
        [SerializeField] private BattleFormationUnitSource source;
        [SerializeField, Min(0)] private long configId;
        [SerializeField] private Vector3 modelOffset;
        [SerializeField] private Vector3 modelScale = Vector3.one;

        public int SlotIndex => slotIndex;
        public bool IsAttacker => slotIndex < 9;
        public BattleFormationUnitSource Source => source;
        public long ConfigId => configId;
        public Vector3 ModelOffset => modelOffset;
        public Vector3 ModelScale => modelScale;

        public void SetIndex(int value)
        {
            slotIndex = Mathf.Clamp(value, 0, 17);
        }

        public void Configure(BattleFormationUnitSource unitSource, long id)
        {
            source = unitSource;
            configId = unitSource == BattleFormationUnitSource.None
                ? 0
                : Math.Max(0, id);
        }
    }

    [CreateAssetMenu(
        fileName = "TurnBasedBattleFormation",
        menuName = "LxyDemo/战斗/18 位战斗演示阵容")]
    public sealed class TurnBasedBattleFormationAsset : ScriptableObject
    {
        public const int SlotCount = 18;

        [SerializeField] private string displayName = "18 位战斗演示";
        [SerializeField] private List<TurnBasedFormationSlotAuthoring> slots =
            new List<TurnBasedFormationSlotAuthoring>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? name
            : displayName;
        public IReadOnlyList<TurnBasedFormationSlotAuthoring> Slots => slots;

        public TurnBasedFormationSlotAuthoring GetSlot(int index)
        {
            EnsureSlots();
            return index >= 0 && index < slots.Count ? slots[index] : null;
        }

        public void EnsureSlots()
        {
            slots = slots ?? new List<TurnBasedFormationSlotAuthoring>();
            while (slots.Count < SlotCount)
            {
                slots.Add(new TurnBasedFormationSlotAuthoring());
            }
            if (slots.Count > SlotCount)
            {
                slots.RemoveRange(SlotCount, slots.Count - SlotCount);
            }
            for (int index = 0; index < slots.Count; index++)
            {
                slots[index] = slots[index] ??
                    new TurnBasedFormationSlotAuthoring();
                slots[index].SetIndex(index);
            }
        }

        public void ResetToSample()
        {
            slots = new List<TurnBasedFormationSlotAuthoring>();
            EnsureSlots();
            slots[0].Configure(BattleFormationUnitSource.Hero, 101);
            slots[9].Configure(BattleFormationUnitSource.Monster, 1000001);
        }

        private void OnValidate()
        {
            EnsureSlots();
        }
    }
}
