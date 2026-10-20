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
    [CustomEditor(typeof(HueShift3D))]
    sealed class HueShift3DEditor : PostEffectSettingsEditor<HueShift3DRenderer>
    {
        SerializedDataParameter colorSource;
        SerializedDataParameter gradientTex;
        SerializedDataParameter intensity;
        SerializedDataParameter speed;
        SerializedDataParameter size;
        SerializedDataParameter geoInfluence;

        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<HueShift3D>(serializedObject);

            colorSource = Unpack(o.Find(x => x.colorSource));
            gradientTex = Unpack(o.Find(x => x.gradientTex));
            intensity = Unpack(o.Find(x => x.intensity));
            speed = Unpack(o.Find(x => x.speed));
            size = Unpack(o.Find(x => x.size));
            geoInfluence = Unpack(o.Find(x => x.geoInfluence));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            PropertyField(colorSource);
            if (colorSource.value.intValue == (int)HueShift3D.ColorSource.GradientTexture)
            {
                PropertyField(gradientTex);
                DisplayTextureOverrideWarning(gradientTex.overrideState.boolValue);
            }
            PropertyField(speed);
            PropertyField(size);

            PropertyField(geoInfluence);
        }
    }
}