using System;
using System.Collections.Generic;
using System.Reflection;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.RuntimeData;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Battle.Editor
{
    public sealed class TurnBasedFormationPreviewWindow : EditorWindow
    {
        private const string DefaultAssetPath =
            "Assets/GameResources/Battle/TurnBased/Formations/DefaultBattleFormation.asset";
        private const float BrowserWidth = 285f;

        private TurnBasedBattleFormationAsset formation;
        private SerializedObject serialized;
        private PreviewRenderUtility preview;
        private RenderTexture previewTexture;
        private readonly GameObject[] previewSlotModels =
            new GameObject[TurnBasedBattleFormationAsset.SlotCount];
        private readonly Vector3[] previewHomePositions =
            new Vector3[TurnBasedBattleFormationAsset.SlotCount];
        private readonly Vector3[] previewHomeScales =
            new Vector3[TurnBasedBattleFormationAsset.SlotCount];
        private readonly List<Component> animatedComponents = new List<Component>();
        private readonly List<string> expressionCues = new List<string>();
        private MaterialPropertyBlock expressionPropertyBlock;
        private Vector2 slotScroll;
        private Vector2 detailScroll;
        private int selectedSlot;
        private double lastEditorTime;
        private bool previewDirty = true;
        private TurnBasedSkillExpressionAsset expressionAsset;
        private CompiledBattleExpression compiledExpression;
        private int expressionFrame;
        private int expressionCasterSlot;
        private int expressionTargetSlot = 9;

        [MenuItem("工具/战斗/18 位阵容与模型预览", false, 2)]
        public static void Open()
        {
            TurnBasedFormationPreviewWindow window = OpenWindow();
            window.Show();
        }

        public static void BeginExpressionPreview(
            TurnBasedSkillExpressionAsset asset,
            CompiledBattleExpression expression,
            int frame)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            TurnBasedFormationPreviewWindow window = OpenWindow();
            window.expressionAsset = asset;
            window.compiledExpression = expression;
            window.expressionCasterSlot = window.FindPopulatedSlot(0, 9, 0);
            int defaultTargetSlot = window.FindPopulatedSlot(9, 18, 9);
            window.expressionTargetSlot = ResolveExpressionTargetSlot(
                expression,
                defaultTargetSlot);
            window.SetExpressionFrame(frame);
            window.Show();
        }

        public static void SetExpressionPreviewFrame(
            TurnBasedSkillExpressionAsset asset,
            int frame)
        {
            TurnBasedFormationPreviewWindow[] windows =
                Resources.FindObjectsOfTypeAll<TurnBasedFormationPreviewWindow>();
            for (int index = 0; index < windows.Length; index++)
            {
                if (windows[index].expressionAsset == asset)
                {
                    windows[index].SetExpressionFrame(frame);
                }
            }
        }

        public static void EndExpressionPreview(TurnBasedSkillExpressionAsset asset)
        {
            TurnBasedFormationPreviewWindow[] windows =
                Resources.FindObjectsOfTypeAll<TurnBasedFormationPreviewWindow>();
            for (int index = 0; index < windows.Length; index++)
            {
                if (asset == null || windows[index].expressionAsset == asset)
                {
                    windows[index].ClearExpressionPreview();
                }
            }
        }

        private static TurnBasedFormationPreviewWindow OpenWindow()
        {
            var window = GetWindow<TurnBasedFormationPreviewWindow>();
            window.titleContent = new GUIContent("战斗阵容预览");
            window.minSize = new Vector2(1050f, 650f);
            window.LoadOrCreateDefault();
            return window;
        }

        private void OnEnable()
        {
            typeof(EditorWindow).GetProperty(
                "antiAliasing",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?
                .SetValue(this, 1);
            EditorApplication.update += OnEditorUpdate;
            TurnBasedConfigEditorCatalog.Reload();
            if (formation == null)
            {
                LoadOrCreateDefault();
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            CleanupPreview();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSlots();
                GUILayout.Space(4f);
                DrawPreviewAndDetails();
            }
        }

        private void DrawSlots()
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.Width(BrowserWidth),
                       GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("18 位站位", EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                formation = (TurnBasedBattleFormationAsset)EditorGUILayout.ObjectField(
                    formation,
                    typeof(TurnBasedBattleFormationAsset),
                    false);
                if (EditorGUI.EndChangeCheck())
                {
                    SelectFormation(formation);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("新建", EditorStyles.miniButtonLeft))
                    {
                        CreateFormation();
                    }
                    if (GUILayout.Button("刷新表格", EditorStyles.miniButtonMid))
                    {
                        TurnBasedConfigEditorCatalog.Reload();
                        previewDirty = true;
                    }
                    if (GUILayout.Button("保存", EditorStyles.miniButtonRight))
                    {
                        Save();
                    }
                }

                if (!string.IsNullOrWhiteSpace(TurnBasedConfigEditorCatalog.LastError))
                {
                    EditorGUILayout.HelpBox(
                        TurnBasedConfigEditorCatalog.LastError,
                        MessageType.Error);
                }

                if (formation == null || serialized == null)
                {
                    EditorGUILayout.HelpBox("请选择或新建阵容资产。", MessageType.Info);
                    return;
                }

                slotScroll = EditorGUILayout.BeginScrollView(slotScroll);
                DrawCampSlots("攻击方（左）", 0);
                EditorGUILayout.Space(8f);
                DrawCampSlots("防守方（右）", 9);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCampSlots(string title, int offset)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            for (int row = 0; row < 3; row++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int column = 0; column < 3; column++)
                    {
                        bool attacker = offset == 0;
                        int localIndex = attacker
                            ? (2 - column) * 3 + row
                            : column * 3 + row;
                        int index = offset + localIndex;
                        TurnBasedFormationSlotAuthoring slot = formation.GetSlot(index);
                        TurnBasedConfigUnitOption option =
                            TurnBasedConfigEditorCatalog.Find(slot.Source, slot.ConfigId);
                        string label = option == null
                            ? $"{index + 1}\n<空>"
                            : $"{index + 1}\n{option.Name}";
                        Color previous = GUI.backgroundColor;
                        if (selectedSlot == index)
                        {
                            GUI.backgroundColor = new Color(0.25f, 0.68f, 1f);
                        }
                        if (GUILayout.Button(label, GUILayout.Width(82f), GUILayout.Height(48f)))
                        {
                            selectedSlot = index;
                        }
                        GUI.backgroundColor = previous;
                    }
                }
            }
        }

        private void DrawPreviewAndDetails()
        {
            using (new EditorGUILayout.VerticalScope(
                       GUILayout.ExpandWidth(true),
                       GUILayout.ExpandHeight(true)))
            {
                if (formation == null || serialized == null)
                {
                    return;
                }

                serialized.Update();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    string title = compiledExpression == null
                        ? formation.DisplayName
                        : $"{formation.DisplayName} · {compiledExpression.Id} · " +
                          $"第 {expressionFrame}/{compiledExpression.DurationFrames} 帧";
                    GUILayout.Label(title, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (compiledExpression != null)
                    {
                        GUILayout.Label(
                            $"施法位 {expressionCasterSlot + 1} → 目标位 {expressionTargetSlot + 1}",
                            EditorStyles.miniLabel);
                    }
                    GUILayout.Label("EntityModel.ModelPath · 默认 Idle", EditorStyles.miniLabel);
                    if (GUILayout.Button("重建模型", EditorStyles.toolbarButton))
                    {
                        previewDirty = true;
                    }
                }

                Rect previewRect = GUILayoutUtility.GetRect(
                    400f,
                    420f,
                    GUILayout.ExpandWidth(true));
                DrawModelPreview(previewRect);

                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                DrawSlotDetails();
                EditorGUILayout.EndScrollView();

                if (serialized.ApplyModifiedProperties())
                {
                    formation.EnsureSlots();
                    EditorUtility.SetDirty(formation);
                    previewDirty = true;
                }
            }
        }

        private void DrawSlotDetails()
        {
            SerializedProperty slots = serialized.FindProperty("slots");
            if (slots == null || slots.arraySize == 0)
            {
                return;
            }
            selectedSlot = Mathf.Clamp(selectedSlot, 0, slots.arraySize - 1);
            SerializedProperty slot = slots.GetArrayElementAtIndex(selectedSlot);
            EditorGUILayout.LabelField(
                $"{(selectedSlot < 9 ? "攻击方" : "防守方")} · 位置 {selectedSlot + 1}",
                EditorStyles.boldLabel);

            SerializedProperty sourceProperty = slot.FindPropertyRelative("source");
            BattleFormationUnitSource source = (BattleFormationUnitSource)
                sourceProperty.enumValueIndex;
            EditorGUI.BeginChangeCheck();
            source = (BattleFormationUnitSource)EditorGUILayout.EnumPopup("单位来源", source);
            if (EditorGUI.EndChangeCheck())
            {
                sourceProperty.enumValueIndex = (int)source;
                IReadOnlyList<TurnBasedConfigUnitOption> nextOptions =
                    TurnBasedConfigEditorCatalog.GetOptions(source);
                slot.FindPropertyRelative("configId").longValue =
                    nextOptions.Count > 0 ? nextOptions[0].Id : 0;
            }

            DrawUnitPopup(slot, source);
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("modelOffset"), new GUIContent("模型偏移"));
            EditorGUILayout.PropertyField(slot.FindPropertyRelative("modelScale"), new GUIContent("模型缩放"));

            long configId = slot.FindPropertyRelative("configId").longValue;
            TurnBasedConfigUnitOption option = TurnBasedConfigEditorCatalog.Find(source, configId);
            if (option != null)
            {
                EditorGUILayout.LabelField("EntityId", option.EntityId.ToString());
                EditorGUILayout.LabelField("ModelPath", option.ModelPath ?? "<空>");
                if (TurnBasedConfigEditorCatalog.LoadModelPrefab(option) == null)
                {
                    EditorGUILayout.HelpBox(
                        "ModelPath 对应的 Prefab 未找到：" + option.AssetPath,
                        MessageType.Warning);
                }
            }
        }

        private static void DrawUnitPopup(
            SerializedProperty slot,
            BattleFormationUnitSource source)
        {
            IReadOnlyList<TurnBasedConfigUnitOption> options =
                TurnBasedConfigEditorCatalog.GetOptions(source);
            SerializedProperty idProperty = slot.FindPropertyRelative("configId");
            if (source == BattleFormationUnitSource.None || options.Count == 0)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Popup("单位配置", 0, new[] { "<空>" });
                }
                idProperty.longValue = 0;
                return;
            }

            string[] labels = new string[options.Count];
            int current = 0;
            for (int index = 0; index < options.Count; index++)
            {
                labels[index] = options[index].Label;
                if (options[index].Id == idProperty.longValue)
                {
                    current = index;
                }
            }
            int next = EditorGUILayout.Popup("单位配置", current, labels);
            idProperty.longValue = options[Mathf.Clamp(next, 0, options.Count - 1)].Id;
        }

        private void DrawModelPreview(Rect rect)
        {
            if (Event.current.type != EventType.Repaint)
            {
                GUI.Box(rect, GUIContent.none);
                return;
            }
            if (previewDirty)
            {
                RebuildPreview();
            }
            if (preview == null)
            {
                EditorGUI.DrawRect(rect, new Color(0.06f, 0.07f, 0.09f));
                GUI.Label(rect, "暂无可预览模型", CenteredStyle());
                return;
            }

            EnsurePreviewTexture(rect);
            preview.camera.targetTexture = previewTexture;
            preview.camera.aspect = rect.width / Mathf.Max(1f, rect.height);
            preview.camera.Render();
            GUI.DrawTexture(rect, previewTexture, ScaleMode.StretchToFill, false);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, 180f, 22f), "攻击方 · 左侧 9 位", EditorStyles.boldLabel);
            GUI.Label(new Rect(rect.xMax - 190f, rect.y + 8f, 180f, 22f), "防守方 · 右侧 9 位", RightStyle());
            if (compiledExpression != null)
            {
                GUI.Label(
                    new Rect(rect.x + 10f, rect.yMax - 28f, 300f, 20f),
                    $"{compiledExpression.Id} · 第 {expressionFrame} 帧",
                    PreviewOverlayStyle(TextAnchor.MiddleLeft));
                for (int index = 0; index < expressionCues.Count && index < 4; index++)
                {
                    GUI.Label(
                        new Rect(rect.xMax - 310f, rect.yMax - 28f - index * 20f, 300f, 20f),
                        expressionCues[index],
                        PreviewOverlayStyle(TextAnchor.MiddleRight));
                }
            }
        }

        private void RebuildPreview()
        {
            previewDirty = false;
            CleanupPreview();
            if (formation == null)
            {
                return;
            }

            preview = new PreviewRenderUtility(true);
            preview.camera.orthographic = true;
            preview.camera.orthographicSize = 4.7f;
            preview.camera.nearClipPlane = 0.1f;
            preview.camera.farClipPlane = 100f;
            preview.camera.transform.position = new Vector3(0f, 0f, -15f);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = new Color(0.035f, 0.055f, 0.08f, 1f);
            preview.camera.allowMSAA = false;
            preview.lights[0].intensity = 1.1f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
            preview.lights[1].intensity = 0.8f;

            for (int index = 0; index < TurnBasedBattleFormationAsset.SlotCount; index++)
            {
                TurnBasedFormationSlotAuthoring slot = formation.GetSlot(index);
                TurnBasedConfigUnitOption option =
                    TurnBasedConfigEditorCatalog.Find(slot.Source, slot.ConfigId);
                GameObject prefab = TurnBasedConfigEditorCatalog.LoadModelPrefab(option);
                if (prefab == null)
                {
                    continue;
                }

                GameObject instance = Object.Instantiate(prefab);
                instance.name = $"PreviewSlot_{index}_{prefab.name}";
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);
                preview.AddSingleGO(instance);
                PlaceModel(instance, slot, index);
                TryPlayIdle(instance);
                previewSlotModels[index] = instance;
                previewHomePositions[index] = instance.transform.position;
                previewHomeScales[index] = instance.transform.localScale;
            }

            if (compiledExpression != null)
            {
                EvaluateExpressionFrame();
            }
        }

        private static void PlaceModel(
            GameObject instance,
            TurnBasedFormationSlotAuthoring slot,
            int index)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(instance.transform.position, Vector3.one);
            bool hasBounds = false;
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                if (!renderers[rendererIndex].enabled)
                {
                    continue;
                }
                if (!hasBounds)
                {
                    bounds = renderers[rendererIndex].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[rendererIndex].bounds);
                }
            }

            float fitScale = hasBounds && bounds.size.y > 0.01f
                ? 1.55f / bounds.size.y
                : 1f;
            Vector3 authoredScale = slot.ModelScale;
            bool attacker = index < 9;
            int local = index % 9;
            int row = local % 3;
            int column = local / 3;
            float x = attacker
                ? -2.15f - column * 1.45f
                : 2.15f + column * 1.45f;
            float y = 1.75f - row * 1.75f;
            instance.transform.localScale = Vector3.Scale(
                instance.transform.localScale,
                authoredScale * fitScale);
            if (!attacker)
            {
                Vector3 scale = instance.transform.localScale;
                scale.x = -Mathf.Abs(scale.x);
                instance.transform.localScale = scale;
            }
            Vector3 centerOffset = hasBounds
                ? instance.transform.position - bounds.center
                : Vector3.zero;
            instance.transform.position = new Vector3(x, y, 0f) +
                centerOffset * fitScale + slot.ModelOffset;
        }

        private void TryPlayIdle(GameObject root)
        {
            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int index = 0; index < animators.Length; index++)
            {
                Animator animator = animators[index];
                if (animator.runtimeAnimatorController != null)
                {
                    animator.Play("idle_1", 0, 0f);
                    animator.Update(0f);
                    animatedComponents.Add(animator);
                }
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || component.GetType().FullName !=
                    "Spine.Unity.SkeletonAnimation")
                {
                    continue;
                }
                try
                {
                    component.GetType().GetMethod(
                        "Initialize",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { typeof(bool) },
                        null)?.Invoke(component, new object[] { true });
                    object state = component.GetType().GetProperty("AnimationState")?
                        .GetValue(component);
                    state?.GetType().GetMethod(
                        "SetAnimation",
                        new[] { typeof(int), typeof(string), typeof(bool) })?
                        .Invoke(state, new object[] { 0, "idle_1", true });
                    animatedComponents.Add(component);
                }
                catch
                {
                    // 部分旧 Spine 资源没有 idle_1，Prefab 的默认姿势仍可用于预览。
                }
            }
        }

        private int FindPopulatedSlot(int start, int end, int fallback)
        {
            if (formation == null)
            {
                return fallback;
            }

            for (int index = start; index < end; index++)
            {
                TurnBasedFormationSlotAuthoring slot = formation.GetSlot(index);
                if (slot != null && slot.Source != BattleFormationUnitSource.None &&
                    TurnBasedConfigEditorCatalog.Find(slot.Source, slot.ConfigId) != null)
                {
                    return index;
                }
            }

            return fallback;
        }

        private static int ResolveExpressionTargetSlot(
            CompiledBattleExpression expression,
            int fallback)
        {
            CompiledBattleExpressionClip[] clips = expression.Clips;
            for (int index = 0; index < clips.Length; index++)
            {
                switch (clips[index].Type)
                {
                    case BattleExpressionClipType.MoveToTarget:
                    case BattleExpressionClipType.SwapPosition:
                    case BattleExpressionClipType.ProjectileEffect:
                    case BattleExpressionClipType.CameraFocus:
                    case BattleExpressionClipType.RefreshGrid:
                        return Mathf.Clamp(clips[index].PreviewTargetSlot - 1, 0, 17);
                }
            }

            return fallback;
        }

        private void SetExpressionFrame(int frame)
        {
            if (compiledExpression == null)
            {
                return;
            }

            expressionFrame = Mathf.Clamp(frame, 0, compiledExpression.DurationFrames);
            if (!previewDirty && preview != null)
            {
                EvaluateExpressionFrame();
            }
            Repaint();
        }

        private void ClearExpressionPreview()
        {
            expressionAsset = null;
            compiledExpression = null;
            expressionFrame = 0;
            expressionCues.Clear();
            ResetPreviewModels();
            animatedComponents.Clear();
            for (int index = 0; index < previewSlotModels.Length; index++)
            {
                if (previewSlotModels[index] != null)
                {
                    TryPlayIdle(previewSlotModels[index]);
                }
            }
            Repaint();
        }

        private void EvaluateExpressionFrame()
        {
            if (compiledExpression == null)
            {
                return;
            }

            ResetPreviewModels();
            ResetPreviewCamera();
            expressionCues.Clear();
            GameObject caster = GetPreviewModel(expressionCasterSlot);
            GameObject target = GetPreviewModel(expressionTargetSlot);
            if (caster == null || target == null)
            {
                expressionCues.Add("请在施法位和目标位配置模型");
                return;
            }

            string casterAnimation = "idle_1";
            string targetAnimation = "idle_1";
            int casterAnimationStart = 0;
            int targetAnimationStart = 0;
            bool casterAnimationLoop = true;
            bool targetAnimationLoop = true;
            CompiledBattleExpressionClip[] clips = compiledExpression.Clips;
            for (int index = 0; index < clips.Length; index++)
            {
                CompiledBattleExpressionClip clip = clips[index];
                if (expressionFrame < clip.StartFrame)
                {
                    continue;
                }

                GameObject subject = clip.Subject == BattleExpressionSubject.PrimaryTarget
                    ? target
                    : caster;
                string subjectName = clip.Subject == BattleExpressionSubject.PrimaryTarget
                    ? $"目标位 {expressionTargetSlot + 1}"
                    : $"施法位 {expressionCasterSlot + 1}";
                bool active = clip.DurationFrames <= 0
                    ? expressionFrame == clip.StartFrame
                    : expressionFrame <= clip.EndFrame;
                float normalized = clip.DurationFrames <= 0
                    ? 1f
                    : Mathf.Clamp01(
                        (expressionFrame - clip.StartFrame) /
                        (float)clip.DurationFrames);

                switch (clip.Type)
                {
                    case BattleExpressionClipType.Animation:
                        if (clip.Subject == BattleExpressionSubject.PrimaryTarget)
                        {
                            targetAnimation = clip.ResourceKey;
                            targetAnimationStart = clip.StartFrame;
                            targetAnimationLoop = false;
                        }
                        else
                        {
                            casterAnimation = clip.ResourceKey;
                            casterAnimationStart = clip.StartFrame;
                            casterAnimationLoop = false;
                        }
                        if (active)
                        {
                            AddExpressionCue(subjectName, "动作", clip.ResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.MoveToTarget:
                    {
                        int destinationSlot = Mathf.Clamp(
                            clip.PreviewTargetSlot - 1,
                            0,
                            previewSlotModels.Length - 1);
                        GameObject destinationTarget = GetPreviewModel(destinationSlot);
                        if (destinationTarget == null)
                        {
                            expressionCues.Add(
                                $"位置 {destinationSlot + 1} 未配置演示目标模型");
                            break;
                        }
                        Vector3 destination = previewHomePositions[destinationSlot] +
                            ToVector3(clip.Offset);
                        subject.transform.position = Vector3.Lerp(
                            subject.transform.position,
                            destination,
                            Mathf.SmoothStep(0f, 1f, normalized));
                        if (active)
                        {
                            AddExpressionCue(
                                subjectName,
                                "移动",
                                $"目标位置 {destinationSlot + 1}",
                                clip);
                        }
                        break;
                    }
                    case BattleExpressionClipType.MoveHome:
                    {
                        int slot = clip.Subject == BattleExpressionSubject.PrimaryTarget
                            ? expressionTargetSlot
                            : expressionCasterSlot;
                        subject.transform.position = Vector3.Lerp(
                            subject.transform.position,
                            previewHomePositions[slot],
                            Mathf.SmoothStep(0f, 1f, normalized));
                        if (active)
                        {
                            AddExpressionCue(subjectName, "移动", "返回站位", clip);
                        }
                        break;
                    }
                    case BattleExpressionClipType.SwapPosition:
                    {
                        int destinationSlot = Mathf.Clamp(
                            clip.PreviewTargetSlot - 1,
                            0,
                            previewSlotModels.Length - 1);
                        GameObject swapTarget = GetPreviewModel(destinationSlot);
                        if (swapTarget == null)
                        {
                            expressionCues.Add(
                                $"位置 {destinationSlot + 1} 未配置交换目标模型");
                            break;
                        }
                        Vector3 spacing = ToVector3(clip.Offset);
                        Vector3 subjectHome = GetSubjectHomePosition(clip.Subject);
                        Vector3 targetHome = previewHomePositions[destinationSlot];
                        subject.transform.position = Vector3.Lerp(
                            subjectHome,
                            targetHome + spacing,
                            Mathf.SmoothStep(0f, 1f, normalized));
                        swapTarget.transform.position = Vector3.Lerp(
                            targetHome,
                            subjectHome - spacing,
                            Mathf.SmoothStep(0f, 1f, normalized));
                        if (active)
                        {
                            AddExpressionCue(subjectName, "换位", $"位置 {destinationSlot + 1}", clip);
                        }
                        break;
                    }
                    case BattleExpressionClipType.Effect:
                        if (active)
                        {
                            float pulse = 0.35f + Mathf.Sin(normalized * Mathf.PI) * 0.65f;
                            SetPreviewTint(subject, ToColor(clip.Color), pulse * clip.Intensity);
                            AddExpressionCue(subjectName, "特效", clip.ResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.Audio:
                        if (active)
                        {
                            AddExpressionCue(subjectName, "声音", clip.ResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.Hit:
                        if (active)
                        {
                            float shake = Mathf.Sin(normalized * Mathf.PI * 8f) *
                                (1f - normalized) * 0.13f;
                            subject.transform.position += Vector3.right * shake;
                            if (clip.Subject == BattleExpressionSubject.PrimaryTarget)
                            {
                                targetAnimation = "hit_1";
                                targetAnimationStart = clip.StartFrame;
                                targetAnimationLoop = false;
                            }
                            else
                            {
                                casterAnimation = "hit_1";
                                casterAnimationStart = clip.StartFrame;
                                casterAnimationLoop = false;
                            }
                            AddExpressionCue(subjectName, "受击", "演示伤害", clip);
                        }
                        break;
                    case BattleExpressionClipType.ColorFlash:
                        if (active)
                        {
                            float pulse = Mathf.Sin(normalized * Mathf.PI);
                            SetPreviewTint(subject, ToColor(clip.Color), pulse * clip.Intensity);
                            AddExpressionCue(subjectName, "闪色", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.CheckDead:
                        if (active)
                        {
                            AddExpressionCue(subjectName, "逻辑", "死亡检查", clip);
                        }
                        break;
                    case BattleExpressionClipType.BuffText:
                        if (active)
                        {
                            AddExpressionCue(subjectName, "飘字", clip.ResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.CameraFocus:
                        ApplyPreviewCameraFocus(clip, subject, caster, target, normalized);
                        if (active)
                        {
                            AddExpressionCue("镜头", "聚焦", GetAnchorLabel(clip.Anchor), clip);
                        }
                        break;
                    case BattleExpressionClipType.CameraShake:
                        if (active)
                        {
                            ApplyPreviewCameraShake(clip, normalized);
                            AddExpressionCue("镜头", "震屏", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.ProjectileEffect:
                        if (active)
                        {
                            SetPreviewTint(target, ToColor(clip.Color),
                                Mathf.Sin(normalized * Mathf.PI) * clip.Intensity);
                            AddExpressionCue(subjectName, "弹道", clip.ResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.EffectAnimation:
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(subjectName, "特效动画", clip.SecondaryResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.RemoveEffect:
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(subjectName, "移除特效", clip.ResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.DarkenOthers:
                        ApplyPreviewDarkening(subject, target, ToColor(clip.Color), clip.Intensity);
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(subjectName, "压暗非目标", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.ClearDark:
                        ClearPreviewTint();
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue("场景", "清除压暗", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.SetBackgroundVisible:
                        preview.camera.backgroundColor = clip.State
                            ? new Color(0.035f, 0.055f, 0.08f, 1f)
                            : Color.black;
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue("场景", clip.State ? "显示背景" : "隐藏背景", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.SetUnitVisible:
                        SetPreviewModelVisible(subject, clip.State);
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(subjectName, clip.State ? "显示模型" : "隐藏模型", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.SetHudVisible:
                    case BattleExpressionClipType.SetShadowVisible:
                    case BattleExpressionClipType.SetPetVisible:
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(
                                subjectName,
                                TurnBasedExpressionOperationCatalog.GetDisplayName(clip.Type),
                                clip.State ? "开" : "关",
                                clip);
                        }
                        break;
                    case BattleExpressionClipType.SetSkin:
                    case BattleExpressionClipType.ChangeModel:
                    case BattleExpressionClipType.ToggleLoopEffect:
                    case BattleExpressionClipType.RefreshGrid:
                    case BattleExpressionClipType.BackgroundAudio:
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(
                                subjectName,
                                TurnBasedExpressionOperationCatalog.GetDisplayName(clip.Type),
                                clip.ResourceKey,
                                clip);
                        }
                        break;
                    case BattleExpressionClipType.SwitchForm:
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(subjectName, "切换形态", clip.Option.ToString(), clip);
                        }
                        break;
                    case BattleExpressionClipType.UnitShake:
                        if (active)
                        {
                            Vector3 axis = ToVector3(clip.Offset);
                            if (axis.sqrMagnitude <= 0.0001f)
                            {
                                axis = Vector3.right;
                            }
                            float elapsed = (expressionFrame - clip.StartFrame) /
                                (float)compiledExpression.FramesPerSecond;
                            float wave = Mathf.Sin(elapsed * clip.Frequency * Mathf.PI * 2f);
                            float envelope = Mathf.Sin(normalized * Mathf.PI);
                            subject.transform.position += axis.normalized * wave * envelope *
                                Mathf.Max(0f, clip.Intensity);
                            AddExpressionCue(subjectName, "人物抖动", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.PresentationTimeScale:
                        if (active)
                        {
                            AddExpressionCue("时间", "表现速度", $"x{clip.Intensity:0.##}", clip);
                        }
                        break;
                    case BattleExpressionClipType.LeaveField:
                        SetPreviewModelVisible(subject, false);
                        if (expressionFrame == clip.StartFrame)
                        {
                            AddExpressionCue(subjectName, "离场", string.Empty, clip);
                        }
                        break;
                    case BattleExpressionClipType.Summon:
                        SetPreviewModelVisible(subject, true);
                        SetSubjectAnimation(
                            clip,
                            ref casterAnimation,
                            ref targetAnimation,
                            ref casterAnimationStart,
                            ref targetAnimationStart,
                            ref casterAnimationLoop,
                            ref targetAnimationLoop);
                        if (active)
                        {
                            AddExpressionCue(subjectName, "召唤入场", clip.ResourceKey, clip);
                        }
                        break;
                    case BattleExpressionClipType.FloatingTip:
                    case BattleExpressionClipType.BubbleTip:
                        if (active)
                        {
                            AddExpressionCue(
                                subjectName,
                                clip.Type == BattleExpressionClipType.BubbleTip ? "气泡" : "提示",
                                clip.ResourceKey,
                                clip);
                        }
                        break;
                    case BattleExpressionClipType.HitReaction:
                        SetSubjectAnimation(
                            clip,
                            ref casterAnimation,
                            ref targetAnimation,
                            ref casterAnimationStart,
                            ref targetAnimationStart,
                            ref casterAnimationLoop,
                            ref targetAnimationLoop);
                        if (active)
                        {
                            AddExpressionCue(subjectName, "受击组合", clip.SecondaryResourceKey, clip);
                        }
                        break;
                }
            }

            float secondsPerFrame = 1f / compiledExpression.FramesPerSecond;
            PlayPreviewAnimationAtTime(
                caster,
                casterAnimation,
                Mathf.Max(0, expressionFrame - casterAnimationStart) * secondsPerFrame,
                casterAnimationLoop);
            PlayPreviewAnimationAtTime(
                target,
                targetAnimation,
                Mathf.Max(0, expressionFrame - targetAnimationStart) * secondsPerFrame,
                targetAnimationLoop);
        }

        private void ResetPreviewModels()
        {
            for (int index = 0; index < previewSlotModels.Length; index++)
            {
                GameObject model = previewSlotModels[index];
                if (model == null)
                {
                    continue;
                }

                model.SetActive(true);
                model.transform.position = previewHomePositions[index];
                model.transform.localScale = previewHomeScales[index];
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    renderers[rendererIndex].SetPropertyBlock(null);
                }
            }
        }

        private void ResetPreviewCamera()
        {
            if (preview?.camera == null)
            {
                return;
            }
            preview.camera.transform.position = new Vector3(0f, 0f, -15f);
            preview.camera.transform.rotation = Quaternion.identity;
            preview.camera.orthographicSize = 4.7f;
            preview.camera.backgroundColor =
                new Color(0.035f, 0.055f, 0.08f, 1f);
        }

        private Vector3 GetSubjectHomePosition(BattleExpressionSubject subject)
        {
            int slot = subject == BattleExpressionSubject.PrimaryTarget
                ? expressionTargetSlot
                : expressionCasterSlot;
            return previewHomePositions[Mathf.Clamp(
                slot,
                0,
                previewHomePositions.Length - 1)];
        }

        private void ApplyPreviewCameraFocus(
            in CompiledBattleExpressionClip clip,
            GameObject subject,
            GameObject caster,
            GameObject target,
            float normalized)
        {
            if (preview?.camera == null)
            {
                return;
            }

            Vector3 anchor = ResolvePreviewAnchor(clip, subject, caster, target);
            Vector3 offset = ToVector3(clip.Offset);
            Vector3 cameraHome = new Vector3(0f, 0f, -15f);
            Vector3 destination = new Vector3(
                anchor.x + offset.x,
                anchor.y + offset.y,
                cameraHome.z + offset.z);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            preview.camera.transform.position = Vector3.Lerp(
                cameraHome,
                destination,
                eased);
            preview.camera.orthographicSize = Mathf.Lerp(
                4.7f,
                Mathf.Max(0.01f, clip.Intensity),
                eased);
        }

        private void ApplyPreviewCameraShake(
            in CompiledBattleExpressionClip clip,
            float normalized)
        {
            if (preview?.camera == null)
            {
                return;
            }
            Vector3 direction = ToVector3(clip.Offset);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = new Vector3(1f, 0.35f, 0f);
            }
            float elapsed = (expressionFrame - clip.StartFrame) /
                (float)compiledExpression.FramesPerSecond;
            float wave = Mathf.Sin(elapsed * clip.Frequency * Mathf.PI * 2f);
            float envelope = Mathf.Sin(normalized * Mathf.PI);
            preview.camera.transform.position += direction.normalized * wave * envelope *
                Mathf.Max(0f, clip.Intensity);
        }

        private Vector3 ResolvePreviewAnchor(
            in CompiledBattleExpressionClip clip,
            GameObject subject,
            GameObject caster,
            GameObject target)
        {
            switch (clip.Anchor)
            {
                case BattleExpressionAnchor.Subject:
                    return subject.transform.position;
                case BattleExpressionAnchor.Caster:
                    return caster.transform.position;
                case BattleExpressionAnchor.PrimaryTarget:
                {
                    GameObject configuredTarget = GetPreviewModel(Mathf.Clamp(
                        clip.PreviewTargetSlot - 1,
                        0,
                        previewSlotModels.Length - 1));
                    return configuredTarget == null
                        ? target.transform.position
                        : configuredTarget.transform.position;
                }
                case BattleExpressionAnchor.CampCenter:
                case BattleExpressionAnchor.RowCenter:
                case BattleExpressionAnchor.ColumnCenter:
                    return CalculatePreviewFormationCenter(
                        clip.PreviewTargetSlot - 1,
                        clip.Anchor,
                        target.transform.position);
                case BattleExpressionAnchor.ScreenCenter:
                    return Vector3.zero;
                default:
                    return subject.transform.position;
            }
        }

        private Vector3 CalculatePreviewFormationCenter(
            int targetSlot,
            BattleExpressionAnchor anchor,
            Vector3 fallback)
        {
            int safeSlot = Mathf.Clamp(targetSlot, 0, previewSlotModels.Length - 1);
            int sideStart = safeSlot < 9 ? 0 : 9;
            int local = safeSlot - sideStart;
            int targetRow = local / 3;
            int targetColumn = local % 3;
            Vector3 sum = Vector3.zero;
            int count = 0;
            for (int index = sideStart; index < sideStart + 9; index++)
            {
                if (previewSlotModels[index] == null)
                {
                    continue;
                }
                int indexLocal = index - sideStart;
                if (anchor == BattleExpressionAnchor.RowCenter &&
                    indexLocal / 3 != targetRow)
                {
                    continue;
                }
                if (anchor == BattleExpressionAnchor.ColumnCenter &&
                    indexLocal % 3 != targetColumn)
                {
                    continue;
                }
                sum += previewHomePositions[index];
                count++;
            }
            return count > 0 ? sum / count : fallback;
        }

        private void ApplyPreviewDarkening(
            GameObject subject,
            GameObject primaryTarget,
            Color color,
            float intensity)
        {
            for (int index = 0; index < previewSlotModels.Length; index++)
            {
                GameObject model = previewSlotModels[index];
                if (model != null && model != subject && model != primaryTarget)
                {
                    SetPreviewTint(model, color, intensity);
                }
            }
        }

        private void ClearPreviewTint()
        {
            for (int index = 0; index < previewSlotModels.Length; index++)
            {
                GameObject model = previewSlotModels[index];
                if (model == null)
                {
                    continue;
                }
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    renderers[rendererIndex].SetPropertyBlock(null);
                }
            }
        }

        private static void SetPreviewModelVisible(GameObject model, bool visible)
        {
            if (model != null && model.activeSelf != visible)
            {
                model.SetActive(visible);
            }
        }

        private static void SetSubjectAnimation(
            in CompiledBattleExpressionClip clip,
            ref string casterAnimation,
            ref string targetAnimation,
            ref int casterAnimationStart,
            ref int targetAnimationStart,
            ref bool casterAnimationLoop,
            ref bool targetAnimationLoop)
        {
            if (string.IsNullOrWhiteSpace(clip.ResourceKey))
            {
                return;
            }
            if (clip.Subject == BattleExpressionSubject.PrimaryTarget)
            {
                targetAnimation = clip.ResourceKey;
                targetAnimationStart = clip.StartFrame;
                targetAnimationLoop = clip.Loop;
            }
            else
            {
                casterAnimation = clip.ResourceKey;
                casterAnimationStart = clip.StartFrame;
                casterAnimationLoop = clip.Loop;
            }
        }

        private static string GetAnchorLabel(BattleExpressionAnchor anchor)
        {
            switch (anchor)
            {
                case BattleExpressionAnchor.Subject:
                    return "执行对象";
                case BattleExpressionAnchor.Caster:
                    return "施法者";
                case BattleExpressionAnchor.PrimaryTarget:
                    return "逻辑主目标";
                case BattleExpressionAnchor.CampCenter:
                    return "阵营中心";
                case BattleExpressionAnchor.RowCenter:
                    return "排中心";
                case BattleExpressionAnchor.ColumnCenter:
                    return "列中心";
                case BattleExpressionAnchor.ScreenCenter:
                    return "屏幕中心";
                default:
                    return anchor.ToString();
            }
        }

        private GameObject GetPreviewModel(int slot)
        {
            return slot >= 0 && slot < previewSlotModels.Length
                ? previewSlotModels[slot]
                : null;
        }

        private void SetPreviewTint(GameObject root, Color color, float intensity)
        {
            Color tint = Color.Lerp(Color.white, color, Mathf.Clamp01(intensity));
            if (expressionPropertyBlock == null)
            {
                expressionPropertyBlock = new MaterialPropertyBlock();
            }
            expressionPropertyBlock.Clear();
            expressionPropertyBlock.SetColor("_BaseColor", tint);
            expressionPropertyBlock.SetColor("_Color", tint);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                renderers[index].SetPropertyBlock(expressionPropertyBlock);
            }
        }

        private void AddExpressionCue(
            string subject,
            string operation,
            string resource,
            in CompiledBattleExpressionClip clip)
        {
            string logic = string.IsNullOrWhiteSpace(clip.LogicOutputKey)
                ? string.Empty
                : $" [{clip.LogicOutputKey}]";
            string resourceText = string.IsNullOrWhiteSpace(resource)
                ? string.Empty
                : $" {resource}";
            expressionCues.Add($"{subject} · {operation}{resourceText}{logic}");
        }

        private static Vector3 ToVector3(BattleExpressionVector value)
        {
            return new Vector3(value.X, value.Y, value.Z);
        }

        private static Color ToColor(BattleExpressionColor value)
        {
            return new Color(value.Red, value.Green, value.Blue, value.Alpha);
        }

        private static void PlayPreviewAnimationAtTime(
            GameObject root,
            string animationName,
            float elapsed,
            bool loop)
        {
            if (root == null || string.IsNullOrWhiteSpace(animationName))
            {
                return;
            }

            Animator[] animators = root.GetComponentsInChildren<Animator>(true);
            for (int index = 0; index < animators.Length; index++)
            {
                Animator animator = animators[index];
                if (animator.runtimeAnimatorController == null)
                {
                    continue;
                }
                animator.Play(animationName, 0, 0f);
                animator.Update(elapsed);
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == null || component.GetType().FullName !=
                    "Spine.Unity.SkeletonAnimation")
                {
                    continue;
                }

                try
                {
                    Type type = component.GetType();
                    type.GetMethod(
                        "Initialize",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { typeof(bool) },
                        null)?.Invoke(component, new object[] { true });
                    object state = type.GetProperty("AnimationState")?.GetValue(component);
                    state?.GetType().GetMethod(
                        "SetAnimation",
                        new[] { typeof(int), typeof(string), typeof(bool) })?
                        .Invoke(state, new object[] { 0, animationName, loop });
                    type.GetMethod(
                        "Update",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { typeof(float) },
                        null)?.Invoke(component, new object[] { elapsed });
                    type.GetMethod(
                        "LateUpdate",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null)?.Invoke(component, null);
                }
                catch
                {
                    // 资源缺少目标动作时保留当前姿势，避免打断其他模型的演示。
                }
            }
        }

        private void AdvanceAnimations(float deltaTime)
        {
            for (int index = animatedComponents.Count - 1; index >= 0; index--)
            {
                Component component = animatedComponents[index];
                if (component == null)
                {
                    animatedComponents.RemoveAt(index);
                    continue;
                }
                if (component is Animator animator)
                {
                    animator.Update(deltaTime);
                    continue;
                }
                try
                {
                    component.GetType().GetMethod(
                        "Update",
                        BindingFlags.Instance | BindingFlags.Public,
                        null,
                        new[] { typeof(float) },
                        null)?.Invoke(component, new object[] { deltaTime });
                    component.GetType().GetMethod(
                        "LateUpdate",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null)?.Invoke(component, null);
                }
                catch
                {
                    animatedComponents.RemoveAt(index);
                }
            }
        }

        private void CleanupPreview()
        {
            animatedComponents.Clear();
            expressionCues.Clear();
            Array.Clear(previewSlotModels, 0, previewSlotModels.Length);
            Array.Clear(previewHomePositions, 0, previewHomePositions.Length);
            Array.Clear(previewHomeScales, 0, previewHomeScales.Length);
            ReleasePreviewTexture();
            if (preview != null)
            {
                preview.Cleanup();
                preview = null;
            }
        }

        private void EnsurePreviewTexture(Rect rect)
        {
            int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
            int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
            if (previewTexture != null && previewTexture.width == width &&
                previewTexture.height == height)
            {
                return;
            }

            ReleasePreviewTexture();
            previewTexture = new RenderTexture(
                width,
                height,
                24,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default)
            {
                antiAliasing = 1,
                hideFlags = HideFlags.HideAndDontSave,
                name = "TurnBasedFormationPreview",
            };
            previewTexture.Create();
        }

        private void ReleasePreviewTexture()
        {
            if (previewTexture == null)
            {
                return;
            }

            if (preview != null && preview.camera != null &&
                preview.camera.targetTexture == previewTexture)
            {
                preview.camera.targetTexture = null;
            }
            previewTexture.Release();
            DestroyImmediate(previewTexture);
            previewTexture = null;
        }

        private void LoadOrCreateDefault()
        {
            TurnBasedBattleFormationAsset asset =
                AssetDatabase.LoadAssetAtPath<TurnBasedBattleFormationAsset>(DefaultAssetPath);
            if (asset == null)
            {
                TurnBasedAssetEditorUtility.EnsureFolder(
                    "Assets/GameResources/Battle/TurnBased/Formations");
                asset = CreateInstance<TurnBasedBattleFormationAsset>();
                asset.ResetToSample();
                AssetDatabase.CreateAsset(asset, DefaultAssetPath);
                AssetDatabase.SaveAssets();
            }
            SelectFormation(asset);
        }

        private void CreateFormation()
        {
            TurnBasedBattleFormationAsset asset =
                TurnBasedAssetEditorUtility.CreateAsset<TurnBasedBattleFormationAsset>(
                    "Assets/GameResources/Battle/TurnBased/Formations",
                    "TurnBasedBattleFormation",
                    value => value.EnsureSlots());
            if (asset != null)
            {
                SelectFormation(asset);
            }
        }

        private void SelectFormation(TurnBasedBattleFormationAsset asset)
        {
            formation = asset;
            formation?.EnsureSlots();
            serialized = formation == null ? null : new SerializedObject(formation);
            selectedSlot = Mathf.Clamp(selectedSlot, 0, 17);
            previewDirty = true;
            Repaint();
        }

        private void Save()
        {
            if (formation == null)
            {
                return;
            }
            serialized?.ApplyModifiedProperties();
            formation.EnsureSlots();
            EditorUtility.SetDirty(formation);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("18 位阵容已保存"));
        }

        private void OnEditorUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float delta = lastEditorTime <= 0d
                ? 0f
                : Mathf.Min(0.05f, (float)(now - lastEditorTime));
            lastEditorTime = now;
            if (compiledExpression == null && delta > 0f &&
                animatedComponents.Count > 0)
            {
                AdvanceAnimations(delta);
                Repaint();
            }
        }

        private static GUIStyle CenteredStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
            };
        }

        private static GUIStyle RightStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleRight,
            };
        }

        private static GUIStyle PreviewOverlayStyle(TextAnchor alignment)
        {
            var style = new GUIStyle(EditorStyles.miniBoldLabel)
            {
                alignment = alignment,
            };
            style.normal.textColor = Color.white;
            return style;
        }
    }
}
