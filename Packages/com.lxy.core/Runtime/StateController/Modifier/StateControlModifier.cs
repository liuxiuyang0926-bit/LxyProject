using System;
using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    [Serializable]
    public class StateControlModifier : BaseModifier
    {
        /// <summary>
        /// 公开的状态Group名称数据。
        /// </summary>
        public string StateGroupName;
        /// <summary>
        /// 公开的状态名称数据。
        /// </summary>
        public string StateName;

        /// <summary>
        /// 修改目标状态。
        /// </summary>
        public override void Modify(ModifierTarget target)
        {
            if (target.TargetObject is StateController stateController)
            {
                switch (ModifierType)
                {
                    case ModifierTypeEnum.StateControlChangeState:
                        stateController.ChangeStateByName(StateGroupName, StateName);
                        break;
                }
            }
        }

        /// <summary>
        /// 执行记录原始值值相关逻辑。
        /// </summary>
        public override void RecordOriginValue(ModifierTarget target)
        {
            // StateController doesn't have a "current state" to record in the same way
            // as other modifiers, so we can leave this empty or optionally store current state
        }

        /// <summary>
        /// 添加Field。
        /// </summary>
        public override void AddField(VisualElement root, Action onValueChanged, ModifierTarget target)
        {
#if UNITY_EDITOR
            var stateGroupField = new TextField("状态组名称");
            stateGroupField.value = StateGroupName;
            stateGroupField.RegisterValueChangedCallback(evt =>
            {
                StateGroupName = evt.newValue;
                onValueChanged?.Invoke();
            });
            root.Add(stateGroupField);

            var stateNameField = new TextField("状态名称");
            stateNameField.value = StateName;
            stateNameField.RegisterValueChangedCallback(evt =>
            {
                StateName = evt.newValue;
                onValueChanged?.Invoke();
            });
            root.Add(stateNameField);
#endif
        }

        /// <summary>
        /// 创建当前实例的副本。
        /// </summary>
        public override BaseModifier Clone()
        {
            return new StateControlModifier
            {
                ModifierType = this.ModifierType,
                StateGroupName = this.StateGroupName,
                StateName = this.StateName
            };
        }
    }
}
