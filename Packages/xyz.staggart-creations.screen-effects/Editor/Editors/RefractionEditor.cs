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
    [CustomEditor(typeof(Refraction))]
    sealed class RefractionEditor : PostEffectSettingsEditor<RefractionRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter normalMap;
        SerializedDataParameter tint;
        SerializedDataParameter textureAnimation;
        
        private List<Texture2D> presetTextures;

        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Refraction>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            normalMap = Unpack(o.Find(x => x.normalMap));
            tint = Unpack(o.Find(x => x.tint));
            textureAnimation = Unpack(o.Find(x => x.textureAnimation));
            
            //Load all the preset textures in the /Textures folder
            LoadTexturesFromFolder("eef1c9682cc7af242b0f389fc3161e4e", ref presetTextures);
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            DrawTextureFieldWithDropDown(normalMap, presetTextures);

            if (normalMap.overrideState.boolValue && normalMap.value.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a texture", MessageType.Info);
            }

            PropertyField(tint);
            
            EditorGUILayout.Separator();
            
            PropertyField(textureAnimation);
        }
    }
}