//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DistantLands.Cozy.EditorScripts
{
    [InitializeOnLoad]
    public class IntegrationValidator : AssetPostprocessor
    {
        // InitializeOnLoad ensures this runs immediately on project load or script compilation
        static IntegrationValidator()
        {
            EditorApplication.delayCall += ValidateProjectIntegrations;
        }

        // AssetPostprocessor ensures this runs if a user imports a new integration package
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // Simple check to avoid running on irrelevant asset imports
            if (importedAssets.Any(a => a.Contains("Cozy") || a.EndsWith(".cs")))
            {
                ValidateProjectIntegrations();
            }
        }

        public static void ValidateProjectIntegrations()
        {
            var settings = CozyIntegrations.Instance;
            if (settings == null) return;

            SerializedObject serializedSettings = CozyIntegrations.GetSerializedSettings();
            SerializedProperty integrationsProp = serializedSettings.FindProperty("integrations");

            serializedSettings.Update();
#if UNITY_6000_5_OR_NEWER
            var systemTypes = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => typeof(CozyIntegration).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();
#else
            var systemTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(t => typeof(CozyIntegration).IsAssignableFrom(t) && !t.IsAbstract)
                .ToList();
#endif

            for (int i = integrationsProp.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty element = integrationsProp.GetArrayElementAtIndex(i);

                if (element.managedReferenceValue == null)
                {
                    integrationsProp.DeleteArrayElementAtIndex(i);
                }
            }

            var serializedTypes = new HashSet<Type>();
            for (int i = 0; i < integrationsProp.arraySize; i++)
            {
                var obj = integrationsProp.GetArrayElementAtIndex(i).managedReferenceValue;
                if (obj != null)
                {
                    serializedTypes.Add(obj.GetType());
                }
            }

            bool addedAny = false;
            foreach (var type in systemTypes)
            {
                if (!serializedTypes.Contains(type))
                {
                    try
                    {
                        var newIntegration = (CozyIntegration)Activator.CreateInstance(type);

                        int newIndex = integrationsProp.arraySize;
                        integrationsProp.InsertArrayElementAtIndex(newIndex);

                        var newElement = integrationsProp.GetArrayElementAtIndex(newIndex);
                        newElement.managedReferenceValue = newIntegration;

                        addedAny = true;
                        Debug.Log($"[COZY] Automatically registered new integration: {type.Name}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[COZY] Failed to auto-instantiate integration type {type.Name}: {e.Message}");
                    }
                }
            }

            if (addedAny || serializedSettings.hasModifiedProperties)
            {
                serializedSettings.ApplyModifiedProperties();
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }
    }
}