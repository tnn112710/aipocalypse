using MB13.Aipocalypse.Core.EnvironmentSystem;
using MB13.Aipocalypse.Core.SleepSystem;
using MB13.Aipocalypse.Core.SpawnSystem;
using Pathfinding;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // What the dog senses of the world, owned and ticked by DogManager: whether it can walk to the player and the fence
    // between them, indoors or not, deep water (swimming), rain and shelter (soaked, then a shake), the floor spot by the
    // player's bed.
    public sealed class DogSenses
    {
        const float PathCheckInterval = 0.5f;
        // How far up the body the water may come before it swims (the model floats with its back at the surface).
        const float SwimBodyDepth = 1.1f;
        const float ShakeWindow = 20f;

        readonly DogRoster _roster;
        readonly DogSettings _settings;
        readonly IWaterLevel _water;
        readonly WeatherService _weather;
        readonly SleepService _sleep;
        readonly DogWetness _wetness;
        bool _hasPath = true;
        float _nextPathCheck;
        float _fencedFor;
        float _nextRainCheck;
        bool _rainedOn;
        float _shakeUntil = float.NegativeInfinity;
        float _nextShakeTry;
        bool _bedSpotFound;
        Vector3 _bedSpot;

        public DogSenses(DogRoster roster, DogSettings settings, IWaterLevel water, WeatherService weather, SleepService sleep)
        {
            _roster = roster;
            _settings = settings;
            _water = water;
            _weather = weather;
            _sleep = sleep;
            _wetness = new DogWetness(settings);
        }

        // Whether it can walk to the player (checked every PathCheckInterval); false across the shut fence or into an island.
        public bool HasPathToPlayer => _hasPath;

        // The fence between it and a player within ScrambleRange: at once when there's no way round, else after ScrambleAfter
        // (a gap close by gets used first): it scrambles over (DogScramble). A gap (unbuilt or fallen segment, open gate)
        // within GapDetour of the way over is walked through instead.
        public bool WantsScramble { get; private set; }

        public void CheckPath(Dog dog, float now)
        {
            if (_roster.HasPlayer && now >= _nextPathCheck)
            {
                _nextPathCheck = now + PathCheckInterval;
                _hasPath = HasPath(dog.transform.position, _roster.PlayerPosition);
            }
        }

        public void Fenced(Dog dog, float dt)
        {
            Vector3 at = dog.transform.position;
            bool fenced = _roster.HasPlayer && _roster.Yard != null
                && _roster.InYard(at) != _roster.InYard(_roster.PlayerPosition) && FlatMath.Length(at - _roster.PlayerPosition) <= _settings.ScrambleRange
                && _roster.ScrambleCrossing(at, _roster.PlayerPosition, _hasPath) != null;
            _fencedFor = fenced ? _fencedFor + dt : 0f;
            WantsScramble = fenced && (!_hasPath || _fencedFor >= _settings.ScrambleAfter);
        }

        // A wall between them: one in the yard and the other not (the fence), or one in the house and the other not (the house
        // floor is tagged RobotsOnly).
        public bool Separated(Vector3 dog)
        {
            Vector3 player = _roster.PlayerPosition;
            return Indoors(dog) != Indoors(player) || (_roster.Yard != null && _roster.InYard(dog) != _roster.InYard(player));
        }

        static bool Indoors(Vector3 point)
        {
            AstarPath navigation = AstarPath.active;
            GraphNode node = navigation != null ? navigation.GetNearest(point, NearestNodeConstraint.Walkable).node : null;
            return node != null && node.Tag == EnemySystem.NavTags.RobotsOnly;
        }

        static bool HasPath(Vector3 from, Vector3 to)
        {
            AstarPath navigation = AstarPath.active;
            if (navigation == null)
                return true;
            GraphNode a = navigation.GetNearest(from, NearestNodeConstraint.Walkable).node;
            GraphNode b = navigation.GetNearest(to, NearestNodeConstraint.Walkable).node;
            return a == null || b == null || PathUtilities.IsPathPossible(a, b);
        }

        // Deep water: it swims, the model floating with its back at the surface; out again, it shakes itself dry soon after.
        public void Swim(Dog dog, float now)
        {
            Vector3 at = dog.transform.position;
            float level = 0f;
            bool deep = _water != null && _water.TryGetLevel(at, out level) && level - at.y > _settings.SwimDepth;
            if (deep)
                dog.TargetLift = Mathf.Max(0f, level - at.y - SwimBodyDepth);
            if (deep != dog.Swimming)
            {
                dog.SetSwimming(deep);
                dog.ShakePending = !deep;
                if (!deep)
                    _shakeUntil = now + ShakeWindow;
            }
            if (!dog.ShakePending)
                return;
            // Done only once the act really plays: a settle (sit, lie) in the same moment can swallow the trigger.
            if (dog.Shaking || now >= _shakeUntil)
                dog.ShakePending = false;
            else if (now >= _nextShakeTry && dog.Standing && dog.Pose == DogPose.Stand && FlatMath.Length(dog.Legs.Velocity) < 0.5f)
            {
                _nextShakeTry = now + 0.6f;
                dog.Act(DogAct.ShakeWater);
            }
        }

        // Rain soaks it unless something is over it (indoors, its dog house, a roof, a canopy); out of it after a soaking it
        // shakes itself dry (the same shake as after a swim).
        public void Rain(Dog dog, float now, float dt)
        {
            if (_weather == null || dog.Swimming)
                return;
            if (now >= _nextRainCheck)
            {
                _nextRainCheck = now + 0.5f;
                _rainedOn = _weather.Mix.Rain > _settings.RainWetsAbove && !Sheltered(dog);
            }
            if (!_wetness.Tick(dt, _rainedOn))
                return;
            dog.ShakePending = true;
            _shakeUntil = now + ShakeWindow;
        }

        static bool Sheltered(Dog dog)
        {
            Vector3 at = dog.transform.position;
            return dog.InHouse || Indoors(at)
                || Physics.Raycast(at + Vector3.up * 1.6f, Vector3.up, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        // The player at the bed (from the wake-up clock until the hours have passed; then the dog is sent home): a floor spot
        // beside it for a following dog, found once per sleep; `lyingDown` once they lie down.
        public bool TryGetBedSpot(out Vector3 spot, out Vector3 bed, out bool lyingDown)
        {
            spot = bed = default;
            lyingDown = false;
            if (_sleep == null || _sleep.Bed == null || _sleep.Stage is SleepStage.None or SleepStage.Waking)
            {
                _bedSpotFound = false;
                return false;
            }
            lyingDown = _sleep.Stage != SleepStage.Choosing;
            Transform anchor = _sleep.Bed.Anchor;
            bed = anchor.position;
            if (!_bedSpotFound)
            {
                _bedSpotFound = true;
                _bedSpot = BedSide(anchor);
            }
            spot = _bedSpot;
            return true;
        }

        // The walkable floor point closest to the bed among its sides (one is against a wall).
        static Vector3 BedSide(Transform anchor)
        {
            AstarPath navigation = AstarPath.active;
            Vector3 best = anchor.position;
            if (navigation == null)
                return best;
            float bestGap = float.MaxValue;
            Vector3[] sides = { anchor.right, -anchor.right, anchor.forward, -anchor.forward };
            foreach (Vector3 side in sides)
            {
                Vector3 wanted = anchor.position + side * 1.2f;
                NNInfo nearest = navigation.GetNearest(wanted, NearestNodeConstraint.Walkable);
                if (nearest.node == null)
                    continue;
                float gap = Vector3.Distance(nearest.position, wanted);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = nearest.position;
                }
            }
            return best;
        }
    }
}
