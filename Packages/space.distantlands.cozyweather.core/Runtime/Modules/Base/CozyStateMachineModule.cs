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
    public struct ProfileWeight
    {
        public float weight;
        internal bool isTransitioning;
        private float targetWeight;
        private float startingWeight;
        private float t;
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
    }


    /// <summary>
    /// Creates a module that uses a state machine to blend profiles (states) into a parameters struct for output. Used for modules such as the Weather module, Ambience module, and ReSound module.
    /// </summary>
    /// <typeparam name="T">The profile used in the state machine</typeparam>
    public abstract class CozyStateMachineModule<T>
        : CozyBiomeModule<CozyStateMachineModule<T>>, ISkippable
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

        public override void InitializeModule()
        {
            Setup();

            if (IsBiomeModule) return;

            profileWeights = new Dictionary<T, float>();
        }

        private void Setup()
        {
            localProfileWeights = new();

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
                if (WeatherSphere.Time != null)
                    timeTillProfileChange -= Time.deltaTime * WeatherSphere.Time.TimeMovementSpeed * timerSpeedMultiplier;
                else
                    timeTillProfileChange -= Time.deltaTime / 1440f * timerSpeedMultiplier;

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
            if (resetTimer)
            {
                timeTillProfileChange = duration;
            }
            if (!Application.isPlaying || !transition)
            {
                localProfileWeights = new Dictionary<T, ProfileWeight>
                {
                    { profile, new ProfileWeight(1) }
                };

                return true;
            }
            if (profile == null) return false;
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
            if (profile == null) return false;
            return SetProfile(profile, GetProfilePlayTime(profile), resetTimer, transition);
        }

        public virtual float GetProfilePlayTime(T profile)
        {
            return Random.Range(profile.MinTime, profile.MaxTime);
        }

        #endregion

        public void SkipTime(MeridiemTimeDelta timeToSkip)
        {
            timeTillProfileChange -= timeToSkip;
        }
    }
}