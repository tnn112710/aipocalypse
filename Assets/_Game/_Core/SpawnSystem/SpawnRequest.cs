using UnityEngine;

namespace MB13.Aipocalypse.Core.SpawnSystem
{
    public enum DespawnMode
    {
        // Back to the pool at once.
        Now,
        // Stops counting as alive, keeps acting while the player sees it and goes back to the pool once out of sight.
        WhenUnseen,
    }

    // What SpawnService.Spawn brings in: what (a card, a squad or a pick from a table), how many, where (an area to pick a
    // spot in, out of the player's view, or a point to place it at) and how the group stands. Build one with Group, At, Of or
    // From, then With / WithRules / Facing.
    public struct SpawnRequest
    {
        public SpawnCard Card;
        public SpawnSquad Squad;
        public SpawnTable Table;
        public SpawnConditions Conditions;
        // Members of a card or table pick; 0 = the card's group size. A squad has its own.
        public int Count;
        // Where to pick a spot; null = at Position.
        public ISpawnArea Area;
        public Vector3 Position;
        // The way the group faces; zero = towards the area's origin.
        public Vector3 Direction;
        // Null = a scatter (SpawnSystemSettings.GroupRadius / GroupSpacing), or the squad's.
        public Formation Formation;
        // Null = SpawnSystemSettings.DefaultRules.
        public SpawnRules Rules;
        // Anything the spawner wants its members to know (SpawnHandle.Tag, read in ISpawnable.OnSpawned): enemies take
        // SpawnOrders. Null = nothing.
        public object Tag;

        // `count` of `card` somewhere in `area`, out of the player's view when it can.
        public static SpawnRequest Group(SpawnCard card, ISpawnArea area, int count = 0) =>
            new() { Card = card, Area = area, Count = count };

        // `count` of `card` round `position` (placed by its medium there, seen or not).
        public static SpawnRequest At(SpawnCard card, Vector3 position, int count = 1) =>
            new() { Card = card, Position = position, Count = count };

        // A squad somewhere in `area`.
        public static SpawnRequest Of(SpawnSquad squad, ISpawnArea area) => new() { Squad = squad, Area = area };

        // A card picked from `table` for `conditions` (nothing spawns when it picks nothing), somewhere in `area`.
        public static SpawnRequest From(SpawnTable table, in SpawnConditions conditions, ISpawnArea area, int count = 0) =>
            new() { Table = table, Conditions = conditions, Area = area, Count = count };

        public SpawnRequest With(Formation formation)
        {
            SpawnRequest request = this;
            request.Formation = formation;
            return request;
        }

        public SpawnRequest WithRules(SpawnRules rules)
        {
            SpawnRequest request = this;
            request.Rules = rules;
            return request;
        }

        public SpawnRequest Facing(Vector3 direction)
        {
            SpawnRequest request = this;
            request.Direction = direction;
            return request;
        }

        public SpawnRequest WithTag(object tag)
        {
            SpawnRequest request = this;
            request.Tag = tag;
            return request;
        }
    }
}
