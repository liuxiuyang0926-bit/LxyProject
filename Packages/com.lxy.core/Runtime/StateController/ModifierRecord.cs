using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class ModifierRecord
    {
        [SerializeReference] public List<BaseModifier> Modifiers = new();

        public List<string> SelectedStateGroups = new();
        public List<string> SelectedStates = new();
        
        public void AddModifier(BaseModifier modifier)
        {
            Modifiers.Add(modifier);
        }

        // 新增：移除修改器（按实例）
        public bool RemoveModifier(BaseModifier modifier)
        {
            return Modifiers.Remove(modifier);
        }
        
        public void AddSelection(string groupName, string stateName)
        {
            SelectedStateGroups.Add(groupName);
            SelectedStates.Add(stateName);
        }

        public void SetState(string groupName, string stateName)
        {
            int groupIndex = SelectedStateGroups.FindIndex(x => x == groupName);
            SelectedStates[groupIndex] = stateName;
        }

        public bool ContainsGroup(string groupName)
        {
            return SelectedStateGroups.Contains(groupName);
        }

        public void RenameGroup(string oldName, string newName)
        {
            if (ContainsGroup(oldName))
            {
                int groupIndex = SelectedStateGroups.FindIndex(x => x == oldName);
                SelectedStateGroups[groupIndex] = newName;
            }
        }

        public bool SatisfiedStateGroup(string groupName, string stateName)
        {
            if (ContainsGroup(groupName))
            {
                int groupIndex = SelectedStateGroups.FindIndex(x => x == groupName);
                return SelectedStates[groupIndex] == stateName;
            }

            return false;
        }

        public ModifierRecord Clone()
        {
            var newRecord = new ModifierRecord();
            newRecord.SelectedStateGroups = new List<string>(SelectedStateGroups);
            newRecord.SelectedStates = new List<string>(SelectedStates);
        
            // 深拷贝修改器
            foreach (var modifier in this.Modifiers)
            {
                newRecord.Modifiers.Add(modifier?.Clone());
            }
        
            return newRecord;
        }

        public bool Satisfied(StateController stateController)
        {
            for(int i=0;i<SelectedStateGroups.Count;i++)
            {
                var curState = stateController.GetCurrentState(SelectedStateGroups[i]);
                if (curState.Name != SelectedStates[i])
                    return false;
            }

            return true;
        }
        
        public void Apply(ModifierTarget target)
        {
            if (Modifiers == null || Modifiers.Count == 0)
            {
                Modifiers = new List<BaseModifier>();
            }
            // 应用所有Modifier
            for(int i = 0; i < Modifiers.Count; i++)
            {
                Modifiers[i]?.Modify(target);
            }
        }

        public int GetStateGroupCount()
        {
            return SelectedStateGroups.Count;
        }

        public string GetSelectedState(string groupName)
        {
            int index = SelectedStateGroups.FindIndex(x => x == groupName);
            if (index != -1)
                return SelectedStates[index];
            return null;
        }
    }
}