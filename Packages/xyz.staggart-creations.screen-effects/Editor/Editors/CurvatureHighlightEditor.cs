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
    [CustomEditor(typeof(CurvatureHighlight))]
    sealed class CurvatureHighlightEditor : PostEffectSettingsEditor<CurvatureHighlightRenderer>
    {
        SerializedDataParameter intensity;
        
        SerializedDataParameter threshold;
        SerializedDataParameter radius;
        SerializedDataParameter softness;
        
        SerializedDataParameter convexity;
        SerializedDataParameter concavity;
        
        public override void OnEnable()
        {
            Initialize();

            compatible2D = false;

            var o = new PropertyFetcher<CurvatureHighlight>(serializedObject);

            intensity = Unpack(o.Find(x => x.intensity));
            
            threshold = Unpack(o.Find(x => x.threshold));
            radius = Unpack(o.Find(x => x.radius));
            softness = Unpack(o.Find(x => x.softness));
            
            convexity = Unpack(o.Find(x => x.convexity));
            concavity = Unpack(o.Find(x => x.concavity));
        }

        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            DrawIntensityParameter(intensity);
            
            EditorGUILayout.Space();
            
            PropertyField(threshold);
            PropertyField(radius);
            PropertyField(softness);
            
            EditorGUILayout.Space();
            
            PropertyField(convexity, new GUIContent("Highlights (Convexity)"));
            PropertyField(concavity, new GUIContent("Crevices (Concavity)"));
        }
    }
}