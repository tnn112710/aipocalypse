using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    public class ValidationController
    {
        private class ScanData
        {
            public enum Status
            {
                Ok,
                Warning,
                Error
            }

            public PresetItem presetItem;

            public Status status = Status.Ok;
            public string statusText = Status.Ok.ToString();

            public string prefabName = "";
            public ScanData(PresetItem presetItem) 
            { 
                this.presetItem = presetItem;
            }
            
        }

        private BrowserToolsModule editor;

        private List<ScanData> scanDataList = new();

        private Vector2 scrollPosition = Vector2.zero;


        private string scanPackageName = "";
        private string scanContentTypeName = "";

        private bool showErrorOnly = false;

        public ValidationController(BrowserToolsModule editor)
        {
            this.editor = editor;

        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox("Scan presets for pre-defined validation inconsistencies (eg missing prefabs)", MessageType.None);
            }
            EditorGUILayout.EndVertical();

            ContentBrowser browser = ContentBrowserManager.GetContentBrowser();

            // browser not found, show message
            if (browser == null)
            {
                EditorGUILayout.HelpBox("Please open the Content Browser", MessageType.Error);
            }
            // browser found
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                {
                    EditorGUILayout.TextField("Package", $"{browser.GetSelectedPackage()?.packName}");
                    EditorGUILayout.TextField("Presets", $"{browser.GetPresets()?.Count}");
                    EditorGUILayout.TextField("Content Type", $"{browser.GetSelectedPackage()?.contentType}");
                }
                EditorGUI.EndDisabledGroup();

                showErrorOnly = EditorGUILayout.Toggle("Show Error Only", showErrorOnly);

                if (GUILayout.Button("Scan"))
                {
                    PerformScan(browser);
                }
            }

            EditorGUILayout.BeginVertical("box");
            {
                string scanResultText = string.IsNullOrWhiteSpace(scanContentTypeName) ? "Press Scan Button" : $"Scan Result for {scanPackageName} ({scanContentTypeName})";
                EditorGUILayout.HelpBox(scanResultText, MessageType.None);

                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                {
                    foreach (ScanData scanData in scanDataList)
                    {
                        if (showErrorOnly && scanData.status == ScanData.Status.Ok)
                            continue;

                        EditorGUILayout.BeginHorizontal();
                        {
                            EditorGUILayout.LabelField(scanData.prefabName);
                            EditorGUILayout.LabelField(scanData.statusText);
                        }
                        EditorGUILayout.EndHorizontal();

                    }
                }
                EditorGUILayout.EndScrollView();

            }
            EditorGUILayout.EndVertical();
        }

        private void PerformScan(ContentBrowser browser)
        {
            scanPackageName = browser.GetSelectedPackage()?.packName;
            scanContentTypeName = browser.GetSelectedPackage()?.contentType.ToString();

            scanDataList.Clear();
            
            List<PresetItem> presets = browser.GetPresets();

            for (int i = 0; i < presets.Count; i++)
            {
                PresetItem preset = presets[i];

                ScanData scanData = new ScanData(preset);

                // height stamps don't have prefabs, we check the type explicitly here in order to not perform null-checks
                if (preset.collection.contentType == ContentType.Height)
                {
                    scanData.prefabName = preset.content.stamp;

                    CheckHeightStamp(scanData);
                }
                else
                {
                    scanData.prefabName = preset.content.prefab == null ? "" : preset.content.prefab.name;

                    CheckTextureStamps(scanData);
                    CheckTreeStamps(scanData);
                    CheckDetailStamps(scanData);

                    // If the stamp is okay, check if the falloff filter is set to range
                    // Must be invoked after the stamp was checked
                    CheckFalloff(scanData);

                    CheckTransform(scanData);
                }

                scanDataList.Add(scanData);
            }
        }

        private void CheckHeightStamp(ScanData scanData)
        {
            PresetItem preset = scanData.presetItem;

            // preset is stamp
            Texture2D heightmap = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(new GUID(preset.content.stamp)));
            if (heightmap == null)
            {
                scanData.status = ScanData.Status.Error;
                scanData.statusText = "Missing Layer";
            }
            else
            {
                // update the name, otherwise it would be the guid
                scanData.prefabName = heightmap.name;
            }
        }


        private void CheckTextureStamps(ScanData scanData)
        {
            PresetItem preset = scanData.presetItem;

            GameObject go = preset.content.prefab;
            if (go == null)
                return;

            TextureStamp[] stamps = go.GetComponentsInChildren<TextureStamp>();
            foreach (TextureStamp stamp in stamps)
            {
                if( stamp.layer == null)
                {
                    scanData.status = ScanData.Status.Error;
                    scanData.statusText = $"Missing Texture Stamp: {stamp.transform.name}";
                }
            }
        }

        /// <summary>
        /// If the stamp is okay, check if the falloff filter is set to range
        /// </summary>
        /// <param name="scanData"></param>
        private void CheckFalloff(ScanData scanData)
        {
            PresetItem preset = scanData.presetItem;

            GameObject go = preset.content.prefab;
            if (go == null)
                return;

            if( scanData.status == ScanData.Status.Ok)
            {
                FalloffOverride fo = go.transform.GetComponent<FalloffOverride>();
                if ( fo != null)
                {
                    if( fo.filter.filterType != FalloffFilter.FilterType.Range)
                    {
                        scanData.status = ScanData.Status.Warning;
                        scanData.statusText = $"Falloff Filter != Range";
                    }

                    if (fo.filter.filterType == FalloffFilter.FilterType.Range)
                    {
                        if (go.transform.localScale != editor.GetEditorTarget().defaultScale)
                        {
                            scanData.status = ScanData.Status.Warning;
                            scanData.statusText = $"Scale {go.transform.localScale} != default scale {editor.GetEditorTarget().defaultScale}";
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Check if the transform of the children is reset
        /// </summary>
        /// <param name="scanData"></param>
        private void CheckTransform(ScanData scanData)
        {
            PresetItem preset = scanData.presetItem;

            GameObject go = preset.content.prefab;
            if (go == null)
                return;

            if (scanData.status == ScanData.Status.Ok)
            {
                Transform[] children = go.GetComponentsInChildren<Transform>();

                foreach (Transform child in children)
                {
                    // skip self
                    if (child == go.transform)
                        continue;

                    bool isTransformReset = child.position == Vector3.zero && child.rotation == Quaternion.identity && child.localScale == Vector3.one;
                    if (!isTransformReset)
                    {
                        scanData.status = ScanData.Status.Warning;
                        scanData.statusText = $"Transform not reset: {child.name} or a parent";
                    }
                }
            }
        }

        private void CheckTreeStamps(ScanData scanData)
        {
#if __MICROVERSE_VEGETATION__
            PresetItem preset = scanData.presetItem;

            GameObject go = preset.content.prefab;

            TreeStamp[] stamps = go.GetComponentsInChildren<TreeStamp>();
            foreach (TreeStamp stamp in stamps)
            {
                foreach (TreePrototypeSerializable pt in stamp.prototypes)
                {
                    GameObject prefab = pt.prefab;
                    ScanData.Status status = CheckPrefab(scanData, prefab);

                    if (status == ScanData.Status.Error)
                    {
                        string path = CreatePath(go.transform, stamp.transform);
                        Debug.Log($"Tree stamp error found on prefab: {path}");
                    }
                }
            }
#endif
        }

        private void CheckDetailStamps(ScanData scanData)
        {
#if __MICROVERSE_VEGETATION__
            PresetItem preset = scanData.presetItem;

            GameObject go = preset.content.prefab;

            DetailStamp[] stamps = go.GetComponentsInChildren<DetailStamp>();
            foreach (DetailStamp stamp in stamps)
            {
                GameObject prototype = stamp.prototype.prototype;

                ScanData.Status status = ScanData.Status.Ok;

                if (stamp.prototype.prototype == null && stamp.prototype.prototypeTexture == null)
                {
                    // Debug.Log("missing prototype: " + preset.collection.contentType + ", " + preset.collection.packName + ", " + preset.content.prefab + ", " + preset.content.stamp);
                    scanData.status = ScanData.Status.Error;
                    scanData.statusText = $"Missing Prototype";

                    status = ScanData.Status.Error;
                }
                else if (stamp.prototype.usePrototypeMesh)
                {
                    status = CheckPrefab(scanData, prototype?.gameObject);
                }
                else
                {
                    if (stamp.prototype.prototypeTexture == null)
                    {
                        scanData.status = ScanData.Status.Error;
                        scanData.statusText = $"Missing Texture";

                        status = ScanData.Status.Error;
                    }
                }

                if(status == ScanData.Status.Error)
                {
                    string path = CreatePath(go.transform, stamp.transform);
                    Debug.Log($"Detail stamp error found on prefab: {path}");
                }
            }
#endif
        }

        private ScanData.Status CheckPrefab(ScanData scanData, GameObject prefab)
        {
            if (prefab == null)
            {
                scanData.status = ScanData.Status.Error;
                scanData.statusText = $"Missing Prefab";

                return ScanData.Status.Error;
            }
            else
            {
                PrefabAssetType status = PrefabUtility.GetPrefabAssetType(prefab);
                switch (status)
                {
                    case PrefabAssetType.Regular:
                    case PrefabAssetType.Variant:
                        return ScanData.Status.Ok;

                    case PrefabAssetType.NotAPrefab:
                    case PrefabAssetType.Model:
                    case PrefabAssetType.MissingAsset:
                        scanData.status = ScanData.Status.Error;
                        scanData.statusText = $"Not a Prefab";

                        return ScanData.Status.Error;

                    default:
                        return ScanData.Status.Ok;
                }
            }
        }

        /// <summary>
        /// Create a path from root to child
        /// </summary>
        /// <param name="root"></param>
        /// <param name="child"></param>
        /// <returns></returns>
        private static string CreatePath(Transform root, Transform child)
        {
            return CreatePath(root, child, child.name);
        }

        /// <summary>
        /// Create a path for the full hierarchy up to the root, ie until there is no parent anymore
        /// </summary>
        /// <param name="child"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        private static string CreatePath(Transform root, Transform child, string path)
        {
            if (root == child)
                return path;

            if (child == null)
                return path;

            if (child.parent == null)
                return path;

            path = child.parent.name + "/" + path;

            return CreatePath( root, child.parent, path);
        }
    }
}