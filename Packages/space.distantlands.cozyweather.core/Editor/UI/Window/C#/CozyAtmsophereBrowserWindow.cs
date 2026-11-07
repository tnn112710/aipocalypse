//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;


#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyAtmosphereBrowserWindow : EditorWindow
    {
        private static CozyAtmosphereBrowserWindow wnd;

        private CozyAtmosphereModule atmosphereModule;
        private Action action;

        public AtmosphereProfile currentInspectedProfile;
        public AtmosphereProfile initialWindowProfile;
        private AtmosphereProfile editableProfile;

        private string searchTerm;
        private string initialProfileJson = "";

        private bool isCreatingNew = false;
        private bool hasSaved = false;
        private bool isExplicitCancel = false;

        private List<AtmosphereProfile> foundProfiles = new List<AtmosphereProfile>();

        public static void Open(CozyAtmosphereModule atm, Action act)
        {
            if (wnd)
            {
                wnd.atmosphereModule = atm;
                wnd.action = act;
                // wnd.RefreshWindowData();
                wnd.Focus();
                return;
            }

            wnd = CreateInstance<CozyAtmosphereBrowserWindow>();
            wnd.titleContent = new GUIContent("Select an Atmosphere");
            wnd.atmosphereModule = atm;
            wnd.action = act;

            wnd.saveChangesMessage = "You have unsaved changes to your Atmosphere Profile. Would you like to save them?";

            wnd.RefreshWindowData();

            wnd.ShowUtility();
            wnd.minSize = new Vector2(500, 320);
        }

        private void RefreshWindowData()
        {
            if (atmosphereModule != null && atmosphereModule.AtmosphereProfile != null)
                currentInspectedProfile = atmosphereModule.AtmosphereProfile;
            else
                currentInspectedProfile = null;

            initialWindowProfile = currentInspectedProfile;
            hasSaved = false;
            isExplicitCancel = false;
            hasUnsavedChanges = false;

            foundProfiles = new List<AtmosphereProfile>();
            string[] guids = AssetDatabase.FindAssets("t:AtmosphereProfile");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AtmosphereProfile asset = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(path);
                if (asset != null) foundProfiles.Add(asset);
            }

            if (rootVisualElement.childCount > 0)
            {
                CreateGUI();
            }
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();

            if (atmosphereModule == null)
            {
                Close();
                return;
            }

            StyleSheet style = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss"
            );

            if (style != null)
                rootVisualElement.styleSheets.Add(style);

            VisualElement searchContainer = new VisualElement();
            searchContainer.AddToClassList("searchContainer");
            rootVisualElement.Add(searchContainer);

            ToolbarSearchField searchField = new ToolbarSearchField();
            searchContainer.Add(searchField);

            TwoPaneSplitView splitView = new TwoPaneSplitView();
            rootVisualElement.Add(splitView);
            splitView.fixedPaneInitialDimension = 280;

            ScrollView atmosphereListContainer = new ScrollView();
            splitView.Add(atmosphereListContainer);

            ScrollView detailsContainer = new ScrollView();
            detailsContainer.AddToClassList("detailsContainer");
            splitView.Add(detailsContainer);

            void BuildAtmosphereList()
            {
                atmosphereListContainer.Clear();

                VisualElement emptyOption = new VisualElement();
                emptyOption.AddToClassList("atmosphereProfileOption");
                emptyOption.RegisterCallback<ClickEvent>(evt =>
                {
                    HandleProfileSwitch(null);
                });

                Label emptyLabel = new Label();
                emptyLabel.text = "None";
                emptyLabel.name = "label";
                emptyOption.Add(emptyLabel);

                atmosphereListContainer.Add(emptyOption);

                var groups = foundProfiles.GroupBy(x => x.Category).OrderBy(x => x.Key);

                foreach (var group in groups)
                {
                    VisualElement parent = atmosphereListContainer;

                    if (group.Key != "")
                    {
                        Foldout categoryLabel = new Foldout();
                        categoryLabel.text = group.Key;
                        categoryLabel.AddToClassList("atmosphereProfileFoldout");
                        atmosphereListContainer.Add(categoryLabel);

                        parent = categoryLabel;
                    }


                    List<AtmosphereProfile> profilesInGroup = group.ToList();

                    for (int i = 0; i < profilesInGroup.Count; i++)
                    {
                        AtmosphereProfile prof = profilesInGroup[i];

                        if (!string.IsNullOrEmpty(searchTerm))
                        {
                            bool nameMatch = !string.IsNullOrEmpty(prof.name) && prof.name.ToLower().Contains(searchTerm);
                            bool catMatch = !string.IsNullOrEmpty(prof.Category) && prof.Category.ToLower().Contains(searchTerm);
                            bool descMatch = !string.IsNullOrEmpty(prof.Description) && prof.Description.ToLower().Contains(searchTerm);

                            if (!nameMatch && !descMatch && !catMatch)
                                continue;
                        }

                        VisualElement profileOption = new VisualElement();
                        profileOption.AddToClassList("atmosphereProfileOption");

                        if (prof == currentInspectedProfile)
                            profileOption.AddToClassList("active");

                        profileOption.RegisterCallback<ClickEvent>(evt =>
                        {
                            HandleProfileSwitch(prof);
                        });
                        profileOption.RegisterCallback<ContextClickEvent>(evt =>
                        {
                            GenericMenu menu = new GenericMenu();
                            menu.AddItem(new GUIContent("View in Project"), false, () =>
                            {
                                Selection.activeObject = prof;
                            });
                            menu.AddItem(new GUIContent("Locked"), prof.Locked, () =>
                            {
                                prof.Locked = !prof.Locked;
                            });
                            menu.ShowAsContext();
                        });

                        Label label = new Label();
                        label.text = prof.Name;
                        label.name = "label";
                        profileOption.Add(label);

                        VisualElement icon = new VisualElement();
                        icon.name = "icon";
                        if (prof.Icon != null)
                            icon.style.backgroundImage = new StyleBackground(prof.Icon);
                        profileOption.Add(icon);

                        parent.Add(profileOption);
                    }
                }
            }

            void HandleProfileSwitch(AtmosphereProfile newProfile)
            {
                if (hasUnsavedChanges)
                {
                    if (!EditorUtility.DisplayDialog("Unsaved Changes", "You have unsaved changes. Discard them to view a different profile?", "Discard", "Cancel"))
                    {
                        return;
                    }
                }

                hasUnsavedChanges = false;
                isCreatingNew = false;
                currentInspectedProfile = newProfile;
                BuildAtmosphereList();
                BuildDetailsContainer();
            }

            void BuildDetailsContainer()
            {
                detailsContainer.Clear();

                if (editableProfile != null)
                    DestroyImmediate(editableProfile);

                if (isCreatingNew)
                {
                    editableProfile = CreateInstance<AtmosphereProfile>();
                    editableProfile.name = "New Atmosphere Profile";
                }
                else if (currentInspectedProfile)
                {
                    editableProfile = Instantiate(currentInspectedProfile);
                    editableProfile.name = currentInspectedProfile.name;
                }
                else
                {
                    editableProfile = null;
                }

                initialProfileJson = editableProfile != null ? EditorJsonUtility.ToJson(editableProfile) : "";
                hasUnsavedChanges = false;

                if (atmosphereModule != null)
                {
                    atmosphereModule.AtmosphereProfile = editableProfile;
                    action?.Invoke();
                }

                VisualElement screenshot = new VisualElement();
                screenshot.name = "screenshot";
                if (currentInspectedProfile && !isCreatingNew && currentInspectedProfile.Icon != null)
                    screenshot.style.backgroundImage = new StyleBackground(currentInspectedProfile.Icon);
                detailsContainer.Add(screenshot);

                Label title = new Label();
                if (isCreatingNew)
                    title.text = "New Atmosphere Profile";
                else if (currentInspectedProfile)
                    title.text = currentInspectedProfile.Name;
                else
                    title.text = "No Profile";

                title.name = "title";
                detailsContainer.Add(title);

                if (editableProfile == null)
                {
                    Label noneLabel = new Label("Select a profile to edit or rely on overrides for your atmosphere.");
                    detailsContainer.Add(noneLabel);
                    return;
                }

                SerializedObject serializedObj = new SerializedObject(editableProfile);
                InspectorElement inspectorElement = new InspectorElement(serializedObj);

                inspectorElement.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
                {
                    UpdateUnsavedStatus();
                    atmosphereModule?.UpdateModel();
                    action?.Invoke();
                });

                ScrollView inspectorScroll = new ScrollView();
                inspectorScroll.Add(inspectorElement);
                detailsContainer.Add(inspectorScroll);
            }

            searchField.RegisterValueChangedCallback(evt =>
            {
                searchTerm = evt.newValue != null ? evt.newValue.ToLower() : "";
                BuildAtmosphereList();
            });

            BuildAtmosphereList();
            BuildDetailsContainer();

            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("atmosphereBrowserFooter");

            Button cancel = new Button();
            cancel.text = "Cancel";
            cancel.clicked += () =>
            {
                isExplicitCancel = true;
                hasUnsavedChanges = false;
                EditorApplication.delayCall += Close;
            };
            toolbar.Add(cancel);

            Button submit = new Button();
            submit.text = "Save";
            submit.clicked += () =>
            {
                try
                {
                    HandleSaving();
                    hasUnsavedChanges = false;
                    EditorApplication.delayCall += Close;
                }
                catch (OperationCanceledException)
                {
                }
            };
            toolbar.Add(submit);

            rootVisualElement.Add(toolbar);
        }

        private void UpdateUnsavedStatus()
        {
            if (editableProfile == null)
            {
                hasUnsavedChanges = false;
                return;
            }

            string currentJson = EditorJsonUtility.ToJson(editableProfile);
            hasUnsavedChanges = (currentJson != initialProfileJson);
        }

        public override void SaveChanges()
        {
            HandleSaving();
            base.SaveChanges();
        }

        private void HandleSaving()
        {
            if (editableProfile != null)
            {
                bool actualChangesMade = true;
                if (!isCreatingNew && currentInspectedProfile != null)
                {
                    string originalJson = EditorJsonUtility.ToJson(currentInspectedProfile);
                    string editedJson = EditorJsonUtility.ToJson(editableProfile);
                    actualChangesMade = (originalJson != editedJson);
                }

                if (!isCreatingNew && !actualChangesMade)
                {
                    if (atmosphereModule != null)
                    {
                        atmosphereModule.AtmosphereProfile = currentInspectedProfile;
                        action?.Invoke();
                    }
                    hasSaved = true;
                    hasUnsavedChanges = false;
                    return;
                }

                if (isCreatingNew || (currentInspectedProfile != null && currentInspectedProfile.Locked))
                {
                    string defaultName = isCreatingNew ? "New Atmosphere Profile" : currentInspectedProfile.name + " (Copy)";
                    string dialogDesc = isCreatingNew ? "Save your new atmosphere profile." : "This profile is locked. Save your modified profile as a new asset.";

                    string path = EditorUtility.SaveFilePanelInProject("Save Atmosphere Profile", defaultName, "asset", dialogDesc);

                    if (!string.IsNullOrEmpty(path))
                    {
                        AssetDatabase.CreateAsset(editableProfile, path);
                        AssetDatabase.SaveAssets();

                        AtmosphereProfile savedProfile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(path);
                        if (atmosphereModule != null)
                        {
                            atmosphereModule.AtmosphereProfile = savedProfile;
                            action?.Invoke();
                        }

                        hasSaved = true;
                        hasUnsavedChanges = false;
                        initialProfileJson = EditorJsonUtility.ToJson(editableProfile);
                        editableProfile = null;
                    }
                    else
                    {
                        throw new OperationCanceledException("User cancelled the save operation.");
                    }
                }
                else if (currentInspectedProfile != null)
                {
                    EditorUtility.CopySerialized(editableProfile, currentInspectedProfile);
                    EditorUtility.SetDirty(currentInspectedProfile);
                    AssetDatabase.SaveAssets();

                    if (atmosphereModule != null)
                    {
                        atmosphereModule.AtmosphereProfile = currentInspectedProfile;
                        action?.Invoke();
                    }
                    hasSaved = true;
                    hasUnsavedChanges = false;
                    initialProfileJson = EditorJsonUtility.ToJson(editableProfile);
                }
            }
            else
            {
                hasSaved = true;
                hasUnsavedChanges = false;
            }
        }

        private void OnDestroy()
        {
            if (atmosphereModule != null)
            {
                if (isExplicitCancel)
                {
                    atmosphereModule.AtmosphereProfile = initialWindowProfile;
                }
                else if (!hasSaved)
                {
                    atmosphereModule.AtmosphereProfile = currentInspectedProfile;
                }

                action?.Invoke();
            }

            if (editableProfile != null && !AssetDatabase.Contains(editableProfile))
            {
                DestroyImmediate(editableProfile);
            }
        }
    }
}