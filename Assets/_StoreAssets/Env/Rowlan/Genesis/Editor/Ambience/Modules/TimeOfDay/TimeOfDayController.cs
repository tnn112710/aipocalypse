using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Ambience
{
    public class TimeOfDayController
    {
        private AmbienceModule editor;

        private class Preset
        {
            public string name;
            public GUIContent icon;
            public float azimuth;
            public float altitude;

        }

        private GUIContent[] icons;
        private Preset[] presets;
        private int selectedPresetIndex = -1;

        private TimeOfDaySetup setup;

        public TimeOfDayController(AmbienceModule editor)
        {
            this.editor = editor;

            Light light = GameObjectUtils.GetDirectionalLight();

            setup = ScriptableObject.CreateInstance<TimeOfDaySetup>();

            setup.directionalLight = light;


            presets = new Preset[]
            {
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p0_p135")), azimuth = 135.0f, altitude = 0.0f },
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p10_p100")), azimuth = 100.0f, altitude = 10.0f },
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p30_m30")), azimuth = 30.0f, altitude = -30.0f },
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p35_p35")), azimuth = 35.0f, altitude = 35.0f },
                
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p0_p90")), azimuth = 0.0f, altitude = 90.0f },

                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p35_n35")), azimuth = -35.0f, altitude = 35.0f },
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_n30_n30")), azimuth = -30.0f, altitude = -30.0f },
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p10_n100")), azimuth = -100.0f, altitude = 10.0f },
                new Preset() { name="", icon = new GUIContent( ResourceUtils.LoadIcon(ProjectSetup.RESOURCE_PATH_ICONS, "Sun_p0_n135")), azimuth = -135.0f, altitude = 0.0f },
            };

            icons = presets.Select(x => x.icon).ToArray();
        }


        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Sun Position", GUIStyles.BoxTitleStyle);

                setup.directionalLight = EditorGUILayout.ObjectField("Light", setup.directionalLight, typeof(Light), true) as Light;
            }
            EditorGUILayout.EndVertical();

            bool hasLight = setup.directionalLight != null && setup.directionalLight.type == LightType.Directional;
            if (!hasLight)
            {
                EditorGUILayout.HelpBox("Directional Light required", MessageType.Error);
            }

            if (hasLight)
            {
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Presets", GUIStyles.BoxTitleStyle);

                    selectedPresetIndex = GUILayout.SelectionGrid(selectedPresetIndex, icons, 9);

                    if (selectedPresetIndex != -1)
                    {
                        Preset preset = presets[selectedPresetIndex];

                        setup.directionalLight.transform.eulerAngles = new Vector3( preset.altitude, preset.azimuth, 0f);

                        selectedPresetIndex = -1;
                    }
                }
                EditorGUILayout.EndVertical();
            }

        }

    }
}