using MB13.Aipocalypse.Core.Entities;

namespace MB13.Aipocalypse.Core.EnemySystem
{
    // What a spawner tells the enemies it brings (SpawnRequest.WithTag): what they go for before they notice the player
    // (raiders: the Farm; none = they roam where they are), and whether they're elite whatever their kind's chance says.
    public readonly struct SpawnOrders
    {
        public readonly TargetKind? Objective;
        public readonly bool Elite;

        public SpawnOrders(TargetKind? objective, bool elite = false)
        {
            Objective = objective;
            Elite = elite;
        }

        public static SpawnOrders Raid => new(TargetKind.Farm);
    }
}
