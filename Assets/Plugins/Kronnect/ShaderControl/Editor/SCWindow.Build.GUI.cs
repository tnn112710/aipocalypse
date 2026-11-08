/*
Shader Control - (C) Copyright Kronnect Technologies SL
*/

using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using UnityEngine.Rendering;
using System.IO;

namespace ShaderControl {

    public enum BuildViewSortType {
        ShaderName = 0,
        ShaderKeywordCount = 1,
        ShaderVariantCount = 2,
        Keyword = 3
    }

    public partial class SCWindow : EditorWindow {

        string buildShaderNameFilter;
        StringBuilder sb = new StringBuilder();

        void DrawBuildGUI() {

            GUILayout.Box(new GUIContent("This tab shows all shaders compiled in your last build.\nHere you can exclude any number of shaders or keywords from future compilations. No file is modified, only excluded from the build.\nIf you have exceeded the maximum allowed keywords in your project, use the <b>Project View</b> tab to remove shaders or disable any unwanted keyword from the project."), titleStyle, GUILayout.ExpandWidth(true));
            EditorGUILayout.Separator();

            EditorGUILayout.BeginHorizontal();
            if (nextQuickBuild)
            {
                if (GUILayout.Button(new GUIContent("Cancel Quick Build")))
                {
                    SetEditorPrefBool(QUICK_BUILD, false);
                    SetEditorPrefBool(QUICK_BUILD_ADDRESSABLES, false);
                    ShaderDebugBuildProcessor.CreateDummyQuickBuildGO(true);
                    nextQuickBuild = false;
                }
                if (GUILayout.Button(new GUIContent("Clear", "Reset the built shaders database."), GUILayout.Width(60)))
                {
                    ClearBuildData();
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("Quick Build", "Forces a quick compilation to extract all shaders and keywords included in the build.")))
                {
                    EditorUtility.DisplayDialog("Ready to analyze!", "Now make a build as normal (select 'File -> Build Settings -> Build').\n\nShader Control will detect the shaders and keywords from the build process and list that information here.\n\nImportant Note!\nTo make this special build faster, shaders won't be compiled (they will show in pink in the build). This is normal. To create a normal build, just build the project again without clicking 'Quick Build'.", "Ok");
                    SetEditorPrefBool(QUICK_BUILD, true);
#if UNITY_2022_1_OR_NEWER
                    SetEditorPrefBool(DELAY_SCENE_UPDATE, false);
#endif
                    ShaderDebugBuildProcessor.CreateDummyQuickBuildGO(false);
                    ShaderDebugBuildProcessor.ResetQuickBuildWarning();
                    nextQuickBuild = true;
                }
                if (GUILayout.Button(new GUIContent("Clear", "Reset the built shaders database."), GUILayout.Width(60)))
                {
                    ClearBuildData();
                }
            }
            if (GUILayout.Button("Help", GUILayout.Width(40))) {
                ShowHelpWindowBuildView();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (nextQuickBuild) {
                EditorGUILayout.HelpBox("Shader Control is ready to collect data during the next build.", MessageType.Info);
                if (AddressablesInUse()) {
                    EditorGUI.BeginChangeCheck();
                    bool includeAddressables = GetEditorPrefBool(QUICK_BUILD_ADDRESSABLES, false);
                    includeAddressables = EditorGUILayout.ToggleLeft(new GUIContent("Include Addressables", "When enabled, Quick Build will also skip shader compilation during Addressables builds. Warning: shaders may appear pink in the editor while this is active."), includeAddressables);
                    if (EditorGUI.EndChangeCheck()) {
                        SetEditorPrefBool(QUICK_BUILD_ADDRESSABLES, includeAddressables);
                    }
                }
            }

            int shadersCount = shadersBuildInfo == null || shadersBuildInfo.shaders == null ? 0 : shadersBuildInfo.shaders.Count;

            if (shadersBuildInfo != null) {
                if (!nextQuickBuild) {
                    EditorGUILayout.LabelField("Last build: " + ((shadersBuildInfo.creationDateTicks != 0) ? shadersBuildInfo.creationDateString : "no data yet. Click 'Quick Build' for more details."), EditorStyles.boldLabel);
                }
                if (shadersBuildInfo.requiresBuild) {
                    EditorGUILayout.HelpBox("Project shaders have been modified. Do a 'Quick Build' again to ensure the data shown in this tab is accurate.", MessageType.Warning);
                }
            }

            if (shadersCount == 0) return;

            if (totalBuildShaders == 0 || totalBuildIncludedShaders == 0 || totalBuildKeywords == 0 || (totalBuildKeywords == totalBuildIncludedKeywords && totalBuildShaders == totalBuildIncludedShaders)) {
                EditorGUILayout.HelpBox("Total Compiled Shaders: " + totalBuildShaders + "  Shaders Using Keywords: " + totalBuildShadersWithKeywords + "\nTotal Unique Keywords: " + totalBuildKeywords, MessageType.Info);
            } else {
                int shadersPerc = totalBuildIncludedShaders * 100 / totalBuildShaders;
                int shadersWithKeywordsPerc = totalBuildIncludedShadersWithKeywords * 100 / totalBuildIncludedShaders;
                int keywordsPerc = totalBuildIncludedKeywords * 100 / totalBuildKeywords;
                EditorGUILayout.HelpBox("Total Compiled Shaders: " + totalBuildIncludedShaders + " of " + totalBuildShaders + " (" + shadersPerc + "%" + "  Shaders Using Keywords: " + totalBuildIncludedShadersWithKeywords + " of " + totalBuildShadersWithKeywords + " (" + shadersWithKeywordsPerc + "%)\nTotal Unique Keywords: " + totalBuildIncludedKeywords + " of " + totalBuildKeywords + " (" + keywordsPerc.ToString() + "%)", MessageType.Info);
            }

            EditorGUILayout.Separator();

            // Configuration System UI
            CheckConfigurationStore();
            if (buildConfiguration != null) {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Configuration", GUILayout.Width(100));
                
                var configNames = new string[buildConfiguration.configurations.Count];
                for (int i = 0; i < configNames.Length; i++) {
                    configNames[i] = buildConfiguration.configurations[i].name;
                }
                
                EditorGUI.BeginChangeCheck();
                buildConfiguration.selectedConfigurationIndex = EditorGUILayout.Popup(buildConfiguration.selectedConfigurationIndex, configNames);
                if (EditorGUI.EndChangeCheck()) {
                    EditorUtility.SetDirty(buildConfiguration);
                }
                
                if (!isRenamingConfiguration) {
                    if (GUILayout.Button("Rename", GUILayout.Width(60))) {
                        isRenamingConfiguration = true;
                        configurationRenameText = buildConfiguration.GetSelectedConfiguration()?.name ?? "";
                    }
                } else {
                    configurationRenameText = EditorGUILayout.TextField(configurationRenameText, GUILayout.Width(100));
                    if (GUILayout.Button("OK", GUILayout.Width(30))) {
                        buildConfiguration.RenameConfiguration(buildConfiguration.selectedConfigurationIndex, configurationRenameText);
                        EditorUtility.SetDirty(buildConfiguration);
                        AssetDatabase.SaveAssets();
                        isRenamingConfiguration = false;
                    }
                    if (GUILayout.Button("Cancel", GUILayout.Width(50))) {
                        isRenamingConfiguration = false;
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("", GUILayout.Width(100));
                
                if (GUILayout.Button(new GUIContent("Save", "Save current shader exclusion settings to the selected configuration."), GUILayout.Width(60))) {
                    buildConfiguration.SaveCurrentBuildSettings(shadersBuildInfo);
                    EditorUtility.SetDirty(buildConfiguration);
                    AssetDatabase.SaveAssets();
                    EditorUtility.DisplayDialog("Configuration Saved", "Current shader exclusion settings have been saved to configuration '" + buildConfiguration.GetSelectedConfiguration()?.name + "'", "OK");
                }
                
                if (GUILayout.Button(new GUIContent("Load", "Load shader exclusion settings from the selected configuration."), GUILayout.Width(60))) {
                    if (EditorUtility.DisplayDialog("Load Configuration", "This will replace current shader exclusion settings with configuration '" + buildConfiguration.GetSelectedConfiguration()?.name + "'. Continue?", "Yes", "No")) {
                        buildConfiguration.LoadToBuildSettings(shadersBuildInfo);
                        AfterConfigurationApplied(shadersBuildInfo);
                    }
                }
                
                if (GUILayout.Button(new GUIContent("Reset", "Reset the selected configuration by removing all shader and keyword exclusions."), GUILayout.Width(60))) {
                    if (EditorUtility.DisplayDialog("Reset Configuration", "This will reset the current configuration '" + buildConfiguration.GetSelectedConfiguration()?.name + "' and remove all exclusions. Continue?", "Yes", "No")) {
                        buildConfiguration.ResetCurrentConfiguration();
                        buildConfiguration.LoadToBuildSettings(shadersBuildInfo);
                        AfterConfigurationApplied(shadersBuildInfo);
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Separator();
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sort By", GUILayout.Width(100));
            EditorGUI.BeginChangeCheck();
            shadersBuildInfo.sortType = (BuildViewSortType)EditorGUILayout.EnumPopup(shadersBuildInfo.sortType);
            if (EditorGUI.EndChangeCheck()) {
                if (shadersBuildInfo != null) {
                    shadersBuildInfo.Resort();
                }
                EditorUtility.SetDirty(shadersBuildInfo);
                GUIUtility.ExitGUI();
                return;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Shader Name", GUILayout.Width(100));
            buildShaderNameFilter = EditorGUILayout.TextField(buildShaderNameFilter);
            if (GUILayout.Button(new GUIContent("Clear", "Clear filter."), EditorStyles.miniButton, GUILayout.Width(60))) {
                buildShaderNameFilter = "";
                GUIUtility.keyboardControl = 0;
            }
            EditorGUILayout.EndHorizontal();

            if (shadersBuildInfo.sortType != BuildViewSortType.Keyword) {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Keywords >=", GUILayout.Width(100));
                minimumKeywordCount = EditorGUILayout.IntSlider(minimumKeywordCount, 0, maxBuildKeywordsCountFound);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Keyword Filter", GUILayout.Width(100));
            keywordFilter = EditorGUILayout.TextField(keywordFilter);
            if (GUILayout.Button(new GUIContent("Clear", "Clear filter."), EditorStyles.miniButton, GUILayout.Width(60))) {
                keywordFilter = "";
                GUIUtility.keyboardControl = 0;
            }
            EditorGUILayout.EndHorizontal();

            if (shadersBuildInfo.sortType != BuildViewSortType.Keyword) {
                shadersBuildInfo.hideReadOnlyShaders = EditorGUILayout.ToggleLeft("Hide read-only/internal shaders", shadersBuildInfo.hideReadOnlyShaders);
            }

            EditorGUILayout.Separator();

            scrollViewPosProject = EditorGUILayout.BeginScrollView(scrollViewPosProject);

            bool requireUpdate = false;
            bool needsTitle = true;

            if (shadersBuildInfo.sortType == BuildViewSortType.Keyword) {
                if (buildKeywordView != null) {
                    int kvCount = buildKeywordView.Count;
                    for (int s = 0; s < kvCount; s++) {
                        BuildKeywordView kwv = buildKeywordView[s];
                        string keyword = kwv.keyword;
                        if (!string.IsNullOrEmpty(keywordFilter) && keyword.IndexOf(keywordFilter, StringComparison.InvariantCultureIgnoreCase) < 0)
                            continue;


                        if (needsTitle) {
                            needsTitle = false;
                            GUILayout.Label("Used keywords:");
                        }

                        int kvShadersCount = kwv.shaders.Count;

                        sb.Length = 0;
                        sb.Append("Keyword #");
                        sb.Append(s + 1);
                        sb.Append(" <b>");
                        sb.Append(kwv.keyword);
                        sb.Append("</b> found in ");
                        sb.Append(kvShadersCount);
                        sb.Append(" shader(s)");
                        EditorGUILayout.BeginHorizontal();
                        kwv.foldout = EditorGUILayout.Foldout(kwv.foldout, new GUIContent(sb.ToString()), foldoutRTF);

                        if (!kwv.isInternal && GUILayout.Button("Show In Project View", EditorStyles.miniButton, GUILayout.Width(160))) {
                            sortType = SortType.EnabledKeywordsCount;
                            projectShaderNameFilter = "";
                            keywordFilter = kwv.keyword;
                            keywordScopeFilter = KeywordScopeFilter.Any;
                            pragmaTypeFilter = PragmaTypeFilter.Any;
                            scanAllShaders = true;
                            if (shaders == null) ScanProject();
                            viewMode = ViewMode.Project;
                            GUIUtility.ExitGUI();
                        }

                        EditorGUILayout.EndHorizontal();
                        if (kwv.foldout) {
                            for (int m = 0; m < kvShadersCount; m++) {
                                ShaderBuildInfo shaderBuildInfo = kwv.shaders[m];
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("", GUILayout.Width(30));
                                EditorGUILayout.LabelField(shaderIcon, GUILayout.Width(18));
                                EditorGUILayout.LabelField(shaderBuildInfo.name);
                                if (shaderBuildInfo.isInternal) {
                                    GUILayout.Label("(Internal Shader)");
                                } else {
                                    if (GUILayout.Button("Locate", EditorStyles.miniButton, GUILayout.Width(80))) {
                                        PingShader(shaderBuildInfo.name);
                                    }
                                    if (GUILayout.Button("Show In Project View", EditorStyles.miniButton, GUILayout.Width(160))) {
                                        projectShaderNameFilter = shaderBuildInfo.simpleName;
                                        keywordFilter = "";
                                        keywordScopeFilter = KeywordScopeFilter.Any;
                                        pragmaTypeFilter = PragmaTypeFilter.Any;
                                        scanAllShaders = true;
                                        PingShader(shaderBuildInfo.name);
                                        if (shaders == null) ScanProject();
                                        viewMode = ViewMode.Project;
                                        GUIUtility.ExitGUI();
                                    }
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }
                    }
                }
            } else {
                for (int k = 0; k < shadersCount; k++) {
                    ShaderBuildInfo shaderBuildInfo = shadersBuildInfo.shaders[k];

                    int kwCount = shaderBuildInfo.keywords == null ? 0 : shaderBuildInfo.keywords.Count;
                    if (kwCount < minimumKeywordCount && minimumKeywordCount > 0) continue;

                    if ((shaderBuildInfo.isReadOnly || shaderBuildInfo.isInternal) && shadersBuildInfo.hideReadOnlyShaders) continue;
                    if (!string.IsNullOrEmpty(keywordFilter) && !shaderBuildInfo.ContainsKeyword(keywordFilter, false))
                        continue;
                    if (!string.IsNullOrEmpty(buildShaderNameFilter) && shaderBuildInfo.name.IndexOf(buildShaderNameFilter, StringComparison.InvariantCultureIgnoreCase) < 0) continue;

                    if (needsTitle) {
                        needsTitle = false;
                        GUILayout.Label("Compiled shaders:");
                    }

                    GUI.enabled = shaderBuildInfo.includeInBuild;
                    EditorGUILayout.BeginHorizontal();

                    sb.Length = 0;
                    sb.Append(shaderBuildInfo.name);
                    if (shaderBuildInfo.isInternal) sb.Append(" (internal, ");
                    else if (shaderBuildInfo.isReadOnly) sb.Append(" (read-only, ");
                    else sb.Append(" (");
                    sb.Append(kwCount);
                    sb.Append(" keyword");
                    sb.Append(kwCount != 1 ? "s, " : ", ");
                    sb.Append(shaderBuildInfo.variantCount);
                    sb.Append(" variant");
                    sb.Append(shaderBuildInfo.variantCount != 1 ? "s)" : ")");
                    
                    shaderBuildInfo.isExpanded = EditorGUILayout.Foldout(shaderBuildInfo.isExpanded, sb.ToString(), shaderBuildInfo.isInternal ? foldoutDim : foldoutNormal);
                    GUILayout.FlexibleSpace();
                    GUI.enabled = true;
                    if (shaderBuildInfo.name != "Standard") {
                        EditorGUI.BeginChangeCheck();
                        shaderBuildInfo.includeInBuild = EditorGUILayout.ToggleLeft("Include", shaderBuildInfo.includeInBuild, GUILayout.Width(90));
                        if (EditorGUI.EndChangeCheck()) {
                            if (!shaderBuildInfo.includeInBuild) {
                                OfferRemoveFromAlwaysIncluded(shaderBuildInfo.name);
                            }
                            ForceReimportShaderAsset(shaderBuildInfo.path);
                            requireUpdate = true;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    if (shaderBuildInfo.isExpanded) {
                        GUI.enabled = shaderBuildInfo.includeInBuild;
                        EditorGUI.indentLevel++;
                        if (kwCount == 0) {
                            EditorGUILayout.LabelField("No keywords.");
                        } else {
                            if (!shaderBuildInfo.isInternal) {
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("", GUILayout.Width(15));
                                if (GUILayout.Button("Locate", EditorStyles.miniButton, GUILayout.Width(80))) {
                                    PingShader(shaderBuildInfo.name);
                                }
                                if (!shaderBuildInfo.isInternal && GUILayout.Button("Show In Project View", EditorStyles.miniButton, GUILayout.Width(160))) {
                                    projectShaderNameFilter = shaderBuildInfo.simpleName;
                                    keywordScopeFilter = KeywordScopeFilter.Any;
                                    pragmaTypeFilter = PragmaTypeFilter.Any;
                                    scanAllShaders = true;
                                    PingShader(shaderBuildInfo.name);
                                    if (shaders == null) ScanProject();
                                    viewMode = ViewMode.Project;
                                    GUIUtility.ExitGUI();
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                            for (int j = 0; j < kwCount; j++) {
                                KeywordBuildSettings kw = shaderBuildInfo.keywords[j];
                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField(kw.keyword);
                                GUILayout.FlexibleSpace();
                                EditorGUI.BeginChangeCheck();
                                kw.includeInBuild = EditorGUILayout.ToggleLeft("Include", kw.includeInBuild, GUILayout.Width(90));
                                if (EditorGUI.EndChangeCheck()) {
                                    if (!kw.includeInBuild) {
                                        OfferRemoveFromAlwaysIncluded(shaderBuildInfo.name);
                                    }
                                    ForceReimportShaderAsset(shaderBuildInfo.path);
                                    requireUpdate = true;
                                }
                                EditorGUILayout.EndHorizontal();
                            }
                        }

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(20);
                        if (GUILayout.Button("Advanced...", GUILayout.Width(120))) {
                            SCShader projectShader = GetShaderByName(shaderBuildInfo.name);
                            SCWindowAdvanced.ShowWindow(shaderBuildInfo, projectShader);
                        }
                        int variantsCount = shaderBuildInfo.variants != null ? shaderBuildInfo.variants.Count : 0;
                        if (variantsCount > 0) {
                            GUILayout.Label("(Only building " + variantsCount + " variants)");
                        }
                        EditorGUILayout.EndHorizontal();


                        EditorGUI.indentLevel--;
                    }
                    GUI.enabled = true;
                }
            }
            EditorGUILayout.EndScrollView();

            if (requireUpdate) {
                ShaderDebugBuildProcessor.CreateDummyQuickBuildGO(false);
                RefreshBuildStats(true);
                shadersBuildInfo.requiresBuild = true;
                EditorUtility.SetDirty(shadersBuildInfo);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

        }


        static bool IsShaderAlwaysIncluded(Shader shader) {
            if (shader == null) return false;
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (objs == null || objs.Length == 0 || objs[0] == null) return false;
            SerializedObject so = new SerializedObject(objs[0]);
            SerializedProperty prop = so.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null || !prop.isArray) return false;
            int size = prop.arraySize;
            for (int i = 0; i < size; i++) {
                SerializedProperty elem = prop.GetArrayElementAtIndex(i);
                if (elem != null && elem.objectReferenceValue == shader) return true;
            }
            return false;
        }

        static void RemoveShaderFromAlwaysIncluded(Shader shader) {
            if (shader == null) return;
            UnityEngine.Object[] objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (objs == null || objs.Length == 0 || objs[0] == null) return;
            SerializedObject so = new SerializedObject(objs[0]);
            SerializedProperty prop = so.FindProperty("m_AlwaysIncludedShaders");
            if (prop == null || !prop.isArray) return;
            for (int i = 0; i < prop.arraySize; i++) {
                SerializedProperty elem = prop.GetArrayElementAtIndex(i);
                if (elem != null && elem.objectReferenceValue == shader) {
                    prop.DeleteArrayElementAtIndex(i);
                    so.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    return;
                }
            }
        }

        static void ForceReimportShaderAsset(string assetPath) {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (!File.Exists(assetPath)) return;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        static void ForceReimportAllBuildShaders(ShadersBuildInfo info) {
            if (info == null || info.shaders == null) return;
            int count = info.shaders.Count;
            for (int i = 0; i < count; i++) {
                ShaderBuildInfo sb = info.shaders[i];
                if (sb != null && !string.IsNullOrEmpty(sb.path)) {
                    ForceReimportShaderAsset(sb.path);
                }
            }
        }

        void AfterConfigurationApplied(ShadersBuildInfo info) {
            if (info == null) return;
            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
            ForceReimportAllBuildShaders(info);
            EditorUtility.SetDirty(info);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ShaderDebugBuildProcessor.CreateDummyQuickBuildGO(false);
            RefreshBuildStats(true);
            GUIUtility.ExitGUI();
        }

        static void OfferRemoveFromAlwaysIncluded(string shaderName) {
            if (string.IsNullOrEmpty(shaderName)) return;
            Shader shaderObj = Shader.Find(shaderName);
            if (shaderObj == null) return;
            if (!IsShaderAlwaysIncluded(shaderObj)) return;
            bool remove = EditorUtility.DisplayDialog("Shader Always Included",
                "This shader is in the 'Always Included Shaders' list and won't be stripped.\n\nDo you want to remove it from that list now?",
                "Yes", "No");
            if (remove) {
                RemoveShaderFromAlwaysIncluded(shaderObj);
            }
        }

    }

}
