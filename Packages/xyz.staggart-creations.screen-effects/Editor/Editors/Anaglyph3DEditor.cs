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
using UnityEditor.Rendering;
using UnityEditor;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(Anaglyph3D))]
    sealed class Anaglyph3DEditor : PostEffectSettingsEditor<Anaglyph3DRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter glasses;
        SerializedDataParameter focalPoint;
        SerializedDataParameter eyeSeparation;
        SerializedDataParameter monochrome;
        
        public override void OnEnable()
        {
            Initialize();
            
            compatible2D = false;

            var o = new PropertyFetcher<Anaglyph3D>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            glasses = Unpack(o.Find(x => x.glasses));
            focalPoint = Unpack(o.Find(x => x.focalPoint));
            eyeSeparation = Unpack(o.Find(x => x.eyeSeparation));
            monochrome = Unpack(o.Find(x => x.monochrome));
        }
        
        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();

            PropertyField(glasses);
            
            EditorGUILayout.Separator();
            
            PropertyField(focalPoint);
            PropertyField(eyeSeparation);
            
            EditorGUILayout.Separator();
            
            PropertyField(monochrome);
            
            UI.DrawFooter();
        }
    }
}