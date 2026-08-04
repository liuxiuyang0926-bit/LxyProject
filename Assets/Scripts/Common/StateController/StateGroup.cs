using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class StateGroup
    {
        public string Name;

        public List<State> States;

        public int CurStateIndex = -1;

        // todo 保留旧的字段用于向后兼容，后面要删掉
        public State currentState;
        
        [NonSerialized]
        private State _curState;

        public State CurState
        {
            get 
            {
                // 确保引用正确
                if (_curState == null && CurStateIndex >= 0 && CurStateIndex < States.Count)
                {
                    _curState = States[CurStateIndex];
                }
                return _curState;
            }
            set
            {
                _curState = value;
                // 同时更新索引
                if (value != null && States != null)
                {
                    CurStateIndex = States.IndexOf(value);
                }
                else
                {
                    CurStateIndex = -1;
                }
            }
        }

        public string Note;

        public State FindState(string stateName)
        {
            if (States == null)
                return null;
            foreach (var state in States)
            {
                if (state.Name == stateName)
                    return state;
            }

            return null;
        }
        
        public void ApplyState(string stateName)
        {
            // 查找匹配名称的状态
            State foundState = null;
            foreach (var state in States)
            {
                if (state.Name == stateName)
                {
                    foundState = state;
                    break;
                }
            }
            
            // 如果找到状态，将其设为当前状态并应用
            if (foundState != null)
            {
                // 设置新的当前状态
                CurState = foundState;
            }
            else
            {
                Debug.LogWarning($"找不到名为 {stateName} 的状态");
            }
        }

        public string GetShowName(bool onlyNote = false)
        {
            bool isBuiltIn = IsBuiltInStateEnum(Name, out BuiltInStateEnum builtInState);
            if (isBuiltIn)
            {
                BuiltInStateEnumAttribute attribute = (BuiltInStateEnumAttribute)Attribute.GetCustomAttribute(
                    typeof(BuiltInStateEnum).GetField(builtInState.ToString()), typeof(BuiltInStateEnumAttribute));
                if (attribute != null)
                    return onlyNote ? attribute.Name : $"{attribute.Name}({Name})";
            }
            
            bool noNote = string.IsNullOrEmpty(Note);
            if (onlyNote)
                return noNote ? Name : Note;
            return noNote ? Name : $"{Note}({Name})";
        }
        
        public static bool IsBuiltInStateEnum(string sgName, out BuiltInStateEnum builtInState)
        {
            return Enum.TryParse<BuiltInStateEnum>(sgName, out builtInState)
                             && Enum.IsDefined(typeof(BuiltInStateEnum), builtInState);
        }
    }
}