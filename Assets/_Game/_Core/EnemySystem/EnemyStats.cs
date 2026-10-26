using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // Tunable numbers of an enemy kind (EnemyDefinition holds the base value of each). Adding one: an entry at the end (the
    // definitions store values by number), a base value in every definition (EnemyCombatTests checks), and whoever applies
    // it re-reads it on EnemyStats.Changed.
    public enum EnemyStat
    {
        MaxHealth,
        // Walking speed (m/s).
        MoveSpeed,
    }

    // A change from one source (a slow, a buff, an elite tier): value = (base + every Add) * every Multiply.
    public readonly struct EnemyStatModifier
    {
        public readonly EnemyStat Stat;
        public readonly float Add;
        public readonly float Multiply;
        public readonly object Source;

        public EnemyStatModifier(EnemyStat stat, object source, float add = 0f, float multiply = 1f)
        {
            Stat = stat;
            Source = source;
            Add = add;
            Multiply = multiply;
        }
    }

    // One enemy's stats: its definition's base values with modifiers on top. Owners that cache a stat (max health, agent
    // speed) re-read it on Changed.
    public sealed class EnemyStats
    {
        static readonly int Count = Enum.GetValues(typeof(EnemyStat)).Length;

        readonly float[] _base = new float[Count];
        readonly float[] _values = new float[Count];
        readonly List<EnemyStatModifier> _modifiers = new();

        public event Action<EnemyStat> Changed;

        public EnemyStats(EnemyDefinition definition)
        {
            for (int i = 0; i < Count; i++)
                _base[i] = _values[i] = definition.BaseStat((EnemyStat)i);
        }

        public float Get(EnemyStat stat) => _values[(int)stat];
        public float GetBase(EnemyStat stat) => _base[(int)stat];

        public void AddModifier(in EnemyStatModifier modifier)
        {
            _modifiers.Add(modifier);
            Recalculate(modifier.Stat);
        }

        // Removes everything `source` added.
        public void RemoveModifiers(object source)
        {
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].Source != source)
                    continue;
                EnemyStat stat = _modifiers[i].Stat;
                _modifiers.RemoveAt(i);
                Recalculate(stat);
            }
        }

        void Recalculate(EnemyStat stat)
        {
            float add = 0f, multiply = 1f;
            foreach (EnemyStatModifier modifier in _modifiers)
            {
                if (modifier.Stat != stat)
                    continue;
                add += modifier.Add;
                multiply *= modifier.Multiply;
            }
            float value = Mathf.Max(0f, (_base[(int)stat] + add) * multiply);
            if (Mathf.Approximately(value, _values[(int)stat]))
                return;
            _values[(int)stat] = value;
            Changed?.Invoke(stat);
        }
    }
}
