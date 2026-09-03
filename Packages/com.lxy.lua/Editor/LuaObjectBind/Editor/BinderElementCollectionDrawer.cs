using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using LuaObjectBind;

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(BinderElementCollection))]
    public class BinderElementCollectionDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;
        private const float VERTICAL_GAP = 5;

        private ReorderableList list;

        /// <summary>
        /// 获取列表。
        /// </summary>
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

        /// <summary>
        /// 获取属性高度。
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
                return 40;

            float height = base.GetPropertyHeight(property, label) + 85; // 增加25像素为HelpBox预留空间
            var bindValues = property.FindPropertyRelative("binds");
            for (int i = 0; i < bindValues.arraySize; i++)
            {
                height += EditorGUI.GetPropertyHeight(bindValues.GetArrayElementAtIndex(i)) + VERTICAL_GAP;
            }
            
            // 为错误信息预留空间
            var errors = GetValidationErrors(property);
            if (errors.Count > 0)
            {
                height += errors.Count * 20 + 5;
            }

            return height;
        }

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.serializedObject.isEditingMultipleObjects)
            {
                EditorGUI.HelpBox(position, "不可多选", MessageType.Warning);
                return;
            }

            var bindValues = property.FindPropertyRelative("binds");
            var reorderableList = GetList(bindValues);
            
            // 计算列表绘制区域
            float listHeight = reorderableList.GetHeight();
            Rect listRect = new Rect(position.x, position.y, position.width, listHeight);
            
            // 绘制列表
            reorderableList.DoList(listRect);
            
            // 在列表绘制完成后，统一显示错误信息
            DisplayValidationErrors(position, property);
        }

        /// <summary>
        /// 响应加法Element事件。
        /// </summary>
        private void OnAddElement(Rect rect, ReorderableList list)
        {
            var bindValues = list.serializedProperty;
            int index = bindValues.arraySize > 0 ? bindValues.arraySize : 0;
            bindValues.arraySize++;
            
            // 清空新添加项的Name和objectBinder引用
            var newElement = bindValues.GetArrayElementAtIndex(bindValues.arraySize - 1);
            newElement.FindPropertyRelative("name").stringValue = "";
            newElement.FindPropertyRelative("objectBinder").objectReferenceValue = null;
            
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
            GUI.Label(rect, "静态自动绑定的Logic对象（含有ObjectBinder）");
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
            EditorGUI.PropertyField(rect, variable, GUIContent.none);

            var buttonRect = new Rect(rect.xMax - 60, rect.y + 1, 50, 16);

            if (GUI.Button(buttonRect, new GUIContent("删除"), EditorStyles.miniButton))
            {
                AskRemoveVariable(bindValues, index);
            }
        }

        /// <summary>
        /// 执行请求移除变量相关逻辑。
        /// </summary>
        protected virtual void AskRemoveVariable(SerializedProperty bindValues, int index)
        {
            if (EditorUtility.DisplayDialog("删除绑定", "确定要删除这个绑定吗？", "确定", "取消"))
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

            target.FindPropertyRelative("name").stringValue = source.FindPropertyRelative("name").stringValue;
            target.FindPropertyRelative("objectBinder").objectReferenceValue = source.FindPropertyRelative("objectBinder").objectReferenceValue;

            bindValues.serializedObject.ApplyModifiedProperties();
        }
        
        /// <summary>
        /// 获取校验Errors。
        /// </summary>
        private List<string> GetValidationErrors(SerializedProperty property)
        {
            var errors = new List<string>();
            var bindValues = property.FindPropertyRelative("binds");
            if (bindValues == null || bindValues.arraySize == 0)
                return errors;
                
            var nameErrors = new Dictionary<string, List<int>>();
            var objectErrors = new Dictionary<UnityEngine.Object, List<int>>();
            
            // 收集所有错误
            for (int i = 0; i < bindValues.arraySize; i++)
            {
                var element = bindValues.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("name");
                var objProp = element.FindPropertyRelative("objectBinder");
                string name = nameProp.stringValue;
                var obj = objProp.objectReferenceValue;
                
                // 检查命名规范
                if (!string.IsNullOrEmpty(name))
                {
                    var namingError = ValidateNaming(name);
                    if (!string.IsNullOrEmpty(namingError))
                    {
                        errors.Add($"项目 {i + 1}: {namingError}");
                    }
                    
                    // 收集重名错误
                    if (!nameErrors.ContainsKey(name))
                        nameErrors[name] = new List<int>();
                    nameErrors[name].Add(i);
                }
                
                // 收集重复引用错误
                if (obj != null)
                {
                    if (!objectErrors.ContainsKey(obj))
                        objectErrors[obj] = new List<int>();
                    objectErrors[obj].Add(i);
                }
            }
            
            // 构建错误信息
            foreach (var kvp in nameErrors)
            {
                if (kvp.Value.Count > 1)
                {
                    errors.Add($"{kvp.Key}重名 (项目 {string.Join(", ", kvp.Value.Select(x => x + 1))})");
                }
            }
            
            foreach (var kvp in objectErrors)
            {
                if (kvp.Value.Count > 1)
                {
                    string objName = GetObjectName(kvp.Key);
                    errors.Add($"{objName}重复引用 (项目 {string.Join(", ", kvp.Value.Select(x => x + 1))})");
                }
            }
            
            return errors;
        }
        
        /// <summary>
        /// 执行DisplayValidationErrors相关逻辑。
        /// </summary>
        private void DisplayValidationErrors(Rect position, SerializedProperty property)
        {
            var errors = GetValidationErrors(property);
            
            // 显示错误信息
            if (errors.Count > 0)
            {
                float errorY = position.y + position.height - errors.Count * 20 - 5;
                for (int i = 0; i < errors.Count; i++)
                {
                    Rect errorRect = new Rect(position.x, errorY + i * 20, position.width, 18);
                    EditorGUI.HelpBox(errorRect, errors[i], MessageType.Error);
                }
            }
        }
        
        /// <summary>
        /// 获取对象名称。
        /// </summary>
        private string GetObjectName(UnityEngine.Object obj)
        {
            if (obj == null) return "未知对象";
            
            if (obj is ObjectBinder binder && binder.gameObject != null)
                return binder.gameObject.name;
            else if (obj is GameObject go)
                return go.name;
            else
                return obj.name;
        }
        
        /// <summary>
        /// 校验Naming。
        /// </summary>
        private string ValidateNaming(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
                
            // 检查是否以字母或下划线开头
            if (!char.IsLetter(name[0]) && name[0] != '_')
            {
                return "变量名必须以字母或下划线开头";
            }
            
            // 检查是否包含非法字符（空格和特殊字符）
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    return "变量名只能包含字母、数字和下划线，不能包含空格和特殊字符";
                }
            }
            
            return null; // 验证通过
        }
    }
} 