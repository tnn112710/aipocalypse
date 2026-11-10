using System;
using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.Stats
{
    // One source's change to one stat: value = (base + every Add) × every Multiply. On a …Scale stat (base 1) "+10%" is
    // Add 0.1 (stacks by adding) or Multiply 1.1 (stacks by multiplying).
    [Serializable]
    public struct StatModifier
    {
        public string Source;
        public StatId Stat;
        public float Add;
        public float Multiply;

        public StatModifier(string source, StatId stat, float add = 0f, float multiply = 1f)
        {
            Source = source;
            Stat = stat;
            Add = add;
            Multiply = multiply;
        }
    }

    // The shared modifier layer behind IStatProvider (root scope): upgrades, skills and gear each Set their modifiers under
    // their own source id and Remove them by it. Sources are strings so the modifiers can be saved (StatSaveSection).
    public sealed class StatModifiers : IStatProvider
    {
        static readonly int StatCount = Enum.GetValues(typeof(StatId)).Length;

        readonly List<StatModifier> _modifiers = new();
        // Totals per stat, so Get stays a lookup: it's read every frame.
        readonly float[] _add = new float[StatCount];
        readonly float[] _multiply = new float[StatCount];

        public event Action Changed;

        public StatModifiers() => Recalculate();

        public IReadOnlyList<StatModifier> All => _modifiers;

        public float Get(StatId stat, float baseValue) => (baseValue + _add[(int)stat]) * _multiply[(int)stat];

        // Adds the source's modifier on a stat, or replaces the one it already has there.
        public void Set(string source, StatId stat, float add = 0f, float multiply = 1f)
        {
            var modifier = new StatModifier(source, stat, add, multiply);
            int index = IndexOf(source, stat);
            if (index >= 0)
                _modifiers[index] = modifier;
            else
                _modifiers.Add(modifier);
            Apply();
        }

        public bool Remove(string source, StatId stat)
        {
            int index = IndexOf(source, stat);
            if (index < 0)
                return false;
            _modifiers.RemoveAt(index);
            Apply();
            return true;
        }

        // Removes everything `source` set. Returns how many.
        public int Remove(string source)
        {
            int removed = _modifiers.RemoveAll(m => m.Source == source);
            if (removed > 0)
                Apply();
            return removed;
        }

        public void Clear()
        {
            if (_modifiers.Count == 0)
                return;
            _modifiers.Clear();
            Apply();
        }

        // Replaces every modifier (loading a save).
        public void Restore(IEnumerable<StatModifier> modifiers)
        {
            _modifiers.Clear();
            if (modifiers != null)
                _modifiers.AddRange(modifiers);
            Apply();
        }

        int IndexOf(string source, StatId stat)
        {
            for (int i = 0; i < _modifiers.Count; i++)
                if (_modifiers[i].Stat == stat && _modifiers[i].Source == source)
                    return i;
            return -1;
        }

        void Apply()
        {
            Recalculate();
            Changed?.Invoke();
        }

        void Recalculate()
        {
            for (int i = 0; i < StatCount; i++)
            {
                _add[i] = 0f;
                _multiply[i] = 1f;
            }
            foreach (StatModifier modifier in _modifiers)
            {
                int stat = (int)modifier.Stat;
                if (stat < 0 || stat >= StatCount)
                    continue;
                _add[stat] += modifier.Add;
                _multiply[stat] *= modifier.Multiply;
            }
        }
    }
}
