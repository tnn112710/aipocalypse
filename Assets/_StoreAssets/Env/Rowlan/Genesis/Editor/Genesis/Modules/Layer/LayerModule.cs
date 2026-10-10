using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;
using System.Linq;

namespace Rowlan.Genesis
{
    public class LayerModule : IGenesisModule, ITemplateProcessor
    {
        private GenesisDataEditor editor;

        private SerializedProperty data;

        private TemplateProvider<LayerTemplateCollection> templateProvider;

        private Vector2 scrollPosition = Vector2.zero;

        public LayerModule(GenesisDataEditor editor)
        {
            this.editor = editor;

            data = editor.serializedObject.FindProperty("layerSettings");

            templateProvider = new TemplateProvider<LayerTemplateCollection>(this);
        }

        public void OnInspectorGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                // templates
                GUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Presets", GUIStyles.BoxTitleStyle);

                    templateProvider.DrawDropTargets();
                }
                GUILayout.EndVertical();

                // toolbar (clear etc)
                GUILayout.BeginVertical("box");
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            Undo.RegisterCompleteObjectUndo(editor.editorTarget, "Clear");

                            editor.editorTarget.layerSettings = new LayerSettings[0];

                            editor.CreateTextureStamps();
                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                // settings
                EditorGUILayout.PropertyField(data, true);
            }
            EditorGUILayout.EndScrollView();

        }
        public void Create(GameObject parent)
        {
            GameObject layers = new GameObject("Texture Stamps");

            // parent
            layers.transform.parent = parent.transform;

            // set group
            layers.AddComponent<LayerGroup>();

            LayerSettings[] layerSettingsList = editor.editorTarget.layerSettings;

            // add splat filters
            for (int i = 0; i < layerSettingsList.Length; i++)
            {
                LayerSettings settings = layerSettingsList[i];

                if (!settings.active)
                    continue;

                TerrainLayer terrainLayer = settings.terrainLayer;

                // stamp gameobject
                string name = "Stamp " + terrainLayer.name;
                GameObject stampGO = new GameObject(name);
                stampGO.transform.parent = layers.transform;

                TextureStamp stamp = stampGO.AddComponent<TextureStamp>();
                stamp.layer = terrainLayer;

                float slopeMin = 0f;
                float slopeMax = 90f;

                FilterSet filterSet = new FilterSet();

                filterSet.slopeFilter.enabled = settings.slopeEnabled;
                filterSet.slopeFilter.range.x = Mathf.Lerp( slopeMin, slopeMax, settings.slopeRange.x); // lerp [0..90], the genesis settings are [0..1]
                filterSet.slopeFilter.range.y = Mathf.Lerp(slopeMin, slopeMax, settings.slopeRange.y); // lerp [0..90], the genesis settings are [0..1]

                stamp.filterSet = filterSet;
            }
        }

        public void Destroy()
        {
            MicroVerse microVerse = MicroVerse.instance;

            if (!microVerse)
                return;

            LayerGroup group = microVerse.GetLayerGroup();

            if (!group)
                return;

            Object.DestroyImmediate(group.gameObject);
        }

        public void Process(ITemplateSettings template, Object[] objects)
        {
            foreach (Object droppedObject in objects)
            {
                // consistency check
                bool accept = droppedObject is TerrainLayer;

                if (!accept)
                {
                    Debug.LogError($"Invalid object {droppedObject} for template {template}");
                    continue;
                }

                LayerSettingsTemplate settingsTemplate = template as LayerSettingsTemplate;

                LayerSettings[] currentObjects = editor.editorTarget.layerSettings;

                // new settings
                LayerSettings newSettings = new LayerSettings();

                // apply template
                newSettings.Apply(settingsTemplate);

                // apply dropped object data
                newSettings.terrainLayer= droppedObject as TerrainLayer;

                // create new settings
                LayerSettings[] newObjects = new LayerSettings[] { newSettings };

                // merge current and new settings
                editor.editorTarget.layerSettings = currentObjects.Concat(newObjects).ToArray();


            }
        }
    }
}