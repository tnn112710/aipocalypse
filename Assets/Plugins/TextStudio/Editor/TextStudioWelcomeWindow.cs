using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // The controller owns editor callbacks registered once per code load and must survive Play Mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioWelcomeWindow : EditorWindow
    {
        const string DocumentationUrl = "https://www.wetzold.com/tools/text-studio/docs/";
        const string PackageId = "com.wetzold.text-studio";
        const int WelcomeRevision = 1;
        const float WindowMinWidth = 520f;
        const float WindowMinHeight = 600f;

        static readonly CommonWelcomeController<TextStudioWelcomeWindow> WelcomeController =
            new CommonWelcomeController<TextStudioWelcomeWindow>(PackageId, WelcomeRevision, OpenWindow);

        static readonly string[] StageLabels =
        {
            "1  See it",
            "2  Understand it",
            "3  Explore further"
        };

        ITextStudioOnboardingPreferenceStore _preferenceStore;
        ITextStudioOnboardingSceneService _sceneService;
        TextStudioOnboardingStage _displayedStage;
        TextStudioEditorPreview _heroPreview;
        VisualElement _stageHost;
        Label _statusLabel;
        Button[] _stageButtons;
        bool _isFocused = true;

        [InitializeOnLoadMethod]
        static void ScheduleAutomaticWelcome()
        {
            WelcomeController.ScheduleAutomaticOpen();
        }

        [MenuItem(TextStudioMenuPaths.Welcome, false, TextStudioMenuPaths.WelcomePriority)]
        public static void ShowWindow()
        {
            WelcomeController.OpenManually();
        }

        static TextStudioWelcomeWindow OpenWindow()
        {
            TextStudioWelcomeWindow window = GetWindow<TextStudioWelcomeWindow>(
                true,
                "Welcome to Text Studio",
                true);
            window.minSize = new Vector2(WindowMinWidth, WindowMinHeight);
            window.Show();
            window.Focus();
            return window;
        }

        void OnEnable()
        {
            EnsureServices();
            EditorSceneManager.sceneOpened -= HandleSceneOpened;
            EditorSceneManager.sceneOpened += HandleSceneOpened;
        }

        void OnDisable()
        {
            EditorSceneManager.sceneOpened -= HandleSceneOpened;
            _heroPreview?.SetPaused(true);
        }

        void OnFocus()
        {
            _isFocused = true;
            _heroPreview?.SetPaused(false);
        }

        void OnLostFocus()
        {
            _isFocused = false;
            _heroPreview?.SetPaused(true);
        }

        public void CreateGUI()
        {
            EnsureServices();
            ResolveInitialStage();
            BuildWindow();
        }

        internal static StyleSheet LoadWindowStyleSheet()
        {
            return CommonUITK.LoadStyleSheetFromAnchor(
                "TextStudioWelcomeWindow",
                "/Editor/TextStudioWelcomeWindow.cs",
                "/Editor/TextStudioWelcome.uss");
        }

        void EnsureServices()
        {
            _preferenceStore ??= TextStudioOnboardingState.DefaultStore;
            _sceneService ??= new TextStudioOnboardingSceneService();
        }

        void ResolveInitialStage()
        {
            TextStudioOnboardingStage progress =
                TextStudioOnboardingState.GetProgress(_preferenceStore);
            if (TextStudioOnboardingActions.IsDropInLessonOpen(_sceneService) &&
                progress < TextStudioOnboardingStage.UnderstandIt)
            {
                TextStudioOnboardingState.AdvanceTo(
                    TextStudioOnboardingStage.UnderstandIt,
                    _preferenceStore);
                progress = TextStudioOnboardingStage.UnderstandIt;
            }

            _displayedStage = progress;
        }

        void BuildWindow()
        {
            VisualElement host = rootVisualElement;
            host.Clear();

            VisualElement root = TextStudioInspectorElements.CreateRoot(
                "ts-onboarding-root",
                LoadWindowStyleSheet(),
                "ts-onboarding-root");
            root.style.flexGrow = 1f;
            host.Add(root);

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ts-onboarding-scroll"
            };
            scroll.AddToClassList("ts-onboarding-scroll");
            root.Add(scroll);

            VisualElement content = new VisualElement
            {
                name = "ts-onboarding-content"
            };
            content.AddToClassList("ts-onboarding-content");
            scroll.Add(content);

            AddHero(content);
            content.Add(CreateProgress());

            _stageHost = new VisualElement
            {
                name = "ts-onboarding-stage"
            };
            _stageHost.AddToClassList("ts-onboarding-stage");
            content.Add(_stageHost);

            _statusLabel = new Label
            {
                name = "ts-onboarding-status"
            };
            _statusLabel.AddToClassList("ts-onboarding-status");
            _statusLabel.style.display = DisplayStyle.None;
            content.Add(_statusLabel);

            root.Add(CreateFooter());
            RebuildStage();
        }

        void AddHero(VisualElement parent)
        {
            _heroPreview = new TextStudioEditorPreview(
                "<gradientFlow><wave a=.12 s=.7>Text that feels alive.</wave></gradientFlow>",
                RevealMode.ByGlyph,
                true,
                4.2f,
                38f);
            _heroPreview.AddToClassList("ts-onboarding-hero-preview");
            _heroPreview.SetPaused(!_isFocused);
            parent.Add(_heroPreview);
        }

        VisualElement CreateProgress()
        {
            VisualElement progress = new VisualElement
            {
                name = "ts-onboarding-progress"
            };
            progress.AddToClassList("ts-onboarding-progress");
            _stageButtons = new Button[StageLabels.Length];

            for (int i = 0; i < StageLabels.Length; i++)
            {
                TextStudioOnboardingStage stage = (TextStudioOnboardingStage)i;
                Button button = new Button(() => ShowStage(stage))
                {
                    name = "ts-onboarding-step-" + i,
                    text = StageLabels[i],
                    tooltip = "Show this part of the Text Studio getting-started journey."
                };
                button.AddToClassList("ts-onboarding-step");
                _stageButtons[i] = button;
                progress.Add(button);
            }

            RefreshProgress();
            return progress;
        }

        VisualElement CreateFooter()
        {
            VisualElement footer = CommonUITK.CreateWindowFooter(
                18f,
                14f,
                "ts-onboarding-footer");
            Label hint = CreateLabel(
                "Reopen any time from Tools > Text Studio > Welcome.",
                "ts-onboarding-footer-hint");
            footer.Add(hint);

            Button restart = TextStudioInspectorElements.CreateButton(
                "Restart Guide",
                "Return the guide to its first step without enabling automatic opening again.",
                RestartGuide);
            restart.AddToClassList("ts-onboarding-quiet-button");
            footer.Add(restart);
            return footer;
        }

        void RebuildStage()
        {
            if (_stageHost == null)
                return;

            _stageHost.Clear();
            ClearStatus();
            switch (_displayedStage)
            {
                case TextStudioOnboardingStage.SeeIt:
                    BuildSeeItStage(_stageHost);
                    break;
                case TextStudioOnboardingStage.UnderstandIt:
                    BuildUnderstandItStage(_stageHost);
                    break;
                default:
                    BuildExploreStage(_stageHost);
                    break;
            }

            RefreshProgress();
        }

        void BuildSeeItStage(VisualElement parent)
        {
            AddStageHeading(
                parent,
                "Start with a working example",
                "The Showcase is a small packaged scene with animated text you can select, edit, and replay without entering Play Mode.");

            Button open = TextStudioInspectorElements.CreateButton(
                "Open the Showcase",
                "Save or keep the current scene, then open the packaged Text Studio Showcase and select its text.",
                OpenDropInLesson,
                primary: true);
            open.name = "ts-onboarding-open-lesson";
            open.AddToClassList("ts-onboarding-stage-primary");
            parent.Add(open);

            VisualElement secondary = CreateActionRow();
            secondary.Add(TextStudioInspectorElements.CreateButton(
                "Browse Examples",
                "Open the complete ordered examples browser.",
                TextStudioExamplesWindow.ShowWindow));
            secondary.Add(TextStudioInspectorElements.CreateButton(
                "Skip to Explore",
                "Skip the guided lesson and choose a Text Studio workflow.",
                AdvanceToExplore));
            parent.Add(secondary);
        }

        void BuildUnderstandItStage(VisualElement parent)
        {
            AddStageHeading(parent, "How Text Studio works");

            VisualElement concepts = new VisualElement();
            concepts.AddToClassList("ts-onboarding-concepts");
            concepts.Add(CreateConcept(
                "Source",
                "Write plain text and add tags only where motion or style belongs."));
            concepts.Add(CreateConcept(
                "Raw and Live",
                "Raw shows your markup. Live plays the result with playback controls."));
            concepts.Add(CreateConcept(
                "Reveal, Active, Hide",
                "Shape how text enters, behaves while visible, and leaves."));
            parent.Add(concepts);

            VisualElement actions = CreateActionRow();
            if (TextStudioOnboardingActions.IsDropInLessonOpen(_sceneService))
            {
                actions.Add(TextStudioInspectorElements.CreateButton(
                    "Select Showcase Text",
                    "Select the main Text Studio Text object and show it in the Inspector.",
                    SelectLessonText));
                actions.Add(TextStudioInspectorElements.CreateButton(
                    "Replay in Edit Mode",
                    "Replay the Showcase text immediately without entering Play Mode.",
                    ReplayLessonText));
            }
            else
            {
                actions.Add(TextStudioInspectorElements.CreateButton(
                    "Open the Showcase",
                    "Save or keep the current scene, then open the packaged Text Studio Showcase to try these controls.",
                    OpenDropInLesson));
            }

            parent.Add(actions);

            Button continueButton = TextStudioInspectorElements.CreateButton(
                "Continue with Lesson 2",
                "Open the Examples browser at Learn and continue with Reveal & Typewriter.",
                ContinueToLearnPath,
                primary: true);
            continueButton.name = "ts-onboarding-continue";
            continueButton.AddToClassList("ts-onboarding-stage-primary");
            parent.Add(continueButton);
        }

        void BuildExploreStage(VisualElement parent)
        {
            AddStageHeading(parent, "Choose your next step");

            VisualElement goals = CreateGrid();
            goals.Add(CreateGoalCard(
                "Continue the Learn path",
                "Follow the numbered lessons, starting with Reveal & Typewriter.",
                "Browse Learn Examples",
                TextStudioExamplesWindow.ShowLearnPath,
                true));
            goals.Add(CreateGoalCard(
                "Dialogue and subtitles",
                "Start from reusable dialogue, long-text, audio, and event setups.",
                "Open Recipes",
                TextStudioRecipeWindow.ShowWindow));
            goals.Add(CreateGoalCard(
                "Effects and motion",
                "Preview effects live, then create an editable example or copy its markup.",
                "Open Text Gallery",
                TextGalleryWindow.ShowWindow));
            goals.Add(CreateThreeDCard());
            parent.Add(goals);

            VisualElement helpActions = CreateActionRow();
            helpActions.Add(TextStudioInspectorElements.CreateButton(
                "Documentation",
                "Open the manual for UI Toolkit, scripting, integrations, and advanced authoring.",
                OpenDocumentation));
            helpActions.Add(TextStudioInspectorElements.CreateButton(
                "Health Check",
                "Find setup, reference, and authoring problems with direct fixes.",
                TextStudioHealthWindow.ShowWindow));
#if TEXTSTUDIO_TEXT_ANIMATOR_INSTALLED
            helpActions.Add(TextStudioInspectorElements.CreateButton(
                "Migrate Text Animator",
                "Scan and convert supported Text Animator content through the guided migration window.",
                TextAnimatorMigrationWindow.ShowWindow));
#endif
            helpActions.Add(TextStudioInspectorElements.CreateButton(
                "Community",
                "Open the Wetzold Studios community.",
                OpenCommunity));
            parent.Add(helpActions);

            Button close = TextStudioInspectorElements.CreateButton(
                "Close and Start Creating",
                "Close the Welcome window. You can reopen it from the Text Studio Tools menu.",
                Close,
                primary: true);
            close.AddToClassList("ts-onboarding-stage-primary");
            parent.Add(close);
        }

        static void AddStageHeading(
            VisualElement parent,
            string title,
            string summary = null)
        {
            parent.Add(CreateLabel(title, "ts-onboarding-stage-title"));
            if (!string.IsNullOrEmpty(summary))
                parent.Add(CreateLabel(summary, "ts-onboarding-stage-summary"));
        }

        static VisualElement CreateConcept(string title, string description)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-onboarding-concept");
            row.Add(CreateLabel(title, "ts-onboarding-concept-title"));
            row.Add(CreateLabel(description, "ts-onboarding-concept-copy"));
            return row;
        }

        static VisualElement CreateGoalCard(
            string title,
            string description,
            string actionLabel,
            Action action,
            bool primary = false)
        {
            VisualElement card = new VisualElement();
            card.AddToClassList("ts-onboarding-card");
            card.Add(CreateLabel(title, "ts-onboarding-card-title"));
            card.Add(CreateLabel(description, "ts-onboarding-card-copy"));
            Button button = TextStudioInspectorElements.CreateButton(
                actionLabel,
                description,
                action,
                primary);
            button.AddToClassList("ts-onboarding-card-action");
            card.Add(button);
            return card;
        }

        static VisualElement CreateGrid()
        {
            VisualElement grid = new VisualElement();
            grid.AddToClassList("ts-onboarding-grid");
            return grid;
        }

        static VisualElement CreateActionRow()
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("ts-onboarding-action-row");
            return row;
        }

        static Label CreateLabel(string text, string className)
        {
            Label label = new Label(text);
            label.AddToClassList(className);
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.minWidth = 0f;
            label.style.flexShrink = 1f;
            return label;
        }

        void ShowStage(TextStudioOnboardingStage stage)
        {
            if (stage > GetMaximumUnlockedStage())
                return;

            _displayedStage = stage;
            RebuildStage();
        }

        void RefreshProgress()
        {
            if (_stageButtons == null)
                return;

            TextStudioOnboardingStage unlocked = GetMaximumUnlockedStage();
            for (int i = 0; i < _stageButtons.Length; i++)
            {
                TextStudioOnboardingStage stage = (TextStudioOnboardingStage)i;
                Button button = _stageButtons[i];
                button.SetEnabled(stage <= unlocked);
                button.EnableInClassList(
                    "ts-onboarding-step--current",
                    stage == _displayedStage);
                button.EnableInClassList(
                    "ts-onboarding-step--complete",
                    stage < unlocked);
            }
        }

        TextStudioOnboardingStage GetMaximumUnlockedStage()
        {
            TextStudioOnboardingStage progress =
                TextStudioOnboardingState.GetProgress(_preferenceStore);
            if (TextStudioOnboardingActions.IsDropInLessonOpen(_sceneService) &&
                progress < TextStudioOnboardingStage.UnderstandIt)
            {
                return TextStudioOnboardingStage.UnderstandIt;
            }

            return progress;
        }

        void OpenDropInLesson()
        {
            if (!TextStudioOnboardingActions.TryOpenDropInLesson(
                    _sceneService,
                    _preferenceStore,
                    out string error))
            {
                SetStatus(error, true);
                return;
            }

            _displayedStage = TextStudioOnboardingStage.UnderstandIt;
            RebuildStage();
            SelectLessonText();
        }

        void SelectLessonText()
        {
            TextStudioText hub = FindLessonText();
            if (hub == null)
            {
                SetStatus("Open the Showcase before selecting its text.", true);
                return;
            }

            Selection.activeGameObject = hub.gameObject;
            EditorGUIUtility.PingObject(hub.gameObject);
            SetStatus("Showcase text selected. Its Text Studio controls are in the Inspector.", false);
        }

        void ReplayLessonText()
        {
            TextStudioText hub = FindLessonText();
            if (hub == null)
            {
                SetStatus("Open the Showcase before replaying its text.", true);
                return;
            }

            Selection.activeGameObject = hub.gameObject;
            hub.StartReveal();
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            SetStatus("Replaying in Edit Mode. No Play Mode setup is required.", false);
        }

        TextStudioText FindLessonText()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() ||
                !TextStudioOnboardingActions.IsDropInLessonOpen(_sceneService))
            {
                return null;
            }

            TextStudioText fallback = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                TextStudioText[] hubs =
                    roots[rootIndex].GetComponentsInChildren<TextStudioText>(true);
                for (int hubIndex = 0; hubIndex < hubs.Length; hubIndex++)
                {
                    TextStudioText hub = hubs[hubIndex];
                    fallback ??= hub;
                    if (string.Equals(
                            hub.gameObject.name,
                            "Passage - native reveal, style tags and marker",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return hub;
                    }
                }
            }

            return fallback;
        }

        void AdvanceToExplore()
        {
            TextStudioOnboardingState.AdvanceTo(
                TextStudioOnboardingStage.ExploreFurther,
                _preferenceStore);
            _displayedStage = TextStudioOnboardingStage.ExploreFurther;
            RebuildStage();
        }

        void ContinueToLearnPath()
        {
            TextStudioOnboardingActions.ContinueToLearnPath(
                _preferenceStore,
                TextStudioExamplesWindow.ShowLearnPath);
            _displayedStage = TextStudioOnboardingStage.ExploreFurther;
            RebuildStage();
        }

        void RestartGuide()
        {
            TextStudioOnboardingState.Restart(_preferenceStore);
            _displayedStage = TextStudioOnboardingStage.SeeIt;
            RebuildStage();
            SetStatus("Guide restarted.", false);
        }

        void HandleSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (_sceneService == null)
                return;

            if (TextStudioOnboardingActions.IsDropInLessonOpen(_sceneService))
            {
                TextStudioOnboardingState.AdvanceTo(
                    TextStudioOnboardingStage.UnderstandIt,
                    _preferenceStore);
                if (_displayedStage < TextStudioOnboardingStage.UnderstandIt)
                    _displayedStage = TextStudioOnboardingStage.UnderstandIt;
            }

            // Understand It offers Showcase actions only while the Showcase is the active scene.
            if (_displayedStage == TextStudioOnboardingStage.UnderstandIt)
                RebuildStage();
        }

        void SetStatus(string message, bool warning)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.text = message ?? string.Empty;
            _statusLabel.style.display = string.IsNullOrEmpty(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
            _statusLabel.EnableInClassList(
                "ts-onboarding-status--warning",
                warning);
        }

        void ClearStatus()
        {
            SetStatus(string.Empty, false);
        }

        // The base Welcome promotes only the Text Studio 3D add-on.
        static VisualElement CreateThreeDCard()
        {
            ToolInfo tool = ToolCatalog.Load().GetTool("TextStudio.3D");
            string description = tool != null
                ? tool.description
                : "Extruded, beveled 3D typography with layered materials for Text Studio.";
            if (IsAddOnInstalled(tool, "TextStudio.ThreeD.Editor.TextStudio3DGalleryProvider"))
            {
                return CreateGoalCard(
                    "Text Studio 3D",
                    description,
                    "Open 3D Gallery",
                    () => ExecuteMenu(TextStudioMenuPaths.ThreeDGallery));
            }

            string url = tool != null && !string.IsNullOrWhiteSpace(tool.assetStoreLink)
                ? tool.assetStoreLink
                : tool != null ? tool.webLink : string.Empty;
            return CreateGoalCard(
                "Text Studio 3D",
                description,
                "Learn More",
                string.IsNullOrWhiteSpace(url)
                    ? null
                    : () => Application.OpenURL(url));
        }

        internal static bool IsAddOnInstalled(ToolInfo tool, string providerTypeName)
        {
            if (tool != null &&
                !string.IsNullOrWhiteSpace(tool.packageName) &&
                UnityEditor.PackageManager.PackageInfo.FindForPackageName(tool.packageName) != null)
            {
                return true;
            }

            foreach (Type type in TypeCache.GetTypesDerivedFrom<ITextStudioGalleryProvider>())
            {
                if (string.Equals(type.FullName, providerTypeName, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static void OpenDocumentation()
        {
            string[] guids = AssetDatabase.FindAssets("Documentation t:DefaultAsset");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
                if (!path.EndsWith(
                        "/TextStudio/Documentation/Documentation.pdf",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Object documentation = AssetDatabase.LoadMainAssetAtPath(path);
                if (documentation != null)
                {
                    AssetDatabase.OpenAsset(documentation);
                    return;
                }
            }

            Application.OpenURL(DocumentationUrl);
        }

        static void OpenCommunity()
        {
            string discordLink = ToolCatalog.Load().discordLink;
            if (!string.IsNullOrWhiteSpace(discordLink))
                Application.OpenURL(discordLink);
        }

        static void ExecuteMenu(string path)
        {
            if (!EditorApplication.ExecuteMenuItem(path))
                Debug.LogWarning("[Text Studio] Menu item not found: " + path);
        }
    }
}
