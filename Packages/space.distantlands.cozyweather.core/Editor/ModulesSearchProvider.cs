//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Experimental.GraphView;
using System.Linq;

namespace DistantLands.Cozy.EditorScripts
{
    /// <summary>
    /// Search provider for modules
    /// </summary>
    public class ModulesSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public CozyWeather weather;
        public CozyWeatherEditor cozyWeatherEditor;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Select a Module"), 0)
            };

            List<SearchTreeGroupEntry> groups = new List<SearchTreeGroupEntry>();

            for (int i = CozyModuleCollection.ModuleTypes.Count - 1; i >= 0; i--)
            {
                Type type = CozyModuleCollection.ModuleTypes[i];
                CozyModuleAttribute attribute = type.GetCustomAttribute<CozyModuleAttribute>();

                if (CozyWeather.Instance.HasModule(type)) continue;

                if (attribute.category == "")
                {
                    if (!groups.Any(x => x.content.text == "Other"))
                    {
                        SearchTreeGroupEntry groupEntry = new SearchTreeGroupEntry(new GUIContent("Other"), 1);
                        groups.Add(groupEntry);
                    }
                }
                else
                {
                    if (!groups.Any(x => x.content.text == attribute.category))
                    {
                        SearchTreeGroupEntry groupEntry = new SearchTreeGroupEntry(new GUIContent(attribute.category), 1);
                        groups.Add(groupEntry);
                    }
                }
            }

            groups = groups.OrderBy(x => x.content.text).ToList();

            foreach (SearchTreeGroupEntry searchTreeGroup in groups)
            {
                entries.Add(searchTreeGroup);
            }

            for (int i = CozyModuleCollection.ModuleTypes.Count - 1; i >= 0; i--)
            {
                Type type = CozyModuleCollection.ModuleTypes[i];
                CozyModuleAttribute attribute = type.GetCustomAttribute<CozyModuleAttribute>();

                if (CozyWeather.Instance.HasModule(type)) continue;

                SearchTreeEntry entry = GetEntry(type, attribute);

                if (attribute.category == "")
                {
                    entries.Insert(entries.FindIndex(x => x is SearchTreeGroupEntry && x.content.text == "Other") + 1, entry);
                }
                else
                {
                    entries.Insert(entries.FindIndex(x => x is SearchTreeGroupEntry && x.content.text == attribute.category) + 1, entry);
                }
            }

            return entries;
        }

        public SearchTreeEntry GetEntry(Type type, CozyModuleAttribute attribute)
        {
            GUIContent content = new GUIContent(attribute.name, (Texture)Resources.Load($"COZY Icons/{attribute.icon}"), attribute.tooltip);

            return new SearchTreeEntry(content)
            {
                userData = type,
                level = 2
            };
        }
        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            // if ((Type)SearchTreeEntry.userData == typeof(CreateCozyModule))
            // {
            //     CreateCozyModuleWizard.ShowSetupWizard(cozyWeatherEditor);
            //     return true;
            // }
            ICozyModule newMod = weather?.AddModule((Type)SearchTreeEntry.userData);
            cozyWeatherEditor.ResetModuleEditors();
            cozyWeatherEditor.RefreshModuleIcons();
            cozyWeatherEditor.RepaintUI();
            cozyWeatherEditor.SelectModule(newMod);
            return true;
        }

    }

    /// <summary>
    /// Search provider for specifically biome modules
    /// </summary>
    public class BiomeModulesSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public CozyBiome biome;
        public CozyBiomeEditor editor;

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Select a Module"), 0)
            };

            List<SearchTreeGroupEntry> groups = new List<SearchTreeGroupEntry>();

            for (int i = CozyModuleCollection.ModuleTypes.Count - 1; i >= 0; i--)
            {
                Type type = CozyModuleCollection.ModuleTypes[i];
                CozyModuleAttribute attribute = type.GetCustomAttribute<CozyModuleAttribute>();

                if (!typeof(IBiomeModule).IsAssignableFrom(type)) continue;
                if (biome.HasModule(type)) continue;

                if (attribute.category == "")
                {
                    if (!groups.Any(x => x.content.text == "Other"))
                    {
                        SearchTreeGroupEntry groupEntry = new SearchTreeGroupEntry(new GUIContent("Other"), 1);
                        groups.Add(groupEntry);
                    }
                }
                else
                {
                    if (!groups.Any(x => x.content.text == attribute.category))
                    {
                        SearchTreeGroupEntry groupEntry = new SearchTreeGroupEntry(new GUIContent(attribute.category), 1);
                        groups.Add(groupEntry);
                    }
                }
            }

            groups = groups.OrderBy(x => x.content.text).ToList();

            foreach (SearchTreeGroupEntry searchTreeGroup in groups)
            {
                entries.Add(searchTreeGroup);
            }

            for (int i = CozyModuleCollection.ModuleTypes.Count - 1; i >= 0; i--)
            {
                Type type = CozyModuleCollection.ModuleTypes[i];
                CozyModuleAttribute attribute = type.GetCustomAttribute<CozyModuleAttribute>();

                if (!typeof(IBiomeModule).IsAssignableFrom(type)) continue;
                if (biome.HasModule(type)) continue;

                SearchTreeEntry entry = GetEntry(type, attribute);

                if (attribute.category == "")
                {
                    entries.Insert(entries.FindIndex(x => x is SearchTreeGroupEntry && x.content.text == "Other") + 1, entry);
                }
                else
                {
                    entries.Insert(entries.FindIndex(x => x is SearchTreeGroupEntry && x.content.text == attribute.category) + 1, entry);
                }
            }

            if (entries.Count == 1)
            {
                entries.Add(new SearchTreeEntry(new GUIContent("No Modules Found"))
                {
                    level = 1,
                    userData = null
                });
            }

            return entries;
        }

        public SearchTreeEntry GetEntry(Type type, CozyModuleAttribute attribute)
        {
            GUIContent content = new GUIContent(attribute.name, (Texture)Resources.Load($"COZY Icons/{attribute.icon}"), attribute.tooltip);

            return new SearchTreeEntry(content)
            {
                userData = type,
                level = 2
            };
        }
        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            if (SearchTreeEntry.userData == null)
            {
                return false;
            }
            ICozyModule newMod = biome?.AddModule((Type)SearchTreeEntry.userData);
            editor.ResetModuleEditors();
            editor.RefreshModuleIcons();
            editor.RepaintUI();
            editor.SelectModule(newMod);
            return true;
        }

    }

}
