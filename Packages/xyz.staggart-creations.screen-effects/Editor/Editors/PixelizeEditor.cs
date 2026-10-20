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
    [CustomEditor(typeof(Pixelize))]
    sealed class PixelizeEditor : PostEffectSettingsEditor<PixelizeRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter resolutionPreset;
        SerializedDataParameter resolution;
        SerializedDataParameter centerPixel;
        SerializedDataParameter preserveAspectRatio;
        
        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Pixelize>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            resolutionPreset = Unpack(o.Find(x => x.resolutionPreset));
            resolution = Unpack(o.Find(x => x.resolution));
            centerPixel = Unpack(o.Find(x => x.centerPixel));
            preserveAspectRatio = Unpack(o.Find(x => x.preserveAspectRatio));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            PropertyField(resolutionPreset);
            if (resolutionPreset.value.intValue == (int)Pixelize.ResolutionPreset.Custom)
            {
                EditorGUI.indentLevel++;
                PropertyField(resolution);
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();

            PropertyField(preserveAspectRatio);
            PropertyField(centerPixel);
            
            EditorGUILayout.Space();
        }
    }
}