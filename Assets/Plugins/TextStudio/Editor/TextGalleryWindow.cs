using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal abstract class TextGalleryWindowBase : EditorWindow
    {
        const float MinCardWidth = 220f;
        const float MaxCardWidth = 380f;
        const float PreviewAspectRatio = 16f / 9f;
        const float CardHorizontalInsets = 18f;

        readonly List<TextGalleryCard> _cards = new List<TextGalleryCard>();
        readonly List<ToolbarToggle> _categoryButtons = new List<ToolbarToggle>();
        readonly List<CardView> _cardViews = new List<CardView>();

        CommonSelectableGridView<TextGalleryCard> _grid;
        readonly List<TextGalleryCard> _filteredCards = new List<TextGalleryCard>();
        ToolbarSearchField _searchField;
        Slider _cardSizeSlider;
        VisualElement _categoryToolbar;
        Label _contextLabel;
        Label _emptyLabel;
        bool _selectionRefreshScheduled;
        bool _catalogRefreshScheduled;
        string _searchText = string.Empty;
        string _origin = "All";
        TextGalleryCategory _selectedCategory = TextGalleryCategory.All;
        float _cardWidth = 270f;

        protected abstract void AddCards(List<TextGalleryCard> cards);

        protected virtual string ContextMessage => string.Empty;

        protected virtual float CardContentHeight => 150f;

        protected virtual bool RefreshWhenSelectionChanges => false;

        protected virtual void RefreshSelectionContext()
        {
        }

        void OnEnable()
        {
            RegisterSelectionRefresh();
            TextStudioStyleCatalogProvider.Changed += ScheduleCatalogRefresh;
        }

        protected virtual void OnDisable()
        {
            Selection.selectionChanged -= ScheduleSelectionRefresh;
            EditorApplication.delayCall -= ExecuteSelectionRefresh;
            _selectionRefreshScheduled = false;
            TextStudioStyleCatalogProvider.Changed -= ScheduleCatalogRefresh;
            EditorApplication.delayCall -= RefreshCatalog;
            _catalogRefreshScheduled = false;
        }

        public virtual void CreateGUI()
        {
            RegisterSelectionRefresh();
            ReloadCards();
            BuildWindow();
        }

        void RegisterSelectionRefresh()
        {
            Selection.selectionChanged -= ScheduleSelectionRefresh;
            if (RefreshWhenSelectionChanges)
                Selection.selectionChanged += ScheduleSelectionRefresh;
        }

        void BuildWindow()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1f;
            CommonUITK.ApplyRoot(root, CommonUITK.LoadStyleSheetFromAnchor(
                "TextGalleryWindow", "/Editor/TextGalleryWindow.cs", "/Editor/TextGalleryWindow.uss"), "ts-gallery", "ts-gallery-dark", "ts-gallery-light");

            root.Add(BuildToolbar());
            AddContextBanner(root);
            _categoryToolbar = new Toolbar();
            _categoryToolbar.style.flexWrap = Wrap.Wrap;
            _categoryToolbar.style.height = StyleKeyword.Auto;
            _categoryToolbar.style.minHeight = 22f;
            root.Add(_categoryToolbar);
            BuildCategoryTabs();

            _emptyLabel = new Label("No gallery entries match the current filters. Clear the search or choose another category.");
            _emptyLabel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_emptyLabel);
            _grid = new CommonSelectableGridView<TextGalleryCard>(() => new VisualElement(), BindVisibleCard)
            {
                AllowMultipleSelection = false
            };
            _grid.style.flexGrow = 1f;
            _grid.style.minHeight = 0f;
            _grid.ItemActivated += (card, _, secondary) => InvokeCardAction(card, secondary);
            _grid.LayoutChanged += (_, width, _) =>
            {
                if (Mathf.Abs(_grid.ActualTileHeight - GetPreviewHeight(width) - CardContentHeight) > 0.5f)
                    UpdateCardLayout();
            };
            root.Add(_grid);

            RenderCards();
        }

        void AddContextBanner(VisualElement root)
        {
            _contextLabel = new Label();
            _contextLabel.style.whiteSpace = WhiteSpace.Normal;
            _contextLabel.style.minWidth = 0f;
            _contextLabel.style.marginLeft = 10f;
            _contextLabel.style.marginRight = 10f;
            _contextLabel.style.marginTop = 7f;
            _contextLabel.style.marginBottom = 3f;
            _contextLabel.style.opacity = 0.72f;
            root.Add(_contextLabel);
            UpdateContextBanner();
        }

        Toolbar BuildToolbar()
        {
            Toolbar toolbar = new Toolbar();
            toolbar.style.flexWrap = Wrap.Wrap;
            toolbar.style.height = StyleKeyword.Auto;
            toolbar.style.minHeight = 22f;

            _searchField = new ToolbarSearchField();
            _searchField.style.width = 240f;
            _searchField.style.minWidth = 100f;
            _searchField.style.flexGrow = 1f;
            _searchField.style.flexShrink = 1f;
            _searchField.tooltip = "Find text effects and examples by name, description, or keyword.";
            _searchField.SetValueWithoutNotify(_searchText);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _searchText = evt.newValue ?? string.Empty;
                UpdateCardViews();
            });
            toolbar.Add(_searchField);

            DropdownField origin = new DropdownField(new List<string> { "All", "Built-in", "Custom" }, 0)
            {
                name = "ts-gallery-origin",
                tooltip = "Browse shipped entries, explicitly registered custom styles, or both."
            };
            origin.SetValueWithoutNotify(_origin);
            origin.RegisterValueChangedCallback(evt => { _origin = evt.newValue; UpdateCardViews(); });
            toolbar.Add(origin);

            VisualElement sizeControls = new VisualElement();
            sizeControls.style.flexDirection = FlexDirection.Row;
            sizeControls.style.alignItems = Align.Center;
            sizeControls.style.flexShrink = 0f;
            toolbar.Add(sizeControls);

            Label sizeLabel = new Label("Size");
            sizeLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            sizeLabel.style.marginRight = 4f;
            sizeControls.Add(sizeLabel);

            _cardSizeSlider = new Slider(MinCardWidth, MaxCardWidth);
            _cardSizeSlider.style.width = 130f;
            _cardSizeSlider.tooltip = "Adjust the gallery card and preview size.";
            _cardSizeSlider.SetValueWithoutNotify(_cardWidth);
            _cardSizeSlider.RegisterValueChangedCallback(evt =>
            {
                float width = Mathf.Round(evt.newValue / 10f) * 10f;
                if (Mathf.Approximately(_cardWidth, width))
                    return;
                _cardWidth = width;
                UpdateCardLayout();
                if (_grid.ActiveIndex >= 0)
                    _grid.ScrollToItem(_grid.ActiveIndex);
            });
            sizeControls.Add(_cardSizeSlider);

            return toolbar;
        }

        void BuildCategoryTabs()
        {
            if (_categoryToolbar == null)
                return;

            _categoryToolbar.Clear();
            _categoryButtons.Clear();

            List<TextGalleryCategory> categoryOrder = new List<TextGalleryCategory>
            {
                TextGalleryCategory.All
            };
            for (int cardIndex = 0; cardIndex < _cards.Count; cardIndex++)
            {
                TextGalleryCategory category = _cards[cardIndex].Category;
                if (!categoryOrder.Contains(category))
                    categoryOrder.Add(category);
            }

            _categoryToolbar.style.display = categoryOrder.Count > 2
                ? DisplayStyle.Flex
                : DisplayStyle.None;

            for (int categoryIndex = 0; categoryIndex < categoryOrder.Count; categoryIndex++)
            {
                TextGalleryCategory category = categoryOrder[categoryIndex];
                TextGalleryCategory capturedCategory = category;
                ToolbarToggle toggle = new ToolbarToggle
                {
                    text = GetCategoryDisplayName(category),
                    userData = category,
                    value = _selectedCategory == category
                };
                toggle.style.flexShrink = 0f;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (!evt.newValue)
                    {
                        toggle.SetValueWithoutNotify(_selectedCategory == capturedCategory);
                        return;
                    }

                    _selectedCategory = capturedCategory;
                    SyncCategoryToggles();
                    UpdateCardViews();
                });
                _categoryButtons.Add(toggle);
                _categoryToolbar.Add(toggle);
            }
        }

        void SyncCategoryToggles()
        {
            for (int i = 0; i < _categoryButtons.Count; i++)
            {
                ToolbarToggle toggle = _categoryButtons[i];
                if (toggle.userData is TextGalleryCategory category)
                    toggle.SetValueWithoutNotify(category == _selectedCategory);
            }
        }

        void ReloadCards()
        {
            _cards.Clear();
            AddCards(_cards);

            if (_selectedCategory != TextGalleryCategory.All &&
                !_cards.Exists(card => card.Category == _selectedCategory))
            {
                _selectedCategory = TextGalleryCategory.All;
            }
        }

        void ScheduleCatalogRefresh()
        {
            if (_catalogRefreshScheduled) return;
            _catalogRefreshScheduled = true;
            EditorApplication.delayCall += RefreshCatalog;
        }

        void RefreshCatalog()
        {
            _catalogRefreshScheduled = false;
            if (_grid == null) return;
            ReloadCards();
            UpdateCardViews(true);
        }

        void ScheduleSelectionRefresh()
        {
            if (_selectionRefreshScheduled)
                return;

            _selectionRefreshScheduled = true;
            EditorApplication.delayCall += ExecuteSelectionRefresh;
        }

        void ExecuteSelectionRefresh()
        {
            EditorApplication.delayCall -= ExecuteSelectionRefresh;
            _selectionRefreshScheduled = false;
            RefreshGalleryContext();
        }

        void RefreshGalleryContext()
        {
            if (rootVisualElement.panel == null)
                return;

            RefreshSelectionContext();
            UpdateContextBanner();
            RefreshCardActions();
        }

        void RenderCards()
        {
            if (_grid == null)
                return;
            UpdateCardViews();
        }

        void BindVisibleCard(VisualElement container, TextGalleryCard card, int index)
        {
            CardView view = container.childCount == 1 ? container[0].userData as CardView : null;
            if (view == null || !ReferenceEquals(view.Card, card))
            {
                container.Clear();
                view = CreateCardElement(card);
                view.Root.userData = view;
                CardView captured = view;
                view.Root.RegisterCallback<AttachToPanelEvent>(_ =>
                {
                    if (!_cardViews.Contains(captured)) _cardViews.Add(captured);
                });
                view.Root.RegisterCallback<DetachFromPanelEvent>(_ => _cardViews.Remove(captured));
                container.Add(view.Root);
            }
            view.Root.style.width = Length.Percent(100f);
            view.Root.style.height = _grid.ActualTileHeight;
            view.Root.style.minHeight = 0f;
            view.Root.style.marginLeft = 0f;
            view.Root.style.marginRight = 0f;
            view.Root.style.marginTop = 0f;
            view.Root.style.marginBottom = 0f;
            view.Preview.style.height = GetPreviewHeight(_grid.ActualTileWidth);
            RefreshCardAction(view);
        }

        CardView CreateCardElement(TextGalleryCard card)
        {
            float previewHeight = GetPreviewHeight(_cardWidth);
            VisualElement cardElement = new VisualElement();
            cardElement.AddToClassList("ts-gallery-card");
            cardElement.style.width = _cardWidth;
            cardElement.style.minHeight = previewHeight + 146f;
            cardElement.style.marginLeft = 6f;
            cardElement.style.marginRight = 6f;
            cardElement.style.marginTop = 6f;
            cardElement.style.marginBottom = 6f;
            cardElement.style.paddingLeft = 8f;
            cardElement.style.paddingRight = 8f;
            cardElement.style.paddingTop = 8f;
            cardElement.style.paddingBottom = 8f;
            cardElement.style.borderTopLeftRadius = 6f;
            cardElement.style.borderTopRightRadius = 6f;
            cardElement.style.borderBottomLeftRadius = 6f;
            cardElement.style.borderBottomRightRadius = 6f;
            cardElement.style.borderLeftWidth = 1f;
            cardElement.style.borderRightWidth = 1f;
            cardElement.style.borderTopWidth = 1f;
            cardElement.style.borderBottomWidth = 1f;

            VisualElement preview = CreatePreviewElement(card, new TextGalleryPreviewContext(false));
            preview.tooltip = GetCapabilityTooltip(card.PreviewQuality);
            preview.style.height = previewHeight;
            preview.style.marginBottom = 8f;
            preview.style.flexGrow = 0f;
            preview.style.flexShrink = 0f;
            cardElement.Add(preview);

            VisualElement titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            cardElement.Add(titleRow);

            Label title = new Label(card.DisplayTitle) { enableRichText = false };
            title.style.flexGrow = 1f;
            title.style.flexShrink = 1f;
            title.style.minWidth = 0f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.whiteSpace = WhiteSpace.Normal;
            title.tooltip = card.ProductionMetadata.IsSpecified
                ? card.Title + "\n\n" + card.ProductionMetadataTooltip
                : card.Title;
            title.style.maxHeight = 38f;
            title.style.overflow = Overflow.Hidden;
            titleRow.Add(title);
            Label capabilityBadge = CreateCapabilityBadge(card);
            if (capabilityBadge != null)
                titleRow.Add(capabilityBadge);

            if (card.AdditionalBadges.Count > 0)
            {
                VisualElement badgeRow = new VisualElement();
                badgeRow.style.flexDirection = FlexDirection.Row;
                badgeRow.style.flexWrap = Wrap.Wrap;
                badgeRow.style.marginTop = 4f;
                for (int i = 0; i < card.AdditionalBadges.Count; i++)
                    badgeRow.Add(CreateSupplementaryBadge(card.AdditionalBadges[i]));
                cardElement.Add(badgeRow);
            }

            Label description = new Label(card.Description);
            description.style.whiteSpace = WhiteSpace.Normal;
            description.style.marginTop = 6f;
            description.style.minHeight = 42f;
            description.style.flexGrow = 1f;
            description.style.overflow = Overflow.Hidden;
            description.tooltip = card.Description;
            cardElement.Add(description);

            VisualElement actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.marginTop = 8f;
            cardElement.Add(actionRow);

            Button primaryButton = new Button(() => InvokeCardAction(card, false));
            primaryButton.style.height = 24f;
            primaryButton.style.flexGrow = 1f;
            actionRow.Add(primaryButton);

            Button secondaryButton = string.IsNullOrEmpty(card.SecondaryActionIcon)
                ? new Button(() => InvokeCardAction(card, true))
                : CommonUITK.CreateIconButton(card.SecondaryActionTooltip, card.SecondaryActionIcon,
                    () => InvokeCardAction(card, true), null, null, "ts-gallery-action-icon");
            if (secondaryButton.Q<Image>() != null)
                secondaryButton.AddToClassList("ts-gallery-icon-button");
            secondaryButton.style.height = 24f;
            secondaryButton.style.marginLeft = 5f;
            actionRow.Add(secondaryButton);

            CardView view = new CardView(card, cardElement, preview, primaryButton, secondaryButton);
            RefreshCardAction(view);
            return view;
        }

        void UpdateCardViews(bool preserveSelection = false)
        {
            if (_grid == null)
                return;
            // Catalog refreshes retain an identity; changed filters start a new selection.
            string selectedId = preserveSelection && _grid.ActiveIndex >= 0 && _grid.ActiveIndex < _filteredCards.Count
                ? _filteredCards[_grid.ActiveIndex].Id : null;
            Vector2 scroll = preserveSelection ? _grid.ScrollView.scrollOffset : Vector2.zero;
            _filteredCards.Clear();
            string search = _searchText?.Trim();
            for (int i = 0; i < _cards.Count; i++)
            {
                TextGalleryCard card = _cards[i];
                bool visible = (_selectedCategory == TextGalleryCategory.All ||
                                card.Category == _selectedCategory) && card.MatchesSearch(search) &&
                               (_origin == "All" || card.IsCustom == (_origin == "Custom"));
                if (visible)
                    _filteredCards.Add(card);
            }
            UpdateCardLayout();
            _grid.SetItems(_filteredCards);
            if (!string.IsNullOrEmpty(selectedId))
            {
                int selected = _filteredCards.FindIndex(card => card.Id == selectedId);
                if (selected >= 0) _grid.SetSelection(new[] { selected }, selected);
            }
            _grid.ScrollView.scrollOffset = scroll;
            if (_emptyLabel != null)
                _emptyLabel.style.display = _filteredCards.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void UpdateCardLayout()
        {
            float width = _grid.ActualTileWidth > 0f ? _grid.ActualTileWidth : _cardWidth;
            _grid.SetLayout(_cardWidth, width / (GetPreviewHeight(width) + CardContentHeight), 6f, true);
        }

        void UpdateContextBanner()
        {
            if (_contextLabel == null)
                return;

            string message = ContextMessage;
            _contextLabel.text = message ?? string.Empty;
            _contextLabel.style.display = string.IsNullOrWhiteSpace(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        void RefreshCardActions()
        {
            for (int i = 0; i < _cardViews.Count; i++)
                RefreshCardAction(_cardViews[i]);
        }

        static void RefreshCardAction(CardView view)
        {
            TextGalleryCardActionState state = view.Card.ResolveActionState();
            view.PrimaryButton.text = state.ActionLabel;
            view.PrimaryButton.tooltip = state.ActionTooltip;
            view.PrimaryButton.SetEnabled(state.PrimaryAction != null);
            view.SecondaryButton.text = view.SecondaryButton.Q<Image>() == null
                ? state.SecondaryActionLabel
                : string.Empty;
            view.SecondaryButton.tooltip = state.SecondaryActionTooltip;
            view.SecondaryButton.style.display = state.SecondaryAction == null
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        static void InvokeCardAction(TextGalleryCard card, bool secondary)
        {
            GameObject previousSelection = Selection.activeGameObject;
            TextGalleryCardActionState state = card.ResolveActionState();
            Action action = secondary ? state.SecondaryAction : state.PrimaryAction;
            action?.Invoke();

            GameObject selected = Selection.activeGameObject;
            if (selected != null && selected != previousSelection)
                TextGallerySelectionUtility.FrameAfterLayout(selected);
        }

        static float GetPreviewHeight(float cardWidth)
        {
            return Mathf.Max(1f, cardWidth - CardHorizontalInsets) / PreviewAspectRatio;
        }

        static Label CreateCapabilityBadge(TextGalleryCard card)
        {
            string text;
            Color background;

            switch (card.PreviewQuality)
            {
                case TextGalleryPreviewQuality.NativeUITK:
                    if (TextStudioUITKCapabilities.FullParityAvailable)
                        return null;
                    text = "Basic preview";
                    background = new Color(0.32f, 0.28f, 0.12f, 1f);
                    break;
                case TextGalleryPreviewQuality.Fallback:
                    text = "Fallback";
                    background = new Color(0.29f, 0.25f, 0.19f, 1f);
                    break;
                case TextGalleryPreviewQuality.RenderedStatic:
                case TextGalleryPreviewQuality.RenderedLive:
                    return null;
                case TextGalleryPreviewQuality.RenderedTMPMaterial:
                    text = "TMP Material";
                    background = new Color(0.24f, 0.19f, 0.36f, 1f);
                    break;
                case TextGalleryPreviewQuality.None:
                default:
                    text = "Create-only";
                    background = new Color(0.24f, 0.24f, 0.24f, 1f);
                    break;
            }

            Label badge = new Label(text);
            badge.style.flexShrink = 0f;
            badge.tooltip = GetCapabilityTooltip(card.PreviewQuality);
            badge.style.fontSize = 10f;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.height = 17f;
            badge.style.marginLeft = 6f;
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;
            badge.style.backgroundColor = background;
            badge.style.color = Color.white;
            return badge;
        }

        static Label CreateSupplementaryBadge(string text)
        {
            Label badge = new Label(text ?? string.Empty);
            badge.tooltip = text == "Screen + Glyph"
                ? "Includes screen-stable cells and glyph-anchored cells that follow text transforms."
                : string.Empty;
            badge.style.fontSize = 10f;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.paddingLeft = 5f;
            badge.style.paddingRight = 5f;
            badge.style.height = 17f;
            badge.style.marginRight = 5f;
            badge.style.borderTopLeftRadius = 4f;
            badge.style.borderTopRightRadius = 4f;
            badge.style.borderBottomLeftRadius = 4f;
            badge.style.borderBottomRightRadius = 4f;
            badge.style.backgroundColor = new Color(0.11f, 0.27f, 0.31f, 1f);
            badge.style.color = Color.white;
            return badge;
        }

        static string GetCapabilityTooltip(TextGalleryPreviewQuality previewQuality)
        {
            switch (previewQuality)
            {
                case TextGalleryPreviewQuality.NativeUITK:
                    return TextStudioUITKCapabilities.FullParityAvailable
                        ? "Native UI Toolkit glyph preview. Material-channel effects are identified separately."
                        : "Basic UI Toolkit compatibility preview for this Unity version.";
                case TextGalleryPreviewQuality.RenderedLive:
                    return "Live TextMeshPro render of the complete effect lifecycle.";
                case TextGalleryPreviewQuality.RenderedTMPMaterial:
                    return "Full-fidelity TextMeshPro material output. UI Toolkit text does not support this material effect.";
                case TextGalleryPreviewQuality.RenderedStatic:
                    return "Pre-rendered product output.";
                case TextGalleryPreviewQuality.Fallback:
                    return "Compatibility preview used when the native renderer is unavailable.";
                default:
                    return "Create the example to inspect its output.";
            }
        }

        public static VisualElement CreatePreviewElement(TextGalleryCard card, TextGalleryPreviewContext context)
        {
            if (card == null)
                return CreateFallbackPreview("Preview unavailable");

            if (card.PreviewFactory != null)
            {
                try
                {
                    VisualElement preview = card.PreviewFactory(context ?? new TextGalleryPreviewContext(false));
                    if (preview != null)
                        return preview;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Text Studio] Gallery preview failed for {card.Id}. {ex.Message}");
                }
            }

            return CreateFallbackPreview("Preview unavailable");
        }

        static VisualElement CreateFallbackPreview(string message)
        {
            VisualElement preview = new VisualElement();
            preview.style.flexGrow = 1f;
            preview.style.justifyContent = Justify.Center;
            preview.style.alignItems = Align.Center;
            preview.style.borderTopLeftRadius = 4f;
            preview.style.borderTopRightRadius = 4f;
            preview.style.borderBottomLeftRadius = 4f;
            preview.style.borderBottomRightRadius = 4f;
            preview.style.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

            Label label = new Label(message);
            label.style.color = Color.white;
            label.style.opacity = 0.62f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            preview.Add(label);
            return preview;
        }

        sealed class CardView
        {
            public TextGalleryCard Card { get; }
            public VisualElement Root { get; }
            public VisualElement Preview { get; }
            public Button PrimaryButton { get; }
            public Button SecondaryButton { get; }

            public CardView(
                TextGalleryCard card,
                VisualElement root,
                VisualElement preview,
                Button primaryButton,
                Button secondaryButton)
            {
                Card = card;
                Root = root;
                Preview = preview;
                PrimaryButton = primaryButton;
                SecondaryButton = secondaryButton;
            }
        }

        internal static string GetCategoryDisplayName(TextGalleryCategory category)
        {
            return category switch
            {
                TextGalleryCategory.Design => "Visual Designs",
                TextGalleryCategory.ThreeD => "3D Styles",
                _ => category.ToString()
            };
        }
    }

    /// <summary>Creates an editor-owned preview element for a Text Gallery card.</summary>
    /// <param name="context">Preview size and presentation context.</param>
    /// <returns>A new visual element owned by the gallery.</returns>
    public delegate VisualElement TextGalleryPreviewFactory(TextGalleryPreviewContext context);

    /// <summary>Describes the gallery surface requesting a card preview.</summary>
    public sealed class TextGalleryPreviewContext
    {
        public bool IsLargePreview { get; }

        /// <summary>Creates context for a card or expanded preview.</summary>
        public TextGalleryPreviewContext(bool isLargePreview)
        {
            IsLargePreview = isLargePreview;
        }
    }

    /// <summary>Immutable gallery contribution describing discovery text, preview content, and user actions.</summary>
    public sealed class TextGalleryCard
    {
        public bool IsCustom { get; private set; }
        internal TextGalleryCard WithCustomOrigin() { IsCustom = true; return this; }
        public string Id { get; }
        public string Title { get; }
        internal string DisplayTitle { get; private set; }
        public string Description { get; }
        public TextGalleryCategory Category { get; }
        public Action PrimaryAction => ResolveActionState().PrimaryAction;
        public TextGalleryPreviewFactory PreviewFactory { get; }
        public TextGalleryPreviewQuality PreviewQuality { get; }
        public IReadOnlyList<string> SearchKeywords => _searchKeywords;
        public IReadOnlyList<string> AdditionalBadges => _additionalBadges;
        public TextGalleryProductionMetadata ProductionMetadata { get; }
        internal string ProductionMetadataDisplayLine { get; }
        internal string ProductionMetadataTooltip { get; }
        public string ActionLabel => ResolveActionState().ActionLabel;
        public string ActionTooltip => ResolveActionState().ActionTooltip;
        public Action SecondaryAction => ResolveActionState().SecondaryAction;
        public string SecondaryActionLabel => ResolveActionState().SecondaryActionLabel;
        public string SecondaryActionTooltip => ResolveActionState().SecondaryActionTooltip;
        internal string SecondaryActionIcon { get; private set; }
        internal TextGalleryCaptureDescriptor CaptureDescriptor { get; private set; }

        readonly string[] _searchKeywords;
        readonly string[] _additionalBadges;
        readonly TextGalleryCardActionState _staticActionState;
        readonly Func<TextGalleryCardActionState> _actionStateProvider;

        /// <summary>Creates an action-only card without a visual preview.</summary>
        public TextGalleryCard(string id, string title, string description, TextGalleryCategory category, Action primaryAction)
            : this(id, title, description, category, primaryAction, null, TextGalleryPreviewQuality.None)
        {
        }

        /// <summary>Creates a card with a preview factory and fixed preview quality.</summary>
        public TextGalleryCard(
            string id,
            string title,
            string description,
            TextGalleryCategory category,
            Action primaryAction,
            TextGalleryPreviewFactory previewFactory,
            TextGalleryPreviewQuality previewQuality)
            : this(id, title, description, category, primaryAction, previewFactory, previewQuality, null)
        {
        }

        /// <summary>Creates a fully configured gallery card with dynamic actions and search metadata.</summary>
        public TextGalleryCard(
            string id,
            string title,
            string description,
            TextGalleryCategory category,
            Action primaryAction,
            TextGalleryPreviewFactory previewFactory,
            TextGalleryPreviewQuality previewQuality,
            IEnumerable<string> searchKeywords,
            string actionLabel = "Create",
            string actionTooltip = "",
            Action secondaryAction = null,
            string secondaryActionLabel = "",
            string secondaryActionTooltip = "",
            Func<TextGalleryCardActionState> actionStateProvider = null,
            IEnumerable<string> additionalBadges = null,
            TextGalleryProductionMetadata productionMetadata = default)
        {
            Id = id ?? string.Empty;
            Title = title ?? string.Empty;
            DisplayTitle = Title;
            Description = description ?? string.Empty;
            Category = category;
            PreviewFactory = previewFactory;
            PreviewQuality = previewFactory == null ? TextGalleryPreviewQuality.None : previewQuality;
            _searchKeywords = NormalizeSearchKeywords(searchKeywords);
            _additionalBadges = NormalizeSearchKeywords(additionalBadges);
            ProductionMetadata = productionMetadata;
            ProductionMetadataDisplayLine = productionMetadata.BuildDisplayLine();
            ProductionMetadataTooltip = productionMetadata.BuildTooltip();
            _staticActionState = new TextGalleryCardActionState(
                primaryAction,
                actionLabel,
                actionTooltip,
                secondaryAction,
                secondaryActionLabel,
                secondaryActionTooltip);
            _actionStateProvider = actionStateProvider;
        }

        internal TextGalleryCardActionState ResolveActionState()
        {
            return _actionStateProvider?.Invoke() ?? _staticActionState;
        }

        internal TextGalleryCard WithCaptureDescriptor(TextGalleryCaptureDescriptor descriptor)
        {
            CaptureDescriptor = descriptor;
            return this;
        }

        internal TextGalleryCard WithSecondaryActionIcon(string iconName)
        {
            SecondaryActionIcon = iconName;
            return this;
        }

        internal TextGalleryCard WithDisplayTitle(string title)
        {
            DisplayTitle = string.IsNullOrWhiteSpace(title) ? Title : title;
            return this;
        }

        /// <summary>Matches trimmed search text against the card ID, title, description, and keywords.</summary>
        public bool MatchesSearch(string search)
        {
            string value = search?.Trim();
            if (string.IsNullOrEmpty(value))
                return true;

            if (Id.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (Title.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (DisplayTitle.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (Description.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (ProductionMetadataDisplayLine.IndexOf(
                    value,
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                ProductionMetadataTooltip.IndexOf(
                    value,
                    StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            for (int i = 0; i < _searchKeywords.Length; i++)
            {
                if (_searchKeywords[i].IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        static string[] NormalizeSearchKeywords(IEnumerable<string> searchKeywords)
        {
            if (searchKeywords == null)
                return Array.Empty<string>();

            List<string> keywords = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string keyword in searchKeywords)
            {
                string normalized = keyword?.Trim();
                if (string.IsNullOrEmpty(normalized) || !seen.Add(normalized))
                    continue;

                keywords.Add(normalized);
            }

            return keywords.Count == 0 ? Array.Empty<string>() : keywords.ToArray();
        }
    }

    /// <summary>Current primary and optional secondary actions displayed by a gallery card.</summary>
    public sealed class TextGalleryCardActionState
    {
        public Action PrimaryAction { get; }
        public string ActionLabel { get; }
        public string ActionTooltip { get; }
        public Action SecondaryAction { get; }
        public string SecondaryActionLabel { get; }
        public string SecondaryActionTooltip { get; }

        /// <summary>Creates an action snapshot with normalized labels and tooltips.</summary>
        public TextGalleryCardActionState(
            Action primaryAction,
            string actionLabel,
            string actionTooltip = "",
            Action secondaryAction = null,
            string secondaryActionLabel = "",
            string secondaryActionTooltip = "")
        {
            PrimaryAction = primaryAction;
            ActionLabel = string.IsNullOrWhiteSpace(actionLabel) ? "Create" : actionLabel;
            ActionTooltip = actionTooltip ?? string.Empty;
            SecondaryAction = secondaryAction;
            SecondaryActionLabel = secondaryActionLabel ?? string.Empty;
            SecondaryActionTooltip = secondaryActionTooltip ?? string.Empty;
        }
    }

    static class TextGallerySelectionUtility
    {
        public static void FrameAfterLayout(GameObject target)
        {
            EditorApplication.delayCall += () =>
            {
                if (target == null || Selection.activeGameObject != target)
                    return;

                SceneView sceneView = SceneView.lastActiveSceneView;
                if (sceneView != null)
                    sceneView.FrameSelected();
            };
        }
    }

    /// <summary>Identifies the preview route actually provided by a gallery card, including native UI Toolkit, fallbacks, static or live rendering, and TMP material previews.</summary>
    public enum TextGalleryPreviewQuality
    {
        None,
        NativeUITK,
        Fallback,
        RenderedStatic,
        RenderedLive,
        RenderedTMPMaterial
    }

    /// <summary>Selects the gallery's setup, reveal, effects, design, placement, or add-on category, with an option to show every card.</summary>
    public enum TextGalleryCategory
    {
        All,
        Setup,
        Reveal,
        Effects,
        Design,
        Placement,
        Mechanical,
        ThreeD,
        Terminal
    }

    internal sealed class TextGalleryWindow : TextGalleryWindowBase
    {
        protected override bool RefreshWhenSelectionChanges => true;

        protected override string ContextMessage
        {
            get
            {
                TextStudioAppearanceBackendSummary summary =
                    TextStudioAppearanceBackendSupport.EvaluateSelection();
                if (!summary.HasUnsupportedTargets)
                    return string.Empty;

                if (summary.TargetCount == 1)
                {
                    string backend = string.IsNullOrWhiteSpace(summary.BackendName)
                        ? "The active text backend"
                        : summary.BackendName;
                    return backend +
                           " owns glyph styling. Text Studio 2D Appearance presets remain assigned but inactive on the selected object.";
                }

                return summary.UnsupportedCount + " of " + summary.TargetCount +
                       " selected Text Studio objects use backends that own glyph styling. Text Studio 2D Appearance presets remain assigned but inactive on those objects.";
            }
        }

        [MenuItem(TextStudioMenuPaths.TextGallery, false, TextStudioMenuPaths.TextGalleryPriority)]
        public static void ShowWindow()
        {
            TextGalleryWindow window = GetWindow<TextGalleryWindow>();
            window.titleContent = new GUIContent("Text Gallery", EditorGUIUtility.IconContent("d_TextAsset Icon").image);
            window.minSize = new Vector2(320f, 420f);
            window.Show();
        }

        protected override void AddCards(List<TextGalleryCard> cards)
        {
            new TextStudioCoreGalleryProvider().AddCards(cards);
        }
    }
}
