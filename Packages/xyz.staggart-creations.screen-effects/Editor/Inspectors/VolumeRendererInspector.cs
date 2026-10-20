// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using System.Collections.Generic;
using sc.screeneffects.runtime;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(PostProcessingVolumeRenderer))]
    public partial class VolumeRendererInspector : Editor
    {
        private PostProcessingVolumeRenderer component;
        private SerializedProperty volumeProp;
        private SerializedProperty settingsProp;
        
        [NoAutoStaticsCleanup]
        private static List<UniversalRenderPipelineAsset> unsupportedRenderers;

        [NoAutoStaticsCleanup]
        private static GUIContent refreshBtnContent;
        private static GUIContent RefreshBtnContent
        {
            get
            {
                if (refreshBtnContent == null)
                {
                    Texture icon = EditorGUIUtility.IconContent("d_TreeEditor.Refresh")?.image ?? EditorGUIUtility.IconContent("Refresh")?.image;
                    refreshBtnContent = new GUIContent(" Refresh Effects", icon, "Re-fetches and initializes the effect features from the assigned Volume profile.");
                }
                return refreshBtnContent;
            }
        }

        private Camera mainCamera;
        private bool alwaysEnabled;

        private void OnEnable()
        {
            component = (PostProcessingVolumeRenderer)target;
            volumeProp = serializedObject.FindProperty("volume");
            settingsProp = serializedObject.FindProperty("settings");

            unsupportedRenderers = Utilities.GetPipelineAssetsWithRenderingLayersDisabled();
            mainCamera = Camera.main;
        }

        public override void OnInspectorGUI()
        {
            UI.DrawHeader();

            if (mainCamera != null)
            {
                UniversalAdditionalCameraData cameraData = mainCamera.GetUniversalAdditionalCameraData();

                if (!alwaysEnabled)
                {
                    if (cameraData.renderPostProcessing == false)
                    {
                        UI.DrawActionBox("Main camera has post processing disabled, effects will not render", "Enable",
                            MessageType.Error,
                            () =>
                            {
                                cameraData.renderPostProcessing = true;
                                EditorUtility.SetDirty(cameraData);
                            });
                    }
                }
                
                UI.DrawNotification((cameraData.volumeLayerMask.value & (1 << component.gameObject.layer)) == 0, $"The main camera's Volume Mask does not include the {LayerMask.LayerToName(component.gameObject.layer)} layer this object is on." +
                    $"\n\n" +
                    $"Effects will most likely not render", "Include",() =>
                    {
                        cameraData.volumeLayerMask |= (1 << component.gameObject.layer);
                        
                        EditorUtility.SetDirty(cameraData);
                    }, MessageType.Warning);
                
            }
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(volumeProp);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                component.GetEffectsFromVolume();
            }

            if (component.volume == null)
            {
                EditorGUILayout.HelpBox("A Volume component must be assigned.", MessageType.Warning);

                Volume attachedVolume = component.GetComponent<Volume>();
                if (attachedVolume != null)
                {
                    if (GUILayout.Button("Assign Attached Volume"))
                    {
                        volumeProp.objectReferenceValue = attachedVolume;
                        serializedObject.ApplyModifiedProperties();
                        component.GetEffectsFromVolume();
                    }
                }
            }
            else if (component.sharedProfile == null)
            {
                EditorGUILayout.HelpBox("The assigned Volume has no Profile assigned.", MessageType.Info);
            }

            if (volumeProp.objectReferenceValue != null)
            {
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(settingsProp, true);
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }

                if (component.settings.renderingLayerMask > 0)
                {
                    unsupportedRenderers = Utilities.GetPipelineAssetsWithRenderingLayersDisabled();
                    if (unsupportedRenderers.Count > 0)
                    {
                        UI.DrawRenderingLayerSetupError();
                    }
                }

                EditorGUILayout.Space();

                DrawEffectsList();
            }

            serializedObject.ApplyModifiedProperties();

            UI.DrawFooter();
        }

        private void DrawEffectsList()
        {
            int effectCount = component.Effects != null ? component.Effects.Count : 0;

            EditorGUILayout.LabelField($"Active Effects ({effectCount})", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (component.volume == null)
                {
                    EditorGUILayout.LabelField("No volume assigned", EditorStyles.miniLabel);
                }
                else if (component.sharedProfile == null)
                {
                    EditorGUILayout.LabelField("No profile assigned to volume", EditorStyles.miniLabel);
                }
                else if (effectCount == 0)
                {
                    EditorGUILayout.LabelField("No SC Screen Effects components found on volume profile", EditorStyles.miniLabel);
                }
                else
                {
                    foreach (PostEffectFeature effect in component.Effects)
                    {
                        if (effect == null) continue;

                        using (new EditorGUI.DisabledGroupScope(!effect.isActive))
                        {
                            EditorGUILayout.LabelField(new GUIContent(effect.GetType().Name, UI.Styles.VolumeRendererIcon), EditorStyles.miniLabel);
                        }
                    }
                }
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(RefreshBtnContent, GUILayout.Width(130f)))
                {
                    component.GetEffectsFromVolume();
                }
            }
        }

        [MenuItem("CONTEXT/Volume/Add Renderer")]
        private static void AddRenderer(MenuCommand cmd)
        {
            Volume volume = (Volume)cmd.context;

            volume.gameObject.AddComponent<PostProcessingVolumeRenderer>();

            EditorUtility.SetDirty(volume);
        }
    }
}
