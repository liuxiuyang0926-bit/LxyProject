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

        /// <summary>
        /// 向调用方提供实体标识。
        /// </summary>
        public int EntityId => entityId;

        /// <summary>
        /// 执行绑定相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行帧推进视觉相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行播放Skill相关逻辑。
        /// </summary>
        public void PlaySkill(int skillId)
        {
            if (animator != null)
            {
                animator.SetTrigger("Skill_" + skillId);
            }
        }

        /// <summary>
        /// 执行播放Hit相关逻辑。
        /// </summary>
        public void PlayHit()
        {
            if (animator != null)
            {
                animator.SetTrigger("Hit");
            }
        }

        /// <summary>
        /// 执行播放Dead相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行Snap转换为Logic位置相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行转换为Unity相关逻辑。
        /// </summary>
        private static Vector3 ToUnity(
            FPVector2 position)
        {
            return new Vector3(
                ToFloat(position.X),
                0.5f,
                ToFloat(position.Y));
        }

        /// <summary>
        /// 执行转换为UnityDirection相关逻辑。
        /// </summary>
        private static Vector3 ToUnityDirection(
            FPVector2 direction)
        {
            return new Vector3(
                ToFloat(direction.X),
                0f,
                ToFloat(direction.Y));
        }

        /// <summary>
        /// 执行转换为浮点数相关逻辑。
        /// </summary>
        private static float ToFloat(FP value) =>
            value.RawValue / (float)FP.Precision;

        /// <summary>
        /// 释放持有的资源并解除事件订阅。
        /// </summary>
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
