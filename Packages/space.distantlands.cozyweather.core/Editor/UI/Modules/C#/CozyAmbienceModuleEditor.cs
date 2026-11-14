//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System;
using System.Reflection;
using System.Collections.Generic;

#if UNITY_6000_5_OR_NEWER
using Unity.Scripting.LifecycleManagement;
#endif

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyAmbienceModule))]
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    public class CozyAmbienceModuleEditor : CozyBiomeModuleEditor
    {
        CozyAmbienceModule ambienceModule;

        static readonly Color[] WeightPalette = new Color[]
        {
            Branding.blue,
            Branding.orange,
            Branding.green,
            Branding.purple,
            Branding.yellow,
            Branding.red,
            Branding.deepBlue
        };

        VisualElement root;
        VisualElement ambienceWeightsContainer;
        VisualElement currentElementSection;
        string lastWeightsSignature;

        void OnEnable()
        {
            if (!target)
                return;

            ambienceModule = (CozyAmbienceModule)target;
        }

        public override ModuleWidget[] ModuleWidgets => new ModuleWidget[]
        {
            new(
                ModuleWidget.WidgetSize.Small, "Current Ambience", true,
                () => {
                    VisualElement container = new();
                    container.style.alignItems = Align.Center;
                    container.style.justifyContent = Justify.Center;

                    container.RegisterCallback<ClickEvent>(evt =>
                    {
                        if (ambienceModule.PossibleProfiles == null || ambienceModule.PossibleProfiles.Length == 0)
                        {
                            Debug.Log("Add Ambience Profiles to the module first.");
                            return;
                        }

                        GenericMenu menu = new GenericMenu();
                        bool hasCurrentAmbience = ambienceModule.CurrentProfile;

                        foreach (AmbienceProfile profile in ambienceModule.PossibleProfiles)
                        {
                            if (profile == null) continue;
                            menu.AddItem(new GUIContent(profile.name), hasCurrentAmbience & profile == ambienceModule.CurrentProfile, () =>
                            {
                                ambienceModule.SetProfile(profile);
                                container.Q<VisualElement>("icon").style.backgroundImage = Resources.Load<Texture2D>("COZY Icons/Ambience");
                                container.Q<Label>("label").text = ambienceModule.CurrentProfile.name;
                            });
                        }

                        menu.ShowAsContext();
                    });

                    VisualElement icon = new();
                    icon.name = "icon";
                    icon.style.width = 48;
                    icon.style.height = 48;
                    icon.style.marginTop = 5;
                    icon.style.marginBottom = 7;
                    icon.style.backgroundImage = Resources.Load<Texture2D>("COZY Icons/Ambience");
                    container.Add(icon);

                    Label label = new Label();
                    label.name = "label";
                    label.style.unityFontStyleAndWeight = FontStyle.Bold;
                    if (ambienceModule.CurrentProfile != null)
                        label.text = ambienceModule.CurrentProfile.name;
                    else
                        label.text = "No Ambience";
                    container.Add(label);

                    return container;
                },
                (widget) =>
                {
                    if (ambienceModule.CurrentProfile == null)
                    {
                        widget.container.Q<Label>("label").text = "No Ambience";
                        return;
                    }
                    widget.container.Q<Label>("label").text = ambienceModule.CurrentProfile.name;
                }
            ),
        };

        public override VisualElement DisplayUI()
        {
            lastWeightsSignature = null;
            root = new VisualElement();
            ambienceWeightsContainer = new VisualElement();

            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/space.distantlands.cozyweather.core/Editor/UI/Modules/UXML/ambience-module-editor.uxml"
            );

            if (asset != null)
            {
                asset.CloneTree(root);
            }

            VisualElement currentProfileContainer = new VisualElement();
            currentProfileContainer.style.display = DisplayStyle.None;
            root.Add(currentProfileContainer);

            Label currentProfileLabel = new Label("Current Profile");
            currentProfileLabel.AddToClassList("h1");
            currentProfileContainer.Add(currentProfileLabel);

            currentElementSection = new VisualElement();
            currentElementSection.AddToClassList("section-bg");
            currentElementSection.style.flexDirection = FlexDirection.Row;
            currentElementSection.style.alignItems = Align.Center;
            currentProfileContainer.Add(currentElementSection);

            VisualElement progressTracker = new VisualElement();
            progressTracker.name = "progressTracker";
            progressTracker.style.width = 24;
            progressTracker.style.height = 24;
            progressTracker.generateVisualContent += ctx =>
            {
                var painter = ctx.painter2D;
                float width = progressTracker.contentRect.width;
                float height = progressTracker.contentRect.height;
                Vector2 center = progressTracker.contentRect.center;
                float radius = width * 0.45f;
                painter.strokeColor = Branding.orange;

                painter.lineWidth = 4f;
                painter.BeginPath();
                painter.Arc(center, radius, 0, 360 * ambienceModule.ProfilePercentageRemaining, ArcDirection.Clockwise);
                painter.Stroke();
            };

            currentElementSection.Add(progressTracker);

            Label currentProfileName = new Label();
            currentProfileName.name = "currentProfileName";
            currentProfileName.style.marginLeft = 6;
            currentElementSection.Add(currentProfileName);

            currentProfileContainer.RegisterCallback<ClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();

                foreach (AmbienceProfile profile in ambienceModule.PossibleProfiles)
                {
                    menu.AddItem(new GUIContent(profile.name), ambienceModule.CurrentProfile && profile == ambienceModule.CurrentProfile, () =>
                    {
                        ambienceModule.SetProfile(profile);
                        currentElementSection.Q("progressTracker").MarkDirtyRepaint();
                        currentElementSection.Q<Label>("currentProfileName").text = profile.name;
                    });
                }

                menu.ShowAsContext();
            });

            Label possibleProfilesLabel = new Label("Ambience Profiles & Distribution");
            possibleProfilesLabel.AddToClassList("h1");
            root.Add(possibleProfilesLabel);

            VisualElement profilesListContainer = new VisualElement();
            profilesListContainer.AddToClassList("section-bg");
            root.Add(profilesListContainer);

            PropertyField possibleProfilesField = new PropertyField();
            possibleProfilesField.BindProperty(serializedObject.FindProperty("possibleProfiles"));
            profilesListContainer.Add(possibleProfilesField);

            ambienceWeightsContainer.name = "ambience-weights";
            ambienceWeightsContainer.AddToClassList("section-bg");
            ambienceWeightsContainer.style.marginTop = 10;
            root.Add(ambienceWeightsContainer);
            UpdateAmbienceWeightsSection();

            Label modeLabel = new Label("Mode Controls");
            modeLabel.AddToClassList("h1");
            root.Add(modeLabel);

            VisualElement selectionContainer = new VisualElement();
            selectionContainer.AddToClassList("section-bg");
            root.Add(selectionContainer);

            PropertyField ambienceChangeMode = new PropertyField();
            ambienceChangeMode.BindProperty(serializedObject.FindProperty("ambienceChangeMode"));
            selectionContainer.Add(ambienceChangeMode);

            PropertyField defaultProfile = new PropertyField();
            defaultProfile.BindProperty(serializedObject.FindProperty("defaultProfile"));
            selectionContainer.Add(defaultProfile);

            PropertyField allowRepeats = new PropertyField();
            allowRepeats.BindProperty(serializedObject.FindProperty("allowRepeats"));
            selectionContainer.Add(allowRepeats);

            root.schedule.Execute(() =>
            {
                currentProfileContainer.style.display = Application.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
                if (!Application.isPlaying || ambienceModule == null) return;

                currentElementSection.Q("progressTracker").MarkDirtyRepaint();
                if (ambienceModule.CurrentProfile)
                    currentElementSection.Q<Label>("currentProfileName").text = ambienceModule.CurrentProfile.name;

                if (ComputeWeightsSignature() != lastWeightsSignature)
                    UpdateAmbienceWeightsSection();
            }).Every(500);

            return root;
        }

        string ComputeWeightsSignature()
        {
            if (ambienceModule == null || ambienceModule.PossibleProfiles == null) return string.Empty;

            var parts = new List<string>();
            ConditionSnapshot snapshot = ConditionSnapshot.GetConditions(ambienceModule.WeatherSphere);
            foreach (var profile in ambienceModule.PossibleProfiles)
            {
                if (profile == null) continue;
                parts.Add($"{profile.name}:{profile.GetChance(snapshot):0.###}");
            }
            return string.Join("|", parts);
        }

        void UpdateAmbienceWeightsSection()
        {
            if (ambienceWeightsContainer == null) return;
            ambienceWeightsContainer.Clear();
            lastWeightsSignature = ComputeWeightsSignature();

            if (ambienceModule == null || ambienceModule.PossibleProfiles == null || ambienceModule.PossibleProfiles.Length == 0)
                return;

            ConditionSnapshot snapshot = ConditionSnapshot.GetConditions(ambienceModule.WeatherSphere);
            var entries = new List<(AmbienceProfile profile, float weight)>();

            foreach (var profile in ambienceModule.PossibleProfiles)
            {
                if (profile == null) continue;
                float chance = profile.GetChance(snapshot);
                entries.Add((profile, Mathf.Max(0f, chance)));
            }

            if (entries.Count == 0)
            {
                Label label = new Label("No ambience profiles available.");
                ambienceWeightsContainer.Add(label);
                return;
            }

            entries = entries.OrderByDescending(e => e.weight).ToList();
            float total = entries.Sum(e => e.weight);

            var colorIndex = entries
                .Select(e => e.profile)
                .OrderBy(p => p.name, StringComparer.Ordinal)
                .Select((p, i) => (p, i))
                .ToDictionary(x => x.p, x => WeightPalette[x.i % WeightPalette.Length]);

            VisualElement bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.height = 12;
            bar.style.marginTop = 2;
            bar.style.marginBottom = 8;
            bar.style.overflow = Overflow.Hidden;
            bar.style.backgroundColor = new Color(0f, 0f, 0f, 0.3f);
            bar.style.borderBottomLeftRadius = 6;
            bar.style.borderBottomRightRadius = 6;
            bar.style.borderTopLeftRadius = 6;
            bar.style.borderTopRightRadius = 6;
            ambienceWeightsContainer.Add(bar);

            foreach (var (profile, weight) in entries)
            {
                if (weight <= 0.05f) continue;

                float pct = total > 0f ? weight / total : 0f;
                VisualElement segment = new();
                segment.style.flexGrow = weight;
                segment.style.flexBasis = 0;
                segment.style.backgroundColor = colorIndex[profile];
                segment.style.borderBottomWidth = 2;
                segment.style.borderBottomColor = ambienceModule.CurrentProfile && profile == ambienceModule.CurrentProfile ? Color.white : Color.clear;
                segment.tooltip = $"{profile.name}: {pct * 100f:0.#}%";
                bar.Add(segment);
            }

            foreach (var (profile, weight) in entries)
            {
                if (weight <= 0.05f) continue;

                float pct = total > 0f ? weight / total : 0f;
                Color color = colorIndex[profile];

                VisualElement row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 3;
                row.style.paddingBottom = 3;
                if (Application.isPlaying)
                    row.style.opacity = profile == ambienceModule.CurrentProfile ? 1f : 0.45f;
                row.tooltip = $"Calculated weight: {weight:0.##}";

                row.RegisterCallback<ClickEvent>(evt =>
                {
                    Selection.activeObject = profile;
                });

                VisualElement swatch = new VisualElement();
                swatch.style.width = 8;
                swatch.style.height = 8;
                swatch.style.marginRight = 6;
                swatch.style.backgroundColor = color;
                swatch.style.borderBottomLeftRadius = 4;
                swatch.style.borderBottomRightRadius = 4;
                swatch.style.borderTopLeftRadius = 4;
                swatch.style.borderTopRightRadius = 4;
                row.Add(swatch);

                Label profileName = new Label(profile.name);
                profileName.style.flexGrow = 1;
                row.Add(profileName);

                VisualElement track = new VisualElement();
                track.style.width = 80;
                track.style.height = 4;
                track.style.marginLeft = 8;
                track.style.marginRight = 8;
                track.style.backgroundColor = new Color(0f, 0f, 0f, 0.35f);
                track.style.borderBottomLeftRadius = 2;
                track.style.borderBottomRightRadius = 2;
                track.style.borderTopLeftRadius = 2;
                track.style.borderTopRightRadius = 2;

                VisualElement fill = new VisualElement();
                fill.style.height = Length.Percent(100);
                fill.style.width = Length.Percent(pct * 100f);
                fill.style.backgroundColor = color;
                fill.style.borderBottomLeftRadius = 2;
                fill.style.borderBottomRightRadius = 2;
                fill.style.borderTopLeftRadius = 2;
                fill.style.borderTopRightRadius = 2;
                track.Add(fill);
                row.Add(track);

                Label percent = new Label($"{pct * 100f:0.#}%");
                percent.style.minWidth = 44;
                percent.style.unityTextAlign = TextAnchor.MiddleRight;
                row.Add(percent);

                ambienceWeightsContainer.Add(row);
            }
        }

        public override void OpenDocumentationURL()
        {
            Application.OpenURL("https://distant-lands.gitbook.io/cozy-weather-4/using-cozy/modules/ambience-module");
        }
    }
}