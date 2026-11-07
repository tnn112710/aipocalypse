using System;
using UnityEngine;

namespace DistantLands.Cozy.Blocks
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Blocks", "Blocks", "Choose atmosphere overrides by time of day.", "Atmosphere", true, 0)]
    public class BlocksModule : CozyBiomeModule<BlocksModule>
    {
        public override Type[] ModuleRequirements => DependsOn<CozyAtmosphereModule, ITimeModule, ICalendarModule>();

        public BlocksCollectionProfile CollectionProfile
        {
            get => collection;
            set
            {
                collection = value;
                Setup();
            }
        }

        /// <summary>
        /// The profile currently selected by the schedule, or the test profile when one is active.
        /// During a transition, this returns the profile being blended toward.
        /// </summary>
        public BlocksProfile CurrentBlockProfile
        {
            get
            {
                if (testBlock != null)
                    return testBlock;

                if (!TryGetSelection(out ProfileSelection selection))
                    return null;

                return selection.blend > 0f
                    ? selection.to ?? selection.from
                    : selection.from;
            }
        }

        [SerializeField]
        private BlocksCollectionProfile collection;

        [SerializeField]
        private BlocksProfile testBlock;
        private CozyAtmosphereModule atmosphereModule;
        private ICalendarModule calendar;
        private ITimeModule time;

        private AtmosphereParameters transitionFrom;
        private AtmosphereParameters transitionTo;
        private AtmosphereParameters transitionResult;

        private BlocksProfile[] yesterdayProfiles;
        private BlocksProfile[] todayProfiles;
        private BlocksProfile[] tomorrowProfiles;
        private int cachedDay = int.MinValue;

        public override void InitializeModule()
        {
            base.InitializeModule();

            WeatherSphere.GetModule(out atmosphereModule);
            WeatherSphere.GetModule(out calendar);
            WeatherSphere.GetModule(out time);

            transitionFrom = new AtmosphereParameters();
            transitionTo = new AtmosphereParameters();
            transitionResult = new AtmosphereParameters();
            Setup();

            if (!IsBiomeModule && atmosphereModule != null)
            {
                atmosphereModule.RunBeforeBiomes -= ApplyToAtmosphere;
                atmosphereModule.RunBeforeBiomes += ApplyToAtmosphere;
            }
        }

        public override void DeinitializeModule()
        {
            if (!IsBiomeModule && atmosphereModule != null)
                atmosphereModule.RunBeforeBiomes -= ApplyToAtmosphere;

            base.DeinitializeModule();
        }

        private void OnValidate()
        {
            collection?.SortAndValidate();
            InvalidateProfileCache();
        }

        private void Setup()
        {
            collection?.SortAndValidate();
            InvalidateProfileCache();
        }

        private void InvalidateProfileCache()
        {
            cachedDay = int.MinValue;
            yesterdayProfiles = null;
            todayProfiles = null;
            tomorrowProfiles = null;
        }

        internal void RefreshProfileCache()
        {
            InvalidateProfileCache();
        }

        private void ApplyToAtmosphere(CozyAtmosphereModule source)
        {
            if (source == null)
                return;

            AtmosphereParameters parameters = source.Parameters;
            if (testBlock != null)
                testBlock.ApplyToWeatherSphere(parameters, 1f);
            else
                ApplyCollection(parameters, 1f);

            ApplyBiomes();
        }

        public override void ApplyBiome(BlocksModule biome, float weight)
        {
            if (biome == null || weight <= 0f || atmosphereModule == null)
                return;

            AtmosphereParameters parameters = atmosphereModule.Parameters;
            if (biome.testBlock != null)
            {
                transitionResult.Copy(parameters);
                biome.testBlock.ApplyToWeatherSphere(transitionResult, 1f);
                parameters.Blend(transitionResult, Mathf.Clamp01(weight));
                return;
            }

            biome.ApplyCollection(parameters, weight);
        }

        private void ApplyCollection(AtmosphereParameters parameters, float weight)
        {
            if (!TryGetSelection(out ProfileSelection selection))
                return;

            ApplySelection(parameters, selection, weight);
        }

        private void ApplySelection(AtmosphereParameters parameters, ProfileSelection selection, float weight)
        {
            if (weight <= 0f)
                return;

            weight = Mathf.Clamp01(weight);

            if (selection.blend <= 0f || selection.from == selection.to)
            {
                selection.from?.ApplyToWeatherSphere(parameters, weight);
                return;
            }

            if (selection.blend >= 1f)
            {
                selection.to?.ApplyToWeatherSphere(parameters, weight);
                return;
            }

            transitionFrom.Copy(parameters);
            transitionTo.Copy(parameters);

            selection.from?.ApplyToWeatherSphere(transitionFrom, 1f);
            selection.to?.ApplyToWeatherSphere(transitionTo, 1f);

            transitionResult.Copy(transitionFrom);
            transitionResult.Blend(transitionTo, selection.blend);

            if (weight >= 1f)
                parameters.Copy(transitionResult);
            else
                parameters.Blend(transitionResult, weight);
        }

        private bool TryGetSelection(out ProfileSelection selection)
        {
            selection = default;
            if (collection == null || collection.blockTimes == null || collection.blockTimes.Count == 0)
                return false;

            MeridiemTime currentTime = time != null ? time.CurrentTime : MeridiemTime.Noon;
            int currentDay = calendar != null ? calendar.CurrentDate.AbsoluteDay : 0;
            EnsureProfileCache(currentDay);

            float timeOfDay = currentTime.AsPercentage();
            int currentIndex = -1;

            for (int i = collection.blockTimes.Count - 1; i >= 0; i--)
            {
                if (timeOfDay >= collection.blockTimes[i].startTime.AsPercentage())
                {
                    currentIndex = i;
                    break;
                }
            }

            int currentDayOffset = 0;
            if (currentIndex < 0)
            {
                currentIndex = collection.blockTimes.Count - 1;
                currentDayOffset = -1;
            }

            int previousIndex = (currentIndex + collection.blockTimes.Count - 1) % collection.blockTimes.Count;
            int nextIndex = (currentIndex + 1) % collection.blockTimes.Count;
            float currentStart = collection.blockTimes[currentIndex].startTime.AsPercentage();
            float nextStart = collection.blockTimes[nextIndex].startTime.AsPercentage();
            float timeSinceStart = Mathf.Repeat(timeOfDay - currentStart, 1f);
            float blockDuration = Mathf.Repeat(nextStart - currentStart, 1f);
            if (collection.blockTimes.Count == 1)
                blockDuration = 1f;

            float transitionDuration = blockDuration * Mathf.Clamp01(
                collection.blockTimes[currentIndex].transitionPercentage);

            int previousDayOffset = currentDayOffset - (currentIndex == 0 ? 1 : 0);

            BlocksProfile previousProfile = GetCachedProfile(previousIndex, previousDayOffset);
            BlocksProfile currentProfile = GetCachedProfile(currentIndex, currentDayOffset);

            if (transitionDuration > 0f && timeSinceStart <= transitionDuration)
            {
                selection = new ProfileSelection(
                    previousProfile,
                    currentProfile,
                    Mathf.Clamp01(timeSinceStart / transitionDuration));
            }
            else
            {
                selection = new ProfileSelection(currentProfile, currentProfile, 0f);
            }

            return true;
        }

        private void EnsureProfileCache(int day)
        {
            int count = collection.blockTimes.Count;
            if (cachedDay == day &&
                yesterdayProfiles != null && yesterdayProfiles.Length == count &&
                todayProfiles != null && todayProfiles.Length == count &&
                tomorrowProfiles != null && tomorrowProfiles.Length == count)
                return;

            yesterdayProfiles = new BlocksProfile[count];
            todayProfiles = new BlocksProfile[count];
            tomorrowProfiles = new BlocksProfile[count];

            FillProfileCache(yesterdayProfiles, day - 1);
            FillProfileCache(todayProfiles, day);
            FillProfileCache(tomorrowProfiles, day + 1);
            cachedDay = day;
        }

        private void FillProfileCache(BlocksProfile[] profiles, int day)
        {
            MeridiemDate date = new MeridiemDate(day);

            for (int i = 0; i < profiles.Length; i++)
            {
                BlockCollection block = collection.blockTimes[i];
                if (block.blocks == null || block.blocks.Count == 0)
                    continue;

                ConditionSnapshot conditions = WeatherSphere != null
                    ? ConditionSnapshot.GetConditions(WeatherSphere, date, block.startTime)
                    : default;

                float totalChance = 0f;
                int firstProfile = -1;

                for (int j = 0; j < block.blocks.Count; j++)
                {
                    BlocksConditions candidate = block.blocks[j];
                    if (candidate.blocksProfile == null)
                        continue;

                    if (firstProfile < 0)
                        firstProfile = j;

                    float chance = candidate.chance != null
                        ? candidate.chance.GetChance(conditions)
                        : 1f;

                    if (!float.IsNaN(chance) && !float.IsInfinity(chance) && chance > 0f)
                        totalChance += chance;
                }

                if (firstProfile < 0)
                    continue;

                if (totalChance <= 0f)
                {
                    profiles[i] = block.blocks[firstProfile].blocksProfile;
                    continue;
                }

                float selection = GetDailyRandom(day, i) * totalChance;
                float accumulatedChance = 0f;

                for (int j = 0; j < block.blocks.Count; j++)
                {
                    BlocksConditions candidate = block.blocks[j];
                    if (candidate.blocksProfile == null)
                        continue;

                    float chance = candidate.chance != null
                        ? candidate.chance.GetChance(conditions)
                        : 1f;

                    if (float.IsNaN(chance) || float.IsInfinity(chance) || chance <= 0f)
                        continue;

                    accumulatedChance += chance;
                    if (selection < accumulatedChance)
                    {
                        profiles[i] = candidate.blocksProfile;
                        break;
                    }
                }

                if (profiles[i] == null)
                    profiles[i] = block.blocks[firstProfile].blocksProfile;
            }
        }

        private static float GetDailyRandom(int day, int blockIndex)
        {
            unchecked
            {
                uint value = (uint)day ^ ((uint)blockIndex + 1u) * 0x9E3779B9u;
                value ^= value >> 16;
                value *= 0x7FEB352Du;
                value ^= value >> 15;
                value *= 0x846CA68Bu;
                value ^= value >> 16;
                return (value & 0x00FFFFFFu) / 16777216f;
            }
        }

        private BlocksProfile GetCachedProfile(int index, int dayOffset)
        {
            if (dayOffset < 0)
                return yesterdayProfiles[index];
            if (dayOffset > 0)
                return tomorrowProfiles[index];
            return todayProfiles[index];
        }

        private readonly struct ProfileSelection
        {
            public readonly BlocksProfile from;
            public readonly BlocksProfile to;
            public readonly float blend;

            public ProfileSelection(BlocksProfile from, BlocksProfile to, float blend)
            {
                this.from = from;
                this.to = to;
                this.blend = blend;
            }
        }

        /// <summary>
        /// Display a profile regardless of the collection or current time.
        /// </summary>
        public void TestBlock(BlocksProfile profile)
        {
            testBlock = profile;
        }

        public void ClearTestBlock()
        {
            testBlock = null;
        }
    }
}
