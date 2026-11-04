// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace GPUInstancerPro.PrefabModule
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [InitializeOnLoad]
    public static class GPUIPrefabDefines
    {
        private static readonly string PACKAGE_NAME = "com.gurbu.gpui-pro.prefab";
        private static readonly string[] AUTO_PACKAGE_IMPORTER_GUIDS = { };
        private static int _demoShaderRetryCount;
        private static double _demoShaderRetryTime;

        static GPUIPrefabDefines()
        {
            GPUIDefines.OnPackageVersionChanged -= OnVersionChanged;
            GPUIDefines.OnPackageVersionChanged += OnVersionChanged;
            GPUIDefines.OnImportPackages -= ImportPackages;
            GPUIDefines.OnImportPackages += ImportPackages;
            GPUIDefines.OnImportDemos -= OnDemosImported;
            GPUIDefines.OnImportDemos += OnDemosImported;

            // Delayed to wait for asset loading
            EditorApplication.delayCall -= DelayedInitialization;
            EditorApplication.delayCall += DelayedInitialization;
        }

        static void DelayedInitialization()
        {
            if (!GPUIEditorSettings.Instance.IsSubModuleExists(PACKAGE_NAME))
                GPUIEditorSettings.Instance.RequirePackageReload();
        }

        private static void OnVersionChanged(string packageName)
        {
            if (packageName == PACKAGE_NAME && GPUIEditorSettings.Instance.IsSubModuleVersionChanged(PACKAGE_NAME, out string currentVersion, out string processedVersion))
            {
#if GPUIPRO_DEVMODE
                Debug.Log(GPUIConstants.LOG_PREFIX + GPUIConstants.LOG_PREFIX_DEV + PACKAGE_NAME + " version changed from: " + processedVersion + " to: " + currentVersion);
#endif
                GPUIEditorSettings.Instance.SetSubModuleProcessedVersion(packageName, currentVersion);

                ImportPackages(false);
            }
        }

        public static void ImportPackages(bool forceReimport)
        {
            GPUIProPackageImporter.ImportPackages(AUTO_PACKAGE_IMPORTER_GUIDS, forceReimport);
        }

        private static void OnDemosImported()
        {
            EditorApplication.update -= RetryDemoShaderGeneration;
            if (GPUIRuntimeSettings.Instance.IsBuiltInRP)
            {
                _demoShaderRetryCount = 0;
                return;
            }

            try
            {
                string assetPath = GPUIConstants.GetDefaultPath() + GPUIProDemoImporter.DEMOS_PATH + "/Prefab/_SharedResources/SRPDependent/MaterialVariations/TreePineLP_GPUIVariationDefinition.asset";
                GPUIMaterialVariationDefinition mvDef = AssetDatabase.LoadAssetAtPath<GPUIMaterialVariationDefinition>(assetPath);
                if (mvDef != null)
                    GPUIMaterialVariationEditorUtility.GenerateShader(mvDef);
#if GPUIPRO_DEVMODE
                else
                    Debug.LogWarning(GPUIConstants.LOG_PREFIX + GPUIConstants.LOG_PREFIX_DEV + "Cant find asset at path: " + assetPath);
#endif

#if GPUI_CROWD
                assetPath = GPUIConstants.GetDefaultPath() + GPUIProDemoImporter.DEMOS_PATH + "/CrowdAnimations/_SharedResources/SRPDependent/MaterialVariations/Chomper_GPUIVariationDefinition.asset";
                mvDef = AssetDatabase.LoadAssetAtPath<GPUIMaterialVariationDefinition>(assetPath);
                if (mvDef != null)
                    GPUIMaterialVariationEditorUtility.GenerateShader(mvDef);
#if GPUIPRO_DEVMODE
                else
                    Debug.LogWarning(GPUIConstants.LOG_PREFIX + GPUIConstants.LOG_PREFIX_DEV + "Cant find asset at path: " + assetPath);
#endif
#endif
                _demoShaderRetryCount = 0;
            }
            catch (IOException exception) when (GPUIEditorUtility.IsFileMappedException(exception))
            {
                if (_demoShaderRetryCount >= 5)
                {
                    _demoShaderRetryCount = 0;
                    Debug.LogError(GPUIConstants.LOG_PREFIX + "Could not generate demo variation shaders because a file is still memory-mapped: " + exception.Message);
                    return;
                }

                _demoShaderRetryCount++;
                _demoShaderRetryTime = EditorApplication.timeSinceStartup + 1;
                EditorApplication.update += RetryDemoShaderGeneration;
#if GPUIPRO_DEVMODE
                Debug.LogWarning(GPUIConstants.LOG_PREFIX + GPUIConstants.LOG_PREFIX_DEV + "File is memory-mapped, retry scheduled: " + exception.Message);
#endif
            }
        }

        private static void RetryDemoShaderGeneration()
        {
            if (EditorApplication.timeSinceStartup < _demoShaderRetryTime)
                return;
            OnDemosImported();
        }
    }
}
