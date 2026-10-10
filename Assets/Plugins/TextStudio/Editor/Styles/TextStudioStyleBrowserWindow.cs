using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>Renderer-neutral, retained style selection with one live preview and a bounded thumbnail cache.</summary>
    internal sealed class TextStudioStyleBrowserWindow : EditorWindow
    {
        const string PreferencePrefix = "TextStudio.StyleBrowser.";
        const int ThumbnailLimit = 64;
        readonly List<TextStudioStyleCatalogEntry> _entries = new List<TextStudioStyleCatalogEntry>();
        readonly List<TextStudioStyleCatalogEntry> _filtered = new List<TextStudioStyleCatalogEntry>();
        readonly HashSet<string> _favorites = new HashSet<string>(StringComparer.Ordinal);
        readonly List<string> _recent = new List<string>();
        readonly Dictionary<string, Texture2D> _thumbnails = new Dictionary<string, Texture2D>();
        readonly Queue<string> _thumbnailOrder = new Queue<string>();
        readonly Queue<TextStudioStyleCatalogEntry> _thumbnailQueue = new Queue<TextStudioStyleCatalogEntry>();
        readonly HashSet<string> _thumbnailPending = new HashSet<string>();
        readonly Dictionary<string, List<Image>> _thumbnailImages = new Dictionary<string, List<Image>>();
        [SerializeReference] TextStudioStyleCatalogProvider _provider;
        TextStudioStyleCatalogEntry _selected;
        TextStudioStylePreviewSession _preview;
        CommonSelectableGridView<TextStudioStyleCatalogEntry> _grid;
        DropdownField _category;
        DropdownField _capability;
        Label _count;
        Label _title;
        Label _description;
        Label _status;
        Label _empty;
        Image _image;
        Button _apply;
        Button _favorite;
        Button _unfreeze;
        TextField _sample;
        [SerializeField] string _search = string.Empty;
        [SerializeField] string _origin = "All";
        [SerializeField] string _scope = "All Styles";
        [SerializeField] string _family = "All Categories";
        [SerializeField] string _feature = "All Capabilities";
        [SerializeField] string _selectedId;
        [SerializeField] string _previewText;
        IVisualElementScheduledItem _previewSchedule;
        double _previewStarted;
        bool _changingPlayMode;
        bool _refreshQueued;

        internal static void Show2D(Object[] owners) => Show(new TextStudio2DStyleCatalogProvider(owners));

        internal static void Show(TextStudioStyleCatalogProvider provider)
        {
            TextStudioStyleBrowserWindow window = GetWindow<TextStudioStyleBrowserWindow>(true, provider.Title);
            window.ReleasePreview();
            window.ClearThumbnails();
            window._provider = provider;
            window._previewText = provider.InitialText;
            window._selectedId = null;
            window._selected = null;
            window.minSize = new Vector2(720, 560);
            window.titleContent = new GUIContent(provider.Title);
            window.LoadPreferences();
            window.BuildWindow();
            window.ShowUtility();
            window.Focus();
        }

        void OnEnable()
        {
            EditorApplication.projectChanged += ScheduleRefresh;
            EditorApplication.hierarchyChanged += RefreshAction;
            Undo.undoRedoPerformed += ScheduleRefresh;
            TextStudioStyleCatalogProvider.Changed += ScheduleRefresh;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseResources;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (_provider != null) LoadPreferences();
        }

        void OnDisable()
        {
            EditorApplication.projectChanged -= ScheduleRefresh;
            EditorApplication.hierarchyChanged -= RefreshAction;
            Undo.undoRedoPerformed -= ScheduleRefresh;
            TextStudioStyleCatalogProvider.Changed -= ScheduleRefresh;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseResources;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.delayCall -= RefreshCatalog;
            EditorApplication.delayCall -= RenderNextThumbnail;
            ReleaseResources();
        }

        public void CreateGUI() { if (_provider != null) BuildWindow(); }

        void BuildWindow()
        {
            _previewSchedule?.Pause();
            ReleasePreview();
            VisualElement root = rootVisualElement;
            root.Clear();
            CommonUITK.ApplyRoot(root, CommonUITK.LoadStyleSheetFromAnchor("TextGalleryWindow", "/Editor/TextGalleryWindow.cs", "/Editor/TextGalleryWindow.uss"), "ts-gallery", "ts-gallery-dark", "ts-gallery-light");
            CommonUITK.ApplyWindowLayout(root);
            Toolbar toolbar = new Toolbar();
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.height = StyleKeyword.Auto;
            ToolbarSearchField search = new ToolbarSearchField { name = "ts-style-search", tooltip = "Search styles by name, category, capability, or registered tag." };
            search.style.minWidth = 160;
            search.style.flexGrow = 1;
            search.SetValueWithoutNotify(_search);
            search.RegisterValueChangedCallback(evt => { _search = evt.newValue; Filter(); });
            toolbar.Add(search);
            toolbar.Add(FilterField("ts-style-origin", new List<string> { "All", "Built-in", "Custom" }, _origin, "Show shipped styles, registered custom styles, or both.", value => { _origin = value; Filter(); }));
            toolbar.Add(FilterField("ts-style-scope", new List<string> { "All Styles", "Favorites", "Recent" }, _scope, "Browse the full catalog, favorites, or recently applied styles.", value => { _scope = value; SavePreferences(); Filter(); }));
            _category = FilterField("ts-style-category", new List<string> { "All Categories" }, "All Categories", "Filter by visual family.", value => { _family = value; Filter(); });
            toolbar.Add(_category);
            _capability = FilterField("ts-style-capability", new List<string> { "All Capabilities" }, "All Capabilities", "Filter by an authored geometry capability.", value => { _feature = value; Filter(); });
            toolbar.Add(_capability);
            root.Add(toolbar);
            _count = TextStudioInspectorElements.CreateMutedText(string.Empty);
            _count.style.marginLeft = 10;
            _count.style.marginTop = 6;
            root.Add(_count);
            VisualElement body = new VisualElement();
            body.AddToClassList(CommonUITK.WindowBodyClass);
            body.style.flexDirection = FlexDirection.Row;
            body.style.flexGrow = 1;
            body.style.minHeight = 0;
            VisualElement browser = new VisualElement();
            browser.style.flexGrow = 1;
            browser.style.minWidth = 260;
            browser.style.minHeight = 0;
            _empty = TextStudioInspectorElements.CreateMutedText("No styles match these filters. Clear the search or choose All.");
            _empty.name = "ts-style-empty";
            _empty.AddToClassList("ts-style-empty");
            browser.Add(_empty);
            _grid = new CommonSelectableGridView<TextStudioStyleCatalogEntry>(() => new VisualElement(), BindCard) { AllowMultipleSelection = false, name = "ts-style-grid" };
            _grid.style.flexGrow = 1;
            _grid.style.minHeight = 0;
            VisualElement focusScope = _grid;
            focusScope.RegisterCallback<FocusInEvent>(_ => focusScope.AddToClassList("ts-style-grid--focused"));
            focusScope.RegisterCallback<FocusOutEvent>(evt =>
            {
                VisualElement next = evt.relatedTarget as VisualElement;
                if (next == null || next != focusScope && !focusScope.Contains(next)) focusScope.RemoveFromClassList("ts-style-grid--focused");
            });
            _grid.SetLayout(220, 1.22f, 6, true);
            _grid.SelectionChanged += (_, index) => { if (index >= 0 && index < _filtered.Count) Select(_filtered[index]); };
            _grid.ItemActivated += (entry, _, _) => { Select(entry); ApplySelected(); };
            browser.Add(_grid);
            body.Add(browser);
            ScrollView detailsScroll = new ScrollView(ScrollViewMode.Vertical) { name = "ts-style-details" };
            detailsScroll.style.width = 310;
            detailsScroll.style.flexShrink = 0;
            VisualElement details = TextStudioInspectorElements.CreateRoot("ts-style-details-content");
            details.style.paddingLeft = 12;
            details.style.paddingRight = 12;
            details.style.paddingTop = 8;
            details.style.paddingBottom = 12;
            detailsScroll.Add(details);
            _image = new Image { scaleMode = ScaleMode.ScaleToFit, name = "ts-style-live-preview" };
            _image.style.height = 210;
            _image.style.backgroundColor = new Color(.12f, .13f, .15f);
            details.Add(_image);
            _title = new Label();
            _title.style.fontSize = 18;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.whiteSpace = WhiteSpace.Normal;
            _title.style.marginTop = 12;
            details.Add(_title);
            _description = TextStudioInspectorElements.CreateMutedText(string.Empty);
            details.Add(_description);
            _sample = new TextField("Preview Text") { value = _previewText ?? _provider.InitialText, tooltip = "Try this style with your own text in the isolated preview." };
            _sample.style.marginTop = 12;
            _sample.RegisterValueChangedCallback(evt => { _previewText = evt.newValue; _preview?.SetText(evt.newValue); });
            details.Add(_sample);
            _favorite = TextStudioInspectorElements.CreateButton("Add to Favorites", "Keep this style in Favorites for quick access.", ToggleFavorite);
            details.Add(_favorite);
            _apply = TextStudioInspectorElements.CreateButton(_provider.ApplyLabel,
                "Assign this reusable style to the captured text selection. The browser stays open.", ApplySelected, true);
            _apply.name = "ts-style-apply";
            _apply.style.marginTop = 10;
            _apply.style.height = 30;
            details.Add(_apply);
            _unfreeze = new Button(() => { _provider.Unfreeze(); RefreshAction(); }) { text = "Unfreeze to Edit", tooltip = "Restore live editing with the existing Unfreeze workflow before applying a style." };
            details.Add(_unfreeze);
            _status = TextStudioInspectorElements.CreateMutedText(string.Empty);
            details.Add(_status);
            body.Add(detailsScroll);
            root.Add(body);
            _previewSchedule = root.schedule.Execute(TickPreview).Every(33);
            RefreshCatalog();
        }

        static DropdownField FilterField(string name, List<string> choices, string value, string tooltip, Action<string> changed)
        {
            DropdownField field = new DropdownField(choices, Math.Max(0, choices.IndexOf(value))) { name = name, tooltip = tooltip };
            field.style.minWidth = 90;
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return field;
        }

        void ScheduleRefresh()
        {
            if (_refreshQueued) return;
            _refreshQueued = true;
            EditorApplication.delayCall += RefreshCatalog;
        }

        void RefreshCatalog()
        {
            _refreshQueued = false;
            if (_provider == null || _grid == null || _changingPlayMode) return;
            ClearThumbnails();
            _entries.Clear();
            _entries.AddRange(_provider.GetEntries());
            List<string> categories = new List<string> { "All Categories" };
            List<string> capabilities = new List<string> { "All Capabilities" };
            foreach (TextStudioStyleCatalogEntry entry in _entries)
            {
                if (!string.IsNullOrEmpty(entry.Category) && !categories.Contains(entry.Category)) categories.Add(entry.Category);
                foreach (string capability in entry.Capabilities) if (!capabilities.Contains(capability)) capabilities.Add(capability);
            }
            categories.Sort(1, categories.Count - 1, StringComparer.Ordinal);
            _category.choices = categories;
            if (!categories.Contains(_family)) _family = "All Categories";
            _category.SetValueWithoutNotify(_family);
            capabilities.Sort(1, capabilities.Count - 1, StringComparer.Ordinal);
            _capability.choices = capabilities;
            if (!capabilities.Contains(_feature)) _feature = "All Capabilities";
            _capability.SetValueWithoutNotify(_feature);
            _capability.style.display = capabilities.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
            if (string.IsNullOrEmpty(_selectedId))
                _selectedId = _entries.Find(entry => entry.Asset != null && entry.Asset == _provider.CurrentAsset)?.Id;
            Filter(true);
        }

        void Filter(bool refreshPreview = false)
        {
            if (_grid == null) return;
            _filtered.Clear();
            foreach (TextStudioStyleCatalogEntry entry in _entries)
            {
                if (_origin == "Built-in" && !entry.BuiltIn || _origin == "Custom" && entry.BuiltIn) continue;
                if (_scope == "Favorites" && !_favorites.Contains(entry.Id) || _scope == "Recent" && !_recent.Contains(entry.Id)) continue;
                if (_family != "All Categories" && entry.Category != _family) continue;
                if (_feature != "All Capabilities" && Array.IndexOf(entry.Capabilities, _feature) < 0) continue;
                string searchable = entry.Title + " " + entry.Description + " " + entry.Category + " " + entry.Keywords + " " + string.Join(" ", entry.Capabilities);
                if (!string.IsNullOrWhiteSpace(_search) && searchable.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                _filtered.Add(entry);
            }
            if (_scope == "Recent") _filtered.Sort((left, right) => _recent.IndexOf(left.Id).CompareTo(_recent.IndexOf(right.Id)));
            Vector2 offset = _grid.ScrollView.scrollOffset;
            _grid.SetItems(_filtered);
            int index = _filtered.FindIndex(entry => entry.Id == _selectedId);
            if (index < 0 && _filtered.Count > 0) index = 0;
            if (index >= 0) _grid.SetSelection(new[] { index }, index);
            _grid.ScrollView.scrollOffset = offset;
            _count.text = _filtered.Count + (_filtered.Count == 1 ? " style" : " styles");
            _empty.text = _origin == "Custom" && !_entries.Exists(entry => !entry.BuiltIn)
                ? "No custom styles are registered. Use Save & Register in the text inspector, or register a style asset."
                : "No styles match these filters. Clear the search or choose All.";
            _empty.style.display = _filtered.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _grid.style.display = _filtered.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            Select(index >= 0 ? _filtered[index] : null, refreshPreview);
        }

        void BindCard(VisualElement root, TextStudioStyleCatalogEntry entry, int index)
        {
            root.Clear();
            root.AddToClassList("ts-gallery-card");
            root.AddToClassList("ts-style-card");
            root.tooltip = entry.Description + "\nDouble-click to " + _provider.ApplyLabel.ToLowerInvariant() + ". The browser stays open.";
            Image image = new Image { scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.style.flexGrow = 1;
            image.style.minHeight = 70;
            image.style.backgroundColor = new Color(.12f, .13f, .15f);
            root.Add(image);
            if (entry.BuiltIn && !string.IsNullOrEmpty(entry.PreviewFileName) && TextGalleryRenderedPreview.TryLoadTexture(entry.PackageName, TextGalleryRenderedPreview.BuildPreviewPath(entry.PreviewFileName), out Texture2D texture, out _)) image.image = texture;
            else if (_thumbnails.TryGetValue(entry.Id, out Texture2D cached)) image.image = cached;
            else
            {
                if (!_thumbnailImages.TryGetValue(entry.Id, out List<Image> images)) _thumbnailImages.Add(entry.Id, images = new List<Image>());
                images.Add(image);
                image.RegisterCallback<DetachFromPanelEvent>(_ => images.Remove(image));
                if (entry.Asset != null && _thumbnailPending.Add(entry.Id))
                {
                    _thumbnailQueue.Enqueue(entry);
                    EditorApplication.delayCall -= RenderNextThumbnail;
                    EditorApplication.delayCall += RenderNextThumbnail;
                }
            }
            Label title = new Label(entry.Title) { tooltip = root.tooltip, pickingMode = PickingMode.Ignore };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.style.marginTop = 5;
            root.Add(title);
            string originText = entry.BuiltIn ? "Built-in" : "Custom";
            if (!string.IsNullOrEmpty(entry.Category) && !string.Equals(entry.Category, originText, StringComparison.OrdinalIgnoreCase)) originText += " · " + entry.Category;
            Label origin = new Label(originText) { pickingMode = PickingMode.Ignore };
            origin.style.opacity = .65f;
            origin.style.fontSize = 10;
            root.Add(origin);
        }

        void Select(TextStudioStyleCatalogEntry entry, bool force = false)
        {
            if (!force && _selected == entry) return;
            ReleasePreview();
            _selected = entry;
            _selectedId = entry?.Id;
            _title.text = entry?.Title ?? "Choose a Style";
            _description.text = entry?.Description ?? "Browse shipped appearances and your registered styles.";
            _favorite.text = entry != null && _favorites.Contains(entry.Id) ? "Remove Favorite" : "Add to Favorites";
            _favorite.SetEnabled(entry != null);
            if (entry != null && entry.Asset != null)
            {
                try
                {
                    string sample = string.IsNullOrEmpty(_sample.value) ? entry.SampleText : _sample.value;
                    _preview = _provider.CreatePreview(entry, sample, true, 768, 512);
                    _image.image = _preview.Texture;
                    _previewStarted = EditorApplication.timeSinceStartup;
                }
                catch (Exception error) { _status.text = "Preview unavailable: " + error.Message; }
            }
            RefreshAction();
        }

        void RefreshAction()
        {
            if (_provider == null || _apply == null) return;
            bool available = _provider.CanApply(_selected, out string reason);
            _apply.text = _provider.ApplyLabel;
            _apply.SetEnabled(available);
            _unfreeze.style.display = _provider.CanUnfreeze ? DisplayStyle.Flex : DisplayStyle.None;
            if (!string.IsNullOrEmpty(reason) || _preview != null) _status.text = reason;
        }

        void ApplySelected()
        {
            if (!_provider.CanApply(_selected, out _)) { RefreshAction(); return; }
            _provider.Apply(_selected);
            _recent.Remove(_selected.Id);
            _recent.Insert(0, _selected.Id);
            if (_recent.Count > 12) _recent.RemoveRange(12, _recent.Count - 12);
            SavePreferences();
            _status.text = _provider.AppliedMessage;
            if (_scope == "Recent") Filter();
        }

        void ToggleFavorite()
        {
            if (_selected == null) return;
            if (!_favorites.Add(_selected.Id)) _favorites.Remove(_selected.Id);
            _favorite.text = _favorites.Contains(_selected.Id) ? "Remove Favorite" : "Add to Favorites";
            SavePreferences();
            if (_scope == "Favorites") Filter();
        }

        void TickPreview()
        {
            if (_changingPlayMode || _preview == null || _image?.panel == null) return;
            try { _preview.Render((float)(EditorApplication.timeSinceStartup - _previewStarted)); }
            catch (Exception error) { ReleasePreview(); _status.text = "Preview unavailable: " + error.Message; }
        }

        void RenderNextThumbnail()
        {
            if (_provider == null || _changingPlayMode || _thumbnailQueue.Count == 0) return;
            TextStudioStyleCatalogEntry entry = _thumbnailQueue.Dequeue();
            try
            {
                if (_thumbnailImages.TryGetValue(entry.Id, out List<Image> images) && images.Exists(image => image.panel != null))
                {
                    float pixelsPerPoint = Mathf.Max(1f, EditorGUIUtility.pixelsPerPoint);
                    using TextStudioStylePreviewSession preview = _provider.CreatePreview(entry, entry.SampleText, false, Mathf.CeilToInt(384 * pixelsPerPoint), Mathf.CeilToInt(216 * pixelsPerPoint));
                    preview.Render(.75f);
                    Texture2D texture = preview.Capture();
                    _thumbnails[entry.Id] = texture;
                    _thumbnailOrder.Enqueue(entry.Id);
                    foreach (Image image in images) image.image = texture;
                    while (_thumbnailOrder.Count > ThumbnailLimit)
                    {
                        string oldest = _thumbnailOrder.Dequeue();
                        if (_thumbnails.Remove(oldest, out Texture2D old)) DestroyImmediate(old);
                    }
                }
            }
            catch (Exception error)
            {
                if (_thumbnailImages.TryGetValue(entry.Id, out List<Image> images))
                    foreach (Image image in images) image.tooltip = "Preview unavailable: " + error.Message;
            }
            finally { _thumbnailPending.Remove(entry.Id); }
            if (_thumbnailQueue.Count > 0) EditorApplication.delayCall += RenderNextThumbnail;
        }

        void LoadPreferences()
        {
            _favorites.Clear(); _recent.Clear();
            string scope = EditorPrefs.GetString(PreferencePrefix + _provider.Key + ".Scope", "All Styles");
            _scope = scope == "Favorites" || scope == "Recent" ? scope : "All Styles";
            foreach (string id in EditorPrefs.GetString(PreferencePrefix + _provider.Key + ".Favorites", string.Empty).Split('|')) if (id.Length > 0) _favorites.Add(id);
            foreach (string id in EditorPrefs.GetString(PreferencePrefix + _provider.Key + ".Recent", string.Empty).Split('|')) if (id.Length > 0) _recent.Add(id);
        }

        void SavePreferences()
        {
            EditorPrefs.SetString(PreferencePrefix + _provider.Key + ".Favorites", string.Join("|", _favorites));
            EditorPrefs.SetString(PreferencePrefix + _provider.Key + ".Recent", string.Join("|", _recent));
            EditorPrefs.SetString(PreferencePrefix + _provider.Key + ".Scope", _scope);
        }

        void OnPlayModeChanged(PlayModeStateChange state)
        {
            _changingPlayMode = state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode;
            if (_changingPlayMode) ReleaseResources();
            else
            {
                _previewSchedule?.Resume();
                ScheduleRefresh();
            }
        }

        void ReleasePreview() { if (_image != null) _image.image = null; _preview?.Dispose(); _preview = null; }
        void ReleaseResources() { _previewSchedule?.Pause(); ReleasePreview(); ClearThumbnails(); }
        void ClearThumbnails()
        {
            EditorApplication.delayCall -= RenderNextThumbnail;
            foreach (Texture2D texture in _thumbnails.Values) if (texture != null) DestroyImmediate(texture);
            _thumbnails.Clear(); _thumbnailOrder.Clear(); _thumbnailQueue.Clear(); _thumbnailPending.Clear(); _thumbnailImages.Clear();
        }
    }
}
