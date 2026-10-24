using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SkyboxPlus {

    public class SkyboxPlusShaderGUI : ShaderGUI {

        const string DOCS_URL = "https://kronnect.com/docs/skybox-plus/";
        const string SUPPORT_URL = "https://kronnect.com/support";
        const string PREFS_PREFIX = "SkyboxPlus.Section.";
        const float HEADER_HEIGHT = 24f;

        class Section {
            public string title;
            public string toggleProp;
            public string keyword;
            public string gateProp;
            public float gateOnValue;
            public bool titleFromToggle;
            public string inheritTexture;
            public string inheritTextureFrom;
            public string[] props;
        }

        static Section Layer (int index) {
            return new Section {
                title = "Layer " + index,
                toggleProp = "_SkyboxLayer" + index,
                keyword = "SKYBOX_LAYER_" + index,
                titleFromToggle = true,
                props = new [] { "_Tex" + index, "_AlphaLayer" + index, "_HShiftLayer" + index, "_RotationSpeed" + index, "_VShiftLayer" + index, "_PhaseLayer" + index }
            };
        }

        static readonly Section[] sections = {
            new Section { title = "Sky", props = new [] { "_SkyTint", "_SkyNightColor", "_NightTintColor", "_Exposure" } },
            new Section { title = "Sun", props = new [] { "_SunColor", "_SunSize", "_SunFlare" } },
            new Section { title = "Rainbow", gateProp = "_RainbowIntensity", gateOnValue = 1f, props = new [] { "_RainbowIntensity", "_RainbowSaturation", "_RainbowRadius", "_RainbowWidth", "_RainbowSecondary" } },
            new Section { title = "Night and Moon", toggleProp = "_SkyboxNight", keyword = "SKYBOX_NIGHT", props = new [] { "_MoonTex", "_MoonSize", "_MoonFlare", "_MoonPhase", "_MoonPhaseAngle", "_StarSize", "_StarDensity", "_StarBrightness", "_StarDarkColor", "_StarBrightColor", "_StarBlinking", "_StarClustering", "_StarBandAxis", "_StarBandWidth" } },
            new Section { title = "Second Moon", gateProp = "_Moon2Size", gateOnValue = 0.08f, inheritTexture = "_Moon2Tex", inheritTextureFrom = "_MoonTex", props = new [] { "_Moon2Tex", "_Moon2Size", "_Moon2Direction", "_Moon2Tint", "_Moon2Brightness", "_Moon2Phase", "_Moon2PhaseAngle" } },
            new Section { title = "Procedural Haze", toggleProp = "_SkyboxHaze", keyword = "SKYBOX_HAZE", props = new [] { "_NoiseTex", "_HazeColor", "_HazeScale", "_HazeThreshold", "_HazeIntensity", "_HazeRotationAxis", "_HazeRotationSpeed" } },
            Layer(1), Layer(2), Layer(3), Layer(4), Layer(5), Layer(6), Layer(7),
            new Section { title = "Meteor Showers", gateProp = "_MeteorFrequency", gateOnValue = 0.3f, props = new [] { "_MeteorFrequency", "_MeteorCount", "_MeteorDuration", "_MeteorSize", "_MeteorTrail", "_MeteorColor", "_MeteorBrightness", "_MeteorRadiant", "_MeteorSpread" } },
            new Section { title = "Aurora Borealis", gateProp = "_AuroraIntensity", gateOnValue = 1f, props = new [] { "_AuroraIntensity", "_AuroraColor1", "_AuroraColor2", "_AuroraElevation", "_AuroraSpan", "_AuroraWaviness", "_AuroraRayDensity", "_AuroraSpeed" } },
            new Section { title = "Landscape", props = new [] { "_HorizonGlow" } },
            new Section { title = "Fog", props = new [] { "_UseUnityFogColor", "_FogColor", "_FogBaseHeight", "_FogHeight", "_FogPower", "_FogMinAmount", "_FogAmount", "_FogDaylightInfluence", "_FogAlpha" } }
        };

        static readonly HashSet<string> vector3Props = new HashSet<string> { "_HazeRotationAxis", "_MeteorRadiant", "_StarBandAxis", "_Moon2Direction" };

        static GUIStyle titleStyle, headerLabelStyle, bodyStyle;
        static Color headerBackColor, headerLineColor;

        readonly List<MaterialProperty> buffer = new List<MaterialProperty>();

        public override void OnGUI (MaterialEditor materialEditor, MaterialProperty[] properties) {

            InitStyles();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Skybox Plus", titleStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Documentation", EditorStyles.miniButtonLeft, GUILayout.Width(96))) {
                Application.OpenURL(DOCS_URL);
            }
            if (GUILayout.Button("Support", EditorStyles.miniButtonRight, GUILayout.Width(64))) {
                Application.OpenURL(SUPPORT_URL);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            float prevLabelWidth = EditorGUIUtility.labelWidth;

            foreach (Section section in sections) {

                MaterialProperty toggle = section.toggleProp != null ? FindProperty(section.toggleProp, properties, false) : null;
                if (section.toggleProp != null && toggle == null) continue;
                MaterialProperty gate = section.gateProp != null ? FindProperty(section.gateProp, properties, false) : null;

                buffer.Clear();
                foreach (string name in section.props) {
                    MaterialProperty prop = FindProperty(name, properties, false);
                    if (prop != null) buffer.Add(prop);
                }
                if (buffer.Count == 0) continue;

                bool hasToggle = toggle != null || gate != null;
                bool on = toggle != null ? toggle.floatValue > 0 : (gate == null || gate.floatValue > 0);
                bool expanded = EditorPrefs.GetBool(PREFS_PREFIX + section.title, false);
                string title = section.titleFromToggle && toggle != null ? Trim(toggle.displayName) : section.title;

                string toggleTip = toggle != null ? Trim(toggle.displayName) : (gate != null ? "Enables " + title + " (sets " + Trim(gate.displayName) + " above zero)" : null);

                bool newOn;
                bool newExpanded = DrawSectionHeader(title, expanded, hasToggle, on, toggleTip, out newOn);

                if (hasToggle && newOn != on) {
                    materialEditor.RegisterPropertyChangeUndo(title);
                    if (toggle != null) {
                        toggle.floatValue = newOn ? 1 : 0;
                        SetKeyword(materialEditor, section.keyword, newOn);
                    } else {
                        gate.floatValue = newOn ? section.gateOnValue : 0;
                    }
                    if (newOn) {
                        InheritTexture(section, properties);
                        newExpanded = true;
                    }
                }

                if (newExpanded != expanded) {
                    EditorPrefs.SetBool(PREFS_PREFIX + section.title, newExpanded);
                }

                if (newExpanded) {
                    EditorGUILayout.BeginVertical(bodyStyle);
                    EditorGUIUtility.labelWidth = Mathf.Max(140f, EditorGUIUtility.currentViewWidth * 0.4f);
                    using (new EditorGUI.DisabledScope(hasToggle && !newOn)) {
                        DrawProperties(materialEditor, properties);
                    }
                    EditorGUIUtility.labelWidth = prevLabelWidth;
                    EditorGUILayout.EndVertical();
                }

                GUILayout.Space(2);
            }

            EditorGUIUtility.labelWidth = prevLabelWidth;
        }

        void DrawProperties (MaterialEditor materialEditor, MaterialProperty[] properties) {
            MaterialProperty useUnityFog = FindProperty("_UseUnityFogColor", properties, false);
            foreach (MaterialProperty prop in buffer) {
                if (prop.name == "_FogColor" && useUnityFog != null && useUnityFog.floatValue > 0) continue;
                string label = Trim(prop.displayName);
                if (prop.propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture) {
                    materialEditor.TexturePropertySingleLine(new GUIContent(label), prop);
                    if ((prop.propertyFlags & UnityEngine.Rendering.ShaderPropertyFlags.NoScaleOffset) == 0) {
                        EditorGUI.indentLevel++;
                        materialEditor.TextureScaleOffsetProperty(prop);
                        EditorGUI.indentLevel--;
                    }
                } else if (vector3Props.Contains(prop.name)) {
                    EditorGUI.showMixedValue = prop.hasMixedValue;
                    EditorGUI.BeginChangeCheck();
                    Vector4 current = prop.vectorValue;
                    Vector3 edited = EditorGUILayout.Vector3Field(label, new Vector3(current.x, current.y, current.z));
                    if (EditorGUI.EndChangeCheck()) {
                        prop.vectorValue = new Vector4(edited.x, edited.y, edited.z, current.w);
                    }
                    EditorGUI.showMixedValue = false;
                } else {
                    materialEditor.ShaderProperty(prop, label);
                }
            }
        }

        bool DrawSectionHeader (string title, bool expanded, bool hasToggle, bool on, string toggleTip, out bool newOn) {

            newOn = on;
            Rect rect = GUILayoutUtility.GetRect(1f, HEADER_HEIGHT, GUILayout.ExpandWidth(true));

            if (Event.current.type == EventType.Repaint) {
                EditorGUI.DrawRect(rect, headerBackColor);
                EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), headerLineColor);
            }

            Rect arrowRect = new Rect(rect.x + 4f, rect.y + (HEADER_HEIGHT - 16f) * 0.5f, 14f, 16f);
            Rect toggleRect = new Rect(rect.xMax - 24f, rect.y + (HEADER_HEIGHT - 16f) * 0.5f, 16f, 16f);
            Rect labelRect = new Rect(rect.x + 22f, rect.y, rect.width - 52f, HEADER_HEIGHT);

            GUI.Label(labelRect, new GUIContent(title, toggleTip), headerLabelStyle);

            if (hasToggle) {
                newOn = GUI.Toggle(toggleRect, on, new GUIContent("", toggleTip), EditorStyles.toggle);
            }

            bool newExpanded = EditorGUI.Foldout(arrowRect, expanded, GUIContent.none, false);

            Event e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition) && !toggleRect.Contains(e.mousePosition)) {
                newExpanded = !newExpanded;
                e.Use();
                GUI.changed = true;
            }

            return newExpanded;
        }

        static void InheritTexture (Section section, MaterialProperty[] properties) {
            if (section.inheritTexture == null) return;
            MaterialProperty target = FindProperty(section.inheritTexture, properties, false);
            MaterialProperty source = FindProperty(section.inheritTextureFrom, properties, false);
            if (target == null || source == null || target.textureValue != null) return;
            target.textureValue = source.textureValue;
        }

        static void SetKeyword (MaterialEditor materialEditor, string keyword, bool enabled) {
            if (string.IsNullOrEmpty(keyword)) return;
            foreach (Object target in materialEditor.targets) {
                Material mat = target as Material;
                if (mat == null) continue;
                if (enabled) {
                    mat.EnableKeyword(keyword);
                } else {
                    mat.DisableKeyword(keyword);
                }
            }
        }

        static string Trim (string label) {
            return label != null ? label.Trim() : "";
        }

        static void InitStyles () {
            bool pro = EditorGUIUtility.isProSkin;
            headerBackColor = pro ? new Color(0.245f, 0.245f, 0.245f) : new Color(0.79f, 0.79f, 0.79f);
            headerLineColor = pro ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.62f, 0.62f, 0.62f);
            Color accent = pro ? new Color(0.52f, 0.66f, 0.9f) : new Color(0.12f, 0.16f, 0.4f);
            if (titleStyle == null) {
                titleStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            }
            titleStyle.normal.textColor = accent;
            if (headerLabelStyle == null) {
                headerLabelStyle = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            }
            headerLabelStyle.normal.textColor = accent;
            if (bodyStyle == null) {
                bodyStyle = new GUIStyle { padding = new RectOffset(12, 8, 6, 8) };
            }
        }

    }

}
