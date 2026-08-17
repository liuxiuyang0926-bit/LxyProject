using System;
using System.Collections.Generic;
using Game.Battle.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Battle.View
{
    public sealed class BattleViewWorld :
        IBattleEventSink,
        IDisposable
    {
        private readonly BattleWorld world;
        private readonly EntityView entityPrefab;
        private readonly Transform root;
        private readonly Dictionary<int, EntityView> views =
            new Dictionary<int, EntityView>();

        public BattleViewWorld(
            BattleWorld world,
            EntityView entityPrefab,
            Transform root)
        {
            this.world = world ??
                         throw new ArgumentNullException(nameof(world));
            this.entityPrefab = entityPrefab;
            this.root = root;
        }

        public void CreateEntityView(int entityId, Color color)
        {
            if (views.ContainsKey(entityId))
            {
                return;
            }

            EntityView view;
            if (entityPrefab != null)
            {
                view = Object.Instantiate(entityPrefab, root);
            }
            else
            {
                GameObject cube = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                cube.transform.SetParent(root, false);
                view = cube.AddComponent<EntityView>();
            }

            view.name = $"EntityView_{entityId}";
            view.Bind(world, entityId, color);
            views.Add(entityId, view);
        }

        public void ProcessEvents(
            IReadOnlyList<BattleEvent> battleEvents)
        {
            for (int index = 0; index < battleEvents.Count; index++)
            {
                BattleEvent battleEvent = battleEvents[index];
                switch (battleEvent.Type)
                {
                    case BattleEventType.SkillStarted:
                        if (views.TryGetValue(
                                battleEvent.SourceEntityId,
                                out EntityView caster))
                        {
                            caster.PlaySkill(battleEvent.IntValue1);
                        }
                        break;

                    case BattleEventType.Damage:
                    case BattleEventType.BuffTicked:
                        if (views.TryGetValue(
                                battleEvent.TargetEntityId,
                                out EntityView target))
                        {
                            target.PlayHit();
                        }
                        break;

                    case BattleEventType.EntityDied:
                        if (views.TryGetValue(
                                battleEvent.TargetEntityId,
                                out EntityView dead))
                        {
                            dead.PlayDead();
                        }
                        break;
                }
            }
        }

        public void TickVisual(float deltaTime)
        {
            foreach (EntityView view in views.Values)
            {
                if (view != null)
                {
                    view.TickVisual(deltaTime);
                }
            }
        }

        public void Dispose()
        {
            foreach (EntityView view in views.Values)
            {
                if (view != null)
                {
                    Object.Destroy(view.gameObject);
                }
            }

            views.Clear();
        }
    }
}
