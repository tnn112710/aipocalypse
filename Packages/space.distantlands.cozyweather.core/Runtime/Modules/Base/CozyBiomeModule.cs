//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Defines a module that can also be used for biomes
    /// </summary>
    /// <typeparam name="TCozyBiomeModule">Refer to the enclosing type</typeparam>
    [AddComponentMenu("")]
    public class CozyBiomeModule<TCozyBiomeModule>
        : CozyModule, IBiomeModule
        where TCozyBiomeModule : CozyModule, IBiomeModule
    {
        internal static List<TCozyBiomeModule> biomeModuleInstances = new();
        public bool IsBiomeModule => isBiomeModule;
        private bool isBiomeModule;
        private Action<TCozyBiomeModule, float> m_CachedBiomeAction;

        /// <summary>
        /// Runs every frame only on instances of this module on a biome
        /// </summary>
        public virtual void UpdateBiome()
        {

        }

        public void ApplyBiomes()
        {
            biomeModuleInstances.Sort(biomePriorityComparison);
            int highestPriority = -1000;

            foreach (var moduleInstance in biomeModuleInstances)
            {
                if (moduleInstance.System.weight == 0)
                {
                    ApplyBiome(moduleInstance, 0f);
                    continue;
                }
                if (moduleInstance.System.Priority <= highestPriority) continue;

                ApplyBiome(moduleInstance, moduleInstance.System.weight);

                highestPriority = moduleInstance.System.Priority;
            }
        }

        public override void InitializeModule()
        {
            System = GetComponentInParent<CozySystem>();
            isBiomeModule = System is CozyBiome;
            if (isBiomeModule)
                RegisterBiomeModule(this as TCozyBiomeModule);

            m_CachedBiomeAction = ApplyBiome;

        }

        public virtual void ApplyBiome(TCozyBiomeModule biome, float weight)
        {

        }

        /// <summary>
        /// Registers a biome module to the static list
        /// </summary>
        static void RegisterBiomeModule(TCozyBiomeModule biomeModule)
        {
            biomeModuleInstances.Add(biomeModule);
        }

        /// <summary>
        /// Unregisters a biome module from the static list
        /// </summary>
        static void UnregisterBiomeModule(TCozyBiomeModule biomeModule)
        {
            biomeModuleInstances.Remove(biomeModule);
        }

        static readonly Comparison<TCozyBiomeModule> biomePriorityComparison = CompareBiomePriority;

        static int CompareBiomePriority(TCozyBiomeModule x, TCozyBiomeModule y)
        {
            return x.System.Priority.CompareTo(y.System.Priority);
        }

        public override void DeinitializeModule()
        {
            if (isBiomeModule)
                UnregisterBiomeModule(this as TCozyBiomeModule);
        }

        internal override void SubscribeToExecutionEvents()
        {
            if (isBiomeModule)
                CozyWeather.OnCozyUpdateLoop += UpdateBiome;
            else
                base.SubscribeToExecutionEvents();
        }
        internal override void UnsubscribeToExecutionEvents()
        {
            if (isBiomeModule)
                CozyWeather.OnCozyUpdateLoop -= UpdateBiome;
            else
                base.UnsubscribeToExecutionEvents();
        }
    }
}