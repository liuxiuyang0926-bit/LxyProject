using UnityEngine;
using StateControl.Runtime;

namespace LuaObjectBind
{
    [System.Serializable]
    public class StateControlBindValue
    {
        [SerializeField] protected string name = "";

        [SerializeField] protected StateController stateController;

        [SerializeField] protected string stateGroupName = "";

        public string Name
        {
            get => name;
            set => name = value;
        }

        public string StateGroupName
        {
            get => stateGroupName;
            set => stateGroupName = value;
        }

        public System.Type ValueType
        {
            get
            {
                return this.stateController == null ? typeof(StateController) : this.stateController.GetType();
            }
        }

        public T GetValue<T>()
        {
            return (T)GetValue();
        }

        /// <summary>
        /// 设置值。
        /// </summary>
        public void SetValue(object value)
        {
            stateController = (StateController)value;
        }

        /// <summary>
        /// 获取值。
        /// </summary>
        public object GetValue()
        {
            return this.stateController;
        }

        /// <summary>
        /// 获取状态Controller。
        /// </summary>
        public StateController GetStateController()
        {
            return this.stateController;
        }
    }
}
