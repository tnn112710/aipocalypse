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
    [CustomEditor(typeof(TiltShift))]
    sealed class TiltShiftEditor : PostEffectSettingsEditor<TiltShiftRenderer>
    {
        SerializedDataParameter intensity;
        
        SerializedDataParameter mode;
        SerializedDataParameter areaSize;
        SerializedDataParameter areaFalloff;
        SerializedDataParameter offset;
        SerializedDataParameter angle;

        public override void OnEnable()
        {
            Initialize();

            var o = new PropertyFetcher<TiltShift>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            mode = Unpack(o.Find(x => x.mode));
            areaSize = Unpack(o.Find(x => x.areaSize));
            areaFalloff = Unpack(o.Find(x => x.areaFalloff));
            offset = Unpack(o.Find(x => x.offset));
            angle = Unpack(o.Find(x => x.angle));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
 
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawOverrideCheckbox(mode);
                using (new EditorGUI.DisabledGroupScope(mode.overrideState.boolValue == false))
                {
                    EditorGUILayout.PrefixLabel(mode.displayName);
                    mode.value.intValue = GUILayout.Toolbar(mode.value.intValue, mode.value.enumDisplayNames, GUILayout.Height(17f));
                }
            }
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Screen area", EditorStyles.boldLabel);
            PropertyField(areaSize, new GUIContent("Size"));
            PropertyField(areaFalloff, new GUIContent("Falloff"));
            if (mode.value.intValue == (int)TiltShift.TiltShiftMethod.Horizontal)
            {
                PropertyField(offset, new GUIContent("Offset"));
                PropertyField(angle, new GUIContent("Angleº"));
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(" ");
                TiltShift.debug = GUILayout.Toggle(TiltShift.debug,"Visualize area", "Button");
            }

        }
    }
}