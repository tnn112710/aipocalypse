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
using UnityEngine;

namespace sc.screeneffects.editor
{
    [CustomEditor(typeof(Colorize))]
    sealed class ColorizeEditor : PostEffectSettingsEditor<ColorizeRenderer>
    {
        Colorize settings;
        
        SerializedDataParameter intensity;
        SerializedDataParameter source;
        SerializedDataParameter gradient;
        SerializedDataParameter gradientTexture;
        SerializedDataParameter range;
        SerializedDataParameter blendMode;
        
        public override void OnEnable()
        {
            Initialize();

            settings = target as Colorize;
            
            var o = new PropertyFetcher<Colorize>(serializedObject);
            intensity = Unpack(o.Find(x => x.intensity));
            source = Unpack(o.Find(x => x.source));
            gradient = Unpack(o.Find(x => x.gradient));
            gradientTexture = Unpack(o.Find(x => x.gradientTexture));
            range = Unpack(o.Find(x => x.range));
            blendMode = Unpack(o.Find(x => x.blendMode));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            PropertyField(source);

            if (source.value.enumValueIndex == (int)Colorize.Source.Gradient)
            {
                EditorGUI.BeginChangeCheck();
                PropertyField(gradient);
                
                if(EditorGUI.EndChangeCheck()) settings.RebuildGradient();
            }
            else
            {
                PropertyField(gradientTexture);
            }
            
            EditorGUILayout.Separator();
            
            float minBrightness = range.value.vector2Value.x;
            float maxBrightness = range.value.vector2Value.y;
            
            using (new EditorGUILayout.HorizontalScope())
            {
                // Override checkbox
                var overrideRect = GUILayoutUtility.GetRect(17f, 17f, GUILayout.ExpandWidth(false));
                overrideRect.yMin += 4f;
                overrideRect.xMin += 8f;
                range.overrideState.boolValue = GUI.Toggle(overrideRect, range.overrideState.boolValue, EditorGUIUtility.TrTextContent("", "Override this setting for this volume."), CoreEditorStyles.smallTickbox);

                // Property
                using (new EditorGUI.DisabledScope(!range.overrideState.boolValue))
                {
                    EditorGUI.BeginChangeCheck();
                    
                    EditorGUILayout.LabelField(range.displayName, GUILayout.MaxWidth(EditorGUIUtility.labelWidth));
                    EditorGUILayout.FloatField(minBrightness, GUILayout.Width(50f));
                    EditorGUILayout.MinMaxSlider(ref minBrightness, ref maxBrightness, 0f, 1f);
                    EditorGUILayout.FloatField(maxBrightness, GUILayout.Width(50f));

                    if (EditorGUI.EndChangeCheck())
                    {
                        range.value.vector2Value = new Vector2(minBrightness, maxBrightness);
                    }
                }
            }
            PropertyField(blendMode);
        }
    }
}
