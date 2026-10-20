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

using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEditor.AnimatedValues;
using sc.screeneffects.runtime;
using UnityEditor;
using UnityEngine;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(Fog))]
    sealed class FogEditor : PostEffectSettingsEditor<FogRenderer>
    {
        private Fog settings;
        
        SerializedDataParameter intensity;
        
        SerializedDataParameter fogMode;
        SerializedDataParameter fogStartDistance;
        SerializedDataParameter fogEndDistance;

        SerializedDataParameter colorMode;
        SerializedDataParameter skyboxBlurring;

        SerializedDataParameter fogColor;
        SerializedDataParameter fogColorGradient;
        SerializedDataParameter gradientDistance;
        SerializedDataParameter gradientUseFarClipPlane;

        SerializedDataParameter distanceFog;
        SerializedDataParameter distanceDensity;
        SerializedDataParameter useRadialDistance;

        SerializedDataParameter horizonMinMax;
        SerializedDataParameter skyboxInfluence;

        SerializedDataParameter enableDirectionalLight;
        SerializedDataParameter useLightDirection;
        SerializedDataParameter sunIntensityInfluence;
        SerializedDataParameter sunColorInfluence;
        SerializedDataParameter sunLightColor;
        SerializedDataParameter sunTemperatureShift;
        
        SerializedDataParameter lightDirection;
        SerializedDataParameter sunLightIntensity;
        SerializedDataParameter lightExponent;

        SerializedDataParameter heightFog;
        SerializedDataParameter height;
        SerializedDataParameter invertHeightFog;
        SerializedDataParameter heightDensity;

        SerializedDataParameter heightFogNoise;
        SerializedDataParameter heightNoiseTex3D;
        SerializedDataParameter heightNoiseSize;
        SerializedDataParameter heightNoiseStrength;
        SerializedDataParameter heightNoiseHorizontalSpeed;
        SerializedDataParameter heightNoiseVerticalSpeed;
        
        private float animSpeed = 4f;
        AnimBool m_showHeight;
        AnimBool m_showSun;
        AnimBool m_showScattering;
        
        public override void OnEnable()
        {
            Initialize();
            
            compatible2D = false;

            var o = new PropertyFetcher<Fog>(serializedObject);
            
            settings = (Fog)target;
            
            fogMode = Unpack(o.Find(x => x.fogMode));
            intensity = Unpack(o.Find(x => x.intensity));
            fogStartDistance = Unpack(o.Find(x => x.startDistance));
            intensity = Unpack(o.Find(x => x.intensity));
            fogEndDistance = Unpack(o.Find(x => x.endDistance));
            colorMode = Unpack(o.Find(x => x.colorSource));
            skyboxBlurring = Unpack(o.Find(x => x.skyboxBlurring));

            fogColor = Unpack(o.Find(x => x.fogColor));
            fogColorGradient = Unpack(o.Find(x => x.fogColorGradient));
            gradientDistance = Unpack(o.Find(x => x.gradientDistance));
            gradientUseFarClipPlane = Unpack(o.Find(x => x.gradientUseFarClipPlane));
            distanceFog = Unpack(o.Find(x => x.distanceFog));
            distanceDensity = Unpack(o.Find(x => x.distanceDensity));
            useRadialDistance = Unpack(o.Find(x => x.useRadialDistance));
            heightFog = Unpack(o.Find(x => x.heightFog));
            height = Unpack(o.Find(x => x.height));
            invertHeightFog = Unpack(o.Find(x => x.invertHeightFog));
            heightDensity = Unpack(o.Find(x => x.heightDensity));
            heightFogNoise = Unpack(o.Find(x => x.heightFogNoise));
            heightNoiseTex3D = Unpack(o.Find(x => x.heightNoiseTex3D));
            heightNoiseSize = Unpack(o.Find(x => x.heightNoiseSize));
            heightNoiseStrength = Unpack(o.Find(x => x.heightNoiseStrength));
            heightNoiseHorizontalSpeed = Unpack(o.Find(x => x.heightNoiseHorizontalSpeed));
            heightNoiseVerticalSpeed = Unpack(o.Find(x => x.heightNoiseVerticalSpeed));

            horizonMinMax = Unpack(o.Find(x => x.horizonMinMax));
            skyboxInfluence = Unpack(o.Find(x => x.skyboxInfluence));

            enableDirectionalLight = Unpack(o.Find(x => x.enableDirectionalLight));
            useLightDirection = Unpack(o.Find(x => x.useLightDirection));
            sunColorInfluence = Unpack(o.Find(x => x.sunColorInfluence));
            sunIntensityInfluence = Unpack(o.Find(x => x.sunIntensityInfluence));
            sunLightColor = Unpack(o.Find(x => x.sunLightColor));
            lightDirection = Unpack(o.Find(x => x.lightDirection));
            sunLightIntensity = Unpack(o.Find(x => x.sunLightIntensity));
            sunTemperatureShift = Unpack(o.Find(x => x.sunTemperatureShift));
            lightExponent = Unpack(o.Find(x => x.lightExponent));
            
            m_showHeight = new AnimBool(true);
            m_showHeight.valueChanged.AddListener(Repaint);
            m_showHeight.speed = animSpeed;

            m_showSun = new AnimBool(true);
            m_showSun.valueChanged.AddListener(Repaint);
            m_showSun.speed = animSpeed;

            m_showScattering = new AnimBool(true);
            m_showScattering.valueChanged.AddListener(Repaint);
            m_showScattering.speed = animSpeed;
        }
        
        public override GUIContent GetDisplayTitle()
        {
            return new GUIContent("Screen-Space Fog (" + colorMode.value.enumDisplayNames[colorMode.value.intValue] + ")");
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            if (RenderSettings.fog)
            {
                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.HelpBox("Fog is currently enabled in the active scene, resulting in an overlapping fog effect", MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Disable scene fog"))
                        {
                            RenderSettings.fog = false;
                            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                        }
                        GUILayout.FlexibleSpace();
                    }


                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.Space();
            }

            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            PropertyField(colorMode);
            if (colorMode.value.intValue == (int)Fog.FogColorSource.UniformColor)
            {
                PropertyField(fogColor);
            }
            else if (colorMode.value.intValue == (int)Fog.FogColorSource.Gradient) //gradient
            {
                EditorGUI.BeginChangeCheck();
                PropertyField(fogColorGradient);
                
                if(EditorGUI.EndChangeCheck()) settings.RebuildGradient();

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUI.BeginDisabledGroup(!gradientUseFarClipPlane.value.boolValue);
                    {
                        PropertyField(gradientDistance, new GUIContent("Distance"));
                    }
                    EditorGUI.EndDisabledGroup();
                    gradientUseFarClipPlane.overrideState.boolValue = true;
                    gradientUseFarClipPlane.value.boolValue = !GUILayout.Toggle(!gradientUseFarClipPlane.value.boolValue, new GUIContent("Automatic", "Distance will be set by the camera's far clipping plane"), EditorStyles.miniButton);
                }
                EditorGUILayout.EndHorizontal();

                //EditorGUILayout.HelpBox("In scene view, the gradient will appear to look different due to the camera's dynamic far clipping value", MessageType.None);
            }
            else if (colorMode.value.intValue == (int)Fog.FogColorSource.SkyboxColor)
            {
                PropertyField(skyboxBlurring);
            }

            PropertyField(distanceFog, new GUIContent("Enabled", distanceFog.value.tooltip));
            if (colorMode.value.intValue == (int)Fog.FogColorSource.Gradient && distanceFog.value.boolValue == false && heightFog.value.boolValue)
            {
                EditorGUILayout.HelpBox("Distance fog must be enabled when using the Gradient Texture color mode. Height fog only will not be colored correctly", MessageType.Warning);
            }
            if (distanceFog.value.boolValue)
            {
                PropertyField(fogMode);
                PropertyField(fogStartDistance);
                if(fogStartDistance.value.floatValue < 0) fogStartDistance.value.floatValue = 0;
            
                if (fogMode.value.intValue == (int)FogMode.Linear)
                {
                    PropertyField(fogEndDistance);
                }
                
                EditorGUILayout.Separator();
                
                PropertyField(useRadialDistance, new GUIContent("Radial"));
                PropertyField(distanceDensity);
            }

            EditorGUILayout.LabelField("Skybox", EditorStyles.boldLabel);
            float minBrightness = horizonMinMax.value.vector2Value.x;
            float maxBrightness = horizonMinMax.value.vector2Value.y;
            
            using (new EditorGUILayout.HorizontalScope())
            {
                // Override checkbox
                var overrideRect = GUILayoutUtility.GetRect(17f, 17f, GUILayout.ExpandWidth(false));
                overrideRect.yMin += 4f;
                overrideRect.xMin += 8f;
                horizonMinMax.overrideState.boolValue = GUI.Toggle(overrideRect, horizonMinMax.overrideState.boolValue, EditorGUIUtility.TrTextContent("", "Override this setting for this volume."), CoreEditorStyles.smallTickbox);

                // Property
                using (new EditorGUI.DisabledScope(!horizonMinMax.overrideState.boolValue))
                {
                    EditorGUI.BeginChangeCheck();
                    
                    EditorGUILayout.LabelField(horizonMinMax.displayName, GUILayout.MaxWidth(EditorGUIUtility.labelWidth));
                    EditorGUILayout.FloatField(minBrightness, GUILayout.Width(50f));
                    EditorGUILayout.MinMaxSlider(ref minBrightness, ref maxBrightness, 0f, 1f);
                    EditorGUILayout.FloatField(maxBrightness, GUILayout.Width(50f));

                    if (EditorGUI.EndChangeCheck())
                    {
                        horizonMinMax.value.vector2Value = new Vector2(minBrightness, maxBrightness);
                    }
                }
            }

            //Always exclude skybox for skybox color mode
            PropertyField(skyboxInfluence, new GUIContent("Influence"));


            EditorGUILayout.LabelField("Sun Light", EditorStyles.boldLabel);
            PropertyField(enableDirectionalLight);
            
            m_showSun.target = enableDirectionalLight.value.boolValue;
            if (EditorGUILayout.BeginFadeGroup(m_showSun.faded))
            {
                DrawSunInfo();

                PropertyField(useLightDirection);
                if (!useLightDirection.value.boolValue) PropertyField(lightDirection);
                
                PropertyField(sunLightColor);
                EditorGUI.indentLevel++;
                PropertyField(sunColorInfluence);
                EditorGUI.indentLevel--;
                PropertyField(sunTemperatureShift);
                
                EditorGUILayout.Separator();
                
                PropertyField(sunLightIntensity);
                EditorGUI.indentLevel++;
                PropertyField(sunIntensityInfluence);
                EditorGUI.indentLevel--;
                
                PropertyField(lightExponent);
            }
            EditorGUILayout.EndFadeGroup();

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Height Fog", EditorStyles.boldLabel);
            
            PropertyField(heightFog, new GUIContent("Enabled", heightFog.value.tooltip));
            m_showHeight.target = heightFog.value.boolValue;
            if (EditorGUILayout.BeginFadeGroup(m_showHeight.faded))
            {
                PropertyField(height, new GUIContent("Height", height.value.tooltip));
                PropertyField(invertHeightFog, new GUIContent("Invert", invertHeightFog.value.tooltip));
                PropertyField(heightDensity, new GUIContent("Density", heightDensity.value.tooltip));

                EditorGUILayout.Separator();
                PropertyField(heightFogNoise, new GUIContent("Noise", heightFogNoise.value.tooltip));
                if (heightFogNoise.value.boolValue)
                {
                    PropertyField(heightNoiseTex3D, new GUIContent("3D Texture", heightNoiseTex3D.value.tooltip));
                    if (heightNoiseTex3D.value.objectReferenceValue)
                    {
                        PropertyField(heightNoiseSize, new GUIContent("Size", heightNoiseSize.value.tooltip));
                        PropertyField(heightNoiseStrength, new GUIContent("Strength", heightNoiseStrength.value.tooltip));
                        PropertyField(heightNoiseHorizontalSpeed, new GUIContent("Horizontal Speed", heightNoiseHorizontalSpeed.value.tooltip));
                        PropertyField(heightNoiseVerticalSpeed, new GUIContent("Vertical Speed", heightNoiseVerticalSpeed.value.tooltip));
                    }
                }
            }
            EditorGUILayout.EndFadeGroup();
            
            UI.DrawFooter();
        }
    }
}