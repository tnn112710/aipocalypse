using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // A consumable in hand (first aid kit, bandage, shot): a fresh press of the attack button starts using it, holding it
    // for UseSeconds finishes. While using, the holder walks slower and can't sprint. Letting go, switching away or a hit
    // that hurts cancels; the effects go to the holder (IConsumer).
    public sealed class HeldConsumable : HeldItem
    {
        // Seconds a refusal ("Health full") stays readable.
        const float RefusalSeconds = 1.5f;

        float _elapsed = -1f;
        float _startedAt;
        string _refusal;
        float _refusedAt = float.NegativeInfinity;

        public ConsumableDefinition Consumable => Item?.Definition as ConsumableDefinition;
        public bool IsUsing => _elapsed >= 0f;
        // 0..1 through the current use, or -1.
        public float UseProgress => IsUsing && Consumable != null ? Mathf.Clamp01(_elapsed / Consumable.UseSeconds) : -1f;
        // Why the last try didn't start, for a moment after it.
        public string Refusal => Time.time - _refusedAt < RefusalSeconds ? _refusal : null;

        public override float MoveSpeedScale => IsUsing && Consumable != null ? Consumable.UseMoveSpeedScale : 1f;
        public override bool StopsSprint(bool useHeld, bool aimHeld) => useHeld;

        public override void Tick(in HeldItemInput input, float deltaTime)
        {
            ConsumableDefinition definition = Consumable;
            if (definition == null || Owner is not IConsumer consumer || !input.IsReady || !input.UseHeld)
            {
                Cancel();
                return;
            }
            if (!IsUsing)
            {
                // Holding the button on after a use doesn't start the next one.
                if (!input.UsePressed)
                    return;
                string refusal = consumer.CannotUse(definition);
                if (refusal != null)
                {
                    _refusal = refusal;
                    _refusedAt = Time.time;
                    return;
                }
                _elapsed = 0f;
                _startedAt = Time.time;
                return;
            }
            if (consumer.LastHurtTime > _startedAt)
            {
                Cancel();
                return;
            }
            _elapsed += deltaTime;
            if (_elapsed >= definition.UseSeconds)
                Finish(consumer, definition);
        }

        public void Cancel() => _elapsed = -1f;

        void Finish(IConsumer consumer, ConsumableDefinition definition)
        {
            Cancel();
            ItemInstance item = Item;
            consumer.Use(definition);
            // The last use (or a single-use item) leaves the slot: the rig then drops this model.
            if (item is ConsumableInstance multiUse)
            {
                if (multiUse.UseOnce())
                    item.Consume(item.Count);
            }
            else
            {
                item.Consume();
            }
        }

        protected override void OnHidden() => Cancel();
    }
}
