using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using StateControl.Runtime;

namespace LuaObjectBind.Editor
{
    [CustomPropertyDrawer(typeof(StateControlBindValue))]
    public class StateControlBindValueDrawer : PropertyDrawer
    {
        private const float HORIZONTAL_GAP = 5;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var name = property.FindPropertyRelative("name");
            var stateController = property.FindPropertyRelative("stateController");
            var stateGroupName = property.FindPropertyRelative("stateGroupName");

            float y = position.y;
            float x = position.x;
            float height = GetPropertyHeight(property, label);
            float width = position.width - HORIZONTAL_GAP * 2;

            float rightButtonSpace = 40;
            width -= rightButtonSpace;

            Rect nameRect = new Rect(x, y, Mathf.Min(200, width * 0.35f), height);
            Rect controllerRect = new Rect(nameRect.xMax + HORIZONTAL_GAP, y, Mathf.Max(width - 400, width * 0.35f), height);
            Rect stateGroupRect = new Rect(controllerRect.xMax + HORIZONTAL_GAP, y, Mathf.Min(200, width * 0.3f), height);

            EditorGUI.PropertyField(nameRect, name, GUIContent.none);
            EditorGUI.PropertyField(controllerRect, stateController, GUIContent.none);

            if (stateController.objectReferenceValue != null && stateController.objectReferenceValue is StateController controller)
            {
                List<string> stateGroupNames = new List<string>();
                stateGroupNames.Add("无");

                foreach (var group in controller.StateGroups)
                {
                    if (group != null && !string.IsNullOrEmpty(group.Name))
                    {
                        stateGroupNames.Add(group.Name);
                    }
                }

                int currentIndex = 0;
                if (!string.IsNullOrEmpty(stateGroupName.stringValue))
                {
                    currentIndex = stateGroupNames.IndexOf(stateGroupName.stringValue);
                    if (currentIndex < 0)
                        currentIndex = 0;
                }

                EditorGUI.BeginChangeCheck();
                int newIndex = EditorGUI.Popup(stateGroupRect, currentIndex, stateGroupNames.ToArray());
                if (EditorGUI.EndChangeCheck())
                {
                    if (newIndex == 0)
                    {
                        stateGroupName.stringValue = "";
                    }
                    else if (newIndex > 0 && newIndex < stateGroupNames.Count)
                    {
                        stateGroupName.stringValue = stateGroupNames[newIndex];
                    }
                }
            }
            else
            {
                EditorGUI.LabelField(stateGroupRect, "请选择StateController");
            }

            EditorGUI.EndProperty();
        }
    }
}
