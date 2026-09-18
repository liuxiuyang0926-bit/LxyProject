using System;
using Game.Battle.TurnBased.Core;
using Game.Battle.TurnBased.Core.Session;
using Game.Battle.TurnBased.Core.Trace;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;

namespace Game.Battle.TurnBased.Application
{
    public sealed class BattleApplication
    {
        private readonly IBattleRuntimeDatabase database;

        public BattleApplication(IBattleRuntimeDatabase database)
        {
            this.database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public BattleSession CreateSession(
            BattleUnit[] initialUnits,
            int seed,
            BattleExecutionLimits? limits = null,
            BattleTraceLevel traceLevel = BattleTraceLevel.Normal)
        {
            if (initialUnits == null || initialUnits.Length == 0)
            {
                throw new ArgumentException("初始战斗单位不能为空。", nameof(initialUnits));
            }

            var world = new BattleWorld();
            for (int index = 0; index < initialUnits.Length; index++)
            {
                world.AddUnit(initialUnits[index]);
            }

            var context = new BattleContext(world, database, seed, limits, traceLevel);
            return new BattleSession(context);
        }
    }
}
