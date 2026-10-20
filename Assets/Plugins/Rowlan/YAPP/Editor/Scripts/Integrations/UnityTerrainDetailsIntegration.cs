using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Yapp
{
    public class UnityTerrainDetailsIntegration
    {
        private int detailDensity = 16;
        private float overlapThreshold = 1.0f;

        SerializedProperty targetTerrain;

        PrefabPainterEditor editor;

        UnityTerrainDetailManager terrainDetailManager;

        public UnityTerrainDetailsIntegration(PrefabPainterEditor editor)
        {
            this.editor = editor;

            terrainDetailManager = new UnityTerrainDetailManager(editor);

            targetTerrain = editor.FindProperty(x => x.brushSettings.targetTerrain);
        }

        public void OnInspectorGUI()
        {
            GUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Terrain Details", GUIStyles.BoxTitleStyle);

                if (this.editor.GetPainter().brushSettings.targetTerrain == null)
                {
                    editor.SetErrorBackgroundColor();
                }

                EditorGUILayout.PropertyField(targetTerrain, new GUIContent("Target Terrain", "The terrain to work with"));

                editor.SetDefaultBackgroundColor();

                EditorGUILayout.Space();

                detailDensity = EditorGUILayout.IntSlider(new GUIContent("Detail Density", "Density of details to paint (1-16)"), detailDensity, 1, 16);

                if (!editor.GetPainter().brushSettings.allowOverlap)
                {
                    overlapThreshold = EditorGUILayout.Slider(new GUIContent("Overlap Threshold", "Maximum density allowed as percentage of max (when Allow Overlap is OFF)"), overlapThreshold, 0.1f, 1.0f);
                }

                EditorGUILayout.Space();

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(new GUIContent("Extract Prefabs", "Replace the prefabs with the ones from the Unity terrain details"), GUILayout.Width(100)))
                    {
                        CreatePrefabSettingsFromUnityTerrain();
                    }

                    if (GUILayout.Button(new GUIContent("Clear Prefabs", "Remove all prefab settings"), GUILayout.Width(100)))
                    {
                        ClearPrefabs();
                    }

                    if (GUILayout.Button(new GUIContent("Log Info", "Log terrain detail info to the console"), GUILayout.Width(100)))
                    {
                        LogInfo();
                    }

                    if (GUILayout.Button(new GUIContent("Clear Terrain", "Remove all details from the terrain"), GUILayout.Width(120)))
                    {
                        RemoveAll();
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.HelpBox("Terrain Details is highly experimental and not fully implemented yet! Backup your project!", MessageType.Warning);
                EditorGUILayout.HelpBox("This paints Unity terrain details into terrain data, as such not all YAPP features apply. If details don't show up while painting, check their settings (eg density) on the terrain itself.", MessageType.Info);
            }
            GUILayout.EndVertical();
        }

        private void LogInfo()
        {
            terrainDetailManager.LogPrototypes();
        }

        private void RemoveAll()
        {
            terrainDetailManager.RemoveAllInstances();
        }

        public void AddNewPrefab(PrefabSettings prefabSettings, Vector3 newPosition, Quaternion newRotation, Vector3 newLocalScale, float overlapOffset)
        {
            float brushSize = editor.GetPainter().brushSettings.brushSize;

            if (editor.GetPainter().brushSettings.distribution == BrushSettings.Distribution.Poisson_Any || editor.GetPainter().brushSettings.distribution == BrushSettings.Distribution.Poisson_Terrain)
            {
                brushSize = editor.GetPainter().brushSettings.poissonDiscSize;
            }

            GameObject prefab = prefabSettings.prefab;
            bool allowOverlap = editor.GetPainter().brushSettings.allowOverlap;
            bool autoAddPrototype = editor.GetPainter().brushSettings.autoAddPrototype;

            terrainDetailManager.PlacePrefab(prefab, autoAddPrototype, newPosition, newLocalScale, newRotation, brushSize, detailDensity, allowOverlap, overlapThreshold);
        }

        public void RemovePrefabs(RaycastHit raycastHit)
        {
            Vector3 position = raycastHit.point;
            float brushSize = editor.GetPainter().brushSettings.brushSize;

            terrainDetailManager.RemoveOverlapping(position, brushSize);
        }

        private void CreatePrefabSettingsFromUnityTerrain()
        {
            List<GameObject> prefabs = terrainDetailManager.ExtractPrefabs();

            editor.AddPrefabs(Constants.TEMPLATE_NAME_DETAIL, prefabs, true);
        }

        private void ClearPrefabs()
        {
            editor.ClearPrefabs();
        }
    }
}
