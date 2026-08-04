using UnityEditor;
using UnityEngine;
using LuaObjectBind;
using UnityEditorInternal;

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(BinderElement))]
    public class BinderElementDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }
        
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float width = position.width;
            float labelWidth = 80f;
            float fieldWidth = (width - labelWidth - 10f) * 0.5f;
            Rect nameRect = new Rect(position.x, position.y, fieldWidth, position.height);
            Rect objRect = new Rect(position.x + fieldWidth + 10f, position.y, fieldWidth, position.height);

            var nameProp = property.FindPropertyRelative("name");
            var objProp = property.FindPropertyRelative("objectBinder");
            EditorGUI.PropertyField(nameRect, nameProp, GUIContent.none);
            // 先绘制objectBinder，后绘制name
            EditorGUI.BeginChangeCheck();
            objProp.objectReferenceValue = EditorGUI.ObjectField(objRect, GUIContent.none, objProp.objectReferenceValue, typeof(ObjectBinder), true);
            if (EditorGUI.EndChangeCheck())
            {
                Debug.Log("name: " + nameProp.stringValue);
                if (objProp.objectReferenceValue != null && string.IsNullOrEmpty(nameProp.stringValue))
                {
                    var obj = objProp.objectReferenceValue;
                    string baseName = null;
                    if (obj is ObjectBinder binder && binder.gameObject != null)
                        baseName = binder.gameObject.name;
                    else if (obj is GameObject go)
                        baseName = go.name;
                    else
                        baseName = obj.name;
                    nameProp.stringValue = baseName;
                }
            }
            EditorGUI.EndProperty();
        }
        

    }
}

// 辅助方法：获取父级数组属性
public static class SerializedPropertyExtensions
{
    public static SerializedProperty GetArrayPropertyParent(this SerializedProperty property)
    {
        var path = property.propertyPath;
        int arrayIndex = path.LastIndexOf(".Array.data[");
        if (arrayIndex < 0) return null;
        var arrayPath = path.Substring(0, arrayIndex);
        var parent = property.serializedObject.FindProperty(arrayPath);
        return parent;
    }
}