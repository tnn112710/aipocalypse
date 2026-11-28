using System;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.Stats;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // The player's hunger, thirst and sanity: drains them on game time (NeedsTicker calls Advance), applies their tiers
    // through the NeedConditions stat layer, and their health effects (an empty need drains to a floor, well fed heals out
    // of combat). Built by Player when its tuning has needs settings.
    public sealed class PlayerNeeds : IDisposable
    {
        // A long advance (a sleep skip) is cut into steps this long, so a need emptying mid-way starts its effects there.
        const float MaxStepHours = 0.02f;

        readonly PlayerNeedsSettings _settings;
        readonly NeedConditions _conditions;
        readonly Health _health;
        readonly Stamina _stamina;
        float _sinceHurt = float.PositiveInfinity;

        public Need Hunger { get; }
        public Need Thirst { get; }
        public Need Sanity { get; }
        // The player's stats with the needs' tiers on top: hand this on instead of the layer it was built over.
        public IStatProvider Stats => _conditions;
        public PlayerNeedsSettings Settings => _settings;
        // A cheat: no need drains (refills and cheats still work).
        public bool Frozen { get; set; }
        public float HoursAwake { get; set; }

        public PlayerNeeds(PlayerNeedsSettings settings, IStatProvider below, Health health, Stamina stamina)
        {
            _settings = settings;
            _health = health;
            _stamina = stamina;
            Hunger = new Need(settings.Hunger, below.Get(settings.Hunger.MaxStat, settings.Hunger.BaseMax));
            Thirst = new Need(settings.Thirst, below.Get(settings.Thirst.MaxStat, settings.Thirst.BaseMax));
            Sanity = new Need(settings.Sanity, below.Get(settings.Sanity.MaxStat, settings.Sanity.BaseMax));
            _conditions = new NeedConditions(below, settings.EaseTime);
            _conditions.Track(Hunger, settings.Hunger.PenaltyScaleStat);
            _conditions.Track(Thirst, settings.Thirst.PenaltyScaleStat);
            _conditions.Track(Sanity, settings.Sanity.PenaltyScaleStat);
            _conditions.Changed += OnStatsChanged;
        }

        public IMeter Meter(PlayerMeterId id) => id switch
        {
            PlayerMeterId.Health => _health,
            PlayerMeterId.Stamina => _stamina,
            _ => Need(id),
        };

        public Need Need(PlayerMeterId id) => id switch
        {
            PlayerMeterId.Hunger => Hunger,
            PlayerMeterId.Thirst => Thirst,
            PlayerMeterId.Sanity => Sanity,
            _ => null,
        };

        // Real time: tier easing and the time since the last hit.
        public void Tick(float deltaTime)
        {
            _sinceHurt += deltaTime;
            _conditions.Tick(deltaTime);
        }

        public void OnHurt() => _sinceHurt = 0f;

        // Game time passing, without anything for sanity (no lack of sleep, no darkness). Effort is the stamina spent since the last call.
        public void Advance(float gameHours, bool asleep) => Advance(gameHours, asleep, false, default);

        // Game time passing where the player is: sanity reacts to the hours awake, darkness, lights and comfort (none while asleep).
        public void Advance(float gameHours, bool asleep, in SanityInput sanity) => Advance(gameHours, asleep, true, sanity);

        // Company calms (petting Loki, later comfort food): sanity up, scaled by StatId.SanityRecovery.
        public void Comfort(float sanity)
        {
            if (Sanity.Settings.Enabled && sanity > 0f)
                Sanity.Add(sanity * Mathf.Max(0f, Stats.GetScale(StatId.SanityRecovery)));
        }

        // A sleep ended: the hours awake start again.
        public void OnWoke() => HoursAwake = 0f;

        void Advance(float gameHours, bool asleep, bool exposed, in SanityInput sanity)
        {
            float spent = _stamina.TakeSpent();
            if (!Frozen)
            {
                Use(Hunger, spent);
                Use(Thirst, spent);
                Use(Sanity, spent);
            }
            while (gameHours > 0f)
            {
                float step = Mathf.Min(gameHours, MaxStepHours);
                Step(step, asleep, exposed, sanity);
                gameHours -= step;
            }
        }

        // The hook for anything else that drains a need (an enemy's scream, a curse), scaled by the need's drain stat.
        public void Drain(PlayerMeterId id, float amount, string source)
        {
            Need need = Need(id);
            if (need == null || Frozen || !need.Settings.Enabled || amount <= 0f)
                return;
            need.Add(-amount * Mathf.Max(0f, Stats.GetScale(need.Settings.DrainStat)));
        }

        // After a death: each need under the floor share is raised to it.
        public void ApplyDeathFloor()
        {
            float floor = Mathf.Clamp01(Stats.Get(StatId.DeathNeedFloor, _settings.BaseDeathFloor));
            Raise(Hunger, floor);
            Raise(Thirst, floor);
            Raise(Sanity, floor);
        }

        void Step(float hours, bool asleep, bool exposed, in SanityInput sanity)
        {
            if (!Frozen)
            {
                float timeScale = asleep ? _settings.AsleepDrainScale : 1f;
                Drain(Hunger, hours * timeScale);
                Drain(Thirst, hours * timeScale);
                Drain(Sanity, hours * timeScale);
            }
            if (exposed && !asleep)
                Expose(sanity, hours);
            if (!asleep)
                HoursAwake += hours;
            ApplyHealth(hours);
        }

        // Sanity's own rates (SanityExposure); the drain part scaled by SanityDrain (none while frozen), recovery by SanityRecovery.
        void Expose(in SanityInput sanity, float hours)
        {
            if (!Sanity.Settings.Enabled)
                return;
            SanityExposure.Rates(sanity, HoursAwake, _settings.SanityRules, Sanity.Settings.BaseMax, out float drain, out float recovery);
            float change = recovery * Mathf.Max(0f, Stats.GetScale(StatId.SanityRecovery));
            if (!Frozen)
                change -= drain * Mathf.Max(0f, Stats.GetScale(Sanity.Settings.DrainStat));
            if (change != 0f)
                Sanity.Add(change * hours);
        }

        void Drain(Need need, float hours)
        {
            if (!need.Settings.Enabled || need.Settings.DrainPerHour <= 0f)
                return;
            need.Add(-need.Settings.DrainPerHour * hours * Mathf.Max(0f, Stats.GetScale(need.Settings.DrainStat)));
        }

        void Use(Need need, float staminaSpent)
        {
            if (!need.Settings.Enabled || staminaSpent <= 0f || need.Settings.PerStaminaSpent <= 0f)
                return;
            need.Add(-need.Settings.PerStaminaSpent * staminaSpent * Mathf.Max(0f, Stats.GetScale(need.Settings.DrainStat)));
        }

        // Empty needs drain health down to the floor; regeneration (well fed, upgrades) only out of combat.
        void ApplyHealth(float hours)
        {
            if (_health.IsDead)
                return;
            float drain = 0f;
            float heal = 0f;
            AddHealth(Hunger, ref drain, ref heal);
            AddHealth(Thirst, ref drain, ref heal);
            AddHealth(Sanity, ref drain, ref heal);
            heal = _sinceHurt >= _settings.OutOfCombatSeconds ? heal + Mathf.Max(0f, Stats.Get(StatId.HealthRegen, 0f)) : 0f;

            if (drain > 0f)
            {
                float floor = _health.Max * _settings.HealthFloor;
                if (_health.Current > floor)
                    _health.Set(Mathf.Max(floor, _health.Current - drain * hours));
            }
            if (heal > 0f)
                _health.Heal(heal * hours);
        }

        static void AddHealth(Need need, ref float drain, ref float heal)
        {
            if (!need.Settings.Enabled || need.Tier == null)
                return;
            float perHour = need.Tier.HealthPerHour;
            if (perHour < 0f)
                drain -= perHour;
            else
                heal += perHour;
        }

        static void Raise(Need need, float floor)
        {
            if (need.Settings.Enabled && need.Normalized < floor)
                need.Set(floor * need.Max);
        }

        // A max upgrade keeps each need's share.
        void OnStatsChanged()
        {
            SetMax(Hunger);
            SetMax(Thirst);
            SetMax(Sanity);
        }

        void SetMax(Need need)
        {
            float max = Mathf.Max(1f, Stats.Get(need.Settings.MaxStat, need.Settings.BaseMax));
            if (!Mathf.Approximately(max, need.Max))
                need.SetMax(max);
        }

        public void Dispose()
        {
            _conditions.Changed -= OnStatsChanged;
            _conditions.Dispose();
        }
    }
}
