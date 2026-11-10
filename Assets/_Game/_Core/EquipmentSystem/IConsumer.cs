namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // On an actor's root: takes what a consumable does (the player: PlayerEffects and its health).
    public interface IConsumer
    {
        // Time.time of the last hit that hurt; a hit during a use cancels it.
        float LastHurtTime { get; }

        // Why it can't be used now ("Health full"), or null.
        string CannotUse(ConsumableDefinition item);

        void Use(ConsumableDefinition item);
    }
}
