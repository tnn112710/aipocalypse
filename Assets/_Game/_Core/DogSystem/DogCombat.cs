using System.Collections.Generic;
using MB13.Aipocalypse.Core.EnemySystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // What the dog knows about one enemy (DogFight fills these from the EnemyRegistry each think).
    public readonly struct FoeView
    {
        public readonly int Id;
        public readonly Vector3 Position, Forward;
        public readonly bool Ranged, AwareOfPlayer, AttackingPlayer, Swinging;
        public readonly float HealthFraction;
        // Its weight class (index into DogSettings.Classes).
        public readonly int Class;
        public readonly bool Alive;
        // Inside the home yard (a raider that got in).
        public readonly bool InYard;

        public FoeView(int id, Vector3 position, Vector3 forward, bool ranged, bool awareOfPlayer, bool attackingPlayer, bool swinging,
            float healthFraction, int @class, bool alive, bool inYard = false)
        {
            Id = id;
            Position = position;
            Forward = forward;
            Ranged = ranged;
            AwareOfPlayer = awareOfPlayer;
            AttackingPlayer = attackingPlayer;
            Swinging = swinging;
            HealthFraction = healthFraction;
            Class = @class;
            Alive = alive;
            InYard = inYard;
        }
    }

    // What the dog knows about one farm animal while herding.
    public readonly struct HerdView
    {
        public readonly int Id;
        public readonly Vector3 Position;
        public readonly float Speed;
        public readonly bool Home;
        public readonly float NoProgressFor;

        public HerdView(int id, Vector3 position, float speed, bool home, float noProgressFor)
        {
            Id = id;
            Position = position;
            Speed = speed;
            Home = home;
            NoProgressFor = noProgressFor;
        }
    }

    // The dog's fight rules in plain C# (tested): which enemy it goes for, whether it may bite it, when there's an opening.
    public static class DogCombat
    {
        // Sent (`sentId`): that one, while alive and within SentChaseRange of the player. Passive: nothing else. Active: enemies
        // aware of the player within EngageRange of the player; with `homeDefence` (a raid, the dog in the yard) raiders inside
        // the yard only, aware or not (outside the fence they're only barked at). Ranged first, then those attacking the
        // player, then the nearest to the player. −1: none.
        public static int PickTarget(IReadOnlyList<FoeView> foes, Vector3 player, DogStance stance, int sentId, bool homeDefence, DogStatSheet stats)
        {
            if (sentId >= 0)
            {
                float chase = stats.Get(DogStat.SentChaseRange);
                for (int i = 0; i < foes.Count; i++)
                    if (foes[i].Id == sentId && foes[i].Alive && FlatMath.Length(foes[i].Position - player) <= chase)
                        return sentId;
                return -1;
            }
            if (stance == DogStance.Passive)
                return -1;
            float engage = stats.Get(DogStat.EngageRange);
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < foes.Count; i++)
            {
                FoeView foe = foes[i];
                if (!foe.Alive)
                    continue;
                float distance = FlatMath.Length(foe.Position - player);
                bool engageable = homeDefence ? foe.InYard : foe.AwareOfPlayer && distance <= engage;
                if (!engageable)
                    continue;
                // Lower is better: ranged, then attacking the player, then near the player.
                float score = (foe.Ranged ? 0f : 1000f) + (foe.AttackingPlayer ? 0f : 500f) + distance;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = foe.Id;
                }
            }
            return best;
        }

        public static bool CanBite(in FoeView foe, DogStatSheet stats) => foe.Class <= stats.GetClass(DogStat.MaxBiteClass);

        // An enemy's weight class: its stagger profile's place in `classes`. One not in the list (or none) counts as the
        // heaviest, so a new kind (a boss) is never bitten or taken down until it's given a class.
        public static int ClassOf(StaggerProfile[] classes, StaggerProfile profile)
        {
            if (classes == null || classes.Length == 0)
                return 0;
            int index = profile != null ? System.Array.IndexOf(classes, profile) : -1;
            return index < 0 ? classes.Length - 1 : index;
        }

        // A bite gives an enemy within `radius` of the dog (flat distance) a reason to swing back.
        public static bool Provokes(Vector3 dogAt, Vector3 enemyAt, float radius)
        {
            Vector3 d = enemyAt - dogAt;
            d.y = 0f;
            return d.sqrMagnitude <= radius * radius;
        }

        // A moment to dart in: the target faces away (more than `openingAngle`), is swinging at someone else, or the dog has
        // circled long enough (`circleWait` × the cooldown).
        public static bool IsOpening(in FoeView foe, Vector3 dog, float circledFor, float cooldown, float openingAngle, float circleWait)
        {
            if (foe.Swinging || circledFor >= circleWait * cooldown)
                return true;
            Vector3 toDog = dog - foe.Position;
            toDog.y = 0f;
            Vector3 forward = foe.Forward;
            forward.y = 0f;
            return toDog.sqrMagnitude > 1e-4f && Vector3.Angle(forward, toDog) > openingAngle;
        }

        // The nearest enemy within `range` of the dog that hasn't noticed the player (a warning growl), or −1.
        public static int WarnTarget(IReadOnlyList<FoeView> foes, Vector3 dog, float range)
        {
            int best = -1;
            float bestDistance = range;
            for (int i = 0; i < foes.Count; i++)
            {
                if (!foes[i].Alive || foes[i].AwareOfPlayer)
                    continue;
                float distance = FlatMath.Length(foes[i].Position - dog);
                if (distance <= bestDistance)
                {
                    bestDistance = distance;
                    best = foes[i].Id;
                }
            }
            return best;
        }
    }
}
