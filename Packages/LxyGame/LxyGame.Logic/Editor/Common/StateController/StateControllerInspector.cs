using System;
using UnityEngine;
using UnityEditor;
using StateControl.Runtime;

namespace StateControl.Editor
{
    [CustomEditor(typeof(StateController))]
    public class StateControllerInspector : UnityEditor.Editor
    {
        private SerializedObject serializedController;

        /// <summary>
        /// 在组件启用时建立运行时关联。
        /// </summary>
        private void OnEnable()
        {
            serializedController = new SerializedObject(target);

            // 注册Undo事件，确保在Undo/Redo时刷新Inspector
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        /// <summary>
        /// 在组件停用时解除运行时关联。
        /// </summary>
        private void OnDisable()
        {
            // 取消注册Undo事件
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        /// <summary>
        /// 响应UndoRedo事件。
        /// </summary>
        private void OnUndoRedo()
        {
            // 当执行Undo/Redo时，重新同步SerializedObject并刷新Inspector
            if (serializedController != null)
            {
                serializedController.Update();
            }

            // 重新应用所有当前状态
            var controller = target as StateController;
            if (controller != null)
            {
                controller.ReapplyAllCurrentStates();
            }

            Repaint();
        }

        /// <summary>
        /// 响应检查器GUI事件。
        /// </summary>
        public override void OnInspectorGUI()
        {
            serializedController.Update();

            var controller = (StateController)target;

            // 打开编辑器按钮
            if (GUILayout.Button("打开状态编辑器", GUILayout.Height(30)))
            {
                StateEditor.ShowWindow();
            }

            // 收集有问题的状态组名称和未绑定修改器的状态组名称
            var problematicGroupNames = StateControllerCheckUtil.GetProblematicStateGroupNames(controller);
            var boundGroupNames = StateControllerCheckUtil.GetBoundStateGroupNames(controller);

            // 为每个状态组创建UI
            for (int i = 0; i < controller.StateGroups.Count; i++)
            {
                var group = controller.StateGroups[i];
                bool hasIssue = problematicGroupNames.Contains(group.Name);
                bool unbound = !boundGroupNames.Contains(group.Name);
                if (hasIssue)
                    GUI.backgroundColor = new Color(1f, 0.3f, 0.3f);
                else if (unbound)
                    GUI.backgroundColor = new Color(1f, 0.9f, 0.3f);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                string groupName = group.GetShowName();
                bool isBuiltIn = StateGroup.IsBuiltInStateEnum(groupName, out BuiltInStateEnum stateEnum);
                if (isBuiltIn)
                {
                    BuiltInStateEnumAttribute attribute = (BuiltInStateEnumAttribute)Attribute.GetCustomAttribute(
                        typeof(BuiltInStateEnum).GetField(stateEnum.ToString()), typeof(BuiltInStateEnumAttribute));
                    if (attribute != null)
                    {
                        groupName = attribute.Name;
                    }
                    
                }
                // 状态组标题
                EditorGUILayout.LabelField(groupName, EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                
                // 添加所有状态按钮
                for (int j = 0; j < group.States.Count; j++)
                {
                    var state = group.States[j];
                    GUI.backgroundColor = state == group.CurState ? new Color(0.3f, 0.3f, 0.3f) : Color.gray;
                    
                    if (GUILayout.Button(state.GetShowName(), GUILayout.Height(25)))
                    {
                        Undo.RecordObject(controller, "Change State");
                        controller.ChangeStateByName(group.Name, state.Name);
                        EditorUtility.SetDirty(target);
                    }
                    
                    GUI.backgroundColor = Color.white;
                }
                
                EditorGUILayout.EndHorizontal();
                if (hasIssue || unbound)
                    GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }
            
            EditorGUILayout.Space(10);
            
            // 打开编辑器按钮
            if (GUILayout.Button("打开连续状态编辑器", GUILayout.Height(30)))
            {
                ContinuousStateEditor.ShowWindow();
            }
            
            // 为每个状态组创建UI
            for (int i = 0; i < controller.ContinuousStateGroups.Count; i++)
            {
                var group = controller.ContinuousStateGroups[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // 状态组标题
                EditorGUILayout.LabelField(group.Name, EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                float oldVal = group.CurrentValue;
                float newVal = GUILayout.HorizontalSlider(group.CurrentValue, 0, 1, GUILayout.Height(25));
                if (oldVal != newVal)
                {
                    Undo.RecordObject(controller, "Change State");
                    group.Apply(newVal);
                    EditorUtility.SetDirty(target);
                }

                newVal = EditorGUILayout.FloatField(newVal, GUILayout.Width(50));
                if (oldVal != newVal)
                {
                    Undo.RecordObject(controller, "Change State");
                    group.Apply(newVal);
                    EditorUtility.SetDirty(target);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(5);
            }

            serializedController.ApplyModifiedProperties();
        }
    }
} 