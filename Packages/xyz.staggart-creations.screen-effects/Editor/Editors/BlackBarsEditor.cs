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
    [CustomEditor(typeof(BlackBars))]
    sealed class BlackBarsEditor : PostEffectSettingsEditor<BlackBarsRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter mode;
        SerializedDataParameter maxSize;
        
        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<BlackBars>(serializedObject);

            mode = Unpack(o.Find(x => x.mode));
            intensity = Unpack(o.Find(x => x.intensity));
            maxSize = Unpack(o.Find(x => x.maxSize));
        }
        
        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();

            PropertyField(mode);
            PropertyField(maxSize);
        }
    }
}