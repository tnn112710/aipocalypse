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
    [CustomEditor(typeof(Overlay))]
    sealed class OverlayEditor : PostEffectSettingsEditor<OverlayRenderer>
    {
        SerializedDataParameter overlayTex;
        SerializedDataParameter textureProjection;
        SerializedDataParameter autoAspect;
        SerializedDataParameter blendMode;
        SerializedDataParameter intensity;
        SerializedDataParameter luminanceThreshold;
        SerializedDataParameter tiling;
        
        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Overlay>(serializedObject);

            overlayTex = Unpack(o.Find(x => x.overlayTex));
            textureProjection = Unpack(o.Find(x => x.textureProjection));
            autoAspect = Unpack(o.Find(x => x.autoAspect));
            blendMode = Unpack(o.Find(x => x.blendMode));
            intensity = Unpack(o.Find(x => x.intensity));
            luminanceThreshold = Unpack(o.Find(x => x.luminanceThreshold));
            tiling = Unpack(o.Find(x => x.tiling));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            PropertyField(overlayTex);
            DisplayTextureOverrideWarning(overlayTex.overrideState.boolValue);

            if (overlayTex.overrideState.boolValue && overlayTex.value.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a texture", MessageType.Info);
            }

            PropertyField(textureProjection);
            
            EditorGUILayout.Space();
            
            PropertyField(luminanceThreshold);
            PropertyField(autoAspect);
            PropertyField(blendMode);
            PropertyField(tiling);
        }
    }
}