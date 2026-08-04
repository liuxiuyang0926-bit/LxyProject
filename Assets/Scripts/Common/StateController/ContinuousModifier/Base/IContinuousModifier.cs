using UnityEngine.UIElements;

namespace StateControl.Runtime
{
    public interface IContinuousModifier
    {
        public ContinuousModifierTypeEnum ModifierType { get; }
        public void Apply(float progress);
        public void AddField(VisualElement root);
    }
}