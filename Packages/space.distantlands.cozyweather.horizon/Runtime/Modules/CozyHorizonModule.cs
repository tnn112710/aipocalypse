using System;
using System.Collections.Generic;
using UnityEngine;

namespace DistantLands.Cozy.Horizon
{
    [ExecuteAlways]
    [AddComponentMenu("")]
    [CozyModule("Horizon", "Horizon", "Add stylized backgrounds to your weather system", "Atmosphere")]
    public class CozyHorizonModule : CozyBiomeModule<CozyHorizonModule>
    {
        [SerializeField]
        [CozySearchable("horizon", "skybox", "layers")]
        private CozyHorizonProfile profile;

        public CozyHorizonProfile Profile
        {
            get => profile;
            set => profile = value;
        }

        public SortedList<int, HorizonLayerInstance> ActiveLayers => activeLayers;
        private SortedList<int, HorizonLayerInstance> activeLayers;


        public override void InitializeModule()
        {
            base.InitializeModule();

            if (IsBiomeModule) return;

            activeLayers = new SortedList<int, HorizonLayerInstance>();
        }

        public override void OnCozyUpdateLoop()
        {
            activeLayers.Clear();

            if (Profile == null) return;

            foreach (var layer in Profile.layers)
            {
                int resolvedDistance = layer.distance;
                while (activeLayers.ContainsKey(resolvedDistance))
                    resolvedDistance++;
                activeLayers.Add(resolvedDistance, new(layer, System.Weight));
            }

            ApplyBiomes();
        }

        public override void ApplyBiome(CozyHorizonModule biome, float weight)
        {
            for (int i = 0; i < activeLayers.Count; i++)
            {
                var layer = activeLayers[i];
                layer.blend = Mathf.Lerp(layer.blend, 0, weight);
                activeLayers[i] = layer;
            }

            foreach (var layer in biome.Profile.layers)
            {
                int resolvedDistance = layer.distance;
                while (activeLayers.ContainsKey(resolvedDistance))
                    resolvedDistance++;
                activeLayers.Add(resolvedDistance, new(layer, weight));
            }
        }
    }
}
