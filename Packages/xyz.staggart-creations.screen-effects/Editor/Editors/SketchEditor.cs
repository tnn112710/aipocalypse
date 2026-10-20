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
    [CustomEditor(typeof(Sketch))]
    sealed class SketchEditor : PostEffectSettingsEditor<SketchRenderer>
    {
        SerializedDataParameter intensity;
        
        SerializedDataParameter strokeTex;
        SerializedDataParameter projectionMode;
        SerializedDataParameter blendMode;
        SerializedDataParameter darkThreshold;
        SerializedDataParameter lightThreshold;
        SerializedDataParameter contrast;
        SerializedDataParameter tiling;
        SerializedDataParameter animationInterval;
        
        float minBrightness;
        float maxBrightness;
        
        private List<Texture2D> presetTextures;

        public override void OnEnable()
        {
            Initialize();
            
            compatible2D = false;

            var o = new PropertyFetcher<Sketch>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            strokeTex = Unpack(o.Find(x => x.strokeTex));
            projectionMode = Unpack(o.Find(x => x.projectionMode));
            blendMode = Unpack(o.Find(x => x.blendMode));
            darkThreshold = Unpack(o.Find(x => x.darkThreshold));
            lightThreshold = Unpack(o.Find(x => x.lightThreshold));
            contrast = Unpack(o.Find(x => x.contrast));
            tiling = Unpack(o.Find(x => x.tiling));
            animationInterval = Unpack(o.Find(x => x.animationInterval));
            
            //Load all the preset textures in the /Textures folder
            LoadTexturesFromFolder("7ce17854d15b52f4795ba82be506df0f", ref presetTextures);
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            DrawTextureFieldWithDropDown(strokeTex, presetTextures);
            
            EditorGUILayout.Separator();
            PropertyField(projectionMode);
            PropertyField(blendMode);
            
            EditorGUILayout.Separator();
            PropertyField(darkThreshold);
            PropertyField(lightThreshold);
            PropertyField(contrast);

            EditorGUILayout.Separator();

            PropertyField(tiling);
            PropertyField(animationInterval);
        }
    }
}