using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StateControl.Runtime
{
    [Serializable]
    public class ModifierRecordGroup
    {
        /// <summary>
        /// 公开的目标数据。
        /// </summary>
        public ModifierTarget Target;
        /// <summary>
        /// 公开的Records数据。
        /// </summary>
        public List<ModifierRecord> Records = new List<ModifierRecord>();
        /// <summary>
        /// 公开的默认值Record数据。
        /// </summary>
        public ModifierRecord DefaultRecord = new ModifierRecord();

        /// <summary>
        /// 执行应用相关逻辑。
        /// </summary>
        public void Apply(StateController stateController,string stateGroupName)
        {
            ModifierRecord wantRecord = null;
            foreach (var record in Records)
            {
                if (record.ContainsGroup(stateGroupName) && record.Satisfied(stateController))
                {
                    if (wantRecord == null || wantRecord.GetStateGroupCount() < record.GetStateGroupCount())
                    {
                        wantRecord = record;
                    }
                }
            }
            if(wantRecord != null)
                wantRecord.Apply(Target);
        } 

        /// <summary>
        /// 获取全部状态分组。
        /// </summary>
        public List<string> GetAllStateGroup()
        {
            List<string> stateGroups = new List<string>();
            foreach (ModifierRecord record in Records)
            {
                foreach (string key in record.SelectedStateGroups)
                {
                    if(!stateGroups.Contains(key))
                        stateGroups.Add(key);
                }   
            }

            return stateGroups;
        }

        /// <summary>
        /// 获取当前项Record。
        /// </summary>
        public ModifierRecord GetCurrentRecord(StateController stateController)
        {
            var allGroup = GetAllStateGroup();
            foreach (var r in Records)
            {
                if (r.Satisfied(stateController))
                {
                    bool isThis = true;
                    foreach (string s in allGroup)
                    {
                        if (!r.ContainsGroup(s))
                        {
                            isThis = false;
                        }
                    }

                    if (isThis)
                        return r;
                }
            }

            return DefaultRecord;
        }

        /// <summary>
        /// 执行Contain状态Group相关逻辑。
        /// </summary>
        public bool ContainStateGroup(string name)
        {
            foreach (ModifierRecord record in Records)
            {
                if (record.ContainsGroup(name))
                    return true;
            }

            return false;
        }
        
        /// <summary>
        /// 添加状态分组。
        /// </summary>
        public void AddStateGroup(StateGroup stateGroup)
        {
            if(ContainStateGroup(stateGroup.Name))
                return;
            // 增量添加记录
            var newRecords = new List<ModifierRecord>();

            // 1. 为现有记录扩展新状态组的所有状态
            foreach (var record in Records)
            {
                foreach (var state in stateGroup.States)
                {
                    var newRecord = record.Clone();
                    newRecord.AddSelection(stateGroup.Name, state.Name);
                    newRecords.Add(newRecord);
                }
            }

            // 2. 添加仅包含新状态组的记录
            foreach (var state in stateGroup.States)
            {
                var newRecord = DefaultRecord.Clone();
                newRecord.AddSelection(stateGroup.Name, state.Name);
                newRecords.Add(newRecord);
            } 
            Records.AddRange(newRecords);
        }

        /// <summary>
        /// 移除状态分组。
        /// </summary>
        public void RemoveStateGroup(string groupName)
        {
            // 移除所有包含该状态组的记录 
            Records.RemoveAll(r => r.ContainsGroup(groupName));
        }

        /// <summary>
        /// 执行Rename状态Group相关逻辑。
        /// </summary>
        public void RenameStateGroup(string oldName, string newName)
        {
            // 更新记录中的组名（不重建记录）
            foreach (var record in Records)
            {
                record.RenameGroup(oldName, newName);
            }
        }

        // 添加状态到指定组
        /// <summary>
        /// 添加状态。
        /// </summary>
        public void AddState(string groupName, string stateName)
        {
            // 增量生成新记录
            var newRecords = new List<ModifierRecord>();
            var firstRecord = Records.Find(r => r.ContainsGroup(groupName));
            string firstState = firstRecord?.GetSelectedState(groupName);
            if (firstState != null)
            {
                foreach (var record in Records.Where(r => r.ContainsGroup(groupName)))
                {
                    if (record.GetStateGroupCount() > 1 && record.GetSelectedState(groupName) == firstState)
                    {
                        var newRecord = record.Clone();
                        newRecord.SetState(groupName, stateName);
                        newRecords.Add(newRecord);
                    }
                }

                var singleRecord = DefaultRecord.Clone();
                singleRecord.AddSelection(groupName, stateName);
                newRecords.Add(singleRecord);

                Records.AddRange(newRecords);
            }
        }

        // 从指定组移除状态
        /// <summary>
        /// 移除状态。
        /// </summary>
        public void RemoveState(string groupName, string stateName)
        {
            // 移除所有使用该状态的记录
            Records.RemoveAll(r => 
                r.SatisfiedStateGroup(groupName, stateName));
        }

        // 重命名指定组的状态
        /// <summary>
        /// 执行Rename状态相关逻辑。
        /// </summary>
        public void RenameState(string groupName, string oldName, string newName)
        {
            // 更新所有相关记录中的状态名
            foreach (var record in Records.Where(r => 
                         r.SatisfiedStateGroup(groupName, oldName)))
            {
                record.SetState(groupName, newName);
            }
        }

        /// <summary>
        /// 添加Modifier。
        /// </summary>
        public void AddModifier(ModifierTypeEnum modifierType)
        {
            foreach (BaseModifier modifier in DefaultRecord.Modifiers)
            {
                if(modifier.ModifierType == modifierType)
                    return;
            }
            Type useType = modifierType.GetUseModifierType();
            if (useType != null)
            {
                AddRecordModifier(DefaultRecord, modifierType, useType);
                
                foreach (var record in Records)
                {
                    AddRecordModifier(record, modifierType, useType);
                }
            }
            else
            {
                Debug.LogWarning($"No modifier type found for {modifierType}");
            }
        }

        void AddRecordModifier(ModifierRecord record, ModifierTypeEnum modifierType, Type useType)
        {
            if (record.Modifiers == null)
            {
                record.Modifiers = new List<BaseModifier>();
            }
                    
            // 使用激活器创建修改器的实例
            var modifier = (BaseModifier)Activator.CreateInstance(useType);
            modifier.ModifierType = modifierType;
            modifier.RecordOriginValue(Target);
            record.Modifiers.Add(modifier);
        }

        /// <summary>
        /// 移除Modifier。
        /// </summary>
        public void RemoveModifier(ModifierTypeEnum modifierType)
        {
            RemoveRecordModifier(DefaultRecord, modifierType);
            foreach (var record in Records)
            {
                for (int i = 0; i < record.Modifiers.Count; i++)
                {
                    RemoveRecordModifier(record, modifierType);
                }
            }
        }

        void RemoveRecordModifier(ModifierRecord record, ModifierTypeEnum modifierType)
        {
            for (int i = 0; i < record.Modifiers.Count; i++)
            {
                if (record.Modifiers[i].ModifierType == modifierType)
                {
                    record.Modifiers.RemoveAt(i);
                }
            }
        }
    }
}