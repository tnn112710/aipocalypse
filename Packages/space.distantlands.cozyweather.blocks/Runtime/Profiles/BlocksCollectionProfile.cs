using System;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy.Blocks
{
    [CreateAssetMenu(menuName = "Distant Lands/Cozy/Blocks Collection", order = 361)]
    public class BlocksCollectionProfile : CozyProfile
    {
        public List<BlockCollection> blockTimes = new()
        {
            new BlockCollection()
            {
                blockName = "Day",
                startTime = new MeridiemTime(6,00, Meridiem.AM)
            },
            new BlockCollection()
            {
                blockName = "Night",
                startTime = new MeridiemTime(6,00, Meridiem.PM)
            }
        };

        private void OnValidate()
        {
            SortAndValidate();

#if UNITY_EDITOR
            foreach (BlocksModule module in FindObjectsByType<BlocksModule>(FindObjectsInactive.Include))
            {
                if (module.CollectionProfile == this)
                    module.RefreshProfileCache();
            }
#endif
        }

        public void SortAndValidate()
        {
            if (blockTimes == null)
                return;

            if (blockTimes.Count == 0)
                return;

            blockTimes.Sort((left, right) => left.startTime.CompareTo(right.startTime));

            for (int i = 0; i < blockTimes.Count; i++)
            {
                int nextIndex = (i + 1) % blockTimes.Count;
                float currentStart = blockTimes[i].startTime.AsPercentage();
                float nextStart = blockTimes[nextIndex].startTime.AsPercentage();
                float blockDuration = Mathf.Repeat(nextStart - currentStart, 1f);

                if (blockTimes.Count == 1)
                    blockDuration = 1f;

                BlockCollection block = blockTimes[i];
                if (block.transitionDuration != new MeridiemTimeDelta(0, 0))
                {
                    block.transitionPercentage = blockDuration > 0f
                        ? Mathf.Clamp01((float)block.transitionDuration / blockDuration)
                        : 0f;
                    block.transitionDuration = new MeridiemTimeDelta(0, 0);
                }

                if (float.IsNaN(block.transitionPercentage) || float.IsInfinity(block.transitionPercentage))
                    block.transitionPercentage = 0f;
                else
                    block.transitionPercentage = Mathf.Clamp01(block.transitionPercentage);

                blockTimes[i] = block;
            }
        }
    }

    [Serializable]
    public struct BlockCollection
    {
        public string blockName;
        [Tooltip("The time this block starts. Its transition blends in from the previous block during the time immediately after this.")]
        public MeridiemTime startTime;

        [Range(0f, 1f)]
        [Tooltip("The fraction of this block used to transition to the next block. 0 is instant; 1 uses the entire block.")]
        public float transitionPercentage;

        [HideInInspector]
        public MeridiemTimeDelta transitionDuration;

        [Tooltip("Profiles that may be selected for this block. Chances are evaluated once per day.")]
        public List<BlocksConditions> blocks;
    }
}
