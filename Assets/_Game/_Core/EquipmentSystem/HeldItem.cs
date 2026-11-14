using System.Collections.Generic;
using MB13.Aipocalypse.Core.HandSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // Root of an item's first-person model. Subclasses add what the item does in hand (fire, swing, use).
    // Position the model as a child: the root sits on the rig's hand point.
    [DisallowMultipleComponent]
    public class HeldItem : MonoBehaviour
    {
        readonly List<Renderer> _renderers = new();

        public ItemInstance Item { get; private set; }
        // Where the first-person hands hold this item; null = no hands.
        public ItemHands Hands { get; private set; }
        protected IEquipmentOwner Owner { get; private set; }

        public virtual ViewKick ViewKick => default;
        // Degrees the holder's look turns this frame and keeps (recoil to pull against): x = pitch (negative = up), y = yaw.
        public virtual Vector2 LookTurn => default;
        public virtual bool IsAiming => false;
        // 0 at rest, 1 fully aimed down sights; eases with the aim pose.
        public virtual float AimWeight => 0f;
        // Field of view multiplier the holder applies (aiming down sights): 1 = none.
        public virtual float Zoom => 1f;
        // The aim part of Zoom alone (no shot punch): the held model's own lens zooms by it, so the sights grow on screen.
        public virtual float AimZoom => 1f;
        // False while an obstacle pose would interfere with the item's motion.
        public virtual bool KeepsClear => true;
        // Degrees shots may go off the aim (airborne spread plus pellet spread); the crosshair shows it.
        public virtual float Spread => 0f;
        // Multiplies the holder's move speed (using a consumable slows the walk): 1 = none.
        public virtual float MoveSpeedScale => 1f;
        // Multiplies the aim and model sway on top of the item's HoldFeel (a magnified scope calms it): 1 = none.
        public virtual float SwayScale => 1f;
        // A full scope's view over the screen while aiming through it (the HUD draws it); none by default.
        public virtual ScopeView Scope => default;

        // Whether the holder's attack / aim input ends a sprint.
        public virtual bool StopsSprint(bool useHeld, bool aimHeld) => false;

        public void Bind(ItemInstance item, IEquipmentOwner owner)
        {
            Item = item;
            Owner = owner;
            Hands = GetComponent<ItemHands>();
            RefreshRenderers();
            OnBound();
        }

        // Collects the model's meshes for TryGetTip; again after parts are added to it. Before anything gets parented to
        // the model (muzzle flashes), so only the model's own meshes count.
        protected void RefreshRenderers()
        {
            _renderers.Clear();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer or SkinnedMeshRenderer)
                    _renderers.Add(renderer);
            }
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
