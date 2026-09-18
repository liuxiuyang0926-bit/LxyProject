using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using Game.Resource;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Battle.TurnBased.Presentation
{
    [DisallowMultipleComponent]
    [AddComponentMenu("LxyDemo/Battle/Turn Based Unit View")]
    public sealed class TurnBasedUnitView : MonoBehaviour
    {
        [SerializeField] private int unitId;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Slider healthSlider;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text turnMarker;
        [SerializeField] private TMP_Text floatingTextTemplate;
        [Header("技能表现扩展")]
        [SerializeField] private GameObject expressionShadowRoot;
        [SerializeField] private GameObject expressionPetRoot;
        [SerializeField] private GameObject[] expressionForms =
            Array.Empty<GameObject>();

        private BattleUnit unit;
        private BattleUnitAuthoring authoring;
        private Animator animator;
        private Component spineAnimation;
        private PropertyInfo spineStateProperty;
        private MethodInfo spineSetAnimation;
        private MethodInfo spineAddAnimation;
        private Vector3 homeLocalPosition;
        private Renderer[] renderers = Array.Empty<Renderer>();
        private bool[] rendererEnabledStates = Array.Empty<bool>();
        private MaterialPropertyBlock expressionPropertyBlock;
        private bool reflectionWarningLogged;
        private bool formWarningLogged;
        private AudioSource expressionLoopAudioSource;
        private GameResourceHandle<AudioClip> expressionLoopAudioHandle;
        private readonly List<GameResourceHandle<AudioClip>> transientAudioHandles =
            new List<GameResourceHandle<AudioClip>>();
        private readonly List<GameResourceInstanceHandle> transientExpressionEffects =
            new List<GameResourceInstanceHandle>();
        private readonly List<GameObject> transientExpressionObjects =
            new List<GameObject>();
        private readonly Dictionary<string, GameResourceInstanceHandle>
            loopExpressionEffects =
                new Dictionary<string, GameResourceInstanceHandle>(
                    StringComparer.Ordinal);

        public int UnitId => unitId;
        public BattleUnit Unit => unit;
        public Vector3 HomeLocalPosition => homeLocalPosition;
        public Vector3 ExpressionLocalPosition => transform.localPosition;
        public Transform ExpressionRoot => visualRoot != null ? visualRoot : transform;

        public void Bind(BattleUnit battleUnit, BattleUnitAuthoring definition)
        {
            unit = battleUnit ?? throw new ArgumentNullException(nameof(battleUnit));
            authoring = definition ?? throw new ArgumentNullException(nameof(definition));
            unitId = battleUnit.Id.Value;
            homeLocalPosition = transform.localPosition;
            Transform root = visualRoot != null ? visualRoot : transform;
            renderers = root.GetComponentsInChildren<Renderer>(true);
            rendererEnabledStates = new bool[renderers.Length];
            for (int index = 0; index < renderers.Length; index++)
            {
                rendererEnabledStates[index] = renderers[index] != null &&
                    renderers[index].enabled;
            }
            expressionPropertyBlock = new MaterialPropertyBlock();
            CacheAnimationDriver();

            if (nameText != null)
            {
                nameText.text = string.IsNullOrWhiteSpace(authoring.displayName)
                    ? $"单位 {unitId}"
                    : authoring.displayName;
            }

            if (floatingTextTemplate != null)
            {
                floatingTextTemplate.gameObject.SetActive(false);
            }

            Refresh();
            SetCurrentTurn(false, Color.white);
            PlayLoop(authoring.idleAnimation);
        }

        public void Refresh()
        {
            if (unit == null)
            {
                return;
            }

            long current = unit.Attributes.Get(AttributeType.Hp);
            long maximum = unit.Attributes.Get(AttributeType.MaxHp);
            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = 1f;
                healthSlider.value = maximum <= 0
                    ? 0f
                    : Mathf.Clamp01(current / (float)maximum);
            }

            if (healthText != null)
            {
                healthText.text = $"{current} / {maximum}";
            }
        }

        public void SetCurrentTurn(bool current, Color accent)
        {
            if (turnMarker == null)
            {
                return;
            }

            turnMarker.gameObject.SetActive(current && unit != null && !unit.IsDead);
            turnMarker.color = accent;
        }

        public IEnumerator PlaySkill(
            TurnBasedUnitView target,
            string animationName,
            float duration)
        {
            duration = Mathf.Max(0.05f, duration);
            PlayOneShot(animationName, authoring?.idleAnimation);

            Vector3 origin = homeLocalPosition;
            Vector3 targetLocal = origin;
            if (target != null && transform.parent == target.transform.parent)
            {
                targetLocal = Vector3.Lerp(
                    origin,
                    target.homeLocalPosition,
                    0.42f);
                targetLocal.y = origin.y;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float lunge = normalized < 0.38f
                    ? normalized / 0.38f
                    : 1f - (normalized - 0.38f) / 0.62f;
                transform.localPosition = Vector3.Lerp(
                    origin,
                    targetLocal,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lunge)));
                yield return null;
            }

            transform.localPosition = origin;
        }

        public IEnumerator PlayHit(
            long damage,
            bool critical,
            bool miss,
            float duration,
            float shakeIntensity = 0.13f)
        {
            duration = Mathf.Max(0.05f, duration);
            PlayOneShot(authoring?.hitAnimation, authoring?.idleAnimation);

            TMP_Text floating = CreateFloatingText(damage, critical, miss);
            Vector3 origin = homeLocalPosition;
            Vector3 floatingOrigin = floating == null
                ? Vector3.zero
                : floating.rectTransform.localPosition;
            Color floatingColor = floating == null
                ? Color.white
                : floating.color;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float shake = Mathf.Sin(normalized * Mathf.PI * 8f) *
                              (1f - normalized) * Mathf.Max(0f, shakeIntensity);
                transform.localPosition = origin + Vector3.right * shake;

                if (floating != null)
                {
                    floating.rectTransform.localPosition = floatingOrigin +
                        Vector3.up * (55f * normalized);
                    floating.color = new Color(
                        floatingColor.r,
                        floatingColor.g,
                        floatingColor.b,
                        1f - Mathf.Clamp01((normalized - 0.55f) / 0.45f));
                }

                yield return null;
            }

            transform.localPosition = origin;
            if (floating != null)
            {
                transientExpressionObjects.Remove(floating.gameObject);
                Destroy(floating.gameObject);
            }
        }

        public IEnumerator PlayDeath(float duration)
        {
            PlayOneShot(authoring?.deathAnimation, string.Empty);
            float elapsed = 0f;
            Vector3 originalScale = transform.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, duration));
                transform.localScale = Vector3.Lerp(
                    originalScale,
                    originalScale * 0.88f,
                    normalized);
                yield return null;
            }
        }

        public void PlayVictory()
        {
            PlayLoop(authoring?.victoryAnimation);
        }

        public void PlayExpressionAnimation(string animationName)
        {
            PlayOneShot(animationName, authoring?.idleAnimation);
        }

        public void SetExpressionLocalPosition(Vector3 position)
        {
            transform.localPosition = position;
        }

        public Vector3 GetExpressionWorldPosition(
            string anchorName,
            BattleExpressionVector offset)
        {
            Transform anchor = FindExpressionAnchor(anchorName);
            return anchor.position + new Vector3(offset.X, offset.Y, offset.Z);
        }

        public Vector3 GetExpressionTargetPosition(
            TurnBasedUnitView target,
            BattleExpressionVector offset)
        {
            Vector3 result = homeLocalPosition;
            if (target != null)
            {
                result = transform.parent == target.transform.parent
                    ? target.HomeLocalPosition
                    : transform.parent.InverseTransformPoint(target.transform.position);
            }

            result += new Vector3(offset.X, offset.Y, offset.Z);
            return result;
        }

        public void ResetExpressionPose()
        {
            transform.localPosition = homeLocalPosition;
            SetExpressionFlash(Color.white, 0f);
        }

        public void SetExpressionVisible(bool visible)
        {
            Transform root = ExpressionRoot;
            if (root != transform)
            {
                root.gameObject.SetActive(visible);
                return;
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].enabled = visible &&
                        index < rendererEnabledStates.Length &&
                        rendererEnabledStates[index];
                }
            }
        }

        public void SetExpressionHudVisible(bool visible)
        {
            SetObjectActive(healthSlider == null ? null : healthSlider.gameObject, visible);
            SetObjectActive(nameText == null ? null : nameText.gameObject, visible);
            SetObjectActive(healthText == null ? null : healthText.gameObject, visible);
            if (!visible)
            {
                SetObjectActive(turnMarker == null ? null : turnMarker.gameObject, false);
            }
        }

        public void SetExpressionShadowVisible(bool visible)
        {
            SetObjectActive(expressionShadowRoot, visible);
        }

        public void SetExpressionPetVisible(bool visible)
        {
            SetObjectActive(expressionPetRoot, visible);
        }

        public void SetExpressionDimmed(Color color, float intensity)
        {
            SetExpressionFlash(color, Mathf.Clamp01(intensity));
        }

        public void SetExpressionSkin(string skinName)
        {
            if (string.IsNullOrWhiteSpace(skinName) || spineAnimation == null)
            {
                return;
            }

            try
            {
                object skeleton = spineAnimation.GetType()
                    .GetProperty("Skeleton", BindingFlags.Instance | BindingFlags.Public)?
                    .GetValue(spineAnimation, null);
                if (skeleton == null)
                {
                    return;
                }
                Type skeletonType = skeleton.GetType();
                skeletonType.GetMethod("SetSkin", new[] { typeof(string) })?
                    .Invoke(skeleton, new object[] { skinName });
                skeletonType.GetMethod("SetSlotsToSetupPose", Type.EmptyTypes)?
                    .Invoke(skeleton, null);
                object state = spineStateProperty?.GetValue(spineAnimation, null);
                state?.GetType().GetMethod(
                        "Apply",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { skeletonType },
                        null)?
                    .Invoke(state, new[] { skeleton });
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[TurnBasedBattle] 单位 {unitId} 切换皮肤 {skinName} 失败：" +
                    exception.Message,
                    this);
            }
        }

        public void SwitchExpressionForm(int formIndex, string animationName)
        {
            if (expressionForms == null || expressionForms.Length == 0)
            {
                if (!formWarningLogged)
                {
                    formWarningLogged = true;
                    Debug.LogWarning(
                        $"[TurnBasedBattle] 单位 {unitId} 未配置技能表现形态节点。",
                        this);
                }
                return;
            }

            int selectedForm = Mathf.Clamp(formIndex, 0, expressionForms.Length - 1);
            for (int index = 0; index < expressionForms.Length; index++)
            {
                SetObjectActive(expressionForms[index], index == selectedForm);
            }
            CacheAnimationDriver();
            if (!string.IsNullOrWhiteSpace(animationName))
            {
                PlayExpressionAnimation(animationName);
            }
        }

        public void SetExpressionFlash(Color color, float intensity)
        {
            if (expressionPropertyBlock == null)
            {
                expressionPropertyBlock = new MaterialPropertyBlock();
            }

            if (intensity <= 0f)
            {
                for (int index = 0; index < renderers.Length; index++)
                {
                    if (renderers[index] != null)
                    {
                        renderers[index].SetPropertyBlock(null);
                    }
                }
                return;
            }

            Color tint = Color.Lerp(Color.white, color, Mathf.Clamp01(intensity));
            expressionPropertyBlock.Clear();
            expressionPropertyBlock.SetColor("_BaseColor", tint);
            expressionPropertyBlock.SetColor("_Color", tint);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index] != null)
                {
                    renderers[index].SetPropertyBlock(expressionPropertyBlock);
                }
            }
        }

        public IEnumerator PlayExpressionEffect(
            string effectKey,
            Color color,
            float duration,
            string anchorName = "",
            BattleExpressionVector offset = default)
        {
            GameResourceInstanceHandle resourceHandle =
                TryInstantiateExpressionEffect(effectKey, anchorName);
            if (resourceHandle != null)
            {
                transientExpressionEffects.Add(resourceHandle);
                yield return resourceHandle;
                if (this == null)
                {
                    yield break;
                }
                GameObject instance = resourceHandle.Result;
                if (instance != null)
                {
                    instance.transform.localPosition = new Vector3(
                        offset.X,
                        offset.Y,
                        offset.Z);
                    ApplyEffectTint(instance, color);
                    yield return WaitUnscaled(duration);
                    ReleaseTransientEffect(resourceHandle);
                    yield break;
                }
                ReleaseTransientEffect(resourceHandle);
            }

            if (floatingTextTemplate == null)
            {
                yield break;
            }

            TMP_Text effect = Instantiate(
                floatingTextTemplate,
                floatingTextTemplate.transform.parent);
            transientExpressionObjects.Add(effect.gameObject);
            effect.name = "ExpressionEffect_" + effectKey;
            effect.gameObject.SetActive(true);
            effect.text = string.IsNullOrWhiteSpace(effectKey)
                ? "FX"
                : $"FX {effectKey}";
            effect.color = color;
            effect.fontSize = 20f;
            Vector3 origin = effect.rectTransform.localPosition;
            float elapsed = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                effect.rectTransform.localPosition = origin +
                    Vector3.up * (28f * normalized);
                Color next = color;
                next.a *= 1f - normalized;
                effect.color = next;
                yield return null;
            }

            transientExpressionObjects.Remove(effect.gameObject);
            Destroy(effect.gameObject);
        }

        public IEnumerator PlayExpressionProjectileEffect(
            TurnBasedUnitView target,
            string effectKey,
            string startAnchor,
            string targetAnchor,
            BattleExpressionVector startOffset,
            BattleExpressionVector targetOffset,
            Color color,
            float duration)
        {
            GameResourceManager manager = GameResourceManager.Instance;
            if (manager == null || string.IsNullOrWhiteSpace(effectKey))
            {
                yield return PlayExpressionEffect(effectKey, color, duration);
                yield break;
            }

            GameResourceInstanceHandle handle = null;
            try
            {
                handle = manager.InstantiateAsync(effectKey, null, true, true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[TurnBasedBattle] 创建弹道特效 {effectKey} 失败：" +
                    exception.Message,
                    this);
            }
            if (handle == null)
            {
                yield break;
            }

            transientExpressionEffects.Add(handle);
            yield return handle;
            if (this == null)
            {
                yield break;
            }
            GameObject projectile = handle.Result;
            if (projectile == null)
            {
                ReleaseTransientEffect(handle);
                yield break;
            }

            Vector3 start = GetExpressionWorldPosition(startAnchor, startOffset);
            Vector3 destination = target == null
                ? start
                : target.GetExpressionWorldPosition(targetAnchor, targetOffset);
            projectile.transform.position = start;
            ApplyEffectTint(projectile, color);
            duration = Mathf.Max(0.05f, duration);
            float elapsed = 0f;
            while (elapsed < duration && projectile != null)
            {
                elapsed += Time.unscaledDeltaTime;
                projectile.transform.position = Vector3.Lerp(
                    start,
                    destination,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
                yield return null;
            }
            ReleaseTransientEffect(handle);
        }

        public IEnumerator SetLoopExpressionEffect(
            string effectKey,
            string anchorName,
            BattleExpressionVector offset,
            bool enabled)
        {
            if (string.IsNullOrWhiteSpace(effectKey))
            {
                yield break;
            }
            if (!enabled)
            {
                RemoveExpressionEffect(effectKey);
                yield break;
            }

            RemoveExpressionEffect(effectKey);
            GameResourceInstanceHandle handle =
                TryInstantiateExpressionEffect(effectKey, anchorName);
            if (handle == null)
            {
                yield break;
            }
            loopExpressionEffects[effectKey] = handle;
            yield return handle;
            if (this == null || handle.IsReleased)
            {
                yield break;
            }
            GameObject instance = handle.Result;
            if (instance == null)
            {
                RemoveExpressionEffect(effectKey);
                yield break;
            }
            instance.transform.localPosition = new Vector3(
                offset.X,
                offset.Y,
                offset.Z);
        }

        public bool PlayExpressionEffectAnimation(
            string effectKey,
            string animationName)
        {
            if (string.IsNullOrWhiteSpace(effectKey) ||
                string.IsNullOrWhiteSpace(animationName) ||
                !loopExpressionEffects.TryGetValue(effectKey, out
                    GameResourceInstanceHandle handle) ||
                handle == null || handle.Result == null)
            {
                return false;
            }

            Animator animator = handle.Result.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                return false;
            }
            animator.Play(animationName, 0, 0f);
            return true;
        }

        public void RemoveExpressionEffect(string effectKey)
        {
            if (string.IsNullOrWhiteSpace(effectKey) ||
                !loopExpressionEffects.TryGetValue(
                    effectKey,
                    out GameResourceInstanceHandle handle))
            {
                return;
            }
            loopExpressionEffects.Remove(effectKey);
            handle?.Release();
        }

        public IEnumerator PlayExpressionText(
            string text,
            Color color,
            float duration,
            bool bubble)
        {
            if (floatingTextTemplate == null)
            {
                yield break;
            }

            TMP_Text floating = Instantiate(
                floatingTextTemplate,
                floatingTextTemplate.transform.parent);
            transientExpressionObjects.Add(floating.gameObject);
            floating.name = bubble ? "ExpressionBubble" : "ExpressionText";
            floating.gameObject.SetActive(true);
            floating.text = string.IsNullOrWhiteSpace(text)
                ? (bubble ? "…" : "TIP")
                : text;
            floating.color = color;
            floating.fontSize = bubble ? 22f : 26f;
            Vector3 origin = floating.rectTransform.localPosition;
            duration = Mathf.Max(0.05f, duration);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                floating.rectTransform.localPosition = origin +
                    Vector3.up * ((bubble ? 12f : 42f) * normalized);
                Color next = color;
                next.a *= 1f - Mathf.Clamp01((normalized - 0.72f) / 0.28f);
                floating.color = next;
                yield return null;
            }
            transientExpressionObjects.Remove(floating.gameObject);
            Destroy(floating.gameObject);
        }

        public void PlayExpressionAudio(string resourceKey)
        {
            PlayExpressionAudio(resourceKey, false, 1f);
        }

        public bool PlayExpressionAudio(
            string resourceKey,
            bool loop,
            float volume)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                return false;
            }

            AudioClip clip = Resources.Load<AudioClip>(resourceKey);
            if (clip == null)
            {
                return false;
            }
            volume = Mathf.Clamp01(volume);
            if (!loop)
            {
                AudioSource.PlayClipAtPoint(clip, transform.position, volume);
                return true;
            }

            if (expressionLoopAudioSource == null)
            {
                expressionLoopAudioSource = gameObject.AddComponent<AudioSource>();
                expressionLoopAudioSource.playOnAwake = false;
                expressionLoopAudioSource.spatialBlend = 0f;
            }
            expressionLoopAudioHandle?.Release();
            expressionLoopAudioHandle = null;
            expressionLoopAudioSource.Stop();
            expressionLoopAudioSource.clip = clip;
            expressionLoopAudioSource.loop = true;
            expressionLoopAudioSource.volume = volume;
            expressionLoopAudioSource.Play();
            return true;
        }

        public IEnumerator PlayExpressionAudioAsync(
            string resourceKey,
            bool loop,
            float volume)
        {
            if (string.IsNullOrWhiteSpace(resourceKey))
            {
                yield break;
            }

            GameResourceHandle<AudioClip> handle = null;
            GameResourceManager manager = GameResourceManager.Instance;
            if (manager != null)
            {
                try
                {
                    handle = manager.LoadAssetAsync<AudioClip>(resourceKey);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        $"[TurnBasedBattle] 加载表现音频 {resourceKey} 失败：" +
                        exception.Message,
                        this);
                }
            }

            if (handle != null)
            {
                transientAudioHandles.Add(handle);
                yield return handle;
                if (this == null)
                {
                    yield break;
                }
                AudioClip loadedClip = handle.Asset;
                if (loadedClip != null)
                {
                    if (loop)
                    {
                        AssignLoopAudio(handle, loadedClip, volume);
                    }
                    else
                    {
                        AudioSource.PlayClipAtPoint(
                            loadedClip,
                            transform.position,
                            Mathf.Clamp01(volume));
                        yield return WaitUnscaled(loadedClip.length + 0.1f);
                        ReleaseTransientAudio(handle);
                    }
                    yield break;
                }
                ReleaseTransientAudio(handle);
            }

            PlayExpressionAudio(resourceKey, loop, volume);
        }

        private GameResourceInstanceHandle TryInstantiateExpressionEffect(
            string effectKey,
            string anchorName)
        {
            GameResourceManager manager = GameResourceManager.Instance;
            if (manager == null || string.IsNullOrWhiteSpace(effectKey))
            {
                return null;
            }
            try
            {
                return manager.InstantiateAsync(
                    effectKey,
                    FindExpressionAnchor(anchorName),
                    false,
                    true);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"[TurnBasedBattle] 创建表现特效 {effectKey} 失败：" +
                    exception.Message,
                    this);
                return null;
            }
        }

        private void ReleaseTransientEffect(GameResourceInstanceHandle handle)
        {
            transientExpressionEffects.Remove(handle);
            handle?.Release();
        }

        private void AssignLoopAudio(
            GameResourceHandle<AudioClip> handle,
            AudioClip clip,
            float volume)
        {
            transientAudioHandles.Remove(handle);
            expressionLoopAudioHandle?.Release();
            expressionLoopAudioHandle = handle;
            if (expressionLoopAudioSource == null)
            {
                expressionLoopAudioSource = gameObject.AddComponent<AudioSource>();
                expressionLoopAudioSource.playOnAwake = false;
                expressionLoopAudioSource.spatialBlend = 0f;
            }
            expressionLoopAudioSource.Stop();
            expressionLoopAudioSource.clip = clip;
            expressionLoopAudioSource.loop = true;
            expressionLoopAudioSource.volume = Mathf.Clamp01(volume);
            expressionLoopAudioSource.Play();
        }

        private void ReleaseTransientAudio(GameResourceHandle<AudioClip> handle)
        {
            transientAudioHandles.Remove(handle);
            handle?.Release();
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float end = Time.realtimeSinceStartup + Mathf.Max(0.05f, duration);
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        private static void ApplyEffectTint(GameObject instance, Color color)
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            Renderer[] effectRenderers =
                instance.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < effectRenderers.Length; index++)
            {
                effectRenderers[index].SetPropertyBlock(block);
            }
        }

        public void CancelExpressionTransientPresentation()
        {
            for (int index = 0; index < transientAudioHandles.Count; index++)
            {
                transientAudioHandles[index]?.Release();
            }
            transientAudioHandles.Clear();
            for (int index = 0; index < transientExpressionEffects.Count; index++)
            {
                transientExpressionEffects[index]?.Release();
            }
            transientExpressionEffects.Clear();
            for (int index = 0; index < transientExpressionObjects.Count; index++)
            {
                if (transientExpressionObjects[index] != null)
                {
                    Destroy(transientExpressionObjects[index]);
                }
            }
            transientExpressionObjects.Clear();
        }

        public void ReleaseExpressionPresentation()
        {
            CancelExpressionTransientPresentation();
            if (expressionLoopAudioSource != null)
            {
                expressionLoopAudioSource.Stop();
                expressionLoopAudioSource.clip = null;
            }
            expressionLoopAudioHandle?.Release();
            expressionLoopAudioHandle = null;
            foreach (KeyValuePair<string, GameResourceInstanceHandle> pair in
                     loopExpressionEffects)
            {
                pair.Value?.Release();
            }
            loopExpressionEffects.Clear();
        }

        private void OnDestroy()
        {
            ReleaseExpressionPresentation();
        }

        private TMP_Text CreateFloatingText(
            long damage,
            bool critical,
            bool miss)
        {
            if (floatingTextTemplate == null)
            {
                return null;
            }

            TMP_Text floating = Instantiate(
                floatingTextTemplate,
                floatingTextTemplate.transform.parent);
            transientExpressionObjects.Add(floating.gameObject);
            floating.name = "DamageText";
            floating.gameObject.SetActive(true);
            floating.text = miss ? "MISS" : $"-{damage}";
            floating.fontSize = critical ? 38f : 30f;
            floating.color = miss
                ? new Color(0.75f, 0.85f, 1f, 1f)
                : critical
                    ? new Color(1f, 0.72f, 0.16f, 1f)
                    : new Color(1f, 0.34f, 0.28f, 1f);
            return floating;
        }

        private void CacheAnimationDriver()
        {
            Transform root = visualRoot != null ? visualRoot : transform;
            animator = null;
            spineAnimation = null;
            spineStateProperty = null;
            spineSetAnimation = null;
            spineAddAnimation = null;
            animator = root.GetComponentInChildren<Animator>(true);
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null ||
                    !string.Equals(
                        component.GetType().FullName,
                        "Spine.Unity.SkeletonAnimation",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                spineAnimation = component;
                spineStateProperty = component.GetType().GetProperty(
                    "AnimationState",
                    BindingFlags.Instance | BindingFlags.Public);
                object state = spineStateProperty?.GetValue(component, null);
                if (state != null)
                {
                    Type stateType = state.GetType();
                    spineSetAnimation = stateType.GetMethod(
                        "SetAnimation",
                        new[] { typeof(int), typeof(string), typeof(bool) });
                    spineAddAnimation = stateType.GetMethod(
                        "AddAnimation",
                        new[]
                        {
                            typeof(int),
                            typeof(string),
                            typeof(bool),
                            typeof(float),
                        });
                }

                break;
            }
        }

        private Transform FindExpressionAnchor(string anchorName)
        {
            Transform root = ExpressionRoot;
            if (string.IsNullOrWhiteSpace(anchorName))
            {
                return root;
            }

            Transform direct = root.Find(anchorName);
            if (direct != null)
            {
                return direct;
            }
            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < children.Length; index++)
            {
                if (string.Equals(
                        children[index].name,
                        anchorName,
                        StringComparison.Ordinal))
                {
                    return children[index];
                }
            }
            return root;
        }

        private static void SetObjectActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void PlayLoop(string animationName)
        {
            TryPlay(animationName, true, string.Empty);
        }

        private void PlayOneShot(string animationName, string followUp)
        {
            TryPlay(animationName, false, followUp);
        }

        private void TryPlay(
            string animationName,
            bool loop,
            string followUp)
        {
            if (string.IsNullOrWhiteSpace(animationName))
            {
                return;
            }

            if (animator != null)
            {
                animator.Play(animationName, 0, 0f);
                return;
            }

            if (spineAnimation == null || spineStateProperty == null ||
                spineSetAnimation == null)
            {
                return;
            }

            try
            {
                object state = spineStateProperty.GetValue(spineAnimation, null);
                spineSetAnimation.Invoke(
                    state,
                    new object[] { 0, animationName, loop });
                if (!loop && !string.IsNullOrWhiteSpace(followUp) &&
                    spineAddAnimation != null)
                {
                    spineAddAnimation.Invoke(
                        state,
                        new object[] { 0, followUp, true, 0f });
                }
            }
            catch (Exception exception)
            {
                if (!reflectionWarningLogged)
                {
                    reflectionWarningLogged = true;
                    Debug.LogWarning(
                        $"[TurnBasedBattle] 单位 {unitId} 播放动画失败：" +
                        exception.Message,
                        this);
                }
            }
        }
    }
}
