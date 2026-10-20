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

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(LightStreaks))]
    sealed class LightStreaksEditor : PostEffectSettingsEditor<LightStreaksRenderer>
    {
        SerializedDataParameter debug;
        
        SerializedDataParameter intensity;
        SerializedDataParameter luminanceThreshold;
        SerializedDataParameter brightness;
        SerializedDataParameter length;
        SerializedDataParameter angle;
        
        SerializedDataParameter sampleDistance;
        SerializedDataParameter downscaling;

        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<LightStreaks>(serializedObject);

            debug = Unpack(o.Find(x => x.debug));
            
            intensity = Unpack(o.Find(x => x.intensity));
            luminanceThreshold = Unpack(o.Find(x => x.luminanceThreshold));
            brightness = Unpack(o.Find(x => x.brightness));
            length = Unpack(o.Find(x => x.length));
            angle = Unpack(o.Find(x => x.angle));
            
            sampleDistance = Unpack(o.Find(x => x.sampleDistance));
            downscaling = Unpack(o.Find(x => x.downscaling));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            PropertyField(debug);
            
            PropertyField(luminanceThreshold);
            PropertyField(brightness);
            PropertyField(length);
            PropertyField(angle);
            
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("Quality", EditorStyles.boldLabel);
            PropertyField(sampleDistance);
            PropertyField(downscaling);
        }
    }
}
