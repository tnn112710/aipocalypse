using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using Unity.Scripting.LifecycleManagement;
using UnityEditor.UIElements;
using System;

namespace DistantLands.Cozy.EditorScripts
{
#if UNITY_6000_5_OR_NEWER
    [NoAutoStaticsCleanup]
#endif
    [CustomEditor(typeof(CozyResoundModule))]
    public class CozyResoundModuleEditor : CozyBiomeModuleEditor
    {
        VisualElement root;
        VisualElement recordContainer;
        CozyResoundModule module;

        Gradient DiscColors = new Gradient()
        {
            colorKeys = new GradientColorKey[8]
            {
                new GradientColorKey(Branding.blue, 0),
                new GradientColorKey(Branding.orange, 0.1f),
                new GradientColorKey(Branding.red, 0.3f),
                new GradientColorKey(Branding.yellow, 0.4f),
                new GradientColorKey(Branding.pink, 0.6f),
                new GradientColorKey(Branding.purple, 0.7f),
                new GradientColorKey(Branding.deepBlue, 0.8f),
                new GradientColorKey(Branding.green, 0.9f),
            }
        };

        void OnEnable()
        {
            if (!target)
                return;

            module = (CozyResoundModule)target;
        }

        public override VisualElement DisplayUI()
        {
            root = new VisualElement();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/space.distantlands.cozyweather.core/Editor/UI/Globals.uss");
            if (styleSheet != null) root.styleSheets.Add(styleSheet);

            VisualElement playmodeControls = new VisualElement();
            root.Add(playmodeControls);

            recordContainer = new VisualElement();
            recordContainer.name = "recordContainer";
            recordContainer.style.height = 225;
            recordContainer.style.justifyContent = Justify.Center;
            recordContainer.style.alignItems = Align.Center;
            playmodeControls.Add(recordContainer);

            CreateNewDisc(module.CurrentTrack);

            recordContainer.RegisterCallback<ClickEvent>(evt =>
            {
                GenericMenu menu = new GenericMenu();

                if (module.DJ != null && module.DJ.tracks != null)
                {
                    foreach (ResoundTrack track in module.DJ.tracks)
                    {
                        if (track == null) continue;

                        menu.AddItem(new GUIContent(track.name), track == module.CurrentTrack, () =>
                        {
                            module.SetProfile(track);
                            CreateNewDisc(module.CurrentTrack);
                        });
                    }
                }

                menu.ShowAsContext();
            });

            VisualElement playbackSection = new VisualElement();
            playbackSection.AddToClassList("section-bg");
            playbackSection.style.alignItems = Align.Center;
            playbackSection.style.marginBottom = 10;
            playbackSection.style.marginTop = 5;

            // 1. Current Track Name Label (Readonly)
            Label trackNameLabel = new Label("Track: None");
            trackNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            trackNameLabel.style.fontSize = 12;
            trackNameLabel.style.marginBottom = 5;
            playbackSection.Add(trackNameLabel);

            // 2. Playback Progress Bar (Readonly Time Indicator)
            ProgressBar playbackProgress = new ProgressBar();
            playbackProgress.title = "--:-- / --:--";
            playbackProgress.style.width = 220;
            playbackProgress.style.marginBottom = 8;
            playbackSection.Add(playbackProgress);
            ResoundTrack displayedTrack = module.CurrentTrack;

            // 3. Playback Controls Buttons Row
            VisualElement controlsRow = new VisualElement();
            controlsRow.style.flexDirection = FlexDirection.Row;
            controlsRow.style.justifyContent = Justify.Center;
            controlsRow.style.alignItems = Align.Center;

            Button playPauseButton = new Button(() =>
            {
                module.Playing = !module.Playing;
            })
            { text = module.Playing ? "⏸" : "▶" };
            Button nextButton = new Button(() =>
            {
                module.ChangeProfile();
                if (module.CurrentTrack != null)
                    CreateNewDisc(module.CurrentTrack);
            })
            { text = "⏭" };

            foreach (var btn in new[] { playPauseButton, nextButton })
            {
                btn.style.fontSize = 14;
                btn.style.unityFontStyleAndWeight = FontStyle.Bold;
                btn.style.minWidth = 45;
                controlsRow.Add(btn);
            }
            playbackSection.Add(controlsRow);

            // Scheduled update loop for track name and progress during Play Mode
            playbackSection.schedule.Execute(() =>
            {
                // Update Play/Pause button symbol
                playPauseButton.text = module.Playing ? "⏸" : "▶";

                // Update Track Name
                ResoundTrack currentTrack = module.CurrentTrack;
                if (currentTrack != displayedTrack)
                {
                    displayedTrack = currentTrack;
                    CreateNewDisc(displayedTrack);
                }

                if (currentTrack != null)
                    trackNameLabel.text = $"Playing: {currentTrack.name}";
                else
                    trackNameLabel.text = "Track: None";

                // Update Progress Bar
                if (module.TryGetPlayback(out float time, out float length) && length > 0f)
                {
                    playbackProgress.highValue = length;
                    playbackProgress.value = time;
                    playbackProgress.title = $"{TimeSpan.FromSeconds(time):mm\\:ss} / {TimeSpan.FromSeconds(length):mm\\:ss}";
                }
                else
                {
                    playbackProgress.value = 0f;
                    playbackProgress.highValue = 1f;
                    playbackProgress.title = "--:-- / --:--";
                }
            }).Every(100);

            playmodeControls.Add(playbackSection);

            playmodeControls.schedule.Execute(() =>
            {
                playmodeControls.style.display = Application.isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            }).Every(500);

            Label playbackOptionsLabel = new Label("Playback Options");
            playbackOptionsLabel.AddToClassList("h1");
            root.Add(playbackOptionsLabel);

            VisualElement playbackOptionsContainer = new VisualElement();
            playbackOptionsContainer.AddToClassList("section-bg");
            root.Add(playbackOptionsContainer);

            AddProperty(playbackOptionsContainer, "dj");
            AddProperty(playbackOptionsContainer, "mixerGroup");
            AddProperty(playbackOptionsContainer, "masterVolume");
            AddProperty(playbackOptionsContainer, "allowRepeats");
            PropertyField useSilence = AddProperty(playbackOptionsContainer, "useSilence");

            VisualElement useSilenceSettings = new VisualElement();
            playbackOptionsContainer.Add(useSilenceSettings);

            AddProperty(useSilenceSettings, "minSilence");
            AddProperty(useSilenceSettings, "maxSilence");

            useSilence.RegisterValueChangeCallback(evt =>
            {
                useSilenceSettings.style.display = serializedObject.FindProperty("useSilence").boolValue ? DisplayStyle.Flex : DisplayStyle.None;
            });

            AddProperty(playbackOptionsContainer, "initializationBehavior");
            AddProperty(playbackOptionsContainer, "initialTrack");
            AddProperty(playbackOptionsContainer, "skipTrackIfChanceIsZero");
            AddProperty(playbackOptionsContainer, "transitionType");
            return root;
        }

        private PropertyField AddProperty(VisualElement container, string propertyName)
        {
            var prop = serializedObject.FindProperty(propertyName);
            if (prop != null)
            {
                PropertyField field = new PropertyField();
                field.BindProperty(prop);
                container.Add(field);

                return field;
            }

            return null;
        }

        private (Color mainColor, Color secondaryColor) GetDiscInformation(ResoundTrack track)
        {
            if (!track || !module.DJ || !module.DJ.tracks.Contains(track))
                return (Color.gray, Color.gray);

            Color color = DiscColors.Evaluate((float)module.DJ.tracks.LastIndexOf(track) / module.DJ.tracks.Count);

            return (color, Color.Lerp(color, Color.white, 0.2f));
        }

        private void CreateNewDisc(ResoundTrack track)
        {
            var discInfo = GetDiscInformation(track);
            recordContainer.Clear();

            VisualElement discBackground = new VisualElement();
            discBackground.style.borderBottomLeftRadius = new Length(50, LengthUnit.Percent);
            discBackground.style.borderBottomRightRadius = new Length(50, LengthUnit.Percent);
            discBackground.style.borderTopRightRadius = new Length(50, LengthUnit.Percent);
            discBackground.style.borderTopLeftRadius = new Length(50, LengthUnit.Percent);
            discBackground.style.height = 200;
            discBackground.style.width = 200;
            discBackground.style.backgroundColor = Color.gray1;

            discBackground.generateVisualContent += ctx =>
            {
                var painter = ctx.painter2D;
                float width = discBackground.contentRect.width;
                float height = discBackground.contentRect.height;
                Vector2 center = discBackground.contentRect.center;
                float radius = width * 0.35f;

                painter.lineCap = LineCap.Round;
                painter.strokeGradient = new Gradient()
                {
                    alphaKeys = new GradientAlphaKey[6]
                    {
                        new GradientAlphaKey(0, 0),
                        new GradientAlphaKey(0.1f, 0.1f),
                        new GradientAlphaKey(0, 0.2f),
                        new GradientAlphaKey(0, 0.5f),
                        new GradientAlphaKey(0.1f, 0.6f),
                        new GradientAlphaKey(0, 0.7f)
                    },
                    colorKeys = new GradientColorKey[1]
                    {
                        new GradientColorKey(Color.white, 0)
                    }
                };
                painter.lineWidth = 58f;
                painter.BeginPath();
                painter.Arc(center, radius, 0, 360);
                painter.Stroke();
            };

            float rotation = 0;

            VisualElement discLabel = new VisualElement();
            discLabel.style.borderBottomLeftRadius = new Length(50, LengthUnit.Percent);
            discLabel.style.borderBottomRightRadius = new Length(50, LengthUnit.Percent);
            discLabel.style.borderTopRightRadius = new Length(50, LengthUnit.Percent);
            discLabel.style.borderTopLeftRadius = new Length(50, LengthUnit.Percent);
            discLabel.style.height = 80;
            discLabel.style.width = 80;
            discLabel.style.position = Position.Absolute;
            discLabel.style.top = 60;
            discLabel.style.left = 60;
            discLabel.style.backgroundColor = discInfo.secondaryColor;
            discBackground.Add(discLabel);

            VisualElement discHole = new VisualElement();
            discHole.style.borderBottomLeftRadius = new Length(50, LengthUnit.Percent);
            discHole.style.borderBottomRightRadius = new Length(50, LengthUnit.Percent);
            discHole.style.borderTopRightRadius = new Length(50, LengthUnit.Percent);
            discHole.style.borderTopLeftRadius = new Length(50, LengthUnit.Percent);
            discHole.style.height = 20;
            discHole.style.width = 20;
            discHole.style.position = Position.Absolute;
            discHole.style.top = 90;
            discHole.style.left = 90;
            discHole.style.backgroundColor = Color.black;
            discBackground.Add(discHole);

            discBackground.schedule.Execute(() =>
            {
                if (module.Playing)
                    rotation += 5;

                discBackground.style.rotate = new Rotate(rotation);
            }).Every(50);

            recordContainer.Add(discBackground);
        }
    }
}