using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System;
using System.Linq;

namespace LuaObjectBind.Editor
{ 
    /// <summary>
    /// 静态文本绑定集合的自定义编辑器绘制器
    /// </summary>
    [CustomPropertyDrawer(typeof(StaticTextBindValueCollection))]
    public class StaticTextBindValueCollectionDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;
        private const float VERTICAL_GAP = 5;
        private const float TEXT_FIELD_HEIGHT = 60; // 文本输入框高度

        private ReorderableList list;

        /// <summary>
        /// 获取列表。
        /// </summary>
        private ReorderableList GetList(SerializedProperty property)
        {
            if (list == null)
            {
                list = new ReorderableList(property.serializedObject, property, true, true, true, true);
                list.elementHeight = TEXT_FIELD_HEIGHT + 25; // 增加高度以适应多行文本和标签
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

        /// <summary>
        /// 获取属性高度。
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
                return 40;

            float height = base.GetPropertyHeight(property, label) + 60;
            var bindValues = property.FindPropertyRelative("binds");
            for (int i = 0; i < bindValues.arraySize; i++)
                height += TEXT_FIELD_HEIGHT + 25 + VERTICAL_GAP;

            return height;
        }

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
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

        /// <summary>
        /// 响应加法Element事件。
        /// </summary>
        private void OnAddElement(Rect rect, ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            int index = bindValues.arraySize > 0 ? bindValues.arraySize : 0;
            bindValues.arraySize++;
            
            // 设置默认名称
            var newElement = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);
            var nameProperty = newElement.FindPropertyRelative("name");
            
            // 生成默认名称：Text + 当前数量
            int textCount = 1;
            for (int i = 0; i < bindValues.arraySize - 1; i++)
            {
                var element = bindValues.GetArrayElementAtIndex(i);
                var elementName = element.FindPropertyRelative("name").stringValue;
                if (elementName.StartsWith("Text"))
                {
                    textCount++;
                }
            }
            
            nameProperty.stringValue = $"Text{textCount}";
            
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 响应移除Element事件。
        /// </summary>
        private void OnRemoveElement(ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            AskRemoveVariable(bindValues, list.index);
        }

        /// <summary>
        /// 绘制Header。
        /// </summary>
        private void DrawHeader(Rect rect)
        {
            Rect labelRect = new Rect(rect.x, rect.y, rect.width - 120, rect.height);
            Rect clearButtonRect = new Rect(rect.x + rect.width - 120, rect.y, 100, rect.height);
            
            GUI.Label(labelRect, "静态文本绑定");
            if (GUI.Button(clearButtonRect, "清空所有绑定"))
            {
                ClearAllBindings();
            }
        }

        /// <summary>
        /// 清空全部绑定。
        /// </summary>
        private void ClearAllBindings()
        {
            var bindValues = list.serializedProperty;
            bindValues.arraySize = 0;
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制Element。
        /// </summary>
        private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            var bindValues = list.serializedProperty;
            if (index < 0 || index >= bindValues.arraySize)
                return;

            var variable = bindValues.GetArrayElementAtIndex(index);
            var nameProperty = variable.FindPropertyRelative("name");
            var textProperty = variable.FindPropertyRelative("text");

            float x = rect.x;
            float y = rect.y + 2;
            float width = rect.width - 40;

            // 绘制变量名
            Rect nameRect = new Rect(x, y, width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(nameRect, nameProperty, new GUIContent("变量名"));

            // 绘制文本输入框（多行）
            Rect textRect = new Rect(x, y + EditorGUIUtility.singleLineHeight + 2, width, TEXT_FIELD_HEIGHT);
            EditorGUI.BeginChangeCheck();
            string newText = EditorGUI.TextArea(textRect, textProperty.stringValue);
            if (EditorGUI.EndChangeCheck())
            {
                textProperty.stringValue = newText;
            }

            // 绘制操作按钮
            var buttonLeftRect = new Rect(nameRect.xMax + HORIZONTAL_GAP, y, 18, 18);
            var buttonRightRect = new Rect(buttonLeftRect.xMax, y, 18, 18);

            if (GUI.Button(buttonLeftRect, new GUIContent("+"), EditorStyles.miniButtonLeft))
            {
                DuplicateVariable(bindValues, index);
            }
            if (GUI.Button(buttonRightRect, new GUIContent("-"), EditorStyles.miniButtonRight))
            {
                AskRemoveVariable(bindValues, index);
            }
        }

        /// <summary>
        /// 执行请求移除变量相关逻辑。
        /// </summary>
        protected virtual void AskRemoveVariable(SerializedProperty bindValues, int index)
        {
            if (EditorUtility.DisplayDialog("删除绑定", "确定要删除这个静态文本绑定吗？", "确定", "取消"))
            {
                RemoveVariable(bindValues, index);
            }
        }

        /// <summary>
        /// 移除Variable。
        /// </summary>
        protected virtual void RemoveVariable(SerializedProperty bindValues, int index)
        {
            bindValues.DeleteArrayElementAtIndex(index);
            bindValues.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 执行复制变量相关逻辑。
        /// </summary>
        protected virtual void DuplicateVariable(SerializedProperty bindValues, int index)
        {
            bindValues.arraySize++;
            var source = bindValues.GetArrayElementAtIndex(index);
            var target = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);
            
            target.FindPropertyRelative("name").stringValue = source.FindPropertyRelative("name").stringValue + "_Copy";
            target.FindPropertyRelative("text").stringValue = source.FindPropertyRelative("text").stringValue;
            
            bindValues.serializedObject.ApplyModifiedProperties();
        }
    }
}

