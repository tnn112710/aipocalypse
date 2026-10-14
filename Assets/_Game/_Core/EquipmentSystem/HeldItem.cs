using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Root of an item's first-person model. Subclasses add what the item does in hand (fire, swing, use).
    // Position the model as a child: the root sits on the rig's hand point.
    [DisallowMultipleComponent]
    public class HeldItem : MonoBehaviour
    {
        public ItemInstance Item { get; private set; }
        protected IEquipmentOwner Owner { get; private set; }

        public virtual ViewKick ViewKick => default;
        public virtual bool IsAiming => false;

        public void Bind(ItemInstance item, IEquipmentOwner owner)
        {
            Item = item;
            Owner = owner;
            OnBound();
        }

        // Shown when it starts being raised, hidden once fully holstered.
        public void Show()
        {
            gameObject.SetActive(true);
            OnShown();
        }

        public void Hide()
        {
            OnHidden();
            gameObject.SetActive(false);
        }

        public virtual void OnReady() { }

        // Every frame while this is the item in hand, raising and holstering included (input.IsReady is false then).
        public virtual void Tick(in HeldItemInput input, float deltaTime) { }

        protected virtual void OnBound() { }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
