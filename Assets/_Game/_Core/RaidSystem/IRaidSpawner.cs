using Aipocalypse.Core.EnemySystem;

namespace Aipocalypse.Core.RaidSystem
{
    // The world side of a raid (RaidSpawner): where its groups appear, how many of them are alive, and how they leave.
    // RaidDirector stays pure timing logic, so tests drive it with a fake.
    public interface IRaidSpawner
    {
        // Raiders this raid spawned that are still alive.
        int Alive { get; }

        // Gets the pool ready for `plan` with at most `instances` of a card in play at once (alive raiders and lying bodies).
        void Prepare(RaidPlan plan, int instances);

        // One group of `count` enemies of `card` from the zone with id `zone`; `announce` plays the card's arrival cue at the
        // group's spawn point. Returns how many spawned: 0 when no spawn point is ready yet (try again later).
        int SpawnGroup(EnemySpawnCard card, int count, string zone, bool announce);

        // The raiders leave: all at once (`now`, behind the death screen or by hand), or each as soon as the player can't see
        // it (dawn). Either way they stop counting as Alive.
        void Withdraw(bool now);
    }
}
