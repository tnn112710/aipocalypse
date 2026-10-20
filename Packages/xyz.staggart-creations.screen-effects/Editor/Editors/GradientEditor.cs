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
    [CustomEditor(typeof(Gradient))]
    sealed class GradientEditor : PostEffectSettingsEditor<GradientRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter input;
        SerializedDataParameter color1;
        SerializedDataParameter color2;
        SerializedDataParameter rotation;
        SerializedDataParameter gradientTex;
        SerializedDataParameter mode;
        
        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Gradient>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            input = Unpack(o.Find(x => x.input));
            color1 = Unpack(o.Find(x => x.color1));
            color2 = Unpack(o.Find(x => x.color2));
            rotation = Unpack(o.Find(x => x.rotation));
            gradientTex = Unpack(o.Find(x => x.gradientTex));
            mode = Unpack(o.Find(x => x.mode));
        }


        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            PropertyField(input);

            //If Radial
            if (input.value.intValue == 1)
            {
                PropertyField(gradientTex);
                DisplayTextureOverrideWarning(gradientTex.overrideState.boolValue);
            }
            else
            {
                PropertyField(color1);
                PropertyField(color2);
            }

            PropertyField(mode);
            PropertyField(rotation);
        }
    }
}