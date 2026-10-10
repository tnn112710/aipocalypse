#if USING_STANDARD
using UnityEditor;
using UnityEngine;

namespace Rowlan.Biomes_Presets_5
{
    [CustomEditor(typeof(Skybox_Standard))]
    public class Skybox_StandardEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            Skybox_Standard editorTarget = (Skybox_Standard)target;

            EditorGUILayout.Space();

            if (GUILayout.Button("Pick Values from Current Skybox"))
            {
                // copy values from current RenderSettings
                editorTarget.skybox = RenderSettings.skybox;
                editorTarget.realtimeShadowColor = RenderSettings.subtractiveShadowColor;

                editorTarget.ambientMode = RenderSettings.ambientMode;
                editorTarget.ambientEquatorColor = RenderSettings.ambientEquatorColor;
                editorTarget.ambientSkyColor = RenderSettings.ambientSkyColor;
                editorTarget.ambientGroundColor = RenderSettings.ambientGroundColor;

                editorTarget.fogEnabled = RenderSettings.fog;
                editorTarget.fogColor = RenderSettings.fogColor;
                editorTarget.fogMode = RenderSettings.fogMode;
                editorTarget.density = RenderSettings.fogDensity;
                editorTarget.fogStartDistance = RenderSettings.fogStartDistance;
                editorTarget.fogEndDistance = RenderSettings.fogEndDistance;
                                
                EditorUtility.SetDirty(editorTarget);

                Debug.Log("Skybox values updated from current RenderSettings");
            }
        }
    }
}
#endif
