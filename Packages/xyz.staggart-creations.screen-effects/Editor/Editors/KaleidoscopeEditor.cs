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
    [CustomEditor(typeof(Kaleidoscope))]
    sealed class KaleidoscopeEditor : PostEffectSettingsEditor<KaleidoscopeRenderer>
    {
        SerializedDataParameter intensity;
        
        SerializedDataParameter radialSplits;
        SerializedDataParameter horizontalSplits;
        SerializedDataParameter verticalSplits;
        SerializedDataParameter center;
        SerializedDataParameter maintainAspectRatio;
        SerializedDataParameter borderReflection;

        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Kaleidoscope>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            
            radialSplits = Unpack(o.Find(x => x.radialSplits));
            horizontalSplits = Unpack(o.Find(x => x.horizontalSplits));
            verticalSplits = Unpack(o.Find(x => x.verticalSplits));
            center = Unpack(o.Find(x => x.center));
            maintainAspectRatio = Unpack(o.Find(x => x.maintainAspectRatio));
            borderReflection = Unpack(o.Find(x => x.borderReflection));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawIntensityParameter(intensity);

            EditorGUILayout.Space();
            
            PropertyField(radialSplits);
            
            PropertyField(horizontalSplits);
            PropertyField(verticalSplits);

            EditorGUILayout.Space();
            
            PropertyField(center);
            PropertyField(maintainAspectRatio);
            PropertyField(borderReflection);
        }
    }
}