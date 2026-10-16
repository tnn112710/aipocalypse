using System.Collections.Generic;
using Aipocalypse.Core.HandSystem;
using UnityEngine;

namespace Aipocalypse.Core.EquipmentSystem
{
    // Root of an item's first-person model. Subclasses add what the item does in hand (fire, swing, use).
    // Position the model as a child: the root sits on the rig's hand point.
    [DisallowMultipleComponent]
    public class HeldItem : MonoBehaviour
    {
        readonly List<Renderer> _renderers = new();

        public ItemInstance Item { get; private set; }
        // Where the first-person hands hold this item, and its hand actions; null = no hands.
        public ItemHands Hands { get; private set; }
        protected IEquipmentOwner Owner { get; private set; }

        public virtual ViewKick ViewKick => default;
        public virtual bool IsAiming => false;
        // False while an obstacle pose would interfere with the item's motion.
        public virtual bool KeepsClear => true;

        public void Bind(ItemInstance item, IEquipmentOwner owner)
        {
            Item = item;
            Owner = owner;
            Hands = GetComponent<ItemHands>();
            // Before anything gets parented to the model (muzzle flashes), so only the model's own meshes count.
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer or SkinnedMeshRenderer)
                    _renderers.Add(renderer);
            }
            OnBound();
        }

        // The model's furthest point along `forward` from `eye`, from its meshes' bounds as drawn this frame.
        public bool TryGetTip(Vector3 eye, Vector3 forward, out Vector3 tip)
        {
            tip = eye;
            float furthest = float.MinValue;
            foreach (Renderer renderer in _renderers)
            {
                if (renderer == null || !renderer.enabled)
                    continue;
                Bounds bounds = renderer.localBounds;
                Transform space = renderer.transform;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 point = space.TransformPoint(corner);
                    float along = Vector3.Dot(point - eye, forward);
                    if (along <= furthest)
                        continue;
                    furthest = along;
                    tip = point;
                }
            }
            return furthest > 0f;
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

        // After the holder applied ViewKick: the camera and this model are where they'll be drawn this frame.
        // Spawn things that must line up with the model here (bullets from the muzzle).
        public virtual void LateTick() { }

        protected virtual void OnBound() { }
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }
    }
}
