using System;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.RuntimeData;
using UnityEngine;
using UnityEngine.Events;

namespace Game.Battle.TurnBased.Presentation
{
    [Serializable]
    public sealed class TurnBasedExpressionModelRequestEvent :
        UnityEvent<TurnBasedUnitView, string>
    {
    }

    /// <summary>
    /// 将通用表现时间轴连接到具体 Battle 场景。背景可直接配置；模型替换和格子刷新
    /// 通过事件转发给项目已有的资源/地图系统，避免通用播放器依赖业务实现。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("LxyDemo/Battle/Turn Based Expression Scene Bindings")]
    public sealed class TurnBasedExpressionSceneBindings :
        MonoBehaviour,
        ITurnBasedExpressionExtension
    {
        [SerializeField] private GameObject[] backgroundRoots =
            Array.Empty<GameObject>();
        [SerializeField] private TurnBasedExpressionModelRequestEvent onChangeModel =
            new TurnBasedExpressionModelRequestEvent();
        [SerializeField] private UnityEvent<int> onRefreshGrid =
            new UnityEvent<int>();

        public bool TryStartExpressionClip(
            in CompiledBattleExpressionClip clip,
            TurnBasedUnitView subject,
            TurnBasedUnitView primaryTarget,
            DamageResolvedEvent damage)
        {
            switch (clip.Type)
            {
                case BattleExpressionClipType.SetBackgroundVisible:
                    if (!HasBackgroundBinding())
                    {
                        return false;
                    }
                    SetBackgroundVisible(clip.State);
                    return true;
                case BattleExpressionClipType.ChangeModel:
                    if (subject == null || onChangeModel == null ||
                        onChangeModel.GetPersistentEventCount() == 0)
                    {
                        return false;
                    }
                    onChangeModel.Invoke(subject, clip.ResourceKey);
                    return true;
                case BattleExpressionClipType.RefreshGrid:
                    TurnBasedUnitView target = primaryTarget ?? subject;
                    if (target?.Unit == null || onRefreshGrid == null ||
                        onRefreshGrid.GetPersistentEventCount() == 0)
                    {
                        return false;
                    }
                    onRefreshGrid.Invoke(target.Unit.Position);
                    return true;
                default:
                    return false;
            }
        }

        public void ResetExpressionPresentation()
        {
            // 背景、模型和格子均为显式状态操作，不在单次技能结束时擅自回滚。
        }

        private void SetBackgroundVisible(bool visible)
        {
            if (backgroundRoots == null)
            {
                return;
            }
            for (int index = 0; index < backgroundRoots.Length; index++)
            {
                GameObject root = backgroundRoots[index];
                if (root != null && root.activeSelf != visible)
                {
                    root.SetActive(visible);
                }
            }
        }

        private bool HasBackgroundBinding()
        {
            if (backgroundRoots == null)
            {
                return false;
            }
            for (int index = 0; index < backgroundRoots.Length; index++)
            {
                if (backgroundRoots[index] != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
