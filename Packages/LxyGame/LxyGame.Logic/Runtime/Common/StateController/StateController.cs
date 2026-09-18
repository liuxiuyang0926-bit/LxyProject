using System;
using UnityEngine;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace StateControl.Runtime
{
    public class StateController : MonoBehaviour
    {
        
        /// <summary>
        /// 向调用方提供状态Groups。
        /// </summary>
        public List<StateGroup> StateGroups => stateGroups;
        /// <summary>
        /// 向调用方提供Continuous状态Groups。
        /// </summary>
        public List<ContinuousStateGroup> ContinuousStateGroups => continuousStateGroups;
        /// <summary>
        /// 向调用方提供ModifierRecordGroups。
        /// </summary>
        public List<ModifierRecordGroup> ModifierRecordGroups => modifierRecordGroups;
        
        /// <summary>
        /// 公开的状态Groups数据。
        /// </summary>
        public List<StateGroup> stateGroups = new List<StateGroup>();
        /// <summary>
        /// 公开的continuous状态Groups数据。
        /// </summary>
        public List<ContinuousStateGroup> continuousStateGroups = new List<ContinuousStateGroup>();
        /// <summary>
        /// 公开的modifierRecordGroups数据。
        /// </summary>
        public List<ModifierRecordGroup> modifierRecordGroups = new List<ModifierRecordGroup>();

        /// <summary>
        /// 获取状态值。
        /// </summary>
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
        
        /// <summary>
        /// 获取状态值。
        /// </summary>
        public int GetStateValue(string stateGroupName)
        {
            StateGroup stateGroup = FindStateGroup(stateGroupName);
            if (stateGroup == null)
                return -1;
            return stateGroup.States.IndexOf(stateGroup.CurState);
        }
        
        /// <summary>
        /// 获取当前项状态。
        /// </summary>
        public State GetCurrentState(string stateGroupName)
        {
            StateGroup stateGroup = FindStateGroup(stateGroupName);
            if (stateGroup == null)
                return null;
            return stateGroup.CurState;
        }
        
        /// <summary>
        /// 执行变更状态相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 查找状态分组。
        /// </summary>
        public StateGroup FindStateGroup(string groupName)
        {
            foreach (var group in StateGroups)
            {
                if (group.Name == groupName)
                    return group;
            }

            return null;
        }

        /// <summary>
        /// 查找Continuous状态Groups。
        /// </summary>
        public ContinuousStateGroup FindContinuousStateGroups(string groupName)
        {
            foreach (var group in ContinuousStateGroups)
            {
                if (group.Name == groupName)
                    return group;
            }

            return null;
        }
        
        /// <summary>
        /// 执行变更状态相关逻辑。
        /// </summary>
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
        
        /// <summary>
        /// 执行变更状态相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 执行变更状态By名称相关逻辑。
        /// </summary>
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
        
        /// <summary>
        /// 执行变更Continuous状态相关逻辑。
        /// </summary>
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
        
        /// <summary>
        /// 获取Continuous状态进度。
        /// </summary>
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
        
        /// <summary>
        /// 添加构建结果InContinuous分组。
        /// </summary>
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
        
        /// <summary>
        /// 响应校验事件。
        /// </summary>
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
        
        /// <summary>
        /// 初始化组件的运行时状态。
        /// </summary>
        private void Awake()
        {
            // 确保每个StateGroup的当前状态已应用
            ReapplyAllCurrentStates();
        }

        // 重新应用所有状态组的当前状态
        /// <summary>
        /// 执行ReapplyAll当前项状态相关逻辑。
        /// </summary>
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
        /// <summary>
        /// 执行判断是否预制体Mode相关逻辑。
        /// </summary>
        public bool IsPrefabMode()
        {
            return UnityEditor.EditorUtility.IsPersistent(this) || 
                   UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) ||
                   UnityEditor.PrefabUtility.IsPartOfPrefabInstance(this);
        }
        #endif

        /// <summary>
        /// 添加构建结果In分组。
        /// </summary>
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
        
        /// <summary>
        /// 获取状态分组字符串。
        /// </summary>
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
        
        /// <summary>
        /// 执行判断是否状态Group名称Repeat相关逻辑。
        /// </summary>
        public bool IsStateGroupNameRepeat(string n)
        {
            foreach (StateGroup stateGroup in StateGroups)
            {
                if (stateGroup.Name == n)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// 执行判断是否状态名称Repeat相关逻辑。
        /// </summary>
        public bool IsStateNameRepeat(StateGroup stateGroup, string n)
        {
            foreach (State state in stateGroup.States)
            {
                if (state.Name == n)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 执行变更状态Group名称相关逻辑。
        /// </summary>
        public void ChangeStateGroupName(StateGroup stateGroup, string newName)
        {
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.RenameStateGroup(stateGroup.Name, newName);
            }
            stateGroup.Name = newName;
        }

        /// <summary>
        /// 执行变更状态名称相关逻辑。
        /// </summary>
        public void ChangeStateName(StateGroup stateGroup, State state, string newName)
        {
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.RenameState(stateGroup.Name, state.Name, newName);
            }

            state.Name = newName;
        }
        
        /// <summary>
        /// 添加Modifier分组。
        /// </summary>
        public void AddModifierGroup(ModifierTarget target, ModifierTypeEnum modifierType)
        {
            var group = new ModifierRecordGroup();
            group.Target = target;
            group.AddModifier(modifierType);
            ModifierRecordGroups.Add(group);
        }
        
        /// <summary>
        /// 执行Delete修饰器记录相关逻辑。
        /// </summary>
        public void DeleteModifierRecord(int modifierIndex)
        {
            if (modifierIndex >= 0 && modifierIndex < ModifierRecordGroups.Count)
            {
                ModifierRecordGroups.RemoveAt(modifierIndex);
            }
        }

        /// <summary>
        /// 移除状态分组。
        /// </summary>
        public void RemoveStateGroup(int index)
        {
            StateGroup stateGroup = StateGroups[index];
            foreach (ModifierRecordGroup recordGroup in ModifierRecordGroups)
            {
                recordGroup.RemoveStateGroup(stateGroup.Name);
            }
            StateGroups.RemoveAt(index);
        }
        
        /// <summary>
        /// 移除状态。
        /// </summary>
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

        /// <summary>
        /// 添加状态。
        /// </summary>
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

        /// <summary>
        /// 添加状态分组。
        /// </summary>
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
