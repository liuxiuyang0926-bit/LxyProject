using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using StateControl.Runtime;
using Object = UnityEngine.Object;

namespace StateControl.Editor
{
    public class StateEditor : EditorWindow
    {
        private class StateClipboard
        {
            /// <summary>
            /// 公开的名称数据。
            /// </summary>
            public string Name;
            /// <summary>
            /// 公开的Note数据。
            /// </summary>
            public string Note;
        }

        private class StateGroupClipboard
        {
            /// <summary>
            /// 公开的Group名称数据。
            /// </summary>
            public string GroupName;
            /// <summary>
            /// 公开的GroupNote数据。
            /// </summary>
            public string GroupNote;
            /// <summary>
            /// 公开的状态数据。
            /// </summary>
            public List<StateClipboard> States = new List<StateClipboard>();
        }

        private static StateGroupClipboard s_clipboard;
        private static StateController currentController;
        bool hasSelectedStateController = false;
        private Dictionary<int, TextField> editingGroupNames = new Dictionary<int, TextField>();
        private Dictionary<int, TextField> editingGroupNotes = new Dictionary<int, TextField>();
        private HashSet<string> selectedGroupNames = new HashSet<string>();

        // UI元素
        private ScrollView groupList;
        private TextField newGroupInput;
        private ScrollView modifierContainer;
        private VisualElement dragArea;

        /// <summary>
        /// 执行显示窗口相关逻辑。
        /// </summary>
        [MenuItem("Window/状态编辑器")]
        public static void ShowWindow()
        {
            var window = GetWindow<StateEditor>();
            window.titleContent = new GUIContent("状态编辑器");
            window.Show();
        }

        /// <summary>
        /// 创建GUI。
        /// </summary>
        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;

            // 顶部控制器显示
            var controllerField = new ObjectField("当前控制器");
            controllerField.objectType = typeof(StateController);
            controllerField.value = currentController;
            controllerField.SetEnabled(false);
            controllerField.style.marginLeft = 10;
            controllerField.style.marginRight = 10;
            controllerField.style.marginTop = 10;
            controllerField.style.marginBottom = 10;
            root.Add(controllerField);

            // 添加工具栏（包含保存按钮）
            AddToolbar();

            // 主要内容区域
            var mainContent = new VisualElement();
            mainContent.style.flexGrow = 1;
            mainContent.style.flexDirection = FlexDirection.Row;
            root.Add(mainContent);

            // 左右分栏
            var splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            mainContent.Add(splitView);

            // 创建左侧面板
            var leftPanel = new VisualElement();
            leftPanel.style.width = 300;
            leftPanel.style.borderRightWidth = 1;
            leftPanel.style.borderRightColor = new Color(0.1f, 0.1f, 0.1f);
            leftPanel.style.paddingRight = 10;
            leftPanel.style.paddingLeft = 10;
            leftPanel.style.paddingTop = 10;
            splitView.Add(leftPanel);

            var addBuiltInGroupContainer = new VisualElement();
            addBuiltInGroupContainer.style.flexDirection = FlexDirection.Row;
            addBuiltInGroupContainer.style.marginBottom = 10;
            leftPanel.Add(addBuiltInGroupContainer);

            List<string> builtInStateNames = new List<string>();
            foreach (var stateEnum in Enum.GetValues(typeof(BuiltInStateEnum)))
            {
                BuiltInStateEnumAttribute attribute = (BuiltInStateEnumAttribute)Attribute.GetCustomAttribute(
                    typeof(BuiltInStateEnum).GetField(stateEnum.ToString()), typeof(BuiltInStateEnumAttribute));
                if (attribute != null)
                {
                    builtInStateNames.Add(stateEnum.ToString());
                }
            }

            Func<string, string> func = value =>
            {
                BuiltInStateEnumAttribute attribute = (BuiltInStateEnumAttribute)Attribute.GetCustomAttribute(
                    typeof(BuiltInStateEnum).GetField(value), typeof(BuiltInStateEnumAttribute));
                return attribute.Name;
            };
            
            var builtInStateDropDown = new DropdownField(builtInStateNames, 0, func,func);
            builtInStateDropDown.style.flexGrow = 1;
            builtInStateDropDown.style.flexShrink = 1;
            builtInStateDropDown.style.marginRight = 5;
            addBuiltInGroupContainer.Add(builtInStateDropDown);
            
            var addBuiltInStateBtn = new Button(() => {
                if (builtInStateDropDown.index >= 0 && builtInStateDropDown.index < builtInStateNames.Count)
                {
                    var groupName = builtInStateNames[builtInStateDropDown.index];
                    AddBuiltInGroup(groupName);
                }
            }) { text = "添加内置的状态组" };
            addBuiltInStateBtn.style.width = 110;
            addBuiltInGroupContainer.Add(addBuiltInStateBtn);

            // 添加状态组输入框
            var addGroupContainer = new VisualElement();
            addGroupContainer.style.flexDirection = FlexDirection.Row;
            addGroupContainer.style.marginBottom = 10;
            leftPanel.Add(addGroupContainer);

            newGroupInput = new TextField();
            newGroupInput.style.flexGrow = 1;
            newGroupInput.style.marginRight = 5;
            addGroupContainer.Add(newGroupInput);

            var addGroupBtn = new Button(() => AddNewGroup(newGroupInput.value)) { text = "添加自定义状态组" };
            addGroupBtn.style.width = 110;
            addGroupContainer.Add(addGroupBtn);

            // 粘贴状态组按钮
            var pasteGroupBtn = new Button(() => PasteStateGroup()) { text = "粘贴状态组" };
            pasteGroupBtn.style.marginBottom = 10;
            leftPanel.Add(pasteGroupBtn);

            // 状态组列表
            var groupListLabel = new Label("状态组列表");
            groupListLabel.style.fontSize = 14;
            groupListLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            groupListLabel.style.marginBottom = 10;
            leftPanel.Add(groupListLabel);

            groupList = new ScrollView();
            groupList.style.flexGrow = 1;
            leftPanel.Add(groupList);

            // 右侧面板
            var rightPanel = new VisualElement();
            rightPanel.style.flexGrow = 1;
            rightPanel.style.paddingLeft = 10;
            rightPanel.style.paddingRight = 10;
            rightPanel.style.paddingTop = 10;
            splitView.Add(rightPanel);

            // 添加状态输入框
            var addStateContainer = new VisualElement();
            addStateContainer.style.flexDirection = FlexDirection.Row;
            addStateContainer.style.marginBottom = 10;
            rightPanel.Add(addStateContainer);

            // 拖拽区域
            dragArea = new VisualElement();
            dragArea.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
            dragArea.style.height = 60;
            dragArea.style.marginBottom = 10;
            dragArea.style.justifyContent = Justify.Center;
            dragArea.style.alignItems = Align.Center;
            dragArea.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            dragArea.RegisterCallback<DragPerformEvent>(OnDragPerform);
            rightPanel.Add(dragArea);

            var dragHintLabel = new Label("拖拽GameObject到这里添加修改目标");
            dragHintLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            dragHintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            dragArea.Add(dragHintLabel);

            modifierContainer = new ScrollView();
            modifierContainer.style.flexGrow = 1;
            modifierContainer.contentContainer.style.flexDirection = FlexDirection.Row;
            modifierContainer.contentContainer.style.flexWrap = Wrap.Wrap;
            modifierContainer.contentContainer.style.width = new Length(100, LengthUnit.Percent);
            rightPanel.Add(modifierContainer);

            // 监听选择变化
            Selection.selectionChanged += OnSelectionChanged;

            // 添加编辑器更新事件监听
            EditorApplication.update += OnEditorUpdate;

            // 注册Undo事件，确保在Undo/Redo时刷新StateEditor
            Undo.undoRedoPerformed += OnUndoRedo;

            // 初始化完成后，调用一次OnSelectionChanged
            EditorApplication.delayCall += () => OnSelectionChanged();
            
            // 如果没有当前控制器，清空UI
            if (currentController == null)
            {
                ClearUI();
            }
        }
        
        // 使用EditorApplication.update而不是MonoBehaviour.Update
        /// <summary>
        /// 响应编辑器Update事件。
        /// </summary>
        private void OnEditorUpdate()
        {
            // 检查当前控制器是否还存在
            if (currentController != null)
            {
                if (currentController.gameObject == null)
                {
                    ClearUI();
                    Repaint();
                }
            }
            else
            {
                if (hasSelectedStateController)
                {
                    ClearUI();
                    Repaint();
                }
            }
        }

        /// <summary>
        /// 响应UndoRedo事件。
        /// </summary>
        private void OnUndoRedo()
        {
            // 当执行Undo/Redo时，刷新StateEditor UI
            if (currentController != null)
            {
                // 重新应用所有当前状态
                currentController.ReapplyAllCurrentStates();

                RefreshUI();
                Repaint();
            }
        }

        /// <summary>
        /// 响应SelectionChanged事件。
        /// </summary>
        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                var controller = Selection.activeGameObject.GetComponent<StateController>();
                if (controller != null)
                {
                    if (controller != currentController)
                    {
                        editingGroupNames.Clear();
                        editingGroupNotes.Clear();
                    }
                    currentController = controller;
                    hasSelectedStateController = true;
                    RefreshUI();
                }
            }
        }

        // 清空UI并显示提示
        /// <summary>
        /// 清空UI。
        /// </summary>
        private void ClearUI()
        {
            // 清空当前控制器引用
            currentController = null;
            hasSelectedStateController = false;
            
            // 更新顶部控制器显示
            var controllerField = rootVisualElement.Q<ObjectField>();
            if (controllerField != null)
            {
                controllerField.value = null;
            }

            modifierContainer?.Clear();
            groupList?.Clear();
            
            if (modifierContainer != null)
            {
                var modifierHintLabel = new Label("请选择带有StateController组件的游戏对象");
                modifierHintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                modifierHintLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                modifierHintLabel.style.fontSize = 14;
                modifierHintLabel.style.marginTop = 50;
                modifierContainer.Add(modifierHintLabel);
            }
        }

        /// <summary>
        /// 刷新UI。
        /// </summary>
        private void RefreshUI()
        {
            // 如果当前控制器为空，调用ClearUI并返回
            if (currentController == null)
            {
                ClearUI();
                return;
            }

            // 更新顶部控制器显示
            var controllerField = rootVisualElement.Q<ObjectField>();
            if (controllerField != null)
            {
                controllerField.value = currentController;
            }

            // 清空列表
            modifierContainer?.Clear();
            groupList?.Clear();

            if (currentController == null)
                return;

            // 刷新状态组列表
            for (int i = 0; i < currentController.StateGroups.Count; i++)
            {
                var group = currentController.StateGroups[i];
                var groupItem = CreateGroupItem(group, i);
                groupList.Add(groupItem);
            }

            UpdateRecordDisplay();
        }

        void UpdateRecordDisplay()
        {
            modifierContainer.Clear();
            // 筛选提示文本单独一行，宽度100%，不包在Row里
            if (selectedGroupNames.Count > 0)
            {
                var filterRow = new VisualElement();
                filterRow.style.flexDirection = FlexDirection.Row;
                filterRow.style.alignItems = Align.Center;
                filterRow.style.width = Length.Percent(100);
                filterRow.style.marginBottom = 4;
                filterRow.style.marginTop = 2;

                var filterLabel = new Label($"当前状态组筛选：{string.Join(", ", selectedGroupNames)}");
                filterLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                filterLabel.style.color = new Color(0.8f, 0.8f, 0.3f);
                filterLabel.style.flexGrow = 1;
                filterRow.Add(filterLabel);

                var clearFilterBtn = new Button(() =>
                {
                    selectedGroupNames.Clear();
                    RefreshUI();
                }) { text = "清除筛选" };
                clearFilterBtn.style.marginLeft = 8;
                filterRow.Add(clearFilterBtn);

                modifierContainer.Add(filterRow);

                var line = new VisualElement();
                line.style.height = 1;
                line.style.width = Length.Percent(100);
                line.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);
                line.style.marginBottom = 8;
                modifierContainer.Add(line);
            }
            int recordGroupCount = currentController.ModifierRecordGroups.Count;
            for (int i = 0; i < recordGroupCount; i++)
            {
                var recordGroup = currentController.ModifierRecordGroups[i];
                var groupNames = recordGroup.GetAllStateGroup().ToHashSet();
                // 保持原有显示逻辑
                if (selectedGroupNames.Count > 0 && !selectedGroupNames.All(name => groupNames.Contains(name)))
                    continue;
                var target = recordGroup.Target;
                var curIndex = i; // 创建一个固定的索引值

                var recordItem = new VisualElement();
                recordItem.style.marginBottom = 10;
                recordItem.style.marginRight = 10;
                recordItem.style.width = new Length(48, LengthUnit.Percent);
                recordItem.style.flexShrink = 0;
                recordItem.style.flexGrow = 0;
                bool recordHasIssue = StateControllerCheckUtil.HasRecordGroupProblem(recordGroup);
                recordItem.style.backgroundColor = recordHasIssue
                    ? new Color(0.6f, 0.15f, 0.15f, 0.8f)
                    : new Color(0.2f, 0.2f, 0.2f);
                recordItem.style.paddingLeft = 10;
                recordItem.style.paddingRight = 10;
                recordItem.style.paddingTop = 5;
                recordItem.style.paddingBottom = 5;

                var header = new VisualElement();
                header.style.flexDirection = FlexDirection.Row;
                header.style.justifyContent = Justify.SpaceBetween;
                header.style.marginBottom = 5;
                recordItem.Add(header);

                var deleteBtn = new Button(() =>
                {
                    if (target.TargetObject == null)
                    {
                        Undo.RecordObject(currentController, "Delete Modifier Record");
                        currentController.DeleteModifierRecord(curIndex);
                        RefreshUI();
                        EditorUtility.SetDirty(currentController);
                        return;
                    }

                    if (EditorUtility.DisplayDialog("确认删除",
                            $"确定要删除 {target.TargetObject.name} 修改器吗？",
                            "确定", "取消"))
                    {
                        Undo.RecordObject(currentController, "Delete Modifier Record");
                        currentController.DeleteModifierRecord(curIndex);
                        RefreshUI();
                        EditorUtility.SetDirty(currentController);
                    }
                }) { text = "×" };
                deleteBtn.style.width = 20;
                deleteBtn.style.marginRight = 5;
                deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                header.Add(deleteBtn);

                var objectField = new ObjectField();
                objectField.value = target.TargetObject;
                objectField.objectType = typeof(Object);
                objectField.SetEnabled(true);
                objectField.style.flexGrow = 1;
                objectField.RegisterValueChangedCallback(evt =>
                {
                    Object newTarget = evt.newValue;
                    if (newTarget is GameObject go)
                    {
                        Type requiredType = target.ModifierType.GetTargetType();
                        if (requiredType != null && !typeof(GameObject).IsAssignableFrom(requiredType))
                        {
                            var component = go.GetComponent(requiredType);
                            if (component != null)
                                newTarget = component;
                        }
                    }
                    Undo.RecordObject(currentController, "Change Modifier Target");
                    target.TargetObject = newTarget;
                    EditorUtility.SetDirty(currentController);
                    RefreshUI();
                });
                header.Add(objectField);

                // 添加修改器的值编辑字段，并确保在值更改时标记对象为已修改
                // 创建一个可以捕获值改变的容器
                var curRecord = recordGroup.GetCurrentRecord(currentController);
                for (int j = 0; j < curRecord.Modifiers.Count; j++)
                {
                    var modifier = curRecord.Modifiers[j];
                    var modifierType = modifier.ModifierType;
                    var newItem = AddModifierRecordElement(modifier, curRecord, target);
                    var delModifierBtn = new Button(() =>
                    {
                        Undo.RecordObject(currentController, "Remove Modifier");
                        recordGroup.RemoveModifier(modifierType);
                        EditorUtility.SetDirty(currentController);
                        RefreshUI();
                    });
                    newItem.style.paddingLeft = 20;
                    delModifierBtn.text = "×";
                    delModifierBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                    delModifierBtn.style.width = 15;
                    delModifierBtn.style.height = 15;
                    delModifierBtn.style.position = Position.Absolute;
                    delModifierBtn.style.left = 0;
                    newItem.Add(delModifierBtn);
                    recordItem.Add(newItem);
                }

                var addBtn = new Button(() =>
                {
                    var modifierTypes = GetSupportedModifierTypes(recordGroup.Target.TargetObject.GetType());
                    if (modifierTypes.Count > 0)
                    {
                        GenericMenu menu = new GenericMenu();
                        foreach (var modifierType in modifierTypes)
                        {
                            menu.AddItem(
                                new GUIContent($"{modifierType.GetChineseName()}"),
                                false,
                                () => AddGroupModifier(recordGroup, modifierType)
                            );
                        }

                        menu.ShowAsContext();
                    }
                    else
                    {
                        Debug.LogWarning($"No supported modifier types found");
                    }
                }){text = "+新修改器"};
                recordItem.Add(addBtn);

                var stateContainer = new VisualElement();
                stateContainer.style.flexGrow = 1;
                stateContainer.style.flexDirection = FlexDirection.Row;
                stateContainer.style.flexWrap = Wrap.Wrap;
                stateContainer.style.alignItems = Align.FlexStart;
                for (int j = 0; j < currentController.StateGroups.Count; j++)
                {
                    StateGroup stateGroup = currentController.StateGroups[j];
                    var toggle = new Toggle(stateGroup.GetShowName(true));
                    toggle.labelElement.style.flexShrink = 1;
                    toggle.labelElement.style.minWidth = 0;
                    toggle.SetValueWithoutNotify(recordGroup.ContainStateGroup(stateGroup.Name));
                    toggle.RegisterValueChangedCallback(evt =>
                    {
                        if (stateGroup.States.Count < 1)
                        {
                            toggle.SetValueWithoutNotify(false);
                            ShowNotification(new GUIContent($"请先给状态组{stateGroup.Name}添加状态"));
                            return;
                        }

                        var allGroup = recordGroup.GetAllStateGroup();
                        if (allGroup.Count >= 3)
                        {
                            toggle.SetValueWithoutNotify(false);
                            ShowNotification(new GUIContent($"控制的状态组已经有{allGroup.Count}个了！再加就过于复杂了！"));
                            return;
                        }

                        Undo.RecordObject(currentController, evt.newValue ? "Add State Group to Modifier" : "Remove State Group from Modifier");

                        if (evt.newValue)
                        {
                            recordGroup.AddStateGroup(stateGroup);
                        }
                        else
                        {
                            recordGroup.RemoveStateGroup(stateGroup.Name);
                        }
                        RefreshUI();
                        EditorUtility.SetDirty(currentController);
                    });
                    stateContainer.Add(toggle);
                }

                recordItem.Add(stateContainer);
                modifierContainer.Add(recordItem);
            }
        }


        /// <summary>
        /// 添加Modifier分组。
        /// </summary>
        private void AddModifierGroup(Object targetObject, ModifierTypeEnum modifierType)
        {
            var modifierTarget = new ModifierTarget
            {
                TargetObject = targetObject,
                ModifierType = modifierType
            };

            Undo.RecordObject(currentController, "Add Modifier Group");
            currentController.AddModifierGroup(modifierTarget, modifierType);

            // 将StateController对象标记为脏状态
            EditorUtility.SetDirty(currentController);

            // 如果是Prefab实例，确保记录修改
            if (PrefabUtility.IsPartOfPrefabInstance(currentController))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
            }

            // 先刷新UI
            RefreshUI();
        }

        void AddGroupModifier(ModifierRecordGroup group, ModifierTypeEnum modifierTypeEnum)
        {
            Undo.RecordObject(currentController, "Add Modifier");
            group.AddModifier(modifierTypeEnum);

            // 将StateController对象标记为脏状态
            EditorUtility.SetDirty(currentController);

            // 如果是Prefab实例，确保记录修改
            if (PrefabUtility.IsPartOfPrefabInstance(currentController))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
            }

            // 先刷新UI
            RefreshUI();
        }

        /// <summary>
        /// 添加ModifierRecordElement。
        /// </summary>
        private VisualElement AddModifierRecordElement(BaseModifier modifier, ModifierRecord curRecord, ModifierTarget curTarget)
        {
            var fieldContainer = new VisualElement();
            // 为所有子控件添加简化的值变化事件处理
            Action valueChangedHandler = () =>
            {
                Undo.RecordObject(currentController, "Change Modifier Value");
                curRecord.Apply(curTarget);
                EditorUtility.SetDirty(currentController);

                // 如果是Prefab实例，确保记录修改
                if (PrefabUtility.IsPartOfPrefabInstance(currentController))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
                }
            };
            // 将字段添加到容器
            modifier.AddField(fieldContainer, valueChangedHandler, curTarget);
            return fieldContainer;
        }

        /// <summary>
        /// 创建分组项。
        /// </summary>
        private VisualElement CreateGroupItem(StateGroup group, int index)
        {
            var problematicNames = StateControllerCheckUtil.GetProblematicStateGroupNames(currentController);
            var boundNames = StateControllerCheckUtil.GetBoundStateGroupNames(currentController);
            bool hasIssue = problematicNames.Contains(group.Name);
            bool unbound = !boundNames.Contains(group.Name);

            var groupItem = new VisualElement();
            groupItem.style.marginBottom = 5;
            groupItem.style.alignItems = Align.Center;
            if (hasIssue)
                groupItem.style.backgroundColor = new StyleColor(new Color(0.6f, 0.15f, 0.15f, 0.8f));
            else if (unbound)
                groupItem.style.backgroundColor = new StyleColor(new Color(0.6f, 0.55f, 0.1f, 0.8f));
            else
                groupItem.style.backgroundColor = new StyleColor(new Color(0.3f, 0.3f, 0.3f, 0.3f));

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.flexGrow = 1;
            groupItem.Add(container);
            bool isEditing = editingGroupNames.ContainsKey(index);

            if (isEditing)
            {
                var textField = editingGroupNames[index];
                container.Add(textField);
                container.Add(editingGroupNotes[index]);
                var confirmBtn = new Button(() =>
                {
                    string newNote =  editingGroupNotes[index].value;
                    string newName = textField.text;
                    editingGroupNames.Remove(index);
                    editingGroupNotes.Remove(index);

                    bool noteChanged = group.Note != newNote;
                    bool nameChanged = group.Name != newName;

                    if (noteChanged)
                    {
                        Undo.RecordObject(currentController, "Change State Group Note");
                        group.Note = newNote;
                        EditorUtility.SetDirty(currentController);
                    }

                    if (group.Name == newName)
                    {
                        RefreshUI();
                        return;
                    }
                    if(currentController.IsStateGroupNameRepeat(newName))
                    {
                        ShowNotification(new GUIContent($"状态组名称重复"));
                        RefreshUI();
                        return;
                    }
                    Undo.RecordObject(currentController, "Rename State Group");
                    currentController.ChangeStateGroupName(group, newName);
                    EditorUtility.SetDirty(currentController);
                    RefreshUI();
                }) { text = "✓" };
                container.Add(confirmBtn);
            }
            else
            {
                string btnName = group.GetShowName();
                bool isBuiltIn = Enum.TryParse<BuiltInStateEnum>(group.Name, out BuiltInStateEnum builtInState);
                
                var button = new Button(() => {
                    // 多选逻辑
                    if (selectedGroupNames.Contains(group.Name))
                        selectedGroupNames.Remove(group.Name);
                    else
                        selectedGroupNames.Add(group.Name);
                    RefreshUI();
                });
                button.text = btnName;
                button.style.flexGrow = 1;
                button.style.height = 30;
                button.style.fontSize = 13;
                // 高亮选中
                if (selectedGroupNames.Contains(group.Name))
                    button.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
                container.Add(button);

                if (!isBuiltIn)
                {
                    var editBtn = new Button(() => {
                        var textField = new TextField { value = group.Name };
                        textField.style.flexGrow = 1;
                        textField.style.height = 30;
                        editingGroupNames[index] = textField;
                        var textNoteField = new TextField { value = group.Note };
                        textNoteField.style.flexGrow = 1;
                        textNoteField.style.height = 30;
                        editingGroupNotes[index] = textNoteField;
                        RefreshUI();
                    }) { text = "编辑" };
                    container.Add(editBtn);
                }

                var copyBtn = new Button(() => CopyStateGroup(group)) { text = "复制" };
                container.Add(copyBtn);

                var deleteBtn = new Button(() => {
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除状态组 {group.Name} 吗？", "确定", "取消"))
                    {
                        Undo.RecordObject(currentController, "Delete State Group");
                        currentController.RemoveStateGroup(index);
                        EditorUtility.SetDirty(currentController);
                        RefreshUI();
                    }
                }) { text = "×" };
                deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                container.Add(deleteBtn);
            }

            var stateContainer = new VisualElement();
            stateContainer.style.flexDirection = isEditing ? FlexDirection.Column : FlexDirection.Row;
            stateContainer.style.flexGrow = 1;
            stateContainer.style.width = Length.Percent(100);
            stateContainer.style.flexWrap = Wrap.Wrap;
            stateContainer.style.paddingLeft = 5;
            stateContainer.style.paddingRight = 5;
            
            for (int i = 0; i < group.States.Count; i++)
            {
                var state = group.States[i];
                var stateItem = CreateStateItem(group, state, i, isEditing);
                stateContainer.Add(stateItem);
            }

            if (isEditing)
            {
                Button addBtn = new Button(() => {
                    AddNewState(group, $"newState{group.States.Count}");
                }) { text = "+新状态" };
                stateContainer.Add(addBtn);
                addBtn.style.backgroundColor = new Color(0.2f, 0.5f, 0.3f);
            }
            
            groupItem.Add(stateContainer);

            return groupItem;
        }

        /// <summary>
        /// 创建状态项。
        /// </summary>
        private VisualElement CreateStateItem(StateGroup stateGroup, State state, int index, bool isEditing)
        {
            var stateItem = new VisualElement();
            stateItem.style.height = 30;
            stateItem.style.flexDirection = FlexDirection.Row;
            stateItem.style.flexShrink = 1;
            stateItem.style.flexGrow = isEditing ? 1 : 0;

            if (isEditing)
            {
                var deleteBtn = new Button(() => {
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除状态 {state.Name} 吗？", "确定", "取消"))
                    {
                        Undo.RecordObject(currentController, "Delete State");
                        currentController.RemoveState(stateGroup, index);
                        EditorUtility.SetDirty(currentController);
                        RefreshUI();
                    }
                }) { text = "×" };
                deleteBtn.style.width = 20;
                deleteBtn.style.height = 20;
                deleteBtn.style.marginLeft = 5;
                deleteBtn.style.alignSelf = Align.Center;
                deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                stateItem.Add(deleteBtn);
                
                var textField = new TextField(index.ToString());
                textField.SetValueWithoutNotify(state.Name);
                textField.RegisterValueChangedCallback(e =>
                {
                    if(currentController.IsStateNameRepeat(stateGroup, textField.value))
                    {
                        ShowNotification(new GUIContent($"状态名称重复"));
                        textField.SetValueWithoutNotify(state.Name);
                        return;
                    }
                    Undo.RecordObject(currentController, "Rename State");
                    currentController.ChangeStateName(stateGroup, state, textField.value);
                    EditorUtility.SetDirty(currentController);
                });
                textField.style.flexGrow = 1;
                textField.labelElement.style.flexShrink = 1;
                textField.labelElement.style.minWidth = 0;
                textField.labelElement.style.alignSelf = Align.Center;
                stateItem.Add(textField);
                
                var textNoteField = new TextField();
                textNoteField.style.flexGrow = 1;
                textNoteField.SetValueWithoutNotify(state.Note);
                textNoteField.RegisterValueChangedCallback(e =>
                {
                    Undo.RecordObject(currentController, "Change State Note");
                    state.Note = e.newValue;
                    EditorUtility.SetDirty(currentController);
                });
                stateItem.Add(textNoteField);
            }
            else
            {
                var button = new Button(() => {
                    Undo.RecordObject(currentController, "Change State");
                    currentController.ChangeStateByName(stateGroup.Name,state.Name);

                    EditorUtility.SetDirty(currentController);
                    
                    // 检查是否在Prefab模式下
                    bool isPrefabMode = false;
                    #if UNITY_EDITOR
                    isPrefabMode = currentController.IsPrefabMode();
                    #endif
                    
                    // 在prefab模式下强制更新
                    if (isPrefabMode)
                    {
                        try {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
                            // 强制刷新编辑器
                            EditorApplication.QueuePlayerLoopUpdate();
                            SceneView.RepaintAll();
                            Debug.Log($"在Prefab模式下切换到状态: {state.Name}");
                        }
                        catch (Exception e) {
                            Debug.LogWarning($"状态切换更新时出错: {e.Message}");
                        }
                    }
                    
                    // 刷新UI显示
                    RefreshUI();
                    
                    // 立即重绘编辑器窗口
                    Repaint();
                });
                button.text = state.GetShowName();
                if (state == stateGroup.CurState)
                {
                    button.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
                }
                stateItem.Add(button);
            }

            return stateItem;
        }

        /// <summary>
        /// 添加New分组。
        /// </summary>
        private void AddNewGroup(string groupName)
        {
            if (string.IsNullOrEmpty(groupName))
            {
                groupName = $"State_{currentController.StateGroups.Count + 1}";
            }

            if(currentController.IsStateGroupNameRepeat(groupName))
            {
                ShowNotification(new GUIContent($"状态组名称重复"));
                newGroupInput.value = "";
                RefreshUI();
                return;
            }
            Undo.RecordObject(currentController, "Add State Group");
            currentController.AddStateGroup(groupName);
            EditorUtility.SetDirty(currentController);
            newGroupInput.value = "";
            RefreshUI();
        }
        
        /// <summary>
        /// 添加构建结果In分组。
        /// </summary>
        private void AddBuiltInGroup(string name)
        {
            if (Enum.TryParse(name, out BuiltInStateEnum builtInState))
            {
                if(currentController.IsStateGroupNameRepeat(builtInState.ToString()))
                {
                    ShowNotification(new GUIContent($"状态组名称重复"));
                    newGroupInput.value = "";
                    RefreshUI();
                    return;
                }
                Undo.RecordObject(currentController, "Add Built-in State Group");
                currentController.AddBuiltInGroup(builtInState);
                EditorUtility.SetDirty(currentController);
                newGroupInput.value = "";
                RefreshUI();
            }
        }

        /// <summary>
        /// 添加New状态。
        /// </summary>
        private void AddNewState(StateGroup stateGroup ,string stateName)
        {
            if (string.IsNullOrEmpty(stateName))
            {
                stateName = $"状态_{stateGroup.States.Count + 1}";
            }

            Undo.RecordObject(currentController, "Add State");
            currentController.AddState(stateGroup, stateName);
            EditorUtility.SetDirty(currentController);

            RefreshUI();
        }

        /// <summary>
        /// 响应DragUpdated事件。
        /// </summary>
        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            evt.StopPropagation();
        }

        /// <summary>
        /// 响应DragPerform事件。
        /// </summary>
        private void OnDragPerform(DragPerformEvent evt)
        {
            Object draggedObject = DragAndDrop.objectReferences[0];
            if (draggedObject == null) return;

            // 如果拖入的是GameObject，检查其所有组件
            GenericMenu menu;
            if (draggedObject is GameObject go)
            {
                menu = new GenericMenu();
                var components = go.GetComponents<Component>();
                
                foreach (var component in components)
                {
                    if (component == null) continue;
                    
                    var modifierTypes = GetSupportedModifierTypes(component.GetType());
                    foreach (var modifierType in modifierTypes)
                    {
                        menu.AddItem(
                            new GUIContent($"{component.GetType().Name}/{modifierType.GetChineseName()}"),
                            false,
                            () => AddModifierGroup(component, modifierType)
                        );
                    }
                }
                
                var goModifierTypes = GetSupportedModifierTypes(go.GetType());
                foreach (var modifierType in goModifierTypes)
                {
                    menu.AddItem(
                        new GUIContent($"{go.GetType().Name}/{modifierType.GetChineseName()}"),
                        false,
                        () => AddModifierGroup(go, modifierType)
                    );
                }

                if (menu.GetItemCount() > 0)
                {
                    menu.ShowAsContext();
                }
                else
                {
                    Debug.LogWarning($"No supported modifier types found for any component on {go.name}");
                }
            }
            else
            {
                // 如果拖入的是组件，直接检查该组件
                var modifierTypes = GetSupportedModifierTypes(draggedObject.GetType());
                if (modifierTypes.Count > 0)
                {
                    menu = new GenericMenu();
                    foreach (var modifierType in modifierTypes)
                    {
                        menu.AddItem(
                            new GUIContent($"{modifierType.GetChineseName()}"),
                            false,
                            () => AddModifierGroup(draggedObject, modifierType)
                        );
                    }
                    menu.ShowAsContext();
                }
                else
                {
                    Debug.LogWarning($"No supported modifier types found for {draggedObject.name}");
                }
            }

            evt.StopPropagation();
        }

        /// <summary>
        /// 获取SupportedModifier类型。
        /// </summary>
        private List<ModifierTypeEnum> GetSupportedModifierTypes(Type objectType)
        {
            var supportedTypes = new List<ModifierTypeEnum>();
            var enumValues = System.Enum.GetValues(typeof(ModifierTypeEnum));
            
            foreach (ModifierTypeEnum value in enumValues)
            {
                var targetType = value.GetTargetType();
                if (targetType == null) continue;
                
                if (targetType.IsAssignableFrom(objectType))
                {
                    supportedTypes.Add(value);
                }
            }

            return supportedTypes.Distinct().ToList();
        }

        /// <summary>
        /// 执行Copy状态Group相关逻辑。
        /// </summary>
        private void CopyStateGroup(StateGroup group)
        {
            var clipboard = new StateGroupClipboard
            {
                GroupName = group.Name,
                GroupNote = group.Note
            };

            foreach (var state in group.States)
            {
                clipboard.States.Add(new StateClipboard { Name = state.Name, Note = state.Note });
            }

            s_clipboard = clipboard;
            ShowNotification(new GUIContent($"已复制状态组: {group.Name}"));
        }

        /// <summary>
        /// 执行Paste状态Group相关逻辑。
        /// </summary>
        private void PasteStateGroup()
        {
            if (s_clipboard == null)
            {
                ShowNotification(new GUIContent("剪贴板为空，请先复制一个状态组"));
                return;
            }

            if (currentController == null)
                return;

            Undo.RecordObject(currentController, "Paste State Group");

            string baseName = s_clipboard.GroupName;
            string finalName = baseName;
            int suffix = 1;
            while (currentController.IsStateGroupNameRepeat(finalName))
            {
                finalName = $"{baseName}_{suffix}";
                suffix++;
            }

            var newGroup = new StateGroup
            {
                Name = finalName,
                Note = s_clipboard.GroupNote,
                States = new List<State>()
            };

            foreach (var stateClip in s_clipboard.States)
            {
                newGroup.States.Add(new State { Name = stateClip.Name, Note = stateClip.Note });
            }

            if (newGroup.States.Count > 0)
                newGroup.CurState = newGroup.States[0];

            currentController.StateGroups.Add(newGroup);

            EditorUtility.SetDirty(currentController);
            RefreshUI();
            ShowNotification(new GUIContent($"已粘贴状态组: {finalName}"));
        }

        /// <summary>
        /// 释放持有的资源并解除事件订阅。
        /// </summary>
        private void OnDestroy()
        {
            // 取消注册Selection变化事件
            Selection.selectionChanged -= OnSelectionChanged;

            // 取消注册编辑器更新事件
            EditorApplication.update -= OnEditorUpdate;

            // 取消注册Undo事件
            Undo.undoRedoPerformed -= OnUndoRedo;

            // 清除可能的循环引用
            if (groupList != null)
            {
                groupList.Clear();
                groupList = null;
            }
            
            
            if (modifierContainer != null)
            {
                modifierContainer.Clear();
                modifierContainer = null;
            }
            
            // 清除事件注册
            if (dragArea != null)
            {
                dragArea.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
                dragArea.UnregisterCallback<DragPerformEvent>(OnDragPerform);
                dragArea = null;
            }
            
            editingGroupNames.Clear();
            editingGroupNotes.Clear();
        }
        
        /// <summary>
        /// 在组件停用时解除运行时关联。
        /// </summary>
        private void OnDisable()
        {
            // 在禁用窗口时也清理资源
            editingGroupNames.Clear();
            editingGroupNotes.Clear();
        }

        // 添加保存方法
        /// <summary>
        /// 保存当前项Controller。
        /// </summary>
        private void SaveCurrentController()
        {
            if (currentController == null)
                return;

            // 标记当前控制器为脏状态
            EditorUtility.SetDirty(currentController);

            // 如果是prefab实例，记录修改但不自动应用
            if (PrefabUtility.IsPartOfPrefabInstance(currentController))
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
            }

            // 保存场景或资源
            AssetDatabase.SaveAssetIfDirty(currentController);
        }
        
        /// <summary>
        /// 添加Toolbar。
        /// </summary>
        private void AddToolbar()
        {
            var toolbar = new Toolbar();
            toolbar.style.marginBottom = 10;
            rootVisualElement.Insert(1, toolbar); // 插入到控制器显示之后
            
            var saveButton = new ToolbarButton(() => {
                SaveCurrentController();
                Debug.Log("已保存状态控制器");
            }) { text = "强制保存" };
            
            toolbar.Add(saveButton);
        }
    }
} 