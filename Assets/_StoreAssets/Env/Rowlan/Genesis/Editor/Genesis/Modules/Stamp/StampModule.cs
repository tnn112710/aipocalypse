using JBooth.MicroVerseCore;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Rowlan.Genesis.GenesisData;

namespace Rowlan.Genesis
{
    public class StampModule : IGenesisModule, ITemplateProcessor
    {
        private GenesisDataEditor editor;

        private SerializedProperty heightStamps;
        private SerializedProperty heightStampPlacement;

        private TemplateProvider<StampTemplateCollection> templateProvider;

        private Vector2 scrollPosition = Vector2.zero;

        public StampModule(GenesisDataEditor editor)
        {
            this.editor = editor;

            heightStamps = editor.serializedObject.FindProperty("heightStamps");
            heightStampPlacement = editor.serializedObject.FindProperty("heightStampPlacement");

            templateProvider = new TemplateProvider<StampTemplateCollection>( this);
        }

        public void OnInspectorGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                // module settings
                GUILayout.BeginVertical("box");
                {
                    // placement
                    EditorGUILayout.PropertyField(heightStampPlacement, new GUIContent("Placement", "Placement algorithm"));
                }
                GUILayout.EndVertical();

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

                            editor.editorTarget.heightStamps = new HeightStampSettings[0];

                            editor.CreateHeightStamps();

                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                // settings
                EditorGUILayout.PropertyField(heightStamps, true);
            }
            EditorGUILayout.EndScrollView();
        }

        public void Create(GameObject parent)
        {
            GameObject stamps = new GameObject("Height Stamps");

            // parent
            stamps.transform.parent = parent.transform;

            // set group
            stamps.AddComponent<StampGroup>();

            // algorithm
            HeightStampPlacement placement = (HeightStampPlacement) heightStampPlacement.intValue;

            switch (placement)
            {
                case HeightStampPlacement.Random:
                    PerformRandomPlacement( stamps);
                    break;

                case HeightStampPlacement.Grid:
                    PerformGridPlacement(stamps);
                    break;

                default:
                    Debug.LogError("Unsupported placement: " + placement);
                    break;
            }

        }

        private void PerformRandomPlacement( GameObject parent)
        {
            HeightStampSettings[] heightStampCollection = editor.editorTarget.heightStamps;
            TerrainSettings terrainSettings = editor.editorTarget.terrainSettings;

            // stamps
            for (int i = 0; i < heightStampCollection.Length; i++)
            {
                HeightStampSettings settings = heightStampCollection[i];

                if (!settings.active)
                    continue;

                Texture2D texture = settings.texture;

                // stamp gameobject
                string name = texture.name + " (Height Stamp)";
                GameObject stampGO = new GameObject(name);
                stampGO.transform.parent = parent.transform;

                Vector3 position;
                Quaternion rotation;
                Vector3 scale;

                HeightStamp stamp = stampGO.AddComponent<HeightStamp>();

                stamp.stamp = texture;
                stamp.mode = HeightStamp.CombineMode.Add;

                // position
                float xPositionRandom = Random.Range(settings.positionRangeX.x, settings.positionRangeX.y);
                float yPositionRandom = Random.Range(settings.positionRangeY.x, settings.positionRangeY.y);
                float zPositionRandom = Random.Range(settings.positionRangeZ.x, settings.positionRangeZ.y);

                float roationRandom = Random.Range(settings.rotationRange.x, settings.rotationRange.y);

                float xScaleRandom = Random.Range(settings.scaleRangeWidth.x, settings.scaleRangeWidth.x);
                float yScaleRandom = Random.Range(settings.scaleRangeHeight.y, settings.scaleRangeHeight.y);
                float zScaleRandom = xScaleRandom; // for now uniform, could be distorted

                if (heightStampCollection[i].falloffType == HeightStampSettings.FalloffType.Box)
                {
                    stamp.falloff = new FalloffFilter();
                    stamp.falloff.filterType = FalloffFilter.FilterType.Box;
                    //stamp.falloff.falloffRange = new Vector2(0.0f, 0.5f);
                }
                else
                {
                    stamp.falloff = new FalloffFilter();
                    stamp.falloff.filterType = FalloffFilter.FilterType.Range;
                    stamp.falloff.falloffRange = new Vector2(0.0f, 1.0f);
                }

                float positionX = terrainSettings.totalWidth * xPositionRandom;
                float positionY = terrainSettings.totalHeight * yPositionRandom;
                float positionZ = terrainSettings.totalLength * zPositionRandom;

                position = new Vector3(positionX, positionY, positionZ);

                // rotation
                rotation = Quaternion.Euler(0f, roationRandom, 0f);

                // scale
                float scaleX = terrainSettings.totalWidth * xScaleRandom;
                float scaleY = terrainSettings.totalHeight * yScaleRandom;
                float scaleZ = terrainSettings.totalLength * zScaleRandom;

                scale = new Vector3(scaleX, scaleY, scaleZ);

                // set gameobject transform
                stampGO.transform.position = position;
                stampGO.transform.rotation = rotation;
                stampGO.transform.localScale = scale;

            }
        }



        private void PerformGridPlacement(GameObject parent)
        {
            HeightStampSettings[] heightStampCollection = editor.editorTarget.heightStamps;
            TerrainSettings terrainSettings = editor.editorTarget.terrainSettings;

            HeightStampSettings[] activeStamps = heightStampCollection.Where(x => x.active == true).ToArray();

            if (activeStamps.Length == 0)
                return;

            // calculate optimal size
            float sizeX = LayoutUtils.FitSquares(terrainSettings.totalWidth, terrainSettings.totalLength, activeStamps.Length);
            float sizeZ = sizeX;
            
            int columns = (int) Mathf.Round( terrainSettings.totalWidth / sizeX);

            float heightRatio = sizeX / terrainSettings.totalWidth;

            // stamps
            for (int i = 0; i < activeStamps.Length; i++)
            {

                int gridX = i % columns;
                int gridY = i / columns;

                HeightStampSettings settings = activeStamps[i];

                Texture2D texture = settings.texture;

                // stamp gameobject
                string name = texture.name + " (Height Stamp)";
                GameObject stampGO = new GameObject(name);
                stampGO.transform.parent = parent.transform;

                // stamp settings
                HeightStamp stamp = stampGO.AddComponent<HeightStamp>();

                stamp.stamp = texture;
                stamp.mode = HeightStamp.CombineMode.Add;

                stamp.falloff = new FalloffFilter();
                stamp.falloff.filterType = FalloffFilter.FilterType.Box;

                // position
                float positionX = gridX * sizeX + sizeX / 2f;
                float positionY = 0f;
                float positionZ = gridY * sizeZ + sizeZ / 2f;

                Vector3 position = new Vector3(positionX, positionY, positionZ);

                // rotation
                Quaternion rotation = Quaternion.identity;

                // scale
                float yScaleRandom = Random.Range(settings.scaleRangeHeight.y, settings.scaleRangeHeight.y);

                float scaleX = sizeX;
                float scaleY = terrainSettings.totalHeight * yScaleRandom * heightRatio;
                float scaleZ = sizeZ;

                Vector3 scale = new Vector3(scaleX, scaleY, scaleZ);

                // set gameobject transform
                stampGO.transform.position = position;
                stampGO.transform.rotation = rotation;
                stampGO.transform.localScale = scale;

            }
        }

        public void Destroy()
        {
            JBooth.MicroVerseCore.MicroVerse microVerse = JBooth.MicroVerseCore.MicroVerse.instance;

            if (!microVerse)
                return;

            StampGroup group = microVerse.GetStampGroup();

            if (!group)
                return;

            Object.DestroyImmediate(group.gameObject);

        }

        public void Process(ITemplateSettings template, Object[] objects)
        {
            foreach (Object droppedObject in objects)
            {
                // consistency check
                bool accept = droppedObject is Texture2D;

                if (!accept)
                {
                    Debug.LogError($"Invalid object {droppedObject} for template {template}");
                    continue;
                }

                StampSettingsTemplate settingsTemplate = template as StampSettingsTemplate;

                // get current settings
                HeightStampSettings[] currentObjects = editor.editorTarget.heightStamps;

                // new settings
                HeightStampSettings newSettings = new HeightStampSettings();

                // apply template
                newSettings.Apply(settingsTemplate);

                // apply dropped object data
                newSettings.texture = droppedObject as Texture2D;

                // create new settings
                HeightStampSettings[] newObjects = new HeightStampSettings[] { newSettings };

                // merge current and new settings
                editor.editorTarget.heightStamps = currentObjects.Concat(newObjects).ToArray();

            }
        }
    }
}