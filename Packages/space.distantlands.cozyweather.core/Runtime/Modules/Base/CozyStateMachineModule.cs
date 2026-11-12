//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Helper for CozyStateMachines to manage profile weights without the use of coroutines
    /// </summary>
    [Serializable]
    public struct ProfileWeight
    {
        public float weight;
        [SerializeField]
        internal bool isTransitioning;
        [SerializeField]
        private float targetWeight;
        [SerializeField]
        private float startingWeight;
        [SerializeField]
        private float t;
        [SerializeField]
        private float time;

        public ProfileWeight(float weight)
        {
            this.weight = weight;
            isTransitioning = false;
            targetWeight = 1;
            startingWeight = weight;
            t = 0;
            time = 1;
        }

        public void StartTransition(float targetWeight, float transitionTime)
        {
            if (transitionTime == 0)
            {
                weight = targetWeight;
                isTransitioning = false;
                return;
            }
            t = 0;
            startingWeight = weight;
            this.targetWeight = targetWeight;
            time = transitionTime;
            isTransitioning = true;

        }

        internal void RunTransition()
        {
            if (!isTransitioning) return;

            t += Time.deltaTime;

            if (t > time)
            {
                weight = targetWeight;
                isTransitioning = false;
            }

            weight = Mathf.Lerp(startingWeight, targetWeight, t / time);
        }

        internal bool IsValid()
        {
            return IsFinite(weight) && IsFinite(targetWeight) && IsFinite(startingWeight) &&
                IsFinite(t) && IsFinite(time) && (!isTransitioning || (time > 0 && t >= 0));
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    [Serializable]
    internal class StateMachineSaveData
    {
        public int version;
        public MeridiemTimeDelta timeTillProfileChange;
        public MeridiemTimeDelta resolvedProfileDuration;
        public float transitionTime;
        public float timerSpeedMultiplier;
        public string selectedProfileName;
        public List<StateMachineProfileSaveData> profiles;
    }

    [Serializable]
    internal class StateMachineProfileSaveData
    {
        public string name;
        public ProfileWeight weight;
    }
    /// <summary>
    /// Creates a module that uses a state machine to blend profiles (states) into a parameters struct for output. Used for modules such as the Weather module, Ambience module, and ReSound module.
    /// </summary>
    /// <typeparam name="T">The profile used in the state machine</typeparam>
    public abstract class CozyStateMachineModule<T>
        : CozyBiomeModule<CozyStateMachineModule<T>>, ISkippable, ISaveable
        where T : CozyProfile, IStateMachineProfile
    {

        #region Dictionary Management
        /// <summary>
        /// Holds the resolved final weights of the profiles in the state machine
        /// </summary>
        public Dictionary<T, float> ProfileWeights => profileWeights;
        /// <summary>
        /// Holds the local profile weights of this module based on the current system or biome
        /// </summary>
        public Dictionary<T, ProfileWeight> LocalProfileWeights => localProfileWeights;
        private Dictionary<T, float> profileWeights;
        private Dictionary<T, ProfileWeight> localProfileWeights;

        private List<T> markedForRemoval;
        private List<T> keysCache;
        Dictionary<T, float> cachedProfiles = new();

        #endregion


        /// <summary>
        /// Get the time (in in-game hours and minutes) till the next profile starts changing
        /// </summary>
        public MeridiemTimeDelta TimeTillProfileChange => timeTillProfileChange;
        [SerializeField]
        private MeridiemTimeDelta timeTillProfileChange;

        public float ProfilePercentageRemaining => TimeTillProfileChange / Mathf.Max(0.0001f, resolvedProfileDuration);
        public MeridiemTimeDelta resolvedProfileDuration;


        /// <summary>
        /// Determine the amount of time (is real-world seconds) it takes to change the active profile
        /// </summary>
        public float TransitionTime { get => transitionTime; set => transitionTime = value; }
        [SerializeField]
        private float transitionTime = 5;

        /// <summary>
        /// Runs when the current profile changes. Takes in the old profile and new profile as arguments
        /// </summary>
        public Action<T, T> OnProfileChange { get => onProfileChange; set => onProfileChange = value; }
        private Action<T, T> onProfileChange;

        /// <summary>
        /// Get the current resolved profile
        /// </summary>
        public T CurrentProfile
        {
            get
            {
                T strongestProfile = null;
                float strongestWeight = 0;

                profileWeights ??= new Dictionary<T, float>();

                foreach (var kvp in profileWeights)
                {
                    if (kvp.Value > strongestWeight)
                    {
                        strongestProfile = kvp.Key;
                        strongestWeight = kvp.Value;
                    }
                }

                return strongestProfile;
            }
        }

        /// <summary>
        /// Get the current profile for this system
        /// </summary>
        public T CurrentLocalProfile
        {
            get
            {
                T strongestProfile = null;
                float strongestWeight = 0;

                localProfileWeights ??= new Dictionary<T, ProfileWeight>();

                foreach (var kvp in localProfileWeights)
                {
                    if (kvp.Value.weight > strongestWeight)
                    {
                        strongestProfile = kvp.Key;
                        strongestWeight = kvp.Value.weight;
                    }
                }

                return strongestProfile;
            }
        }
        private T cachedStrongestProfile;
        private T selectedProfile;

        /// <summary>
        /// Change the rate at which time moves for the state machine. For example, if it will normally take 2 in game hours before a profile changes, a multiplier of 2 will make it take 1 hour to complete.
        /// </summary>
        public float TimerSpeedMultiplier { get => timerSpeedMultiplier; set => timerSpeedMultiplier = value; }
        [SerializeField]
        private float timerSpeedMultiplier = 1;

        /// <summary>
        /// Should the state machine update the timer? If not, handle changing profiles manually.
        /// </summary>
        public abstract bool UpdateStateMachineTimer { get; }

        /// <summary>Time elapsed on this state machine's clock, in days.</summary>
        protected virtual float StateMachineDeltaTime =>
            WeatherSphere.Time != null
                ? Time.deltaTime * WeatherSphere.Time.TimeMovementSpeed * timerSpeedMultiplier
                : Time.deltaTime / 1440f * timerSpeedMultiplier;

        public override void InitializeModule()
        {
            Setup();

            if (IsBiomeModule) return;

            profileWeights = new Dictionary<T, float>();
        }

        private void Setup()
        {
            localProfileWeights = new();
            selectedProfile = null;

            markedForRemoval = new List<T>();
            cachedProfiles = new();

            SetupStateMachine();
        }

        #region Execution
        public override void FrameReset()
        {
            profileWeights ??= new();
            profileWeights.Clear();
        }

        public override void OnUpdateWeatherWeights()
        {
            ManageLocalWeights();

            foreach (var kvp in localProfileWeights)
                profileWeights[kvp.Key] = kvp.Value.weight;

            ApplyBiomes();

            if (CurrentProfile && CurrentProfile != cachedStrongestProfile)
            {
                OnProfileChange?.Invoke(cachedStrongestProfile, CurrentProfile);
                cachedStrongestProfile = CurrentProfile;
            }

            foreach (var weather in profileWeights)
            {
                weather.Key.Play(weather.Value);
            }
        }

        public override void ApplyBiome(CozyStateMachineModule<T> biome, float weight)
        {
            biome.ManageLocalWeights();

            cachedProfiles.Clear();

            foreach (var kvp in profileWeights)
                cachedProfiles[kvp.Key] = Mathf.Lerp(kvp.Value, 0, weight);

            foreach (var kvp in biome.localProfileWeights)
            {
                profileWeights.TryGetValue(kvp.Key, out float oldValue);
                cachedProfiles[kvp.Key] = Mathf.Lerp(oldValue, kvp.Value.weight, weight);
            }

            profileWeights.Clear();
            foreach (var kvp in cachedProfiles)
                profileWeights[kvp.Key] = kvp.Value;
        }

        void ManageLocalWeights()
        {
            markedForRemoval ??= new();
            markedForRemoval.Clear();

            keysCache ??= new List<T>();
            localProfileWeights ??= new();
            keysCache.Clear();
            keysCache.AddRange(localProfileWeights.Keys);

            foreach (var prof in keysCache)
            {
                ProfileWeight weight = localProfileWeights[prof];

                if (!weight.isTransitioning && weight.weight < 0.001f)
                {
                    markedForRemoval.Add(prof);
                }

                weight.RunTransition();

                localProfileWeights[prof] = weight;
            }

            foreach (T prof in markedForRemoval) localProfileWeights.Remove(prof);
        }

        public override void OnCozyUpdateLoop()
        {
            OnStateMachineUpdate();
            UpdateStateMachine();
        }

        public override void UpdateBiome()
        {
            OnStateMachineUpdate();
            UpdateStateMachine();
        }

        public virtual void OnStateMachineUpdate()
        {

        }

        private void UpdateStateMachine()
        {
            if (!Application.isPlaying) return;

            if (UpdateStateMachineTimer)
            {
                timeTillProfileChange -= StateMachineDeltaTime;

                if (timeTillProfileChange <= 0)
                {
                    ChangeProfile();
                }
            }
        }

        /// <summary>
        /// Attempts to change the profile to the next profile. 
        /// </summary>
        /// <returns>Returns true if the operation succeeds</returns>
        public bool ChangeProfile()
        {
            ConditionSnapshot conditionSnapshot = ConditionSnapshot.GetConditions(WeatherSphere);
            return SetProfile(GetNextProfile(CurrentProfile, conditionSnapshot));
        }

        /// <summary>
        /// Runs when the state machine is setup. Override this method to implement initial behavior
        /// </summary>
        public abstract void SetupStateMachine();

        /// <summary>
        /// Gets the next profile after the previous profile finishes.
        /// </summary>
        /// <param name="lastProfile">The previous profile before the profile is swapped</param>
        /// <param name="conditionSnapshot">A current snapshot of the conditions in COZY</param>
        /// <returns>The next profile</returns>
        public abstract T GetNextProfile(T lastProfile, ConditionSnapshot conditionSnapshot);

        /// <summary>Returns the profiles available for resolving saved profile names.</summary>
        public abstract List<T> GetAllProfiles();

        /// <summary>
        /// Sets the current profile immediately
        /// </summary>
        /// <param name="profile">The profile to assign</param>
        /// <param name="duration">How long (in in-game time) should this profile play for</param>
        /// <param name="resetTimer">Should the profile timer (TimeTillNextProfile) be reset when this function is called</param>
        /// <param name="transition">Should this profile transition when changing</param>
        /// <returns>Returns true if the operation is successful</returns>
        public bool SetProfile(T profile, MeridiemTimeDelta duration, bool resetTimer = true, bool transition = true)
        {
            if (profile == null)
            {
                Debug.LogWarning("Cannot set a null profile.", this);
                return false;
            }
            if (resetTimer)
            {
                timeTillProfileChange = duration;
            }
            selectedProfile = profile;
            if (!Application.isPlaying || !transition)
            {
                OnProfileChange?.Invoke(CurrentProfile, profile);
                localProfileWeights = new Dictionary<T, ProfileWeight>
                {
                    { profile, new ProfileWeight(1) }
                };

                resolvedProfileDuration = timeTillProfileChange;
                return true;
            }
            OnProfileChange?.Invoke(CurrentProfile, profile);

            keysCache ??= new List<T>();
            keysCache.Clear();
            keysCache.AddRange(localProfileWeights.Keys);

            foreach (var key in keysCache)
            {
                if (key != profile)
                {
                    ProfileWeight w = localProfileWeights[key];
                    if (transition)
                        w.StartTransition(0, transitionTime);
                    else
                        w.weight = 0;
                    localProfileWeights[key] = w;
                }
            }

            if (!localProfileWeights.ContainsKey(profile))
                localProfileWeights[profile] = new ProfileWeight(0);

            ProfileWeight targetWeight = localProfileWeights[profile];
            if (transition)
                targetWeight.StartTransition(1, transitionTime);
            else
                targetWeight.weight = 1;
            localProfileWeights[profile] = targetWeight; // Save changes


            resolvedProfileDuration = timeTillProfileChange;

            return true;
        }

        /// <summary>
        /// Sets the current profile immediately. Duration is calculated based on the min and max time for the profile
        /// </summary>
        /// <param name="profile">The profile to assign</param>
        /// <param name="resetTimer">Should the profile timer (TimeTillNextProfile) be reset when this function is called</param>
        /// <param name="transition">Should this profile transition when changing</param>
        /// <returns>Returns true if the operation is successful</returns>
        public bool SetProfile(T profile, bool resetTimer = true, bool transition = true)
        {
            if (profile == null)
            {
                Debug.LogWarning("Cannot set a null profile.", this);
                return false;
            }
            return SetProfile(profile, GetProfilePlayTime(profile), resetTimer, transition);
        }

        public virtual float GetProfilePlayTime(T profile)
        {
            return Random.Range(profile.MinTime, profile.MaxTime);
        }

        #endregion

        private Dictionary<string, T> GetProfileNameLookup()
        {
            var lookup = new Dictionary<string, T>(StringComparer.Ordinal);
            List<T> profiles = GetAllProfiles();
            if (profiles == null)
                throw new InvalidOperationException($"{GetType().Name} returned a null profile list.");
            foreach (T profile in profiles)
            {
                if (!profile || string.IsNullOrEmpty(profile.name)) continue;
                if (lookup.TryGetValue(profile.name, out T existing))
                {
                    if (existing != profile)
                        lookup[profile.name] = null;
                }
                else
                    lookup.Add(profile.name, profile);
            }
            return lookup;
        }

        private void ValidateSaveProfile(T profile, Dictionary<string, T> lookup)
        {
            if (!profile || string.IsNullOrEmpty(profile.name) ||
                !lookup.TryGetValue(profile.name, out T match) || match != profile)
                throw new InvalidOperationException($"Profile '{(profile ? profile.name : "<missing>")}' in {GetType().Name} is missing from its profile list or has an ambiguous name.");
        }

        /// <summary>Captures local profile weights, transitions and timing using profile names.</summary>
        public SavedData Save()
        {
            var lookup = GetProfileNameLookup();

            StateMachineSaveData data = new()
            {
                version = 2,
                timeTillProfileChange = timeTillProfileChange,
                resolvedProfileDuration = resolvedProfileDuration,
                transitionTime = transitionTime,
                timerSpeedMultiplier = timerSpeedMultiplier,
                profiles = new List<StateMachineProfileSaveData>()
            };

            if (selectedProfile)
            {
                ValidateSaveProfile(selectedProfile, lookup);
                data.selectedProfileName = selectedProfile.name;
                if (localProfileWeights == null || !localProfileWeights.ContainsKey(selectedProfile))
                    throw new InvalidOperationException($"Selected profile '{selectedProfile.name}' has no local weight.");
            }
            if (localProfileWeights != null)
            {
                foreach (var pair in localProfileWeights)
                {
                    ValidateSaveProfile(pair.Key, lookup);
                    if (!pair.Value.IsValid())
                        throw new InvalidOperationException($"Profile '{pair.Key.name}' has invalid transition state.");
                    data.profiles.Add(new StateMachineProfileSaveData { name = pair.Key.name, weight = pair.Value });
                }
            }

            return new SavedData(GetType().FullName, new List<string> { JsonUtility.ToJson(data) });
        }

        /// <summary>Restores local profile weights and timing; biome-resolved weights update on the next weather update.</summary>
        public void Load(SavedData save)
        {
            if (save.properties == null || save.properties.Count != 1 || string.IsNullOrWhiteSpace(save.properties[0]))
            {
                Debug.LogError("COZY state machine save is missing required data.", this);
                return;
            }

            StateMachineSaveData data;
            try
            {
                data = JsonUtility.FromJson<StateMachineSaveData>(save.properties[0]);
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"Invalid COZY state machine save: {exception.Message}", this);
                return;
            }

            if (data == null || data.version != 2 || data.profiles == null ||
                float.IsNaN(data.transitionTime) || float.IsInfinity(data.transitionTime) ||
                float.IsNaN(data.timerSpeedMultiplier) || float.IsInfinity(data.timerSpeedMultiplier))
            {
                Debug.LogError("COZY state machine save contains invalid or unsupported data.", this);
                return;
            }

            var lookup = GetProfileNameLookup();
            var restoredWeights = new Dictionary<T, ProfileWeight>();
            foreach (var entry in data.profiles)
            {
                if (entry == null || string.IsNullOrEmpty(entry.name) ||
                    !lookup.TryGetValue(entry.name, out T profile) || !profile ||
                    !entry.weight.IsValid() || restoredWeights.ContainsKey(profile))
                {
                    Debug.LogError($"COZY state machine save contains an invalid, missing, or ambiguous profile: '{entry?.name}'.", this);
                    return;
                }
                restoredWeights.Add(profile, entry.weight);
            }

            T restoredSelection = null;
            if (!string.IsNullOrEmpty(data.selectedProfileName))
            {
                if (!lookup.TryGetValue(data.selectedProfileName, out T profile) ||
                    !profile || !restoredWeights.ContainsKey(profile))
                {
                    Debug.LogError($"COZY state machine save contains an invalid selected profile: '{data.selectedProfileName}'.", this);
                    return;
                }
                restoredSelection = profile;
            }

            T oldProfile = CurrentProfile;
            localProfileWeights = restoredWeights;
            profileWeights = new Dictionary<T, float>();
            foreach (var pair in restoredWeights)
                profileWeights.Add(pair.Key, pair.Value.weight);
            timeTillProfileChange = data.timeTillProfileChange;
            resolvedProfileDuration = data.resolvedProfileDuration;
            transitionTime = data.transitionTime;
            timerSpeedMultiplier = data.timerSpeedMultiplier;
            selectedProfile = restoredSelection;
            cachedStrongestProfile = CurrentProfile;
            if (oldProfile || restoredSelection)
                OnProfileChange?.Invoke(oldProfile, restoredSelection);
        }

        public void SkipTime(MeridiemTimeDelta timeToSkip)
        {
            timeTillProfileChange -= timeToSkip;
        }
    }
}