using Game.Battle.Core;
using Game.Battle.Core.Math;
using UnityEngine;

namespace Game.Battle.View
{
    public sealed class EntityView : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;

        [SerializeField]
        [Min(0.1f)]
        private float positionLerpSpeed = 15f;

        private BattleWorld world;
        private int entityId;
        private Material runtimeMaterial;

        public int EntityId => entityId;

        public void Bind(
            BattleWorld battleWorld,
            int battleEntityId,
            Color color)
        {
            world = battleWorld;
            entityId = battleEntityId;
            if (TryGetComponent(out Renderer targetRenderer))
            {
                runtimeMaterial = targetRenderer.material;
                runtimeMaterial.color = color;
            }

            SnapToLogicPosition();
        }

        public void TickVisual(float deltaTime)
        {
            if (world == null ||
                !world.Transforms.TryGet(
                    entityId,
                    out TransformComponent logicTransform))
            {
                return;
            }

            Vector3 target = ToUnity(logicTransform.Position);
            float factor = 1f - Mathf.Exp(
                -positionLerpSpeed * Mathf.Max(0f, deltaTime));
            transform.position = Vector3.Lerp(
                transform.position,
                target,
                factor);

            Vector3 forward = ToUnityDirection(logicTransform.Forward);
            if (forward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    forward,
                    Vector3.up);
            }
        }

        public void PlaySkill(int skillId)
        {
            if (animator != null)
            {
                animator.SetTrigger("Skill_" + skillId);
            }
        }

        public void PlayHit()
        {
            if (animator != null)
            {
                animator.SetTrigger("Hit");
            }
        }

        public void PlayDead()
        {
            if (animator != null)
            {
                animator.SetTrigger("Dead");
            }

            if (runtimeMaterial != null)
            {
                runtimeMaterial.color = Color.gray;
            }
        }

        private void SnapToLogicPosition()
        {
            if (world != null &&
                world.Transforms.TryGet(
                    entityId,
                    out TransformComponent logicTransform))
            {
                transform.position = ToUnity(logicTransform.Position);
            }
        }

        private static Vector3 ToUnity(
            FPVector2 position)
        {
            return new Vector3(
                ToFloat(position.X),
                0.5f,
                ToFloat(position.Y));
        }

        private static Vector3 ToUnityDirection(
            FPVector2 direction)
        {
            return new Vector3(
                ToFloat(direction.X),
                0f,
                ToFloat(direction.Y));
        }

        private static float ToFloat(FP value) =>
            value.RawValue / (float)FP.Precision;

        private void OnDestroy()
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }
    }
}
