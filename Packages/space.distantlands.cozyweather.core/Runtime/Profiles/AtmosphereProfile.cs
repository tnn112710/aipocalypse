//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DistantLands.Cozy
{
        [System.Serializable]
        [CreateAssetMenu(menuName = "Distant Lands/Cozy/Atmosphere Profile", order = 361)]
        public class AtmosphereProfile : CozyProfile
        {
#if UNITY_EDITOR
                public string Name => name;
                public string Description { get => description; set => description = value; }
                [SerializeField]
                private string description = "Custom atmosphere profile";
                public string Category { get => category; set => category = value; }
                [SerializeField]
                private string category = "Custom";
                public Texture2D Icon => icon;
                [SerializeField]
                private Texture2D icon;
                public bool Locked { get => locked; set => locked = value; }
                [SerializeField]
                private bool locked;
#endif

                public AtmospherePresetParameters presetParameters;
                [SerializeReference]
                [AtmosphereOverridesList]
                public List<AtmosphereOverride> overrides;

                private void OnValidate()
                {
                        #if UNITY_6000_5_OR_NEWER
                        foreach (CozyAtmosphereModule atmosphereModule in FindObjectsByType<CozyAtmosphereModule>(FindObjectsInactive.Include))
                        #else
                        foreach (CozyAtmosphereModule atmosphereModule in FindObjectsByType<CozyAtmosphereModule>(FindObjectsSortMode.None))
                        #endif
                        {
                                if (atmosphereModule.AtmosphereProfile == this)
                                        atmosphereModule.UpdateModel();
                        }
                }
        }
}