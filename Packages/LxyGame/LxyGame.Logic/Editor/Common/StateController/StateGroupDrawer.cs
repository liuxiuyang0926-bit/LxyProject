using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using StateControl.Runtime;

namespace StateControl.Editor
{
    [CustomPropertyDrawer(typeof(StateGroup))]
    public class StateGroupDrawer : PropertyDrawer
    {
        private const float DRAG_AREA_HEIGHT = 50f;
        private const float SPACING = 2f;

        /// <summary>
        /// 获取属性高度。
        /// </summary>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUI.GetPropertyHeight(property, label);
            height += DRAG_AREA_HEIGHT + SPACING;
            return height;
        }

        /// <summary>
        /// 绘制编辑器窗口界面。
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // 绘制默认属性
            EditorGUI.PropertyField(position, property, label, true);

            // 计算拖拽区域的位置
            Rect dragAreaRect = new Rect(
                position.x,
                position.y + EditorGUI.GetPropertyHeight(property, label) + SPACING,
                position.width,
                DRAG_AREA_HEIGHT
            );

            // 绘制拖拽区域
            GUI.Box(dragAreaRect, "拖拽GameObject到这里");
            Event currentEvent = Event.current;

            if (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform)
            {
                if (!dragAreaRect.Contains(currentEvent.mousePosition))
                    return;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (currentEvent.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    HandleDragAndDrop(property);
                }
                currentEvent.Use();
            }
        }

        /// <summary>
        /// 处理DragAndDrop。
        /// </summary>
        private void HandleDragAndDrop(SerializedProperty property)
        {
            GameObject draggedObject = DragAndDrop.objectReferences[0] as GameObject;
            if (draggedObject == null) return;

            // 获取所有支持Modifier的组件
            var supportedComponents = GetSupportedComponents(draggedObject);
            if (supportedComponents.Count == 0)
            {
                Debug.LogWarning($"No supported components found on {draggedObject.name}");
                return;
            }

            // 创建下拉菜单
            GenericMenu menu = new GenericMenu();
            foreach (var component in supportedComponents)
            {
                var componentType = component.GetType();
                var modifierTypes = GetSupportedModifierTypes(componentType);
                
                foreach (var modifierType in modifierTypes)
                {
                    menu.AddItem(
                        new GUIContent($"{componentType.Name}/{modifierType.GetChineseName()}"),
                        false,
                        () => AddModifierTarget(property, component, modifierType)
                    );
                }
            }
            menu.ShowAsContext();
        }

        /// <summary>
        /// 获取SupportedComponents。
        /// </summary>
        private List<Component> GetSupportedComponents(GameObject gameObject)
        {
            var allComponents = gameObject.GetComponents<Component>();
            var supportedComponents = new List<Component>();

            foreach (var component in allComponents)
            {
                if (component == null) continue;
                
                var componentType = component.GetType();
                var modifierTypes = GetSupportedModifierTypes(componentType);
                
                if (modifierTypes.Count > 0)
                {
                    supportedComponents.Add(component);
                }
            }

            return supportedComponents;
        }

        /// <summary>
        /// 获取SupportedModifier类型。
        /// </summary>
        private List<ModifierTypeEnum> GetSupportedModifierTypes(Type componentType)
        {
            var supportedTypes = new List<ModifierTypeEnum>();
            var enumValues = System.Enum.GetValues(typeof(ModifierTypeEnum));
            
            foreach (ModifierTypeEnum value in enumValues)
            {
                var targetType = value.GetTargetType();
                if (targetType != null && targetType.IsAssignableFrom(componentType))
                {
                    supportedTypes.Add(value);
                }
            }

            return supportedTypes.Distinct().ToList();
        }

        /// <summary>
        /// 添加Modifier目标。
        /// </summary>
        private void AddModifierTarget(SerializedProperty property, Component component, ModifierTypeEnum modifierType)
        {
            // 记录Undo操作
            var targetObject = property.serializedObject.targetObject;
            Undo.RecordObject(targetObject, "Add Modifier Target");

            var modifierTargets = property.FindPropertyRelative("ModifierTargets");
            modifierTargets.arraySize++;

            var newTarget = modifierTargets.GetArrayElementAtIndex(modifierTargets.arraySize - 1);
            newTarget.FindPropertyRelative("TargetObject").objectReferenceValue = component;
            newTarget.FindPropertyRelative("ModifierType").enumValueIndex = (int)modifierType;

            property.serializedObject.ApplyModifiedProperties();

            // 标记对象为脏状态
            EditorUtility.SetDirty(targetObject);
        }
    }
} 