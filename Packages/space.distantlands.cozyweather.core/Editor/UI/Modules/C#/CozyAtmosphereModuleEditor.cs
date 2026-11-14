//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyAtmosphereModule))]
    public class CozyAtmosphereModuleEditor : CozyBiomeModuleEditor
    {

        CozyAtmosphereModule atmosphereModule;

        [NonSerialized]
        public VisualElement inspectorContainer;

        private VisualElement atmosphereProfileDisplayContainer;
        VisualElement root;


        void OnEnable()
        {
            if (target && target.GetType() == typeof(CozyAtmosphereModule))
                atmosphereModule = (CozyAtmosphereModule)target;
        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            atmosphereProfileDisplayContainer = new VisualElement();
            atmosphereProfileDisplayContainer.AddToClassList("profileDisplay");
            root.Add(atmosphereProfileDisplayContainer);

            VisualElement background = new VisualElement();
            background.name = "background";
            atmosphereProfileDisplayContainer.Add(background);

            VisualElement dropdown = new VisualElement();
            dropdown.name = "dropdown";
            atmosphereProfileDisplayContainer.Add(dropdown);

            Label title = new Label();
            title.name = "title";
            atmosphereProfileDisplayContainer.Add(title);

            Label description = new Label();
            description.name = "description";
            atmosphereProfileDisplayContainer.Add(description);

            inspectorContainer = new VisualElement();
            inspectorContainer.style.marginRight = 5;
            inspectorContainer.style.marginLeft = 14;
            root.Add(inspectorContainer);

            atmosphereProfileDisplayContainer.RegisterCallback<ClickEvent>(evt =>
            {
                CozyAtmosphereBrowserWindow.Open(atmosphereModule, () =>
                {
                    RefreshInspector();
                });
            });

            atmosphereProfileDisplayContainer.RegisterCallback<ContextClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Open Atmosphere Browser"), false, () =>
                {
                    CozyAtmosphereBrowserWindow.Open(atmosphereModule, () =>
                    {
                        RefreshInspector();
                    });
                });

                if (atmosphereModule.AtmosphereProfile)
                    menu.AddItem(new GUIContent("View in Project"), false, () =>
                    {
                        Selection.activeObject = atmosphereModule.AtmosphereProfile;
                    });

                menu.ShowAsContext();
            });

            RefreshInspector();


            Label overridesLabel = new Label("Local Overrides");
            overridesLabel.AddToClassList("h1");
            root.Add(overridesLabel);

            PropertyField overrides = new PropertyField();
            overrides.BindProperty(serializedObject.FindProperty("overrides"));
            root.Add(overrides);


            Button addOverrideButton = new Button { text = "Add Override" };
            addOverrideButton.style.marginTop = 7;
            addOverrideButton.RegisterCallback<ClickEvent>(evt =>
            {
                atmosphereModule.UpdateModel();
                AtmosphereOverrideSearchProvider provider = CreateInstance<AtmosphereOverrideSearchProvider>();
                provider.overridesList = atmosphereModule.overrides;
                provider.model = atmosphereModule.ResolvedModel;
                provider.arrayProperty = serializedObject.FindProperty("overrides");
                provider.action = () =>
                {
                    serializedObject.Update();
                    overrides.Bind(serializedObject);
                    atmosphereModule.UpdateModel();
                };
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            });
            root.Add(addOverrideButton);

            Button saveButton = new Button { text = "Save to Profile" };
            saveButton.enabledSelf = serializedObject.FindProperty("overrides").arraySize > 0;
            saveButton.tooltip = "Save the local overrides to a profile that can be assigned elsewhere.";
            saveButton.RegisterCallback<ClickEvent>(evt =>
            {
                if (atmosphereModule.overrides.Count == 0)
                {
                    Debug.Log("Trying to save with no overrides. Add an override before saving!");
                    return;
                }

                if (!atmosphereModule.AtmosphereProfile)
                {
                    EditorUtility.DisplayDialog("Save Atmosphere Profile", "Select an atmosphere profile before saving local overrides.", "OK");
                    return;
                }

                string defaultName = $"Modified {atmosphereModule.AtmosphereProfile.Name}";
                string path = EditorUtility.SaveFilePanelInProject("Save Atmosphere Profile", defaultName, "asset", "Save your new atmosphere profile.");

                if (!string.IsNullOrEmpty(path))
                {
                    serializedObject.ApplyModifiedProperties();
                    AssetDatabase.CreateAsset(CompileProfileAndOverrides(), path);
                    AssetDatabase.SaveAssets();

                    AtmosphereProfile savedProfile = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(path);
                    if (atmosphereModule != null)
                    {
                        atmosphereModule.AtmosphereProfile = savedProfile;
                        Selection.activeObject = savedProfile;
                        atmosphereModule.overrides.Clear();

                    }
                }
            });
            root.Add(saveButton);


            overrides.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                atmosphereModule?.UpdateModel();
                saveButton.enabledSelf = serializedObject.FindProperty("overrides").arraySize > 0;
            });

            atmosphereProfileDisplayContainer.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            atmosphereProfileDisplayContainer.RegisterCallback<DragPerformEvent>(OnDragPerform);

            return root;

        }

        private AtmosphereProfile CompileProfileAndOverrides()
        {
            return CompileProfileAndOverrides(atmosphereModule.AtmosphereProfile, serializedObject);
        }

        private static AtmosphereProfile CompileProfileAndOverrides(AtmosphereProfile sourceProfile, SerializedObject localObject)
        {
            var newProfile = Instantiate(sourceProfile);
            newProfile.name = sourceProfile.name;
            newProfile.Locked = false;

            var profileObject = new SerializedObject(newProfile);
            var profileOverrides = profileObject.FindProperty("overrides");
            var localOverrides = localObject.FindProperty("overrides");

            for (int i = 0; i < localOverrides.arraySize; i++)
            {
                var localOverride = localOverrides.GetArrayElementAtIndex(i);
                var localValue = localOverride.managedReferenceValue as AtmosphereOverride;
                if (localValue == null || !localValue.active)
                    continue;

                SerializedProperty targetOverride = null;
                for (int j = profileOverrides.arraySize - 1; j >= 0; j--)
                {
                    var candidate = profileOverrides.GetArrayElementAtIndex(j);
                    var candidateValue = candidate.managedReferenceValue as AtmosphereOverride;
                    if (candidateValue != null && candidateValue.active && candidateValue.GetType() == localValue.GetType())
                    {
                        targetOverride = candidate;
                        break;
                    }
                }

                var property = localOverride.Copy();
                var end = property.GetEndProperty();
                bool enterChildren = true;
                while (property.Next(enterChildren) && !SerializedProperty.EqualContents(property, end))
                {
                    enterChildren = false;
                    if (property.depth != localOverride.depth + 1 || property.propertyType != SerializedPropertyType.Generic)
                        continue;

                    var enabled = property.FindPropertyRelative("overrideValue");
                    if (enabled == null || !enabled.boolValue)
                        continue;

                    if (targetOverride == null)
                    {
                        int index = profileOverrides.arraySize;
                        profileOverrides.InsertArrayElementAtIndex(index);
                        targetOverride = profileOverrides.GetArrayElementAtIndex(index);
                        targetOverride.managedReferenceValue = Activator.CreateInstance(localValue.GetType());
                    }

                    targetOverride.FindPropertyRelative(property.name).boxedValue = property.boxedValue;
                }

                profileObject.ApplyModifiedPropertiesWithoutUndo();
            }

            return newProfile;
        }

        private void OnDragUpdated(DragUpdatedEvent evt)
        {
            bool isValid = false;
            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is AtmosphereProfile)
                {
                    isValid = true;
                    break;
                }
            }

            if (isValid)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
            }
            else
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
            }
        }

        private void OnDragPerform(DragPerformEvent evt)
        {
            DragAndDrop.AcceptDrag();

            foreach (var obj in DragAndDrop.objectReferences)
            {
                if (obj is AtmosphereProfile profile)
                {
                    atmosphereModule.AtmosphereProfile = profile;
                    RefreshInspector();
                    break;
                }
            }
        }

        public void RefreshInspector()
        {
            if (!atmosphereModule) return;

            if (!atmosphereModule.AtmosphereProfile)
            {
                atmosphereProfileDisplayContainer.Q<Label>("title").text = "Select a Profile";
                atmosphereProfileDisplayContainer.Q<Label>("description").text = "Or directly override properties below";
                atmosphereProfileDisplayContainer.Q<VisualElement>("background").style.backgroundImage = null;
                return;
            }

            atmosphereProfileDisplayContainer.Q<Label>("title").text = atmosphereModule.AtmosphereProfile.Name;
            atmosphereProfileDisplayContainer.Q<Label>("description").text = atmosphereModule.AtmosphereProfile.Description;
            atmosphereProfileDisplayContainer.Q<VisualElement>("background").style.backgroundImage = atmosphereModule.AtmosphereProfile.Icon;
        }

        public override VisualElement DisplayBiomeUI()
        {
            root = new VisualElement();

            atmosphereProfileDisplayContainer = new VisualElement();
            atmosphereProfileDisplayContainer.AddToClassList("profileDisplay");
            root.Add(atmosphereProfileDisplayContainer);

            VisualElement background = new VisualElement();
            background.name = "background";
            atmosphereProfileDisplayContainer.Add(background);

            VisualElement dropdown = new VisualElement();
            dropdown.name = "dropdown";
            atmosphereProfileDisplayContainer.Add(dropdown);

            Label title = new Label();
            title.name = "title";
            atmosphereProfileDisplayContainer.Add(title);

            Label description = new Label();
            description.name = "description";
            atmosphereProfileDisplayContainer.Add(description);

            inspectorContainer = new VisualElement();
            inspectorContainer.style.marginRight = 5;
            inspectorContainer.style.marginLeft = 14;
            root.Add(inspectorContainer);

            atmosphereProfileDisplayContainer.RegisterCallback<ClickEvent>(evt =>
            {
                CozyAtmosphereBrowserWindow.Open(atmosphereModule, () =>
                {
                    RefreshInspector();
                });
            });

            atmosphereProfileDisplayContainer.RegisterCallback<ContextClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();

                menu.AddItem(new GUIContent("Open Atmosphere Browser"), false, () =>
                {
                    CozyAtmosphereBrowserWindow.Open(atmosphereModule, () =>
                    {
                        RefreshInspector();
                    });
                });

                if (atmosphereModule.AtmosphereProfile)
                    menu.AddItem(new GUIContent("View in Project"), false, () =>
                    {
                        Selection.activeObject = atmosphereModule.AtmosphereProfile;
                    });

                menu.ShowAsContext();
            });

            RefreshInspector();


            Label overridesLabel = new Label("Local Overrides");
            overridesLabel.AddToClassList("h1");
            root.Add(overridesLabel);

            PropertyField overrides = new PropertyField();
            overrides.BindProperty(serializedObject.FindProperty("overrides"));
            root.Add(overrides);

            overrides.RegisterCallback<SerializedPropertyChangeEvent>(evt =>
            {
                atmosphereModule?.UpdateModel();
            });

            Button addOverrideButton = new Button { text = "Add Override" };
            addOverrideButton.style.marginTop = 7;
            addOverrideButton.RegisterCallback<ClickEvent>(evt =>
            {
                atmosphereModule.UpdateModel();
                AtmosphereOverrideSearchProvider provider = CreateInstance<AtmosphereOverrideSearchProvider>();
                provider.overridesList = atmosphereModule.overrides;
                provider.model = atmosphereModule.ResolvedModel;
                provider.arrayProperty = serializedObject.FindProperty("overrides");
                provider.action = () =>
                {
                    serializedObject.Update();
                    overrides.Bind(serializedObject);
                    atmosphereModule.UpdateModel();
                };
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            });
            root.Add(addOverrideButton);

            atmosphereProfileDisplayContainer.RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
            atmosphereProfileDisplayContainer.RegisterCallback<DragPerformEvent>(OnDragPerform);

            return root;

        }

        public override void DrawGizmos()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null && sceneView.camera != null)
            {
                Vector3 cameraPos = sceneView.camera.transform.position;

                //Draw Sun

                Vector3 sunPathDiscDirection = Quaternion.AngleAxis(CozyPreferences.SkyPitch, CozyPreferences.West) * CozyPreferences.North;

                Handles.color = new Color(1, 0.84f, 0.32f, 0.3f);
                Handles.DrawWireArc(cameraPos, sunPathDiscDirection, Quaternion.AngleAxis(5, sunPathDiscDirection) * -atmosphereModule.SunDirection.normalized, 350, 0.5f, 1);
                Handles.DrawWireArc(cameraPos - atmosphereModule.SunDirection * 10, atmosphereModule.SunDirection, Vector3.Cross(atmosphereModule.SunDirection, Vector3.up).normalized, 360, 0.5f, 1);

                //Draw Moon

                Vector3 moonPathDiscDirection = Quaternion.AngleAxis(CozyPreferences.SkyPitch, CozyPreferences.West) * CozyPreferences.North;
                Vector3 moonPositionDirection = -atmosphereModule.MoonDirection;

                Handles.color = new Color(0.23f, 0.34f, 0.92f, 0.3f);
                Handles.DrawWireArc(cameraPos, moonPathDiscDirection, Quaternion.AngleAxis(5, moonPathDiscDirection) * moonPositionDirection.normalized, 350, 0.5f, 1);
                Handles.DrawWireArc(cameraPos + moonPositionDirection * 10, -moonPositionDirection, Vector3.Cross(moonPositionDirection, Vector3.up).normalized, 360, 0.5f, 1);

                //Draw Compass

                GUIStyle compassTextStyle = new GUIStyle();
                compassTextStyle.normal.textColor = Color.white;
                compassTextStyle.fontStyle = FontStyle.Bold;
                compassTextStyle.alignment = TextAnchor.MiddleCenter;

                Handles.color = new Color(1, 1, 1, 0.1f);
                Handles.Label(cameraPos + CozyPreferences.West, "W", compassTextStyle);
                Handles.Label(cameraPos + CozyPreferences.East, "E", compassTextStyle);
                Handles.Label(cameraPos + CozyPreferences.North, "N", compassTextStyle);
                Handles.Label(cameraPos + CozyPreferences.South, "S", compassTextStyle);

                Quaternion rotation = Quaternion.AngleAxis(7.5f, Vector3.up);
                Handles.DrawWireArc(cameraPos, Vector3.up, rotation * CozyPreferences.West, 75, 10, 2);
                Handles.DrawWireArc(cameraPos, Vector3.up, rotation * CozyPreferences.East, 75, 10, 2);
                Handles.DrawWireArc(cameraPos, Vector3.up, rotation * CozyPreferences.North, 75, 10, 2);
                Handles.DrawWireArc(cameraPos, Vector3.up, rotation * CozyPreferences.South, 75, 10, 2);

            }
        }


        public override ModuleWidget[] ModuleWidgets => new ModuleWidget[]
        {
            new(
                ModuleWidget.WidgetSize.Small, "Atmosphere Widget", true,
                () => {
                    VisualElement container = new();
                    container.style.paddingBottom = 8;
                    container.style.paddingLeft = 8;
                    container.style.paddingRight = 8;
                    container.style.paddingTop = 8;
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.SpaceAround;
                    container.style.flexGrow = 1;

                    VisualElement icon = new VisualElement();
                    icon.style.height = 40;
                    icon.style.width = 40;
                    icon.style.backgroundImage = (Texture2D)Resources.Load("COZY Icons/Atmosphere");
                    container.Add(icon);

                    VisualElement textContainer = new VisualElement();

                    Label profileLabel = new Label();
                    profileLabel.name = "profileLabel";
                    profileLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    profileLabel.style.fontSize = 9;
                    profileLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                    if (atmosphereModule.AtmosphereProfile == null)
                        profileLabel.text = "No Profile";
                    else
                        profileLabel.text = atmosphereModule.AtmosphereProfile.name;

                    textContainer.Add(profileLabel);

                    Label overridesLabel = new Label();
                    overridesLabel.name = "overridesLabel";
                    overridesLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    overridesLabel.style.fontSize = 8;
                    overridesLabel.text = $"{atmosphereModule.overrides.Count} Overrides";
                    textContainer.Add(overridesLabel);

                    container.Add(textContainer);


                    container.RegisterCallback<ClickEvent>(evt =>
                    {
                        GenericMenu menu = new GenericMenu();

                        bool hasCurrentWeather = atmosphereModule.AtmosphereProfile;
                        List<AtmosphereProfile> results = new List<AtmosphereProfile>();

                        string[] guids = AssetDatabase.FindAssets("t:" + typeof(AtmosphereProfile).Name);

                        foreach (string guid in guids)
                        {
                            string path = AssetDatabase.GUIDToAssetPath(guid);
                            AtmosphereProfile asset = AssetDatabase.LoadAssetAtPath<AtmosphereProfile>(path);
                            if (asset != null)
                            {
                                results.Add(asset);
                            }
                        }

                        foreach (AtmosphereProfile profile in results)
                        {
                            menu.AddItem(new GUIContent(profile.name), hasCurrentWeather & profile == atmosphereModule.AtmosphereProfile, () =>
                            {
                                atmosphereModule.AtmosphereProfile = profile;
                                container.Q<Label>("profileLabel").text = atmosphereModule.AtmosphereProfile.name;
                                container.Q<Label>("overridesLabel").text = $"{atmosphereModule.AtmosphereProfile.overrides.Count} Overrides";
                            });
                        }

                        menu.ShowAsContext();
                    });

                    return container;
                },
                (widget) => {
                    if (atmosphereModule.AtmosphereProfile == null)
                    {
                        widget.container.Q<Label>("label").text = "Click to Select a Profile";
                        return;
                    }
                    widget.container.Q<Label>("profileLabel").text = atmosphereModule.AtmosphereProfile.name;
                    widget.container.Q<Label>("overridesLabel").text = $"{atmosphereModule.AtmosphereProfile.overrides.Count} Overrides";
                }
            )
        };

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/modules/atmosphere-module");
        }


    }
}