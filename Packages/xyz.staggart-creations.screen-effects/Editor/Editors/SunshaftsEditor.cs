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
using sc.screeneffects.runtime;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(Sunshafts))]
    sealed class SunshaftsEditor : PostEffectSettingsEditor<SunshaftsRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter maxStrength;
        
        SerializedDataParameter sourceMode;
        SerializedDataParameter skyLuminanceThreshold;
        
        SerializedDataParameter casterIntensityInfluence;
        SerializedDataParameter casterColorInfluence;
        SerializedDataParameter temperature;

        SerializedDataParameter resolution;
        SerializedDataParameter sunThreshold;
        SerializedDataParameter sunColor;

        SerializedDataParameter length;
        SerializedDataParameter falloff;
        SerializedDataParameter highQuality;

        public override void OnEnable()
        {
            Initialize();
            
            compatible2D = false;
            
            var o = new PropertyFetcher<Sunshafts>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            maxStrength = Unpack(o.Find(x => x.maxStrength));
            
            sourceMode = Unpack(o.Find(x => x.sourceMode));
            skyLuminanceThreshold = Unpack(o.Find(x => x.skyLuminanceThreshold));
            
            casterIntensityInfluence = Unpack(o.Find(x => x.casterIntensityInfluence));
            casterColorInfluence = Unpack(o.Find(x => x.casterColorInfluence));
            temperature = Unpack(o.Find(x => x.temperatureShift));

            resolution = Unpack(o.Find(x => x.resolution));
            sunThreshold = Unpack(o.Find(x => x.sunThreshold));
            sunColor = Unpack(o.Find(x => x.sunColor));

            length = Unpack(o.Find(x => x.length));
            falloff = Unpack(o.Find(x => x.falloff));
            highQuality = Unpack(o.Find(x => x.highQuality));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawSunInfo();
            
            DrawIntensityParameter(intensity);
            PropertyField(maxStrength);
            
            PropertyField(sourceMode);
            if (sourceMode.value.intValue == (int)Sunshafts.SourceMode.SkyLuminance)
            {
                PropertyField(skyLuminanceThreshold);
            }
            
            EditorGUILayout.Space();
            
            PropertyField(sunColor);
            PropertyField(temperature, new GUIContent("Temperature Shift"));
            
            EditorGUILayout.LabelField("Quality");
            PropertyField(resolution);
            PropertyField(highQuality, new GUIContent("High quality"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Light influence");
            PropertyField(casterColorInfluence, new GUIContent("Color"));
            PropertyField(casterIntensityInfluence, new GUIContent("Intensity"));

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Sunshafts");
            //PropertyField(sunThreshold);
            PropertyField(length);
            PropertyField(falloff);
        }
    }
}