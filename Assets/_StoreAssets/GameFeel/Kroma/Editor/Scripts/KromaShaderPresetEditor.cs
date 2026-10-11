using UnityEngine;
using UnityEditor;

namespace FayLabs.Kroma.Editor
{
    [CustomEditor(typeof(KromaShaderPreset))]
    public class KromaShaderPresetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            KromaShaderPreset preset = (KromaShaderPreset)target;

            // Draw header
            EditorGUILayout.LabelField(preset.sectionName + " Preset", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            Undo.RecordObject(target, "Modify Preset");

            foreach (var prop in preset.properties)
            {
                // Clean Name: "_TopColor" -> "Top Color"
                string displayName = ObjectNames.NicifyVariableName(prop.name.Replace("_", ""));
                
                // Override for known ugly ones if needed, otherwise Nicify is usually decent
                
                EditorGUILayout.BeginHorizontal();
                
                // Label
                EditorGUILayout.LabelField(displayName, GUILayout.Width(EditorGUIUtility.labelWidth));
                
                // Field
                switch (prop.type)
                {
                    case ShaderPropertyData.Type.Color:
                        prop.colorValue = EditorGUILayout.ColorField(GUIContent.none, prop.colorValue, true, true, prop.isHDR);
                        break;
                        
                    case ShaderPropertyData.Type.Texture:
                        prop.textureValue = (Texture)EditorGUILayout.ObjectField(prop.textureValue, typeof(Texture), false);
                        break;
                        
                    case ShaderPropertyData.Type.Vector:
                         prop.vectorValue = EditorGUILayout.Vector4Field(GUIContent.none, prop.vectorValue);
                        break;
                        
                    case ShaderPropertyData.Type.Float:
                        if (prop.isRange)
                        {
                            prop.floatValue = EditorGUILayout.Slider(prop.floatValue, prop.rangeMin, prop.rangeMax);
                        }
                        else
                        {
                            prop.floatValue = EditorGUILayout.FloatField(prop.floatValue);
                        }
                        break;
                }
                
                EditorGUILayout.EndHorizontal();
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(target);
            }
        }
    }
}
