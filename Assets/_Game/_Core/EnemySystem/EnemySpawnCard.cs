using System.Collections.Generic;
using Ami.BroAudio;
using UnityEngine;

namespace Aipocalypse.Core.EnemySystem
{
    // One spawnable kind of enemy for spawners and raids: its looks (prefab variants and how often each is picked), how many
    // come together and the cue that announces it. Lives with the enemy in Content/Enemies/<Name>/<Name>_Card.asset.
    [CreateAssetMenu(fileName = "Card", menuName = "Aipocalypse/Enemies/Spawn Card")]
    public sealed class EnemySpawnCard : ScriptableObject
    {
        [SerializeField, Tooltip("Prefab variants of this kind and their relative weight")]
        Dictionary<EnemyBrain, float> _variants = new();
        [Tooltip("Enemies per group, min and max (inclusive)")] public Vector2Int GroupSize = new(2, 3);
        [Tooltip("Played once per raid wave this kind is in, at its first group's spawn point (goblins: a war horn)")]
        public SoundID ArrivalCue;

        public IReadOnlyDictionary<EnemyBrain, float> Variants => _variants;

        // `roll` in [0, 1); null when there's no variant with a weight.
        public EnemyBrain PickVariant(float roll)
        {
            float total = 0f;
            foreach ((EnemyBrain prefab, float weight) in _variants)
                if (prefab != null && weight > 0f)
                    total += weight;
            if (total <= 0f)
                return null;
            float pick = Mathf.Clamp01(roll) * total;
            EnemyBrain last = null;
            foreach ((EnemyBrain prefab, float weight) in _variants)
            {
                if (prefab == null || weight <= 0f)
                    continue;
                last = prefab;
                pick -= weight;
                if (pick < 0f)
                    return prefab;
            }
            return last;
        }

        public int PickGroupSize(float roll)
        {
            int min = Mathf.Max(1, GroupSize.x);
            int max = Mathf.Max(min, GroupSize.y);
            return Mathf.Min(max, min + (int)(Mathf.Clamp01(roll) * (max - min + 1)));
        }

#if UNITY_EDITOR
        // Setup scripts and tests only.
        public void SetVariant(EnemyBrain prefab, float weight) => _variants[prefab] = weight;
#endif
    }
}
