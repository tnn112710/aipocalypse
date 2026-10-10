using System;
using System.Collections.Generic;
using System.Linq;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    /// <summary>Project overview of prepared runtime data. Open scenes are checked live; saved build content is checked
    /// once per editor session and refreshed when its files change. Presentation never selects preparation work.</summary>
#if UNITY_6000_7_OR_NEWER
    // Add-on authoring callbacks and the session's build-content check stay available throughout the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioRuntimePreparationWindow : EditorWindow
    {
        const int OverviewTab = 0, DataTab = 1, SettingsTab = 2;
        const string SnapshotKey = "TextStudio.RuntimePreparation.BuildContent", DeclinedKey = "TextStudio.RuntimePreparation.CheckDeclined";
        const double RecheckSeconds = 120;

        internal static event Action<TextStudioText, VisualElement, Action<TextStudioText>> BuildAddonSettings;

        static BuildContentSnapshot s_buildContent;
        static readonly HashSet<string> s_stalePaths = new HashSet<string>(StringComparer.Ordinal);
        static bool s_recheckBuildContent;

        [SerializeField] TextStudioText _target;
        [SerializeField] bool _openScenesOnly;
        [SerializeField] int _tab;
        [SerializeField] bool _detailsOpen;
        [SerializeField] List<string> _openGroups = new List<string>();

        VisualElement _root;
        CommonTabbedPane _tabs;
        ScrollView _overviewScroll, _dataScroll, _settingsScroll;
        TextStudioPreparationReport _live, _operation;
        ISet<string> _operationPaths;
        bool _operationPrepares, _operationIncludesBuild, _canceledByUser, _wasBusy, _liveDirty = true, _visible = true, _focusPending, _checkRequested;
        double _liveDue;
        int _renderedSignature, _combinedInputs;
        readonly Dictionary<string, string> _failures = new Dictionary<string, string>(StringComparer.Ordinal);
        List<TextStudioPreparationItem> _items = new List<TextStudioPreparationItem>();
        string[] _keys = Array.Empty<string>();
        TextStudioPreparationSummary _summary;

        [MenuItem(TextStudioMenuPaths.RuntimePreparation, false, TextStudioMenuPaths.RuntimePreparationPriority)]
        internal static void Open() => Open(null);

        /// <summary>Opens the project overview. A supplied text is highlighted in Details.</summary>
        internal static void Open(TextStudioText target) => OpenWindow(target, false);

        /// <summary>Opens Settings with the selected text's expert preparation controls.</summary>
        internal static void OpenAdvanced() =>
            OpenWindow(Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<TextStudioText>() : null, true);

        static void OpenWindow(TextStudioText target, bool settings)
        {
            TextStudioRuntimePreparationWindow window = GetWindow<TextStudioRuntimePreparationWindow>();
            window.titleContent = new GUIContent("Runtime Preparation");
            window.minSize = new Vector2(380, 460);
            window._target = target;
            window._tab = settings ? SettingsTab : OverviewTab;
            window._focusPending = target != null && !settings;
            window._expandAddons = settings && target != null;
            if (window._focusPending) window._detailsOpen = true;
            window._checkRequested = true;
            window._liveDirty = true;
            window._liveDue = 0;
            window.Show();
            if (window._root == null) return;
            window.ShowTab(window._tab);
            window.ApplyExpertTarget();
            window._renderedSignature = 0;
            window.Tick();
        }

        void OnEnable()
        {
            EditorApplication.hierarchyChanged += OnContentChanged;
            EditorApplication.projectChanged += OnContentChanged;
            ObjectChangeEvents.changesPublished += OnObjectsChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            PrefabStage.prefabSaved += OnPrefabSaved;
            EditorBuildSettings.sceneListChanged += OnBuildScenesChanged;
        }

        void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnContentChanged;
            EditorApplication.projectChanged -= OnContentChanged;
            ObjectChangeEvents.changesPublished -= OnObjectsChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            PrefabStage.prefabSaved -= OnPrefabSaved;
            EditorBuildSettings.sceneListChanged -= OnBuildScenesChanged;
            ReleaseExpertEditor();
        }

        void OnBecameVisible()
        {
            _visible = true;
            _renderedSignature = 0;
        }

        void OnBecameInvisible() => _visible = false;
        void OnContentChanged() => MarkLiveDirty(.75);
        void OnObjectsChanged(ref ObjectChangeEventStream stream) => MarkLiveDirty(.75);
        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!EditorSceneManager.IsPreviewScene(scene)) MarkLiveDirty(0);
        }

        void OnSceneClosed(Scene scene)
        {
            // Preparation and checks read build files in preview scenes; closing those does not change saved content.
            if (EditorSceneManager.IsPreviewScene(scene)) return;
            MarkLiveDirty(0);
            // A closed build scene returns to its saved state, which the next quiet moment checks again.
            if (!string.IsNullOrEmpty(scene.path) && EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == scene.path)) s_stalePaths.Add(scene.path);
        }

        void OnPrefabSaved(GameObject prefab)
        {
            string path = AssetDatabase.GetAssetPath(prefab);
            if (BuildContent.Items.Any(item => item.SourcePath == path)) s_stalePaths.Add(path);
        }

        static void OnBuildScenesChanged() => s_recheckBuildContent = true;

        void MarkLiveDirty(double delay)
        {
            _liveDirty = true;
            _liveDue = EditorApplication.timeSinceStartup + delay;
        }

        void CreateGUI()
        {
            // Unity can rebuild the GUI of a live window; new controls must not inherit cached render state.
            rootVisualElement.Clear();
            _cards.Clear();
            _settingsState = -1;
            _combinedInputs = 0;
            _expertBuiltFor = null;
            StyleSheet style = CommonUITK.LoadStyleSheetFromAnchor("TextStudioRuntimePreparationWindow", "/Preparation/TextStudioRuntimePreparationWindow.cs", "/Preparation/TextStudioRuntimePreparationWindow.uss");
            _root = TextStudioInspectorElements.CreateRoot("ts-preparation-root", style, "ts-preparation-window");
            rootVisualElement.Add(_root);
            VisualElement masthead = CommonUITK.CreateContainer("ts-preparation-masthead");
            masthead.Add(CommonUITK.CreateLogoHeader("Runtime Preparation", CommonUIStyles.LoadTexture("TextStudio"), 30, "ts-preparation-header", "ts-preparation-logo", "ts-preparation-title"));
            masthead.Add(Copy("Prepare text data ahead of time so Play Mode and your builds skip that work while running. Editing is not affected.", "ts-preparation-subtitle"));
            _root.Add(masthead);
            _tabs = new CommonTabbedPane(new CommonTabbedPane.TabbedPaneClasses
            { RootClass = "ts-preparation-tabs", TabStripClass = "ts-preparation-tab-strip", TabClass = "ts-preparation-tab", SelectedTabClass = "ts-preparation-tab--selected", BodyClass = "ts-preparation-tab-body" });
            _tabs.SetTabs(new[]
            {
                new GUIContent("Overview", "See how much of your project's text is prepared and prepare the rest in one step."),
                new GUIContent("Prepared data", "Browse the saved assets that hold prepared data, with their size and status."),
                new GUIContent("Settings", "Automation, font permissions, runtime text and expert settings for individual text objects.")
            }, _tab, SelectTab);
            _root.Add(_tabs);
            _overviewScroll = CreateScroll("preparation-scroll");
            _dataScroll = CreateScroll("preparation-data-scroll");
            _settingsScroll = CreateScroll("preparation-settings-scroll");
            BuildOverview(_overviewScroll.contentContainer);
            BuildInventory(_dataScroll.contentContainer);
            BuildSettings(_settingsScroll.contentContainer);
            ShowTab(_tab);
            _renderedSignature = 0;
            _root.schedule.Execute(Tick).Every(250);
            Tick();
        }

        ScrollView CreateScroll(string name)
        {
            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical) { name = name, horizontalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("ts-preparation-scroll");
            scroll.contentContainer.AddToClassList("ts-preparation-scroll-content");
            _tabs.Body.Add(scroll);
            return scroll;
        }

        void SelectTab(int index)
        {
            _tab = index;
            ShowTab(index);
            _renderedSignature = 0;
            Tick();
        }

        void ShowTab(int index)
        {
            _tabs.SetSelectedIndexWithoutNotify(index);
            _overviewScroll.style.display = index == OverviewTab ? DisplayStyle.Flex : DisplayStyle.None;
            _dataScroll.style.display = index == DataTab ? DisplayStyle.Flex : DisplayStyle.None;
            _settingsScroll.style.display = index == SettingsTab ? DisplayStyle.Flex : DisplayStyle.None;
            if (index == DataTab) RefreshInventory(_saved == null);
            if (index == SettingsTab) ApplyExpertTarget();
        }

        void Tick()
        {
            if (_root == null) return;
            bool busy = TextStudioRuntimePreparation.IsBusy;
            if (_operation != null && !busy) Harvest();
            else if (_operation == null && _wasBusy && !busy) MarkLiveDirty(0);
            _wasBusy = busy;
            // A hidden window keeps its operation bookkeeping but defers analysis and rendering until it is shown again.
            if (!_visible) return;
            bool idle = !busy && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating;
            if (idle && (_live == null || _liveDirty && EditorApplication.timeSinceStartup >= _liveDue)) AnalyzeLive();
            if (idle) StartAutomaticCheck();
            Render();
        }

        void AnalyzeLive()
        {
            _liveDirty = false;
            _live = TextStudioRuntimePreparation.Analyze(TextStudioRuntimePreparation.OpenTexts());
        }

        void StartAutomaticCheck()
        {
            bool requested = _checkRequested;
            _checkRequested = false;
            if (_openScenesOnly) return;
            BuildContentSnapshot content = BuildContent;
            bool firstCheck = !content.Checked && (requested || !SessionState.GetBool(DeclinedKey, false));
            bool refresh = content.Checked && (s_recheckBuildContent || requested && content.AgeSeconds > RecheckSeconds);
            if (firstCheck || refresh)
            {
                s_recheckBuildContent = false;
                StartCheck(null);
            }
            else if (content.Checked && s_stalePaths.Count > 0) StartCheck(new HashSet<string>(s_stalePaths, StringComparer.Ordinal));
        }

        void StartCheck(ISet<string> paths)
        {
            if (!TextStudioRuntimePreparation.CheckBuildContent(paths)) return;
            if (paths == null) s_stalePaths.Clear();
            else s_stalePaths.ExceptWith(paths);
            Own(TextStudioRuntimePreparation.LastReport, false, true, paths);
        }

        void Own(TextStudioPreparationReport report, bool prepares, bool includesBuild, ISet<string> paths)
        {
            _operation = report;
            _operationPrepares = prepares;
            _operationIncludesBuild = includesBuild;
            _operationPaths = paths;
            _wasBusy = true;
            _renderedSignature = 0;
        }

        /// <summary>Prepares every preparable open text and the build files that still need work. Without a complete
        /// check, all build content is visited, which also completes the overview.</summary>
        void PrepareAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (TextStudioRuntimePreparation.IsBusy)
            {
                if (_operation == null || _operationPrepares) return;
                // A background check yields to an explicit request; its partial results are kept.
                TextStudioRuntimePreparation.Cancel();
                Harvest();
            }
            AnalyzeLive();
            bool denied = EmbeddingDenied;
            TextStudioText[] texts = _live.Items.Where(item => TextStudioPreparationView.Preparable(item, denied)).Select(item => item.SourceText).Where(text => text != null).Distinct().ToArray();
            ISet<string> paths = null;
            bool includeBuild = !_openScenesOnly;
            if (includeBuild && BuildContent.Complete)
            {
                HashSet<string> open = TextStudioRuntimePreparation.OpenScenePaths();
                paths = new HashSet<string>(BuildContent.Items.Where(item => TextStudioPreparationView.Preparable(item, denied) && !open.Contains(item.SourcePath)).Select(item => item.SourcePath), StringComparer.Ordinal);
                includeBuild = paths.Count > 0;
            }
            if (texts.Length == 0 && !includeBuild) { _renderedSignature = 0; return; }
            _failures.Clear();
            if (TextStudioRuntimePreparation.PrepareProject(texts, includeBuild, includeBuild ? paths : null))
                Own(TextStudioRuntimePreparation.LastReport, true, includeBuild, includeBuild ? paths : null);
            Tick();
        }

        void CancelOperation()
        {
            _canceledByUser = _operation != null;
            TextStudioRuntimePreparation.Cancel();
            Tick();
        }

        void Harvest()
        {
            TextStudioPreparationReport report = _operation;
            _operation = null;
            if (_operationIncludesBuild) UpdateBuildContent(report, _operationPaths);
            // A declined first check is not restarted automatically in this editor session.
            if (!report.IsComplete && !_operationPrepares && _operationPaths == null && _canceledByUser) SessionState.SetBool(DeclinedKey, true);
            _canceledByUser = false;
            AnalyzeLive();
            if (_operationPrepares && report.IsComplete)
            {
                bool denied = EmbeddingDenied;
                string[] keys = TextStudioPreparationView.Keys(report.Items);
                for (int i = 0; i < report.Items.Count; i++)
                    if (TextStudioPreparationView.Preparable(report.Items[i], denied)) _failures[keys[i]] = report.Items[i].Message;
                if (_failures.Count > 0) _detailsOpen = true;
            }
            _operationPaths = null;
            _saved = null;
            if (_tab == DataTab) RefreshInventory(true);
            _renderedSignature = 0;
        }

        static void UpdateBuildContent(TextStudioPreparationReport report, ISet<string> visited)
        {
            BuildContentSnapshot content = BuildContent;
            List<TextStudioPreparationItem> fresh = report.Items.Where(item => !string.IsNullOrEmpty(item.SourcePath)).ToList();
            // A finished visit replaces every visited file. An interrupted visit replaces only the files it reached.
            HashSet<string> replaced = !report.IsComplete ? new HashSet<string>(fresh.Select(item => item.SourcePath), StringComparer.Ordinal)
                : visited != null ? new HashSet<string>(visited, StringComparer.Ordinal) : null;
            List<TextStudioPreparationItem> items = replaced == null ? new List<TextStudioPreparationItem>() : content.Items.Where(item => !replaced.Contains(item.SourcePath)).ToList();
            items.AddRange(fresh);
            content.Items = items;
            if (visited == null)
            {
                content.complete = report.IsComplete;
                content.checkedTicks = DateTime.UtcNow.Ticks;
            }
            content.Save();
        }

        static bool EmbeddingDenied => TextStudioRuntimePreparationSettings.instance.embeddingChoiceMade && !TextStudioRuntimePreparationSettings.instance.allowFontEmbedding;

        bool NeedsFontChoice() => !TextStudioRuntimePreparationSettings.instance.embeddingChoiceMade &&
            _items.Any(item => item.RequiresFontEmbedding && TextStudioPreparationView.Pending(item));

        void ChooseEmbedding(bool allowed)
        {
            TextStudioRuntimePreparationSettings settings = TextStudioRuntimePreparationSettings.instance;
            settings.embeddingChoiceMade = true;
            settings.allowFontEmbedding = allowed;
            SaveSettings();
            if (allowed) PrepareAll();
        }

        void SaveSettings()
        {
            TextStudioRuntimePreparationSettings.instance.Persist();
            TextStudioRuntimePreparation.RequestRefresh();
            MarkLiveDirty(0);
            _renderedSignature = 0;
        }

        void Render()
        {
            bool busy = TextStudioRuntimePreparation.IsBusy;
            // Rows are recombined only when their sources change; in-place state changes are caught by the signature below.
            int inputs = HashCode.Combine(_live, BuildContent.Version, _operation, _operation?.Items.Count ?? 0, _openScenesOnly, OpenScenesHash());
            if (inputs != _combinedInputs)
            {
                _combinedInputs = inputs;
                IReadOnlyList<TextStudioPreparationItem> live = _live != null ? _live.Items : (IReadOnlyList<TextStudioPreparationItem>)Array.Empty<TextStudioPreparationItem>();
                IReadOnlyList<TextStudioPreparationItem> build = _openScenesOnly ? Array.Empty<TextStudioPreparationItem>() : (IReadOnlyList<TextStudioPreparationItem>)BuildContent.Items;
                _items = TextStudioPreparationView.Combine(live, build, TextStudioRuntimePreparation.OpenScenePaths(), _operation?.Items, out _keys);
            }
            TextStudioRuntimePreparationSettings settings = TextStudioRuntimePreparationSettings.instance;
            int settingState = (settings.automaticInEditor ? 1 : 0) | (settings.automaticInBuilds ? 2 : 0) | (settings.embeddingChoiceMade ? 4 : 0) | (settings.allowFontEmbedding ? 8 : 0) | (_openScenesOnly ? 16 : 0);
            int signature = HashCode.Combine(_tab, busy, _operation != null, _operationPrepares, settingState, _detailsOpen, EditorApplication.isPlayingOrWillChangePlaymode, _failures.Count);
            signature = HashCode.Combine(signature, BuildContent.Version, (int)(BuildContent.AgeSeconds / 60), _operation?.OperationLabel, _query, _target, _focusPending,
                HashCode.Combine(settings.runtimeCharacters, settings.additionalText));
            for (int i = 0; i < _items.Count; i++)
                signature = HashCode.Combine(signature, _keys[i], _items[i].State, _items[i].Message, _items[i].RequiresManualAction, _items[i].DataPaths.Count);
            if (signature == _renderedSignature) return;
            _renderedSignature = signature;
            _summary = new TextStudioPreparationSummary(_items, EmbeddingDenied);
            SyncSettings(settings);
            if (_tab != OverviewTab) return;
            RenderHero(busy);
            RenderDetails();
        }

        static int OpenScenesHash()
        {
            int hash = SceneManager.sceneCount;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                hash = HashCode.Combine(hash, scene.path, scene.isLoaded);
            }
            return hash;
        }

        static Label Copy(string text, params string[] classes)
        {
            Label label = TextStudioInspectorElements.CreateMutedText(text);
            label.AddToClassList("ts-preparation-copy");
            CommonUITK.AddClasses(label, classes);
            return label;
        }

        static void SetText(Label label, string text, string tooltip = null)
        {
            if (label.text != text) label.text = text;
            label.tooltip = tooltip ?? string.Empty;
        }

        static void SetVisible(VisualElement element, bool visible) => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        static Button Button(string text, string tooltip, Action action, bool primary = false, string name = null)
        {
            Button button = TextStudioInspectorElements.CreateButton(text, tooltip, action, primary);
            button.name = name;
            button.AddToClassList("ts-preparation-button");
            return button;
        }

        static Button LinkButton(string text, string tooltip, Action action)
        {
            Button button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("ts-preparation-link");
            return button;
        }

        static void Reveal(UnityEngine.Object target)
        {
            if (target == null) return;
            Selection.activeObject = target;
            EditorGUIUtility.PingObject(target);
        }

        static void RevealAssets(IEnumerable<string> paths)
        {
            UnityEngine.Object[] assets = paths.Distinct().Select(AssetDatabase.LoadMainAssetAtPath).Where(asset => asset != null).ToArray();
            if (assets.Length == 0) return;
            Selection.objects = assets;
            EditorGUIUtility.PingObject(assets[0]);
        }

        static BuildContentSnapshot BuildContent
        {
            get
            {
                if (s_buildContent != null) return s_buildContent;
                string json = SessionState.GetString(SnapshotKey, string.Empty);
                s_buildContent = json.Length > 0 ? JsonUtility.FromJson<BuildContentSnapshot>(json) : null;
                if (s_buildContent == null) s_buildContent = new BuildContentSnapshot();
                s_buildContent.Restore();
                return s_buildContent;
            }
        }

        /// <summary>The last check of saved build content in this editor session. Session state survives domain reloads.</summary>
        [Serializable]
        sealed class BuildContentSnapshot
        {
            [SerializeField] List<SavedItem> items = new List<SavedItem>();
            [SerializeField] internal long checkedTicks;
            [SerializeField] internal bool complete;
            [NonSerialized] List<TextStudioPreparationItem> _items = new List<TextStudioPreparationItem>();
            [NonSerialized] internal int Version;

            internal bool Checked => checkedTicks != 0;
            internal bool Complete => Checked && complete;
            internal double AgeSeconds => Checked ? (DateTime.UtcNow - new DateTime(checkedTicks, DateTimeKind.Utc)).TotalSeconds : 0;

            internal List<TextStudioPreparationItem> Items
            {
                get => _items;
                set { _items = value; Version++; }
            }

            internal void Restore() => _items = (items ?? new List<SavedItem>()).Select(item => item.ToItem()).ToList();

            internal void Save()
            {
                items = _items.Select(SavedItem.From).ToList();
                SessionState.SetString(SnapshotKey, JsonUtility.ToJson(this));
            }
        }

        [Serializable]
        sealed class SavedItem
        {
            public string provider, target, message, consequence, summary, sourcePath, productId, productName, kindId, kindName, benefit;
            public int state;
            public bool required, manual, embedding, characters;
            public string[] data;

            internal static SavedItem From(TextStudioPreparationItem item) => new SavedItem
            {
                provider = item.Provider, target = item.Target, message = item.Message, consequence = item.Consequence, summary = item.Summary,
                sourcePath = item.SourcePath, productId = item.Presentation?.ProductId, productName = item.Presentation?.ProductName,
                kindId = item.Presentation?.KindId, kindName = item.Presentation?.KindName, benefit = item.Presentation?.Benefit,
                state = (int)item.State, required = item.Required, manual = item.RequiresManualAction, embedding = item.RequiresFontEmbedding,
                characters = item.Presentation != null && item.Presentation.UsesCharacters,
                data = item.DataPaths.ToArray()
            };

            internal TextStudioPreparationItem ToItem() => new TextStudioPreparationItem(provider, target, (TextStudioPreparationState)state, message, required)
            {
                RequiresManualAction = manual, RequiresFontEmbedding = embedding, Consequence = Empty(consequence), Summary = Empty(summary), SourcePath = sourcePath,
                DataPaths = data ?? Array.Empty<string>(),
                Presentation = string.IsNullOrEmpty(productId) && string.IsNullOrEmpty(kindId) ? null
                    : new TextStudioPreparationPresentation(productId, productName, kindId, kindName, benefit) { UsesCharacters = characters }
            };

            static string Empty(string value) => string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
