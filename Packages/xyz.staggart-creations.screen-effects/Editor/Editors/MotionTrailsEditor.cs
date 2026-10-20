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
    [CustomEditor(typeof(MotionTrails))]
    sealed class MotionTrailsEditor : PostEffectSettingsEditor<MotionTrailsRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter blendMode;
        
        SerializedDataParameter intervalMode;
        SerializedDataParameter captureInterval;
        
        SerializedDataParameter luminanceThreshold;
        SerializedDataParameter clamped;
        SerializedDataParameter length;
        
        SerializedDataParameter constantDirection;
        SerializedDataParameter useMotionVectors;
        SerializedDataParameter motionVectorsIntensity;
        
        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<MotionTrails>(serializedObject);
            
            intensity = Unpack(o.Find(x => x.intensity));
            blendMode = Unpack(o.Find(x => x.blendMode));
            
            intervalMode = Unpack(o.Find(x => x.intervalMode));
            captureInterval = Unpack(o.Find(x => x.captureInterval));
            
            luminanceThreshold = Unpack(o.Find(x => x.luminanceThreshold));
            clamped = Unpack(o.Find(x => x.clamped));
            length = Unpack(o.Find(x => x.length));
            
            constantDirection = Unpack(o.Find(x => x.constantDirection));
            useMotionVectors = Unpack(o.Find(x => x.useMotionVectors));
            motionVectorsIntensity = Unpack(o.Find(x => x.motionVectorsIntensity));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);

            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            PropertyField(blendMode);
            PropertyField(luminanceThreshold);
            PropertyField(clamped);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Accumulation", EditorStyles.boldLabel);
            PropertyField(intervalMode);
            if (intervalMode.value.intValue == (int)MotionTrails.Interval.Seconds)
            {
                EditorGUI.indentLevel++;
                PropertyField(captureInterval);
                EditorGUI.indentLevel--;
            }
            PropertyField(length);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Motion contribution", EditorStyles.boldLabel);
            PropertyField(constantDirection);
            PropertyField(useMotionVectors);
            if (useMotionVectors.value.boolValue)
            {
                EditorGUI.indentLevel++;
                PropertyField(motionVectorsIntensity, new GUIContent("Intensity", motionVectorsIntensity.value.tooltip));
                EditorGUI.indentLevel--;
            }
        }
    }
}