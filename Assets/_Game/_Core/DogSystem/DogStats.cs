using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Every number the dog fights with that can be upgraded later. Saved by number in DogStatBlock: append only.
    public enum DogStat
    {
        BiteDamage,
        BiteCooldown,
        MaxBiteClass,
        StaggerTime,
        RetaliationWindow,
        CircleRadius,
        CircleSpeed,
        FeintChance,
        EscapeSpeed,
        DodgeChance,
        Toughness,
        RegenPerSecond,
        HelpUpToughness,
        LimpTime,
        RetreatAt,
        EngageRange,
        SentChaseRange,
        SenseRange,
        TakedownChanceBase,
        TakedownChanceBonus,
        MaxTakedownClass,
        TakedownCooldown,
        PinTime,
    }

    // The dog's base fighting numbers (Data/Dogs/DogStatBlock.asset). A class stat (MaxBiteClass, MaxTakedownClass) is an
    // index into DogSettings.Classes (Light, Medium, Heavy, Boss).
    [CreateAssetMenu(fileName = "DogStatBlock", menuName = "Aipocalypse/Dogs/Dog Stat Block")]
    public sealed class DogStatBlock : ScriptableObject
    {
        [SerializeField, Tooltip("Base value of each stat (before upgrades)")] Dictionary<DogStat, float> _base = new();

        public bool Has(DogStat stat) => _base.ContainsKey(stat);
        public float Base(DogStat stat) => _base.TryGetValue(stat, out float value) ? value : 0f;

        public void SetBase(DogStat stat, float value) => _base[stat] = value;
    }

    // Something that changes the dog's stats (a level, a perk, gear): added to the base.
    public interface IDogUpgradeSource
    {
        float Modifier(DogStat stat);
    }

    // The stats the dog uses: base + every upgrade source. A levelling system only adds sources; nothing else changes.
    public sealed class DogStatSheet
    {
        readonly DogStatBlock _block;
        readonly int _classCount;
        readonly List<IDogUpgradeSource> _sources = new();

        public DogStatSheet(DogStatBlock block, int classCount)
        {
            _block = block;
            _classCount = Mathf.Max(1, classCount);
        }

        public void Add(IDogUpgradeSource source)
        {
            if (source != null && !_sources.Contains(source))
                _sources.Add(source);
        }

        public void Remove(IDogUpgradeSource source) => _sources.Remove(source);

        public float Get(DogStat stat)
        {
            float value = _block != null ? _block.Base(stat) : 0f;
            for (int i = 0; i < _sources.Count; i++)
                value += _sources[i].Modifier(stat);
            return value;
        }

        // A class stat as an index into the class list (rounded down, clamped).
        public int GetClass(DogStat stat) => Mathf.Clamp(Mathf.FloorToInt(Get(stat) + 1e-4f), 0, _classCount - 1);
    }
}
