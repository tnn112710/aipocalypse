#if KWS_HDRP || KWS_URP || KWS_BUILTIN
using UnityEditor;
using UnityEngine;

using KWS;
using JBooth.MicroVerseCore;
using System.Linq;
using static JBooth.MicroVerseCore.FalloffFilter;
using System.IO;

namespace Rowlan.Genesis.Integrations.KWS2
{
    [CustomEditor(typeof(MV_KWS_StampWizard))]
    public class MV_KWS_StampWizardEditor : Editor
    {
        private static GUIContent CreateTextureContent = new GUIContent("Create and Assign Texture", "Create mask texture and assign it to the stamps");
        private static GUIContent ResetStampsContent = new GUIContent("Reset Stamps", "Set stamp texture to black");
        private static GUIContent PreviewTitleContent = new GUIContent("Stamp Texture Preview");
        private static GUIContent AutoSetupContent = new GUIContent("Auto-Setup", "Assign stamps, set transforms based on dynamic water zone, set texture falloff");
        
        public enum MicroVerseType
        {
            ClearStamp,
            HeightStamp,
            FalloffOverride
        }

        #region Water Source Alignment

        private float raycastHeight = 100f;
        private float offsetY = 0f;
        private LayerMask groundLayer = ~0; // Default to all layers
        private bool alignToNormal = false;

        #endregion Water Source Alignment

        private MV_KWS_StampWizardEditor editor;
        private MV_KWS_StampWizard editorTarget;

        private SerializedProperty zone;
        private SerializedProperty clearStamp;
        private SerializedProperty heightStamp;
        private SerializedProperty falloffOverrides;

        private SerializedProperty heightStampSettings;
        //private SerializedProperty heightStampScaleY;

        private SerializedProperty filterSettings;

        private SerializedProperty fileName;
        private SerializedProperty appendKwsSuffix;
        private SerializedProperty overwriteFile;

        private Texture2D previewTexture = null;
        private string previewFileName = "";

        void OnEnable()
        {
            editor = this;
            editorTarget = target as MV_KWS_StampWizard;

            zone = serializedObject.FindProperty("zone");
            clearStamp = serializedObject.FindProperty("clearStamp");
            heightStamp = serializedObject.FindProperty("heightStamp");
            falloffOverrides = serializedObject.FindProperty("falloffOverrides");

            heightStampSettings = serializedObject.FindProperty("heightStampSettings");
            //heightStampScaleY = heightStampSettings.FindPropertyRelative("scaleY");

            filterSettings = serializedObject.FindProperty("filterSettings");

            fileName = serializedObject.FindProperty("fileName");
            appendKwsSuffix = serializedObject.FindProperty("appendKwsSuffix");
            overwriteFile = serializedObject.FindProperty("overwriteFile");

            // initialization
            CreatePreviewFileName();
            UpdatePreviewTexture();
        }

        public override void OnInspectorGUI()
        {
            // title
            EditorGUILayout.LabelField("KWS Stamp Wizard (Beta)", EditorStyles.boldLabel);

            // info
            EditorGUILayout.HelpBox("Create MicroVerse stamp textures based on KWS2 heightmaps.\nAbout Beta version: Features and compatibility might change in future versions.", MessageType.None);

            EditorGUILayout.HelpBox("The texture creation is an iterative process:" +
                "\n" +
                "\n1. Assign zone with cached dynamic waves simulation" +
                "\n2. Assign target stamps with texture filters (clear, height, falloff overrides)" +
                "\n3. Shape texture using filters (use inspector preview)" +
                "\n4. Assign to stamps, ie modify underlying terrain" +
                "\n5. Persist to file and assign file to stamps" +
                "\n6. Apply another dynamic waves simulation on modified terrain"
                , MessageType.None);

            bool updatePreviewFileName = false;
            bool updatePreviewTexture = false;

            serializedObject.Update();

            // base.OnInspectorGUI();

            EditorGUILayout.LabelField("KWS2", EditorStyles.boldLabel);

            using (var zoneChangeCheck = new EditorGUI.ChangeCheckScope())
            {
                GUI.backgroundColor = zone.objectReferenceValue == null ? GUIStyles.ErrorBackgroundColor : GUIStyles.DefaultBackgroundColor;
                {
                    EditorGUILayout.PropertyField(zone);
                }
                GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;

                if ( zoneChangeCheck.changed)
                {
                    updatePreviewFileName = true;
                    updatePreviewTexture = true;

                }
            }

            // show the textures as compact object field
            EditorGUI.indentLevel++;
            {
                // readonly
                using (new GUIUtils.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Saved Depth", editorTarget.zone?.SavedDepth, typeof(Object), true);
                    EditorGUILayout.ObjectField("Saved Distance Field", editorTarget.zone?.SavedDistanceField, typeof(Object), true);
                    EditorGUILayout.ObjectField("Saved Dynamic Waves Simulation", editorTarget.zone?.SavedDynamicWavesSimulation, typeof(Object), true);
                }
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.BeginHorizontal();
            {
                using (new GUIUtils.DisabledScope(editorTarget.zone == null || editorTarget.zone.IsBaking))
                {
                    if (GUILayout.Button("Start Cache"))
                    {
                        KWS_EditorDynamicWavesSimulationZones.StartBaking(editorTarget.zone);
                    }
                }

                using (new GUIUtils.DisabledScope(editorTarget.zone == null || !editorTarget.zone.IsBaking))
                {
                    if (GUILayout.Button("Stop & Save"))
                    {
                        KWS_EditorDynamicWavesSimulationZones.StopBaking(editorTarget.zone);
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            using (new GUIUtils.DisabledScope(editorTarget.zone?.SavedDynamicWavesSimulation == null))
            {

                EditorGUILayout.LabelField("MicroVerse", EditorStyles.boldLabel);

                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUILayout.PropertyField(clearStamp);

                    using (new GUIUtils.DisabledScope(editorTarget.clearStamp != null || editorTarget.zone?.SavedDynamicWavesSimulation == null))
                    {
                        if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            CreateClearStamp();
                            AutoSetup();
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                {

                    EditorGUILayout.PropertyField(heightStamp);

                    using (new GUIUtils.DisabledScope(editorTarget.heightStamp != null || editorTarget.zone?.SavedDynamicWavesSimulation == null))
                    {
                        if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            CreateHeightStamp();
                            AutoSetup();
                        }
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(heightStampSettings);

                EditorGUILayout.PropertyField(falloffOverrides);


                EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

                if (GUILayout.Button(AutoSetupContent))
                {
                    AutoSetup();
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Shaping", EditorStyles.boldLabel);

                using (var filterChangeCheck = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(filterSettings);

                    if( filterChangeCheck.changed)
                    {
                        updatePreviewTexture = true;
                    }
                }

                if (GUILayout.Button( CreateTextureContent, GUILayout.Height(30)))
                {
                    UpdateInMemoryStamps();
                    updatePreviewTexture = true;
                }

                if (GUILayout.Button( ResetStampsContent))
                {
                    ResetStamps();
                    updatePreviewTexture = true;
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Persistence", EditorStyles.boldLabel);

                // readonly
                using (new GUIUtils.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Saved Texture", editorTarget.savedTexture, typeof(Object), true);
                }

                using (var fileChangeCheck = new EditorGUI.ChangeCheckScope())
                {
                    EditorGUILayout.PropertyField(fileName);
                    EditorGUILayout.PropertyField(appendKwsSuffix);

                    if (fileChangeCheck.changed)
                    {
                        updatePreviewFileName = true;
                    }
                }

                EditorGUILayout.PropertyField(overwriteFile);

                // readonly
                using (new GUIUtils.DisabledScope(true))
                {
                    EditorGUILayout.LabelField("Preview File Path", previewFileName, EditorStyles.wordWrappedLabel);
                }

                if (GUILayout.Button("Save to File", GUILayout.Height(30)))
                {
                    // save and assign the texture
                    if (SaveToFile(out Texture2D savedTexture))
                    {
                        AssignTextureToStamps(savedTexture, true);
                    }
                }

                if (!editorTarget.isTextureSaved)
                {
                    EditorGUILayout.HelpBox("Stamp assigned texture isn't saved.", MessageType.Warning);
                }

                /*
                 * disabled for now:
                 * KWS_DynamicWavesObject got renamed to KWS_DynamicWavesSimulationEffector which causes compile errors
                 * 
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

                if (GUILayout.Button("Align Child Dynamic Waves Objects to Ground"))
                {
                    AlignDynamicWavesObjectsToGround();
                }
                */
            }

            serializedObject.ApplyModifiedProperties();

            if (updatePreviewFileName)
            {
                CreatePreviewFileName();
                EditorUtility.SetDirty(this);
            }

            if( updatePreviewTexture)
            {
                UpdatePreviewTexture();
                EditorUtility.SetDirty(this);
            }
        }

        public override bool HasPreviewGUI()
        {
            return previewTexture != null;
        }

        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            if (previewTexture == null)
            {
                // EditorGUI.LabelField(r, "No Texture Assigned");
            }
            else
            {
                GUI.DrawTexture(r, previewTexture, ScaleMode.ScaleToFit);
            }
        }

        public override GUIContent GetPreviewTitle()
        {
            return PreviewTitleContent;
        }

        private void UpdatePreviewTexture()
        {
            if (editorTarget.zone == null)
                previewTexture = null;
            else
                previewTexture = GetInMemoryStampTexture();
        }

        private void AutoSetup()
        {
            // if zone is null try to find one in the children
            if (editorTarget.zone == null)
            {
                editorTarget.zone = editorTarget.transform.GetComponentInChildren<KWS_DynamicWavesSimulationZone>();
            }

            editorTarget.clearStamp = editorTarget.transform.GetComponentInChildren<ClearStamp>();
            editorTarget.heightStamp = editorTarget.transform.GetComponentInChildren<HeightStamp>();
            editorTarget.falloffOverrides = editorTarget.transform.GetComponentsInChildren<FalloffOverride>();

            // set clear stamp transform, align to zone
            AlignToZone(editorTarget.clearStamp?.transform, MicroVerseType.ClearStamp);

            // height
            AlignToZone(editorTarget.heightStamp?.transform, MicroVerseType.HeightStamp);

            // falloff overrides, eg texture
            if (editorTarget.falloffOverrides != null)
            {
                foreach (FalloffOverride fo in editorTarget.falloffOverrides)
                {
                    if (fo.filter.filterType == FalloffFilter.FilterType.Texture)
                    {
                        AlignToZone(fo?.transform, MicroVerseType.FalloffOverride);
                    }
                    else
                    {
                        Debug.LogWarning("Falloff filter type isn't texture: " + fo.transform.name);
                    }

                    AlignToZone(fo.transform, MicroVerseType.FalloffOverride);
                }
            }

            // assign textures
            UpdateInMemoryStamps();

        }

        private void AlignToZone(Transform transform, MicroVerseType microVerseType)
        {
            if (transform == null || editorTarget.zone == null)
                return;

            Transform source = editorTarget.zone.transform;
            Transform target = transform;

            Matrix4x4 sourceWorld = source.transform.localToWorldMatrix;
            Matrix4x4 targetWorld = target.transform.localToWorldMatrix;

            Matrix4x4 transformMatrix = targetWorld * sourceWorld.inverse;
            Vector3 worldPosition = transformMatrix * editorTarget.zone.transform.position;

            target.transform.position = worldPosition;

            target.transform.rotation = editorTarget.zone.transform.rotation;
            target.transform.localScale = editorTarget.zone.transform.localScale;

            switch (microVerseType)
            {
                case MicroVerseType.ClearStamp:
                case MicroVerseType.FalloffOverride:
                    {
                        float y = target.transform.position.y - target.transform.localScale.y * 0.5f;
                        target.transform.position = new Vector3(target.transform.position.x, y, target.transform.position.z);
                    }
                    break;

                case MicroVerseType.HeightStamp:
                    {
                        target.transform.position = new Vector3(target.transform.position.x, 0f, target.transform.position.z);

                        float scaleY = editorTarget.heightStampSettings.scaleY;
                        target.transform.localScale = new Vector3(target.transform.localScale.x, scaleY, target.transform.localScale.z);
                    }
                    break;

            }
        }

        /*
        private void AlignDynamicWavesObjectsToGround()
        {
            KWS_DynamicWavesSimulationEffector[] kWS_DynamicWavesObjects = editorTarget.transform.GetComponentsInChildren<KWS_DynamicWavesSimulationEffector>();
            Transform[] transforms = kWS_DynamicWavesObjects.Select(x => x.transform).ToArray();

            RaycastUtils.AlignToGround(transforms, raycastHeight, offsetY, alignToNormal, groundLayer);

        }
        */

        private void CreateClearStamp()
        {
            GameObject go = new GameObject("Clear Stamp");

            GameObjectUtility.SetParentAndAlign(go, editorTarget.gameObject);

            // stamp
            ClearStamp stamp = go.AddComponent<ClearStamp>();

            FalloffFilter filter = stamp.filterSet.falloffFilter;

            filter.filterType = FalloffFilter.FilterType.Texture;
            filter.textureChannel = TextureChannel.R;

            // undo
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            editorTarget.clearStamp = stamp;

            // set transform to dynamic waves zone
            AlignToZone(go.transform, MicroVerseType.ClearStamp);

        }

        private void CreateHeightStamp()
        {
            GameObject go = new GameObject("Height Stamp");

            GameObjectUtility.SetParentAndAlign(go, editorTarget.gameObject);

            // stamp
            HeightStamp stamp = go.AddComponent<HeightStamp>();

            stamp.mode = HeightStamp.CombineMode.Subtract;

            // undo
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            editorTarget.heightStamp = stamp;

            // set transform to dynamic waves zone
            AlignToZone(go.transform, MicroVerseType.HeightStamp);
        }

        private void ResetStamps()
        {
            AssignTextureToStamps(Texture2D.blackTexture, true);

        }
        private void UpdateInMemoryStamps()
        {
            Texture2D r16Tex = GetInMemoryStampTexture();

            AssignTextureToStamps(r16Tex, false);
        }

        private Texture2D GetInMemoryStampTexture()
        {
            TextureFilter textureFilter = new TextureFilter();

            Texture2D r16Tex = textureFilter.GetInMemoryStampTexture(editorTarget.zone, editorTarget.filterSettings);

            return r16Tex;

        }

        private void AssignTextureToStamps(Texture2D r16Tex, bool isTextureSaved)
        {
            editorTarget.isTextureSaved = isTextureSaved;

            // clear
            if (editorTarget.clearStamp != null)
            {
                FalloffFilter filter = editorTarget.clearStamp.filterSet?.falloffFilter;

                filter.texture = r16Tex;
                filter.textureChannel = FalloffFilter.TextureChannel.R;
            }

            // height
            if (editorTarget.heightStamp != null)
            {
                editorTarget.heightStamp.stamp = r16Tex;
            }

            // falloff overrides
            if (editorTarget.falloffOverrides != null)
            {
                foreach (FalloffOverride fo in editorTarget.falloffOverrides)
                {
                    if (fo.filter.filterType == FalloffFilter.FilterType.Texture)
                    {
                        fo.filter.texture = r16Tex;
                    }
                }
            }

            Bounds bounds = new Bounds(editorTarget.zone.transform.position, editorTarget.zone.transform.lossyScale);

            // MicroVerse.instance.Invalidate(bounds);
            MicroVerse.instance.Invalidate(null);
        }

        private void CreatePreviewFileName()
        {
            if (CreateFilePath(out string path))
            {
                previewFileName = path;
            }
            else
            {
                previewFileName = "";
            }
        }

        private bool CreateFilePath( out string path)
        {
            path = null;

            if (editorTarget.zone?.SavedDynamicWavesSimulation == null)
                return false;

            // get filename of kws texture
            Texture2D kwsTexture = editorTarget.zone.SavedDynamicWavesSimulation;

            string assetPath = AssetDatabase.GetAssetPath(kwsTexture); // e.g., "Assets/Textures/myTexture.png"
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("KWS zone has no saved dynamic waves simulation.");
                return false;
            }

            // default filename
            string newFileName = editorTarget.fileName;

            // append randomness of kws filename (eg for DynamicWavesSimulation_HE04K we want Heightmap_HE04K)
            if (editorTarget.appendKwsSuffix)
            {
                string kwsFileNamePrefix = "DynamicWavesSimulation_";
                string kwsFileName = Path.GetFileNameWithoutExtension(assetPath);
                if (kwsFileName.StartsWith(kwsFileNamePrefix))
                {
                    newFileName += "_" + kwsFileName.Substring(kwsFileNamePrefix.Length);
                }
            }

            // add extension
            newFileName += ".png";

            string folderPath = Path.GetDirectoryName(assetPath);    // e.g., "Assets/Textures"
            string newPath = Path.Combine(folderPath, newFileName);  // e.g., "Assets/Textures/newTexture.png"
            newPath.Replace("\\", "/"); // Ensure compatibility across OSes

            path = newPath;

            return true;
        }

        private bool SaveToFile(out Texture2D savedTexture)
        {
            savedTexture = null;

            if (!CreateFilePath(out string filePath))
                return false;

            // overwrite file check
            if (!editorTarget.overwriteFile)
            {
                if (File.Exists(filePath))
                {
                    bool isContinue = EditorUtility.DisplayDialog("Overwrite File", $"File {filePath} exists. Continue?", "Yes", "No");

                    if (!isContinue)
                    {
                        return false;
                    }
                }
            }

            // get the processed texture
            Texture2D image = GetInMemoryStampTexture();

            byte[] pngData = image.EncodeToPNG();

            File.WriteAllBytes(filePath, pngData);

            Debug.Log($"Saved R16 PNG to: {filePath}");

            //AssetDatabase.SaveAssets();
            //AssetDatabase.Refresh();

            AssetDatabase.ImportAsset(filePath);

            TextureImporter importer = AssetImporter.GetAtPath(filePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                TextureImporterPlatformSettings platformSettings = importer.GetDefaultPlatformTextureSettings();

                platformSettings.textureCompression = TextureImporterCompression.Uncompressed;
                platformSettings.maxTextureSize = 4096;
                platformSettings.overridden = true;
                platformSettings.format = TextureImporterFormat.R16;

                importer.SetPlatformTextureSettings(platformSettings);

                importer.SaveAndReimport();
            }

            savedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(filePath);

            editorTarget.savedTexture = savedTexture;

            return true;
        }


    }
}
#endif