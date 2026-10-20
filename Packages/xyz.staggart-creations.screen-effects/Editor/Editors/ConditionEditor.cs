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
    [CustomEditor(typeof(Condition))]
    sealed class ConditionEditor : PostEffectSettingsEditor<ConditionRenderer>
    {
        SerializedDataParameter intensity;
        
        SerializedDataParameter overlayTex;
        SerializedDataParameter color;
        SerializedDataParameter vignetting;
        
        SerializedDataParameter textureAnimation1;
        SerializedDataParameter dualLayer;
        SerializedDataParameter textureAnimation2;
        
        SerializedDataParameter animationInterval;
        SerializedDataParameter falloff;
        SerializedDataParameter hardFalloff;
        SerializedDataParameter circular;
        
        private List<Texture2D> presetTextures;
        
        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Condition>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            overlayTex = Unpack(o.Find(x => x.overlayTex));
            color = Unpack(o.Find(x => x.color));
            vignetting = Unpack(o.Find(x => x.vignetting));
            
            textureAnimation1 = Unpack(o.Find(x => x.textureAnimation1));
            dualLayer = Unpack(o.Find(x => x.dualLayer));
            textureAnimation2 = Unpack(o.Find(x => x.textureAnimation2));
            
            animationInterval = Unpack(o.Find(x => x.animationInterval));
            falloff = Unpack(o.Find(x => x.falloff));
            hardFalloff = Unpack(o.Find(x => x.hardFalloff));
            circular = Unpack(o.Find(x => x.circular));
            
            LoadTexturesFromFolder("5c657a05d4527664193d2ca6e56a2507", ref presetTextures);
        }
        
        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            DrawTextureFieldWithDropDown(overlayTex, presetTextures);

            if (overlayTex.overrideState.boolValue && overlayTex.value.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a texture", MessageType.Info);
            }
            PropertyField(circular);
            
            PropertyField(color);
            PropertyField(vignetting);
            
            EditorGUILayout.Space();
            
            PropertyField(textureAnimation1);
            PropertyField(dualLayer);
            if(dualLayer.value.boolValue) PropertyField(textureAnimation2);

            EditorGUILayout.Space();
            
            PropertyField(animationInterval);
            PropertyField(falloff);
            EditorGUI.indentLevel++;
            PropertyField(hardFalloff);
            EditorGUI.indentLevel--;
        }
    }
}
