using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    public interface IContinuousModifier
    {
        /// <summary>
        /// 向调用方提供Modifier类型。
        /// </summary>
        public ContinuousModifierTypeEnum ModifierType { get; }
        /// <summary>
        /// 执行应用相关逻辑。
        /// </summary>
        public void Apply(float progress);
        /// <summary>
        /// 添加Field。
        /// </summary>
        public void AddField(VisualElement root);
    }
}