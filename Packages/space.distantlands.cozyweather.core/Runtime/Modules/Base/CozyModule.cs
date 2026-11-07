//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEngine.SceneManagement;
using System;

namespace DistantLands.Cozy
{

    public abstract class CozyModule : MonoBehaviour, ICozyModule
    {

        public CozyWeather WeatherSphere
        {
            get
            {
                if (cachedWeatherSphere == null)
                {
                    cachedWeatherSphere = CozyWeather.Instance;
                }

                return cachedWeatherSphere;
            }

            set
            {
                cachedWeatherSphere = value;
            }
        }
        private CozyWeather cachedWeatherSphere;

        public virtual CozySystem System
        {
            get
            {
                return cachedSystem;
            }
            set
            {
                cachedSystem = value;
            }
        }
        internal CozySystem cachedSystem;

        public bool Active
        {
            get
            {
                return active;
            }
            set
            {
                active = value;
            }
        }
        public static implicit operator bool(CozyModule module)
        {
            return module != null;
        }
        private bool active = true;
        public Type[] Requirements => ModuleRequirements;
        /// <summary>
        /// List up to 5 required modules on the system in order for this module to be added. Use the DependsOn&lt;Types&gt; method to get the list of types.
        /// </summary>
        public virtual Type[] ModuleRequirements { get; }
        protected Type[] DependsOn<T1>() where T1 : ICozyModule
        => new[] { typeof(T1) };

        protected Type[] DependsOn<T1, T2>()
            where T1 : ICozyModule
            where T2 : ICozyModule
            => new[] { typeof(T1), typeof(T2) };
        protected Type[] DependsOn<T1, T2, T3>()
            where T1 : ICozyModule
            where T2 : ICozyModule
            where T3 : ICozyModule
            => new[] { typeof(T1), typeof(T2), typeof(T3) };
        protected Type[] DependsOn<T1, T2, T3, T4>()
            where T1 : ICozyModule
            where T2 : ICozyModule
            where T3 : ICozyModule
            where T4 : ICozyModule
            => new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4) };

        protected Type[] DependsOn<T1, T2, T3, T4, T5>()
            where T1 : ICozyModule
            where T2 : ICozyModule
            where T3 : ICozyModule
            where T4 : ICozyModule
            where T5 : ICozyModule
            => new[] { typeof(T1), typeof(T2), typeof(T3), typeof(T4), typeof(T5) };

        void OnEnable()
        {
            InitializeModule();
            SubscribeToExecutionEvents();
        }
        void OnDisable()
        {
            DeinitializeModule();
            UnsubscribeToExecutionEvents();
        }
        public virtual void InitializeModule()
        {
            
        }
        internal virtual void SubscribeToExecutionEvents()
        {
            // Unsubscribe first to prevent duplicates if called multiple times
            CozyWeather.OnFrameReset -= FrameReset;
            CozyWeather.OnUpdateWeatherWeights -= OnUpdateWeatherWeights;
            CozyWeather.OnSendToGPU -= PropagateVariables;
            CozyWeather.OnCozyUpdateLoop -= OnCozyUpdateLoop;

            // Now subscribe
            CozyWeather.OnFrameReset += FrameReset;
            CozyWeather.OnUpdateWeatherWeights += OnUpdateWeatherWeights;
            CozyWeather.OnSendToGPU += PropagateVariables;
            CozyWeather.OnCozyUpdateLoop += OnCozyUpdateLoop;
        }

        public virtual bool CheckIfModuleCanBeRemoved(out string warning)
        {
            warning = "";
            return true;
        }
        public virtual bool CheckIfModuleCanBeAdded(out string warning)
        {
            warning = "";
            return true;
        }
        public virtual void FrameReset()
        {

        }
        public virtual void OnUpdateWeatherWeights()
        {

        }
        public virtual void PropagateVariables()
        {

        }
        public virtual void OnCozyUpdateLoop()
        {

        }
        public virtual void OnSceneLoaded(LoadSceneMode mode)
        {

        }

        public virtual void OnSceneUnloaded()
        {

        }
        public void ResetModule()
        {
            DeinitializeModule();
            InitializeModule();
            
            UnsubscribeToExecutionEvents();
            SubscribeToExecutionEvents();
        }
        public virtual void SetupModule()
        {
            if (Requirements != null)
                foreach (Type type in Requirements)
                {
                    if (!WeatherSphere.HasModule(type))
                    {
                        WeatherSphere.AddModule(type);
                        Debug.Log($"{GetType().Name} requires {type.Name} to function. {type.Name} has been automatically added to the weather sphere!");
                    }
                }
        }

        public virtual void DeinitializeModule()
        {
            
        }
        internal virtual void UnsubscribeToExecutionEvents()
        {
            CozyWeather.OnFrameReset -= FrameReset;
            CozyWeather.OnUpdateWeatherWeights -= OnUpdateWeatherWeights;
            CozyWeather.OnSendToGPU -= PropagateVariables;
            CozyWeather.OnCozyUpdateLoop -= OnCozyUpdateLoop;
        }


    }
}