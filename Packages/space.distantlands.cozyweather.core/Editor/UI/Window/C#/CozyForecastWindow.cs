//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    public class GraphNavigator : MouseManipulator
    {
        private bool _active;
        private VisualElement _content;

        public GraphNavigator(VisualElement content)
        {
            _content = content;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.MiddleMouse });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<WheelEvent>(OnScroll);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<WheelEvent>(OnScroll);
        }

        private void OnMouseDown(MouseDownEvent e)
        {
            if (CanStartManipulation(e))
            {
                _active = true;
                target.CaptureMouse();
                e.StopPropagation();
            }
        }

        private void OnMouseMove(MouseMoveEvent e)
        {
            if (_active && target.HasMouseCapture())
            {
                Vector2 currentTranslate = _content.resolvedStyle.translate;
                _content.style.translate = new Translate(currentTranslate.x + e.mouseDelta.x, currentTranslate.y + e.mouseDelta.y);
                e.StopPropagation();
            }
        }

        private void OnMouseUp(MouseUpEvent e)
        {
            if (_active && target.HasMouseCapture())
            {
                _active = false;
                target.ReleaseMouse();
                e.StopPropagation();
            }
        }
        private void OnScroll(WheelEvent e)
        {
            float zoomStep = 0.05f;

            float oldScale = _content.style.scale.value.value.x;
            Vector2 currentTranslate = new Vector2(_content.style.translate.value.x.value, _content.style.translate.value.y.value);
            float newScale = Mathf.Clamp(oldScale - (e.delta.y * zoomStep), 0.2f, 6.0f);

            if (Mathf.Approximately(oldScale, newScale)) return;

            Vector2 mousePosInViewport = e.localMousePosition;

            Vector2 mousePosInContent = (mousePosInViewport - currentTranslate) / oldScale;
            _content.style.scale = new Scale(new Vector2(newScale, newScale));
            Vector2 newTranslate = mousePosInViewport - (mousePosInContent * newScale);
            _content.style.translate = new Translate(newTranslate.x, newTranslate.y);
            e.StopPropagation();
        }
    }

#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyForecastWindow : EditorWindow
    {
        private bool _hasFramedInitial = false;
        VisualElement root;
        public static Vector2 winSize = new Vector2(600, 1000);
        private static CozyForecastWindow wnd;
        public static ForecastProfile forecastProfile;
        public ForecastProfile savedForecastProfile;
        public static SerializedObject so;

        [NonSerialized]
        public VisualElement content;
        [NonSerialized]
        public VisualElement viewport;
        VisualElement profileList;
        ScrollView profileInspector;

        public int selectedProfile = -1;
        int indexToEdit = -1;
        [NonSerialized]
        public ListView listView;

        [NonSerialized]
        public Dictionary<VisualElement, VisualElement> connectors;


        public static Color topColor = new Color(0.9176471f, 0.4705882f, 0.8352941f);
        public static Color repeatColor = new Color(0.3254902f, 0.7921569f, 0.5960785f);
        public static Color bottomColor = new Color(0.254902f, 0.5176471f, 0.9568627f);
        public static Color excludeColor = new Color(0.754902f, 0.2176471f, 0.4568627f);
        public static Color middleColor = Color.Lerp(bottomColor, topColor, 0.5f);

        struct NodeConnection
        {
            Vector2 start;
            Vector2 end;
            Color startColor;
            Color endColor;
        }

        [OnOpenAsset]
        public static bool OnOpenAsset(EntityId entityId, int line)
        {
            UnityEngine.Object obj = EditorUtility.EntityIdToObject(entityId);

            if (obj is ForecastProfile myData)
            {
                forecastProfile = myData;
                if (wnd) wnd.Close();

                wnd = GetWindow<CozyForecastWindow>("Forecast Editor");
                wnd.savedForecastProfile = forecastProfile;

                wnd.titleContent = new GUIContent(forecastProfile.name, (Texture2D)Resources.Load("COZY Icons/Sunny"));
                wnd.Show();

                return true;
            }

            return false;
        }

        public static void OpenWindow(ForecastProfile profile)
        {
            forecastProfile = profile;
            if (wnd) wnd.Close();

            wnd = GetWindow<CozyForecastWindow>("Forecast Editor");
            wnd.savedForecastProfile = forecastProfile;

            wnd.titleContent = new GUIContent(forecastProfile.name, (Texture2D)Resources.Load("COZY Icons/Sunny"));
            wnd.Show();
        }

        public void CreateGUI()
        {
            _hasFramedInitial = false;

            if (forecastProfile == null)
            {
                if (wnd && wnd.savedForecastProfile)
                {
                    forecastProfile = wnd.savedForecastProfile;
                }
                else
                {
                    Close();
                    return;
                }
            }
            so = new SerializedObject(forecastProfile);

            rootVisualElement.Clear();
            root = rootVisualElement;

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Window/UXML/ForecastWindow.uxml"
            );
            asset.CloneTree(root);

            VisualElement panel = root.Q<VisualElement>("panel");

            TwoPaneSplitView splitView = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            panel.Add(splitView);

            profileList = new VisualElement();
            profileInspector = new ScrollView();
            profileInspector.style.paddingLeft = 5;
            VisualElement blackboard = new VisualElement();
            blackboard.style.flexGrow = 1;
            blackboard.style.overflow = Overflow.Hidden;
            blackboard.AddToClassList("blackboard");

            splitView.Add(profileList);
            TwoPaneSplitView mainView = new TwoPaneSplitView(1, 320, TwoPaneSplitViewOrientation.Horizontal);
            splitView.Add(mainView);
            mainView.Add(blackboard);
            mainView.Add(profileInspector);

            viewport = new VisualElement() { name = "Viewport" };
            viewport.style.flexGrow = 1;
            viewport.style.overflow = Overflow.Hidden;
            viewport.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
            viewport.pickingMode = PickingMode.Position;



            content = new VisualElement() { name = "Content" };
            content.style.position = Position.Absolute;
            content.style.width = 0;
            content.style.height = 0;
            content.style.overflow = Overflow.Visible;
            content.style.transformOrigin = new TransformOrigin(0, 0);
            content.usageHints = UsageHints.DynamicTransform;
            content.pickingMode = PickingMode.Ignore;

            content.style.backgroundColor = new Color(0.15f, 0.15f, 0.15f);

            viewport.Add(content);
            blackboard.Add(viewport);

            viewport.AddManipulator(new GraphNavigator(content));

            UpdateSidePanel();
            SelectWeatherProfile();
        }


        public VisualElement NewGraphPanel(int x, int y)
        {
            VisualElement v = new VisualElement();
            v.style.position = Position.Absolute;
            v.style.left = x;
            v.style.top = y;
            v.style.transformOrigin = new TransformOrigin(new Length(50, LengthUnit.Percent), new Length(50, LengthUnit.Percent), 0);
            return v;
        }

        public void UpdateWeatherProfileInspector()
        {
            profileInspector.Unbind();
            profileInspector.Clear();

            SerializedProperty profilesArray = so.FindProperty("profiles");
            if (selectedProfile == -1 || selectedProfile >= profilesArray.arraySize)
                return;

            ForecastProfile.ForecastableWeather forecastable = forecastProfile.profiles[selectedProfile];
            SerializedProperty prop = profilesArray.GetArrayElementAtIndex(selectedProfile);
            SerializedProperty profileProp = prop.FindPropertyRelative("profile");


            Editor profileUI = Editor.CreateEditor(forecastable.profile);

            profileInspector.Add(profileUI.CreateInspectorGUI());
            SerializedObject profileSO = new SerializedObject(forecastable.profile);

            profileInspector.TrackPropertyValue(profileSO.FindProperty("lateralShifts"), evt =>
            {
                SelectWeatherProfile();
            });

            profileInspector.schedule.Execute(evt =>
            {
                profileInspector.Q<PropertyField>("lateralShifts").RegisterValueChangeCallback(evt => { SelectWeatherProfile(); });
                profileInspector.Q<PropertyField>("lateralShifts").Query<PropertyField>("lateralProfile").ForEach(el =>
                {
                    el.RegisterCallback<BlurEvent>(evt =>
                    {
                        SelectWeatherProfile();
                    });
                });


            }).ExecuteLater(5);
        }

        public void UpdateSidePanel()
        {
            profileList.Clear();
            SerializedProperty profilesArray = so.FindProperty("profiles");

            listView = new ListView();
            listView.bindingPath = "profiles";
            listView.showFoldoutHeader = false;
            listView.headerTitle = "";
            listView.showBoundCollectionSize = false;
            listView.reorderable = true;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            listView.makeItem = () =>
            {
                VisualElement root = new VisualElement();
                root.AddToClassList("forecastableItem");
                root.style.flexDirection = FlexDirection.Row;

                VisualElement icon = new VisualElement() { name = "icon" };

                Label visualLabel = new Label();
                visualLabel.name = "display-label";

                Label ioLabel = new Label();
                ioLabel.name = "io-label";

                VisualElement warning = new VisualElement() { name = "warning" };
                warning.style.height = 16;
                warning.style.width = 16;
                warning.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("Warning").image;

                root.Add(icon);
                root.Add(visualLabel);
                root.Add(ioLabel);
                root.Add(warning);
                return root;
            };

            listView.bindItem = (element, i) =>
            {
                var label = element.Q<Label>("display-label");
                var ioLabel = element.Q<Label>("io-label");
                var icon = element.Q<VisualElement>("icon");
                var warning = element.Q<VisualElement>("warning");
                warning.style.display = DisplayStyle.None;

                element.RegisterCallback<ContextClickEvent>(evt =>
                {
                    GenericMenu menu = new GenericMenu();

                    menu.AddItem(new GUIContent("Open"), false, () =>
                    {
                        selectedProfile = i;
                        UpdateSidePanel();
                        SelectWeatherProfile();
                    });
                    menu.AddItem(new GUIContent("Select Profile"), false, () =>
                    {
                        selectedProfile = i;
                        UpdateSidePanel();
                        SelectWeatherProfile();

                        Selection.activeObject = forecastProfile.profiles[selectedProfile].profile;

                    });
                    menu.AddItem(new GUIContent("Delete"), false, () =>
                    {
                        profilesArray.DeleteArrayElementAtIndex(i);
                        so.ApplyModifiedProperties();
                        UpdateSidePanel();
                        SelectWeatherProfile();
                    });

                    menu.ShowAsContext();
                });

                if (forecastProfile.profiles[i].profile)
                {
                    label.text = forecastProfile.profiles[i].profile.name;
                    icon.style.backgroundImage = forecastProfile.profiles[i].profile.icon;

                    int inCount = 0;
                    int outCount = 0;

                    for (int index = 0; index < forecastProfile.profiles.Count; index++)
                    {
                        var forecastableProfile = forecastProfile.profiles[index];

                        if (forecastableProfile.CanForecastNext(forecastProfile.profiles[i].profile))
                            inCount++;
                        if (forecastProfile.profiles[i].CanForecastNext(forecastableProfile.profile))
                            outCount++;
                    }

                    ioLabel.text = $"{inCount} > {outCount}";

                    if (outCount == 0)
                    {
                        warning.style.display = DisplayStyle.Flex;
                        warning.tooltip = "This profile is not connected to any profiles and cannot play any other weather after this profile is done. Defaulting to any profile";
                    }
                    if (inCount == 0)
                    {
                        warning.style.display = DisplayStyle.Flex;
                        warning.tooltip = "This profile is not forecast by any other profile and will never appear in a forecast. Please add rules for this profile to be forecast!";
                    }
                }
                else
                {
                    label.text = "None";
                    icon.style.backgroundImage = null;
                    ioLabel.text = "";
                }
            };

            listView.selectionChanged += (selectedItems) =>
            {
                selectedProfile = listView.selectedIndex;
                SelectWeatherProfile();
            };

            profileList.Add(listView);
            listView.Bind(so);

            VisualElement spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            profileList.Add(spacer);

            Button add = new Button();
            add.text = "Add Weather";
            add.RegisterCallback<ClickEvent>(evt =>
            {
                // int pickerID = "RowPicker".GetHashCode();

                // indexToEdit = 1;
                // EditorGUIUtility.ShowObjectPicker<ForecastableProfile>(null, false, "", pickerID);

                string[] guids = AssetDatabase.FindAssets("t:WeatherProfile");
                HashSet<WeatherProfile> foundProfiles = new HashSet<WeatherProfile>();

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    WeatherProfile asset = AssetDatabase.LoadAssetAtPath<WeatherProfile>(path);
                    if (asset != null) foundProfiles.Add(asset);
                }

                var provider = CreateInstance<WeatherProfileSearchProvider>();
                so.Update();
                provider.Init((val) =>
                {
                    profilesArray.InsertArrayElementAtIndex(profilesArray.arraySize);
                    var forecastableWeatherReference = profilesArray.GetArrayElementAtIndex(profilesArray.arraySize - 1);
                    forecastableWeatherReference.FindPropertyRelative("profile").objectReferenceValue = val;
                    forecastableWeatherReference.FindPropertyRelative("forecastNextBehavior").ClearArray();
                    so.ApplyModifiedProperties();
                    so.Update();

                    listView.Rebuild();
                    SelectWeatherProfile(profilesArray.arraySize - 1);
                }, foundProfiles, forecastProfile.ForecastableWeatherProfiles);

                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            });
            profileList.Add(add);

            IMGUIContainer pickerListener = new IMGUIContainer(() =>
            {
                if (Event.current.commandName == "ObjectSelectorCanceled")
                {
                    Debug.Log("Cancel");
                    return;
                }
                if (Event.current.commandName == "ObjectSelectorClosed" && indexToEdit != -1)
                {
                    UnityEngine.Object picked = EditorGUIUtility.GetObjectPickerObject();

                    if (picked == null) return;

                    for (int i = 0; i < profilesArray.arraySize; i++)
                        if (profilesArray.GetArrayElementAtIndex(i).FindPropertyRelative("profile").objectReferenceValue == picked) return;

                    // Apply the picked object to the correct array element
                    profilesArray.InsertArrayElementAtIndex(profilesArray.arraySize);
                    var elementProp = profilesArray.GetArrayElementAtIndex(profilesArray.arraySize - 1);
                    elementProp.FindPropertyRelative("profile").objectReferenceValue = picked;
                    elementProp.FindPropertyRelative("forecastNextBehavior").ClearArray();


                    so.ApplyModifiedProperties();
                    listView.Rebuild();
                    indexToEdit = -1;

                    SelectWeatherProfile(profilesArray.arraySize - 1);
                }
            });
            profileList.Add(pickerListener);

            listView.SetSelectionWithoutNotify(new List<int>() { selectedProfile });

        }

        public void SelectWeatherProfile(int index)
        {
            selectedProfile = index;
            listView.selectedIndex = index;
            SelectWeatherProfile();
        }

        public void SelectWeatherProfile()
        {
            content.Clear();

            VisualElement container = NewGraphPanel(0, 0);
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;


            VisualElement inProfile = DrawInContainer();
            container.Add(inProfile);

            VisualElement profileContainer = new VisualElement();
            profileContainer.style.flexDirection = FlexDirection.Row;
            profileContainer.style.justifyContent = Justify.Center;
            profileContainer.style.alignItems = Align.Center;
            container.Add(profileContainer);

            VisualElement ruleContainer = DrawRuleContainer();
            container.Add(ruleContainer);

            VisualElement outContainer = DrawOutContainer();
            container.Add(outContainer);

            SerializedProperty profilesArray = so.FindProperty("profiles");
            if (selectedProfile == -1 || selectedProfile >= profilesArray.arraySize)
                return;

            ForecastProfile.ForecastableWeather forecastable = forecastProfile.profiles[selectedProfile];
            SerializedProperty prop = profilesArray.GetArrayElementAtIndex(selectedProfile);
            SerializedProperty profileProp = prop.FindPropertyRelative("profile");

            VisualElement profilePanel = new VisualElement();
            profilePanel.AddToClassList("forecastablePanel");


            VisualElement topBorder = new VisualElement();
            topBorder.AddToClassList("forecastPanelBorderSection");
            topBorder.AddToClassList("top");
            topBorder.generateVisualContent += (MeshGenerationContext context) =>
            {
                float width = topBorder.contentRect.width;
                float height = topBorder.contentRect.height;
                float radius = 10f;

                var painter = context.painter2D;
                painter.lineWidth = 2;
                painter.strokeGradient = new Gradient()
                {
                    colorKeys = new GradientColorKey[2] {
                            new GradientColorKey(topColor, 0),
                            new GradientColorKey(middleColor, 1)
                        }
                };

                painter.BeginPath();
                painter.MoveTo(new Vector2(radius, 0));
                painter.ArcTo(new Vector2(0, 0), new Vector2(0, radius), radius);
                painter.LineTo(new Vector2(0, height));
                painter.Stroke();

                painter.BeginPath();
                painter.MoveTo(new Vector2(width - radius, 0));
                painter.ArcTo(new Vector2(width, 0), new Vector2(width, radius), radius);
                painter.LineTo(new Vector2(width, height));
                painter.Stroke();

                painter.strokeColor = topColor;
                painter.BeginPath();
                painter.MoveTo(new Vector2(width - radius, 0));
                painter.LineTo(new Vector2(radius, 0));
                painter.Stroke();
            };
            profilePanel.Add(topBorder);
            VisualElement topConnection = new VisualElement();
            topConnection.AddToClassList("forecastPanelNode");
            topConnection.AddToClassList("top");
            topConnection.style.top = 0;
            profilePanel.Add(topConnection);

            VisualElement bottomBorder = new VisualElement();
            bottomBorder.AddToClassList("forecastPanelBorderSection");
            bottomBorder.AddToClassList("bottom");
            bottomBorder.generateVisualContent += (MeshGenerationContext context) =>
                {
                    float width = topBorder.contentRect.width;
                    float height = topBorder.contentRect.height;
                    float radius = 10f;

                    var painter = context.painter2D;
                    painter.lineWidth = 2;
                    painter.strokeGradient = new Gradient()
                    {
                        colorKeys = new GradientColorKey[2] {
                            new GradientColorKey(bottomColor, 0),
                            new GradientColorKey(middleColor, 1)
                        }
                    };

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(radius, height));
                    painter.ArcTo(new Vector2(0, height), new Vector2(0, height - radius), radius);
                    painter.LineTo(new Vector2(0, 0));
                    painter.Stroke();

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(width - radius, height));
                    painter.ArcTo(new Vector2(width, height), new Vector2(width, height - radius), radius);
                    painter.LineTo(new Vector2(width, 0));
                    painter.Stroke();

                    painter.strokeColor = bottomColor;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(width - radius, height));
                    painter.LineTo(new Vector2(radius, height));
                    painter.Stroke();
                };

            profilePanel.Add(bottomBorder);
            VisualElement bottomConnection = new VisualElement();
            bottomConnection.AddToClassList("forecastPanelNode");
            bottomConnection.AddToClassList("bottom");
            bottomConnection.style.bottom = 0;
            profilePanel.Add(bottomConnection);


            VisualElement titleContainer = new VisualElement() { name = "titleContainer" };
            titleContainer.RegisterCallback<ClickEvent>(evt =>
            {
                if (forecastable.profile)
                    Selection.activeObject = forecastable.profile;
            });
            VisualElement icon = new VisualElement() { name = "icon" };
            if (forecastable.profile && forecastable.profile.icon)
                icon.style.backgroundImage = forecastable.profile.icon;
            else
                icon.style.display = DisplayStyle.None;
            Label label = new Label(forecastable.profile ? forecastable.profile.name : "Empty") { name = "weatherLabel" };
            titleContainer.Add(icon);
            titleContainer.Add(label);
            profilePanel.Add(titleContainer);
            VisualElement contentContainer = new VisualElement() { name = "contentContainer" };


            profilePanel.Add(contentContainer);

            VisualElement forecastingContainer = new VisualElement();
            forecastingContainer.AddToClassList("contentContainer");
            contentContainer.Add(forecastingContainer);

            VisualElement buttonContainer = new VisualElement();
            buttonContainer.AddToClassList("contentContainer");
            contentContainer.Add(buttonContainer);

            if (profileProp.objectReferenceValue != null)
            {
                SerializedObject targetProfileSO = new SerializedObject(profileProp.objectReferenceValue);


                SerializedProperty minTimeProp = targetProfileSO.FindProperty("minTime");
                PropertyField minTimeField = new PropertyField();
                minTimeField.BindProperty(minTimeProp);
                forecastingContainer.Add(minTimeField);

                SerializedProperty maxTimeProp = targetProfileSO.FindProperty("maxTime");
                PropertyField maxTimeField = new PropertyField();
                maxTimeField.BindProperty(maxTimeProp);
                forecastingContainer.Add(maxTimeField);


                // Button addLateralForecastButton = new Button();
                // addLateralForecastButton.RegisterCallback<ClickEvent>(evt =>
                // {
                //     targetProfileSO.FindProperty("lateralShifts").InsertArrayElementAtIndex(targetProfileSO.FindProperty("lateralShifts").arraySize);

                //     targetProfileSO.ApplyModifiedProperties();
                //     targetProfileSO.Update();
                //     SelectWeatherProfile();
                // });
                // addLateralForecastButton.text = "Add Lateral Shift Rule";
                // buttonContainer.Add(addLateralForecastButton);

            }
            else
            {
                Debug.LogWarning("The profile reference is empty!");
            }

            Button addConnectionButton = new Button();
            addConnectionButton.RegisterCallback<ClickEvent>(evt =>
            {
                prop.FindPropertyRelative("forecastNextBehavior").InsertArrayElementAtIndex(prop.FindPropertyRelative("forecastNextBehavior").arraySize);

                so.ApplyModifiedProperties();
                so.Update();
                SelectWeatherProfile();
                listView.RefreshItems();
            });
            addConnectionButton.text = "Add Forecast Next Rule";

            buttonContainer.Add(addConnectionButton);

            content.Add(container);


            profileContainer.Add(profilePanel);

            if (forecastable.profile.lateralShifts != null)
            {
                VisualElement shiftsContainer = new VisualElement();
                shiftsContainer.style.position = Position.Absolute;
                shiftsContainer.style.flexDirection = FlexDirection.Row;
                shiftsContainer.style.left = new Length(100, LengthUnit.Percent);
                shiftsContainer.style.top = new Length(50, LengthUnit.Percent);
                shiftsContainer.style.height = 150;
                shiftsContainer.style.translate = new Translate(30, new Length(-50, LengthUnit.Percent));

                profileContainer.Add(shiftsContainer);


                for (int index = 0; index < forecastable.profile.lateralShifts.Length; index++)
                {

                    LateralWeatherShift lateral = forecastable.profile.lateralShifts[index];
                    bool allowed = lateral.lateralProfile && forecastProfile.ForecastableWeatherProfiles.Contains(lateral.lateralProfile);

                    VisualElement arrow = new VisualElement();
                    arrow.style.marginLeft = 10;
                    arrow.style.marginRight = 35;
                    arrow.style.width = 30;
                    arrow.generateVisualContent += (ctx) =>
                    {
                        float width = arrow.contentRect.width;
                        float height = arrow.contentRect.height;
                        Painter2D painter = ctx.painter2D;

                        painter.lineWidth = 2;

                        if (allowed)
                        {
                            painter.strokeColor = new Color(0.3f, 0.95f, 0.56f);

                            painter.BeginPath();
                            painter.MoveTo(new Vector2(0, height / 2f));
                            painter.LineTo(new Vector2(width, height / 2f));
                            painter.Stroke();

                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width, height / 2f));
                            painter.LineTo(new Vector2(width - 8, height / 2f - 8));
                            painter.Stroke();

                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width, height / 2f));
                            painter.LineTo(new Vector2(width - 8, height / 2f + 8));
                            painter.Stroke();
                        }
                        else
                        {
                            painter.strokeColor = Color.darkOrange;

                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width / 2f - 8, height / 2f - 8));
                            painter.LineTo(new Vector2(width / 2f + 8, height / 2f + 8));
                            painter.Stroke();
                            painter.BeginPath();
                            painter.MoveTo(new Vector2(width / 2f + 8, height / 2f - 8));
                            painter.LineTo(new Vector2(width / 2f - 8, height / 2f + 8));
                            painter.Stroke();
                        }
                    };

                    VisualElement lateralPanel = new();
                    lateralPanel.AddToClassList("forecastablePanelItem");
                    lateralPanel.AddToClassList("lateralShift");

                    VisualElement lateralTitleContainer = new VisualElement();
                    lateralTitleContainer.name = "header";

                    VisualElement weatherIconContainer = new VisualElement();
                    weatherIconContainer.name = "iconContainer";
                    VisualElement weatherIcon = new VisualElement();
                    weatherIcon.name = "icon";
                    weatherIconContainer.Add(weatherIcon);
                    lateralTitleContainer.Add(weatherIconContainer);
                    Label weatherLabel = new Label();
                    lateralTitleContainer.Add(weatherLabel);

                    lateralPanel.Add(lateralTitleContainer);

                    if (allowed)
                    {
                        weatherIcon.style.backgroundImage = lateral.lateralProfile.icon;
                        weatherLabel.text = lateral.lateralProfile.name;
                    }
                    else
                    {
                        weatherIcon.style.backgroundImage = null;
                        if (!lateral.lateralProfile)
                            weatherLabel.text = "Assign a Profile";
                        else
                            weatherLabel.text = "Not in Forecast";
                        lateralPanel.style.borderBottomColor = Color.darkOrange;
                        lateralPanel.style.borderLeftColor = Color.darkOrange;
                        lateralPanel.style.borderRightColor = Color.darkOrange;
                        lateralPanel.style.borderTopColor = Color.darkOrange;
                    }


                    string conditionType = "Custom Condition";
                    string conditionValue = "Anything";

                    string conditionQualifier = lateral.changeCondition.compare switch
                    {
                        WeatherChangeCondition.CompareCondition.LessThan => "<",
                        WeatherChangeCondition.CompareCondition.EqualTo => "=",
                        WeatherChangeCondition.CompareCondition.GreaterThan => ">",
                        _ => "!="
                    };

                    switch (lateral.changeCondition.conditionType)
                    {
                        case ChanceConditionType.Temperature:
                            conditionType = "Temperature";
                            conditionValue = lateral.changeCondition.temperature.ToString();
                            break;
                        case ChanceConditionType.Humidity:
                            conditionType = "Humidity";
                            conditionValue = lateral.changeCondition.humidity.ToString();
                            break;
                        case ChanceConditionType.Pressure:
                            conditionType = "Pressure";
                            conditionValue = lateral.changeCondition.pressure.ToString();
                            break;
                        case ChanceConditionType.UVIndex:
                            conditionType = "UV Index";
                            conditionValue = lateral.changeCondition.uvIndex.ToString();
                            break;
                        case ChanceConditionType.AirQuality:
                            conditionType = "Air Quality";
                            conditionValue = lateral.changeCondition.airQuality.ToString();
                            break;
                        case ChanceConditionType.ConvectiveRainChance:
                            conditionType = "Heavy Storm Chance";
                            conditionValue = lateral.changeCondition.convectiveRainChance.ToString();
                            break;
                        case ChanceConditionType.StratiformRainChance:
                            conditionType = "Light Storm Chance";
                            conditionValue = lateral.changeCondition.stratiformRainChance.ToString();
                            break;
                        case ChanceConditionType.AccumulatedRain:
                            conditionType = "Accumulated Rain";
                            conditionValue = lateral.changeCondition.accumulatedRain.ToString();
                            break;
                        case ChanceConditionType.AccumulatedSnow:
                            conditionType = "Accumulated Snow";
                            conditionValue = lateral.changeCondition.accumulatedSnow.ToString();
                            break;
                        case ChanceConditionType.Season:
                            conditionType = "Season";
                            conditionValue = lateral.changeCondition.season.ToString();
                            break;
                        case ChanceConditionType.Time:
                            conditionType = "Time";
                            conditionValue = lateral.changeCondition.time.ToString();
                            break;
                        case ChanceConditionType.WindSpeed:
                            conditionType = "Wind";
                            conditionValue = lateral.changeCondition.windSpeed.ToString();
                            break;
                    }

                    Label lateralDescription = new Label();
                    lateralDescription.style.unityTextAlign = TextAnchor.MiddleCenter;
                    lateralDescription.style.marginLeft = 5;
                    lateralDescription.style.marginRight = 5;
                    lateralDescription.text = $"{conditionType} {conditionQualifier} {conditionValue}";
                    lateralPanel.Add(lateralDescription);


                    shiftsContainer.Add(arrow);
                    shiftsContainer.Add(lateralPanel);
                }
            }

            Dictionary<ForecastProfile.ForecastNext, List<VisualElement>> connectedRules = new Dictionary<ForecastProfile.ForecastNext, List<VisualElement>>();

            for (int index = 0; index < forecastProfile.profiles.Count; index++)
            {
                var forecastableProfile = forecastProfile.profiles[index];

                if (forecastableProfile.CanForecastNext(forecastable.profile, out ForecastProfile.ForecastNext inRule))
                {
                    VisualElement element = DrawInProfile(out VisualElement port, index, inRule);
                    DrawConnectors(port, topConnection, forecastableProfile.profile == forecastable.profile ? repeatColor : topColor, topColor);
                    inProfile.Add(element);
                }

                if (forecastable.CanForecastNext(forecastableProfile.profile, out ForecastProfile.ForecastNext outRule))
                {
                    VisualElement element = DrawOutProfile(out VisualElement port, index, outRule);
                    if (!connectedRules.ContainsKey(outRule) || connectedRules[outRule] == null)
                        connectedRules[outRule] = new List<VisualElement>();

                    connectedRules[outRule].Add(port);

                    outContainer.Add(element);
                }
            }

            for (int index = 0; index < forecastProfile.profiles[selectedProfile].forecastNextBehavior.Length; index++)
            {
                ForecastProfile.ForecastNext rule = forecastProfile.profiles[selectedProfile].forecastNextBehavior[index];
                SerializedProperty transitionProperty = prop.FindPropertyRelative("forecastNextBehavior");
                VisualElement element = DrawRuleProfile(out VisualElement topPort, out VisualElement bottomPort, index, rule, transitionProperty);
                DrawConnectors(bottomConnection, topPort, bottomColor, rule.modifierType == ForecastProfile.ForecastNext.ForecastNextModifierType.ForceExclude ? excludeColor : bottomColor);

                if (connectedRules.ContainsKey(rule))
                {
                    foreach (var c in connectedRules[rule])
                        DrawConnectors(bottomPort, c, bottomColor, c.parent.ClassListContains("repeat") ? repeatColor : bottomColor);
                }

                ruleContainer.Add(element);
            }


            UpdateWeatherProfileInspector();
            if (!_hasFramedInitial)
            {
                content.schedule.Execute(() =>
                {
                    FrameContent();
                    _hasFramedInitial = true;
                }).ExecuteLater(50);
            }
            // content.schedule.Execute(FrameContent).ExecuteLater(50);
        }

        public VisualElement DrawInProfile(out VisualElement port, int index, ForecastProfile.ForecastNext rule)
        {
            var forecastableProfile = forecastProfile.profiles[index];

            VisualElement panel = new VisualElement();
            panel.AddToClassList("forecastablePanelItem");
            panel.AddToClassList("inProfile");


            port = new VisualElement();
            port.AddToClassList("forecastPanelNode");
            port.AddToClassList("top");
            port.style.bottom = -6;
            panel.RegisterCallback<ClickEvent>(evt =>
            {
                SelectWeatherProfile(index);
            });
            panel.Add(port);

            VisualElement header = new VisualElement();
            header.name = "header";
            panel.Add(header);
            VisualElement content = new VisualElement();
            content.name = "content";
            panel.Add(content);


            VisualElement denoterContainer = new VisualElement();
            denoterContainer.name = "denoterContainer";
            panel.Add(denoterContainer);

            VisualElement denoter = new VisualElement();
            denoter.name = "denoter";
            denoterContainer.Add(denoter);

            VisualElement repeatWarning = new VisualElement();
            if (forecastProfile.profiles[index].profile == forecastProfile.profiles[selectedProfile].profile)
            {
                panel.AddToClassList("repeat");
                repeatWarning.style.display = DisplayStyle.Flex;
            }

            repeatWarning.name = "repeatWarning";
            repeatWarning.tooltip = "This profile can forecast itself";
            denoter.Add(repeatWarning);

            VisualElement islandWarning = new VisualElement();
            islandWarning.name = "islandWarning";
            denoter.Add(islandWarning);

            VisualElement weatherIconContainer = new VisualElement();
            weatherIconContainer.name = "iconContainer";
            VisualElement weatherIcon = new VisualElement();
            weatherIcon.style.backgroundImage = forecastableProfile.profile.icon;
            weatherIcon.name = "icon";
            weatherIconContainer.Add(weatherIcon);
            header.Add(weatherIconContainer);

            Label weatherLabel = new Label();
            weatherLabel.text = forecastableProfile.profile.name;
            header.Add(weatherLabel);

            Label ruleLabel = new Label();
            switch (rule.method)
            {
                case ForecastProfile.ForecastNext.ForecastNextMethod.Any:
                    ruleLabel.text = "Any";
                    break;
                case ForecastProfile.ForecastNext.ForecastNextMethod.ByTag:
                    ruleLabel.text = "By Tag";
                    break;
                case ForecastProfile.ForecastNext.ForecastNextMethod.ByProfile:
                    ruleLabel.text = "By Profile";
                    break;
            }

            content.Add(ruleLabel);

            Label chanceLabel = new Label();
            chanceLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            chanceLabel.style.marginTop = 4;
            chanceLabel.text = "30%";

            // content.Add(chanceLabel);

            return panel;
        }

        public void FrameContent()
        {
            if (viewport == null || float.IsNaN(viewport.layout.width) || viewport.layout.width <= 0) return;

            Rect bounds = new Rect();
            bool hasBounds = false;

            // Recursive function to find the true bounding box of all graph nodes
            void Traverse(VisualElement ve)
            {
                // Skip your connector lines, as they are absolutely positioned to stretch 
                // across the container and will completely break the bounds calculation.
                if (ve.pickingMode == PickingMode.Ignore && ve.parent == content) return;

                // If this element has a valid layout, include it in the bounds calculation
                if (!float.IsNaN(ve.layout.width) && ve.layout.width > 0 && ve.layout.height > 0)
                {
                    // Get the element's Rect relative to the 'content' container
                    Rect rect = new Rect(ve.ChangeCoordinatesTo(content, Vector2.zero), ve.layout.size);

                    if (!hasBounds)
                    {
                        bounds = rect;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.xMin = Mathf.Min(bounds.xMin, rect.xMin);
                        bounds.yMin = Mathf.Min(bounds.yMin, rect.yMin);
                        bounds.xMax = Mathf.Max(bounds.xMax, rect.xMax);
                        bounds.yMax = Mathf.Max(bounds.yMax, rect.yMax);
                    }
                }

                foreach (var child in ve.Children())
                    Traverse(child);
            }

            // Measure all children inside the content container
            foreach (var child in content.Children())
                Traverse(child);

            if (!hasBounds) return;

            // 1. Add padding so nodes don't sit completely flush against the edge of the window
            float padding = 80f;
            bounds.xMin -= padding;
            bounds.yMin -= padding;
            bounds.xMax += padding;
            bounds.yMax += padding;

            // 2. Calculate the zoom scale required to fit the bounds inside the viewport
            float scaleX = viewport.layout.width / bounds.width;
            float scaleY = viewport.layout.height / bounds.height;

            // Use the smallest scale to ensure everything fits. Clamp to a max of 1.0f 
            // so we don't awkwardly zoom in too close on small graphs.
            float newScale = Mathf.Clamp(Mathf.Min(scaleX, scaleY), 0.2f, 1.0f);
            content.style.scale = new Scale(new Vector2(newScale, newScale));

            // 3. Calculate translation to align the scaled bounds center with the viewport center
            Vector2 viewportCenter = new Vector2(viewport.layout.width / 2f, viewport.layout.height / 2f);
            Vector2 scaledBoundsCenter = bounds.center * newScale;

            Vector2 newTranslate = viewportCenter - scaledBoundsCenter;
            content.style.translate = new Translate(newTranslate.x, newTranslate.y);
        }

        public VisualElement DrawInContainer()
        {
            VisualElement panel = new VisualElement();
            // panel.style.position = Position.Absolute;
            // panel.style.left = 0;
            // panel.style.bottom = 150;
            // panel.style.transformOrigin = new TransformOrigin(50, 50, 0);
            // panel.style.transformOrigin = new TransformOrigin(new Length(50, LengthUnit.Percent), new Length(100, LengthUnit.Percent), 0);
            panel.AddToClassList("forecastInContainer");
            panel.generateVisualContent += (MeshGenerationContext context) =>
                {
                    float width = panel.contentRect.width;
                    float height = panel.contentRect.height;
                    float radius = 10f;

                    var painter = context.painter2D;
                    painter.lineWidth = 2;
                    painter.strokeColor = Color.gray2;
                    painter.SetDashPattern(10, 20);

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(radius, 0));
                    painter.ArcTo(new Vector2(0, 0), new Vector2(0, radius), radius);
                    painter.LineTo(new Vector2(0, height - radius));
                    painter.ArcTo(new Vector2(0, height), new Vector2(radius, height), radius);
                    painter.LineTo(new Vector2(width - radius, height));
                    painter.ArcTo(new Vector2(width, height), new Vector2(width, height - radius), radius);
                    painter.LineTo(new Vector2(width, radius));
                    painter.ArcTo(new Vector2(width, 0), new Vector2(width - radius, 0), radius);
                    painter.LineTo(new Vector2(radius, 0));
                    painter.Stroke();

                };


            return panel;
        }

        public VisualElement DrawOutProfile(out VisualElement port, int index, ForecastProfile.ForecastNext rule)
        {
            var forecastableProfile = forecastProfile.profiles[index];

            VisualElement panel = new VisualElement();
            panel.AddToClassList("forecastablePanelItem");
            panel.AddToClassList("outProfile");


            port = new VisualElement();
            port.AddToClassList("forecastPanelNode");
            port.AddToClassList("bottom");
            port.style.top = 0;
            panel.RegisterCallback<ClickEvent>(evt =>
            {
                SelectWeatherProfile(index);
            });
            panel.Add(port);

            VisualElement header = new VisualElement();
            header.name = "header";
            panel.Add(header);
            VisualElement content = new VisualElement();
            content.name = "content";
            panel.Add(content);


            VisualElement denoterContainer = new VisualElement();
            denoterContainer.name = "denoterContainer";
            panel.Add(denoterContainer);

            VisualElement denoter = new VisualElement();
            denoter.name = "denoter";
            denoterContainer.Add(denoter);

            VisualElement repeatWarning = new VisualElement();
            if (forecastProfile.profiles[index].profile == forecastProfile.profiles[selectedProfile].profile)
            {
                panel.AddToClassList("repeat");
                repeatWarning.style.display = DisplayStyle.Flex;
            }

            repeatWarning.name = "repeatWarning";
            repeatWarning.tooltip = "This profile can forecast itself";
            denoter.Add(repeatWarning);

            VisualElement islandWarning = new VisualElement();
            islandWarning.name = "islandWarning";
            denoter.Add(islandWarning);

            VisualElement weatherIconContainer = new VisualElement();
            weatherIconContainer.name = "iconContainer";
            VisualElement weatherIcon = new VisualElement();
            weatherIcon.style.backgroundImage = forecastableProfile.profile.icon;
            weatherIcon.name = "icon";
            weatherIconContainer.Add(weatherIcon);
            header.Add(weatherIconContainer);

            Label weatherLabel = new Label();
            weatherLabel.text = forecastableProfile.profile.name;
            header.Add(weatherLabel);

            Label ruleLabel = new Label();
            switch (rule.method)
            {
                case ForecastProfile.ForecastNext.ForecastNextMethod.Any:
                    ruleLabel.text = "Any Profile";
                    break;
                case ForecastProfile.ForecastNext.ForecastNextMethod.ByTag:
                    ruleLabel.text = "Tag";
                    break;
                case ForecastProfile.ForecastNext.ForecastNextMethod.ByProfile:
                    ruleLabel.text = "Profile";
                    break;
            }
            content.Add(ruleLabel);

            return panel;
        }

        public VisualElement DrawOutContainer()
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("forecastOutContainer");
            panel.generateVisualContent += (MeshGenerationContext context) =>
                {
                    float width = panel.contentRect.width;
                    float height = panel.contentRect.height;
                    float radius = 10f;

                    var painter = context.painter2D;
                    painter.lineWidth = 2;
                    painter.strokeColor = Color.gray2;
                    painter.SetDashPattern(10, 20);

                    painter.BeginPath();
                    painter.MoveTo(new Vector2(radius, 0));
                    painter.ArcTo(new Vector2(0, 0), new Vector2(0, radius), radius);
                    painter.LineTo(new Vector2(0, height - radius));
                    painter.ArcTo(new Vector2(0, height), new Vector2(radius, height), radius);
                    painter.LineTo(new Vector2(width - radius, height));
                    painter.ArcTo(new Vector2(width, height), new Vector2(width, height - radius), radius);
                    painter.LineTo(new Vector2(width, radius));
                    painter.ArcTo(new Vector2(width, 0), new Vector2(width - radius, 0), radius);
                    painter.LineTo(new Vector2(radius, 0));
                    painter.Stroke();

                };


            return panel;
        }

        public VisualElement DrawRuleProfile(out VisualElement topPort, out VisualElement bottomPort, int index, ForecastProfile.ForecastNext rule, SerializedProperty property)
        {
            VisualElement panel = new VisualElement();
            panel.AddToClassList("forecastablePanelItem");
            panel.AddToClassList("outProfile");
            panel.style.width = 200;

            topPort = new VisualElement();
            topPort.AddToClassList("forecastPanelNode");
            topPort.AddToClassList("bottom");
            topPort.style.top = 0;
            panel.Add(topPort);

            bottomPort = new VisualElement();
            bottomPort.AddToClassList("forecastPanelNode");
            bottomPort.AddToClassList("bottom");
            bottomPort.style.bottom = 0;

            if (rule.modifierType == ForecastProfile.ForecastNext.ForecastNextModifierType.ForceExclude)
            {
                panel.style.borderBottomColor = excludeColor;
                panel.style.borderTopColor = excludeColor;
                panel.style.borderLeftColor = excludeColor;
                panel.style.borderRightColor = excludeColor;

                topPort.style.borderBottomColor = excludeColor;
                topPort.style.borderTopColor = excludeColor;
                topPort.style.borderLeftColor = excludeColor;
                topPort.style.borderRightColor = excludeColor;
            }
            else
            {
                panel.Add(bottomPort);
            }

            VisualElement header = new VisualElement();
            header.name = "header";
            panel.Add(header);
            VisualElement content = new VisualElement();
            content.name = "content";
            panel.Add(content);

            SerializedProperty prop = property.GetArrayElementAtIndex(index);

            Button removeConnectionButton = new Button();
            removeConnectionButton.text = "Remove Rule";
            removeConnectionButton.style.marginBottom = 5;
            removeConnectionButton.RegisterCallback<ClickEvent>(evt =>
            {
                property.DeleteArrayElementAtIndex(index);
                property.serializedObject.ApplyModifiedProperties();
                property.serializedObject.Update();
                SelectWeatherProfile();
                listView.RefreshItems();
            });
            header.Add(removeConnectionButton);


            PropertyField modifierType = new PropertyField();
            modifierType.BindProperty(prop.FindPropertyRelative("modifierType"));
            TrackChangeAndRebuild(modifierType, prop.FindPropertyRelative("modifierType"));
            modifierType.label = "";
            content.Add(modifierType);

            PropertyField chanceMultiplier = new PropertyField();
            if (prop.FindPropertyRelative("modifierType").intValue != (int)ForecastProfile.ForecastNext.ForecastNextModifierType.ModifiedChance)
                chanceMultiplier.style.display = DisplayStyle.None;
            chanceMultiplier.BindProperty(prop.FindPropertyRelative("chanceMultiplier"));
            TrackChangeAndRebuild(chanceMultiplier, prop.FindPropertyRelative("chanceMultiplier"));
            chanceMultiplier.label = "";
            content.Add(chanceMultiplier);

            PropertyField method = new PropertyField();
            method.BindProperty(prop.FindPropertyRelative("method"));
            TrackChangeAndRebuild(method, prop.FindPropertyRelative("method"));
            method.label = "";
            content.Add(method);

            PropertyField tags = new PropertyField();
            if (prop.FindPropertyRelative("method").intValue != (int)ForecastProfile.ForecastNext.ForecastNextMethod.ByTag)
                tags.style.display = DisplayStyle.None;
            tags.BindProperty(prop.FindPropertyRelative("tag"));
            TrackChangeAndRebuild(tags, prop.FindPropertyRelative("tag"));
            tags.label = "";
            content.Add(tags);

            SerializedProperty profilesProperty = prop.FindPropertyRelative("profiles");

            ListView profilesListView = new ListView();
            if (prop.FindPropertyRelative("method").intValue != (int)ForecastProfile.ForecastNext.ForecastNextMethod.ByProfile)
                profilesListView.style.display = DisplayStyle.None;
            profilesListView.showFoldoutHeader = false;
            profilesListView.showAddRemoveFooter = true;
            profilesListView.showBoundCollectionSize = false;
            profilesListView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            profilesListView.reorderable = true;
            profilesListView.showBorder = true;
            profilesListView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;

            profilesListView.makeItem = () =>
            {
                ObjectField objField = new ObjectField();
                objField.objectType = typeof(WeatherProfile);
                objField.RegisterValueChangedCallback(evt =>
                {
                    so.ApplyModifiedProperties();
                    so.Update();
                    UpdateSidePanel();
                });
                return objField;
            };

            // 2. Define how the UI binds data to that row
            profilesListView.bindItem = (element, index) =>
            {
                ObjectField objField = (ObjectField)element;
                objField.style.marginLeft = 10;
                objField.style.marginRight = 10;
                objField.BindProperty(profilesProperty.GetArrayElementAtIndex(index));
            };

            profilesListView.BindProperty(profilesProperty);
            content.Add(profilesListView);

            void OpenSearch()
            {
                var provider = CreateInstance<WeatherProfileSearchProvider>();
                so.Update();
                provider.Init((val) =>
                {
                    profilesProperty.InsertArrayElementAtIndex(profilesProperty.arraySize);
                    profilesProperty.GetArrayElementAtIndex(profilesProperty.arraySize - 1).objectReferenceValue = val;
                    so.ApplyModifiedProperties();
                    so.Update();
                    SelectWeatherProfile();
                }, forecastProfile.ForecastableWeatherProfiles, rule.profiles.ToHashSet());

                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), provider);
            }

            profilesListView.onAdd = (evt) =>
            {
                OpenSearch();
            };

            profilesListView.itemsAdded += (evt) =>
            {
                so.ApplyModifiedProperties();
                so.Update();
                UpdateSidePanel();
            };
            profilesListView.itemsRemoved += (evt) =>
            {
                so.ApplyModifiedProperties();
                so.Update();
                UpdateSidePanel();
                profilesListView.schedule.Execute(() =>
                {
                    SelectWeatherProfile();
                }).ExecuteLater(10);
            };

            // void ScheduleUpdateCallback(PropertyField e)
            // {
            //     e.schedule.Execute(() =>
            //     {
            //         e.RegisterValueChangeCallback(evt =>
            //         {
            //             listView.RefreshItems();
            //             SelectWeatherProfile();
            //         });
            //     }).ExecuteLater(10);
            // }

            void TrackChangeAndRebuild(VisualElement element, SerializedProperty propertyToTrack)
            {
                element.TrackPropertyValue(propertyToTrack, sp =>
                {
                    listView.RefreshItems();
                    SelectWeatherProfile();
                });
            }

            return panel;
        }

        public VisualElement DrawRuleContainer()
        {
            VisualElement panel = new VisualElement();
            // panel.style.position = Position.Absolute;
            // panel.style.left = 0;
            // panel.style.top = 200;
            // panel.style.transformOrigin = new TransformOrigin(50, 50, 0);
            // panel.style.transformOrigin = new TransformOrigin(new Length(50, LengthUnit.Percent), new Length(100, LengthUnit.Percent), 0);
            panel.AddToClassList("forecastRuleContainer");
            return panel;
        }

        public void DrawConnectors(VisualElement start, VisualElement end, Color startColor, Color endColor)
        {
            // 1. Create a dedicated visual element to hold this specific connection line
            VisualElement connector = new VisualElement();

            // Stretch it across the entire content container so the line isn't clipped
            connector.style.position = Position.Absolute;
            connector.style.top = 0;
            connector.style.left = 0;
            connector.style.right = 0;
            connector.style.bottom = 0;

            // Ensure the line doesn't block mouse clicks for your GraphNavigator
            connector.pickingMode = PickingMode.Ignore;

            // 2. Draw the curve using Unity's 2D Painter API
            connector.generateVisualContent += (MeshGenerationContext context) =>
            {
                // Calculate the center point of the start and end ports
                // We use ChangeCoordinatesTo to translate the local port center into the 'content' space
                Vector2 startPos = start.ChangeCoordinatesTo(content, new Vector2(start.resolvedStyle.width / 2f, start.resolvedStyle.height / 2f));
                Vector2 endPos = end.ChangeCoordinatesTo(content, new Vector2(end.resolvedStyle.width / 2f, end.resolvedStyle.height / 2f));

                var painter = context.painter2D;
                painter.lineWidth = 2f;
                painter.lineCap = LineCap.Round;

                // Apply a gradient between the two port colors
                painter.strokeGradient = new Gradient()
                {
                    colorKeys = new GradientColorKey[] {
                new GradientColorKey(startColor * 0.5f, 0f),
                new GradientColorKey(endColor * 0.5f, 1f)
                    }
                };

                // Calculate vertical Bezier tangents to make the curve flow naturally from top to bottom
                // UI Toolkit Y-axis is inverted (0 is at the top), so +Y goes down, -Y goes up.
                float distance = Mathf.Abs(startPos.y - endPos.y);
                float tangentLength = Mathf.Max(distance * 0.5f, 70); // Prevents the curve from pinching when nodes are close

                Vector2 startTangent = startPos + Vector2.up * tangentLength;  // Pushes curve down from the out-port
                Vector2 endTangent = endPos + Vector2.down * tangentLength;    // Pushes curve up from the in-port

                // Draw the bezier curve
                painter.BeginPath();
                painter.MoveTo(startPos);
                painter.BezierCurveTo(startTangent, endTangent, endPos);
                painter.Stroke();
            };

            // 3. Add the connector to the content pane (insert at index 0 so it renders behind your nodes)
            content.Insert(0, connector);

            // 4. Force the line to redraw whenever the ports move or finish their initial layout
            start.RegisterCallback<GeometryChangedEvent>(e => connector.MarkDirtyRepaint());
            end.RegisterCallback<GeometryChangedEvent>(e => connector.MarkDirtyRepaint());
        }

    }

    public class WeatherProfileSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        Action<WeatherProfile> action;
        public HashSet<WeatherProfile> include;
        public HashSet<WeatherProfile> exclude;
        SerializedProperty property;

        public void Init(Action<WeatherProfile> action, HashSet<WeatherProfile> include, HashSet<WeatherProfile> exclude)
        {
            this.action = action;
            this.include = include;
            this.exclude = exclude;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var tree = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("Select a Profile"), 0),
            };

            foreach (var profile in include)
            {
                bool contains = false;
                foreach (var excludedProfile in exclude)
                    if (excludedProfile == profile)
                    {
                        contains = true;
                        break;
                    }
                if (contains) continue;

                tree.Add(new SearchTreeEntry(new GUIContent(profile.name, profile.icon)) { level = 1, userData = profile });
            }

            return tree;
        }

        public bool OnSelectEntry(SearchTreeEntry entry, SearchWindowContext context)
        {
            action.Invoke((WeatherProfile)entry.userData);
            return true;
        }
    }

}