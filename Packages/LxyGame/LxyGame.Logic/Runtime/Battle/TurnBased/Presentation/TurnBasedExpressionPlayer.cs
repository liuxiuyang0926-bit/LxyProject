using System;
using System.Collections;
using System.Collections.Generic;
using Game.Battle.TurnBased.Core.Events;
using Game.Battle.TurnBased.RuntimeData;
using UnityEngine;

namespace Game.Battle.TurnBased.Presentation
{
    /// <summary>
    /// 只消费已经编译的表现时间轴。该播放器可以使用 Unity 时间、镜头与渲染 API，
    /// 但不得写入战斗世界或决定伤害、命中、死亡等权威结果。
    /// </summary>
    public sealed class TurnBasedExpressionPlayer
    {
        private readonly MonoBehaviour coroutineHost;
        private readonly TurnBasedUnitView[] unitViews;
        private readonly ITurnBasedExpressionExtension[] extensions;
        private readonly HashSet<BattleExpressionClipType> unsupportedWarnings =
            new HashSet<BattleExpressionClipType>();
        private readonly HashSet<TurnBasedUnitView> darkenedViews =
            new HashSet<TurnBasedUnitView>();
        private readonly List<Coroutine> activeCoroutines = new List<Coroutine>();

        public TurnBasedExpressionPlayer(
            MonoBehaviour coroutineHost,
            IReadOnlyList<TurnBasedUnitView> battleUnitViews = null)
        {
            this.coroutineHost = coroutineHost ??
                throw new ArgumentNullException(nameof(coroutineHost));
            unitViews = CopyUnitViews(battleUnitViews);
            extensions = FindExtensions(coroutineHost);
        }

        public IEnumerator Play(
            CompiledBattleExpression expression,
            TurnBasedUnitView caster,
            TurnBasedUnitView primaryTarget,
            DamageResolvedEvent damage)
        {
            if (expression == null || caster == null)
            {
                yield break;
            }

            CompiledBattleExpressionClip[] clips = expression.Clips;
            var states = new ClipRuntimeState[clips.Length];
            float secondsPerFrame = 1f / expression.FramesPerSecond;
            float nextFrameTime = Time.realtimeSinceStartup;
            Camera battleCamera = Camera.main;
            Vector3 cameraHomePosition = battleCamera == null
                ? Vector3.zero
                : battleCamera.transform.position;
            float cameraHomeSize = battleCamera != null && battleCamera.orthographic
                ? battleCamera.orthographicSize
                : 0f;
            float cameraHomeFieldOfView = battleCamera == null
                ? 0f
                : battleCamera.fieldOfView;
            bool completed = false;

            try
            {
                for (int frame = 0; frame <= expression.DurationFrames; frame++)
                {
                    for (int index = 0; index < clips.Length; index++)
                    {
                        CompiledBattleExpressionClip clip = clips[index];
                        TurnBasedUnitView subject = ResolveSubject(
                            clip.Subject,
                            caster,
                            primaryTarget);
                        if (subject == null)
                        {
                            continue;
                        }

                        if (frame == clip.StartFrame)
                        {
                            states[index] = CaptureState(
                                subject,
                                primaryTarget,
                                battleCamera);
                            states[index].HandledByExtension = StartExtensions(
                                clip,
                                subject,
                                primaryTarget,
                                damage);
                            if (!states[index].HandledByExtension)
                            {
                                states[index].HandledByBuiltIn = StartClip(
                                    expression,
                                    clip,
                                    subject,
                                    primaryTarget,
                                    damage);
                                if (!states[index].HandledByBuiltIn)
                                {
                                    WarnUnsupported(clip.Type);
                                }
                            }
                        }

                        if (frame >= clip.StartFrame && frame <= clip.EndFrame &&
                            !states[index].HandledByExtension)
                        {
                            UpdateClip(
                                expression,
                                clip,
                                subject,
                                caster,
                                primaryTarget,
                                battleCamera,
                                states[index],
                                frame);
                        }
                    }

                    float playbackSpeed = ResolvePresentationSpeed(clips, frame);
                    nextFrameTime += secondsPerFrame / playbackSpeed;
                    while (Time.realtimeSinceStartup < nextFrameTime)
                    {
                        yield return null;
                    }
                }
                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    StopActiveCoroutines();
                    CancelTransientUnitPresentation(caster, primaryTarget);
                }
                else
                {
                    activeCoroutines.Clear();
                }
                caster.ResetExpressionPose();
                primaryTarget?.ResetExpressionPose();
                ClearDarkening();
                RestoreCamera(
                    battleCamera,
                    cameraHomePosition,
                    cameraHomeSize,
                    cameraHomeFieldOfView);
                for (int index = 0; index < extensions.Length; index++)
                {
                    try
                    {
                        extensions[index]?.ResetExpressionPresentation();
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception, coroutineHost);
                    }
                }
            }
        }

        private bool StartClip(
            CompiledBattleExpression expression,
            in CompiledBattleExpressionClip clip,
            TurnBasedUnitView subject,
            TurnBasedUnitView primaryTarget,
            DamageResolvedEvent damage)
        {
            float duration = Mathf.Max(
                0.05f,
                clip.DurationFrames / (float)expression.FramesPerSecond);
            Color color = ToColor(clip.Color);
            switch (clip.Type)
            {
                case BattleExpressionClipType.Animation:
                    subject.PlayExpressionAnimation(clip.ResourceKey);
                    return true;
                case BattleExpressionClipType.MoveToTarget:
                case BattleExpressionClipType.MoveHome:
                case BattleExpressionClipType.SwapPosition:
                    if (!string.IsNullOrWhiteSpace(clip.ResourceKey))
                    {
                        subject.PlayExpressionAnimation(clip.ResourceKey);
                    }
                    return true;
                case BattleExpressionClipType.Effect:
                    if (clip.Loop)
                    {
                        StartPresentationCoroutine(subject.SetLoopExpressionEffect(
                            clip.ResourceKey,
                            clip.AnchorKey,
                            clip.Offset,
                            true));
                    }
                    else
                    {
                        StartPresentationCoroutine(subject.PlayExpressionEffect(
                            clip.ResourceKey,
                            color,
                            duration,
                            clip.AnchorKey,
                            clip.Offset));
                    }
                    return true;
                case BattleExpressionClipType.ProjectileEffect:
                    StartPresentationCoroutine(subject.PlayExpressionProjectileEffect(
                        primaryTarget,
                        clip.ResourceKey,
                        clip.AnchorKey,
                        clip.SecondaryResourceKey,
                        clip.Offset,
                        clip.TargetOffset,
                        color,
                        duration));
                    return true;
                case BattleExpressionClipType.Audio:
                case BattleExpressionClipType.BackgroundAudio:
                    StartPresentationCoroutine(subject.PlayExpressionAudioAsync(
                        clip.ResourceKey,
                        clip.Loop,
                        clip.Intensity));
                    return true;
                case BattleExpressionClipType.Hit:
                    if (damage != null)
                    {
                        StartPresentationCoroutine(subject.PlayHit(
                            damage.Damage,
                            damage.Critical,
                            damage.Miss,
                            duration,
                            0.13f * Mathf.Max(0f, clip.Intensity)));
                    }
                    return true;
                case BattleExpressionClipType.HitReaction:
                    subject.PlayExpressionAnimation(clip.ResourceKey);
                    if (!string.IsNullOrWhiteSpace(clip.SecondaryResourceKey))
                    {
                        StartPresentationCoroutine(subject.PlayExpressionEffect(
                            clip.SecondaryResourceKey,
                            color,
                            duration,
                            clip.AnchorKey,
                            clip.Offset));
                    }
                    return true;
                case BattleExpressionClipType.CheckDead:
                    if (damage != null && damage.TargetDead)
                    {
                        StartPresentationCoroutine(subject.PlayDeath(duration));
                    }
                    return true;
                case BattleExpressionClipType.BuffText:
                case BattleExpressionClipType.FloatingTip:
                case BattleExpressionClipType.BubbleTip:
                    StartPresentationCoroutine(subject.PlayExpressionText(
                        clip.ResourceKey,
                        color,
                        duration,
                        clip.Type == BattleExpressionClipType.BubbleTip));
                    return true;
                case BattleExpressionClipType.ColorFlash:
                case BattleExpressionClipType.UnitShake:
                case BattleExpressionClipType.CameraFocus:
                case BattleExpressionClipType.CameraShake:
                case BattleExpressionClipType.PresentationTimeScale:
                    return true;
                case BattleExpressionClipType.DarkenOthers:
                    ApplyDarkening(subject, primaryTarget, color, clip.Intensity);
                    return true;
                case BattleExpressionClipType.ClearDark:
                    ClearDarkening();
                    return true;
                case BattleExpressionClipType.SetUnitVisible:
                    subject.SetExpressionVisible(clip.State);
                    return true;
                case BattleExpressionClipType.SetHudVisible:
                    darkenedViews.Remove(subject);
                    subject.SetExpressionHudVisible(clip.State);
                    return true;
                case BattleExpressionClipType.SetShadowVisible:
                    subject.SetExpressionShadowVisible(clip.State);
                    return true;
                case BattleExpressionClipType.SetPetVisible:
                    subject.SetExpressionPetVisible(clip.State);
                    return true;
                case BattleExpressionClipType.SetSkin:
                    subject.SetExpressionSkin(clip.ResourceKey);
                    return true;
                case BattleExpressionClipType.SwitchForm:
                    subject.SwitchExpressionForm(clip.Option, clip.ResourceKey);
                    return true;
                case BattleExpressionClipType.LeaveField:
                    darkenedViews.Remove(subject);
                    subject.SetExpressionHudVisible(false);
                    subject.SetExpressionVisible(false);
                    return true;
                case BattleExpressionClipType.Summon:
                    subject.SetExpressionVisible(true);
                    subject.SetExpressionHudVisible(true);
                    if (!string.IsNullOrWhiteSpace(clip.ResourceKey))
                    {
                        subject.PlayExpressionAnimation(clip.ResourceKey);
                    }
                    return true;
                case BattleExpressionClipType.EffectAnimation:
                    return subject.PlayExpressionEffectAnimation(
                        clip.ResourceKey,
                        clip.SecondaryResourceKey);
                case BattleExpressionClipType.RemoveEffect:
                    subject.RemoveExpressionEffect(clip.ResourceKey);
                    return true;
                case BattleExpressionClipType.ToggleLoopEffect:
                    StartPresentationCoroutine(subject.SetLoopExpressionEffect(
                        clip.ResourceKey,
                        clip.AnchorKey,
                        clip.Offset,
                        clip.State));
                    return true;
                case BattleExpressionClipType.SetBackgroundVisible:
                case BattleExpressionClipType.ChangeModel:
                case BattleExpressionClipType.RefreshGrid:
                    return false;
                default:
                    return false;
            }
        }

        private void UpdateClip(
            CompiledBattleExpression expression,
            in CompiledBattleExpressionClip clip,
            TurnBasedUnitView subject,
            TurnBasedUnitView caster,
            TurnBasedUnitView primaryTarget,
            Camera battleCamera,
            in ClipRuntimeState state,
            int currentFrame)
        {
            float normalized = clip.DurationFrames <= 0
                ? 1f
                : Mathf.Clamp01(
                    (currentFrame - clip.StartFrame) /
                    (float)clip.DurationFrames);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            float elapsedSeconds =
                (currentFrame - clip.StartFrame) /
                (float)expression.FramesPerSecond;
            switch (clip.Type)
            {
                case BattleExpressionClipType.MoveToTarget:
                {
                    Vector3 target = subject.GetExpressionTargetPosition(
                        primaryTarget,
                        clip.Offset);
                    subject.SetExpressionLocalPosition(Vector3.Lerp(
                        state.SubjectStart,
                        target,
                        eased));
                    break;
                }
                case BattleExpressionClipType.MoveHome:
                    subject.SetExpressionLocalPosition(Vector3.Lerp(
                        state.SubjectStart,
                        subject.HomeLocalPosition,
                        eased));
                    break;
                case BattleExpressionClipType.SwapPosition:
                    if (primaryTarget != null && primaryTarget != subject)
                    {
                        Vector3 spacing = ToVector3(clip.Offset);
                        subject.SetExpressionLocalPosition(Vector3.Lerp(
                            state.SubjectStart,
                            state.TargetStart + spacing,
                            eased));
                        primaryTarget.SetExpressionLocalPosition(Vector3.Lerp(
                            state.TargetStart,
                            state.SubjectStart - spacing,
                            eased));
                    }
                    break;
                case BattleExpressionClipType.UnitShake:
                {
                    Vector3 axis = ToVector3(clip.Offset);
                    if (axis.sqrMagnitude <= 0.0001f)
                    {
                        axis = Vector3.right;
                    }
                    float wave = Mathf.Sin(
                        elapsedSeconds * Mathf.Max(0f, clip.Frequency) *
                        Mathf.PI * 2f);
                    float envelope = Mathf.Sin(normalized * Mathf.PI);
                    subject.SetExpressionLocalPosition(
                        state.SubjectStart +
                        axis.normalized * wave * envelope *
                        Mathf.Max(0f, clip.Intensity));
                    break;
                }
                case BattleExpressionClipType.ColorFlash:
                {
                    float pulse = Mathf.Sin(normalized * Mathf.PI);
                    subject.SetExpressionFlash(
                        ToColor(clip.Color),
                        pulse * Mathf.Max(0f, clip.Intensity));
                    break;
                }
                case BattleExpressionClipType.CameraFocus:
                    UpdateCameraFocus(
                        clip,
                        subject,
                        caster,
                        primaryTarget,
                        battleCamera,
                        state,
                        eased);
                    break;
                case BattleExpressionClipType.CameraShake:
                    UpdateCameraShake(
                        clip,
                        battleCamera,
                        state,
                        normalized,
                        elapsedSeconds);
                    break;
            }
        }

        private void UpdateCameraFocus(
            in CompiledBattleExpressionClip clip,
            TurnBasedUnitView subject,
            TurnBasedUnitView caster,
            TurnBasedUnitView primaryTarget,
            Camera battleCamera,
            in ClipRuntimeState state,
            float normalized)
        {
            if (battleCamera == null)
            {
                return;
            }

            Vector3 anchor = ResolveAnchorPosition(
                clip.Anchor,
                subject,
                caster,
                primaryTarget);
            Vector3 offset = ToVector3(clip.Offset);
            Vector3 destination = new Vector3(
                anchor.x + offset.x,
                anchor.y + offset.y,
                state.CameraStart.z + offset.z);
            battleCamera.transform.position = Vector3.Lerp(
                state.CameraStart,
                destination,
                normalized);
            if (battleCamera.orthographic)
            {
                battleCamera.orthographicSize = Mathf.Lerp(
                    state.CameraSize,
                    Mathf.Max(0.01f, clip.Intensity),
                    normalized);
            }
        }

        private static void UpdateCameraShake(
            in CompiledBattleExpressionClip clip,
            Camera battleCamera,
            in ClipRuntimeState state,
            float normalized,
            float elapsedSeconds)
        {
            if (battleCamera == null)
            {
                return;
            }

            Vector3 direction = ToVector3(clip.Offset);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = new Vector3(1f, 0.35f, 0f);
            }
            float wave = Mathf.Sin(
                elapsedSeconds * Mathf.Max(0f, clip.Frequency) *
                Mathf.PI * 2f);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            battleCamera.transform.position = state.CameraStart +
                direction.normalized * wave * envelope *
                Mathf.Max(0f, clip.Intensity);
        }

        private Vector3 ResolveAnchorPosition(
            BattleExpressionAnchor anchor,
            TurnBasedUnitView subject,
            TurnBasedUnitView caster,
            TurnBasedUnitView primaryTarget)
        {
            switch (anchor)
            {
                case BattleExpressionAnchor.Subject:
                case BattleExpressionAnchor.Caster:
                    return caster.transform.position;
                case BattleExpressionAnchor.PrimaryTarget:
                    return primaryTarget == null
                        ? subject.transform.position
                        : primaryTarget.transform.position;
                case BattleExpressionAnchor.CampCenter:
                case BattleExpressionAnchor.RowCenter:
                case BattleExpressionAnchor.ColumnCenter:
                    return CalculateUnitCenter(primaryTarget ?? subject, anchor);
                case BattleExpressionAnchor.ScreenCenter:
                    return Vector3.zero;
                default:
                    return subject.transform.position;
            }
        }

        private Vector3 CalculateUnitCenter(
            TurnBasedUnitView fallback,
            BattleExpressionAnchor anchor)
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            int fallbackPosition = fallback.Unit == null
                ? 0
                : fallback.Unit.Position;
            int fallbackRow = fallbackPosition <= 0
                ? -1
                : (fallbackPosition - 1) / 3;
            int fallbackColumn = fallbackPosition <= 0
                ? -1
                : (fallbackPosition - 1) % 3;
            for (int index = 0; index < unitViews.Length; index++)
            {
                TurnBasedUnitView view = unitViews[index];
                if (view == null || view.Unit == null || fallback.Unit == null ||
                    view.Unit.Camp != fallback.Unit.Camp)
                {
                    continue;
                }
                int position = view.Unit.Position;
                if (anchor == BattleExpressionAnchor.RowCenter &&
                    (position <= 0 || (position - 1) / 3 != fallbackRow))
                {
                    continue;
                }
                if (anchor == BattleExpressionAnchor.ColumnCenter &&
                    (position <= 0 || (position - 1) % 3 != fallbackColumn))
                {
                    continue;
                }
                sum += view.transform.position;
                count++;
            }
            return count > 0 ? sum / count : fallback.transform.position;
        }

        private void ApplyDarkening(
            TurnBasedUnitView subject,
            TurnBasedUnitView primaryTarget,
            Color color,
            float intensity)
        {
            for (int index = 0; index < unitViews.Length; index++)
            {
                TurnBasedUnitView view = unitViews[index];
                if (view != null && view != subject && view != primaryTarget)
                {
                    view.SetExpressionDimmed(color, intensity);
                    view.SetExpressionHudVisible(false);
                    darkenedViews.Add(view);
                }
            }
        }

        private void ClearDarkening()
        {
            foreach (TurnBasedUnitView view in darkenedViews)
            {
                if (view == null)
                {
                    continue;
                }
                view.SetExpressionDimmed(Color.white, 0f);
                view.SetExpressionHudVisible(true);
            }
            darkenedViews.Clear();
        }

        private bool StartExtensions(
            in CompiledBattleExpressionClip clip,
            TurnBasedUnitView subject,
            TurnBasedUnitView primaryTarget,
            DamageResolvedEvent damage)
        {
            for (int index = 0; index < extensions.Length; index++)
            {
                ITurnBasedExpressionExtension extension = extensions[index];
                if (extension == null)
                {
                    continue;
                }
                try
                {
                    if (extension.TryStartExpressionClip(
                            clip,
                            subject,
                            primaryTarget,
                            damage))
                    {
                        return true;
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, coroutineHost);
                }
            }
            return false;
        }

        private void WarnUnsupported(BattleExpressionClipType type)
        {
            if (!unsupportedWarnings.Add(type))
            {
                return;
            }
            Debug.LogWarning(
                $"[TurnBasedBattle] 表现操作 {type} 需要场景中的 " +
                $"{nameof(ITurnBasedExpressionExtension)} 实现。",
                coroutineHost);
        }

        private void StartPresentationCoroutine(IEnumerator routine)
        {
            if (routine != null)
            {
                activeCoroutines.Add(coroutineHost.StartCoroutine(routine));
            }
        }

        private void StopActiveCoroutines()
        {
            for (int index = 0; index < activeCoroutines.Count; index++)
            {
                if (activeCoroutines[index] != null)
                {
                    coroutineHost.StopCoroutine(activeCoroutines[index]);
                }
            }
            activeCoroutines.Clear();
        }

        private void CancelTransientUnitPresentation(
            TurnBasedUnitView caster,
            TurnBasedUnitView primaryTarget)
        {
            for (int index = 0; index < unitViews.Length; index++)
            {
                unitViews[index]?.CancelExpressionTransientPresentation();
            }
            if (Array.IndexOf(unitViews, caster) < 0)
            {
                caster.CancelExpressionTransientPresentation();
            }
            if (primaryTarget != null && Array.IndexOf(unitViews, primaryTarget) < 0)
            {
                primaryTarget.CancelExpressionTransientPresentation();
            }
        }

        private static float ResolvePresentationSpeed(
            CompiledBattleExpressionClip[] clips,
            int frame)
        {
            float speed = 1f;
            for (int index = 0; index < clips.Length; index++)
            {
                CompiledBattleExpressionClip clip = clips[index];
                if (clip.Type == BattleExpressionClipType.PresentationTimeScale &&
                    frame >= clip.StartFrame && frame <= clip.EndFrame)
                {
                    speed = Mathf.Clamp(clip.Intensity, 0.01f, 8f);
                }
            }
            return speed;
        }

        private static ClipRuntimeState CaptureState(
            TurnBasedUnitView subject,
            TurnBasedUnitView primaryTarget,
            Camera battleCamera)
        {
            return new ClipRuntimeState
            {
                SubjectStart = subject.ExpressionLocalPosition,
                TargetStart = primaryTarget == null
                    ? Vector3.zero
                    : primaryTarget.ExpressionLocalPosition,
                CameraStart = battleCamera == null
                    ? Vector3.zero
                    : battleCamera.transform.position,
                CameraSize = battleCamera != null && battleCamera.orthographic
                    ? battleCamera.orthographicSize
                    : 0f,
            };
        }

        private static TurnBasedUnitView ResolveSubject(
            BattleExpressionSubject subject,
            TurnBasedUnitView caster,
            TurnBasedUnitView primaryTarget)
        {
            return subject == BattleExpressionSubject.PrimaryTarget
                ? primaryTarget
                : caster;
        }

        private static TurnBasedUnitView[] CopyUnitViews(
            IReadOnlyList<TurnBasedUnitView> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<TurnBasedUnitView>();
            }
            var result = new TurnBasedUnitView[source.Count];
            for (int index = 0; index < source.Count; index++)
            {
                result[index] = source[index];
            }
            return result;
        }

        private static ITurnBasedExpressionExtension[] FindExtensions(
            MonoBehaviour host)
        {
            MonoBehaviour[] behaviours =
                host.GetComponentsInChildren<MonoBehaviour>(true);
            var result = new List<ITurnBasedExpressionExtension>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                if (behaviours[index] is ITurnBasedExpressionExtension extension)
                {
                    result.Add(extension);
                }
            }
            return result.ToArray();
        }

        private static void RestoreCamera(
            Camera battleCamera,
            Vector3 position,
            float orthographicSize,
            float fieldOfView)
        {
            if (battleCamera == null)
            {
                return;
            }
            battleCamera.transform.position = position;
            if (battleCamera.orthographic)
            {
                battleCamera.orthographicSize = orthographicSize;
            }
            else
            {
                battleCamera.fieldOfView = fieldOfView;
            }
        }

        private static Vector3 ToVector3(BattleExpressionVector value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Color ToColor(BattleExpressionColor color)
        {
            return new Color(color.Red, color.Green, color.Blue, color.Alpha);
        }

        private struct ClipRuntimeState
        {
            public Vector3 SubjectStart;
            public Vector3 TargetStart;
            public Vector3 CameraStart;
            public float CameraSize;
            public bool HandledByExtension;
            public bool HandledByBuiltIn;
        }
    }
}
