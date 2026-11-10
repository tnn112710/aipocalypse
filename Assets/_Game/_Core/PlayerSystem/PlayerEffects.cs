using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Combat;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.Stats;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // What the player's consumables do: health now, health over time (a hit pauses it for HealPauseOnHit seconds) and stat
    // buffs. It's the player's IStatProvider, over the base one, so the motor and stamina read buffed values. Ticked on game
    // time by Player (nothing runs while paused); cleared on death.
    public sealed class PlayerEffects : IStatProvider, IDisposable
    {
        // One effect running: a buff, or healing over time. Using the same item again refreshes it.
        public sealed class Active
        {
            public ConsumableDefinition Source { get; internal set; }
            public bool Heals { get; internal set; }
            public float Remaining { get; internal set; }
            public float Duration { get; internal set; }
            internal float HealLeft;
        }

        readonly IStatProvider _base;
        readonly float _healPauseOnHit;
        readonly List<Active> _active = new();
        float _healPause;

        public event Action Changed;

        public PlayerEffects(IStatProvider baseStats, float healPauseOnHit)
        {
            _base = baseStats;
            _healPauseOnHit = healPauseOnHit;
            _base.Changed += RaiseChanged;
        }

        public IReadOnlyList<Active> Effects => _active;
        public bool HealingPaused => _healPause > 0f;

        public float Get(StatId stat, float baseValue)
        {
            float value = _base.Get(stat, baseValue);
            for (int i = 0; i < _active.Count; i++)
            {
                Active effect = _active[i];
                if (!effect.Heals && effect.Source.BuffScales.TryGetValue(stat, out float scale))
                    value *= scale;
            }
            return value;
        }

        public void Use(ConsumableDefinition item, Health health)
        {
            if (item == null || health == null || health.IsDead)
                return;
            if (item.Heal > 0f)
                health.Heal(item.Heal);
            if (item.HealOverTime > 0f)
            {
                // Again before it ran out: what's left adds up, over a fresh duration.
                Active healing = Find(item, heals: true) ?? Add(item, heals: true);
                healing.HealLeft += item.HealOverTime;
                healing.Remaining = healing.Duration = item.HealSeconds;
            }
            if (item.HasBuff)
            {
                Active buff = Find(item, heals: false) ?? Add(item, heals: false);
                buff.Remaining = buff.Duration = item.BuffSeconds;
                RaiseChanged();
            }
        }

        public void OnHurt() => _healPause = _healPauseOnHit;

        public void Tick(float deltaTime, Health health)
        {
            if (deltaTime <= 0f)
                return;
            if (_healPause > 0f)
                _healPause -= deltaTime;
            bool buffEnded = false;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Active effect = _active[i];
                if (effect.Heals)
                {
                    // Paused healing keeps its time and what's left.
                    if (_healPause > 0f || health == null || health.IsDead)
                        continue;
                    float step = Math.Min(deltaTime, effect.Remaining);
                    float amount = effect.Remaining > 0f ? effect.HealLeft * step / effect.Remaining : effect.HealLeft;
                    health.Heal(amount);
                    effect.HealLeft -= amount;
                }
                effect.Remaining -= deltaTime;
                if (effect.Remaining > 0f)
                    continue;
                _active.RemoveAt(i);
                buffEnded |= !effect.Heals;
            }
            if (buffEnded)
                RaiseChanged();
        }

        public void Clear()
        {
            bool hadBuff = false;
            foreach (Active effect in _active)
                hadBuff |= !effect.Heals;
            _active.Clear();
            _healPause = 0f;
            if (hadBuff)
                RaiseChanged();
        }

        public void Dispose() => _base.Changed -= RaiseChanged;

        Active Find(ConsumableDefinition item, bool heals)
        {
            foreach (Active effect in _active)
                if (effect.Source == item && effect.Heals == heals)
                    return effect;
            return null;
        }

        Active Add(ConsumableDefinition item, bool heals)
        {
            var effect = new Active { Source = item, Heals = heals };
            _active.Add(effect);
            return effect;
        }

        void RaiseChanged() => Changed?.Invoke();
    }
}
