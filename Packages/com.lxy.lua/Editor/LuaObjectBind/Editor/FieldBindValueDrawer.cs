using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(FieldBindValue))]
    public class FieldBindValueDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var name = property.FindPropertyRelative("name");
            var objectValue = property.FindPropertyRelative("objectValue");
            var fieldBindType = property.FindPropertyRelative("fieldBindType");

            float y = position.y;
            float x = position.x;
            float height = GetPropertyHeight(property, label);
            float width = position.width - HORIZONTAL_GAP * 2;

            // 调整各个区域的宽度比例，预留右侧按钮空间
            float rightButtonSpace = 40; // 为+-按钮预留空间
            width -= rightButtonSpace;   // 减去按钮空间
            
            Rect nameRect = new Rect(x, y, Mathf.Min(200, width * 0.3f), height); 
            Rect valueRect = new Rect(nameRect.xMax + HORIZONTAL_GAP, y, Mathf.Max(width - 400, width * 0.4f), height);
            Rect componentRect = new Rect(valueRect.xMax + HORIZONTAL_GAP, y, Mathf.Min(120, width * 0.15f), height);
            Rect typeRect = new Rect(componentRect.xMax + HORIZONTAL_GAP, y, Mathf.Min(120, width * 0.15f), height);
            Rect typeEnumRect = new Rect(typeRect.xMax + HORIZONTAL_GAP, y, Mathf.Min(80, width * 0.1f), height);

            EditorGUI.PropertyField(nameRect, name, GUIContent.none);
            EditorGUI.PropertyField(valueRect, objectValue, GUIContent.none);

            if (objectValue.objectReferenceValue != null)
            {
                // 组件选择
                List<Object> components = new List<Object>();
                List<GUIContent> componentContents = new List<GUIContent>();

                // 添加对象本身
                components.Add(objectValue.objectReferenceValue);
                componentContents.Add(new GUIContent(objectValue.objectReferenceValue.GetType().Name, objectValue.objectReferenceValue.GetType().FullName));

                // 如果是GameObject，添加所有组件
                if (objectValue.objectReferenceValue is GameObject gameObject)
                {
                    foreach (var component in gameObject.GetComponents<Component>())
                    {
                        if (component == null)
                            continue;
                        components.Add(component);
                        componentContents.Add(new GUIContent(component.GetType().Name, component.GetType().FullName));
                    }
                }
                // 如果是Component，添加GameObject和其他组件
                else if (objectValue.objectReferenceValue is Component component)
                {
                    components.Add(component.gameObject);
                    componentContents.Add(new GUIContent("GameObject", "GameObject本身"));

                    foreach (var comp in component.gameObject.GetComponents<Component>())
                    {
                        if (comp == null || comp == component)
                            continue;
                        components.Add(comp);
                        componentContents.Add(new GUIContent(comp.GetType().Name, comp.GetType().FullName));
                    }
                }

                int componentCurrentIndex = components.IndexOf(objectValue.objectReferenceValue);
                if (componentCurrentIndex < 0) componentCurrentIndex = 0;

                EditorGUI.BeginChangeCheck();
                int componentNewIndex = EditorGUI.Popup(componentRect, GUIContent.none, componentCurrentIndex, componentContents.ToArray(), EditorStyles.popup);
                if (EditorGUI.EndChangeCheck() && componentNewIndex >= 0 && componentNewIndex < components.Count)
                {
                    objectValue.objectReferenceValue = components[componentNewIndex];
                }

                // 绑定类型选择
                List<FieldBindEnum> availableTypes = new List<FieldBindEnum>();
                List<GUIContent> typeContents = new List<GUIContent>();

                // 获取所有FieldBindEnum值
                foreach (FieldBindEnum bindEnum in System.Enum.GetValues(typeof(FieldBindEnum)))
                {
                    if (bindEnum == FieldBindEnum.None)
                        continue;

                    // 获取FieldBindTypeAttribute
                    FieldInfo fieldInfo = bindEnum.GetType().GetField(bindEnum.ToString());
                    if (fieldInfo == null)
                        continue;

                    FieldBindTypeAttribute attribute = fieldInfo.GetCustomAttribute<FieldBindTypeAttribute>();
                    if (attribute == null)
                        continue;

                    // 检查对象类型是否匹配
                    if (attribute.CanBindObjectType.IsAssignableFrom(objectValue.objectReferenceValue.GetType()))
                    {
                        availableTypes.Add(bindEnum);
                        // 将Text_FontSize这样的格式转换为FontSize(Text)
                        string enumName = bindEnum.ToString();
                        string[] parts = enumName.Split('_');
                        if (parts.Length == 2)
                        {
                            string componentName = parts[0];
                            string fieldName = parts[1];
                            typeContents.Add(new GUIContent($"{fieldName}({componentName})"));
                        }
                        else
                        {
                            typeContents.Add(new GUIContent(enumName));
                        }
                    }
                }

                if (availableTypes.Count > 0)
                {
                    int typeCurrentIndex = availableTypes.IndexOf((FieldBindEnum)fieldBindType.enumValueIndex);
                    if (typeCurrentIndex < 0) typeCurrentIndex = 0;

                    EditorGUI.BeginChangeCheck();
                    int typeNewIndex = EditorGUI.Popup(typeRect, GUIContent.none, typeCurrentIndex, typeContents.ToArray(), EditorStyles.popup);
                    if (EditorGUI.EndChangeCheck() && typeNewIndex >= 0 && typeNewIndex < availableTypes.Count)
                    {
                        fieldBindType.enumValueIndex = (int)availableTypes[typeNewIndex];
                    }

                    // 显示当前选择的FieldBindTypeEnum
                    FieldBindEnum currentBindEnum = (FieldBindEnum)fieldBindType.enumValueIndex;
                    FieldInfo currentFieldInfo = currentBindEnum.GetType().GetField(currentBindEnum.ToString());
                    if (currentFieldInfo != null)
                    {
                        FieldBindTypeAttribute currentAttribute = currentFieldInfo.GetCustomAttribute<FieldBindTypeAttribute>();
                        if (currentAttribute != null)
                        {
                            string typeEnumText = currentAttribute.FieldBindType.ToString();
                            EditorGUI.LabelField(typeEnumRect, typeEnumText);
                        }
                    }
                }
                else
                {
                    EditorGUI.LabelField(typeRect, "无可用类型");
                    EditorGUI.LabelField(typeEnumRect, "");
                }
            }
            else
            {
                EditorGUI.LabelField(componentRect, "请选择对象");
                EditorGUI.LabelField(typeRect, "请选择对象");
                EditorGUI.LabelField(typeEnumRect, "");
            }

            EditorGUI.EndProperty();
        }
    }
} 