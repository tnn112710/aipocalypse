using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>
    /// Guided Text Animator migration. It leads with one verdict, offers additive one-click steps that also run per
    /// item, explains the work that needs the user, and ends with a checklist for removing Text Animator. The window
    /// never changes or removes Text Animator components, assets or settings.
    /// </summary>
    internal sealed partial class TextAnimatorMigrationWindow : EditorWindow
    {
        const string WindowTitle = "Text Animator Migration";

        [SerializeField] TextAnimatorMigrationScanScope _scope = TextAnimatorMigrationScanScope.Project;
        [SerializeField] string _folder = "Assets";
        [SerializeField] string _outputFolder = TextAnimatorMigrationActions.DefaultOutputFolder;
        [SerializeField] bool _includeSamples;
        [SerializeField] int _tab;

        TextAnimatorMigrationScanResult _scan;
        TextAnimatorMigrationPlan _plan = TextAnimatorMigrationPlan.Empty;
        bool _scanning;
        string _resultMessage = string.Empty;
        IVisualElementScheduledItem _scanItem;
        IVisualElementScheduledItem _refreshItem;

        VisualElement _root;
        CommonTabbedPane _tabs;
        ScrollView _guide;
        DropdownField _scopeField;
        Label _scopeInfo;
        Button _rescanButton;
        VisualElement _summary;
        Label _summaryIcon;
        Label _headline;
        Label _summaryPill;
        Label _guidance;
        VisualElement _stats;
        StatTile _worksTile;
        StatTile _automaticTile;
        StatTile _manualTile;
        VisualElement _safety;
        Button _runAllButton;
        Label _resultLabel;
        VisualElement _planContent;
        VisualElement _stepsSection;
        Label _stepsPill;
        Label _outputLabel;
        Label _notNeededLabel;
        VisualElement _topicsSection;
        Label _topicsPill;
        VisualElement _worksSection;
        Label _samplesNote;
        readonly List<StepCard> _stepCards = new List<StepCard>();
        readonly List<TopicCard> _topicCards = new List<TopicCard>();

        public static void ShowWindow()
        {
            if (!TextAnimatorMigrationAvailability.IsInstalled)
                return;

            TextAnimatorMigrationWindow window = GetWindow<TextAnimatorMigrationWindow>();
            window.titleContent = new GUIContent(WindowTitle);
            window.minSize = new Vector2(420f, 480f);
            window.Show();
        }

        void OnEnable()
        {
            EditorApplication.hierarchyChanged += ScheduleRefresh;
            EditorApplication.projectChanged += ScheduleRefresh;
            Undo.undoRedoPerformed += ScheduleRefresh;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= ScheduleRefresh;
            EditorApplication.projectChanged -= ScheduleRefresh;
            Undo.undoRedoPerformed -= ScheduleRefresh;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        public void CreateGUI()
        {
            BuildLayout();
            BeginScan();
        }

        void BuildLayout()
        {
            rootVisualElement.Clear();
            _stepCards.Clear();
            _topicCards.Clear();
            StyleSheet style = CommonUITK.LoadStyleSheetFromAnchor(
                "TextAnimatorMigrationWindow",
                "/Migration/TextAnimatorMigrationWindow.cs",
                "/Migration/TextAnimatorMigrationWindow.uss");
            _root = TextStudioInspectorElements.CreateRoot("ts-migration-root", style, "ts-migration-window");
            _root.style.flexGrow = 1f;
            rootVisualElement.Add(_root);

            VisualElement masthead = CommonUITK.CreateContainer("ts-migration-masthead");
            masthead.Add(CommonUITK.CreateLogoHeader(
                "Migrate from Text Animator",
                CommonUIStyles.LoadTexture("TextStudio"),
                32,
                "ts-migration-header",
                "ts-migration-logo",
                "ts-migration-title"));
            masthead.Add(Copy("Text Studio reads most Text Animator markup as it is. This guide shows what converts in one click, what needs you, and what already works."));
            _root.Add(masthead);

            _tabs = new CommonTabbedPane(new CommonTabbedPane.TabbedPaneClasses
            {
                RootClass = "ts-migration-tabs",
                TabStripClass = "ts-migration-tab-strip",
                TabClass = "ts-migration-tab",
                SelectedTabClass = "ts-migration-tab--selected",
                BodyClass = "ts-migration-tab-body"
            });
            _tabs.SetTabs(
                new[]
                {
                    new GUIContent("Guide", "What to do, in order: one-click conversions, work that needs you, and the finish checklist."),
                    new GUIContent("All findings", "Every Text Animator text, component, asset and script the scan found, with search and filters.")
                },
                _tab,
                SwitchTab);
            _root.Add(_tabs);

            _guide = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "ts-migration-guide",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden
            };
            _guide.AddToClassList("ts-migration-scroll");
            _guide.contentContainer.AddToClassList("ts-migration-scroll-content");
            _tabs.Body.Add(_guide);

            BuildScope();
            BuildSummary();
            _planContent = CommonUITK.CreateContainer("ts-migration-plan");
            _planContent.name = "ts-migration-plan";
            _guide.Add(_planContent);
            BuildSteps();
            BuildTopics();
            BuildWorks();
            BuildFinish();
            _samplesNote = Copy(string.Empty, "ts-migration-samples-note");
            _planContent.Add(_samplesNote);
            BuildAdvanced();
            BuildFindings();
            SwitchTab(_tab);
        }

        void BuildScope()
        {
            VisualElement scope = CommonUITK.CreateContainer("ts-migration-scope");
            _scopeField = new DropdownField(
                "Check",
                new List<string> { "Whole project", "Open scenes", "Selection", "One folder" },
                (int)_scope)
            {
                name = "ts-migration-scope",
                tooltip = "What to check for Text Animator content. Whole project covers open scenes, prefabs, assets, closed scenes and scripts."
            };
            _scopeField.RegisterValueChangedCallback(_ => ChangeScope(_scopeField.index));
            scope.Add(_scopeField);
            _rescanButton = Button(
                "Check again",
                "Check the selected scope again, for example after editing scenes or prefabs outside this window.",
                BeginScan,
                "ts-migration-rescan");
            scope.Add(_rescanButton);
            _scopeInfo = Copy(string.Empty, "ts-migration-scope-info");
            scope.Add(_scopeInfo);
            _guide.Add(scope);
        }

        void BuildSummary()
        {
            _summary = CommonUITK.CreateContainer("ts-migration-summary");
            _summary.name = "ts-migration-summary";
            VisualElement heading = CommonUITK.CreateContainer("ts-migration-summary-heading");
            _summaryIcon = CommonUITK.CreateLabel(string.Empty, "ts-migration-status-icon");
            heading.Add(_summaryIcon);
            _headline = CommonUITK.CreateLabel(string.Empty, "ts-migration-headline");
            _headline.name = "ts-migration-headline";
            heading.Add(_headline);
            _summaryPill = Pill(string.Empty, string.Empty, string.Empty);
            heading.Add(_summaryPill);
            _summary.Add(heading);

            _guidance = Copy(string.Empty, "ts-migration-guidance");
            _summary.Add(_guidance);

            _stats = CommonUITK.CreateContainer("ts-migration-stats");
            _worksTile = CreateStatTile("ts-migration-stat--works", "Already work as they are in Text Studio.");
            _automaticTile = CreateStatTile("ts-migration-stat--automatic", "Convert with one click. Each step only adds Text Studio content.");
            _manualTile = CreateStatTile("ts-migration-stat--manual", "Need a decision or a small change from you. Each one comes with instructions.");
            _stats.Add(_worksTile.Root);
            _stats.Add(_automaticTile.Root);
            _stats.Add(_manualTile.Root);
            _summary.Add(_stats);

            _safety = CommonUITK.CreateContainer("ts-migration-safety");
            _safety.tooltip = "The wizard never edits or deletes Text Animator components, assets or settings. Remove Text Animator yourself when you are ready.";
            _safety.Add(CommonUITK.CreateLabel("✓", "ts-migration-safety-icon"));
            _safety.Add(Copy("Safe to try: these steps only add Text Studio components and assets. Text Animator stays installed and unchanged until you remove it, and changes support Undo."));
            _summary.Add(_safety);

            _runAllButton = TextStudioInspectorElements.CreateButton(
                "Run one-click conversions",
                "Run every ready one-click step in order. You see a summary of the changes first.",
                RunAll,
                primary: true);
            _runAllButton.name = "ts-migration-run-all";
            _runAllButton.AddToClassList("ts-migration-primary");
            _summary.Add(_runAllButton);

            _resultLabel = Copy(string.Empty, "ts-migration-result");
            _resultLabel.name = "ts-migration-result";
            _summary.Add(_resultLabel);
            _guide.Add(_summary);
        }

        void BuildSteps()
        {
            _stepsSection = CommonUITK.CreateContainer("ts-migration-section");
            _stepsSection.name = "ts-migration-steps";
            _stepsPill = AddSectionHeading(_stepsSection, "One-click steps");
            _stepsSection.Add(Copy(
                "Run them all from the summary, one step at a time, or item by item. Prefabs are saved for you; save open scenes to keep their changes.",
                "ts-migration-section-copy"));

            VisualElement output = CommonUITK.CreateContainer("ts-migration-output");
            _outputLabel = Copy(string.Empty);
            output.Add(_outputLabel);
            output.Add(Button(
                "Change...",
                "Choose the folder inside Assets where converted typing speed, playback and style alias assets are saved.",
                ChooseOutputFolder,
                "ts-migration-output-folder"));
            _stepsSection.Add(output);

            foreach (TextAnimatorMigrationStepKind kind in Enum.GetValues(typeof(TextAnimatorMigrationStepKind)))
            {
                StepCard card = CreateStepCard(kind);
                _stepCards.Add(card);
                _stepsSection.Add(card.Root);
            }

            _notNeededLabel = Copy(string.Empty, "ts-migration-small");
            _stepsSection.Add(_notNeededLabel);
            _planContent.Add(_stepsSection);
        }

        void BuildTopics()
        {
            _topicsSection = CommonUITK.CreateContainer("ts-migration-section");
            _topicsSection.name = "ts-migration-needs-you";
            _topicsPill = AddSectionHeading(_topicsSection, "Needs you");
            _topicsSection.Add(Copy(
                "Text Studio can't do these for you, but each card says exactly what to do. None of them blocks the one-click steps.",
                "ts-migration-section-copy"));
            foreach (TextAnimatorMigrationTopicKind kind in Enum.GetValues(typeof(TextAnimatorMigrationTopicKind)))
            {
                TopicCard card = CreateTopicCard(kind);
                _topicCards.Add(card);
                _topicsSection.Add(card.Root);
            }

            _planContent.Add(_topicsSection);
        }

        void BuildWorks()
        {
            _worksSection = CommonUITK.CreateContainer("ts-migration-section");
            _worksSection.name = "ts-migration-works";
            AddSectionHeading(_worksSection, "Already works");
            _worksCard = CreateWorksCard();
            _worksSection.Add(_worksCard.Root);
            _planContent.Add(_worksSection);
        }

        void BuildFinish()
        {
            VisualElement section = CommonUITK.CreateContainer("ts-migration-section");
            section.name = "ts-migration-finish";
            AddSectionHeading(section, "Finish up");
            _finishCard = CreateFinishCard();
            section.Add(_finishCard.Root);
            _planContent.Add(section);
        }

        void SwitchTab(int index)
        {
            _tab = Mathf.Clamp(index, 0, 1);
            _tabs.SetSelectedIndexWithoutNotify(_tab);
            _guide.style.display = _tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _findingsView.style.display = _tab == 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_tab == 1)
                RefreshFindings();
        }

        void ChangeScope(int index)
        {
            TextAnimatorMigrationScanScope scope = (TextAnimatorMigrationScanScope)Mathf.Clamp(index, 0, 3);
            if (scope == TextAnimatorMigrationScanScope.Folder)
            {
                string chosen = EditorUtility.OpenFolderPanel("Choose a folder to check", Application.dataPath, string.Empty);
                if (!TextAnimatorMigrationActions.TryGetAssetFolder(chosen, out string folder))
                {
                    if (!string.IsNullOrEmpty(chosen))
                        EditorUtility.DisplayDialog(WindowTitle, "Choose a folder inside this project's Assets folder.", "OK");
                    _scopeField.SetValueWithoutNotify(_scopeField.choices[(int)_scope]);
                    return;
                }

                _folder = folder;
            }

            _scope = scope;
            BeginScan();
        }

        void ChooseOutputFolder()
        {
            string chosen = EditorUtility.OpenFolderPanel("Save converted assets in", Application.dataPath, string.Empty);
            if (string.IsNullOrEmpty(chosen))
                return;

            if (!TextAnimatorMigrationActions.TryGetAssetFolder(chosen, out string folder))
            {
                EditorUtility.DisplayDialog(WindowTitle, "Choose a folder inside this project's Assets folder.", "OK");
                return;
            }

            _outputFolder = folder;
            UpdateView();
        }

        void BeginScan()
        {
            if (_root == null)
                return;

            _scanning = true;
            _resultMessage = string.Empty;
            UpdateView();
            _scanItem?.Pause();
            _scanItem = _root.schedule.Execute(RunScan);
            _scanItem.ExecuteLater(50);
        }

        void RunScan()
        {
            _scanItem = null;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _scanning = false;
                UpdateView();
                return;
            }

            _scan = TextAnimatorMigrationAssetScanner.Scan(_scope, _folder);
            _scanning = false;
            RebuildPlan();
            UpdateView();
        }

        // Shows an existing scan without checking the project again; tests use it to exercise every state.
        internal void ShowScan(TextAnimatorMigrationScanResult scan)
        {
            _scanItem?.Pause();
            _scanItem = null;
            _scan = scan;
            _scanning = false;
            RebuildPlan();
            UpdateView();
        }

        void RebuildPlan()
        {
            _plan = _scan == null || _scan.Canceled
                ? TextAnimatorMigrationPlan.Empty
                : TextAnimatorMigrationPlan.Build(_scan, _includeSamples);
        }

        // Live project changes only rebuild the plan; a new scan runs when scenes open or close.
        // Play Mode is skipped because the hierarchy changes constantly there and a full check follows on exit.
        void ScheduleRefresh()
        {
            if (_root == null || _scan == null || _scanning || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _refreshItem?.Pause();
            _refreshItem = _root.schedule.Execute(() =>
            {
                RebuildPlan();
                UpdateView();
            });
            _refreshItem.ExecuteLater(300);
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            ScheduleRescan();
        }

        void OnSceneClosed(Scene scene)
        {
            ScheduleRescan();
        }

        void ScheduleRescan()
        {
            if (_root == null || _scan == null || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            _scanItem?.Pause();
            _scanItem = _root.schedule.Execute(RunScan);
            _scanItem.ExecuteLater(400);
        }

        void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                BeginScan();
            else if (change == PlayModeStateChange.EnteredPlayMode)
                UpdateView();
        }

        static bool CanEdit()
        {
            return !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;
        }

        void UpdateView()
        {
            if (_root == null)
                return;

            bool editable = CanEdit();
            bool hasResults = _scan != null && !_scan.Canceled;
            UpdateScopeInfo();
            UpdateSummary(editable);
            SetDisplay(_planContent, hasResults && _plan.HasWork);
            if (hasResults)
            {
                UpdateSteps(editable);
                UpdateTopics();
                UpdateWorks();
                UpdateFinish(editable);
                _samplesNote.text = _plan.ExcludedSampleCount == 0
                    ? string.Empty
                    : Count(_plan.ExcludedSampleCount, "item", "items") +
                      " in Text Animator's own samples are not counted. They are removed together with Text Animator.";
                SetDisplay(_samplesNote, _plan.ExcludedSampleCount > 0 && !_includeSamples);
            }

            UpdateAdvanced(editable);
            if (_tab == 1)
                RefreshFindings();
        }

        void UpdateScopeInfo()
        {
            _rescanButton.SetEnabled(!_scanning && CanEdit());
            if (_scanning)
            {
                _scopeInfo.text = "Checking " + DescribeScope() + "...";
                return;
            }

            if (_scan == null)
            {
                _scopeInfo.text = string.Empty;
                return;
            }

            _scopeInfo.text = _scan.Canceled
                ? "The check was canceled."
                : "Checked " + DescribeScope() + " in " + _scan.Seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s.";
        }

        string DescribeScope()
        {
            switch (_scope)
            {
                case TextAnimatorMigrationScanScope.OpenScenes:
                    return "the open scenes";
                case TextAnimatorMigrationScanScope.Selection:
                    return "the selection";
                case TextAnimatorMigrationScanScope.Folder:
                    return _folder;
                default:
                    return "the whole project";
            }
        }

        void UpdateSummary(bool editable)
        {
            TextAnimatorMigrationPlan plan = _plan;
            string tone;
            string icon;
            string headline;
            string pill;
            string guidance;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                tone = "attention";
                icon = "!";
                headline = "Exit Play Mode to continue";
                pill = "Paused";
                guidance = "Migration changes scenes, prefabs and assets, so it only runs in Edit Mode. The check starts again when you exit Play Mode.";
            }
            else if (_scanning && _scan == null)
            {
                tone = "info";
                icon = "…";
                headline = "Looking for Text Animator content";
                pill = "Checking";
                guidance = "Checking scenes, prefabs, assets and scripts. Nothing is changed while checking.";
            }
            else if (_scan == null || _scan.Canceled)
            {
                tone = "info";
                icon = "i";
                headline = "Check your project";
                pill = "Not checked";
                guidance = "Choose what to check above and select Check again to see what your project uses from Text Animator.";
            }
            else if (!plan.HasWork)
            {
                tone = "ready";
                icon = "✓";
                headline = "Nothing to migrate";
                pill = "Ready";
                guidance = "No scene, prefab, text or script in " + DescribeScope() +
                           " uses Text Animator. You can remove Text Animator whenever you like.";
            }
            else if (plan.ReadyCount > 0)
            {
                bool manual = plan.NeedsYouCount > 0;
                tone = manual ? "info" : "ready";
                icon = manual ? "i" : "✓";
                headline = manual ? "Mostly automatic" : "Everything converts automatically";
                pill = "Safe to start";
                guidance = Count(plan.ReadyCount, "item converts", "items convert") + " in one click" +
                           (manual
                               ? ". " + Count(plan.NeedsYouCount, "item needs", "items need") + " you; the cards below explain each one."
                               : ". Nothing needs to be done by hand.") +
                           ClosedSceneSuffix(plan);
            }
            else if (plan.ClosedSceneCount > 0)
            {
                tone = "info";
                icon = "i";
                headline = "Open the remaining scenes";
                pill = "In progress";
                guidance = Count(plan.ClosedSceneCount, "closed scene still uses", "closed scenes still use") +
                           " Text Animator. Open each one from the steps below to convert it" +
                           (plan.NeedsYouCount > 0 ? ", and work through the cards under Needs you." : ".");
            }
            else if (plan.NeedsYouCount > 0 || plan.BlockedCount > 0)
            {
                tone = "attention";
                icon = "!";
                headline = plan.NeedsYouCount > 0 ? "A few things need you" : "Some prefabs are locked";
                pill = "Needs you";
                guidance = plan.NeedsYouCount > 0
                    ? "Every one-click step is done. Work through the " + Count(plan.NeedsYouCount, "item", "items") +
                      " under Needs you, then finish up."
                    : "Close Prefab Mode for the locked prefabs, then run the remaining steps.";
            }
            else
            {
                tone = "ready";
                icon = "✓";
                headline = "Ready to remove Text Animator";
                pill = "Done";
                guidance = "Everything is converted. Check your texts, then follow Finish up below to remove Text Animator.";
            }

            _summary.EnableInClassList("ts-migration-summary--ready", tone == "ready");
            _summary.EnableInClassList("ts-migration-summary--attention", tone == "attention");
            _summaryIcon.text = icon;
            _headline.text = headline;
            SetPill(_summaryPill, pill, tone, "Overall migration status for " + DescribeScope() + ".");
            _guidance.text = guidance;

            bool hasPlan = _scan != null && !_scan.Canceled && plan.HasWork && !EditorApplication.isPlayingOrWillChangePlaymode;
            SetDisplay(_stats, hasPlan);
            SetDisplay(_safety, hasPlan && plan.ReadyCount + plan.ClosedSceneCount > 0);
            if (hasPlan)
                UpdateStats(plan);

            SetDisplay(_runAllButton, hasPlan && plan.ReadyCount > 0);
            _runAllButton.SetEnabled(editable);
            _runAllButton.text = plan.ReadyCount == 1
                ? "Run 1 one-click conversion"
                : "Run " + plan.ReadyCount + " one-click conversions";
            _resultLabel.text = _resultMessage;
            SetDisplay(_resultLabel, !string.IsNullOrEmpty(_resultMessage));
        }

        static string ClosedSceneSuffix(TextAnimatorMigrationPlan plan)
        {
            return plan.ClosedSceneCount == 0
                ? string.Empty
                : plan.ClosedSceneCount == 1
                    ? " One closed scene also uses Text Animator; open it from the steps to include it."
                    : " " + plan.ClosedSceneCount + " closed scenes also use Text Animator; open them from the steps to include them.";
        }

        void UpdateStats(TextAnimatorMigrationPlan plan)
        {
            int working = plan.WorkingTexts.Count + plan.BuiltInEquivalentCount;
            SetStat(
                _worksTile,
                working,
                working == 1 ? "already works" : "already work",
                plan.WorkingTexts.Count > 0 || plan.BuiltInEquivalentCount > 0
                    ? Count(plan.WorkingTexts.Count, "text", "texts") + ", " +
                      Count(plan.BuiltInEquivalentCount, "built-in equivalent", "built-in equivalents")
                    : "Nothing needs checking here",
                working > 0);

            int automaticSteps = 0;
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                if (plan.Steps[i].ReadyCount > 0)
                    automaticSteps++;
            }

            string automaticDetail = plan.ReadyCount > 0
                ? "in " + Count(automaticSteps, "step", "steps")
                : plan.DoneCount > 0 ? "All converted" : "Nothing to convert";
            if (plan.DoneCount > 0 && plan.ReadyCount > 0)
                automaticDetail += ", " + plan.DoneCount + " done";
            if (plan.ClosedSceneCount > 0)
                automaticDetail += ", plus " + Count(plan.ClosedSceneCount, "closed scene", "closed scenes");
            SetStat(
                _automaticTile,
                plan.ReadyCount,
                plan.ReadyCount == 1 ? "converts in one click" : "convert in one click",
                automaticDetail,
                plan.ReadyCount > 0);

            SetStat(
                _manualTile,
                plan.NeedsYouCount,
                plan.NeedsYouCount == 1 ? "needs you" : "need you",
                plan.NeedsYouCount > 0 ? DescribeTopics(plan) : "Nothing to do by hand",
                plan.NeedsYouCount > 0);
        }

        static string DescribeTopics(TextAnimatorMigrationPlan plan)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < plan.Topics.Count; i++)
            {
                TextAnimatorMigrationPlanTopic topic = plan.Topics[i];
                if (!topic.IsApplicable)
                    continue;

                if (builder.Length > 0)
                    builder.Append(", ");
                builder.Append(TopicShortName(topic.Kind));
            }

            return Capitalize(builder.ToString());
        }

        void UpdateSteps(bool editable)
        {
            TextAnimatorMigrationPlan plan = _plan;
            bool anyApplicable = false;
            int number = 0;
            StringBuilder notNeeded = new StringBuilder();
            for (int i = 0; i < _stepCards.Count; i++)
            {
                StepCard card = _stepCards[i];
                TextAnimatorMigrationPlanStep step = plan.GetStep(card.Kind);
                SetDisplay(card.Root, step.IsApplicable);
                if (!step.IsApplicable)
                {
                    if (notNeeded.Length > 0)
                        notNeeded.Append(", ");
                    notNeeded.Append(StepShortName(card.Kind));
                    continue;
                }

                anyApplicable = true;
                UpdateStepCard(card, step, ++number, editable);
            }

            SetDisplay(_stepsSection, anyApplicable);
            _outputLabel.text = "New assets are saved in " + _outputFolder + ".";
            _notNeededLabel.text = notNeeded.Length > 0 ? "Not needed in " + DescribeScope() + ": " + notNeeded + "." : string.Empty;
            SetDisplay(_notNeededLabel, notNeeded.Length > 0);
            if (plan.ReadyCount > 0)
                SetPill(_stepsPill, plan.ReadyCount + " ready", "info", "Items that convert in one click.");
            else if (plan.ClosedSceneCount > 0)
                SetPill(_stepsPill, Count(plan.ClosedSceneCount, "closed scene", "closed scenes"), "info", "Open these scenes to convert their Text Animator objects.");
            else
                SetPill(_stepsPill, "All done", "ready", "Every one-click step is complete.");
        }

        void UpdateTopics()
        {
            TextAnimatorMigrationPlan plan = _plan;
            for (int i = 0; i < _topicCards.Count; i++)
            {
                TopicCard card = _topicCards[i];
                TextAnimatorMigrationPlanTopic topic = plan.GetTopic(card.Kind);
                SetDisplay(card.Root, topic.IsApplicable);
                if (topic.IsApplicable)
                    UpdateTopicCard(card, topic);
            }

            SetDisplay(_topicsSection, plan.NeedsYouCount > 0);
            SetPill(_topicsPill, Count(plan.NeedsYouCount, "item", "items"), "attention", "Work that Text Studio can't do automatically.");
        }

        void RunStep(TextAnimatorMigrationStepKind kind, IEnumerable<TextAnimatorMigrationPlanItem> items)
        {
            if (!CanEdit())
                return;

            int group = BeginUndoGroup(StepTitle(kind));
            TextAnimatorMigrationActionResult result;
            try
            {
                result = TextAnimatorMigrationActions.Run(kind, items, _outputFolder);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            ShowResults(new List<TextAnimatorMigrationActionResult> { result });
        }

        void RunAll()
        {
            if (!CanEdit() || _plan.ReadyCount == 0)
                return;

            StringBuilder message = new StringBuilder("Text Studio will:\n");
            for (int i = 0; i < _plan.Steps.Count; i++)
            {
                TextAnimatorMigrationPlanStep step = _plan.Steps[i];
                if (step.ReadyCount > 0)
                    message.Append("• ").Append(StepButtonText(step.Kind, step.ReadyCount)).Append('\n');
            }

            message.Append("\nConverted assets are saved in ").Append(_outputFolder)
                .Append(". Text Animator components, assets and settings stay unchanged, and changes support Undo.");
            if (!EditorUtility.DisplayDialog("Run one-click conversions", message.ToString(), "Run conversions", "Cancel"))
                return;

            List<TextAnimatorMigrationActionResult> results = new List<TextAnimatorMigrationActionResult>();
            int group = BeginUndoGroup("Text Animator Migration");
            try
            {
                for (int i = 0; i < _plan.Steps.Count; i++)
                {
                    TextAnimatorMigrationPlanStep step = _plan.Steps[i];
                    if (step.ReadyCount > 0)
                        results.Add(TextAnimatorMigrationActions.Run(step.Kind, step.Items, _outputFolder));
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }

            ShowResults(results);
        }

        void ShowResults(List<TextAnimatorMigrationActionResult> results)
        {
            StringBuilder message = new StringBuilder();
            bool changedScenes = false;
            for (int i = 0; i < results.Count; i++)
            {
                if (string.IsNullOrEmpty(results[i].Message))
                    continue;

                if (message.Length > 0)
                    message.Append(' ');
                message.Append(results[i].Message);
                changedScenes |= results[i].ChangedScenes;
            }

            if (changedScenes)
                message.Append(" Save your open scenes to keep the changes.");

            _resultMessage = message.Length > 0 ? "✓ " + message : string.Empty;
            RebuildPlan();
            UpdateView();
        }

        static int BeginUndoGroup(string name)
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName(name);
            return Undo.GetCurrentGroup();
        }

        static Label Copy(string text, params string[] classes)
        {
            Label label = TextStudioInspectorElements.CreateMutedText(text);
            label.AddToClassList("ts-migration-copy");
            CommonUITK.AddClasses(label, classes);
            return label;
        }

        static Label Pill(string text, string tone, string tooltip)
        {
            Label label = CommonUITK.CreateLabel(text, "ts-migration-pill");
            SetPill(label, text, tone, tooltip);
            return label;
        }

        static void SetPill(Label label, string text, string tone, string tooltip)
        {
            label.text = text;
            label.tooltip = tooltip ?? string.Empty;
            label.EnableInClassList("ts-migration-pill--ready", tone == "ready");
            label.EnableInClassList("ts-migration-pill--info", tone == "info");
            label.EnableInClassList("ts-migration-pill--attention", tone == "attention");
            SetDisplay(label, !string.IsNullOrEmpty(text));
        }

        static Button Button(string text, string tooltip, Action action, string name = null)
        {
            Button button = TextStudioInspectorElements.CreateButton(text, tooltip, action);
            button.name = name;
            button.AddToClassList("ts-migration-button");
            return button;
        }

        static Label AddSectionHeading(VisualElement parent, string title)
        {
            VisualElement heading = CommonUITK.CreateContainer("ts-migration-section-heading");
            heading.Add(CommonUITK.CreateLabel(title, "ts-migration-section-title"));
            Label pill = Pill(string.Empty, string.Empty, string.Empty);
            heading.Add(pill);
            parent.Add(heading);
            return pill;
        }

        static void SetDisplay(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static string Count(int count, string singular, string plural)
        {
            return count + " " + (count == 1 ? singular : plural);
        }
    }
}
