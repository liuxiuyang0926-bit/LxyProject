using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(StateControlBindValueCollection))]
    public class StateControlBindValueCollectionDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;
        private const float VERTICAL_GAP = 5;

        private ReorderableList list;

        private ReorderableList GetList(SerializedProperty property)
        {
            if (list == null)
            {
                list = new ReorderableList(property.serializedObject, property, true, true, true, true);
                list.elementHeight = 21;
                list.drawElementCallback = DrawElement;
                list.drawHeaderCallback = DrawHeader;
                list.onAddDropdownCallback = OnAddElement;
                list.onRemoveCallback = OnRemoveElement;
            }
            else
            {
                list.serializedProperty = property;
            }
            return list;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
                return 40;

            float height = base.GetPropertyHeight(property, label) + 60;
            var bindValues = property.FindPropertyRelative("binds");
            for (int i = 0; i < bindValues.arraySize; i++)
                height += EditorGUI.GetPropertyHeight(bindValues.GetArrayElementAtIndex(i)) + VERTICAL_GAP;

            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.HelpBox(position,"不可多选", MessageType.Warning);
                return;
            }

            var list = GetList(property.FindPropertyRelative("binds"));
            list.DoList(position);
        }

        private void OnAddElement(Rect rect, ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            int index = bindValues.arraySize > 0 ? bindValues.arraySize : 0;
            bindValues.arraySize++;
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        private void OnRemoveElement(ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            AskRemoveVariable(bindValues, list.index);
        }

        private void DrawHeader(Rect rect)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width - 240, rect.height);
            Rect clearButtonRect = new Rect(rect.x + rect.width - 230, rect.y, 100, rect.height);
            Rect autoBindButtonRect = new Rect(rect.x + rect.width - 120, rect.y, 100, rect.height);
           
            GUI.Label(labelRect, "状态控制绑定");
            if (GUI.Button(clearButtonRect, "清空所有绑定"))
            {
                ClearAllBindings();
            }
            if (GUI.Button(autoBindButtonRect, "自动绑定"))
            {
                AutoBindStateControllers();
            }
        }

        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var bindValues = list.serializedProperty;
            if (index < 0 || index >= bindValues.arraySize)
                return;

            var variable = bindValues.GetArrayElementAtIndex(index);

            float x = rect.x;
            float y = rect.y + 2;
            float width = rect.width - 40;
            float height = rect.height;

            Rect variableRect = new Rect(x, y, width, height);
            EditorGUI.PropertyField(variableRect, variable, GUIContent.none);

            var buttonLeftRect = new Rect(variableRect.xMax + HORIZONTAL_GAP, y - 1, 18, 18);
            var buttonRightRect = new Rect(buttonLeftRect.xMax, y - 1, 18, 18);

            if (GUI.Button(buttonLeftRect, new GUIContent("+"), EditorStyles.miniButtonLeft))
            {
                DuplicateVariable(bindValues, index);
            }
            if (GUI.Button(buttonRightRect, new GUIContent("-"), EditorStyles.miniButtonRight))
            {
                AskRemoveVariable(bindValues, index);
            }
        }

        protected virtual void AskRemoveVariable(SerializedProperty bindValues, int index)
        {
            if (EditorUtility.DisplayDialog("删除绑定", "确定要删除这个绑定吗？", "确定", "取消"))
            {
                RemoveVariable(bindValues, index);
            }
        }

        protected virtual void RemoveVariable(SerializedProperty bindValues, int index)
        {
            bindValues.DeleteArrayElementAtIndex(index);
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        protected virtual void DuplicateVariable(SerializedProperty bindValues, int index)
        {
            bindValues.arraySize++;
            var source = bindValues.GetArrayElementAtIndex(index);
            var target = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);

            target.FindPropertyRelative("name").stringValue = source.FindPropertyRelative("name").stringValue + "_Copy";
            target.FindPropertyRelative("stateController").objectReferenceValue = source.FindPropertyRelative("stateController").objectReferenceValue;
            target.FindPropertyRelative("stateGroupName").stringValue = source.FindPropertyRelative("stateGroupName").stringValue;

            bindValues.serializedObject.ApplyModifiedProperties();
        }

        private void ClearAllBindings()
        {
            var bindValues = list.serializedProperty;
            bindValues.arraySize = 0;
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        private void AutoBindStateControllers()
        {
            var bindValues = list.serializedProperty;
            var targetObject = bindValues.serializedObject.targetObject as MonoBehaviour;
            if (targetObject == null) return;

            // 收集已存在的绑定名称
            var existingNames = new HashSet<string>();
            for (int i = 0; i < bindValues.arraySize; i++)
            {
                var variable = bindValues.GetArrayElementAtIndex(i);
                var name = variable.FindPropertyRelative("name").stringValue;
                if (!string.IsNullOrEmpty(name))
                {
                    existingNames.Add(name);
                }
            }

            // 获取ObjectBinder组件
            ObjectBinder objectBinder = targetObject.GetComponent<ObjectBinder>();

            // 收集所有StateController和StateGroup的组合
            var stateControlBindings = new Dictionary<string, StateControlBinding>();
            FindStateControllers(targetObject.transform, stateControlBindings, objectBinder, targetObject.transform);

            // 添加新的绑定
            bindValues.serializedObject.Update();
            foreach (var kvp in stateControlBindings)
            {
                string bindingName = kvp.Key;
                if (existingNames.Contains(bindingName)) continue;

                StateControlBinding binding = kvp.Value;

                bindValues.arraySize++;
                var variableProperty = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);

                variableProperty.FindPropertyRelative("name").stringValue = bindingName;
                variableProperty.FindPropertyRelative("stateController").objectReferenceValue = binding.Controller;
                variableProperty.FindPropertyRelative("stateGroupName").stringValue = binding.StateGroupName;
            }
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        private void FindStateControllers(Transform currentTransform, Dictionary<string, StateControlBinding> stateControlBindings, ObjectBinder curObjectBinder, Transform rootTransform)
        {
            // 检查当前节点是否是Prefab资源的一部分
            bool isPrefabAsset = UnityEditor.PrefabUtility.IsPartOfPrefabAsset(currentTransform.gameObject);

            // 只处理非Prefab资源的节点
            if (!isPrefabAsset)
            {
                // 查找当前节点上的StateController组件
                var stateController = currentTransform.GetComponent<StateControl.Runtime.StateController>();
                if (stateController != null && stateController.StateGroups != null)
                {
                    // 判断是否是根节点
                    bool isRootNode = (currentTransform == rootTransform);

                    // 遍历所有StateGroup
                    foreach (var stateGroup in stateController.StateGroups)
                    {
                        if (stateGroup == null || string.IsNullOrEmpty(stateGroup.Name))
                            continue;

                        // 跳过以_开头的内置状态组
                        if (stateGroup.Name.StartsWith("_"))
                            continue;

                        // 生成绑定名称
                        string bindingName;
                        if (isRootNode)
                        {
                            // 根节点：SG_状态组名
                            bindingName = $"SG_{stateGroup.Name}";
                        }
                        else
                        {
                            // 子节点：SG_节点名_状态组名
                            string nodeName = currentTransform.name;
                            // 去掉 # 或 @ 开头的后缀
                            int separatorIndex = nodeName.IndexOfAny(new char[] { '#', '@' });
                            if (separatorIndex > 0)
                            {
                                nodeName = nodeName.Substring(0, separatorIndex);
                            }
                            bindingName = $"SG_{nodeName}_{stateGroup.Name}";
                        }

                        // 如果名称已存在，跳过
                        if (stateControlBindings.ContainsKey(bindingName))
                            continue;

                        // 添加到字典
                        stateControlBindings.Add(bindingName, new StateControlBinding
                        {
                            Controller = stateController,
                            StateGroupName = stateGroup.Name
                        });
                    }
                }
            }

            // 递归查找子节点
            foreach (Transform child in currentTransform)
            {
                // 检查子节点是否有ObjectBinder组件，如果有且不是当前的ObjectBinder，则跳过
                child.TryGetComponent<ObjectBinder>(out var objBinder);
                if (objBinder != null && objBinder != curObjectBinder)
                {
                    continue;
                }

                FindStateControllers(child, stateControlBindings, curObjectBinder, rootTransform);
            }
        }

        private struct StateControlBinding
        {
            public StateControl.Runtime.StateController Controller;
            public string StateGroupName;
        }
    }
}
