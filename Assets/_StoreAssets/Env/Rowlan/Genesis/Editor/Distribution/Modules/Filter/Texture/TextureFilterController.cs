using JBooth.MicroVerseCore;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Distribution
{
    public class TextureFilterController
    {
        private enum TargetType
        {
            Hierarchy,
            Sibling,
            Selection,
            TargetChildren,
        }

        private DistributionModule editor;

        private TargetType targetType = TargetType.Hierarchy;
        private GameObject targetObject = null;

        public TextureFilterController(DistributionModule editor)
        {
            this.editor = editor;

            SerializedProperty settings = editor.GetSerializedObject().FindProperty("filterSettings");

        }

        public void OnInspectorGUI()
        {
            bool valid = true;

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Scope", GUIStyles.BoxTitleStyle);

                targetType = (TargetType)EditorGUILayout.EnumPopup("Apply To", targetType);

                // target type specific settings and info
                switch (targetType)
                {
                    case TargetType.Hierarchy:
                        EditorGUILayout.HelpBox("Apply to this gameobject and its children", MessageType.None);
                        break;
                    case TargetType.Sibling:
                        EditorGUILayout.HelpBox("Apply to all gameobjects on the same hierarchy level", MessageType.None);
                        break;
                    case TargetType.Selection:
                        EditorGUILayout.HelpBox("Apply to the selected gameobjects", MessageType.None);
                        break;
                    case TargetType.TargetChildren:
                        targetObject = EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true) as GameObject;
                        EditorGUILayout.HelpBox("Apply to the gameobject and its children", MessageType.None);
                        valid = targetObject != null;
                        break;
                    default:
                        break;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Texture Filter", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    // right-aligned
                    //GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Remove Noise", EditorStyles.miniButton))
                    {
                        List<Stamp> targets = GetTargetStamps();

                        RemoveNoise(targets);

                    }

                    if (GUILayout.Button("Disable Filter", EditorStyles.miniButton))
                    {
                        List<Stamp> targets = GetTargetStamps();

                        DisableTextureFilter(targets);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();

            if (!EditorWindow.HasOpenInstances<TerrainLayerPicker>())
            {
                EditorGUILayout.BeginVertical("box");
                {
                    if (GUILayout.Button("Open Terrain Layer Picker", GUILayout.Height(30)))
                    {
                        TerrainLayerPicker.ShowWindow();
                    }
                }
                EditorGUILayout.EndVertical();

                return;
            }

            EditorGUILayout.BeginVertical();
            {
                EditorGUI.BeginDisabledGroup(!valid);
                {

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GetPicker(out TerrainLayerPicker picker))
                        {
                            // 3 preview /  apply options should be enough
                            if (picker?.GetTerrainLayers()?.Length > 0)
                            {
                                DrawLayerPreview(picker, 1);
                                DrawLayerPreview(picker, 2);
                                DrawLayerPreview(picker, 3);
                            }
                        }

                        // filler to the right
                        EditorGUILayout.BeginVertical("box");
                        {
                            Rect rect = GUILayoutUtility.GetRect(TextureSize, TextureSize + 24 + EditorGUIUtility.singleLineHeight);
                        }

                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.EndDisabledGroup();

            }
            EditorGUILayout.EndVertical();
        }

        private void DrawLayerPreview(TerrainLayerPicker picker, int max)
        {
            if (picker?.GetTerrainLayers()?.Length < max)
                return;

            EditorGUILayout.BeginVertical("box", GUILayout.Width(128));
            {
                EditorGUILayout.BeginHorizontal();
                {
                    for (int i = 0; i < max; i++)
                    {
                        DrawLayerCell(picker, i, max);
                    }
                }
                EditorGUILayout.EndHorizontal();


                if (GUILayout.Button("Apply"))
                {
                    List<Stamp> targets = GetTargetStamps();

                    ApplyTextureFilter(targets, max);
                }
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Get all related stamps and the source stamp
        /// </summary>
        /// <returns></returns>
        private List<Stamp> GetTargetStamps()
        {
            // get source
            Stamp source = GetStamp();

            // get related
            List<Stamp> targets = GetTargetStamps(source);

            // add source
            targets.Add(source);

            return targets;
        }

        /// <summary>
        /// Get the first stamp of the gameobject
        /// </summary>
        /// <returns></returns>
        private Stamp GetStamp()
        {
            Stamp[] stamps = editor.GetEditorTarget().gameObject.GetComponents<Stamp>();

            if (stamps.Length == 0)
                return null;

            return stamps[0];
        }

        private List<Stamp> GetTargetStamps(Stamp source)
        {
            switch (targetType)
            {
                case TargetType.Hierarchy:
                    return StampUtils.GetHierarchy(editor.GetEditorTarget()?.gameObject?.transform);
                case TargetType.Sibling:
                    return StampUtils.GetSiblings(source);
                case TargetType.Selection:
                    return StampUtils.GetSelectedStamps(source);
                case TargetType.TargetChildren:
                    return StampUtils.GetTargetChildren(source, targetObject?.transform);
                default:
                    return new List<Stamp>();
            }
        }

        private void DisableTextureFilter(List<Stamp> stamps)
        {
            StampUtils.RegisterUndo(stamps, "Disable texture filter");

            FilterUtils.DisableTextureFilter(stamps);

        }

        private void RemoveNoise(List<Stamp> stamps)
        {
            StampUtils.RegisterUndo(stamps, "Reset Noise");

            FilterUtils.RemoveNoise(stamps);

        }

        private void ApplyTextureFilter(List<Stamp> stamps, int maxLayers)
        {
            TerrainLayerPicker picker = EditorWindow.GetWindow<TerrainLayerPicker>();

            if (picker == null)
                return;

            TerrainLayer[] layers = picker.GetTerrainLayers();
            float[] weights = picker.GetWeights();

            if (layers == null)
                return;

            StampUtils.RegisterUndo(stamps, "Apply texture filter");

            FilterUtils.ApplyTextureFilter(stamps, layers, weights, maxLayers);

        }


        private const float CellWidth = 32;
        private const float TextureSize = 32;

        private bool GetPicker(out TerrainLayerPicker picker)
        {
            picker = null;

            // only continue if the terrain layer picker is open
            // otherwise GetWindow would open it
            if (!EditorWindow.HasOpenInstances<TerrainLayerPicker>())
                return false;

            picker = EditorWindow.GetWindow<TerrainLayerPicker>(false, null, false);

            return picker != null;
        }

        private void DrawLayerCell(TerrainLayerPicker picker, int index, int maxLayers)
        {
            TerrainLayer[] layers = picker.GetTerrainLayers();
            float[] weights = picker.GetWeights();

            if (layers == null)
                return;

            if (index >= layers.Length)
                return;

            TerrainLayer layer = layers[index];
            float weight = weights[index];
            float percentage = weight * 100f;

            // override weight. all layers filled equally. user can always adapt
            weight = 1f;

            Color prevColor = GUI.backgroundColor;

            EditorGUILayout.BeginVertical("box", GUILayout.Width(CellWidth));

            // Texture preview
            Texture2D previewTex = layer != null ? layer.diffuseTexture : null;
            Rect area = GUILayoutUtility.GetRect(TextureSize, TextureSize);

            if (previewTex != null)
            {
                // top-left aligned
                Rect drawRect = new Rect(
                    area.x,
                    area.y,
                    TextureSize,
                    TextureSize
                );

                // need to use DrawPreviewTexture so that RGB and sRGB won't result in wrong colors (eg darker than the diffuse color would be)
                EditorGUI.DrawPreviewTexture(drawRect, previewTex, null, ScaleMode.ScaleToFit);
            }
            else
            {
                GUI.Box(area, "No Texture");
            }

            /*
            // layer name
            string layerName = layer != null && !string.IsNullOrEmpty(layer.name) ? layer.name : $"Layer {index}";
            EditorGUILayout.LabelField(layerName, EditorStyles.miniBoldLabel, GUILayout.Width(CellWidth));

            // percentage text
            EditorGUILayout.LabelField($"{percentage:F1}%", EditorStyles.miniLabel, GUILayout.Width(CellWidth));
            */

            // simple progress bar
            Rect rect = GUILayoutUtility.GetRect(CellWidth, 12f);
            EditorGUI.ProgressBar(rect, weight, "");

            EditorGUILayout.EndVertical();

            GUI.backgroundColor = prevColor;
        }
    }
}