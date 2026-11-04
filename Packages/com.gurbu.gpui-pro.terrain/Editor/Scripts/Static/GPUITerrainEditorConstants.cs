// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GPUInstancerPro.TerrainModule
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static class GPUITerrainEditorConstants
    {
        public static string GetUIPath()
        {
            return GPUITerrainConstants.GetPackagesPath() + GPUIConstants.PATH_EDITOR + GPUIEditorConstants.PATH_UI;
        }

        public static Func<VisualElement, List<GPUIHelpBox>, GPUIDetailManager, GPUITerrain, string, SerializedProperty, List<int>, Action, string, Func<int, string>, Foldout> DrawProceduralDensitySettingsDelegate;
        public static Foldout DrawProceduralDensitySettings(VisualElement container, List<GPUIHelpBox> helpBoxes, GPUIDetailManager gpuiDetailManager, GPUITerrain gpuiTerrain, string storedValueKey, SerializedProperty proceduralDensityDataArraySP, List<int> selectedIndexes, Action OnValuesChanged, string subPropPath, Func<int, string> GetPrototypeName)
        {
            if (DrawProceduralDensitySettingsDelegate == null)
                return null;
            return DrawProceduralDensitySettingsDelegate(container, helpBoxes, gpuiDetailManager, gpuiTerrain, storedValueKey, proceduralDensityDataArraySP, selectedIndexes, OnValuesChanged, subPropPath, GetPrototypeName);
        }
    }
}
