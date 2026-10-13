using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Root of an item's first-person model. Subclasses add what the item does in hand (fire, swing, use).
    // Position the model as a child: the root sits on the rig's hand point.
    [DisallowMultipleComponent]
    public class HeldItem : MonoBehaviour
    {
        public ItemInstance Item { get; private set; }

        public void Bind(ItemInstance item) => Item = item;

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
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
