using System;

namespace Aipocalypse.Core.EquipmentSystem
{
    public enum SwitchPhase
    {
        Empty,      // nothing in hand
        Holstering, // lowering the held item to swap it
        Equipping,  // raising the held item
        Ready,      // fully raised, usable
    }

    // Timing of swapping what's in hand. Raise goes 1 → 0 while holstering the old item, swaps at 0, then 0 → 1 for the new one.
    // Changing the target mid-switch just turns around: no queue, no restart.
    public sealed class EquipmentSwitcher
    {
        // previous, current; fires at the bottom of a swap, when the model should change
        public event Action<ItemInstance, ItemInstance> HeldChanged;
        public event Action<ItemInstance> BecameReady;

        public ItemInstance Held { get; private set; }
        public ItemInstance Target { get; private set; }
        public float Raise { get; private set; }

        public bool IsReady => Phase == SwitchPhase.Ready;

        public SwitchPhase Phase
        {
            get
            {
                if (Held != Target)
                    return SwitchPhase.Holstering;
                if (Held == null)
                    return SwitchPhase.Empty;
                return Raise < 1f ? SwitchPhase.Equipping : SwitchPhase.Ready;
            }
        }

        public void SetTarget(ItemInstance item)
        {
            if (item != null && !item.IsEquippable)
                throw new ArgumentException($"{item.Definition.name} can't be held.");
            Target = item;
        }

        // Skips the animation (spawn, loading a save).
        public void EquipImmediately(ItemInstance item)
        {
            SetTarget(item);
            Swap();
            Raise = item != null ? 1f : 0f;
            if (item != null)
                BecameReady?.Invoke(item);
        }

        public void Tick(float deltaTime, float speedMultiplier)
        {
            float step = deltaTime * speedMultiplier;

            if (Held != Target)
            {
                Raise = Approach(Raise, 0f, step, Held?.Equippable.HolsterTime ?? 0f);
                if (Raise <= 0f)
                    Swap();
                return;
            }

            if (Held == null || Raise >= 1f)
                return;

            Raise = Approach(Raise, 1f, step, Held.Equippable.EquipTime);
            if (Raise >= 1f)
                BecameReady?.Invoke(Held);
        }

        void Swap()
        {
            ItemInstance previous = Held;
            Held = Target;
            if (previous != Held)
                HeldChanged?.Invoke(previous, Held);
        }

        // Moves across the full 0..1 range in `duration` seconds; zero duration is instant.
        static float Approach(float value, float target, float step, float duration)
        {
            if (duration <= 0f)
                return target;
            float delta = step / duration;
            return target > value ? Math.Min(value + delta, target) : Math.Max(value - delta, target);
        }
    }
}
