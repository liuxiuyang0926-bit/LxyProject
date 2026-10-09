using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.RuntimeData;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Game.Battle.Editor
{
    public sealed class TurnBasedExpressionEditorWindow : EditorWindow
    {
        private const float BrowserWidth = 250f;
        private const float TrackLabelWidth = 155f;
        private static readonly GUIContent[] AnchorOptions =
        {
            new GUIContent("执行对象"),
            new GUIContent("施法者"),
            new GUIContent("逻辑主目标"),
            new GUIContent("目标阵营中心"),
            new GUIContent("目标所在排中心"),
            new GUIContent("目标所在列中心"),
            new GUIContent("屏幕中心"),
        };
        private static readonly GUIContent[] SubjectOptions =
        {
            new GUIContent("施法者", "当前释放技能的角色，与攻击/防守阵营无关"),
            new GUIContent("逻辑主目标", "技能逻辑选中的主目标，也可以是治疗或增益的目标"),
        };
        private static readonly string[] ExecutionModeOptions =
        {
            "顺序（不可重叠）",
            "并行（允许重叠）",
        };

        private List<TurnBasedSkillExpressionAsset> assets =
            new List<TurnBasedSkillExpressionAsset>();
        private TurnBasedSkillExpressionAsset selected;
        private SerializedObject serialized;
        private ReorderableList trackList;
        private ReorderableList operationList;
        private Vector2 assetScroll;
        private Vector2 detailScroll;
        private string search = string.Empty;
        private int selectedTrack;
        private int selectedOperation;
        private int previewFrame;
        private bool previewPlaying;
        private double lastEditorTime;
        private CompiledBattleExpression compiledPreview;
        private TurnBasedSkillExpressionAsset pendingSelection;
        private int pendingTrack = -1;
        private int pendingOperation = -1;
        private string validationMessage;
        private MessageType validationType;
        private bool showGuide;

        [MenuItem("工具/战斗/技能表现编辑器", false, 1)]
        public static void Open()
        {
            Open(null);
        }

        internal static void Open(TurnBasedSkillExpressionAsset asset)
        {
            var window = GetWindow<TurnBasedExpressionEditorWindow>();
            window.titleContent = new GUIContent("技能表现编辑器");
            window.minSize = new Vector2(980f, 650f);
            window.ReloadAssets(asset);
            window.Show();
        }

        private void OnEnable()
        {
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
            ReloadAssets();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            TurnBasedFormationPreviewWindow.EndExpressionPreview(selected);
        }

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                if (pendingSelection != null)
                {
                    Select(pendingSelection);
                    pendingSelection = null;
                }
                if (pendingTrack >= 0)
                {
                    selectedTrack = pendingTrack;
                    selectedOperation = Math.Max(0, pendingOperation);
                    pendingTrack = pendingOperation = -1;
                    if (trackList != null) trackList.index = selectedTrack;
                    BuildOperationList();
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawBrowser();
                GUILayout.Space(4f);
                DrawDetails();
            }
        }

        private void DrawBrowser()
        {
            using (new EditorGUILayout.VerticalScope(
                       EditorStyles.helpBox,
                       GUILayout.Width(BrowserWidth),
                       GUILayout.ExpandHeight(true)))
            {
                EditorGUILayout.LabelField("表现资产", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent("搜索", "按技能标识、资产名或中文说明搜索"), GUILayout.Width(34));
                    search = EditorGUILayout.TextField(search);
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("新建", EditorStyles.miniButtonLeft))
                    {
                        CreateAsset();
                    }
                    if (GUILayout.Button("刷新", EditorStyles.miniButtonRight))
                    {
                        ReloadAssets();
                    }
                }

                assetScroll = EditorGUILayout.BeginScrollView(assetScroll);
                int visibleCount = 0;
                for (int index = 0; index < assets.Count; index++)
                {
                    TurnBasedSkillExpressionAsset asset = assets[index];
                    string label = string.IsNullOrWhiteSpace(asset.ExpressionId)
                        ? asset.name
                        : asset.ExpressionId;
                    if (!TurnBasedSkillEditorUtility.MatchesSearch(search, label, asset.name, asset.Description))
                    {
                        continue;
                    }
                    if (GUILayout.Button(
                            TurnBasedSkillEditorUtility.AssetLabel(label, asset.Description),
                            TurnBasedAssetEditorUtility.SelectedListStyle(asset == selected),
                            GUILayout.Height(44f)))
                    {
                        pendingSelection = asset;
                        Repaint();
                    }
                    visibleCount++;
                }
                if (visibleCount == 0) EditorGUILayout.HelpBox("没有匹配的技能，可清空搜索或新建。", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.LabelField($"显示 {visibleCount} / {assets.Count} 个技能", EditorStyles.miniLabel);
            }
        }

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope(
                       GUILayout.ExpandWidth(true),
                       GUILayout.ExpandHeight(true)))
            {
                if (selected == null || serialized == null)
                {
                    EditorGUILayout.HelpBox("请从左侧选择或新建一个表现资产。", MessageType.Info);
                    return;
                }

                serialized.Update();
                if (DrawToolbar())
                {
                    return;
                }
                if (!string.IsNullOrEmpty(validationMessage))
                    EditorGUILayout.HelpBox(validationMessage, validationType);
                if (selected.UsesLegacyLayout)
                {
                    EditorGUILayout.HelpBox(
                        "该资产仍是旧版平铺片段；迁移后才能按并行组和轨道编辑。",
                        MessageType.Warning);
                    if (GUILayout.Button("迁移为主并行轨"))
                    {
                        Undo.RecordObject(selected, "迁移表现轨道");
                        selected.UpgradeLegacyLayout();
                        EditorUtility.SetDirty(selected);
                        Select(selected);
                        return;
                    }
                }

                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                EditorGUILayout.PropertyField(serialized.FindProperty("expressionId"), new GUIContent("表现标识"));
                EditorGUILayout.PropertyField(serialized.FindProperty("logicSource"), new GUIContent("逻辑数据源"));
                EditorGUILayout.PropertyField(serialized.FindProperty("framesPerSecond"), new GUIContent("每秒帧数"));
                EditorGUILayout.PropertyField(serialized.FindProperty("autoDuration"), new GUIContent("自动计算总帧数"));
                using (new EditorGUI.DisabledScope(serialized.FindProperty("autoDuration").boolValue))
                {
                    if (serialized.FindProperty("autoDuration").boolValue)
                        EditorGUILayout.IntField("总帧数", GetTimelineDuration());
                    else
                        EditorGUILayout.PropertyField(serialized.FindProperty("durationFrames"), new GUIContent("总帧数"));
                }
                EditorGUILayout.PropertyField(serialized.FindProperty("description"), new GUIContent("说明"));

                EditorGUILayout.Space(8f);
                showGuide = EditorGUILayout.Foldout(showGuide, "配置与预览说明", true);
                if (showGuide)
                    EditorGUILayout.HelpBox("先添加轨道，再选择动作、特效或镜头等操作，填写开始帧和持续帧数。所有开始帧都从技能开始计算；顺序轨不允许操作重叠，并行轨允许重叠。\n点击时间轴色块可定位操作，点击刻度可定位预览帧。在上方选择预览施法者和目标；选择“自动”时沿用阵容和操作的演示站位。伤害与死亡结果由技能逻辑提供。", MessageType.Info);
                DrawTimeline();
                trackList?.DoLayoutList();
                DrawSelectedTrackAndOperations();
                EditorGUILayout.EndScrollView();

                if (serialized.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(selected);
                    previewFrame = Mathf.Min(previewFrame, selected.DurationFrames);
                    previewPlaying = false;
                    compiledPreview = null;
                    validationMessage = null;
                    TurnBasedFormationPreviewWindow.EndExpressionPreview(selected);
                    Repaint();
                }
            }
        }

        private bool DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(selected.ExpressionId, EditorStyles.boldLabel, GUILayout.MinWidth(50), GUILayout.MaxWidth(200));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(previewPlaying ? "暂停" : "播放", EditorStyles.toolbarButton))
                {
                    if (previewPlaying)
                    {
                        previewPlaying = false;
                    }
                    else if (ShowPreviewAtCurrentFrame())
                    {
                        previewPlaying = true;
                        lastEditorTime = EditorApplication.timeSinceStartup;
                    }
                }
                if (GUILayout.Button("停止", EditorStyles.toolbarButton))
                {
                    StopPreview(true);
                }
                if (GUILayout.Button("阵容模型预览", EditorStyles.toolbarButton))
                {
                    ShowPreviewAtCurrentFrame();
                }
                if (GUILayout.Button("定位", EditorStyles.toolbarButton))
                {
                    Selection.activeObject = selected;
                    EditorGUIUtility.PingObject(selected);
                }
                if (GUILayout.Button(
                        new GUIContent("移除", "将当前技能表现 ScriptableObject 移到系统回收站"),
                        EditorStyles.toolbarButton))
                {
                    RemoveSelectedAsset();
                    return true;
                }
                if (GUILayout.Button("保存", EditorStyles.toolbarButton))
                {
                    Save();
                }
                if (GUILayout.Button("检查配置", EditorStyles.toolbarButton))
                {
                    ValidateAsset();
                }
            }

            TurnBasedFormationPreviewWindow.DrawExpressionActorSettings();
            int duration = GetTimelineDuration();
            int nextFrame = EditorGUILayout.IntSlider(
                "预览帧",
                previewFrame,
                0,
                duration);
            if (nextFrame != previewFrame)
            {
                previewFrame = nextFrame;
                if (compiledPreview != null)
                {
                    TurnBasedFormationPreviewWindow.SetExpressionPreviewFrame(
                        selected,
                        previewFrame);
                }
            }
            EditorGUILayout.LabelField($"当前 {previewFrame} 帧 / {previewFrame / (float)Math.Max(1, selected.FramesPerSecond):0.00} 秒　　总长 {duration} 帧 / {duration / (float)Math.Max(1, selected.FramesPerSecond):0.00} 秒", EditorStyles.miniLabel);
            return false;
        }

        private void DrawSelectedTrackAndOperations()
        {
            SerializedProperty tracks = serialized.FindProperty("tracks");
            if (tracks == null || tracks.arraySize == 0)
            {
                operationList = null;
                EditorGUILayout.HelpBox("尚无表现轨道。点击上方轨道列表的“＋”，再选择要添加的表现操作。", MessageType.Info);
                return;
            }

            selectedTrack = Mathf.Clamp(selectedTrack, 0, tracks.arraySize - 1);
            SerializedProperty track = tracks.GetArrayElementAtIndex(selectedTrack);
            SirenixEditorGUI.BeginBox("选中表现轨", false);
            try
            {
                EditorGUILayout.PropertyField(track.FindPropertyRelative("trackName"), new GUIContent("轨道名称"));
                EditorGUILayout.PropertyField(track.FindPropertyRelative("parallelGroup"), new GUIContent("并行组"));
                DrawExecutionMode(track.FindPropertyRelative("executionMode"));
                operationList?.DoLayoutList();
                DrawSelectedOperation(track.FindPropertyRelative("operations"));
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }
        }

        private static void DrawExecutionMode(SerializedProperty property)
        {
            int current = Mathf.Clamp(property.intValue, 0, 1);
            property.intValue = EditorGUILayout.Popup(
                new GUIContent("轨内方式"),
                current,
                ExecutionModeOptions);
        }

        private void DrawSelectedOperation(SerializedProperty operations)
        {
            if (operations == null || operations.arraySize == 0)
            {
                EditorGUILayout.HelpBox("这条轨道尚无操作。点击列表下方“＋”，可按角色、特效、音频或镜头分类添加。", MessageType.Info);
                return;
            }
            selectedOperation = Mathf.Clamp(selectedOperation, 0, operations.arraySize - 1);
            SerializedProperty operation = operations.GetArrayElementAtIndex(selectedOperation);
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField("帧操作内容", EditorStyles.boldLabel);
            SerializedProperty typeProperty = operation.FindPropertyRelative("type");
            BattleExpressionClipType operationType =
                (BattleExpressionClipType)typeProperty.intValue;
            int currentMarker = TurnBasedExpressionOperationCatalog.IndexOf(operationType);
            int nextMarker = EditorGUILayout.Popup(
                new GUIContent("操作类型", "选择标准表现操作，不需要手工输入名称。"),
                currentMarker,
                TurnBasedExpressionOperationCatalog.Options);
            if (nextMarker != currentMarker)
            {
                ApplyOperationDescriptor(
                    operation,
                    TurnBasedExpressionOperationCatalog.All[nextMarker],
                    true);
                operationType = (BattleExpressionClipType)typeProperty.intValue;
            }

            TurnBasedExpressionOperationDescriptor descriptor =
                TurnBasedExpressionOperationCatalog.Get(operationType);
            SerializedProperty operationName =
                operation.FindPropertyRelative("operationName");
            EditorGUILayout.PropertyField(operationName, new GUIContent("操作备注", "给同类操作起不同的名字，方便在列表和时间轴中区分。"));
            EditorGUILayout.HelpBox(descriptor.Description, MessageType.None);
            DrawLogicOutputPopup(operation.FindPropertyRelative("logicOutputKey"));
            if (UsesSubject(operationType))
            {
                SerializedProperty subject = operation.FindPropertyRelative("subject");
                subject.intValue = EditorGUILayout.Popup(
                    new GUIContent("执行对象"),
                    Mathf.Clamp(subject.intValue, 0, SubjectOptions.Length - 1),
                    SubjectOptions);
            }

            SerializedProperty start = operation.FindPropertyRelative("startFrame");
            SerializedProperty duration = operation.FindPropertyRelative("durationFrames");
            EditorGUI.BeginChangeCheck();
            int startFrame = Mathf.Max(0, EditorGUILayout.IntField("开始帧", start.intValue));
            int durationFrames = Mathf.Max(0, EditorGUILayout.IntField(new GUIContent("持续帧数", "修改开始帧只移动操作，不改变持续时长。0 表示瞬间执行。"), duration.intValue));
            if (EditorGUI.EndChangeCheck())
                TurnBasedSkillEditorUtility.SetTiming(operation, startFrame, durationFrames);
            int fps = Math.Max(1, serialized.FindProperty("framesPerSecond").intValue);
            EditorGUILayout.LabelField($"结束于第 {TurnBasedSkillEditorUtility.EndFrame(operation)} 帧 · 开始 {startFrame / (float)fps:0.00} 秒 · 持续 {durationFrames / (float)fps:0.00} 秒", EditorStyles.miniLabel);
            if (GUILayout.Button("从该操作开始预览", EditorStyles.miniButton))
            {
                previewPlaying = false;
                previewFrame = startFrame;
                ShowPreviewAtCurrentFrame();
            }

            DrawOperationParameters(operation, operationType);
        }

        private static void DrawOperationParameters(
            SerializedProperty operation,
            BattleExpressionClipType operationType)
        {
            switch (operationType)
            {
                case BattleExpressionClipType.Animation:
                    DrawText(operation, "resourceKey", "动作名称");
                    DrawBool(operation, "loop", "循环播放");
                    break;
                case BattleExpressionClipType.MoveToTarget:
                    DrawText(operation, "resourceKey", "移动动作（可选）");
                    DrawDynamicTarget(operation, "演示目标位置");
                    DrawVector(operation, "offset", "目标相对偏移");
                    break;
                case BattleExpressionClipType.MoveHome:
                    DrawText(operation, "resourceKey", "返回动作（可选）");
                    break;
                case BattleExpressionClipType.SwapPosition:
                    DrawText(operation, "resourceKey", "交换动作（可选）");
                    DrawDynamicTarget(operation, "演示交换位置");
                    DrawVector(operation, "offset", "双方间距偏移");
                    break;
                case BattleExpressionClipType.Summon:
                    DrawText(operation, "resourceKey", "入场动作（可选）");
                    break;
                case BattleExpressionClipType.Hit:
                    DrawFloat(operation, "intensity", "受击抖动倍率", 0f);
                    break;
                case BattleExpressionClipType.HitReaction:
                    DrawText(operation, "resourceKey", "受击动作");
                    DrawText(operation, "secondaryResourceKey", "受击特效标识（可选）");
                    DrawText(operation, "anchorKey", "特效挂点（可选）");
                    DrawVector(operation, "offset", "特效偏移");
                    break;
                case BattleExpressionClipType.CheckDead:
                case BattleExpressionClipType.LeaveField:
                case BattleExpressionClipType.ClearDark:
                    break;
                case BattleExpressionClipType.SetUnitVisible:
                case BattleExpressionClipType.SetHudVisible:
                case BattleExpressionClipType.SetShadowVisible:
                case BattleExpressionClipType.SetPetVisible:
                case BattleExpressionClipType.SetBackgroundVisible:
                    DrawBool(operation, "state", "显示");
                    break;
                case BattleExpressionClipType.SetSkin:
                    DrawText(operation, "resourceKey", "皮肤名称");
                    break;
                case BattleExpressionClipType.SwitchForm:
                    DrawInt(operation, "option", "形态序号", 0);
                    DrawText(operation, "resourceKey", "切换后动作（可选）");
                    break;
                case BattleExpressionClipType.ChangeModel:
                    DrawText(operation, "resourceKey", "模型资源标识");
                    break;
                case BattleExpressionClipType.UnitShake:
                    DrawVector(operation, "offset", "震动轴向");
                    DrawFloat(operation, "intensity", "震动强度", 0f);
                    DrawFloat(operation, "frequency", "震动频率", 0f);
                    break;
                case BattleExpressionClipType.ColorFlash:
                    DrawColor(operation, "color", "闪烁颜色");
                    DrawFloat(operation, "intensity", "颜色强度", 0f);
                    break;
                case BattleExpressionClipType.Effect:
                    DrawEffectParameters(operation, true);
                    DrawBool(operation, "loop", "循环特效");
                    break;
                case BattleExpressionClipType.ProjectileEffect:
                    DrawText(operation, "resourceKey", "弹道特效标识");
                    DrawText(operation, "anchorKey", "起点挂点（可选）");
                    DrawText(operation, "secondaryResourceKey", "终点挂点（可选）");
                    DrawVector(operation, "offset", "起点偏移");
                    DrawVector(operation, "targetOffset", "终点偏移");
                    DrawDynamicTarget(operation, "演示终点位置");
                    break;
                case BattleExpressionClipType.EffectAnimation:
                    DrawText(operation, "resourceKey", "已有特效标识");
                    DrawText(operation, "secondaryResourceKey", "动画或触发器名称");
                    break;
                case BattleExpressionClipType.RemoveEffect:
                    DrawText(operation, "resourceKey", "待移除特效标识");
                    break;
                case BattleExpressionClipType.ToggleLoopEffect:
                    DrawText(operation, "resourceKey", "常驻特效标识");
                    DrawText(operation, "anchorKey", "挂点（可选）");
                    DrawVector(operation, "offset", "挂点偏移");
                    DrawBool(operation, "state", "开启");
                    break;
                case BattleExpressionClipType.Audio:
                case BattleExpressionClipType.BackgroundAudio:
                    DrawText(operation, "resourceKey", operationType ==
                        BattleExpressionClipType.Audio ? "音效资源标识" : "背景音乐资源标识");
                    DrawBool(operation, "loop", "循环播放");
                    DrawFloat(operation, "intensity", "音量", 0f, 1f);
                    break;
                case BattleExpressionClipType.CameraFocus:
                    DrawAnchor(operation);
                    DrawDynamicTarget(operation, "演示锚点位置");
                    DrawVector(operation, "offset", "镜头相对偏移");
                    DrawFloat(operation, "intensity", "正交镜头大小", 0.01f);
                    break;
                case BattleExpressionClipType.CameraShake:
                    DrawVector(operation, "offset", "震动方向");
                    DrawFloat(operation, "intensity", "震动强度", 0f);
                    DrawFloat(operation, "frequency", "震动频率", 0f);
                    break;
                case BattleExpressionClipType.DarkenOthers:
                    DrawColor(operation, "color", "压暗颜色");
                    DrawFloat(operation, "intensity", "压暗强度", 0f, 1f);
                    break;
                case BattleExpressionClipType.RefreshGrid:
                    DrawDynamicTarget(operation, "演示格子位置");
                    break;
                case BattleExpressionClipType.BuffText:
                case BattleExpressionClipType.FloatingTip:
                case BattleExpressionClipType.BubbleTip:
                    DrawText(operation, "resourceKey", operationType ==
                        BattleExpressionClipType.BuffText ? "状态文本或样式标识" :
                        operationType == BattleExpressionClipType.FloatingTip ?
                            "提示文本或配置标识" : "气泡文本或配置标识");
                    DrawColor(operation, "color", "文字颜色");
                    break;
                case BattleExpressionClipType.PresentationTimeScale:
                    DrawFloat(operation, "intensity", "表现速度倍率", 0.01f, 8f);
                    break;
            }
        }

        private static void DrawEffectParameters(
            SerializedProperty operation,
            bool includeColor)
        {
            DrawText(operation, "resourceKey", "特效资源标识");
            DrawText(operation, "anchorKey", "模型挂点（可选）");
            DrawVector(operation, "offset", "挂点偏移");
            if (includeColor)
            {
                DrawColor(operation, "color", "叠加颜色");
                DrawFloat(operation, "intensity", "颜色强度", 0f);
            }
        }

        private static void DrawDynamicTarget(
            SerializedProperty operation,
            string previewLabel)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField("运行时目标", "技能逻辑主目标（动态）");
            }
            SerializedProperty targetSlot =
                operation.FindPropertyRelative("previewTargetSlot");
            int currentSlot = targetSlot.intValue < 1
                ? 10
                : Mathf.Clamp(targetSlot.intValue, 1, 18);
            targetSlot.intValue = Mathf.Clamp(
                EditorGUILayout.IntField(previewLabel, currentSlot),
                1,
                18);
        }

        private static void DrawAnchor(SerializedProperty operation)
        {
            SerializedProperty property = operation.FindPropertyRelative("anchor");
            property.intValue = EditorGUILayout.Popup(
                new GUIContent("镜头锚点"),
                Mathf.Clamp(property.intValue, 0, AnchorOptions.Length - 1),
                AnchorOptions);
        }

        private static void DrawText(
            SerializedProperty operation,
            string propertyName,
            string label)
        {
            EditorGUILayout.PropertyField(
                operation.FindPropertyRelative(propertyName),
                new GUIContent(label));
        }

        private static void DrawVector(
            SerializedProperty operation,
            string propertyName,
            string label)
        {
            EditorGUILayout.PropertyField(
                operation.FindPropertyRelative(propertyName),
                new GUIContent(label));
        }

        private static void DrawColor(
            SerializedProperty operation,
            string propertyName,
            string label)
        {
            EditorGUILayout.PropertyField(
                operation.FindPropertyRelative(propertyName),
                new GUIContent(label));
        }

        private static void DrawBool(
            SerializedProperty operation,
            string propertyName,
            string label)
        {
            EditorGUILayout.PropertyField(
                operation.FindPropertyRelative(propertyName),
                new GUIContent(label));
        }

        private static void DrawInt(
            SerializedProperty operation,
            string propertyName,
            string label,
            int minimum)
        {
            SerializedProperty property = operation.FindPropertyRelative(propertyName);
            property.intValue = Mathf.Max(
                minimum,
                EditorGUILayout.IntField(label, property.intValue));
        }

        private static void DrawFloat(
            SerializedProperty operation,
            string propertyName,
            string label,
            float minimum,
            float maximum = float.PositiveInfinity)
        {
            SerializedProperty property = operation.FindPropertyRelative(propertyName);
            float value = EditorGUILayout.FloatField(label, property.floatValue);
            property.floatValue = Mathf.Clamp(value, minimum, maximum);
        }

        private static bool UsesSubject(BattleExpressionClipType type)
        {
            switch (type)
            {
                case BattleExpressionClipType.CameraShake:
                case BattleExpressionClipType.ClearDark:
                case BattleExpressionClipType.SetBackgroundVisible:
                case BattleExpressionClipType.BackgroundAudio:
                case BattleExpressionClipType.PresentationTimeScale:
                    return false;
                default:
                    return true;
            }
        }

        private static void ApplyOperationDescriptor(
            SerializedProperty operation,
            TurnBasedExpressionOperationDescriptor descriptor,
            bool resetParameters)
        {
            operation.FindPropertyRelative("type").intValue = (int)descriptor.Type;
            operation.FindPropertyRelative("operationName").stringValue =
                descriptor.DisplayName;
            operation.FindPropertyRelative("durationFrames").intValue =
                descriptor.DefaultDurationFrames;
            if (!resetParameters)
            {
                return;
            }

            operation.FindPropertyRelative("resourceKey").stringValue = string.Empty;
            operation.FindPropertyRelative("secondaryResourceKey").stringValue = string.Empty;
            operation.FindPropertyRelative("anchorKey").stringValue = string.Empty;
            operation.FindPropertyRelative("anchor").intValue =
                (int)BattleExpressionAnchor.PrimaryTarget;
            operation.FindPropertyRelative("previewTargetSlot").intValue = 10;
            operation.FindPropertyRelative("offset").vector3Value = Vector3.zero;
            operation.FindPropertyRelative("targetOffset").vector3Value = Vector3.zero;
            operation.FindPropertyRelative("color").colorValue = Color.white;
            operation.FindPropertyRelative("intensity").floatValue =
                descriptor.DefaultIntensity;
            operation.FindPropertyRelative("state").boolValue = true;
            operation.FindPropertyRelative("loop").boolValue = false;
            operation.FindPropertyRelative("option").intValue = 0;
            operation.FindPropertyRelative("frequency").floatValue =
                descriptor.DefaultFrequency;

            switch (descriptor.Type)
            {
                case BattleExpressionClipType.ColorFlash:
                    operation.FindPropertyRelative("color").colorValue =
                        new Color(1f, 0.15f, 0.12f, 1f);
                    break;
                case BattleExpressionClipType.DarkenOthers:
                    operation.FindPropertyRelative("color").colorValue =
                        new Color(0.3f, 0.3f, 0.3f, 1f);
                    break;
                case BattleExpressionClipType.CameraShake:
                    operation.FindPropertyRelative("offset").vector3Value =
                        new Vector3(1f, 0.35f, 0f);
                    break;
                case BattleExpressionClipType.UnitShake:
                    operation.FindPropertyRelative("offset").vector3Value =
                        Vector3.right;
                    break;
            }
        }

        private void DrawLogicOutputPopup(SerializedProperty keyProperty)
        {
            TurnBasedSkillLogicAsset source = (TurnBasedSkillLogicAsset)
                serialized.FindProperty("logicSource").objectReferenceValue;
            TurnBasedSkillEditorUtility.BuildOutputOptions(source, keyProperty.stringValue, out string[] keys, out string[] labels);
            int selectedIndex = Math.Max(0, Array.IndexOf(keys, keyProperty.stringValue));
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup("读取逻辑结果", selectedIndex, labels);
            if (EditorGUI.EndChangeCheck()) keyProperty.stringValue = keys[Mathf.Clamp(next, 0, keys.Length - 1)];
            if (labels[selectedIndex].StartsWith("未找到绑定", StringComparison.Ordinal))
                EditorGUILayout.HelpBox("当前逻辑数据源没有这个标识。请重新选择数据源或绑定；原绑定不会自动清空。", MessageType.Warning);
            else if (source == null)
                EditorGUILayout.HelpBox("需要显示伤害、治疗等结果时，先在上方指定“逻辑数据源”。", MessageType.None);
        }

        private void BuildLists()
        {
            if (serialized == null)
            {
                trackList = null;
                operationList = null;
                return;
            }

            SerializedProperty tracks = serialized.FindProperty("tracks");
            trackList = new ReorderableList(serialized, tracks, true, true, true, true);
            trackList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "并行组 / 表现轨道");
            trackList.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                string name = track.FindPropertyRelative("trackName").stringValue;
                string group = track.FindPropertyRelative("parallelGroup").stringValue;
                int count = track.FindPropertyRelative("operations").arraySize;
                EditorGUI.LabelField(rect, $"{group} / {name}  ({count} 个帧操作)");
            };
            trackList.onSelectCallback = list =>
            {
                pendingTrack = list.index;
                pendingOperation = 0;
                Repaint();
            };
            trackList.onReorderCallback = list =>
            {
                pendingTrack = list.index;
                pendingOperation = selectedOperation;
            };
            trackList.onAddCallback = list =>
            {
                int index = tracks.arraySize;
                tracks.InsertArrayElementAtIndex(index);
                SerializedProperty track = tracks.GetArrayElementAtIndex(index);
                track.FindPropertyRelative("trackName").stringValue = $"表现轨 {index + 1}";
                track.FindPropertyRelative("parallelGroup").stringValue = "主并行组";
                track.FindPropertyRelative("executionMode").enumValueIndex = 0;
                track.FindPropertyRelative("operations").ClearArray();
                selectedTrack = index;
                selectedOperation = 0;
                list.index = index;
                BuildOperationList();
            };
            trackList.onRemoveCallback = list =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                selectedTrack = Mathf.Clamp(selectedTrack, 0, tracks.arraySize - 1);
                selectedOperation = 0;
                BuildOperationList();
            };
            trackList.index = selectedTrack;
            BuildOperationList();
        }

        private void BuildOperationList()
        {
            SerializedProperty tracks = serialized?.FindProperty("tracks");
            if (tracks == null || tracks.arraySize == 0)
            {
                operationList = null;
                return;
            }
            selectedTrack = Mathf.Clamp(selectedTrack, 0, tracks.arraySize - 1);
            SerializedProperty operations = tracks.GetArrayElementAtIndex(selectedTrack)
                .FindPropertyRelative("operations");
            selectedOperation = Mathf.Clamp(selectedOperation, 0, Math.Max(0, operations.arraySize - 1));
            operationList = new ReorderableList(serialized, operations, true, true, true, true);
            operationList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "帧操作列表（绝对帧）");
            operationList.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty operation = operations.GetArrayElementAtIndex(index);
                int type = operation.FindPropertyRelative("type").intValue;
                int start = operation.FindPropertyRelative("startFrame").intValue;
                int end = TurnBasedSkillEditorUtility.EndFrame(operation);
                string marker = TurnBasedExpressionOperationCatalog.GetDisplayName(
                    (BattleExpressionClipType)type);
                string note = operation.FindPropertyRelative("operationName").stringValue;
                string subject = operation.FindPropertyRelative("subject").intValue == 0 ? "施法者" : "主目标";
                EditorGUI.LabelField(rect, new GUIContent($"{index + 1}. [{start}–{end} 帧] {marker} · {subject}" +
                    (string.IsNullOrWhiteSpace(note) || note == marker ? "" : " · " + note), note));
            };
            operationList.onSelectCallback = list => selectedOperation = list.index;
            operationList.onReorderCallback = list => selectedOperation = list.index;
            operationList.onRemoveCallback = list =>
            {
                ReorderableList.defaultBehaviours.DoRemoveButton(list);
                selectedOperation = Math.Max(0, list.index);
            };
            operationList.onAddDropdownCallback = (rect, list) =>
            {
                var menu = new GenericMenu();
                TurnBasedSkillExpressionAsset owner = selected;
                int trackIndex = selectedTrack;
                foreach (TurnBasedExpressionOperationDescriptor item in TurnBasedExpressionOperationCatalog.All)
                {
                    TurnBasedExpressionOperationDescriptor descriptor = item;
                    menu.AddItem(new GUIContent(item.MenuPath, item.Description), false, () =>
                    {
                        if (selected != owner || selectedTrack != trackIndex) return;
                        AddOperation(descriptor);
                    });
                }
                menu.DropDown(rect);
            };
            operationList.index = selectedOperation;
        }

        private void AddOperation(TurnBasedExpressionOperationDescriptor descriptor)
        {
            serialized.Update();
            SerializedProperty operations = serialized.FindProperty("tracks").GetArrayElementAtIndex(selectedTrack).FindPropertyRelative("operations");
            int start = TurnBasedSkillEditorUtility.NextStartFrame(operations);
            int index = operations.arraySize;
            operations.InsertArrayElementAtIndex(index);
            SerializedProperty operation = operations.GetArrayElementAtIndex(index);
            operation.FindPropertyRelative("logicOutputKey").stringValue = string.Empty;
            operation.FindPropertyRelative("subject").intValue = (int)BattleExpressionSubject.Caster;
            ApplyOperationDescriptor(operation, descriptor, true);
            TurnBasedSkillEditorUtility.SetTiming(operation, start, descriptor.DefaultDurationFrames);
            serialized.ApplyModifiedProperties();
            selectedOperation = index;
            BuildOperationList();
            StopPreview(false);
            validationMessage = null;
            Repaint();
        }

        private int GetTimelineDuration()
        {
            if (serialized == null) return 1;
            if (!serialized.FindProperty("autoDuration").boolValue)
                return Math.Max(1, serialized.FindProperty("durationFrames").intValue);
            SerializedProperty tracks = serialized.FindProperty("tracks");
            int result = 1;
            for (int i = 0; i < tracks.arraySize; i++)
                result = Math.Max(result, TurnBasedSkillEditorUtility.NextStartFrame(tracks.GetArrayElementAtIndex(i).FindPropertyRelative("operations")));
            return result;
        }

        private void DrawTimeline()
        {
            SerializedProperty tracks = serialized.FindProperty("tracks");
            if (tracks == null || tracks.arraySize == 0)
            {
                return;
            }

            int duration = GetTimelineDuration();
            EditorGUILayout.LabelField("时间轴 · 点击色块编辑，点击刻度定位预览", EditorStyles.boldLabel);
            float height = 26f + tracks.arraySize * 28f;
            Rect full = GUILayoutUtility.GetRect(200f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(full, new Color(0.08f, 0.09f, 0.11f, 1f));
            Rect timeArea = new Rect(full.x + TrackLabelWidth, full.y, full.width - TrackLabelWidth, full.height);
            for (int tick = 0; tick <= 10; tick++)
            {
                int frame = (int)((long)duration * tick / 10);
                float x = timeArea.x + timeArea.width * frame / duration;
                EditorGUI.DrawRect(new Rect(x, full.y, 1f, full.height), new Color(0.22f, 0.24f, 0.28f));
                GUI.Label(new Rect(x + 2f, full.y, 45f, 20f), frame.ToString(), EditorStyles.miniLabel);
            }

            for (int trackIndex = 0; trackIndex < tracks.arraySize; trackIndex++)
            {
                SerializedProperty track = tracks.GetArrayElementAtIndex(trackIndex);
                float y = full.y + 24f + trackIndex * 28f;
                GUI.Label(new Rect(full.x + 4f, y, TrackLabelWidth - 8f, 22f),
                    track.FindPropertyRelative("trackName").stringValue,
                    EditorStyles.miniLabel);
                SerializedProperty operations = track.FindPropertyRelative("operations");
                for (int operationIndex = 0; operationIndex < operations.arraySize; operationIndex++)
                {
                    SerializedProperty operation = operations.GetArrayElementAtIndex(operationIndex);
                    int start = operation.FindPropertyRelative("startFrame").intValue;
                    int length = Mathf.Max(1, operation.FindPropertyRelative("durationFrames").intValue);
                    float x = timeArea.x + timeArea.width * start / duration;
                    float width = Mathf.Max(3f, timeArea.width * length / duration);
                    Color color = trackIndex == selectedTrack && operationIndex == selectedOperation
                        ? new Color(0.16f, 0.65f, 1f)
                        : new Color(0.24f, 0.42f, 0.62f);
                    Rect block = new Rect(Mathf.Clamp(x, timeArea.x, timeArea.xMax - 3f), y + 2f,
                        Mathf.Min(width, Math.Max(3f, timeArea.xMax - x)), 20f);
                    EditorGUI.DrawRect(block, color);
                    string marker = TurnBasedExpressionOperationCatalog.GetDisplayName((BattleExpressionClipType)operation.FindPropertyRelative("type").intValue);
                    string tip = marker + $" · {start}–{TurnBasedSkillEditorUtility.EndFrame(operation)} 帧";
                    GUI.Label(block, new GUIContent(block.width > 50 ? marker : string.Empty, tip), EditorStyles.whiteMiniLabel);
                    if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && block.Contains(Event.current.mousePosition))
                    {
                        pendingTrack = trackIndex;
                        pendingOperation = operationIndex;
                        previewFrame = Mathf.Clamp(start, 0, duration);
                        previewPlaying = false;
                        if (compiledPreview != null) TurnBasedFormationPreviewWindow.SetExpressionPreviewFrame(selected, previewFrame);
                        GUI.FocusControl(null);
                        Event.current.Use();
                        Repaint();
                    }
                }
            }

            Rect ruler = new Rect(timeArea.x, full.y, timeArea.width, 24f);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && ruler.Contains(Event.current.mousePosition))
            {
                previewFrame = Mathf.Clamp(Mathf.RoundToInt((Event.current.mousePosition.x - timeArea.x) / timeArea.width * duration), 0, duration);
                previewPlaying = false;
                if (compiledPreview != null) TurnBasedFormationPreviewWindow.SetExpressionPreviewFrame(selected, previewFrame);
                Event.current.Use();
                Repaint();
            }

            float playX = timeArea.x + timeArea.width * previewFrame / duration;
            EditorGUI.DrawRect(new Rect(playX, full.y, 2f, full.height), new Color(1f, 0.35f, 0.18f));
        }

        private void CreateAsset()
        {
            TurnBasedSkillExpressionAsset asset =
                TurnBasedAssetEditorUtility.CreateAsset<TurnBasedSkillExpressionAsset>(
                    TurnBasedSkillAssetGenerator.Root + "/Expression",
                    "skill_expression_new");
            if (asset != null)
            {
                asset.ResetToEmpty(
                    TurnBasedAssetEditorUtility.FileNameWithoutExtension(asset));
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                ReloadAssets(asset);
            }
        }

        private void ReloadAssets(TurnBasedSkillExpressionAsset preferred = null)
        {
            assets = TurnBasedAssetEditorUtility.FindAssets<TurnBasedSkillExpressionAsset>();
            Select(preferred != null ? preferred : selected != null ? selected :
                assets.Count > 0 ? assets[0] : null);
        }

        private void RemoveSelectedAsset()
        {
            if (selected == null)
            {
                return;
            }

            TurnBasedSkillExpressionAsset removing = selected;
            string label = string.IsNullOrWhiteSpace(removing.ExpressionId)
                ? removing.name
                : removing.ExpressionId;
            if (!TurnBasedAssetEditorUtility.RemoveAssetWithConfirmation(
                    removing,
                    "技能表现",
                    label,
                    () =>
                    {
                        previewPlaying = false;
                        compiledPreview = null;
                        previewFrame = 0;
                        TurnBasedFormationPreviewWindow.EndExpressionPreview(removing);
                    }))
            {
                return;
            }

            selected = null;
            serialized = null;
            trackList = null;
            operationList = null;
            ReloadAssets();
            ShowNotification(new GUIContent($"已移除 {label}"));
        }

        private void Select(TurnBasedSkillExpressionAsset asset)
        {
            if (selected != null && selected != asset)
            {
                TurnBasedFormationPreviewWindow.EndExpressionPreview(selected);
            }
            selected = asset;
            validationMessage = null;
            pendingTrack = pendingOperation = -1;
            serialized = selected == null ? null : new SerializedObject(selected);
            selectedTrack = 0;
            selectedOperation = 0;
            previewFrame = 0;
            previewPlaying = false;
            compiledPreview = null;
            BuildLists();
            Repaint();
        }

        private bool ShowPreviewAtCurrentFrame()
        {
            if (selected == null || serialized == null)
            {
                return false;
            }

            try
            {
                serialized.ApplyModifiedProperties();
                compiledPreview = selected.Compile();
                previewFrame = Mathf.Clamp(
                    previewFrame,
                    0,
                    compiledPreview.DurationFrames);
                TurnBasedFormationPreviewWindow.BeginExpressionPreview(
                    selected,
                    compiledPreview,
                    previewFrame);
                return true;
            }
            catch (Exception exception)
            {
                previewPlaying = false;
                compiledPreview = null;
                Debug.LogException(exception, selected);
                validationMessage = "无法预览：" + exception.Message;
                validationType = MessageType.Error;
                ShowNotification(new GUIContent("请按窗口内的提示修正配置"));
                return false;
            }
        }

        private void StopPreview(bool resetFrame)
        {
            previewPlaying = false;
            compiledPreview = null;
            if (resetFrame)
            {
                previewFrame = 0;
            }
            TurnBasedFormationPreviewWindow.EndExpressionPreview(selected);
            Repaint();
        }

        private void Save()
        {
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("表现资产已保存"));
        }

        private void ValidateAsset()
        {
            try
            {
                serialized.ApplyModifiedProperties();
                selected.Compile();
                Save();
                validationMessage = "配置检查通过，已保存。";
                validationType = MessageType.Info;
                ShowNotification(new GUIContent("表现校验通过"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, selected);
                validationMessage = "配置检查未通过：" + exception.Message;
                validationType = MessageType.Error;
                ShowNotification(new GUIContent("请按窗口内的提示修正配置"));
            }
        }

        private void OnUndoRedo()
        {
            if (selected == null) return;
            StopPreview(false);
            serialized = new SerializedObject(selected);
            BuildLists();
            validationMessage = null;
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (!previewPlaying || selected == null)
            {
                return;
            }
            double now = EditorApplication.timeSinceStartup;
            int advance = Mathf.FloorToInt(
                (float)((now - lastEditorTime) * selected.FramesPerSecond));
            if (advance <= 0)
            {
                return;
            }
            lastEditorTime += advance / (double)selected.FramesPerSecond;
            previewFrame += advance;
            if (previewFrame > selected.DurationFrames)
            {
                previewFrame = 0;
            }
            TurnBasedFormationPreviewWindow.SetExpressionPreviewFrame(
                selected,
                previewFrame);
            Repaint();
        }
    }
}
