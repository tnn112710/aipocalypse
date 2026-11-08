using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.RandomSystem;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.EnvironmentSystem
{
    // Plans the weather ahead in world-clock hours: picks the next sky weather by its period's weights (WeatherRules), walks
    // there one step of cloud cover at a time, rolls how long it lasts and its wind (which also moves one level per step).
    // A weather never runs into a period or forced weather that doesn't allow it (weight 0): it ends early, and the walk
    // away finishes in time. Plain C#: WeatherSchedule drives it from the clock; tests and the rules inspector run it directly.
    public sealed class WeatherPlanner
    {
        public readonly struct Forced
        {
            public readonly double Start;
            // PositiveInfinity = held until released.
            public readonly double End;
            public readonly SkyWeather Sky;
            // Null = rolled from the sky's wind weights.
            public readonly WindLevel? Wind;

            public Forced(double start, double end, SkyWeather sky, WindLevel? wind)
            {
                Start = start;
                End = end;
                Sky = sky;
                Wind = wind;
            }

            public bool Contains(double time) => time >= Start && time < End;
        }

        const double Never = double.PositiveInfinity;
        const double Epsilon = 1e-6;
        const double ShortestSpell = 0.25;

        // Cloud cover up to rain, in order; Fog sits beside the first three and reaches the rest through Cloudy.
        static readonly SkyWeather[] Ladder =
        {
            SkyWeather.Clear, SkyWeather.PartlyCloudy, SkyWeather.Cloudy, SkyWeather.Overcast,
            SkyWeather.LightRain, SkyWeather.MediumRain, SkyWeather.HeavyRain,
        };
        static readonly SkyWeather[] Skies = (SkyWeather[])Enum.GetValues(typeof(SkyWeather));
        static readonly WindLevel[] Winds = (WindLevel[])Enum.GetValues(typeof(WindLevel));

        readonly WeatherRules _rules;
        readonly RandomStream _random;
        readonly List<WeatherSpell> _plan = new();
        readonly List<Forced> _forced = new();
        readonly HashSet<int> _bloodMoons = new();
        readonly HashSet<int> _eclipses = new();
        readonly List<SkyWeather> _walk = new();
        readonly float[] _weights = new float[Skies.Length];
        readonly float[] _windWeights = new float[Winds.Length];
        readonly float[] _durationWeights = new float[16];

        // Where the plan ends and what the weather is there.
        double _end;
        SkyWeather _sky;
        WindLevel _wind;
        WindLevel _windTarget;
        double _windDue;
        // The last time asked for (At, Restart).
        double _now;

        public WeatherPlanner(WeatherRules rules, RandomStream random)
        {
            _rules = rules;
            _random = random;
        }

        public IReadOnlyList<WeatherSpell> Plan => _plan;
        public IReadOnlyList<Forced> ForcedSpells => _forced;
        public IReadOnlyCollection<int> BloodMoons => _bloodMoons;
        public IReadOnlyCollection<int> Eclipses => _eclipses;
        public double PlanEnd => _end;
        public double NightStart => _rules.NightStart;
        // A weather held until released (Hold) is on now.
        public bool IsHeld => TryForcedAt(_now, out Forced forced) && double.IsPositiveInfinity(forced.End);

        // Drops the plan and starts again from this weather now, which lasts a rolled duration (if allowed now).
        public void Restart(double now, SkyWeather sky, WindLevel wind)
        {
            Reset(now, sky, wind);
            if (TryForcedAt(now, out _))
                return;
            double hours = LeaveInTime(sky, now, RollDuration(sky));
            if (hours > Epsilon)
                Emit(sky, hours, false);
        }

        void Reset(double now, SkyWeather sky, WindLevel wind)
        {
            _plan.Clear();
            _now = now;
            _end = now;
            _sky = sky;
            _wind = wind;
            _windTarget = wind;
            _windDue = now;
        }

        // The planned weather at `time` (not before the plan's start); drops what has passed and keeps PlanHours planned ahead.
        public WeatherSpell At(double time)
        {
            _now = time;
            Extend(time + Math.Max(_rules.PlanHours, 1f));
            int passed = 0;
            while (passed < _plan.Count - 1 && _plan[passed].End <= time)
                passed++;
            if (passed > 0)
                _plan.RemoveRange(0, passed);
            return _plan[0];
        }

        public void Extend(double until)
        {
            while (_end < until)
            {
                double before = _end;
                if (TryForcedAt(_end, out Forced forced))
                    EmitForced(forced, until);
                else
                    PlanNext();
                if (_end <= before)
                    break;
            }
        }

        // The Blood Moon night starting on `day` (from NightStart); off again with on = false. Replans from the last time asked.
        public void MarkBloodMoon(int day, bool on = true)
        {
            if (on ? _bloodMoons.Add(day) : _bloodMoons.Remove(day))
                Replan();
        }

        // An eclipse over the daytime of `day`.
        public void MarkEclipse(int day, bool on = true)
        {
            if (on ? _eclipses.Add(day) : _eclipses.Remove(day))
                Replan();
        }

        // This weather from `from` for `hours` (PositiveInfinity = until Release), whatever the rules say; it replaces forced
        // weathers it overlaps. From now or earlier: it starts at once (WeatherService blends to it). Later: the plan walks
        // there so it's in place on time, and carries on from it afterwards.
        public void Force(SkyWeather sky, WindLevel? wind, double from, double hours)
        {
            if (hours <= 0d)
                return;
            from = Math.Max(from, _now);
            double end = from + hours;
            _forced.RemoveAll(f => f.Start < end && f.End > from);
            _forced.Add(new Forced(from, end, sky, wind));
            if (from > _now + Epsilon)
            {
                Replan();
                return;
            }
            Reset(_now, sky, wind ?? _wind);
            if (wind == null)
                RollWind(sky);
        }

        // Ends the forced weathers running now; the rules take over from the current weather.
        public void Release()
        {
            if (_forced.RemoveAll(f => f.Contains(_now)) > 0)
                Replan();
        }

        public WeatherPeriod PeriodAt(double time)
        {
            double nightStart = _rules.NightStart;
            double nightEnd = _rules.NightEnd;
            double hour = time - Math.Floor(time / WorldTime.HoursPerDay) * WorldTime.HoursPerDay;
            bool night = nightStart > nightEnd ? hour >= nightStart || hour < nightEnd : hour >= nightStart && hour < nightEnd;
            if (night)
            {
                int startDay = (int)Math.Floor((time - nightStart) / WorldTime.HoursPerDay) + 1;
                return _bloodMoons.Contains(startDay) ? WeatherPeriod.BloodMoon : WeatherPeriod.Night;
            }
            int day = (int)Math.Floor(time / WorldTime.HoursPerDay) + 1;
            return _eclipses.Contains(day) ? WeatherPeriod.Eclipse : WeatherPeriod.Day;
        }

        // Whether this weather may show at `time`: the forced weather there, else a weight above 0 in the period's table.
        public bool Allows(SkyWeather sky, double time) =>
            TryForcedAt(time, out Forced forced) ? forced.Sky == sky : _rules.Weight(PeriodAt(time), sky) > 0f;

        // In-between weathers from `from` to `to`, both left out.
        public static void Path(SkyWeather from, SkyWeather to, List<SkyWeather> into)
        {
            into.Clear();
            if (from == to)
                return;
            if (from == SkyWeather.Fog)
            {
                if (NextToFog(to))
                    return;
                into.Add(SkyWeather.Cloudy);
                AddLadder(SkyWeather.Cloudy, to, into);
                return;
            }
            if (to == SkyWeather.Fog)
            {
                if (NextToFog(from))
                    return;
                AddLadder(from, SkyWeather.Cloudy, into);
                into.Add(SkyWeather.Cloudy);
                return;
            }
            AddLadder(from, to, into);
        }

        public static int Steps(SkyWeather from, SkyWeather to)
        {
            if (from == to)
                return 0;
            if (from == SkyWeather.Fog)
                return NextToFog(to) ? 0 : 1 + Gap(SkyWeather.Cloudy, to);
            if (to == SkyWeather.Fog)
                return NextToFog(from) ? 0 : Gap(from, SkyWeather.Cloudy) + 1;
            return Gap(from, to);
        }

        static bool NextToFog(SkyWeather sky) => sky is SkyWeather.Clear or SkyWeather.PartlyCloudy or SkyWeather.Cloudy;

        static int Gap(SkyWeather from, SkyWeather to) =>
            from == to ? 0 : Math.Abs(Array.IndexOf(Ladder, from) - Array.IndexOf(Ladder, to)) - 1;

        static void AddLadder(SkyWeather from, SkyWeather to, List<SkyWeather> into)
        {
            int i = Array.IndexOf(Ladder, from);
            int j = Array.IndexOf(Ladder, to);
            if (i == j)
                return;
            int step = j > i ? 1 : -1;
            for (int k = i + step; k != j; k += step)
                into.Add(Ladder[k]);
        }

        void PlanNext()
        {
            double time = _end;
            WeatherPeriod period = PeriodAt(time);
            for (int i = 0; i < Skies.Length; i++)
            {
                SkyWeather sky = Skies[i];
                float weight = _rules.Weight(period, sky);
                _weights[i] = sky != _sky && weight > 0f && Fits(_sky, sky, time) ? weight : 0f;
            }
            int index = WeightedPick.Index(_weights, _random.Value);
            // Nothing fits: hurry to the fallback at the shortest step.
            bool urgent = index < 0;
            SkyWeather target = urgent ? Fallback(time) : Skies[index];

            RollWind(target);
            Path(_sky, target, _walk);
            foreach (SkyWeather step in _walk)
                if (!Emit(step, urgent ? StepMin : _random.Range(StepMin, StepMax), false))
                    return;
            // Arrived before the fallback is allowed (a forced weather the rules never pick): wait one step short.
            double allowed = FirstAllowed(target, _end);
            if (allowed > _end + Epsilon)
            {
                Emit(_sky, allowed - _end, false);
                return;
            }

            Emit(target, Math.Max(ShortestSpell, LeaveInTime(target, _end, RollDuration(target))), false);
        }

        // `hours` of this weather from `start`, shortened so the walk away from it ends before it stops being allowed.
        double LeaveInTime(SkyWeather sky, double start, double hours)
        {
            double forbidden = FirstForbidden(sky, start, start + hours + ExitWindow);
            return forbidden < Never ? Math.Min(hours, forbidden - start - ExitHours(sky, forbidden)) : hours;
        }

        // Can `target` be walked to from `time` and last ShortestHours without anything showing where it isn't allowed?
        // Steps are checked over every moment they could cover; the walk out of `target` is booked at the longest step.
        bool Fits(SkyWeather from, SkyWeather target, double time)
        {
            Path(from, target, _walk);
            int count = _walk.Count;
            for (int k = 0; k < count; k++)
                if (FirstForbidden(_walk[k], time + k * StepMin, time + (k + 1) * StepMax) < Never)
                    return false;
            double start = time + count * StepMax;
            double forbidden = FirstForbidden(target, time + count * StepMin, start + _rules.LongestHours + ExitWindow);
            return forbidden >= Never || forbidden - start - ExitHours(target, forbidden) >= _rules.ShortestHours;
        }

        // Nothing fits (a weather allowed nowhere near, or marked too late): stay if allowed, else head for the
        // nearest weather allowed where the current one stops being.
        SkyWeather Fallback(double time)
        {
            double forbidden = FirstForbidden(_sky, time, time + _rules.LongestHours);
            if (forbidden >= Never)
                return _sky;
            WeatherPeriod period = PeriodAt(forbidden);
            SkyWeather best = _sky;
            int bestSteps = int.MaxValue;
            float bestWeight = -1f;
            foreach (SkyWeather sky in Skies)
            {
                if (!Allows(sky, forbidden))
                    continue;
                int steps = Steps(_sky, sky);
                float weight = _rules.Weight(period, sky);
                if (steps > bestSteps || (steps == bestSteps && weight <= bestWeight))
                    continue;
                best = sky;
                bestSteps = steps;
                bestWeight = weight;
            }
            return best;
        }

        void EmitForced(Forced forced, double until)
        {
            if (_sky != forced.Sky || _plan.Count == 0 || !_plan[^1].Forced)
            {
                if (forced.Wind.HasValue)
                    _windTarget = forced.Wind.Value;
                else if (_sky != forced.Sky)
                    RollWind(forced.Sky);
            }
            double end = double.IsPositiveInfinity(forced.End) ? Math.Max(until, _end + ShortestSpell) : forced.End;
            Emit(forced.Sky, end - _end, true);
        }

        // Adds `hours` of this weather at the plan's end, the wind stepping toward its target. Stops at the next forced
        // weather's start (returns false then).
        bool Emit(SkyWeather sky, double hours, bool forced)
        {
            double end = _end + Math.Max(hours, Epsilon);
            double nextForced = NextForcedStart(_end);
            bool cut = nextForced < end;
            if (cut)
                end = nextForced;
            _sky = sky;
            while (_end < end - Epsilon)
            {
                if (_wind != _windTarget && _windDue <= _end + Epsilon)
                {
                    _wind += _windTarget > _wind ? 1 : -1;
                    _windDue = _end + _random.Range(StepMin, StepMax);
                }
                double segmentEnd = _wind != _windTarget ? Math.Min(end, _windDue) : end;
                Add(new WeatherSpell(_end, segmentEnd, sky, _wind, forced));
                _end = segmentEnd;
            }
            _end = Math.Max(_end, end);
            return !cut;
        }

        void Add(WeatherSpell spell)
        {
            if (_plan.Count > 0)
            {
                WeatherSpell last = _plan[^1];
                if (last.Sky == spell.Sky && last.Wind == spell.Wind && last.Forced == spell.Forced && Math.Abs(last.End - spell.Start) < Epsilon)
                {
                    _plan[^1] = last.WithEnd(spell.End);
                    return;
                }
            }
            _plan.Add(spell);
        }

        // Keeps the weather on now (cut short if the new rules forbid it soon, ended if it was forced and isn't any more)
        // and plans the rest again.
        void Replan()
        {
            int index = _plan.FindIndex(s => s.End > _now);
            if (index < 0)
            {
                _end = Math.Max(_end, _now);
                return;
            }
            WeatherSpell current = _plan[index];
            _plan.RemoveRange(index + 1, _plan.Count - index - 1);
            double end = current.End;
            if (current.Forced && !(TryForcedAt(_now, out Forced forced) && forced.Sky == current.Sky))
                end = _now;
            end = _now + LeaveInTime(current.Sky, _now, end - _now);
            end = Math.Max(_now, Math.Min(end, NextForcedStart(_now)));
            if (end <= current.Start + Epsilon)
                _plan.RemoveAt(index);
            else
                _plan[index] = current.WithEnd(end);
            _end = end;
            _sky = current.Sky;
            _wind = current.Wind;
            _windTarget = current.Wind;
            _windDue = end;
        }

        double RollDuration(SkyWeather sky)
        {
            WeatherRules.SkyRule rule = _rules.Rule(sky);
            int count = Math.Min(_rules.DurationHours.Count, _durationWeights.Length);
            for (int i = 0; i < count; i++)
                _durationWeights[i] = rule != null ? rule.DurationWeight(i) : 0f;
            for (int i = count; i < _durationWeights.Length; i++)
                _durationWeights[i] = 0f;
            int index = WeightedPick.Index(_durationWeights, _random.Value);
            if (index < 0)
                return _rules.ShortestHours;
            float jitter = _rules.DurationJitter;
            return Math.Max(ShortestSpell, _rules.DurationHours[index] + _random.Range(-jitter, jitter));
        }

        void RollWind(SkyWeather sky)
        {
            WeatherRules.SkyRule rule = _rules.Rule(sky);
            if (rule == null)
                return;
            for (int i = 0; i < Winds.Length; i++)
                _windWeights[i] = rule.WindWeight(Winds[i]);
            int index = WeightedPick.Index(_windWeights, _random.Value);
            if (index >= 0)
                _windTarget = Winds[index];
        }

        // The first moment in [from, to) this weather isn't allowed; PositiveInfinity if none.
        double FirstForbidden(SkyWeather sky, double from, double to)
        {
            if (!Allows(sky, from))
                return from;
            for (double boundary = NextBoundary(from); boundary < to; boundary = NextBoundary(boundary))
                if (!Allows(sky, boundary))
                    return boundary;
            return Never;
        }

        // The first moment from `from` (within two days) this weather is allowed; `from` if never.
        double FirstAllowed(SkyWeather sky, double from)
        {
            if (Allows(sky, from))
                return from;
            double horizon = from + 2d * WorldTime.HoursPerDay;
            for (double boundary = NextBoundary(from); boundary < horizon; boundary = NextBoundary(boundary))
                if (Allows(sky, boundary))
                    return boundary;
            return from;
        }

        // Game hours the walk from `sky` to the nearest weather allowed at `time` takes at the longest step.
        double ExitHours(SkyWeather sky, double time)
        {
            int fewest = 0;
            bool any = false;
            foreach (SkyWeather other in Skies)
            {
                if (!Allows(other, time))
                    continue;
                int steps = Steps(sky, other);
                fewest = any ? Math.Min(fewest, steps) : steps;
                any = true;
            }
            return fewest * StepMax;
        }

        // The next moment after `time` the allowed weathers can change: night start / end, a forced weather's start or end.
        double NextBoundary(double time)
        {
            double next = Never;
            double dayStart = Math.Floor(time / WorldTime.HoursPerDay) * WorldTime.HoursPerDay;
            for (int day = 0; day < 2; day++)
            {
                next = Later(next, time, dayStart + day * WorldTime.HoursPerDay + _rules.NightStart);
                next = Later(next, time, dayStart + day * WorldTime.HoursPerDay + _rules.NightEnd);
            }
            foreach (Forced forced in _forced)
            {
                next = Later(next, time, forced.Start);
                next = Later(next, time, forced.End);
            }
            return next;
        }

        double NextForcedStart(double time)
        {
            double next = Never;
            foreach (Forced forced in _forced)
                next = Later(next, time, forced.Start);
            return next;
        }

        static double Later(double best, double after, double candidate) => candidate > after + Epsilon && candidate < best ? candidate : best;

        bool TryForcedAt(double time, out Forced found)
        {
            foreach (Forced forced in _forced)
            {
                if (!forced.Contains(time))
                    continue;
                found = forced;
                return true;
            }
            found = default;
            return false;
        }

        // The longest walk between any two weathers, at the longest step.
        double ExitWindow => Ladder.Length * StepMax;

        float StepMin => Mathf.Max(0.05f, Mathf.Min(_rules.StepHours.x, _rules.StepHours.y));
        float StepMax => Mathf.Max(StepMin, Mathf.Max(_rules.StepHours.x, _rules.StepHours.y));
    }
}
