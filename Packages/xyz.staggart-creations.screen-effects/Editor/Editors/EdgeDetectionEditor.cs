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

using sc.screeneffects.runtime;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(EdgeDetection))]
    sealed class EdgeDetectionEditor : PostEffectSettingsEditor<EdgeDetectionRenderer>
    {
        SerializedDataParameter intensity;
        
        SerializedDataParameter mode;

        SerializedDataParameter sensitivityDepth;
        SerializedDataParameter sensitivityNormals;
        SerializedDataParameter luminanceInfluence;

        SerializedDataParameter edgeExp;
        SerializedDataParameter edgeSize;

        SerializedDataParameter edgeColor;

        SerializedDataParameter noise;
        SerializedDataParameter noiseTex3D;
        SerializedDataParameter noiseAnimationInterval;
        SerializedDataParameter noiseOffsetScale;
        SerializedDataParameter noiseOffsetStrength;
        SerializedDataParameter noiseThicknessStrength;
        SerializedDataParameter noiseClippingScale;
        SerializedDataParameter noiseClippingStrength;
        
        SerializedDataParameter distanceFade;
        SerializedDataParameter invertFadeDistance;
        SerializedDataParameter startFadeDistance;
        SerializedDataParameter endFadeDistance;
        
        public override void OnEnable()
        {
            Initialize();
            
            compatible2D = false;

            var o = new PropertyFetcher<EdgeDetection>(serializedObject);

            mode = Unpack(o.Find(x => x.mode));

            sensitivityDepth = Unpack(o.Find(x => x.depthInfluence));
            sensitivityNormals = Unpack(o.Find(x => x.normalInfluence));
            luminanceInfluence = Unpack(o.Find(x => x.colorInfluence));

            edgeExp = Unpack(o.Find(x => x.edgeExp));
            edgeSize = Unpack(o.Find(x => x.edgeSize));

            edgeColor = Unpack(o.Find(x => x.edgeColor));
            intensity = Unpack(o.Find(x => x.intensity));

            noise = Unpack(o.Find(x => x.noise));
            noiseTex3D = Unpack(o.Find(x => x.noiseTex3D));
            noiseAnimationInterval = Unpack(o.Find(x => x.noiseAnimationInterval));
            noiseOffsetScale = Unpack(o.Find(x => x.noiseOffsetScale));
            noiseOffsetStrength = Unpack(o.Find(x => x.noiseOffsetStrength));
            noiseThicknessStrength = Unpack(o.Find(x => x.noiseThicknessStrength));
            noiseClippingScale = Unpack(o.Find(x => x.noiseClippingScale));
            noiseClippingStrength = Unpack(o.Find(x => x.noiseClippingStrength));
            
            invertFadeDistance = Unpack(o.Find(x => x.invertFadeDistance));
            distanceFade = Unpack(o.Find(x => x.distanceFade));
            startFadeDistance = Unpack(o.Find(x => x.startFadeDistance));
            endFadeDistance = Unpack(o.Find(x => x.endFadeDistance));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();

            DrawDebugSlider(ref EdgeDetection.debug);

            PropertyField(mode);

            if (mode.overrideState.boolValue)
            {
                EditorGUILayout.BeginHorizontal();
                switch (mode.value.intValue)
                {
                    case (int)EdgeDetection.Solver.CrossAnalysis:
                        EditorGUILayout.HelpBox("Checks neighboring pixels in a + pattern for differences between geometry normals and their distance from the camera (fastest)", MessageType.None);
                        break;
                    case (int)EdgeDetection.Solver.Gradient:
                        EditorGUILayout.HelpBox("Analyses pixels in a 3x3 grid and calculates the edge strength, resulting in gradients (more accurate)", MessageType.None);
                        break;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Influences", EditorStyles.boldLabel);
            PropertyField(sensitivityDepth);
            if (mode.value.intValue == (int)EdgeDetection.Solver.CrossAnalysis)
            {
                PropertyField(sensitivityNormals);
            }
            else if (mode.value.intValue == (int)EdgeDetection.Solver.Gradient)
            {
                PropertyField(edgeExp);
            }
            
            PropertyField(luminanceInfluence);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Shading", EditorStyles.boldLabel);
            PropertyField(edgeColor);
            PropertyField(edgeSize);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("3D Noise", EditorStyles.boldLabel);
            PropertyField(noise, new GUIContent("Enabled", noise.value.tooltip));

            if (noise.value.boolValue)
            {
                EditorGUILayout.Separator();
                
                PropertyField(noiseTex3D, new GUIContent("3D Texture", noiseTex3D.value.tooltip));
                if (noiseTex3D.value.objectReferenceValue)
                {
                    PropertyField(noiseAnimationInterval, new GUIContent("Animation Interval (Seconds)", noiseAnimationInterval.value.tooltip));
                    
                    EditorGUILayout.Separator();
                    
                    EditorGUILayout.LabelField("Offset", EditorStyles.boldLabel);
                    PropertyField(noiseOffsetScale, new GUIContent("Scale", noiseOffsetScale.value.tooltip));
                    PropertyField(noiseOffsetStrength, new GUIContent("Strength", noiseOffsetStrength.value.tooltip));
                    PropertyField(noiseThicknessStrength, new GUIContent("Added Thickness", noiseThicknessStrength.value.tooltip));
                    
                    EditorGUILayout.Separator();
                    
                    EditorGUILayout.LabelField("Clipping", EditorStyles.boldLabel);
                    PropertyField(noiseClippingScale, new GUIContent("Scale", noiseClippingStrength.value.tooltip));
                    PropertyField(noiseClippingStrength, new GUIContent("Clipping", noiseClippingStrength.value.tooltip));
                }
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Distance Fading", EditorStyles.boldLabel);

            PropertyField(distanceFade, new GUIContent("Enabled", distanceFade.value.tooltip));
            if (distanceFade.value.boolValue)
            {
                PropertyField(invertFadeDistance);
                PropertyField(startFadeDistance);
                PropertyField(endFadeDistance);
            }

            UI.DrawFooter();
        }
    }
}
