using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using ImpossibleRobert.Common.Editor.Packages;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal enum TextStudioIntegrationStatusFilter
    {
        All,
        Ready,
        NeedsSetup,
        CompanionMissing,
        Updates
    }

    internal sealed class TextStudioIntegrationsWindow : EditorWindow
    {
        const string AllCategories = "All integrations";

        ToolbarSearchField _search;
        PopupField<string> _category;
        ToolbarToggle _readyFilter;
        ToolbarToggle _setupFilter;
        ToolbarToggle _missingCompanionFilter;
        ToolbarToggle _updatesFilter;
        Label _comparisonInfo;
        VisualElement _contextRow;
        Label _contextLabel;
        readonly HashSet<string> _expandedPackages = new HashSet<string>(StringComparer.Ordinal);
        ScrollView _cards;
        IReadOnlyList<TextStudioIntegrationState> _states;
        TextStudioIntegrationStatusFilter _statusFilter;
        [SerializeField] string _focusedIntegrationId;

        internal void FocusIntegration(string integrationId)
        {
            _focusedIntegrationId = integrationId;
            _statusFilter = TextStudioIntegrationStatusFilter.All;
            _search?.SetValueWithoutNotify(string.Empty);
            _category?.SetValueWithoutNotify(AllCategories);
            _expandedPackages.Add(integrationId);
            Refresh();
            _cards?.schedule.Execute(() => _cards.scrollOffset = Vector2.zero);
        }

        public static void ShowWindow()
        {
            TextStudioIntegrationsWindow window = GetWindow<TextStudioIntegrationsWindow>();
            window.titleContent = new GUIContent("Text Studio Integrations");
            window.minSize = new Vector2(440f, 420f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            CommonUITK.ApplyWindowLayout(rootVisualElement);

            Label title = new Label("Text Studio Integrations");
            title.style.fontSize = 20f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            rootVisualElement.Add(title);

            Label intro = new Label(
                "Connect your tools. Open an Integration Guide for setup and details.");
            intro.style.whiteSpace = WhiteSpace.Normal;
            intro.style.marginTop = 4f;
            intro.style.marginBottom = 10f;
            rootVisualElement.Add(intro);

            VisualElement toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.marginBottom = 10f;

            _search = new ToolbarSearchField();
            _search.tooltip = "Filter integrations by name, description, or category.";
            _search.style.flexGrow = 1f;
            _search.style.flexBasis = 0f;
            _search.style.minWidth = 200f;
            _search.RegisterValueChangedCallback(_ => { _focusedIntegrationId = null; RebuildCards(); });
            toolbar.Add(_search);

            _category = new PopupField<string>(
                BuildCategories(),
                0,
                value => value,
                value => value);
            _category.name = "integration-category-filter";
            _category.tooltip = "Filter integrations by workflow.";
            _category.RegisterValueChangedCallback(_ => { _focusedIntegrationId = null; RebuildCards(); });
            _category.style.width = 180f;
            _category.style.minWidth = 160f;
            _category.style.flexShrink = 0f;
            _category.style.marginLeft = 6f;
            toolbar.Add(_category);
            rootVisualElement.Add(toolbar);

            _contextRow = new VisualElement { name = "integration-request-context" };
            _contextRow.style.flexDirection = FlexDirection.Row;
            _contextRow.style.marginBottom = 8f;
            _contextLabel = new Label();
            _contextLabel.style.flexGrow = 1f;
            _contextLabel.style.whiteSpace = WhiteSpace.Normal;
            _contextRow.Add(_contextLabel);
            _contextRow.Add(new Button(() => { _focusedIntegrationId = null; RebuildCards(); }) { text = "Show All" });
            rootVisualElement.Add(_contextRow);

            VisualElement statusFilters = new VisualElement();
            statusFilters.name = "integration-status-filters";
            statusFilters.style.flexDirection = FlexDirection.Row;
            statusFilters.style.flexWrap = Wrap.NoWrap;
            statusFilters.style.minHeight = 26f;
            statusFilters.style.marginBottom = 8f;
            _readyFilter = CreateStatusFilter(
                "status-filter-ready",
                TextStudioIntegrationStatusFilter.Ready);
            _setupFilter = CreateStatusFilter(
                "status-filter-needs-setup",
                TextStudioIntegrationStatusFilter.NeedsSetup);
            _missingCompanionFilter = CreateStatusFilter(
                "status-filter-companion-missing",
                TextStudioIntegrationStatusFilter.CompanionMissing);
            statusFilters.Add(_readyFilter);
            statusFilters.Add(_setupFilter);
            statusFilters.Add(_missingCompanionFilter);
            _updatesFilter = CreateStatusFilter("status-filter-updates", TextStudioIntegrationStatusFilter.Updates);
            statusFilters.Add(_updatesFilter);
            rootVisualElement.Add(statusFilters);

            _comparisonInfo = new Label();
            _comparisonInfo.style.whiteSpace = WhiteSpace.Normal;
            _comparisonInfo.style.marginBottom = 8f;
            rootVisualElement.Add(_comparisonInfo);

            _cards = new ScrollView(ScrollViewMode.Vertical);
            _cards.style.flexGrow = 1f;
            rootVisualElement.Add(_cards);

            Refresh();
        }

        void OnSelectionChange()
        {
            Refresh();
        }

        void OnEnable()
        {
            TextStudioIntegrationPackageTracker.Changed += Refresh;
            TextStudioIntegrationPackageTracker.Invalidate();
        }

        void OnDisable()
        {
            TextStudioIntegrationPackageTracker.Changed -= Refresh;
        }

        void Refresh()
        {
            Vector2 scrollOffset = _cards != null ? _cards.scrollOffset : Vector2.zero;
            List<TextStudioIntegrationState> states = new List<TextStudioIntegrationState>(
                TextStudioIntegrationCatalog.Evaluate(
                    Selection.activeGameObject,
                    Selection.activeObject));
            SortStatesForDisplay(states);
            _states = states;
            RebuildCards();
            if (_cards != null)
            {
                _cards.schedule.Execute(() => _cards.scrollOffset = scrollOffset);
            }
        }

        internal static void SortStatesForDisplay(List<TextStudioIntegrationState> states)
        {
            states.Sort(CompareStatesForDisplay);
        }

        static int CompareStatesForDisplay(
            TextStudioIntegrationState left,
            TextStudioIntegrationState right)
        {
            int displayNameComparison = StringComparer.OrdinalIgnoreCase.Compare(
                left.Definition.DisplayName,
                right.Definition.DisplayName);
            return displayNameComparison != 0
                ? displayNameComparison
                : StringComparer.Ordinal.Compare(left.Definition.Id, right.Definition.Id);
        }

        List<string> BuildCategories()
        {
            IReadOnlyList<TextStudioIntegrationDefinition> definitions =
                TextStudioIntegrationCatalog.GetDefinitions();
            List<string> categories = new List<string> { AllCategories };
            for (int i = 0; i < definitions.Count; i++)
            {
                string category = definitions[i].Category;
                if (!categories.Contains(category))
                    categories.Add(category);
            }

            categories.Sort(1, categories.Count - 1, StringComparer.OrdinalIgnoreCase);
            return categories;
        }

        void RebuildCards()
        {
            if (_cards == null)
                return;

            string focusedName = (rootVisualElement.focusController?.focusedElement as VisualElement)?.name;
            _cards.Clear();
            if (_states == null)
                return;

            bool hasContext = !string.IsNullOrEmpty(_focusedIntegrationId);
            _contextRow.style.display = hasContext ? DisplayStyle.Flex : DisplayStyle.None;

            string search = _search?.value?.Trim() ?? string.Empty;
            string category = _category?.value ?? AllCategories;
            int visibleCount = 0;
            int availableCount = 0;
            int actionCount = 0;
            int missingCompanionCount = 0;
            int updateCount = 0;
            int packageCount = 0;
            int unavailableCount = 0;

            for (int i = 0; i < _states.Count; i++)
            {
                TextStudioIntegrationState state = _states[i];
                if (hasContext && state.Definition.Id == _focusedIntegrationId)
                    _contextLabel.text = state.Definition.DisplayName + " integration";
                if (state.Package != null && state.Package.IsUpdate)
                    updateCount++;
                if (state.Package != null && state.Package.Status != TextStudioPackageStatus.NotApplicable)
                {
                    packageCount++;
                    if (state.Package.Status == TextStudioPackageStatus.Unavailable)
                        unavailableCount++;
                }
                TextStudioIntegrationStatusFilter stateStatus = GetStatusFilter(state);
                switch (stateStatus)
                {
                    case TextStudioIntegrationStatusFilter.Ready:
                        availableCount++;
                        break;
                    case TextStudioIntegrationStatusFilter.NeedsSetup:
                        actionCount++;
                        break;
                    case TextStudioIntegrationStatusFilter.CompanionMissing:
                        missingCompanionCount++;
                        break;
                }

                if ((hasContext && state.Definition.Id != _focusedIntegrationId) ||
                    !Matches(state.Definition, search, category) ||
                    !MatchesStatus(state, _statusFilter))
                    continue;

                _cards.Add(CreateCard(state));
                visibleCount++;
            }

            if (visibleCount == 0)
            {
                Label empty = new Label(_statusFilter == TextStudioIntegrationStatusFilter.Updates
                    ? packageCount > 0 && unavailableCount == packageCount
                        ? "No bundled packages to compare yet."
                        : "No bundled updates found. Packages are checked automatically when this window opens."
                    : "No integrations match the current filters.");
                empty.style.unityTextAlign = TextAnchor.MiddleCenter;
                empty.style.marginTop = 24f;
                _cards.Add(empty);
            }

            UpdateStatusFilter(
                _readyFilter,
                TextStudioIntegrationStatusFilter.Ready,
                availableCount + " ready",
                "ready integrations");
            UpdateStatusFilter(
                _setupFilter,
                TextStudioIntegrationStatusFilter.NeedsSetup,
                actionCount + " need setup",
                "integrations that need setup");
            UpdateStatusFilter(
                _missingCompanionFilter,
                TextStudioIntegrationStatusFilter.CompanionMissing,
                missingCompanionCount + " companions missing",
                "integrations whose required companion is missing");
            UpdateStatusFilter(_updatesFilter, TextStudioIntegrationStatusFilter.Updates, "Updates (" + updateCount + ")", "integrations with a confirmed bundled update");
            if (_comparisonInfo != null)
            {
                bool unavailable = packageCount > 0 && unavailableCount == packageCount;
                _comparisonInfo.text = unavailable ? "Local package comparison becomes available when bundled integration archives are installed." : string.Empty;
                _comparisonInfo.style.display = unavailable ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (!string.IsNullOrEmpty(focusedName))
                _cards.schedule.Execute(() => _cards.Q<VisualElement>(focusedName)?.Focus());
        }

        ToolbarToggle CreateStatusFilter(
            string name,
            TextStudioIntegrationStatusFilter filter)
        {
            ToolbarToggle toggle = new ToolbarToggle { name = name };
            toggle.style.flexShrink = 0f;
            toggle.style.marginRight = 4f;
            toggle.style.marginBottom = 2f;
            toggle.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    ApplyStatusFilter(filter);
                else if (_statusFilter == filter)
                    ApplyStatusFilter(TextStudioIntegrationStatusFilter.All);
            });
            return toggle;
        }

        void ApplyStatusFilter(TextStudioIntegrationStatusFilter filter)
        {
            if (_statusFilter == filter)
                return;

            _focusedIntegrationId = null;
            _statusFilter = filter;
            RebuildCards();
        }

        void UpdateStatusFilter(
            ToolbarToggle toggle,
            TextStudioIntegrationStatusFilter filter,
            string text,
            string description)
        {
            if (toggle == null)
                return;

            bool selected = _statusFilter == filter;
            toggle.text = text;
            toggle.SetValueWithoutNotify(selected);
            toggle.tooltip = selected
                ? "Showing only " + description + ". Click again to show every status."
                : "Show only " + description + ". This combines with the search and category filters.";
        }

        VisualElement CreateCard(TextStudioIntegrationState state)
        {
            TextStudioIntegrationDefinition definition = state.Definition;
            VisualElement card = new VisualElement();
            card.name = "integration-" + definition.Id;
            card.style.borderTopWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            Color border = EditorGUIUtility.isProSkin
                ? new Color(0.28f, 0.28f, 0.28f)
                : new Color(0.68f, 0.68f, 0.68f);
            card.style.borderTopColor = border;
            card.style.borderRightColor = border;
            card.style.borderBottomColor = border;
            card.style.borderLeftColor = border;
            card.style.borderTopLeftRadius = 5f;
            card.style.borderTopRightRadius = 5f;
            card.style.borderBottomLeftRadius = 5f;
            card.style.borderBottomRightRadius = 5f;
            card.style.paddingLeft = 10f;
            card.style.paddingRight = 10f;
            card.style.paddingTop = 8f;
            card.style.paddingBottom = 8f;
            card.style.marginBottom = 8f;

            VisualElement header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.flexWrap = Wrap.Wrap;

            Label name = new Label(definition.DisplayName);
            name.style.fontSize = 14f;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1f;
            name.style.minWidth = 0f;
            name.style.flexBasis = 190f;
            name.style.whiteSpace = WhiteSpace.Normal;
            header.Add(name);

            Label status = new Label(GetStatusText(state));
            status.tooltip = GetStatusTooltip(state) +
                (state.Package?.Status == TextStudioPackageStatus.Current ? " Installed files match the bundled package." : string.Empty);
            status.style.unityTextAlign = TextAnchor.MiddleCenter;
            status.style.paddingLeft = 8f;
            status.style.paddingRight = 8f;
            status.style.paddingTop = 2f;
            status.style.paddingBottom = 2f;
            status.style.borderTopLeftRadius = 9f;
            status.style.borderTopRightRadius = 9f;
            status.style.borderBottomLeftRadius = 9f;
            status.style.borderBottomRightRadius = 9f;
            status.style.backgroundColor = GetStatusColor(state);
            status.style.marginBottom = 2f;
            header.Add(status);
            card.Add(header);

            if (state.Package != null && state.Package.Status != TextStudioPackageStatus.NotApplicable &&
                state.Package.Status != TextStudioPackageStatus.Current && state.Package.Status != TextStudioPackageStatus.Unavailable)
            {
                Label packageStatus = new Label(GetPackageStatusText(state.Package));
                packageStatus.tooltip = state.Package.Detail;
                packageStatus.style.whiteSpace = WhiteSpace.Normal;
                packageStatus.style.marginTop = 4f;
                packageStatus.style.alignSelf = Align.FlexStart;
                packageStatus.style.unityFontStyleAndWeight = FontStyle.Bold;
                packageStatus.style.paddingLeft = 8f;
                packageStatus.style.paddingRight = 8f;
                packageStatus.style.paddingTop = 2f;
                packageStatus.style.paddingBottom = 2f;
                packageStatus.style.borderTopLeftRadius = 9f;
                packageStatus.style.borderTopRightRadius = 9f;
                packageStatus.style.borderBottomLeftRadius = 9f;
                packageStatus.style.borderBottomRightRadius = 9f;
                bool error = state.Package.Status == TextStudioPackageStatus.InvalidBundle ||
                    state.Package.Status == TextStudioPackageStatus.IncompleteImport;
                bool neutral = state.Package.Status == TextStudioPackageStatus.Checking ||
                    state.Package.Status == TextStudioPackageStatus.Unknown;
                packageStatus.style.backgroundColor = EditorGUIUtility.isProSkin
                    ? error ? new Color(0.45f, 0.20f, 0.19f) : neutral ? new Color(0.30f, 0.30f, 0.30f) : new Color(0.43f, 0.33f, 0.16f)
                    : error ? new Color(0.96f, 0.75f, 0.73f) : neutral ? new Color(0.86f, 0.86f, 0.86f) : new Color(0.96f, 0.86f, 0.63f);
                packageStatus.style.color = EditorGUIUtility.isProSkin ? Color.white : new Color(0.17f, 0.14f, 0.09f);
                card.Add(packageStatus);
            }

            Label description = new Label(definition.Description);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 6f;
            card.Add(description);

            if (state.CompatibilityNotice == null && (!state.CompanionInstalled || !state.DependencySatisfied || !state.IntegrationInstalled))
            {
                Label quickStart = new Label(GetNextStepText(state));
                quickStart.style.whiteSpace = WhiteSpace.Normal;
                quickStart.style.marginTop = 6f;
                quickStart.style.opacity = 0.88f;
                card.Add(quickStart);
            }

            if (state.HasWarnings || state.HasErrors || state.CompatibilityNotice != null)
            {
                VisualElement issues = new VisualElement();
                issues.style.marginTop = 6f;
                string selectionName = state.CompatibilityNotice != null ? "Compatibility" : Selection.activeObject != null
                    ? "Selected: " + Selection.activeObject.name
                    : "Setup check";
                Label issueHeading = new Label(selectionName);
                issueHeading.style.whiteSpace = WhiteSpace.Normal;
                issueHeading.style.unityFontStyleAndWeight = FontStyle.Bold;
                issueHeading.style.marginBottom = 2f;
                issues.Add(issueHeading);
                for (int i = 0; i < state.Issues.Count; i++)
                {
                    TextStudioIntegrationIssue issue = state.Issues[i];
                    if (issue.Severity == TextStudioIntegrationIssueSeverity.Info && !issue.BlocksPackageImport)
                        continue;
                    Label issueLabel = new Label("• " + issue.Title + ": " + issue.Detail);
                    issueLabel.tooltip = issue.Action;
                    issueLabel.style.whiteSpace = WhiteSpace.Normal;
                    issueLabel.style.color = GetIssueColor(issue.Severity);
                    issues.Add(issueLabel);
                }

                card.Add(issues);
            }

            VisualElement actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 8f;

            if (ShouldOfferNestedPackageImport(state))
            {
                bool canImport = CanImportNestedPackage(state);
                bool importsSample = ImportsSamplePackage(state);
                bool firstImport = !state.IntegrationInstalled ||
                    importsSample && string.IsNullOrEmpty(state.SampleAssetPath) && state.Package?.Installed == null;
                bool needsReview = !firstImport && state.Package != null && state.Package.Status != TextStudioPackageStatus.UpdateAvailable &&
                    state.Package.Status != TextStudioPackageStatus.Current && state.Package.Status != TextStudioPackageStatus.Unavailable;
                bool expanded = _expandedPackages.Contains(definition.Id);
                string label = needsReview && !expanded ? "Review Package" :
                    state.Package != null && state.Package.Status == TextStudioPackageStatus.UpdateAvailable
                        ? importsSample ? "Update Sample" : "Update Integration"
                        : expanded ? importsSample ? "Reimport Sample" : "Reimport Integration"
                        : importsSample ? "Import Sample" : "Import Integration";
                string tooltip = canImport
                    ? importsSample
                        ? "Import the optional sample package for this ready adapter."
                        : "Import the nested Text Studio integration package."
                    : state.HasPackageImportBlocker
                        ? "This package requires a different companion version or edition. See Compatibility for details."
                    : !state.DependencySatisfied
                        ? "Import the required Text Studio integration before this dependent package."
                        : "Install " + definition.CompanionDisplayName + " before importing this optional package.";
                Button importButton = CreateButton(
                    label,
                    () =>
                    {
                        if (needsReview && !_expandedPackages.Contains(definition.Id))
                        {
                            _expandedPackages.Add(definition.Id);
                            Refresh();
                        }
                        else
                            ImportIntegration(state);
                    },
                    tooltip,
                    canImport);
                importButton.name = "import-package-" + definition.Id;
                actions.Add(importButton);
            }

            TextStudioIntegrationValidationContext context =
                new TextStudioIntegrationValidationContext(
                    definition,
                    Selection.activeGameObject,
                    Selection.activeObject);
            if (state.Provider != null)
            {
                bool canConfigure = state.IsAvailable && state.Provider.CanConfigure(context);
                actions.Add(CreateButton(
                    "Configure Selected",
                    () => Configure(state),
                    canConfigure
                        ? "Safely configure the selected object for this integration."
                        : "Select a compatible scene object or asset after the companion and Text Studio adapter are ready.",
                    canConfigure));
            }

            if (!string.IsNullOrEmpty(definition.SampleAssetName))
            {
                bool canOpenSample = CanOpenSample(state);
                actions.Add(CreateButton(
                    "Open Sample",
                    () => OpenSample(state.SampleAssetPath),
                    canOpenSample
                        ? "Open the persisted integration sample scene."
                        : state.CompatibilityNotice != null ? state.CompatibilityNotice.Detail
                        : "Install the companion and activate the Text Studio adapter before opening this sample.",
                    canOpenSample));
            }

            if (!string.IsNullOrEmpty(definition.DocumentationUrl))
            {
                actions.Add(CreateButton(
                    "Integration Guide",
                    () => Application.OpenURL(definition.DocumentationUrl),
                    "Open this integration's capabilities, third-party coverage, installation, setup, validation, and troubleshooting guide."));
            }

            card.Add(actions);
            if (state.Package != null && state.Package.Status != TextStudioPackageStatus.NotApplicable &&
                state.Package.Status != TextStudioPackageStatus.Unavailable && state.Package.Status != TextStudioPackageStatus.Current)
            {
                Foldout details = new Foldout { text = "Package Details", value = _expandedPackages.Contains(definition.Id) };
                details.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) _expandedPackages.Add(definition.Id);
                    else _expandedPackages.Remove(definition.Id);
                    Refresh();
                });
                AddPackageDetail(details, state.Package.Detail ?? string.Empty);
                if (state.Package.Available != null)
                    AddPackageDetail(details, "Bundled with " + state.Package.Available.ownerPackageName + " " + state.Package.Available.ownerVersion);
                if (state.Package.Installed != null)
                    AddPackageDetail(details, "Installed from " + state.Package.Installed.ownerVersion);
                foreach (string path in state.Package.Changed)
                    AddPackageDetail(details, "Changed: " + path);
                foreach (string path in state.Package.Missing)
                    AddPackageDetail(details, "Missing: " + path);
                foreach (string path in state.Package.Retired)
                    AddPackageDetail(details, "Earlier package file remains: " + path);
                card.Add(details);
            }
            return card;
        }

        static void AddPackageDetail(Foldout details, string text)
        {
            Label line = new Label(text);
            line.style.whiteSpace = WhiteSpace.Normal;
            line.style.minWidth = 0f;
            details.Add(line);
        }

        static string GetPackageStatusText(TextStudioPackageSnapshot package)
        {
            if (package.IsUpdate && package.Status != TextStudioPackageStatus.UpdateAvailable)
                return "Update available · " + GetPackageStatusTextWithoutUpdate(package);
            return GetPackageStatusTextWithoutUpdate(package);
        }

        static string GetPackageStatusTextWithoutUpdate(TextStudioPackageSnapshot package)
        {
            switch (package.Status)
            {
                case TextStudioPackageStatus.Checking: return "Checking package";
                case TextStudioPackageStatus.UpdateAvailable: return "Update available";
                case TextStudioPackageStatus.BundledPackageDiffers: return "Bundled package differs";
                case TextStudioPackageStatus.InstalledNewer: return "Installed from newer release";
                case TextStudioPackageStatus.LocalChanges: return "Local changes";
                case TextStudioPackageStatus.IncompleteImport: return "Incomplete import";
                case TextStudioPackageStatus.RetiredFiles: return "Earlier package files remain";
                case TextStudioPackageStatus.InvalidBundle: return "Package metadata needs attention";
                default: return "Update status unknown";
            }
        }

        static Button CreateButton(
            string text,
            Action action,
            string tooltip,
            bool enabled = true)
        {
            Button button = new Button(action) { text = text, tooltip = tooltip };
            button.style.marginRight = 4f;
            button.style.marginBottom = 3f;
            button.SetEnabled(enabled);
            return button;
        }

        void ImportIntegration(TextStudioIntegrationState state)
        {
            if (string.IsNullOrEmpty(state.NestedPackageAssetPath))
                return;

            TextStudioIntegrationState latest = null;
            foreach (TextStudioIntegrationState candidate in TextStudioIntegrationCatalog.Evaluate(Selection.activeGameObject, Selection.activeObject))
            {
                if (candidate.Definition.Id == state.Definition.Id)
                {
                    latest = candidate;
                    break;
                }
            }
            if (!CanImportNestedPackage(latest))
                return;
            if (latest.Package != null && latest.Package.Available != null)
            {
                string sidecar = latest.NestedPackageAssetPath + ".manifest.json";
                if (!File.Exists(sidecar) ||
                    !IntegrationPackageDescriptor.TryRead(File.ReadAllText(sidecar), out IntegrationPackageDescriptor descriptor, out _) ||
                    !string.Equals(JsonUtility.ToJson(descriptor.manifest), JsonUtility.ToJson(latest.Package.Available), StringComparison.Ordinal) ||
                    !File.Exists(latest.NestedPackageAssetPath) ||
                    !string.Equals(IntegrationPackageFingerprint.HashFile(latest.NestedPackageAssetPath), descriptor.archiveSha256, StringComparison.Ordinal))
                {
                    TextStudioIntegrationPackageTracker.Invalidate();
                    return;
                }
            }

            TextStudioIntegrationPackageTracker.RememberOldInventory(latest.Package);
            UnityEditorCompat.ImportPackage(latest.NestedPackageAssetPath, true);
        }

        void Configure(TextStudioIntegrationState state)
        {
            TextStudioIntegrationValidationContext context =
                new TextStudioIntegrationValidationContext(
                    state.Definition,
                    Selection.activeGameObject,
                    Selection.activeObject);
            state.Provider.Configure(context);
            Refresh();
        }

        static void OpenSample(string path)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(path);
        }

        internal static bool CanOpenSample(TextStudioIntegrationState state)
        {
            return state != null &&
                   state.IsAvailable &&
                   !state.HasPackageImportBlocker &&
                   !string.IsNullOrEmpty(state.SampleAssetPath);
        }

        internal static bool ShouldOfferNestedPackageImport(TextStudioIntegrationState state)
        {
            if (state == null || string.IsNullOrEmpty(state.NestedPackageAssetPath))
                return false;
            if (!state.IntegrationInstalled)
                return true;

            if (state.Package != null && state.Package.Status != TextStudioPackageStatus.Current &&
                state.Package.Status != TextStudioPackageStatus.NotApplicable &&
                state.Package.Status != TextStudioPackageStatus.Unavailable &&
                state.Package.Status != TextStudioPackageStatus.Checking &&
                state.Package.Status != TextStudioPackageStatus.InvalidBundle)
                return true;

            return ImportsSamplePackage(state) &&
                   string.IsNullOrEmpty(state.SampleAssetPath);
        }

        internal static bool CanImportNestedPackage(TextStudioIntegrationState state)
        {
            return ShouldOfferNestedPackageImport(state) &&
                   state.CompanionInstalled &&
                   state.DependencySatisfied &&
                   (state.Package == null || state.Package.Status != TextStudioPackageStatus.InvalidBundle &&
                    state.Package.Status != TextStudioPackageStatus.Checking) &&
                   !state.HasPackageImportBlocker;
        }

        static bool ImportsSamplePackage(TextStudioIntegrationState state)
        {
            if (state?.Package?.Available != null)
                return state.Package.Available.payloadKind == IntegrationPackagePayloadKind.Samples;
            if (state == null)
                return false;
            string id = state.Definition.Id;
            return TextStudioIntegrationCatalog.IsSamplePackage(state.Definition) ||
                   id == "timeline" || id == "unity-splines" || id == "prime-tween" ||
                   id == "3d-unity-splines";
        }

        static bool Matches(
            TextStudioIntegrationDefinition definition,
            string search,
            string category)
        {
            if (!string.Equals(category, AllCategories, StringComparison.Ordinal) &&
                !string.Equals(definition.Category, category, StringComparison.Ordinal))
            {
                return false;
            }

            if (string.IsNullOrEmpty(search))
                return true;

            return definition.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   definition.CompanionDisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   definition.IntegrationDisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   definition.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   definition.Category.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool MatchesStatus(
            TextStudioIntegrationState state,
            TextStudioIntegrationStatusFilter filter)
        {
            return state != null &&
                   (filter == TextStudioIntegrationStatusFilter.All ||
                    filter == TextStudioIntegrationStatusFilter.Updates && state.Package != null && state.Package.IsUpdate ||
                    GetStatusFilter(state) == filter);
        }

        static TextStudioIntegrationStatusFilter GetStatusFilter(
            TextStudioIntegrationState state)
        {
            if (state.CompatibilityNotice != null)
                return TextStudioIntegrationStatusFilter.All;
            if (state.IsAvailable)
                return TextStudioIntegrationStatusFilter.Ready;
            return state.CompanionInstalled
                ? TextStudioIntegrationStatusFilter.NeedsSetup
                : TextStudioIntegrationStatusFilter.CompanionMissing;
        }

        internal static string GetStatusText(TextStudioIntegrationState state)
        {
            if (state == null)
                return "Unavailable";
            if (state.CompatibilityNotice != null)
                return "Different edition";
            if (state.IsAvailable)
                return "Ready";
            if (!state.CompanionInstalled)
                return "Companion missing";
            if (!state.DependencySatisfied)
                return "Dependency required";
            if (!state.IntegrationInstalled)
            {
                if (string.IsNullOrEmpty(state.Definition.NestedPackageFileName))
                    return "Adapter unavailable";
                if (string.IsNullOrEmpty(state.NestedPackageAssetPath))
                    return TextStudioIntegrationCatalog.IsSamplePackage(state.Definition)
                        ? "Sample not found"
                        : "Package not found";
                return TextStudioIntegrationCatalog.IsSamplePackage(state.Definition)
                    ? "Sample available"
                    : "Import available";
            }

            return state.HasErrors ? "Needs attention" : "Needs setup";
        }

        static string GetStatusTooltip(TextStudioIntegrationState state)
        {
            string tooltip = GetCompanionStatusText(state) + ".\n" +
                             GetIntegrationStatusText(state) + ".\n" +
                             Nicify(state.Definition.SupportLevel) + " • " +
                             Nicify(state.Definition.Provenance);
            for (int i = 0; i < state.Issues.Count; i++)
            {
                TextStudioIntegrationIssue issue = state.Issues[i];
                if (issue.Severity == TextStudioIntegrationIssueSeverity.Info)
                    tooltip += "\n" + issue.Title + ": " + issue.Detail;
            }

            return tooltip;
        }

        internal static string GetCompanionStatusText(TextStudioIntegrationState state)
        {
            if (state == null)
                return "Companion unavailable";

            string version = string.IsNullOrEmpty(state.CompanionVersion)
                ? string.Empty
                : " " + state.CompanionVersion;
            return state.CompanionInstalled
                ? state.Definition.CompanionDisplayName + version + " installed"
                : state.Definition.CompanionDisplayName + " not installed";
        }

        internal static string GetIntegrationStatusText(TextStudioIntegrationState state)
        {
            if (state == null)
                return "Text Studio adapter unavailable";

            string name = state.Definition.IntegrationDisplayName;
            if (TextStudioIntegrationCatalog.IsSamplePackage(state.Definition))
                return name + (state.IntegrationInstalled ? " imported" : " not imported");
            if (state.IntegrationLoaded)
                return name + " active";
            return name + (state.IntegrationInstalled ? " imported, not loaded" : " not imported");
        }

        internal static string GetNextStepText(TextStudioIntegrationState state)
        {
            if (state == null)
                return string.Empty;
            if (!state.CompanionInstalled)
                return "Install " + state.Definition.CompanionDisplayName + ", then return here.";
            if (!state.DependencySatisfied)
                return "Import and activate the required Text Studio integration first.";
            if (!state.IntegrationInstalled)
            {
                if (!string.IsNullOrEmpty(state.NestedPackageAssetPath))
                {
                    return TextStudioIntegrationCatalog.IsSamplePackage(state.Definition)
                        ? "Choose Import Sample."
                        : "Choose Import Integration.";
                }

                string ownerName = TextStudioIntegrationCatalog.GetPackageDisplayName(
                    state.Definition.NestedPackageOwnerPackageName);
                return "Reinstall or update " + ownerName +
                       ", then use the Integration Guide if the optional package is still missing.";
            }

            if (ShouldOfferNestedPackageImport(state))
                return "Choose Import Sample to add the optional showcase.";
            return state.Definition.QuickStart;
        }

        static Color GetStatusColor(TextStudioIntegrationState state)
        {
            if (state.CompatibilityNotice != null)
                return EditorGUIUtility.isProSkin ? new Color(0.27f, 0.27f, 0.27f, 0.9f) : new Color(0.72f, 0.72f, 0.72f, 0.9f);
            if (state.IsAvailable)
                return new Color(0.20f, 0.52f, 0.30f, 0.9f);
            if (state.HasErrors)
                return new Color(0.68f, 0.25f, 0.22f, 0.9f);
            if (state.CompanionInstalled)
                return new Color(0.68f, 0.48f, 0.14f, 0.9f);
            return EditorGUIUtility.isProSkin
                ? new Color(0.27f, 0.27f, 0.27f, 0.9f)
                : new Color(0.72f, 0.72f, 0.72f, 0.9f);
        }

        static Color GetIssueColor(TextStudioIntegrationIssueSeverity severity)
        {
            return severity switch
            {
                TextStudioIntegrationIssueSeverity.Error => new Color(0.93f, 0.34f, 0.30f),
                TextStudioIntegrationIssueSeverity.Warning => new Color(0.93f, 0.68f, 0.25f),
                _ => EditorGUIUtility.isProSkin ? new Color(0.76f, 0.76f, 0.76f) : Color.gray
            };
        }

        static string Nicify<T>(T value) where T : Enum
        {
            return ObjectNames.NicifyVariableName(value.ToString());
        }
    }
}
