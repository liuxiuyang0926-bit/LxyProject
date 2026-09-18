using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StateControl.Runtime;
using Object = UnityEngine.Object;

namespace StateControl.Editor
{
    public class ContinuousStateEditor : EditorWindow
    {
        private static StateController currentController;
        bool hasSelectedStateController = false;
        private int selectedGroupIndex = -1;
        private Dictionary<int, TextField> editingGroupNames = new Dictionary<int, TextField>();
        private Dictionary<string, ContinuousModifierTypeEnum> modifierTypeMap = new ();
        private List<string> modifierTypeNames;
        private ContinuousModifierTypeEnum currentModifierType;

        // UI元素
        private ScrollView groupList;
        private TextField newGroupInput;
        private GroupBox modifierToolContainer;
        private ScrollView modifierContainer;

        /// <summary>
        /// 执行显示窗口相关逻辑。
        /// </summary>
        [MenuItem("Window/连续状态编辑器")]
        public static void ShowWindow()
        {
            var window = GetWindow<ContinuousStateEditor>();
            window.titleContent = new GUIContent("连续状态编辑器");
            window.Show();
        }

        /// <summary>
        /// 创建GUI。
        /// </summary>
        private void CreateGUI()
        {
            modifierTypeMap.Clear();
            foreach (ContinuousModifierTypeEnum enumType in Enum.GetValues(typeof(ContinuousModifierTypeEnum)))
            { 
                string enumName = ContinuousModifierTypeAttribute.GetTypeName(enumType);
                if (enumName == null) continue;
                modifierTypeMap.TryAdd(enumName, enumType);
            }
            modifierTypeNames = modifierTypeMap.Keys.ToList();
            
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
            var splitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
            mainContent.Add(splitView);

            // 创建左侧面板
            var leftPanel = new VisualElement();
            leftPanel.style.width = 200;
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
            foreach (var stateEnum in Enum.GetValues(typeof(BuiltInContinuousStateEnum)))
            {
                var enumType = (BuiltInContinuousStateEnum)stateEnum;
                string enumName = BuiltInContinuousStateEnumAttribute.GetName(enumType);
                if (enumName != null)
                {
                    builtInStateNames.Add(stateEnum.ToString());
                }
            }

            Func<string, string> displayNameFunc = value =>
            {
                return Enum.TryParse(value, out BuiltInContinuousStateEnum enumType)
                    ? BuiltInContinuousStateEnumAttribute.GetName(enumType)
                    : value;
            };
            
            var builtInStateDropDown = new DropdownField(builtInStateNames, 0, displayNameFunc, displayNameFunc);
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
            }) { text = "添加内置状态组" };
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
            
            modifierToolContainer = new GroupBox("");
            rightPanel.Add(modifierToolContainer);

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
        /// 响应SelectionChanged事件。
        /// </summary>
        private void OnSelectionChanged()
        {
            if (Selection.activeGameObject != null)
            {
                var controller = Selection.activeGameObject.GetComponent<StateController>();
                if (controller != null)
                {
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
            selectedGroupIndex = -1;
            
            // 更新顶部控制器显示
            var controllerField = rootVisualElement.Q<ObjectField>();
            if (controllerField != null)
            {
                controllerField.value = null;
            }

            // 清空列表
            modifierContainer?.Clear();
            groupList?.Clear();
            
            // 禁用状态输入框和按钮
            UpdateUIEnabled();
            
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
        /// 更新UI启用状态。
        /// </summary>
        private void UpdateUIEnabled()
        {
            bool hasSelectedGroup = currentController?.ContinuousStateGroups != null
                                    && selectedGroupIndex >= 0
                                    && selectedGroupIndex < currentController.ContinuousStateGroups.Count;
                
            // 更新修改器标签的显示
            var modifierLabel = modifierContainer?.parent?.Q<Label>();
            if (modifierLabel != null)
                modifierLabel.style.display = hasSelectedGroup ? DisplayStyle.Flex : DisplayStyle.None;
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

            currentController.continuousStateGroups ??= new List<ContinuousStateGroup>();

            // 刷新状态组列表
            for (int i = 0; i < currentController.ContinuousStateGroups.Count; i++)
            {
                var group = currentController.ContinuousStateGroups[i];
                var groupItem = CreateGroupItem(group, i);
                groupList.Add(groupItem);
            }

            // 刷新选中的状态组
            if (selectedGroupIndex >= 0 && selectedGroupIndex < currentController.ContinuousStateGroups.Count)
            {
                var selectedGroup = currentController.ContinuousStateGroups[selectedGroupIndex];

                // 刷新修改器部分
                modifierContainer.Clear();
                modifierToolContainer.Clear();
                
                Slider progressSlider = new Slider("当前值",0,1);
                progressSlider.value = selectedGroup.CurrentValue;
                progressSlider.RegisterValueChangedCallback((evt) =>
                {
                    var selectedGroup = currentController.ContinuousStateGroups[selectedGroupIndex];
                    selectedGroup.Apply(evt.newValue);
                });
                modifierToolContainer.Add(progressSlider);
                
                GroupBox groupBox = new GroupBox("");
                groupBox.style.flexDirection = FlexDirection.Row;
                groupBox.style.flexGrow = 1;
                groupBox.style.borderTopWidth = 2;
                groupBox.style.borderTopColor = Color.gray;
                groupBox.style.borderLeftWidth = 2;
                groupBox.style.borderLeftColor = Color.gray;
                groupBox.style.borderRightWidth = 2;
                groupBox.style.borderRightColor = Color.gray;
                groupBox.style.borderBottomWidth = 2;
                groupBox.style.borderBottomColor = Color.gray;
                groupBox.style.borderBottomRightRadius = 2;
                groupBox.style.borderBottomLeftRadius = 2;
                groupBox.style.borderTopRightRadius = 2;
                groupBox.style.borderTopLeftRadius = 2;
                groupBox.style.paddingTop = 10;
                groupBox.style.paddingBottom = 10;
                groupBox.style.paddingLeft = 10;
                groupBox.style.paddingRight = 10;
                groupBox.style.marginTop = 10;
                groupBox.style.marginBottom = 10;
                groupBox.style.marginLeft = 10;
                groupBox.style.marginRight = 10;
                
                
                Label modifierLabel = new Label("增加修改器");
                modifierLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                groupBox.Add(modifierLabel);

                string curTypeName = null;
                foreach (var kv in modifierTypeMap)
                {
                    if (kv.Value == currentModifierType)
                    {
                        curTypeName = kv.Key;
                        break;
                    }
                }

                if (curTypeName == null)
                {
                    curTypeName = modifierTypeNames[0];
                    currentModifierType = modifierTypeMap[curTypeName];
                }
                DropdownField enumTypeDropdown = new DropdownField("", modifierTypeNames, curTypeName);
                enumTypeDropdown.RegisterValueChangedCallback((evt)=>
                {
                    if (modifierTypeMap.TryGetValue(evt.newValue, out var value))
                    {
                        currentModifierType = value;
                    }
                });
                
                
                groupBox.Add(enumTypeDropdown);
                
                Button addModifierButton = new Button(() =>
                { 
                    var selectedGroup = currentController.ContinuousStateGroups[selectedGroupIndex];
                    selectedGroup.Add(currentModifierType);
                    RefreshUI();
                });
                addModifierButton.text = "+";
                groupBox.Add(addModifierButton);
                modifierToolContainer.Add(groupBox);
                
                
                if (selectedGroup.Modifiers.Count > 0)
                {
                    UpdateModifierDisplay(selectedGroup);
                }
                else
                {
                    // 如果没有修改器目标，显示提示信息
                    var hintLabel = new Label("请添加修改器");
                    hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    hintLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                    hintLabel.style.fontSize = 14;
                    hintLabel.style.marginTop = 50;
                    modifierContainer.Add(hintLabel);
                }
            }
            else
            {
                var modifierHintLabel = new Label("请选择一个状态组");
                modifierHintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                modifierHintLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
                modifierHintLabel.style.fontSize = 14;
                modifierHintLabel.style.marginTop = 50;
                modifierContainer.Add(modifierHintLabel);
            }
        }

        /// <summary>
        /// 更新ModifierDisplay。
        /// </summary>
        private void UpdateModifierDisplay(ContinuousStateGroup group)
        {
            modifierContainer.Clear();
            
            int minCount = group.Modifiers.Count;
            
            for (int i = 0; i < minCount; i++)
            {
                var modifier = group.Modifiers[i];
                var curIndex = i; // 创建一个固定的索引值
                
                var modifierItem = new VisualElement();
                modifierItem.style.marginBottom = 10;
                modifierItem.style.marginRight = 10;
                modifierItem.style.width = new Length(48, LengthUnit.Percent);
                modifierItem.style.flexShrink = 0;
                modifierItem.style.flexGrow = 0;
                modifierItem.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);
                modifierItem.style.paddingLeft = 10;
                modifierItem.style.paddingRight = 10;
                modifierItem.style.paddingTop = 5;
                modifierItem.style.paddingBottom = 5;

                var header = new VisualElement();
                header.style.flexDirection = FlexDirection.Row;
                header.style.justifyContent = Justify.SpaceBetween;
                header.style.marginBottom = 5;
                modifierItem.Add(header);
                
                var deleteBtn = new Button(() => {
                    DeleteModifier(curIndex, group);
                }) { text = "×" };
                deleteBtn.style.width = 20;
                deleteBtn.style.marginRight = 5;
                deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                header.Add(deleteBtn);

                string typeName = ContinuousModifierTypeAttribute.GetTypeName(modifier.ModifierType);
                var typeLabel = new Label(typeName);
                header.Add(typeLabel);
                
                // 添加修改器的值编辑字段，并确保在值更改时标记对象为已修改
                // 创建一个可以捕获值改变的容器
                var fieldContainer = new VisualElement();
                fieldContainer.RegisterCallback<ChangeEvent<object>>(_ => {
                    // 确保任何子控件的值变化都会触发SetDirty
                    EditorUtility.SetDirty(currentController);
                    // 如果是Prefab实例，记录修改
                    if (PrefabUtility.IsPartOfPrefabInstance(currentController))
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
                    }
                });
                
                // 将字段添加到容器
                modifier.AddField(fieldContainer);
                
                // 为所有子控件添加简化的值变化事件处理
                Action<object> valueChangedHandler = _ => {
                    // 标记当前控制器为脏状态
                    EditorUtility.SetDirty(currentController);
                    
                    // 如果是Prefab实例，记录修改
                    if (PrefabUtility.IsPartOfPrefabInstance(currentController))
                    {
                        try {
                            // 记录所有相关对象的修改
                            PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
                            
                            // 强制刷新编辑器
                            EditorApplication.QueuePlayerLoopUpdate();
                            SceneView.RepaintAll();
                        }
                        catch (Exception e) {
                            Debug.LogWarning($"记录Prefab修改时出错: {e.Message}");
                        }
                    }
                    
                    // 立即保存修改
                    SaveCurrentController();
                };
                
                foreach (var child in fieldContainer.Children())
                {
                    if (child is BaseField<bool> boolField)
                    {
                        boolField.RegisterValueChangedCallback(evt => valueChangedHandler(evt.newValue));
                    }
                    else if (child is BaseField<int> intField)
                    {
                        intField.RegisterValueChangedCallback(evt => valueChangedHandler(evt.newValue));
                    }
                    else if (child is BaseField<float> floatField)
                    {
                        floatField.RegisterValueChangedCallback(evt => valueChangedHandler(evt.newValue));
                    }
                    else if (child is BaseField<string> stringField)
                    {
                        stringField.RegisterValueChangedCallback(evt => valueChangedHandler(evt.newValue));
                    }
                    else if (child is BaseField<Vector2> vector2Field)
                    {
                        vector2Field.RegisterValueChangedCallback(evt => valueChangedHandler(evt.newValue));
                    }
                    else if (child is BaseField<Vector3> vector3Field)
                    {
                        vector3Field.RegisterValueChangedCallback(evt => valueChangedHandler(evt.newValue));
                    }
                    else if (child is BaseField<Color> colorField)
                    {
                        colorField.RegisterValueChangedCallback(evt => valueChangedHandler(evt.newValue));
                    }
                }
                
                // 将容器添加到修改器项
                modifierItem.Add(fieldContainer);
                modifierContainer.Add(modifierItem);
            }
        }

        void DeleteModifier(int modifierIndex, ContinuousStateGroup group)
        {
            if (modifierIndex >= 0 && modifierIndex < group.Modifiers.Count)
            {
                group.Modifiers.RemoveAt(modifierIndex);
                RefreshUI();
                EditorUtility.SetDirty(currentController);
            }
        }

        /// <summary>
        /// 创建分组项。
        /// </summary>
        private VisualElement CreateGroupItem(ContinuousStateGroup group, int index)
        {
            var groupItem = new VisualElement();
            groupItem.style.marginBottom = 5;
            if (index == selectedGroupIndex)
            {
                groupItem.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f, 0.5f);
            }

            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            groupItem.Add(container);

            if (editingGroupNames.ContainsKey(index))
            {
                var textField = editingGroupNames[index];
                container.Add(textField);
                var confirmBtn = new Button(() => {
                    group.Name = textField.value;
                    editingGroupNames.Remove(index);
                    EditorUtility.SetDirty(currentController);
                    RefreshUI();
                }) { text = "✓" };
                container.Add(confirmBtn);
            }
            else
            {
                string btnName = group.GetShowName();
                bool isBuiltIn = ContinuousStateGroup.IsBuiltInContinuousStateEnum(group.Name, out _);
                
                var button = new Button(() => {
                    selectedGroupIndex = index;
                    RefreshUI();
                    UpdateUIEnabled();
                });
                button.text = btnName;
                button.style.flexGrow = 1;
                button.style.height = 30;
                button.style.fontSize = 13;
                if (index == selectedGroupIndex)
                {
                    button.style.backgroundColor = new Color(0.3f, 0.5f, 0.7f);
                }
                container.Add(button);

                if (!isBuiltIn)
                {
                    var editBtn = new Button(() => {
                        var textField = new TextField { value = group.Name };
                        textField.style.flexGrow = 1;
                        editingGroupNames[index] = textField;
                        RefreshUI();
                    }) { text = "..." };
                    container.Add(editBtn);
                }

                var deleteBtn = new Button(() => {
                    if (EditorUtility.DisplayDialog("确认删除", $"确定要删除连续状态组 {group.Name} 吗？", "确定", "取消"))
                    {
                        currentController.ContinuousStateGroups.RemoveAt(index);
                        if (selectedGroupIndex >= index)
                        {
                            selectedGroupIndex--;
                        }
                        RefreshUI();
                    }
                }) { text = "×" };
                deleteBtn.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                container.Add(deleteBtn);
            }

            return groupItem;
        } 

        /// <summary>
        /// 添加New分组。
        /// </summary>
        private void AddNewGroup(string name)
        {
            if (currentController == null)
            {
                ShowNotification(new GUIContent("请选择带有StateController组件的游戏对象"));
                RefreshUI();
                return;
            }

            currentController.continuousStateGroups ??= new List<ContinuousStateGroup>();

            if (string.IsNullOrEmpty(name))
            {
                name = $"State_{currentController.ContinuousStateGroups.Count + 1}";
            }

            var newGroup = new ContinuousStateGroup
            {
                Name = name,
                Modifiers = new List<IContinuousModifier>()
            };

            currentController.ContinuousStateGroups.Add(newGroup);
            newGroupInput.value = "";
            RefreshUI();
        } 
        
        /// <summary>
        /// 添加构建结果In分组。
        /// </summary>
        private void AddBuiltInGroup(string name)
        {
            if (currentController == null)
            {
                ShowNotification(new GUIContent("请选择带有StateController组件的游戏对象"));
                RefreshUI();
                return;
            }

            currentController.continuousStateGroups ??= new List<ContinuousStateGroup>();

            if (Enum.TryParse(name, out BuiltInContinuousStateEnum builtInState))
            {
                if (FindContinuousStateGroup(builtInState.ToString()) != null)
                {
                    ShowNotification(new GUIContent("连续状态组名称重复"));
                    newGroupInput.value = "";
                    RefreshUI();
                    return;
                }

                Undo.RecordObject(currentController, "Add Built-in Continuous State Group");
                currentController.AddBuiltInContinuousGroup(builtInState);
                EditorUtility.SetDirty(currentController);
                newGroupInput.value = "";
                RefreshUI();
            }
        }

        /// <summary>
        /// 查找Continuous状态分组。
        /// </summary>
        private ContinuousStateGroup FindContinuousStateGroup(string groupName)
        {
            if (currentController == null)
                return null;

            if (currentController.ContinuousStateGroups == null)
                return null;

            foreach (var group in currentController.ContinuousStateGroups)
            {
                if (group.Name == groupName)
                    return group;
            }

            return null;
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
            
            editingGroupNames.Clear(); 
        }
        
        /// <summary>
        /// 在组件停用时解除运行时关联。
        /// </summary>
        private void OnDisable()
        {
            // 在禁用窗口时也清理资源
            editingGroupNames.Clear(); 
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
            
            // 如果是prefab实例，尝试应用修改
            if (PrefabUtility.IsPartOfPrefabInstance(currentController))
            {
                try
                {
                    // 记录所有相关对象的修改
                    PrefabUtility.RecordPrefabInstancePropertyModifications(currentController);
                    
                    // 应用修改到prefab
                    PrefabUtility.ApplyPrefabInstance(currentController.gameObject, InteractionMode.AutomatedAction);
                    
                    // 强制刷新编辑器
                    EditorApplication.QueuePlayerLoopUpdate();
                    SceneView.RepaintAll();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"记录Prefab修改时出错: {e.Message}");
                }
            }
            
            // 保存场景或资源
            AssetDatabase.SaveAssetIfDirty(currentController);
            
            // 如果是prefab资源，确保保存
            if (PrefabUtility.IsPartOfAnyPrefab(currentController))
            {
                var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(currentController);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    try {
                        // 应用修改到prefab
                        PrefabUtility.ApplyPrefabInstance(currentController.gameObject, InteractionMode.AutomatedAction);
                        // 刷新prefab资源
                        AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
                    }
                    catch (Exception e) {
                        Debug.LogWarning($"应用Prefab修改时出错: {e.Message}");
                    }
                }
            }
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
            }) { text = "保存" };
            
            toolbar.Add(saveButton);
        }
    }
} 
