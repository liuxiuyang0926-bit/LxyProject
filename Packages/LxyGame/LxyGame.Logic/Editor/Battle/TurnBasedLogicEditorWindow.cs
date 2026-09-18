using System;
using System.Collections.Generic;
using Game.Battle.TurnBased.Authoring;
using Game.Battle.TurnBased.Domain;
using Game.Battle.TurnBased.RuntimeData;
using Sirenix.OdinInspector.Editor;
using Sirenix.Utilities.Editor;
using UnityEditor;
using UnityEngine;

namespace Game.Battle.Editor
{
    public sealed class TurnBasedLogicEditorWindow : EditorWindow
    {
        private const float BrowserWidth = 250f;

        private enum ListEditCommand
        {
            None,
            Add,
            Remove,
            MoveUp,
            MoveDown,
        }

        private List<TurnBasedSkillLogicAsset> assets =
            new List<TurnBasedSkillLogicAsset>();
        private TurnBasedSkillLogicAsset selected;
        private TurnBasedSkillLogicAsset pendingSelection;
        private SerializedObject serialized;
        private PropertyTree propertyTree;
        private Vector2 assetScroll;
        private Vector2 detailScroll;
        private string search = string.Empty;
        private int selectedTrackIndex;
        private int selectedRuleIndex;
        private int selectedConditionIndex;
        private int selectedActionIndex;
        private bool showTrackSettings;
        private string[] trackOptions = Array.Empty<string>();
        private string[] ruleOptions = Array.Empty<string>();
        private string[] conditionOptions = Array.Empty<string>();
        private string[] actionOptions = Array.Empty<string>();

        private static readonly CompiledConditionType[] ConditionTypeValues =
            (CompiledConditionType[])Enum.GetValues(typeof(CompiledConditionType));
        private static readonly string[] ConditionTypeLabels =
            BuildLabels(ConditionTypeValues, GetConditionTypeLabel);
        private static readonly CompiledActionType[] ActionTypeValues =
            (CompiledActionType[])Enum.GetValues(typeof(CompiledActionType));
        private static readonly string[] ActionTypeLabels =
            BuildLabels(ActionTypeValues, GetActionTypeLabel);
        private static readonly TargetSelectorType[] TargetTypeValues =
            (TargetSelectorType[])Enum.GetValues(typeof(TargetSelectorType));
        private static readonly string[] TargetTypeLabels =
        {
            "规则拥有者",
            "事件攻击者",
            "事件目标",
            "技能选中目标",
            "全部友军",
            "全部敌人",
            "随机敌人",
            "生命最低敌人",
            "攻击最高敌人",
            "前排敌人",
        };
        private static readonly ValueSourceType[] ValueSourceValues =
            (ValueSourceType[])Enum.GetValues(typeof(ValueSourceType));
        private static readonly string[] ValueSourceLabels =
        {
            "固定值",
            "拥有者属性",
            "事件攻击者属性",
            "当前目标属性",
            "当前生命",
            "最大生命",
            "已损失生命",
            "生命百分比",
            "Buff 层数",
            "事件数值",
            "当前回合",
            "战斗计数器",
            "策划参数",
            "单次触发临时变量",
            "技能实例变量",
            "确定性随机值（0~9999）",
        };
        private static readonly AttributeType[] AttributeTypeValues =
            (AttributeType[])Enum.GetValues(typeof(AttributeType));
        private static readonly string[] AttributeTypeLabels =
        {
            "生命",
            "最大生命",
            "攻击",
            "防御",
            "速度",
            "暴击率",
            "暴击伤害",
            "命中率",
            "闪避率",
            "怒气",
            "能量",
            "护盾",
            "Count（内部保留）",
        };
        private static readonly ComparisonOperator[] ComparisonValues =
            (ComparisonOperator[])Enum.GetValues(typeof(ComparisonOperator));
        private static readonly string[] ComparisonLabels =
        {
            "等于",
            "不等于",
            "小于",
            "小于等于",
            "大于",
            "大于等于",
        };
        private static readonly BattleAuthoringExecutionMode[] ExecutionModeValues =
            (BattleAuthoringExecutionMode[])Enum.GetValues(
                typeof(BattleAuthoringExecutionMode));
        private static readonly string[] ExecutionModeLabels = { "顺序", "并行" };
        private static readonly string[] ActionFlagLabels =
        {
            "可以闪避",
            "可以暴击",
            "无视防御",
        };

        [MenuItem("工具/战斗/技能逻辑编辑器", false, 0)]
        public static void Open()
        {
            Open(null);
        }

        internal static void Open(TurnBasedSkillLogicAsset asset)
        {
            var window = GetWindow<TurnBasedLogicEditorWindow>();
            window.titleContent = new GUIContent("技能逻辑编辑器");
            window.minSize = new Vector2(860f, 600f);
            window.ReloadAssets(asset);
            window.Show();
        }

        private void OnEnable()
        {
            ReloadAssets();
        }

        private void OnDisable()
        {
            pendingSelection = null;
            DisposePropertyTree();
        }

        private void OnGUI()
        {
            // A selection made by GUILayout.Button arrives during an input
            // event. Swapping the Odin tree there means the following Repaint
            // consumes layout data produced for the previous asset. Apply the
            // queued selection at the beginning of Layout so Layout/Repaint
            // always render the same asset and the same conditional fields.
            if (Event.current.type == EventType.Layout)
            {
                ApplyPendingSelection();
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
                EditorGUILayout.LabelField("逻辑资产", EditorStyles.boldLabel);
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
                    TurnBasedSkillLogicAsset asset = assets[index];
                    string label = string.IsNullOrWhiteSpace(asset.LogicId)
                        ? asset.name
                        : asset.LogicId;
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
                        QueueSelection(asset);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void QueueSelection(TurnBasedSkillLogicAsset asset)
        {
            if (asset == null || asset == selected)
            {
                return;
            }

            pendingSelection = asset;
            Repaint();
        }

        private void ApplyPendingSelection()
        {
            if (pendingSelection == null)
            {
                return;
            }

            TurnBasedSkillLogicAsset next = pendingSelection;
            pendingSelection = null;
            if (next != selected)
            {
                Select(next);
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
                    EditorGUILayout.HelpBox("请从左侧选择或新建一个逻辑资产。", MessageType.Info);
                    return;
                }

                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label(selected.LogicId, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("定位", EditorStyles.toolbarButton))
                    {
                        Selection.activeObject = selected;
                        EditorGUIUtility.PingObject(selected);
                    }
                    if (GUILayout.Button(
                            new GUIContent("移除", "将当前技能逻辑 ScriptableObject 移到系统回收站"),
                            EditorStyles.toolbarButton))
                    {
                        RemoveSelectedAsset();
                        return;
                    }
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton))
                    {
                        Save();
                    }
                    if (GUILayout.Button(
                            new GUIContent("数据模板", "补充常用策划参数、临时变量和技能实例变量"),
                            EditorStyles.toolbarButton))
                    {
                        propertyTree?.ApplyChanges();
                        serialized?.ApplyModifiedProperties();
                        TurnBasedSkillLogicAsset editingAsset = selected;
                        TurnBasedLogicDataAuthoringUtility.ShowTemplateMenu(
                            editingAsset,
                            () => Select(editingAsset));
                    }
                    if (GUILayout.Button(
                            new GUIContent("生成 C#", "生成稳定的数据 Key 与规则 ID 常量"),
                            EditorStyles.toolbarButton))
                    {
                        GenerateCode();
                    }
                    if (GUILayout.Button("校验编译", EditorStyles.toolbarButton))
                    {
                        ValidateAsset();
                    }
                }

                if (selected.UsesLegacyLayout)
                {
                    EditorGUILayout.HelpBox(
                        "该资产仍是旧版平铺规则；迁移后才能按逻辑轨和并行组编辑。",
                        MessageType.Warning);
                    if (GUILayout.Button("迁移为主逻辑轨"))
                    {
                        Undo.RecordObject(selected, "迁移逻辑轨道");
                        selected.UpgradeLegacyLayout();
                        EditorUtility.SetDirty(selected);
                        Select(selected);
                    }

                    return;
                }

                detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
                SirenixEditorGUI.InfoMessageBox(
                    "Odin 增强编辑已启用：无关参数会按条件与操作类型自动隐藏。" +
                    "同一执行组的 Parallel 轨道表示并行语义，战斗核心仍按" +
                    "阶段、优先级、单位和规则 ID 的固定顺序结算。",
                    true);

                ListEditCommand trackCommand = ListEditCommand.None;
                ListEditCommand ruleCommand = ListEditCommand.None;
                ListEditCommand conditionCommand = ListEditCommand.None;
                ListEditCommand actionCommand = ListEditCommand.None;
                if (propertyTree != null)
                {
                    InspectorUtilities.BeginDrawPropertyTree(propertyTree, true);
                    try
                    {
                        DrawRootProperty("#基础信息");

                        trackCommand = DrawTrackNavigator();
                        if (trackCommand == ListEditCommand.None &&
                            GetSelectedTrack() != null)
                        {
                            ruleCommand = DrawRuleNavigator();
                            if (ruleCommand == ListEditCommand.None)
                            {
                                DrawSelectedRule(
                                    out conditionCommand,
                                    out actionCommand);
                            }
                            DrawSelectedTrackSettings();
                        }

                        DrawRootProperty("#表现数据契约");
                        DrawRootProperty("#临时数据与参数");
                    }
                    finally
                    {
                        InspectorUtilities.EndDrawPropertyTree(propertyTree);
                    }
                }
                EditorGUILayout.EndScrollView();

                if (trackCommand != ListEditCommand.None)
                {
                    ApplyTrackCommand(trackCommand);
                }
                else if (ruleCommand != ListEditCommand.None)
                {
                    ApplyRuleCommand(ruleCommand);
                }
                else if (conditionCommand != ListEditCommand.None)
                {
                    ApplyConditionCommand(conditionCommand);
                }
                else if (actionCommand != ListEditCommand.None)
                {
                    ApplyActionCommand(actionCommand);
                }
            }
        }

        private void DrawRootProperty(string path)
        {
            InspectorProperty property = propertyTree.GetPropertyAtPath(path);
            property?.Draw();
        }

        private ListEditCommand DrawTrackNavigator()
        {
            NormalizeSelection();
            EnsureTrackOptions();

            ListEditCommand command = ListEditCommand.None;
            SirenixEditorGUI.BeginBox("逻辑轨道", false);
            try
            {
                int trackCount = selected.Tracks?.Count ?? 0;
                if (trackCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "当前逻辑还没有轨道，请先添加一条逻辑轨道。",
                        MessageType.Info);
                    if (GUILayout.Button("添加第一条逻辑轨道"))
                    {
                        command = ListEditCommand.Add;
                    }
                    return command;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("当前轨道", GUILayout.Width(64f));
                    int nextIndex = EditorGUILayout.Popup(
                        selectedTrackIndex,
                        trackOptions);
                    if (nextIndex != selectedTrackIndex)
                    {
                        selectedTrackIndex = nextIndex;
                        selectedRuleIndex = 0;
                        selectedConditionIndex = 0;
                        selectedActionIndex = 0;
                        ruleOptions = Array.Empty<string>();
                        conditionOptions = Array.Empty<string>();
                        actionOptions = Array.Empty<string>();
                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("新增", EditorStyles.miniButton, GUILayout.Width(44f)))
                    {
                        command = ListEditCommand.Add;
                    }
                    using (new EditorGUI.DisabledScope(selectedTrackIndex <= 0))
                    {
                        if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(28f)))
                        {
                            command = ListEditCommand.MoveUp;
                        }
                    }
                    using (new EditorGUI.DisabledScope(
                               selectedTrackIndex >= trackCount - 1))
                    {
                        if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(28f)))
                        {
                            command = ListEditCommand.MoveDown;
                        }
                    }
                    if (GUILayout.Button("移除", EditorStyles.miniButtonRight, GUILayout.Width(44f)))
                    {
                        command = ListEditCommand.Remove;
                    }
                }
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }

            return command;
        }

        private void DrawSelectedTrackSettings()
        {
            InspectorProperty trackProperty = propertyTree.GetPropertyAtUnityPath(
                $"tracks.Array.data[{selectedTrackIndex}]");
            if (trackProperty == null)
            {
                EditorGUILayout.HelpBox("无法读取当前轨道属性。", MessageType.Error);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                showTrackSettings = EditorGUILayout.Foldout(
                    showTrackSettings,
                    "轨道高级设置",
                    true,
                    EditorStyles.foldoutHeader);
                if (showTrackSettings)
                {
                    for (int index = 0; index < trackProperty.Children.Count; index++)
                    {
                        InspectorProperty child = trackProperty.Children[index];
                        if (child.Name != "rules")
                        {
                            child.Draw();
                        }
                    }
                }
            }
        }

        private ListEditCommand DrawRuleNavigator()
        {
            NormalizeSelection();
            EnsureRuleOptions();

            ListEditCommand command = ListEditCommand.None;
            SirenixEditorGUI.BeginBox("轨道规则", false);
            try
            {
                BattleLogicTrackAuthoring track = GetSelectedTrack();
                int ruleCount = track?.rules?.Count ?? 0;
                if (ruleCount == 0)
                {
                    EditorGUILayout.HelpBox(
                        "当前轨道还没有规则。新增规则会自动创建一条可编辑的" +
                        "攻击力伤害操作。",
                        MessageType.Info);
                    if (GUILayout.Button("添加第一条规则"))
                    {
                        command = ListEditCommand.Add;
                    }
                    return command;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("当前规则", GUILayout.Width(64f));
                    int nextIndex = EditorGUILayout.Popup(
                        selectedRuleIndex,
                        ruleOptions);
                    if (nextIndex != selectedRuleIndex)
                    {
                        selectedRuleIndex = nextIndex;
                        selectedConditionIndex = 0;
                        selectedActionIndex = 0;
                        conditionOptions = Array.Empty<string>();
                        actionOptions = Array.Empty<string>();
                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("新增", EditorStyles.miniButton, GUILayout.Width(44f)))
                    {
                        command = ListEditCommand.Add;
                    }
                    using (new EditorGUI.DisabledScope(selectedRuleIndex <= 0))
                    {
                        if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(28f)))
                        {
                            command = ListEditCommand.MoveUp;
                        }
                    }
                    using (new EditorGUI.DisabledScope(
                               selectedRuleIndex >= ruleCount - 1))
                    {
                        if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(28f)))
                        {
                            command = ListEditCommand.MoveDown;
                        }
                    }
                    if (GUILayout.Button("移除", EditorStyles.miniButtonRight, GUILayout.Width(44f)))
                    {
                        command = ListEditCommand.Remove;
                    }
                }
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }

            return command;
        }

        private void DrawSelectedRule(
            out ListEditCommand conditionCommand,
            out ListEditCommand actionCommand)
        {
            conditionCommand = ListEditCommand.None;
            actionCommand = ListEditCommand.None;
            BattleLogicTrackAuthoring track = GetSelectedTrack();
            if (track?.rules == null || selectedRuleIndex < 0 ||
                selectedRuleIndex >= track.rules.Count)
            {
                return;
            }

            InspectorProperty ruleProperty = propertyTree.GetPropertyAtUnityPath(
                $"tracks.Array.data[{selectedTrackIndex}].rules.Array.data[{selectedRuleIndex}]");
            if (ruleProperty == null)
            {
                EditorGUILayout.HelpBox("无法读取当前规则属性。", MessageType.Error);
                return;
            }

            SirenixEditorGUI.BeginBox("触发设置", false);
            try
            {
                DrawRuleChild(ruleProperty, "trigger");
                DrawRuleChild(ruleProperty, "phase");
                DrawRuleChild(ruleProperty, "priority");
                DrawRuleChild(ruleProperty, "ownerMustBeAlive");
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }

            SirenixEditorGUI.BeginBox("规则内容", false);
            try
            {
                DrawRuleChild(ruleProperty, "operationName");
                DrawRuleChild(ruleProperty, "executionGroup");
                DrawRuleChild(ruleProperty, "executionMode");
                DrawRuleChild(ruleProperty, "id");
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }

            conditionCommand = DrawConditionNavigator();
            if (conditionCommand == ListEditCommand.None)
            {
                DrawSelectedCondition();
            }

            actionCommand = DrawActionNavigator();
            if (actionCommand == ListEditCommand.None)
            {
                DrawSelectedAction();
            }
        }

        private ListEditCommand DrawConditionNavigator()
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            EnsureConditionOptions();
            return DrawNestedListNavigator(
                "触发条件（全部满足）",
                "当前条件",
                rule?.conditions?.Count ?? 0,
                conditionOptions,
                ref selectedConditionIndex,
                "当前规则没有附加条件，将直接响应触发事件。");
        }

        private void DrawSelectedCondition()
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            if (rule?.conditions == null || selectedConditionIndex < 0 ||
                selectedConditionIndex >= rule.conditions.Count)
            {
                return;
            }

            BattleConditionAuthoring condition =
                rule.conditions[selectedConditionIndex];
            if (condition == null)
            {
                EditorGUILayout.HelpBox(
                    "当前条件数据为空，请移除后重新添加。",
                    MessageType.Error);
                return;
            }

            SirenixEditorGUI.BeginBox("条件内容", false);
            try
            {
                DrawConditionFields(condition);
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }
        }

        private ListEditCommand DrawActionNavigator()
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            EnsureActionOptions();
            return DrawNestedListNavigator(
                "逻辑操作（按列表顺序）",
                "当前操作",
                rule?.actions?.Count ?? 0,
                actionOptions,
                ref selectedActionIndex,
                "当前规则还没有逻辑操作。");
        }

        private void DrawSelectedAction()
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            if (rule?.actions == null || selectedActionIndex < 0 ||
                selectedActionIndex >= rule.actions.Count)
            {
                return;
            }

            BattleActionAuthoring action = rule.actions[selectedActionIndex];
            if (action == null)
            {
                EditorGUILayout.HelpBox(
                    "当前操作数据为空，请移除后重新添加。",
                    MessageType.Error);
                return;
            }

            SirenixEditorGUI.BeginBox("操作内容", false);
            try
            {
                DrawActionFields(action);
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }
        }

        private static ListEditCommand DrawNestedListNavigator(
            string title,
            string selectorLabel,
            int count,
            string[] options,
            ref int selectedIndex,
            string emptyMessage)
        {
            ListEditCommand command = ListEditCommand.None;
            SirenixEditorGUI.BeginBox(title, false);
            try
            {
                if (count == 0)
                {
                    EditorGUILayout.HelpBox(emptyMessage, MessageType.Info);
                    if (GUILayout.Button("新增"))
                    {
                        command = ListEditCommand.Add;
                    }
                    return command;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(selectorLabel, GUILayout.Width(64f));
                    int nextIndex = EditorGUILayout.Popup(selectedIndex, options);
                    if (nextIndex != selectedIndex)
                    {
                        selectedIndex = nextIndex;
                        GUI.FocusControl(null);
                    }

                    if (GUILayout.Button("新增", EditorStyles.miniButton, GUILayout.Width(44f)))
                    {
                        command = ListEditCommand.Add;
                    }
                    using (new EditorGUI.DisabledScope(selectedIndex <= 0))
                    {
                        if (GUILayout.Button("↑", EditorStyles.miniButtonLeft, GUILayout.Width(28f)))
                        {
                            command = ListEditCommand.MoveUp;
                        }
                    }
                    using (new EditorGUI.DisabledScope(selectedIndex >= count - 1))
                    {
                        if (GUILayout.Button("↓", EditorStyles.miniButtonMid, GUILayout.Width(28f)))
                        {
                            command = ListEditCommand.MoveDown;
                        }
                    }
                    if (GUILayout.Button("移除", EditorStyles.miniButtonRight, GUILayout.Width(44f)))
                    {
                        command = ListEditCommand.Remove;
                    }
                }
            }
            finally
            {
                SirenixEditorGUI.EndBox();
            }

            return command;
        }

        private void DrawConditionFields(BattleConditionAuthoring condition)
        {
            // Use the value captured at the start of this IMGUI event to
            // decide which controls are visible. If a popup changes during a
            // mouse/key event, the new shape starts on the next Layout event.
            CompiledConditionType displayType = condition.type;
            CompiledConditionType nextType = DrawLocalizedEnum(
                "条件类型",
                displayType,
                ConditionTypeValues,
                ConditionTypeLabels);
            if (SetManualValue(
                    ref condition.type,
                    nextType,
                    "修改触发条件类型"))
            {
                conditionOptions = Array.Empty<string>();
            }

            if (ConditionUsesTarget(displayType))
            {
                DrawTargetFields(condition.target, "条件目标", "修改触发条件目标");
            }
            if (ConditionUsesComparison(displayType))
            {
                SetManualValue(
                    ref condition.comparison,
                    DrawLocalizedEnum(
                        "比较方式",
                        condition.comparison,
                        ComparisonValues,
                        ComparisonLabels),
                    "修改触发条件比较方式");
            }
            if (ConditionUsesValue(displayType))
            {
                SetManualValue(
                    ref condition.value,
                    EditorGUILayout.LongField("比较值", condition.value),
                    "修改触发条件比较值");
            }
            if (displayType == CompiledConditionType.AttributeCompare)
            {
                SetManualValue(
                    ref condition.attribute,
                    DrawLocalizedEnum(
                        "比较属性",
                        condition.attribute,
                        AttributeTypeValues,
                        AttributeTypeLabels),
                    "修改触发条件属性");
            }
            if (ConditionUsesReferenceId(displayType))
            {
                SetManualValue(
                    ref condition.referenceId,
                    Mathf.Max(0, EditorGUILayout.IntField(
                        new GUIContent(
                            "引用 ID",
                            "拥有 Buff 填 Buff ID；计数器条件填变量 ID。"),
                        condition.referenceId)),
                    "修改触发条件引用 ID");
            }
            if (displayType == CompiledConditionType.LogicValueCompare)
            {
                DrawValueFields(
                    condition.leftValue,
                    "左侧数值",
                    "修改触发条件左侧数值");
                DrawValueFields(
                    condition.rightValue,
                    "右侧数值",
                    "修改触发条件右侧数值");
            }
            if (displayType != CompiledConditionType.Always)
            {
                SetManualValue(
                    ref condition.negate,
                    EditorGUILayout.Toggle("条件取反", condition.negate),
                    "修改触发条件取反");
            }
        }

        private void DrawActionFields(BattleActionAuthoring action)
        {
            SetManualValue(
                ref action.operationName,
                EditorGUILayout.TextField("操作名称", action.operationName),
                "修改逻辑操作名称",
                true);
            SetManualValue(
                ref action.executionGroup,
                EditorGUILayout.TextField("执行组", action.executionGroup),
                "修改逻辑操作执行组");
            SetManualValue(
                ref action.executionMode,
                DrawLocalizedEnum(
                    "执行方式",
                    action.executionMode,
                    ExecutionModeValues,
                    ExecutionModeLabels),
                "修改逻辑操作执行方式");

            CompiledActionType displayType = action.type;
            CompiledActionType nextType = DrawLocalizedEnum(
                "操作类型",
                displayType,
                ActionTypeValues,
                ActionTypeLabels);
            SetManualValue(
                ref action.type,
                nextType,
                "修改逻辑操作类型",
                true);

            if (ActionUsesTarget(displayType))
            {
                DrawTargetFields(action.target, "操作目标", "修改逻辑操作目标");
            }
            if (ActionUsesValue(displayType))
            {
                DrawValueFields(action.value, "数值表达式", "修改逻辑操作数值");
            }
            if (ActionUsesAttribute(displayType))
            {
                SetManualValue(
                    ref action.attribute,
                    DrawLocalizedEnum(
                        "修改属性",
                        action.attribute,
                        AttributeTypeValues,
                        AttributeTypeLabels),
                    "修改逻辑操作属性");
            }

            bool supportsDynamicReference =
                displayType == CompiledActionType.AddBuff ||
                displayType == CompiledActionType.RemoveBuff;
            bool displayDynamicReference = action.useDynamicReference;
            if (supportsDynamicReference)
            {
                SetManualValue(
                    ref action.useDynamicReference,
                    EditorGUILayout.Toggle(
                        new GUIContent(
                            "动态引用 ID",
                            "开启后从策划参数或变量读取 Buff ID。"),
                        displayDynamicReference),
                    "修改逻辑操作引用模式");
            }

            if (supportsDynamicReference && displayDynamicReference)
            {
                DrawValueFields(
                    action.referenceValue,
                    "引用 ID 数值",
                    "修改逻辑操作动态引用 ID");
            }
            else if (ActionUsesReferenceId(displayType))
            {
                SetManualValue(
                    ref action.referenceId,
                    Mathf.Max(0, EditorGUILayout.IntField(
                        new GUIContent(
                            "引用 ID",
                            "添加/移除 Buff 填 Buff ID；计数器和变量操作填变量 ID。"),
                        action.referenceId)),
                    "修改逻辑操作引用 ID");
            }

            if (displayType == CompiledActionType.Damage)
            {
                SetManualValue(
                    ref action.flags,
                    (CompiledActionFlags)EditorGUILayout.MaskField(
                        "伤害选项",
                        (int)action.flags,
                        ActionFlagLabels),
                    "修改伤害选项");
            }
        }

        private void DrawTargetFields(
            BattleTargetAuthoring target,
            string title,
            string undoName)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (target == null)
                {
                    EditorGUILayout.HelpBox(
                        $"{title}数据为空，请移除当前项后重新添加。",
                        MessageType.Error);
                    return;
                }

                SetManualValue(
                    ref target.type,
                    DrawLocalizedEnum(
                        "目标类型",
                        target.type,
                        TargetTypeValues,
                        TargetTypeLabels),
                    undoName);
                SetManualValue(
                    ref target.count,
                    Mathf.Max(0, EditorGUILayout.IntField(
                        new GUIContent("数量上限", "0 表示不限制数量。"),
                        target.count)),
                    undoName);
                SetManualValue(
                    ref target.includeDead,
                    EditorGUILayout.Toggle("包含死亡单位", target.includeDead),
                    undoName);
                SetManualValue(
                    ref target.configIdFilter,
                    Mathf.Max(0, EditorGUILayout.IntField(
                        new GUIContent(
                            "配置 ID 过滤",
                            "大于 0 时，只保留指定配置 ID 的单位。"),
                        target.configIdFilter)),
                    undoName);
            }
        }

        private void DrawValueFields(
            BattleValueAuthoring value,
            string title,
            string undoName)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (value == null)
                {
                    EditorGUILayout.HelpBox(
                        $"{title}数据为空，请移除当前项后重新添加。",
                        MessageType.Error);
                    return;
                }

                ValueSourceType displaySource = value.source;
                bool displayDynamicScale = value.useDynamicScale;
                bool displayMinimum = value.hasMinimum;
                bool displayMaximum = value.hasMaximum;

                SetManualValue(
                    ref value.source,
                    DrawLocalizedEnum(
                        "数值来源",
                        displaySource,
                        ValueSourceValues,
                        ValueSourceLabels),
                    undoName);
                if (ValueUsesConstant(displaySource))
                {
                    SetManualValue(
                        ref value.constant,
                        EditorGUILayout.LongField("固定值", value.constant),
                        undoName);
                }
                if (ValueUsesAttribute(displaySource))
                {
                    SetManualValue(
                        ref value.attribute,
                        DrawLocalizedEnum(
                            "属性",
                            value.attribute,
                            AttributeTypeValues,
                            AttributeTypeLabels),
                        undoName);
                }
                if (ValueUsesReferenceId(displaySource))
                {
                    SetManualValue(
                        ref value.referenceId,
                        Mathf.Max(0, EditorGUILayout.IntField(
                            new GUIContent(
                                "引用 ID",
                                "Buff 层数填 Buff ID；计数器/参数/变量填写对应数据 Key。"),
                            value.referenceId)),
                        undoName);
                }
                if (!displayDynamicScale)
                {
                    SetManualValue(
                        ref value.scaleBasisPoint,
                        Mathf.Max(0, EditorGUILayout.IntField(
                            new GUIContent(
                                "倍率（万分比）",
                                "10000 = 100%，13500 = 135%。"),
                            value.scaleBasisPoint)),
                        undoName);
                }

                SetManualValue(
                    ref value.useDynamicScale,
                    EditorGUILayout.Toggle(
                        new GUIContent(
                            "使用动态倍率",
                            "从策划参数或运行时变量读取倍率。"),
                        displayDynamicScale),
                    undoName);
                if (displayDynamicScale)
                {
                    DrawOperandFields(value.dynamicScale, "动态倍率", undoName);
                }

                SetManualValue(
                    ref value.offset,
                    EditorGUILayout.LongField("最终偏移", value.offset),
                    undoName);
                SetManualValue(
                    ref value.hasMinimum,
                    EditorGUILayout.Toggle("启用下限", displayMinimum),
                    undoName);
                if (displayMinimum)
                {
                    SetManualValue(
                        ref value.minimum,
                        EditorGUILayout.LongField("最小值", value.minimum),
                        undoName);
                }
                SetManualValue(
                    ref value.hasMaximum,
                    EditorGUILayout.Toggle("启用上限", displayMaximum),
                    undoName);
                if (displayMaximum)
                {
                    SetManualValue(
                        ref value.maximum,
                        EditorGUILayout.LongField("最大值", value.maximum),
                        undoName);
                }
            }
        }

        private void DrawOperandFields(
            BattleValueOperandAuthoring operand,
            string title,
            string undoName)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                if (operand == null)
                {
                    EditorGUILayout.HelpBox(
                        $"{title}数据为空，请关闭后重新开启动态倍率。",
                        MessageType.Error);
                    return;
                }

                ValueSourceType displaySource = operand.source;
                SetManualValue(
                    ref operand.source,
                    DrawLocalizedEnum(
                        "倍率来源",
                        displaySource,
                        ValueSourceValues,
                        ValueSourceLabels),
                    undoName);
                if (ValueUsesConstant(displaySource))
                {
                    SetManualValue(
                        ref operand.constant,
                        EditorGUILayout.LongField("固定倍率", operand.constant),
                        undoName);
                }
                if (ValueUsesAttribute(displaySource))
                {
                    SetManualValue(
                        ref operand.attribute,
                        DrawLocalizedEnum(
                            "倍率属性",
                            operand.attribute,
                            AttributeTypeValues,
                            AttributeTypeLabels),
                        undoName);
                }
                if (ValueUsesReferenceId(displaySource))
                {
                    SetManualValue(
                        ref operand.referenceId,
                        Mathf.Max(0, EditorGUILayout.IntField(
                            new GUIContent(
                                "引用 ID",
                                "逻辑参数/变量填写数据 Key；Buff 层数填写 Buff ID。"),
                            operand.referenceId)),
                        undoName);
                }
            }
        }

        private bool SetManualValue<T>(
            ref T field,
            T next,
            string undoName,
            bool invalidateActionOptions = false)
        {
            if (EqualityComparer<T>.Default.Equals(field, next))
            {
                return false;
            }

            Undo.RecordObject(selected, undoName);
            field = next;
            EditorUtility.SetDirty(selected);
            if (invalidateActionOptions)
            {
                actionOptions = Array.Empty<string>();
            }
            return true;
        }

        private static T DrawLocalizedEnum<T>(
            string label,
            T current,
            T[] values,
            string[] labels)
            where T : struct, Enum
        {
            int currentIndex = Array.IndexOf(values, current);
            int nextIndex = EditorGUILayout.Popup(
                label,
                Mathf.Max(0, currentIndex),
                labels);
            return values[Mathf.Clamp(nextIndex, 0, values.Length - 1)];
        }

        private static bool ValueUsesConstant(ValueSourceType source) =>
            source == ValueSourceType.Constant;

        private static bool ValueUsesAttribute(ValueSourceType source) =>
            source == ValueSourceType.OwnerAttribute ||
            source == ValueSourceType.AttackerAttribute ||
            source == ValueSourceType.TargetAttribute;

        private static bool ValueUsesReferenceId(ValueSourceType source) =>
            source == ValueSourceType.BuffStack ||
            source == ValueSourceType.BattleCounter ||
            source == ValueSourceType.LogicParameter ||
            source == ValueSourceType.InvocationVariable ||
            source == ValueSourceType.SkillVariable;

        private static bool ConditionUsesTarget(CompiledConditionType type) =>
            type != CompiledConditionType.Always &&
            type != CompiledConditionType.CounterCompareCurrentRound &&
            type != CompiledConditionType.LogicValueCompare;

        private static bool ConditionUsesComparison(CompiledConditionType type) =>
            type == CompiledConditionType.ConfigIdCompare ||
            type == CompiledConditionType.AttributeCompare ||
            type == CompiledConditionType.HpPercentCompare ||
            type == CompiledConditionType.EventValueCompare ||
            type == CompiledConditionType.CounterCompare ||
            type == CompiledConditionType.RoundCompare ||
            type == CompiledConditionType.TargetCountCompare ||
            type == CompiledConditionType.CounterCompareCurrentRound ||
            type == CompiledConditionType.LogicValueCompare;

        private static bool ConditionUsesValue(CompiledConditionType type) =>
            type == CompiledConditionType.ConfigIdCompare ||
            type == CompiledConditionType.AttributeCompare ||
            type == CompiledConditionType.HpPercentCompare ||
            type == CompiledConditionType.EventValueCompare ||
            type == CompiledConditionType.CounterCompare ||
            type == CompiledConditionType.RoundCompare ||
            type == CompiledConditionType.TargetCountCompare;

        private static bool ConditionUsesReferenceId(CompiledConditionType type) =>
            type == CompiledConditionType.HasBuff ||
            type == CompiledConditionType.CounterCompare ||
            type == CompiledConditionType.CounterCompareCurrentRound;

        private static bool ActionUsesTarget(CompiledActionType type) =>
            type != CompiledActionType.AddCounter &&
            type != CompiledActionType.SetVariable &&
            type != CompiledActionType.EndTurn &&
            type != CompiledActionType.SetInvocationVariable &&
            type != CompiledActionType.AddInvocationVariable &&
            type != CompiledActionType.SetSkillVariable &&
            type != CompiledActionType.AddSkillVariable;

        private static bool ActionUsesValue(CompiledActionType type) =>
            type == CompiledActionType.Damage ||
            type == CompiledActionType.Heal ||
            type == CompiledActionType.ModifyAttribute ||
            type == CompiledActionType.ModifyResource ||
            type == CompiledActionType.Revive ||
            type == CompiledActionType.MovePosition ||
            type == CompiledActionType.AddCounter ||
            type == CompiledActionType.SetVariable ||
            type == CompiledActionType.SetInvocationVariable ||
            type == CompiledActionType.AddInvocationVariable ||
            type == CompiledActionType.SetSkillVariable ||
            type == CompiledActionType.AddSkillVariable;

        private static bool ActionUsesAttribute(CompiledActionType type) =>
            type == CompiledActionType.ModifyAttribute ||
            type == CompiledActionType.ModifyResource;

        private static bool ActionUsesReferenceId(CompiledActionType type) =>
            type == CompiledActionType.AddBuff ||
            type == CompiledActionType.RemoveBuff ||
            type == CompiledActionType.AddCounter ||
            type == CompiledActionType.SetVariable ||
            type == CompiledActionType.SetInvocationVariable ||
            type == CompiledActionType.AddInvocationVariable ||
            type == CompiledActionType.SetSkillVariable ||
            type == CompiledActionType.AddSkillVariable;

        private static void DrawRuleChild(
            InspectorProperty ruleProperty,
            string childName)
        {
            for (int index = 0; index < ruleProperty.Children.Count; index++)
            {
                InspectorProperty child = ruleProperty.Children[index];
                if (child.Name == childName)
                {
                    child.Draw();
                    return;
                }
            }
        }

        private void ApplyTrackCommand(ListEditCommand command)
        {
            if (!(selected?.Tracks is List<BattleLogicTrackAuthoring> tracks))
            {
                Debug.LogError("[TurnBasedBattle] 无法修改逻辑轨道列表。", selected);
                return;
            }

            NormalizeSelection();
            if (command == ListEditCommand.Remove &&
                !EditorUtility.DisplayDialog(
                    "移除逻辑轨道",
                    $"确定移除轨道“{GetTrackLabel(selectedTrackIndex)}”及其全部规则吗？",
                    "移除",
                    "取消"))
            {
                return;
            }

            propertyTree?.ApplyChanges();
            Undo.RecordObject(selected, GetTrackUndoName(command));
            switch (command)
            {
                case ListEditCommand.Add:
                    tracks.Add(new BattleLogicTrackAuthoring
                    {
                        trackName = $"逻辑轨道 {tracks.Count + 1}",
                        executionGroup = "主流程",
                        executionMode = BattleAuthoringExecutionMode.Sequence,
                        rules = new List<BattleRuleAuthoring>(),
                    });
                    selectedTrackIndex = tracks.Count - 1;
                    selectedRuleIndex = 0;
                    selectedConditionIndex = 0;
                    selectedActionIndex = 0;
                    break;
                case ListEditCommand.Remove:
                    if (selectedTrackIndex >= 0 && selectedTrackIndex < tracks.Count)
                    {
                        tracks.RemoveAt(selectedTrackIndex);
                        selectedTrackIndex = Mathf.Min(
                            selectedTrackIndex,
                            tracks.Count - 1);
                        selectedRuleIndex = 0;
                        selectedConditionIndex = 0;
                        selectedActionIndex = 0;
                    }
                    break;
                case ListEditCommand.MoveUp:
                    MoveListItem(tracks, selectedTrackIndex, selectedTrackIndex - 1);
                    selectedTrackIndex--;
                    break;
                case ListEditCommand.MoveDown:
                    MoveListItem(tracks, selectedTrackIndex, selectedTrackIndex + 1);
                    selectedTrackIndex++;
                    break;
            }

            CompleteStructureChange();
        }

        private void ApplyRuleCommand(ListEditCommand command)
        {
            BattleLogicTrackAuthoring track = GetSelectedTrack();
            if (track == null)
            {
                return;
            }

            if (command == ListEditCommand.Remove &&
                !EditorUtility.DisplayDialog(
                    "移除逻辑规则",
                    $"确定移除规则“{GetRuleLabel(selectedRuleIndex)}”吗？",
                    "移除",
                    "取消"))
            {
                return;
            }

            propertyTree?.ApplyChanges();
            Undo.RecordObject(selected, GetRuleUndoName(command));
            if (track.rules == null)
            {
                track.rules = new List<BattleRuleAuthoring>();
            }
            switch (command)
            {
                case ListEditCommand.Add:
                    track.rules.Add(CreateDefaultRule(track));
                    selectedRuleIndex = track.rules.Count - 1;
                    selectedConditionIndex = 0;
                    selectedActionIndex = 0;
                    break;
                case ListEditCommand.Remove:
                    if (selectedRuleIndex >= 0 && selectedRuleIndex < track.rules.Count)
                    {
                        track.rules.RemoveAt(selectedRuleIndex);
                        selectedRuleIndex = Mathf.Min(
                            selectedRuleIndex,
                            track.rules.Count - 1);
                        selectedConditionIndex = 0;
                        selectedActionIndex = 0;
                    }
                    break;
                case ListEditCommand.MoveUp:
                    MoveListItem(
                        track.rules,
                        selectedRuleIndex,
                        selectedRuleIndex - 1);
                    selectedRuleIndex--;
                    break;
                case ListEditCommand.MoveDown:
                    MoveListItem(
                        track.rules,
                        selectedRuleIndex,
                        selectedRuleIndex + 1);
                    selectedRuleIndex++;
                    break;
            }

            CompleteStructureChange();
        }

        private void ApplyConditionCommand(ListEditCommand command)
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            if (rule == null)
            {
                return;
            }

            if (command == ListEditCommand.Remove &&
                !EditorUtility.DisplayDialog(
                    "移除触发条件",
                    $"确定移除“{GetConditionLabel(selectedConditionIndex)}”吗？",
                    "移除",
                    "取消"))
            {
                return;
            }

            propertyTree?.ApplyChanges();
            Undo.RecordObject(selected, GetConditionUndoName(command));
            rule.conditions = rule.conditions ?? new List<BattleConditionAuthoring>();
            switch (command)
            {
                case ListEditCommand.Add:
                    rule.conditions.Add(new BattleConditionAuthoring());
                    selectedConditionIndex = rule.conditions.Count - 1;
                    break;
                case ListEditCommand.Remove:
                    if (selectedConditionIndex >= 0 &&
                        selectedConditionIndex < rule.conditions.Count)
                    {
                        rule.conditions.RemoveAt(selectedConditionIndex);
                        selectedConditionIndex = Mathf.Min(
                            selectedConditionIndex,
                            rule.conditions.Count - 1);
                    }
                    break;
                case ListEditCommand.MoveUp:
                    MoveListItem(
                        rule.conditions,
                        selectedConditionIndex,
                        selectedConditionIndex - 1);
                    selectedConditionIndex--;
                    break;
                case ListEditCommand.MoveDown:
                    MoveListItem(
                        rule.conditions,
                        selectedConditionIndex,
                        selectedConditionIndex + 1);
                    selectedConditionIndex++;
                    break;
            }

            CompleteStructureChange();
        }

        private void ApplyActionCommand(ListEditCommand command)
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            if (rule == null)
            {
                return;
            }

            if (command == ListEditCommand.Remove &&
                !EditorUtility.DisplayDialog(
                    "移除逻辑操作",
                    $"确定移除“{GetActionLabel(selectedActionIndex)}”吗？",
                    "移除",
                    "取消"))
            {
                return;
            }

            propertyTree?.ApplyChanges();
            Undo.RecordObject(selected, GetActionUndoName(command));
            rule.actions = rule.actions ?? new List<BattleActionAuthoring>();
            switch (command)
            {
                case ListEditCommand.Add:
                    rule.actions.Add(new BattleActionAuthoring
                    {
                        operationName = $"逻辑操作 {rule.actions.Count + 1}",
                        executionGroup = string.IsNullOrWhiteSpace(rule.executionGroup)
                            ? "主流程"
                            : rule.executionGroup,
                        executionMode = rule.executionMode,
                    });
                    selectedActionIndex = rule.actions.Count - 1;
                    break;
                case ListEditCommand.Remove:
                    if (selectedActionIndex >= 0 &&
                        selectedActionIndex < rule.actions.Count)
                    {
                        rule.actions.RemoveAt(selectedActionIndex);
                        selectedActionIndex = Mathf.Min(
                            selectedActionIndex,
                            rule.actions.Count - 1);
                    }
                    break;
                case ListEditCommand.MoveUp:
                    MoveListItem(
                        rule.actions,
                        selectedActionIndex,
                        selectedActionIndex - 1);
                    selectedActionIndex--;
                    break;
                case ListEditCommand.MoveDown:
                    MoveListItem(
                        rule.actions,
                        selectedActionIndex,
                        selectedActionIndex + 1);
                    selectedActionIndex++;
                    break;
            }

            CompleteStructureChange();
        }

        private BattleRuleAuthoring CreateDefaultRule(
            BattleLogicTrackAuthoring track)
        {
            string executionGroup = string.IsNullOrWhiteSpace(track.executionGroup)
                ? "主流程"
                : track.executionGroup;
            return new BattleRuleAuthoring
            {
                operationName = "技能命中时结算伤害",
                executionGroup = executionGroup,
                executionMode = track.executionMode,
                id = GetNextRuleId(),
                trigger = BattleEventType.SkillCast,
                phase = BattleEventPhase.Main,
                ownerMustBeAlive = true,
                conditions = new List<BattleConditionAuthoring>(),
                actions = new List<BattleActionAuthoring>
                {
                    new BattleActionAuthoring
                    {
                        operationName = "对选中目标造成伤害",
                        executionGroup = executionGroup,
                        executionMode = BattleAuthoringExecutionMode.Sequence,
                        type = CompiledActionType.Damage,
                        target = new BattleTargetAuthoring
                        {
                            type = TargetSelectorType.SelectedTargets,
                        },
                        value = new BattleValueAuthoring
                        {
                            source = ValueSourceType.OwnerAttribute,
                            attribute = AttributeType.Attack,
                            scaleBasisPoint = BattleNumeric.BasisPointOne,
                            hasMinimum = true,
                            minimum = 1,
                        },
                        flags = CompiledActionFlags.CanMiss |
                            CompiledActionFlags.CanCritical,
                    },
                },
            };
        }

        private int GetNextRuleId()
        {
            int maxId = 0;
            IReadOnlyList<BattleRuleAuthoring> rules = selected.Rules;
            for (int index = 0; index < rules.Count; index++)
            {
                if (rules[index] != null)
                {
                    maxId = Math.Max(maxId, rules[index].id);
                }
            }

            return maxId < int.MaxValue ? Math.Max(1, maxId + 1) : int.MaxValue;
        }

        private void CompleteStructureChange()
        {
            EditorUtility.SetDirty(selected);
            DisposePropertyTree();
            serialized = new SerializedObject(selected);
            CreatePropertyTree();
            trackOptions = Array.Empty<string>();
            ruleOptions = Array.Empty<string>();
            conditionOptions = Array.Empty<string>();
            actionOptions = Array.Empty<string>();
            NormalizeSelection();
            Repaint();
        }

        private void NormalizeSelection()
        {
            int trackCount = selected?.Tracks?.Count ?? 0;
            selectedTrackIndex = trackCount == 0
                ? -1
                : Mathf.Clamp(selectedTrackIndex, 0, trackCount - 1);

            BattleLogicTrackAuthoring track = GetSelectedTrack();
            int ruleCount = track?.rules?.Count ?? 0;
            selectedRuleIndex = ruleCount == 0
                ? -1
                : Mathf.Clamp(selectedRuleIndex, 0, ruleCount - 1);

            BattleRuleAuthoring rule = GetSelectedRule();
            int conditionCount = rule?.conditions?.Count ?? 0;
            selectedConditionIndex = conditionCount == 0
                ? -1
                : Mathf.Clamp(selectedConditionIndex, 0, conditionCount - 1);
            int actionCount = rule?.actions?.Count ?? 0;
            selectedActionIndex = actionCount == 0
                ? -1
                : Mathf.Clamp(selectedActionIndex, 0, actionCount - 1);
        }

        private BattleLogicTrackAuthoring GetSelectedTrack()
        {
            IReadOnlyList<BattleLogicTrackAuthoring> tracks = selected?.Tracks;
            return tracks != null && selectedTrackIndex >= 0 &&
                selectedTrackIndex < tracks.Count
                    ? tracks[selectedTrackIndex]
                    : null;
        }

        private BattleRuleAuthoring GetSelectedRule()
        {
            BattleLogicTrackAuthoring track = GetSelectedTrack();
            return track?.rules != null && selectedRuleIndex >= 0 &&
                selectedRuleIndex < track.rules.Count
                    ? track.rules[selectedRuleIndex]
                    : null;
        }

        private void EnsureTrackOptions()
        {
            int count = selected?.Tracks?.Count ?? 0;
            bool rebuild = trackOptions.Length != count;
            for (int index = 0; !rebuild && index < count; index++)
            {
                rebuild = trackOptions[index] != GetTrackLabel(index);
            }

            if (!rebuild)
            {
                return;
            }

            trackOptions = new string[count];
            for (int index = 0; index < count; index++)
            {
                trackOptions[index] = GetTrackLabel(index);
            }
        }

        private void EnsureRuleOptions()
        {
            BattleLogicTrackAuthoring track = GetSelectedTrack();
            int count = track?.rules?.Count ?? 0;
            bool rebuild = ruleOptions.Length != count;
            for (int index = 0; !rebuild && index < count; index++)
            {
                rebuild = ruleOptions[index] != GetRuleLabel(index);
            }

            if (!rebuild)
            {
                return;
            }

            ruleOptions = new string[count];
            for (int index = 0; index < count; index++)
            {
                ruleOptions[index] = GetRuleLabel(index);
            }
        }

        private void EnsureConditionOptions()
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            int count = rule?.conditions?.Count ?? 0;
            bool rebuild = conditionOptions.Length != count;
            for (int index = 0; !rebuild && index < count; index++)
            {
                rebuild = conditionOptions[index] != GetConditionLabel(index);
            }

            if (!rebuild)
            {
                return;
            }

            conditionOptions = new string[count];
            for (int index = 0; index < count; index++)
            {
                conditionOptions[index] = GetConditionLabel(index);
            }
        }

        private void EnsureActionOptions()
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            int count = rule?.actions?.Count ?? 0;
            bool rebuild = actionOptions.Length != count;
            for (int index = 0; !rebuild && index < count; index++)
            {
                rebuild = actionOptions[index] != GetActionLabel(index);
            }

            if (!rebuild)
            {
                return;
            }

            actionOptions = new string[count];
            for (int index = 0; index < count; index++)
            {
                actionOptions[index] = GetActionLabel(index);
            }
        }

        private string GetTrackLabel(int index)
        {
            IReadOnlyList<BattleLogicTrackAuthoring> tracks = selected?.Tracks;
            if (tracks == null || index < 0 || index >= tracks.Count)
            {
                return "未选择轨道";
            }

            BattleLogicTrackAuthoring track = tracks[index];
            return track != null && !string.IsNullOrWhiteSpace(track.trackName)
                ? track.trackName
                : $"轨道 {index + 1}";
        }

        private string GetRuleLabel(int index)
        {
            BattleLogicTrackAuthoring track = GetSelectedTrack();
            if (track?.rules == null || index < 0 || index >= track.rules.Count)
            {
                return "未选择规则";
            }

            BattleRuleAuthoring rule = track.rules[index];
            return rule != null && !string.IsNullOrWhiteSpace(rule.operationName)
                ? rule.operationName
                : $"规则 {rule?.id ?? index + 1}";
        }

        private string GetConditionLabel(int index)
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            if (rule?.conditions == null || index < 0 ||
                index >= rule.conditions.Count)
            {
                return "未选择条件";
            }

            BattleConditionAuthoring condition = rule.conditions[index];
            return condition == null
                ? $"条件 {index + 1}"
                : $"{index + 1}. {GetConditionTypeLabel(condition.type)}";
        }

        private static string[] BuildLabels<T>(
            IReadOnlyList<T> values,
            Func<T, string> labelGetter)
        {
            var labels = new string[values.Count];
            for (int index = 0; index < values.Count; index++)
            {
                labels[index] = labelGetter(values[index]);
            }
            return labels;
        }

        private string GetActionLabel(int index)
        {
            BattleRuleAuthoring rule = GetSelectedRule();
            if (rule?.actions == null || index < 0 || index >= rule.actions.Count)
            {
                return "未选择操作";
            }

            BattleActionAuthoring action = rule.actions[index];
            if (action == null)
            {
                return $"操作 {index + 1}";
            }

            string label = string.IsNullOrWhiteSpace(action.operationName)
                ? GetActionTypeLabel(action.type)
                : action.operationName;
            return $"{index + 1}. {label}";
        }

        private static string GetConditionTypeLabel(CompiledConditionType type)
        {
            switch (type)
            {
                case CompiledConditionType.Always: return "始终满足";
                case CompiledConditionType.IsSelf: return "目标是自身";
                case CompiledConditionType.IsAlive: return "目标存活";
                case CompiledConditionType.IsDead: return "目标死亡";
                case CompiledConditionType.IsEnemy: return "目标是敌人";
                case CompiledConditionType.IsAlly: return "目标是友军";
                case CompiledConditionType.ConfigIdCompare: return "配置 ID 比较";
                case CompiledConditionType.HasBuff: return "拥有 Buff";
                case CompiledConditionType.AttributeCompare: return "属性比较";
                case CompiledConditionType.HpPercentCompare: return "生命百分比比较";
                case CompiledConditionType.EventValueCompare: return "事件数值比较";
                case CompiledConditionType.CounterCompare: return "计数器比较";
                case CompiledConditionType.RoundCompare: return "回合数比较";
                case CompiledConditionType.TargetCountCompare: return "目标数量比较";
                case CompiledConditionType.CounterCompareCurrentRound:
                    return "计数器与当前回合比较";
                case CompiledConditionType.LogicValueCompare:
                    return "两个动态数值比较";
                default: return type.ToString();
            }
        }

        private static string GetActionTypeLabel(CompiledActionType type)
        {
            switch (type)
            {
                case CompiledActionType.Damage: return "造成伤害";
                case CompiledActionType.Heal: return "恢复生命";
                case CompiledActionType.AddBuff: return "添加 Buff";
                case CompiledActionType.RemoveBuff: return "移除 Buff";
                case CompiledActionType.ModifyAttribute: return "修改属性";
                case CompiledActionType.ModifyResource: return "修改资源";
                case CompiledActionType.Kill: return "直接击杀";
                case CompiledActionType.Revive: return "复活";
                case CompiledActionType.MovePosition: return "修改逻辑站位";
                case CompiledActionType.AddCounter: return "累加计数器";
                case CompiledActionType.SetVariable: return "设置变量";
                case CompiledActionType.EndTurn: return "结束当前行动";
                case CompiledActionType.SetInvocationVariable:
                    return "设置单次触发变量";
                case CompiledActionType.AddInvocationVariable:
                    return "累加单次触发变量";
                case CompiledActionType.SetSkillVariable:
                    return "设置技能实例变量";
                case CompiledActionType.AddSkillVariable:
                    return "累加技能实例变量";
                default: return type.ToString();
            }
        }

        private static void MoveListItem<T>(List<T> list, int from, int to)
        {
            if (list == null || from < 0 || from >= list.Count ||
                to < 0 || to >= list.Count || from == to)
            {
                return;
            }

            T value = list[from];
            list.RemoveAt(from);
            list.Insert(to, value);
        }

        private static string GetTrackUndoName(ListEditCommand command)
        {
            switch (command)
            {
                case ListEditCommand.Add: return "添加逻辑轨道";
                case ListEditCommand.Remove: return "移除逻辑轨道";
                default: return "调整逻辑轨道顺序";
            }
        }

        private static string GetRuleUndoName(ListEditCommand command)
        {
            switch (command)
            {
                case ListEditCommand.Add: return "添加逻辑规则";
                case ListEditCommand.Remove: return "移除逻辑规则";
                default: return "调整逻辑规则顺序";
            }
        }

        private static string GetConditionUndoName(ListEditCommand command)
        {
            switch (command)
            {
                case ListEditCommand.Add: return "添加触发条件";
                case ListEditCommand.Remove: return "移除触发条件";
                default: return "调整触发条件顺序";
            }
        }

        private static string GetActionUndoName(ListEditCommand command)
        {
            switch (command)
            {
                case ListEditCommand.Add: return "添加逻辑操作";
                case ListEditCommand.Remove: return "移除逻辑操作";
                default: return "调整逻辑操作顺序";
            }
        }

        private void CreateAsset()
        {
            int id = 100000 + assets.Count + 1;
            TurnBasedSkillLogicAsset asset = TurnBasedAssetEditorUtility.CreateAsset<TurnBasedSkillLogicAsset>(
                TurnBasedSkillAssetGenerator.Root + "/Logic",
                "skill_logic_new",
                value => value.ResetToDamageSample("skill_logic_new", id, 10000, 0));
            if (asset != null)
            {
                ReloadAssets(asset);
            }
        }

        private void ReloadAssets(TurnBasedSkillLogicAsset preferred = null)
        {
            assets = TurnBasedAssetEditorUtility.FindAssets<TurnBasedSkillLogicAsset>();
            Select(preferred != null ? preferred : selected != null ? selected :
                assets.Count > 0 ? assets[0] : null);
        }

        private void RemoveSelectedAsset()
        {
            if (selected == null)
            {
                return;
            }

            TurnBasedSkillLogicAsset removing = selected;
            string label = string.IsNullOrWhiteSpace(removing.LogicId)
                ? removing.name
                : removing.LogicId;
            if (!TurnBasedAssetEditorUtility.RemoveAssetWithConfirmation(
                    removing,
                    "技能逻辑",
                    label))
            {
                return;
            }

            selected = null;
            serialized = null;
            DisposePropertyTree();
            ReloadAssets();
            ShowNotification(new GUIContent($"已移除 {label}"));
        }

        private void Select(TurnBasedSkillLogicAsset asset)
        {
            bool assetChanged = selected != asset;
            if (propertyTree != null)
            {
                propertyTree.ApplyChanges();
            }
            DisposePropertyTree();
            selected = asset;
            serialized = selected == null ? null : new SerializedObject(selected);
            CreatePropertyTree();
            if (assetChanged)
            {
                selectedTrackIndex = 0;
                selectedRuleIndex = 0;
                selectedConditionIndex = 0;
                selectedActionIndex = 0;
                detailScroll = Vector2.zero;
                showTrackSettings = false;
            }
            trackOptions = Array.Empty<string>();
            ruleOptions = Array.Empty<string>();
            conditionOptions = Array.Empty<string>();
            actionOptions = Array.Empty<string>();
            NormalizeSelection();
            Repaint();
        }

        private void CreatePropertyTree()
        {
            propertyTree = serialized == null ? null : PropertyTree.Create(serialized);
            if (propertyTree != null)
            {
                propertyTree.DrawMonoScriptObjectField = false;
            }
        }

        private void DisposePropertyTree()
        {
            propertyTree?.Dispose();
            propertyTree = null;
        }

        private void Save()
        {
            propertyTree?.ApplyChanges();
            serialized?.ApplyModifiedProperties();
            EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssets();
            ShowNotification(new GUIContent("逻辑资产已保存"));
        }

        private void ValidateAsset()
        {
            try
            {
                propertyTree?.ApplyChanges();
                serialized?.ApplyModifiedProperties();
                selected.Compile();
                Save();
                ShowNotification(new GUIContent("逻辑校验通过"));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, selected);
                ShowNotification(new GUIContent("校验失败，请查看 Console"));
            }
        }

        private void GenerateCode()
        {
            try
            {
                propertyTree?.ApplyChanges();
                serialized?.ApplyModifiedProperties();
                string path = TurnBasedLogicDataAuthoringUtility.GenerateCode(selected);
                EditorUtility.SetDirty(selected);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("C# 已生成"));
                Debug.Log($"[TurnBasedLogicEditor] 已生成逻辑常量：{path}", selected);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, selected);
                ShowNotification(new GUIContent("生成失败，请查看 Console"));
            }
        }
    }
}
