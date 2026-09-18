using System;
using System.Collections.Generic;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class ModifierRecord
    {
        [SerializeReference] public List<BaseModifier> Modifiers = new();

        /// <summary>
        /// 公开的Selected状态Groups数据。
        /// </summary>
        public List<string> SelectedStateGroups = new();
        /// <summary>
        /// 公开的Selected状态数据。
        /// </summary>
        public List<string> SelectedStates = new();
        
        /// <summary>
        /// 添加Modifier。
        /// </summary>
        public void AddModifier(BaseModifier modifier)
        {
            Modifiers.Add(modifier);
        }

        // 新增：移除修改器（按实例）
        /// <summary>
        /// 移除Modifier。
        /// </summary>
        public bool RemoveModifier(BaseModifier modifier)
        {
            return Modifiers.Remove(modifier);
        }
        
        /// <summary>
        /// 添加Selection。
        /// </summary>
        public void AddSelection(string groupName, string stateName)
        {
            SelectedStateGroups.Add(groupName);
            SelectedStates.Add(stateName);
        }

        /// <summary>
        /// 设置状态。
        /// </summary>
        public void SetState(string groupName, string stateName)
        {
            int groupIndex = SelectedStateGroups.FindIndex(x => x == groupName);
            SelectedStates[groupIndex] = stateName;
        }

        /// <summary>
        /// 执行判断是否包含Group相关逻辑。
        /// </summary>
        public bool ContainsGroup(string groupName)
        {
            return SelectedStateGroups.Contains(groupName);
        }

        /// <summary>
        /// 执行RenameGroup相关逻辑。
        /// </summary>
        public void RenameGroup(string oldName, string newName)
        {
            if (ContainsGroup(oldName))
            {
                int groupIndex = SelectedStateGroups.FindIndex(x => x == oldName);
                SelectedStateGroups[groupIndex] = newName;
            }
        }

        /// <summary>
        /// 执行Satisfied状态Group相关逻辑。
        /// </summary>
        public bool SatisfiedStateGroup(string groupName, string stateName)
        {
            if (ContainsGroup(groupName))
            {
                int groupIndex = SelectedStateGroups.FindIndex(x => x == groupName);
                return SelectedStates[groupIndex] == stateName;
            }

            return false;
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
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

        /// <summary>
        /// 执行Satisfied相关逻辑。
        /// </summary>
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
        
        /// <summary>
        /// 执行应用相关逻辑。
        /// </summary>
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

        /// <summary>
        /// 获取状态分组数量。
        /// </summary>
        public int GetStateGroupCount()
        {
            return SelectedStateGroups.Count;
        }

        /// <summary>
        /// 获取选中项状态。
        /// </summary>
        public string GetSelectedState(string groupName)
        {
            int index = SelectedStateGroups.FindIndex(x => x == groupName);
            if (index != -1)
                return SelectedStates[index];
            return null;
        }
    }
}