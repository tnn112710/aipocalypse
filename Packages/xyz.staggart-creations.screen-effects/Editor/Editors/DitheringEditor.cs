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

using System.Collections.Generic;
using sc.screeneffects.runtime;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(Dithering))]
    sealed class DitheringEditor : PostEffectSettingsEditor<DitheringRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter lut;
        SerializedDataParameter textureProjection;
        SerializedDataParameter tiling;
        SerializedDataParameter luminanceThreshold;
        private SerializedDataParameter contrast;

        private List<Texture2D> presetTextures;

        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Dithering>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            lut = Unpack(o.Find(x => x.lut));
            textureProjection = Unpack(o.Find(x => x.textureProjection));
            tiling = Unpack(o.Find(x => x.tiling));
            luminanceThreshold = Unpack(o.Find(x => x.luminanceThreshold));
            contrast = Unpack(o.Find(x => x.contrast));
            
            //Load all the preset textures in the /Textures folder
            LoadTexturesFromFolder("c69bbfae1db15b54a833a347a4dc2d59", ref presetTextures);
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            DrawTextureFieldWithDropDown(lut, presetTextures);
            PropertyField(tiling);
            PropertyField(textureProjection);

            EditorGUILayout.Space();

            PropertyField(luminanceThreshold);
            PropertyField(contrast);
        }
    }
}
