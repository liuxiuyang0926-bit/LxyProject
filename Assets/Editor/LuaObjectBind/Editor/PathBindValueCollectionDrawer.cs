using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System;
using System.Linq;

namespace LuaObjectBind.Editor
{ 
    [CustomPropertyDrawer(typeof(PathBindValueCollection))]
    public class PathBindValueCollectionDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;
        private const float VERTICAL_GAP = 5;

        private ReorderableList list;

        private ReorderableList GetList(SerializedProperty property)
        {
            if (list == null)
            {
                list = new ReorderableList(property.serializedObject, property, true, true, true, true);
                list.elementHeight = 25; // 增加高度以适应提示文本
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
            
            // 设置默认名称
            var newElement = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);
            var nameProperty = newElement.FindPropertyRelative("name");
            
            // 生成默认名称：path + 当前数量
            int pathCount = 1;
            for (int i = 0; i < bindValues.arraySize - 1; i++)
            {
                var element = bindValues.GetArrayElementAtIndex(i);
                var elementName = element.FindPropertyRelative("name").stringValue;
                if (elementName.StartsWith("Path"))
                {
                    pathCount++;
                }
            }
            
            nameProperty.stringValue = $"Path{pathCount}";
            
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        private void OnRemoveElement(ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            AskRemoveVariable(bindValues, list.index);
        }

        private void DrawHeader(Rect rect)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width - 120, rect.height);
            Rect clearButtonRect = new Rect(rect.x + rect.width - 120, rect.y, 100, rect.height);
            
            GUI.Label(labelRect, "路径绑定");
            if (GUI.Button(clearButtonRect, "清空所有绑定"))
            {
                ClearAllBindings();
            }
        }

        private void ClearAllBindings()
        {
            var bindValues = list.serializedProperty;
            bindValues.arraySize = 0;
            bindValues.serializedObject.ApplyModifiedProperties();
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
            target.FindPropertyRelative("path").stringValue = source.FindPropertyRelative("path").stringValue;
            
            bindValues.serializedObject.ApplyModifiedProperties();
        }
    }
} 