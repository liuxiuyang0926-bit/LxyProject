using System;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StateControl.Runtime
{
    public class StateController : MonoBehaviour
    {
        
        public List<StateGroup> StateGroups => stateGroups;
        public List<ContinuousStateGroup> ContinuousStateGroups => continuousStateGroups;
        public List<ModifierRecordGroup> ModifierRecordGroups => modifierRecordGroups;
        
        public List<StateGroup> stateGroups = new List<StateGroup>();
        public List<ContinuousStateGroup> continuousStateGroups = new List<ContinuousStateGroup>();
        public List<ModifierRecordGroup> modifierRecordGroups = new List<ModifierRecordGroup>();

        public int GetStateValue(int stateGroupIndex)
        {
            if (stateGroupIndex < 0 || stateGroupIndex >= StateGroups.Count)
            {
                Debug.LogError("State group index out of range.");
                return -1;
            }

            var stateGroup = StateGroups[stateGroupIndex];
            return stateGroup.States.IndexOf(stateGroup.CurState);
        }
        
        public int GetStateValue(string stateGroupName)
        {
            StateGroup stateGroup = FindStateGroup(stateGroupName);
            if (stateGroup == null)
                return -1;
            return stateGroup.States.IndexOf(stateGroup.CurState);
        }
        
        public State GetCurrentState(string stateGroupName)
        {
            StateGroup stateGroup = FindStateGroup(stateGroupName);
            if (stateGroup == null)
                return null;
            return stateGroup.CurState;
        }
        
        public void ChangeState(int stateGroupIndex, int stateIndex)
        {
            if (stateGroupIndex < 0 || stateGroupIndex >= StateGroups.Count)
            {
                Debug.LogError("State group index out of range.");
                return;
            }

            var stateGroup = StateGroups[stateGroupIndex];
            if (stateIndex < 0 || stateIndex >= stateGroup.States.Count)
            {
                Debug.LogError("State index out of range.");
                return;
            }

            var newState = stateGroup.States[stateIndex];
            stateGroup.ApplyState(newState.Name);
            ApplyState(stateGroup.Name);
        }

        void ApplyState(string stateGroupName)
        {
            foreach (var recordGroup in modifierRecordGroups)
            {
                recordGroup.Apply(this, stateGroupName);
            }
        }

        public StateGroup FindStateGroup(string groupName)
        {
            foreach (var group in StateGroups)
            {
                if (group.Name == groupName)
                    return group;
            }

            return null;
        }

        public ContinuousStateGroup FindContinuousStateGroups(string groupName)
        {
            foreach (var group in ContinuousStateGroups)
            {
                if (group.Name == groupName)
                    return group;
            }

            return null;
        }
        
        public void ChangeState(string stateGroupName, int stateIndex)
        {
            var stateGroup = FindStateGroup(stateGroupName);
            if (stateGroup == null)
            {
                Debug.LogWarning($"State group '{stateGroupName}' not found.", this);
                return;
            }
            
            if (stateIndex < 0 || stateIndex >= stateGroup.States.Count)
            {
                Debug.LogError("State index out of range.", this);
                return;
            }

            var newState = stateGroup.States[stateIndex];
            stateGroup.ApplyState(newState.Name);
            ApplyState(stateGroup.Name);
        }
        
        public void ChangeState(BuiltInStateEnum builtInStateEnum, int stateIndex)
        {
            string stateGroupName = builtInStateEnum.ToString();
            var stateGroup = FindStateGroup(stateGroupName);
            if (stateGroup == null)
            {
                Debug.LogWarning($"State group '{stateGroupName}' not found.");
                return;
            }
            ChangeState(stateGroupName, stateIndex);
        }

        public void ChangeStateByName(string stateGroupName, string stateName)
        {
            var stateGroup = FindStateGroup(stateGroupName);
            if (stateGroup == null)
            {
                Debug.LogWarning($"State group '{stateGroupName}' not found.");
                return;
            }

            var newState = stateGroup.FindState(stateName);
            if (newState == null)
            {
                Debug.LogError($"State '{stateName}' not found in group '{stateGroupName}'.");
                return;
            }

            stateGroup.ApplyState(newState.Name);
            ApplyState(stateGroup.Name);
        }
        
        public void ChangeContinuousState(string name, float progress)
        {
            var stateGroup = FindContinuousStateGroups(name);
            if (stateGroup == null)
            {
                Debug.LogWarning($"Continuous state group '{name}' not found.");
                return;
            }
            
            stateGroup.Apply(progress);
        }
        
        public float GetContinuousStateProgress(string name)
        {
            var stateGroup = FindContinuousStateGroups(name);
            if (stateGroup == null)
            {
                Debug.LogWarning($"Continuous state group '{name}' not found.");
                return -1f;
            }

            return stateGroup.CurrentValue;
        }
        
        public void AddBuiltInContinuousGroup(BuiltInContinuousStateEnum builtInState)
        {
            continuousStateGroups ??= new List<ContinuousStateGroup>();
            string name = builtInState.ToString();
            if(FindContinuousStateGroups(name) != null)
                return;

            var newGroup = new ContinuousStateGroup
            {
                Name = name,
                Modifiers = new List<IContinuousModifier>()
            };

            continuousStateGroups.Add(newGroup);
        }
        
        private void OnValidate()
        {
            // 确保每个组的当前状态引用正确
            // StateGroup现在使用ISerializationCallbackReceiver自动处理引用
            // 但这里仍然保留以确保引用被正确初始化
            foreach (var group in StateGroups)
            {
                if (group.CurState != null && group.States != null)
                {
                    // 访问currentState属性会触发getter，确保引用正确
                    var _ = group.CurState;
                }
            }
        }
        
        private void Awake()
        {
            // 确保每个StateGroup的当前状态已应用
            ReapplyAllCurrentStates();
        }

        // 重新应用所有状态组的当前状态
        public void ReapplyAllCurrentStates()
        {
            foreach (var group in StateGroups)
            {
                if (group.CurState != null)
                {
                    
                    var curState = group.States[group.CurStateIndex];
                    group.ApplyState(curState.Name);
                    ApplyState(group.Name);
                }
            }
        }

        // 检查是否在Prefab模式下
        #if UNITY_EDITOR
        //[BlackList]
        public bool IsPrefabMode()
        {
            return UnityEditor.EditorUtility.IsPersistent(this) || 
                   UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) ||
                   UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
        }
        #endif

        public void AddBuiltInGroup(BuiltInStateEnum builtInState)
        {
            string name = builtInState.ToString();
            if(FindStateGroup(name) != null)
                return;
            
            List<State> states = new List<State>();
            BuiltInStateEnumAttribute attribute = (BuiltInStateEnumAttribute)Attribute.GetCustomAttribute(
                typeof(BuiltInStateEnum).GetField(name), typeof(BuiltInStateEnumAttribute));
            if (attribute != null)
            {
                foreach (string state in attribute.States)
                {
                    var newState = new State
                    {
                        Name = state
                    };
                    states.Add(newState);
                }
            }
            else
            {
                Debug.LogError($"未找到内置状态组 {name} 的定义");
                return;
            }
            var newGroup = new StateGroup
            {
                Name = name,
                States = states,
                CurState = states[0],
            };

            stateGroups.Add(newGroup);
        }
        
        public string GetStateGroupString()
        {
            StringBuilder sb = new StringBuilder();
            //把name中由<>包裹的字符都删除
            string pattern = "<[^>]*>"; // 匹配所有<开头，>结尾的内容
            string validName = Regex.Replace(name, pattern, "");
            // 遍历所有状态组，生成一个class SG_节点名，所有状态名的字符串为该class的字符串常量
            sb.Append($"public class SG_{validName} {{\n");
            foreach (var group in StateGroups)
            {
                sb.Append($"            public const string {group.Name} = \"{group.Name}\";\n");
                foreach (var state in group.States)
                {
                    sb.Append($"            public const string {group.Name}_{state.Name} = \"{state.Name}\";\n");
                }
            }
            
            sb.Append("        }\n");
            
            return sb.ToString();
        }
        
        public bool IsStateGroupNameRepeat(string n)
        {
            foreach (StateGroup stateGroup in StateGroups)
            {
                if (stateGroup.Name == n)
                    return true;
            }
            return false;
        }
        
        public bool IsStateNameRepeat(StateGroup stateGroup, string n)
        {
            foreach (State state in stateGroup.States)
            {
                if (state.Name == n)
                    return true;
            }
            return false;
        }

        public void ChangeStateGroupName(StateGroup stateGroup, string newName)
        {
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.RenameStateGroup(stateGroup.Name, newName);
            }
            stateGroup.Name = newName;
        }

        public void ChangeStateName(StateGroup stateGroup, State state, string newName)
        {
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.RenameState(stateGroup.Name, state.Name, newName);
            }

            state.Name = newName;
        }
        
        public void AddModifierGroup(ModifierTarget target, ModifierTypeEnum modifierType)
        {
            var group = new ModifierRecordGroup();
            group.Target = target;
            group.AddModifier(modifierType);
            ModifierRecordGroups.Add(group);
        }
        
        public void DeleteModifierRecord(int modifierIndex)
        {
            if (modifierIndex >= 0 && modifierIndex < ModifierRecordGroups.Count)
            {
                ModifierRecordGroups.RemoveAt(modifierIndex);
            }
        }

        public void RemoveStateGroup(int index)
        {
            StateGroup stateGroup = StateGroups[index];
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.RemoveStateGroup(stateGroup.Name);
            }
            StateGroups.RemoveAt(index);
        }
        
        public void RemoveState(StateGroup stateGroup, int stateIndex)
        {
            string groupName = stateGroup.Name;
            string stateName = stateGroup.States[stateIndex].Name;
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.RemoveState(groupName, stateName);
            }
            stateGroup.States.RemoveAt(stateIndex);
        }

        public void AddState(StateGroup stateGroup, string stateName)
        {
            var newState = new State
            {
                Name = stateName
            };

            // 如果是第一个状态，则设置为当前状态
            bool isFirstState = stateGroup.States.Count == 0;
            
            stateGroup.States.Add(newState);
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.AddState(stateGroup.Name, stateName);
            }
            
            if (isFirstState)
            {
                ChangeStateByName(stateGroup.Name, newState.Name);
            }
        }

        public void AddStateGroup(string groupName)
        {
            var newGroup = new StateGroup
            {
                Name = groupName,
                States = new List<State>(),
            };
            StateGroups.Add(newGroup);
        }
    }
}
