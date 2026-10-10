using JBooth.MicroVerseCore;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class RandomUtils
    {
        /// <summary>
        /// Get a random value of the given range
        /// </summary>
        /// <param name="range"></param>
        /// <returns></returns>
        public static float GetRandom( Vector2 range)
        {
            return Random.Range(range.x, range.y);
        }

        /// <summary>
        /// Get a random seed within a defined range.
        /// This is not the full range. We'd like to keep the seed somewhat readable
        /// </summary>
        /// <returns></returns>
        public static uint GetRandomSeedUint()
        {

            //return (uint)Random.Range(-int.MaxValue, int.MaxValue);
            return (uint)Random.Range(0, 300); // limit the range, high values seem to become problematic

        }

        /// <summary>
        /// Get a random seed within a defined range.
        /// This is not the full range. We'd like to keep the seed somewhat readable
        /// </summary>
        /// <returns></returns>
        public static int GetRandomSeedInt()
        {
            return Random.Range(0, 300); // limit the range, high values seem to become problematic
        }

        /// <summary>
        /// Get a random noise offset int.
        /// This is not the full range, besides it's an int. We'd like to keep the seed somewhat readable.
        /// </summary>
        /// <returns></returns>
        public static int GetRandomNoiseOffset()
        {
            return Random.Range(0, 100); // limit the range, high values seem to become problematic
        }

        public static void SetRandomSeed(Stamp[] stamps)
        {
            foreach (Stamp stamp in stamps)
            {

#if __MICROVERSE_VEGETATION__
                if (stamp is DetailStamp)
                {
                    (stamp as DetailStamp).prototype.noiseSeed = GetRandomSeedInt();
                }
                else if (stamp is TreeStamp)
                {
                    (stamp as TreeStamp).seed = GetRandomSeedUint();
                }
#endif

#if __MICROVERSE_OBJECTS__
                if (stamp is ObjectStamp)
                {
                    (stamp as ObjectStamp).seed = GetRandomSeedUint();
                }
#endif
            }
        }

        private static float GetNoiseOffset(NoiseRandomization noiseRandomization, float sharedOffset)
        {
            switch (noiseRandomization)
            {
                case NoiseRandomization.SharedOffset:
                    return sharedOffset;
                case NoiseRandomization.FullRandom:
                    return GetRandomNoiseOffset();
                default:
                    Debug.Log($"Unsupported noise randomization: {noiseRandomization}");
                    return GetRandomNoiseOffset();
            }

        }

        public static void SetRandomNoiseOffset(Stamp[] stamps, NoiseRandomization noiseRandomization)
        {
            float sharedOffset = GetRandomNoiseOffset();

            foreach (Stamp stamp in stamps)
            {
                FilterSet filterSet = stamp.GetFilterSet();

                if (stamp is TextureStamp)
                {
                    SetRandomNoiseOffset(filterSet.weightNoise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight2Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight3Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                }

#if __MICROVERSE_VEGETATION__
                if (stamp is DetailStamp)
                {
                    SetRandomNoiseOffset(filterSet.weightNoise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight2Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight3Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                }
                else if (stamp is TreeStamp)
                {
                    SetRandomNoiseOffset(filterSet.weightNoise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight2Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight3Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                }
#endif

#if __MICROVERSE_OBJECTS__
                if (stamp is ObjectStamp)
                {
                    SetRandomNoiseOffset(filterSet.weightNoise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight2Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                    SetRandomNoiseOffset(filterSet.weight3Noise, GetNoiseOffset(noiseRandomization, sharedOffset));
                }
#endif
            }
        }

        private static void SetRandomNoiseOffset(Noise noise)
        {
            float offset = GetRandomNoiseOffset();
            SetRandomNoiseOffset(noise, offset);
        }

        private static void SetRandomNoiseOffset(Noise noise, float offset)
        {
            switch (noise.noiseType)
            {
                case Noise.NoiseType.None:
                    break;

                default:
                    noise.offset = offset;
                    break;
            }
        }
    }
}