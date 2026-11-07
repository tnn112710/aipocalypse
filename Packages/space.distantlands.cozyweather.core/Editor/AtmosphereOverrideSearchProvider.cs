//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using UnityEditor;
using System.Linq;
using System.Reflection;

namespace DistantLands.Cozy.EditorScripts
{
    /// <summary>
    /// Search provider for atmosphere overrides on atmosphere profiles and modules
    /// </summary>
    public class AtmosphereOverrideSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public AtmosphereProfile profile;
        public List<AtmosphereOverride> overridesList;
        [NonSerialized]
        public SerializedProperty arrayProperty;
        public Action action;
        public AtmosphereModel model;

        private static void DisablePropertyOverrides(AtmosphereOverride atmosphereOverride)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public;

            foreach (FieldInfo propertyField in atmosphereOverride.GetType().GetFields(flags))
            {
                Type propertyType = propertyField.FieldType;
                if (!propertyType.IsGenericType || propertyType.GetGenericTypeDefinition() != typeof(AtmosphereProperty<>))
                    continue;

                object property = propertyField.GetValue(atmosphereOverride);
                propertyType.GetField(nameof(AtmosphereProperty<FloatValue>.overrideValue), flags).SetValue(property, false);
                propertyField.SetValue(atmosphereOverride, property);
            }
        }


        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>()
            {
                new SearchTreeGroupEntry(new GUIContent("Add Override"), 0),
            };

            if (overridesList == null)
                overridesList = new List<AtmosphereOverride>();

                            entries.Add(new SearchTreeGroupEntry(new GUIContent("Lighting"), 1));
            if (!overridesList.Any(x => x is LightingSunOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Sun")){ userData = new LightingSunOverride(), level=2});
            if (!overridesList.Any(x => x is LightingSunCloudShadowsOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Sun Cloud Shadows")){ userData = new LightingSunCloudShadowsOverride(), level=2});
            if (!overridesList.Any(x => x is LightingSunLensFlareOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Sun Lens Flare")){ userData = new LightingSunLensFlareOverride(), level=2});
            if (!overridesList.Any(x => x is LightingMoonOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Moon")){ userData = new LightingMoonOverride(), level=2});
            if (!overridesList.Any(x => x is LightingMoonLensFlareOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Moon Lens Flare")){ userData = new LightingMoonLensFlareOverride(), level=2});
            if (!overridesList.Any(x => x is LightingAmbientOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Ambient")){ userData = new LightingAmbientOverride(), level=2});
                entries.Add(new SearchTreeGroupEntry(new GUIContent("Sky"), 1));
            if (!overridesList.Any(x => x is SkyGradientOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Gradient")){ userData = new SkyGradientOverride(), level=2});
            if (!overridesList.Any(x => x is SkyTwoToneOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Two Tone")){ userData = new SkyTwoToneOverride(), level=2});
            if (!overridesList.Any(x => x is SkyStarsOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Stars")){ userData = new SkyStarsOverride(), level=2});
            if (!overridesList.Any(x => x is SkyConstellationsOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Constellations")){ userData = new SkyConstellationsOverride(), level=2});
            if (!overridesList.Any(x => x is SkyGalaxyOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Galaxy")){ userData = new SkyGalaxyOverride(), level=2});
            if (!overridesList.Any(x => x is SkyRainbowOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Rainbow")){ userData = new SkyRainbowOverride(), level=2});
            if (!overridesList.Any(x => x is SkyLightColumnsOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Light Columns")){ userData = new SkyLightColumnsOverride(), level=2});
            if (!overridesList.Any(x => x is SkyShootingStarsOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Shooting Stars")){ userData = new SkyShootingStarsOverride(), level=2});
            if (!overridesList.Any(x => x is SkySunDiskOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Sun Disk")){ userData = new SkySunDiskOverride(), level=2});
            if (!overridesList.Any(x => x is SkyMoonDiskOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Moon Disk")){ userData = new SkyMoonDiskOverride(), level=2});
                entries.Add(new SearchTreeGroupEntry(new GUIContent("Fog"), 1));
            if (!overridesList.Any(x => x is FogGradientOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Gradient")){ userData = new FogGradientOverride(), level=2});
            if (!overridesList.Any(x => x is FogSunLightFlareOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Sun Light Flare")){ userData = new FogSunLightFlareOverride(), level=2});
            if (!overridesList.Any(x => x is FogMoonLightFlareOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Moon Light Flare")){ userData = new FogMoonLightFlareOverride(), level=2});
            if (!overridesList.Any(x => x is FogTwoToneOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Two Tone")){ userData = new FogTwoToneOverride(), level=2});
            if (!overridesList.Any(x => x is FogHeightOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Height")){ userData = new FogHeightOverride(), level=2});
            if (!overridesList.Any(x => x is FogBaseOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Base")){ userData = new FogBaseOverride(), level=2});
                entries.Add(new SearchTreeGroupEntry(new GUIContent("Clouds"), 1));
            if (!overridesList.Any(x => x is CloudsVolumetricOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Volumetric")){ userData = new CloudsVolumetricOverride(), level=2});
            if (!overridesList.Any(x => x is CloudsCumulonimbusOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Cumulonimbus")){ userData = new CloudsCumulonimbusOverride(), level=2});
            if (!overridesList.Any(x => x is CloudsCumulusOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Cumulus")){ userData = new CloudsCumulusOverride(), level=2});
            if (!overridesList.Any(x => x is CloudsAltocumulusOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Altocumulus")){ userData = new CloudsAltocumulusOverride(), level=2});
            if (!overridesList.Any(x => x is CloudsCirrusOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Cirrus")){ userData = new CloudsCirrusOverride(), level=2});
            if (!overridesList.Any(x => x is CloudsContrailsOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Contrails")){ userData = new CloudsContrailsOverride(), level=2});
                entries.Add(new SearchTreeGroupEntry(new GUIContent("Sunshafts"), 1));
            if (!overridesList.Any(x => x is SunshaftsScreenspaceOverride))
                entries.Add(new SearchTreeEntry(new GUIContent("    Screenspace")){ userData = new SunshaftsScreenspaceOverride(), level=2});


            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry searchTreeEntry, SearchWindowContext context)
        {
            AtmosphereOverride atmosphereOverride = (AtmosphereOverride)searchTreeEntry.userData;
            atmosphereOverride.ResetToDefaults();
            if (model != null)
            {
                atmosphereOverride.CopyModel(model);
                DisablePropertyOverrides(atmosphereOverride);
            }
            int index = arrayProperty.arraySize;
            arrayProperty.InsertArrayElementAtIndex(index);
            arrayProperty.GetArrayElementAtIndex(index);
            overridesList.Add((AtmosphereOverride)searchTreeEntry.userData);
            action.Invoke();
            SerializedProperty newElement = arrayProperty.GetArrayElementAtIndex(index);
            newElement.managedReferenceValue = atmosphereOverride;
            arrayProperty.serializedObject.ApplyModifiedProperties();
            arrayProperty.serializedObject.Update();
            return true;
        }
    }
}