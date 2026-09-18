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
            new GUIContent("攻击方", "当前释放技能的角色"),
            new GUIContent("受击方", "技能逻辑选中的主目标"),
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
            ReloadAssets();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            TurnBasedFormationPreviewWindow.EndExpressionPreview(selected);
        }

        private void OnGUI()
        {
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
                search = EditorGUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField"));
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
                for (int index = 0; index < assets.Count; index++)
                {
                    TurnBasedSkillExpressionAsset asset = assets[index];
                    string label = string.IsNullOrWhiteSpace(asset.ExpressionId)
                        ? asset.name
                        : asset.ExpressionId;
                    if (!string.IsNullOrWhiteSpace(search) &&
                        label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    if (GUILayout.Button(
                            label,
                            TurnBasedAssetEditorUtility.SelectedListStyle(asset == selected),
                            GUILayout.Height(24f)))
                    {
                        Select(asset);
                    }
                }
                EditorGUILayout.EndScrollView();
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
                EditorGUILayout.PropertyField(serialized.FindProperty("expressionId"), new GUIContent("表现名称"));
                EditorGUILayout.PropertyField(serialized.FindProperty("logicSource"), new GUIContent("逻辑数据源"));
                EditorGUILayout.PropertyField(serialized.FindProperty("framesPerSecond"), new GUIContent("每秒帧数"));
                EditorGUILayout.PropertyField(serialized.FindProperty("autoDuration"), new GUIContent("自动计算总帧数"));
                using (new EditorGUI.DisabledScope(serialized.FindProperty("autoDuration").boolValue))
                {
                    EditorGUILayout.PropertyField(serialized.FindProperty("durationFrames"), new GUIContent("总帧数"));
                }
                EditorGUILayout.PropertyField(serialized.FindProperty("description"), new GUIContent("说明"));

                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox(
                    "点击播放会打开并驱动阵容模型预览，默认使用攻击方第一个有模型的单位" +
                    "作为施法者、防守方第一个有模型的单位作为目标。\n" +
                    "同名 Parallel Group 下的多条轨道会并行播放。帧操作保存绝对起止帧，" +
                    "不需要手工添加 Delay；组时长由最后一个操作自动计算。\n" +
                    "Lua 参考中的 AddParallelTask 由并行轨表达，beginMark/endMark 由播放器" +
                    "生命周期自动处理，伤害与死亡只消费逻辑事件结果。",
                    MessageType.Info);
                trackList?.DoLayoutList();
                DrawSelectedTrackAndOperations();
                DrawTimeline();
                EditorGUILayout.EndScrollView();

                if (serialized.ApplyModifiedProperties())
                {
                    EditorUtility.SetDirty(selected);
                    previewFrame = Mathf.Min(previewFrame, selected.DurationFrames);
                    previewPlaying = false;
                    compiledPreview = null;
                    TurnBasedFormationPreviewWindow.EndExpressionPreview(selected);
                    Repaint();
                }
            }
        }

        private bool DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(selected.ExpressionId, EditorStyles.boldLabel);
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
                if (GUILayout.Button("校验编译", EditorStyles.toolbarButton))
                {
                    ValidateAsset();
                }
            }

            int duration = Mathf.Max(1, selected.DurationFrames);
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
            return false;
        }

        private void DrawSelectedTrackAndOperations()
        {
            SerializedProperty tracks = serialized.FindProperty("tracks");
            if (tracks == null || tracks.arraySize == 0)
            {
                operationList = null;
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
                new GUIContent("操作标记", "选择标准表现操作，不需要手工输入名称。"),
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
            if (string.IsNullOrWhiteSpace(operationName.stringValue))
            {
                operationName.stringValue = descriptor.DisplayName;
            }
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
            int startFrame = Mathf.Max(0, EditorGUILayout.IntField("开始帧", start.intValue));
            int endFrame = Mathf.Max(startFrame, EditorGUILayout.IntField(
                "结束帧",
                start.intValue + duration.intValue));
            start.intValue = startFrame;
            duration.intValue = endFrame - startFrame;

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
                    DrawText(operation, "secondaryResourceKey", "受击特效 Key（可选）");
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
                    DrawText(operation, "resourceKey", "模型资源 Key");
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
                    DrawText(operation, "resourceKey", "弹道特效 Key");
                    DrawText(operation, "anchorKey", "起点挂点（可选）");
                    DrawText(operation, "secondaryResourceKey", "终点挂点（可选）");
                    DrawVector(operation, "offset", "起点偏移");
                    DrawVector(operation, "targetOffset", "终点偏移");
                    DrawDynamicTarget(operation, "演示终点位置");
                    break;
                case BattleExpressionClipType.EffectAnimation:
                    DrawText(operation, "resourceKey", "已有特效 Key");
                    DrawText(operation, "secondaryResourceKey", "动画或 Trigger 名称");
                    break;
                case BattleExpressionClipType.RemoveEffect:
                    DrawText(operation, "resourceKey", "待移除特效 Key");
                    break;
                case BattleExpressionClipType.ToggleLoopEffect:
                    DrawText(operation, "resourceKey", "常驻特效 Key");
                    DrawText(operation, "anchorKey", "挂点（可选）");
                    DrawVector(operation, "offset", "挂点偏移");
                    DrawBool(operation, "state", "开启");
                    break;
                case BattleExpressionClipType.Audio:
                case BattleExpressionClipType.BackgroundAudio:
                    DrawText(operation, "resourceKey", operationType ==
                        BattleExpressionClipType.Audio ? "音效资源 Key" : "背景音乐资源 Key");
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
                        BattleExpressionClipType.BuffText ? "Buff 文本或样式 Key" :
                        operationType == BattleExpressionClipType.FloatingTip ?
                            "提示文本或配置 Key" : "气泡文本或配置 Key");
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
            DrawText(operation, "resourceKey", "特效资源 Key");
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
            var labels = new List<string> { "<不绑定逻辑数据>" };
            var keys = new List<string> { string.Empty };
            TurnBasedSkillLogicAsset source = (TurnBasedSkillLogicAsset)
                serialized.FindProperty("logicSource").objectReferenceValue;
            if (source != null)
            {
                for (int index = 0; index < source.Outputs.Count; index++)
                {
                    BattleLogicOutputAuthoring output = source.Outputs[index];
                    if (output == null || string.IsNullOrWhiteSpace(output.key))
                    {
                        continue;
                    }
                    keys.Add(output.key);
                    labels.Add($"{output.key}  ({output.eventType})");
                }
            }

            int selectedIndex = Mathf.Max(0, keys.IndexOf(keyProperty.stringValue));
            int next = EditorGUILayout.Popup("逻辑数据", selectedIndex, labels.ToArray());
            keyProperty.stringValue = keys[Mathf.Clamp(next, 0, keys.Count - 1)];
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
                selectedTrack = list.index;
                selectedOperation = 0;
                BuildOperationList();
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
            operationList = new ReorderableList(serialized, operations, true, true, true, true);
            operationList.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "帧操作列表（绝对帧）");
            operationList.drawElementCallback = (rect, index, active, focused) =>
            {
                SerializedProperty operation = operations.GetArrayElementAtIndex(index);
                int type = operation.FindPropertyRelative("type").intValue;
                int start = operation.FindPropertyRelative("startFrame").intValue;
                int duration = operation.FindPropertyRelative("durationFrames").intValue;
                string marker = TurnBasedExpressionOperationCatalog.GetDisplayName(
                    (BattleExpressionClipType)type);
                EditorGUI.LabelField(rect, $"[{start}-{start + duration}] {marker}");
            };
            operationList.onSelectCallback = list => selectedOperation = list.index;
            operationList.onAddCallback = list =>
            {
                int index = operations.arraySize;
                operations.InsertArrayElementAtIndex(index);
                SerializedProperty operation = operations.GetArrayElementAtIndex(index);
                operation.FindPropertyRelative("logicOutputKey").stringValue = string.Empty;
                operation.FindPropertyRelative("subject").enumValueIndex = 0;
                operation.FindPropertyRelative("startFrame").intValue = index == 0
                    ? 0
                    : operations.GetArrayElementAtIndex(index - 1)
                        .FindPropertyRelative("startFrame").intValue +
                      operations.GetArrayElementAtIndex(index - 1)
                        .FindPropertyRelative("durationFrames").intValue;
                ApplyOperationDescriptor(
                    operation,
                    TurnBasedExpressionOperationCatalog.Get(
                        BattleExpressionClipType.Animation),
                    true);
                selectedOperation = index;
                list.index = index;
            };
        }

        private void DrawTimeline()
        {
            SerializedProperty tracks = serialized.FindProperty("tracks");
            if (tracks == null || tracks.arraySize == 0)
            {
                return;
            }

            int duration = Mathf.Max(1, selected.DurationFrames);
            float height = 26f + tracks.arraySize * 28f;
            Rect full = GUILayoutUtility.GetRect(200f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(full, new Color(0.08f, 0.09f, 0.11f, 1f));
            Rect timeArea = new Rect(full.x + TrackLabelWidth, full.y, full.width - TrackLabelWidth, full.height);
            for (int frame = 0; frame <= duration; frame += Mathf.Max(1, duration / 10))
            {
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
                    EditorGUI.DrawRect(new Rect(x, y + 2f, width, 18f), color);
                }
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
                ShowNotification(new GUIContent("无法播放，请查看 Console"));
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
                ShowNotification(new GUIContent("表现校验通过"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, selected);
                ShowNotification(new GUIContent("校验失败，请查看 Console"));
            }
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
