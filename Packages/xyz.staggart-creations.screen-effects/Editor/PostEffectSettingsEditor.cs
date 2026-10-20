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

using System.Collections.Generic;
using System.Reflection;
using sc.screeneffects.runtime;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.editor
{
    public class PostEffectSettingsEditor<T> : VolumeComponentEditor
    {
        SerializedDataParameter intensity;
        
        private bool hasRenderFeature;
        private bool usingVolumeRenderer;
        
        protected bool is2DRenderer;
        protected bool compatible2D = true;
        
        protected void Initialize()
        {
            base.OnEnable();
            
            var o = new PropertyFetcher<Condition>(serializedObject);
            intensity = Unpack(o.Find(x => x.intensity));
            
            hasRenderFeature = AutoSetup.IsRenderFeatureAdded<T>();
            usingVolumeRenderer = false;
            
            Volume selectedVolume = GetSelectedVolumeGameObject();

            if (selectedVolume)
            {
                PostProcessingVolumeRenderer renderer = selectedVolume.gameObject.GetComponent<PostProcessingVolumeRenderer>();
                usingVolumeRenderer = renderer;
            }

            is2DRenderer = Utilities.Using2DRenderer();
        }

        private Volume GetSelectedVolumeGameObject()
        {
            if (Selection.activeGameObject)
            {
                Volume selectedVolume = Selection.activeGameObject.GetComponent<Volume>();
                
                return selectedVolume;
            }

            return null;
        }

        internal new void DrawHeader()
        {
            UI.DrawComponentHeader<T>(hasRenderFeature);
            
            DisplaySetupWarning(ref hasRenderFeature, ref usingVolumeRenderer, compatible2D);

            if (hasRenderFeature && usingVolumeRenderer)
            {
                EditorGUILayout.HelpBox(
                    "This effect is already setup/rendering using a Render Feature. The attached Post Processing Volume Renderer will NOT use this effect.",
                    MessageType.Info);
            }
        }
        
        public void DisplaySetupWarning(ref bool renderFeatureState, ref bool rendererComponentState, bool compatible2D = true)
        {
            DisplayRenderGraphWarning();
            
            if (is2DRenderer && !compatible2D)
            {
                UI.DrawNotification("This effect has limited or no practical purpose for 2D rendering", MessageType.Error);
            }

            Volume selectedVolume = GetSelectedVolumeGameObject();

            if (selectedVolume && !renderFeatureState && !rendererComponentState)
            {
                EditorGUILayout.HelpBox("Effect has not been set up for rendering yet.", MessageType.Warning);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(new GUIContent("Add Render Feature", UI.Styles.RenderFeatureIcon, "Adds a render feature to the current default renderer." +
                            "\n\n" +
                            "The render feature will expose effect-specific quality/performance scaling settings."), GUILayout.MaxHeight(27f)))
                    {
                        AutoSetup.SetupEffect<T>();
                        renderFeatureState = true;
                    }
                    
                    if (GUILayout.Button( new GUIContent("Add Renderer Component", UI.Styles.VolumeRendererIcon, "Adds a Volume Renderer component, which directly renders the effects on this volume." +
                            "\n\n" +
                            "As opposed to a Render Feature this is simpler, but doesn't expose all rendering configurations"), GUILayout.MaxHeight(27f)))
                    {
                        selectedVolume.gameObject.AddComponent<PostProcessingVolumeRenderer>();
                        EditorUtility.SetDirty(selectedVolume.gameObject);
                        rendererComponentState = true;
                    }
                }

            }
        }

        public void DrawIntensityParameter(SerializedDataParameter parameter)
        {
            PropertyField(parameter);

            bool isZero = false;

            SerializedProperty prop = parameter.value;
            
            if (prop.type == "float") isZero = prop.floatValue == 0f;
            else if (prop.type == "int") isZero = prop.intValue == 0;
            
            if (!parameter.overrideState.boolValue)
            {
                EditorGUILayout.HelpBox("Main parameter hasn't been overriden. Effect may not be visible, unless overriden on another volume!", MessageType.Info);
            }
            else
            {
                if (isZero)
                {
                    EditorGUILayout.HelpBox("Main parameter is overridden with a 0 value. Effect will not be visible!", MessageType.Warning);
                }
            }
        }
        
        public void DrawDebugSlider(ref float debug)
        {
            EditorGUI.BeginChangeCheck();
            
            EditorGUI.indentLevel++;
            debug = EditorGUILayout.Slider("Debug", debug, 0f, 1f);
            EditorGUI.indentLevel--;
            
            if(EditorGUI.EndChangeCheck())
            {
                VolumeManager.instance.OnVolumeComponentChanged(target as VolumeComponent);
            }
        }

        public void LoadTexturesFromFolder(string folderGUID, ref List<Texture2D> presetTextures)
        {
            string texturesFolderPath = AssetDatabase.GUIDToAssetPath(folderGUID);

            if (texturesFolderPath != string.Empty)
            {
                presetTextures = new List<Texture2D>();
                
                string[] contentGUIDS = AssetDatabase.FindAssets("t:Texture2D", new[] { texturesFolderPath });
                foreach (var guid in contentGUIDS)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

                    presetTextures.Add(tex);
                }
            }
        }

        public void DrawTextureFieldWithDropDown(SerializedDataParameter textureProperty, List<Texture2D> presetTextures)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                PropertyField(textureProperty);

                //GUILayout.Space(-2);

                if (GUILayout.Button(new GUIContent("▼", "Select a preset texture"), GUILayout.Height(20f), GUILayout.Width(27)))
                {
                    GenericMenu menu = new GenericMenu();

                    for (int i = 0; i < presetTextures.Count; i++)
                    {
                        Texture2D tex = presetTextures[i];
                        menu.AddItem(new GUIContent(presetTextures[i].name, presetTextures[i]), textureProperty.value.objectReferenceValue && textureProperty.value.objectReferenceValue == tex, () =>
                        {
                            textureProperty.overrideState.boolValue = true;
                            textureProperty.value.objectReferenceValue = tex;
                            serializedObject.ApplyModifiedProperties();
                        });
                    }

                    menu.ShowAsContext();
                }
            }
            
            DisplayTextureOverrideWarning(textureProperty.overrideState.boolValue);
        }
        
        public static void DisplayTextureOverrideWarning(bool isOverridden)
        {
            if (!isOverridden)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Texture parameters always need to be overridden. It will otherwise default to a black texture in a build.", MessageType.Error);
            }
        }
        
        public static void DisplayRenderGraphWarning()
        {
            if (AutoSetup.RenderGraphEnabled() == false)
            {
                EditorGUILayout.HelpBox("Render Graph requires to be enabled.", MessageType.Error);
                
                GUILayout.Space(-32);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(new GUIContent("Enable", EditorGUIUtility.IconContent("d_tab_next").image), GUILayout.Width(60)))
                    {
                        AutoSetup.SetRenderGraphCompatibilityMode(false);

                        EditorUtility.DisplayDialog($"{AssetInfo.ASSET_NAME} v{AssetInfo.INSTALLED_VERSION}", "Please note that this option will be removed in Unity 6.4", "OK");
                    }
                    GUILayout.Space(8);
                }
                GUILayout.Space(11);
            }
        }
        
        public static void DrawSunInfo()
        {
            if (!RenderSettings.sun) EditorGUILayout.HelpBox("No directional light is active and enabled", MessageType.Warning);
            else EditorGUILayout.HelpBox(string.Format("Using the \"{0}\" object as the light source (brightest)", RenderSettings.sun.name), MessageType.None);
        }
    }
}