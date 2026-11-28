using System;

namespace MB13.Aipocalypse.Core.CampfireSystem
{
    // A campfire's fuel in game hours: wood adds hours, a lit fire burns an hour per game hour (× rain) and goes out at 0.
    // Strength (0..1) sizes the flame, the light and the safe radius: full above ShrinkBelow of the max, then shrinking to
    // 0 with the fuel (user 2026-11-28: constant above 25 %).
    public sealed class CampfireFuel
    {
        readonly float _shrinkBelow;

        public CampfireFuel(float max, float shrinkBelow, float fuel)
        {
            Max = Math.Max(0.01f, max);
            _shrinkBelow = Math.Clamp(shrinkBelow, 0f, 1f);
            Fuel = Math.Clamp(fuel, 0f, Max);
        }

        public float Fuel { get; private set; }
        public float Max { get; }
        public bool Lit { get; private set; }
        public float Share => Fuel / Max;
        public float Strength => !Lit ? 0f : _shrinkBelow <= 0f || Share >= _shrinkBelow ? 1f : Share / _shrinkBelow;

        // Burned out (not put out).
        public event Action WentOut;

        // Room for all of it: a log that would pass the max isn't split.
        public bool CanAdd(float hours) => hours > 0f && Fuel + hours <= Max + 1e-4f;

        public bool Add(float hours)
        {
            if (!CanAdd(hours))
                return false;
            Fuel = Math.Min(Max, Fuel + hours);
            return true;
        }

        public bool Light()
        {
            if (Fuel <= 0f)
                return false;
            Lit = true;
            return true;
        }

        public void PutOut() => Lit = false;

        // Burns `hours` × burnScale while lit. True when it went out in this step.
        public bool Advance(float hours, float burnScale)
        {
            if (!Lit || hours <= 0f)
                return false;
            Fuel = Math.Max(0f, Fuel - hours * Math.Max(0f, burnScale));
            if (Fuel > 0f)
                return false;
            Lit = false;
            WentOut?.Invoke();
            return true;
        }

        // A save's state, without events.
        public void Restore(float fuel, bool lit)
        {
            Fuel = Math.Clamp(fuel, 0f, Max);
            Lit = lit && Fuel > 0f;
        }
    }
}
