using System.Collections.Generic;
using UnityEngine;

namespace Game.Battle.TurnBased.Authoring
{
    public sealed partial class TurnBasedSkillLogicAsset
    {
        [SerializeField, HideInInspector] private int percentageVersion = 1;
        public bool UpgradeLegacyPercentages()
        {
            if (percentageVersion >= 1) return false;
            var visited = new HashSet<BattleValueAuthoring>();
            BattlePercentageAuthoring.MigrateRules(rules, dataDefinitions, visited);
            if (tracks != null) foreach (var track in tracks)
                if (track != null) BattlePercentageAuthoring.MigrateRules(track.rules, dataDefinitions, visited);
            if (dataDefinitions != null) foreach (var data in dataDefinitions)
                if (data != null && data.valueType == RuntimeData.BattleLogicDataValueType.BasisPoint)
                    data.defaultValue /= 100;
            percentageVersion = 1;
            return true;
        }
    }

    public sealed partial class TurnBasedBattleDefinition
    {
        [SerializeField, HideInInspector] private int percentageVersion = 1;
        public bool UpgradeLegacyPercentages()
        {
            if (percentageVersion >= 1) return false;
            BattlePercentageAuthoring.MigrateRules(rules, null, new HashSet<BattleValueAuthoring>());
            if (units != null) foreach (var unit in units)
            {
                if (unit == null) continue;
                unit.critRate /= 100;
                unit.critDamage /= 100;
                unit.hitRate /= 100;
                unit.dodgeRate /= 100;
            }
            percentageVersion = 1;
            return true;
        }
    }
}
