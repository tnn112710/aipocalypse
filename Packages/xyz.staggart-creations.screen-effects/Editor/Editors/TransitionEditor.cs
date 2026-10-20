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
    [CustomEditor(typeof(Transition))]
    sealed class TransitionEditor : PostEffectSettingsEditor<TransitionRenderer>
    {
        SerializedDataParameter intensity;
        SerializedDataParameter gradientTex;
        SerializedDataParameter color;
        SerializedDataParameter progress;
        SerializedDataParameter softness;
        SerializedDataParameter invert;

        private List<Texture2D> presetTextures;
        
        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<Transition>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            gradientTex = Unpack(o.Find(x => x.gradientTex));
            color = Unpack(o.Find(x => x.color));
            progress = Unpack(o.Find(x => x.progress));
            softness = Unpack(o.Find(x => x.softness));
            invert = Unpack(o.Find(x => x.invert));

            //Load all the preset textures in the /Textures folder
            LoadTexturesFromFolder("862a48dc5207b2e49a359019d9953e6e", ref presetTextures);
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();

            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            DrawTextureFieldWithDropDown(gradientTex, presetTextures);
            
            DisplayTextureOverrideWarning(gradientTex.overrideState.boolValue);
            if (gradientTex.overrideState.boolValue && gradientTex.value.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox("Assign a gradient texture", MessageType.Info);
            }
            PropertyField(color);
            
            EditorGUILayout.Separator();
            PropertyField(progress);
            PropertyField(softness);

            EditorGUILayout.Separator();
            
            PropertyField(invert);
            
            UI.DrawFooter();
        }
    }
}