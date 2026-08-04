using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(BindValue))]
    public class BindValueDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var name = property.FindPropertyRelative("name");
            var objectValue = property.FindPropertyRelative("objectValue");
            var bindValueType = property.FindPropertyRelative("bindValueType");

            float y = position.y;
            float x = position.x;
            float height = GetPropertyHeight(property, label);
            float width = position.width - HORIZONTAL_GAP * 2;

            Rect nameRect = new Rect(x, y, Mathf.Min(200, width * 0.4f), height); 
            Rect valueRect = new Rect(nameRect.xMax + HORIZONTAL_GAP, y, Mathf.Max( width - 320, width * 0.4f) , height);
            Rect typeRect = new Rect(valueRect.xMax + HORIZONTAL_GAP, y, Mathf.Min( 120, width * 0.2f) , height);

            EditorGUI.PropertyField(nameRect, name, GUIContent.none);

            BindValueType bindValueTypeValue = (BindValueType)bindValueType.enumValueIndex;

            switch (bindValueTypeValue)
            { 
                case BindValueType.Object:
                    EditorGUI.BeginChangeCheck();
                    objectValue.objectReferenceValue = EditorGUI.ObjectField(valueRect, GUIContent.none, objectValue.objectReferenceValue, typeof(UnityEngine.Object), true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (string.IsNullOrEmpty(name.stringValue) && objectValue.objectReferenceValue != null)
                            name.stringValue = NormalizeName(objectValue.objectReferenceValue.name);
                    }
                    break;
                default:
                    break;
            }
            
            
            if (bindValueTypeValue == BindValueType.Object)
            {
                int index = 0;
                List<System.Type> types = new List<System.Type>();
                types.Add(typeof(UnityEngine.GameObject));
                if (objectValue.objectReferenceValue != null)
                {
                    GameObject go;
                    if (objectValue.objectReferenceValue is GameObject)
                        go = objectValue.objectReferenceValue as GameObject;
                    else
                        go = (objectValue.objectReferenceValue as Component).gameObject;
                    foreach (var c in go.GetComponents<Component>())
                    {
                        if (c == null)
                            continue;
            
                        if (!types.Contains(c.GetType()))
                            types.Add(c.GetType());
                    }
            
                    for (int i = 0; i < types.Count; i++)
                    {
                        if (objectValue.objectReferenceValue.GetType().Equals(types[i]))
                        {
                            index = i;
                            break;
                        }
                    }
                }
            
                List<GUIContent> contents = new List<GUIContent>();
                foreach (var t in types)
                {
                    contents.Add(new GUIContent(t.Name, t.FullName));
                }
            
                EditorGUI.BeginChangeCheck();
                var newIndex = EditorGUI.Popup(typeRect, GUIContent.none, index, contents.ToArray(), EditorStyles.popup);
                if (EditorGUI.EndChangeCheck())
                {
                    if (objectValue.objectReferenceValue != null)
                    {
                        if (types[newIndex] == typeof(GameObject))
                            objectValue.objectReferenceValue = (objectValue.objectReferenceValue as Component).gameObject;
                        else
                        {
                            if(objectValue.objectReferenceValue is GameObject)
                                objectValue.objectReferenceValue = (objectValue.objectReferenceValue as GameObject).GetComponent(types[newIndex]);
                            else
                                objectValue.objectReferenceValue = (objectValue.objectReferenceValue as Component).gameObject.GetComponent(types[newIndex]);
                        }
                    }
                    else
                    {
                        objectValue.objectReferenceValue = null;
                    }
                }
            }

            EditorGUI.EndProperty();
        }


        protected virtual string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";

            name = name.Replace(" ", "");
            return char.ToLower(name[0]) + name.Substring(1);
        }
    }
}