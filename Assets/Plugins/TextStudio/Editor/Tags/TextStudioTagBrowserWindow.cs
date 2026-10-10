using ImpossibleRobert.Common;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal readonly struct TextStudioTagInsertionSnapshot
    {
        public string SourceText { get; }
        public int SelectionStart { get; }
        public int SelectionEnd { get; }
        public bool HasSelection => SelectionEnd > SelectionStart;

        public TextStudioTagInsertionSnapshot(
            string sourceText,
            int cursorIndex,
            int selectIndex)
        {
            SourceText = sourceText ?? string.Empty;
            int cursor = Math.Max(0, Math.Min(cursorIndex, SourceText.Length));
            int selection = Math.Max(0, Math.Min(selectIndex, SourceText.Length));
            SelectionStart = Math.Min(cursor, selection);
            SelectionEnd = Math.Max(cursor, selection);
        }

        public static TextStudioTagInsertionSnapshot Capture(TextField sourceField)
        {
            string source = sourceField?.value ?? string.Empty;
            int cursor = sourceField != null ? sourceField.cursorIndex : source.Length;
            int selection = sourceField != null ? sourceField.selectIndex : cursor;
            return new TextStudioTagInsertionSnapshot(source, cursor, selection);
        }

        public TextStudioTagSourceEditor.EditResult CreateEdit(string snippet)
        {
            return TextStudioTagSourceEditor.ReplaceSelectionWithSnippet(
                SourceText,
                SelectionStart,
                SelectionEnd,
                snippet);
        }

        public string ResolveSnippet(string snippet)
        {
            TextStudioTagSourceEditor.EditResult edit = CreateEdit(snippet);
            int sourceLength = SourceText?.Length ?? 0;
            int replacedLength = SelectionEnd - SelectionStart;
            int insertedLength = edit.Text.Length - (sourceLength - replacedLength);
            return insertedLength > 0
                ? edit.Text.Substring(SelectionStart, insertedLength)
                : string.Empty;
        }
    }

    internal sealed class TextStudioTagInsertionCapture : IDisposable
    {
        readonly TextField _sourceField;
        readonly VisualElement _activationTarget;
        TextStudioTagInsertionSnapshot _snapshot;
        bool _hasSnapshot;

        public TextStudioTagInsertionCapture(TextField sourceField, VisualElement activationTarget)
        {
            _sourceField = sourceField ?? throw new ArgumentNullException(nameof(sourceField));
            _activationTarget = activationTarget ??
                                throw new ArgumentNullException(nameof(activationTarget));
            _activationTarget.RegisterCallback<PointerDownEvent>(
                OnActivationPointerDown,
                TrickleDown.TrickleDown);
            _sourceField.RegisterCallback<FocusInEvent>(OnSourceFocusIn);
            _sourceField.RegisterCallback<FocusOutEvent>(
                OnSourceFocusOut,
                TrickleDown.TrickleDown);
            _sourceField.RegisterValueChangedCallback(OnSourceValueChanged);
        }

        public void Dispose()
        {
            _activationTarget.UnregisterCallback<PointerDownEvent>(OnActivationPointerDown, TrickleDown.TrickleDown);
            _sourceField.UnregisterCallback<FocusInEvent>(OnSourceFocusIn);
            _sourceField.UnregisterCallback<FocusOutEvent>(OnSourceFocusOut, TrickleDown.TrickleDown);
            _sourceField.UnregisterValueChangedCallback(OnSourceValueChanged);
        }

        public TextStudioTagInsertionSnapshot Consume()
        {
            TextStudioTagInsertionSnapshot result = _hasSnapshot
                ? _snapshot
                : TextStudioTagInsertionSnapshot.Capture(_sourceField);
            _hasSnapshot = false;
            return result;
        }

        void OnActivationPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 0)
                Capture();
        }

        void OnSourceFocusIn(FocusInEvent evt)
        {
            _hasSnapshot = false;
        }

        void OnSourceFocusOut(FocusOutEvent evt)
        {
            if (_hasSnapshot || !IsSameOrDescendant(evt.relatedTarget as VisualElement, _activationTarget))
                return;

            Capture();
        }

        void OnSourceValueChanged(ChangeEvent<string> evt)
        {
            _hasSnapshot = false;
        }

        void Capture()
        {
            _snapshot = TextStudioTagInsertionSnapshot.Capture(_sourceField);
            _hasSnapshot = true;
        }

        static bool IsSameOrDescendant(VisualElement candidate, VisualElement root)
        {
            while (candidate != null)
            {
                if (candidate == root)
                    return true;

                candidate = candidate.parent;
            }

            return false;
        }
    }

    internal sealed class TextStudioTagBrowserWindow : EditorWindow
    {
        const float WindowWidth = 640f;
        const float WindowHeight = 520f;

        TextStudioTagBrowserModel _model;
        TextStudioTagAnalysisContext _context;
        TextStudioTagInsertionSnapshot _insertion;
        Action<TextStudioTagSourceEditor.EditResult> _insert;
        readonly List<Button> _groupButtons = new List<Button>();
        readonly List<TagCardView> _cardViews = new List<TagCardView>();
        VisualElement _browsePane;
        ToolbarSearchField _searchField;
        VisualElement _groupToolbar;
        ScrollView _cards;
        ScrollView _detailPane;
        Label _countLabel;
        Label _emptyLabel;
        string _selectedGroupId = TextStudioTagBrowserModel.AllGroupId;
        string _search = string.Empty;

        public static void Show(
            Rect anchor,
            TextStudioTagAnalysisContext context,
            TextStudioTagInsertionSnapshot insertion,
            Action<TextStudioTagSourceEditor.EditResult> insert)
        {
            TextStudioTagBrowserWindow window = CreateInstance<TextStudioTagBrowserWindow>();
            window.titleContent = new GUIContent("Text Studio Tags");
            window.minSize = new Vector2(520f, 400f);
            window.Initialize(context, insertion, insert);
            window.position = ResolvePosition(anchor);
            window.ShowAuxWindow();
            window.Build();
        }

        internal void Initialize(
            TextStudioTagAnalysisContext context,
            TextStudioTagInsertionSnapshot insertion,
            Action<TextStudioTagSourceEditor.EditResult> insert)
        {
            _context = context ?? new TextStudioTagAnalysisContext();
            _model = TextStudioTagBrowserModel.ForCaret(_context, insertion.SelectionStart);
            _insertion = insertion;
            _insert = insert;
        }

        void CreateGUI()
        {
            _context ??= new TextStudioTagAnalysisContext();
            _model ??= TextStudioTagBrowserModel.Build(_context);
            Build();
        }

        internal void Build()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("ts-tag-browser-root");
            root.EnableInClassList("ts-theme-dark", EditorGUIUtility.isProSkin);
            root.EnableInClassList("ts-theme-light", !EditorGUIUtility.isProSkin);
            StyleSheet style = TextStudioTextEditor.LoadTextStudioInspectorStyleSheet();
            if (style != null)
                root.styleSheets.Add(style);
            CommonUITK.ApplyWindowLayout(root);

            _browsePane = new VisualElement
            {
                name = "ts-tag-browser-browse"
            };
            _browsePane.AddToClassList("ts-tag-browser-browse");
            _browsePane.AddToClassList(CommonUITK.WindowBodyClass);
            CommonUITK.ApplyWindowLayout(_browsePane);
            root.Add(_browsePane);

            Toolbar toolbar = new Toolbar();
            toolbar.AddToClassList("ts-tag-browser-toolbar");
            _searchField = new ToolbarSearchField();
            _searchField.AddToClassList("ts-tag-browser-search");
            _searchField.SetValueWithoutNotify(_search);
            _searchField.RegisterValueChangedCallback(evt =>
            {
                _search = evt.newValue ?? string.Empty;
                UpdateCardVisibility();
            });
            toolbar.Add(_searchField);

            _countLabel = new Label();
            _countLabel.AddToClassList("ts-tag-browser-count");
            toolbar.Add(_countLabel);
            _browsePane.Add(toolbar);

            _groupToolbar = new VisualElement();
            _groupToolbar.AddToClassList("ts-tag-browser-groups");
            _browsePane.Add(_groupToolbar);
            BuildGroupTabs();

            _cards = new ScrollView(ScrollViewMode.Vertical);
            _cards.AddToClassList("ts-tag-browser-cards");
            _cards.AddToClassList("ts-tag-browser-list");
            _browsePane.Add(_cards);

            _detailPane = new ScrollView(ScrollViewMode.Vertical);
            _detailPane.name = "ts-tag-browser-configure";
            _detailPane.AddToClassList("ts-tag-browser-detail");
            root.Add(_detailPane);

            BuildCards();
            ShowBrowser();
        }

        void BuildGroupTabs()
        {
            _groupToolbar.Clear();
            _groupButtons.Clear();
            for (int i = 0; i < _model.Groups.Count; i++)
            {
                TextStudioTagBrowserGroup group = _model.Groups[i];
                string capturedId = group.Id;
                Button button = new Button(() =>
                {
                    if (string.Equals(capturedId, _selectedGroupId, StringComparison.OrdinalIgnoreCase))
                        return;

                    _selectedGroupId = capturedId;
                    SyncGroupTabs();
                    UpdateCardVisibility();
                })
                {
                    text = group.DisplayName,
                    tooltip = "Show " + group.DisplayName + " tags",
                    userData = capturedId
                };
                button.AddToClassList("ts-tag-browser-group-toggle");
                _groupButtons.Add(button);
                _groupToolbar.Add(button);
            }

            SyncGroupTabs();
        }

        void SyncGroupTabs()
        {
            for (int i = 0; i < _groupButtons.Count; i++)
            {
                string groupId = _groupButtons[i].userData as string;
                bool active = string.Equals(groupId, _selectedGroupId, StringComparison.OrdinalIgnoreCase);
                SetClass(_groupButtons[i], "ts-tag-browser-group-active", active);
            }
        }

        void BuildCards()
        {
            if (_cards == null)
                return;

            _cards.Clear();
            _cardViews.Clear();
            IReadOnlyList<TextStudioTagBrowserItem> allItems = _model.Items;
            for (int i = 0; i < allItems.Count; i++)
            {
                TextStudioTagCardElement card = new TextStudioTagCardElement(allItems[i], ChooseItem);
                _cardViews.Add(new TagCardView(allItems[i], card));
                _cards.Add(card);
            }

            _emptyLabel = new Label("No tags match the current filters.");
            _emptyLabel.AddToClassList("ts-tag-browser-empty");
            _cards.Add(_emptyLabel);
            UpdateCardVisibility();
        }

        void UpdateCardVisibility()
        {
            if (_cards == null)
                return;

            IReadOnlyList<TextStudioTagBrowserItem> items = _model.Filter(_selectedGroupId, _search);
            if (_countLabel != null)
                _countLabel.text = items.Count == 1 ? "1 tag" : items.Count + " tags";

            HashSet<TextStudioTagBrowserItem> visibleItems = new HashSet<TextStudioTagBrowserItem>(items);
            for (int i = 0; i < _cardViews.Count; i++)
            {
                TagCardView view = _cardViews[i];
                view.Root.style.display = visibleItems.Contains(view.Item)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }

            if (_emptyLabel != null)
                _emptyLabel.style.display = items.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        internal void ChooseItem(TextStudioTagBrowserItem item)
        {
            if (item.Definition.Kind == TextStudioTagKind.Appearance && item.Definition.Id.StartsWith("$", StringComparison.Ordinal))
            {
                if (!string.IsNullOrEmpty(item.Example)) InsertAndClose(item.Example);
                return;
            }
            if (TextStudioGuidedTagDraft.CanConfigure(item.Definition))
            {
                BuildGuidedEditor(new TextStudioGuidedTagDraft(item.Definition));
                ShowGuidedEditor();
                return;
            }

            InsertAndClose(TextStudioTagSourceEditor.CreateExample(item.Definition));
        }

        internal void ShowBrowser()
        {
            if (_browsePane != null)
                _browsePane.style.display = DisplayStyle.Flex;
            if (_detailPane != null)
                _detailPane.style.display = DisplayStyle.None;
            _searchField?.Focus();
        }

        void ShowGuidedEditor()
        {
            if (_browsePane != null)
                _browsePane.style.display = DisplayStyle.None;
            if (_detailPane != null)
                _detailPane.style.display = DisplayStyle.Flex;
        }

        void BuildGuidedEditor(TextStudioGuidedTagDraft draft)
        {
            _detailPane.Clear();
            _detailPane.scrollOffset = Vector2.zero;

            Button back = new Button(ShowBrowser)
            {
                name = "ts-tag-guide-back",
                text = "‹  Tags",
                tooltip = "Return to the searchable tag list."
            };
            back.AddToClassList("ts-tag-guide-back");
            _detailPane.Add(back);

            Label eyebrow = new Label("CONFIGURE TAG");
            eyebrow.AddToClassList("ts-tag-guide-eyebrow");
            _detailPane.Add(eyebrow);

            Label title = new Label(draft.Definition.DisplayName);
            title.AddToClassList("ts-tag-guide-title");
            _detailPane.Add(title);

            Label tag = new Label("<" + draft.Definition.Id + ">");
            tag.enableRichText = false;
            tag.AddToClassList("ts-tag-guide-tag");
            _detailPane.Add(tag);

            Label description = new Label(draft.Definition.Description);
            description.AddToClassList("ts-tag-guide-description");
            _detailPane.Add(description);

            Label previewSafety = new Label(
                draft.IsPreviewSafe()
                    ? "Preview safe: deterministic editor preview is supported."
                    : "Runtime action: browsing and configuration do not invoke this callback.");
            previewSafety.AddToClassList(
                draft.IsPreviewSafe()
                    ? "ts-tag-guide-preview-safe"
                    : "ts-tag-guide-runtime-only");
            _detailPane.Add(previewSafety);

            VisualElement parameterList = new VisualElement();
            parameterList.AddToClassList("ts-tag-guide-parameters");
            _detailPane.Add(parameterList);

            Action refresh = null;
            for (int i = 0; i < draft.ParameterCount; i++)
            {
                parameterList.Add(CreateParameterEditor(draft, i, () => refresh?.Invoke()));
            }

            Label markupLabel = new Label("Generated Markup");
            markupLabel.AddToClassList("ts-tag-guide-section-label");
            _detailPane.Add(markupLabel);

            TextField markup = new TextField
            {
                isReadOnly = true,
                multiline = false
            };
            markup.name = "ts-tag-guide-markup";
            markup.AddToClassList("ts-tag-guide-markup");
            _detailPane.Add(markup);

            Label validation = new Label();
            validation.name = "ts-tag-guide-validation";
            validation.AddToClassList("ts-tag-guide-validation");
            _detailPane.Add(validation);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("ts-tag-guide-actions");
            Button copy = new Button
            {
                text = "Copy",
                tooltip = "Copy the selection-aware markup without closing this window."
            };
            Button insert = new Button
            {
                text = "Insert",
                tooltip = "Apply the validated markup at the caret or selection captured before this window opened."
            };
            insert.AddToClassList("ts-primary-button");
            actions.Add(copy);
            actions.Add(insert);
            _detailPane.Add(actions);

            refresh = () =>
            {
                bool valid = draft.TryBuildSnippet(out string snippet, out string error);
                markup.SetValueWithoutNotify(valid ? _insertion.ResolveSnippet(snippet) : string.Empty);
                validation.text = valid
                    ? _insertion.HasSelection
                        ? "Ready to wrap the captured selection."
                        : "Ready to insert."
                    : error;
                SetClass(validation, "ts-tag-guide-validation-error", !valid);
                SetClass(validation, "ts-tag-guide-validation-ready", valid);
                copy.SetEnabled(valid);
                insert.SetEnabled(valid);
            };

            copy.clicked += () =>
            {
                if (draft.TryBuildSnippet(out string current, out _))
                    EditorGUIUtility.systemCopyBuffer = _insertion.ResolveSnippet(current);
            };
            insert.clicked += () =>
            {
                if (draft.TryBuildSnippet(out string current, out _))
                    InsertAndClose(current);
            };
            refresh();
        }

        VisualElement CreateParameterEditor(
            TextStudioGuidedTagDraft draft,
            int index,
            Action changed)
        {
            TextStudioTagParameterDefinition parameter = draft.GetParameter(index);
            VisualElement root = new VisualElement();
            root.AddToClassList("ts-tag-guide-parameter");

            VisualElement header = new VisualElement();
            header.AddToClassList("ts-tag-guide-parameter-header");
            root.Add(header);

            Label label = new Label(parameter.DisplayName);
            label.AddToClassList("ts-tag-guide-parameter-name");
            header.Add(label);

            Toggle include = new Toggle(parameter.Required ? "Required" : "Include")
            {
                value = draft.IsIncluded(index)
            };
            include.SetEnabled(!parameter.Required);
            include.AddToClassList("ts-tag-guide-parameter-include");
            header.Add(include);

            Label help = new Label(parameter.Description);
            help.AddToClassList("ts-tag-guide-parameter-help");
            root.Add(help);

            VisualElement inputRow = new VisualElement();
            inputRow.AddToClassList("ts-tag-guide-input-row");
            root.Add(inputRow);

            VisualElement input = CreateParameterInput(draft, index, parameter, changed);
            input.AddToClassList("ts-tag-guide-input");
            input.SetEnabled(draft.IsIncluded(index));
            inputRow.Add(input);

            if (!string.IsNullOrEmpty(parameter.SuggestionKey) &&
                _context.TryGetSuggestionValues(
                    parameter.SuggestionKey,
                    out IReadOnlyList<string> suggestions))
            {
                Button suggestionsButton = new Button(() =>
                    ShowSuggestions(input, draft, index, suggestions, changed))
                {
                    text = "Select",
                    tooltip = "Choose a route or identifier discovered in the current component context."
                };
                suggestionsButton.AddToClassList("ts-tag-guide-suggestions");
                suggestionsButton.SetEnabled(draft.IsIncluded(index));
                inputRow.Add(suggestionsButton);
                include.RegisterValueChangedCallback(evt =>
                    suggestionsButton.SetEnabled(parameter.Required || evt.newValue));
            }

            include.RegisterValueChangedCallback(evt =>
            {
                draft.SetIncluded(index, evt.newValue);
                input.SetEnabled(draft.IsIncluded(index));
                changed?.Invoke();
            });

            return root;
        }

        static VisualElement CreateParameterInput(
            TextStudioGuidedTagDraft draft,
            int index,
            TextStudioTagParameterDefinition parameter,
            Action changed)
        {
            string value = draft.GetValue(index);
            switch (parameter.Kind)
            {
                case TextStudioTagParameterKind.Float:
                {
                    FloatField field = new FloatField();
                    field.SetValueWithoutNotify(ParseFloat(value));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        draft.SetValue(index, evt.newValue.ToString("R", CultureInfo.InvariantCulture));
                        changed?.Invoke();
                    });
                    return field;
                }
                case TextStudioTagParameterKind.Integer:
                {
                    IntegerField field = new IntegerField();
                    field.SetValueWithoutNotify(ParseInteger(value));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        draft.SetValue(index, evt.newValue.ToString(CultureInfo.InvariantCulture));
                        changed?.Invoke();
                    });
                    return field;
                }
                case TextStudioTagParameterKind.Boolean:
                {
                    Toggle field = new Toggle();
                    field.SetValueWithoutNotify(ParseBoolean(value));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        draft.SetValue(index, evt.newValue ? "true" : "false");
                        changed?.Invoke();
                    });
                    return field;
                }
                case TextStudioTagParameterKind.Color:
                {
                    ColorField field = new ColorField();
                    field.SetValueWithoutNotify(ParseColor(value));
                    field.RegisterValueChangedCallback(evt =>
                    {
                        draft.SetValue(index, "#" + ColorUtility.ToHtmlStringRGBA(evt.newValue));
                        changed?.Invoke();
                    });
                    return field;
                }
                case TextStudioTagParameterKind.Enum:
                {
                    List<string> choices = new List<string>(parameter.AllowedValues);
                    if (choices.Count == 0)
                        choices.Add(value);
                    int selected = Mathf.Max(0, choices.FindIndex(candidate =>
                        string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase)));
                    DropdownField field = new DropdownField(choices, selected);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        draft.SetValue(index, evt.newValue);
                        changed?.Invoke();
                    });
                    return field;
                }
                default:
                {
                    TextField field = new TextField();
                    field.SetValueWithoutNotify(value);
                    field.RegisterValueChangedCallback(evt =>
                    {
                        draft.SetValue(index, evt.newValue);
                        changed?.Invoke();
                    });
                    return field;
                }
            }
        }

        static void ShowSuggestions(
            VisualElement input,
            TextStudioGuidedTagDraft draft,
            int index,
            IReadOnlyList<string> suggestions,
            Action changed)
        {
            GenericMenu menu = new GenericMenu();
            for (int i = 0; i < suggestions.Count; i++)
            {
                string suggestion = suggestions[i];
                menu.AddItem(new GUIContent(suggestion), false, () =>
                {
                    draft.SetValue(index, suggestion);
                    if (input is TextField field)
                        field.SetValueWithoutNotify(suggestion);
                    changed?.Invoke();
                });
            }

            menu.DropDown(input.worldBound);
        }

        static float ParseFloat(string value)
        {
            return float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float parsed) &&
                   !float.IsNaN(parsed) &&
                   !float.IsInfinity(parsed)
                ? parsed
                : 0f;
        }

        static int ParseInteger(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : 0;
        }

        static bool ParseBoolean(string value)
        {
            return bool.TryParse(value, out bool parsed) && parsed;
        }

        static Color ParseColor(string value)
        {
            return ColorUtility.TryParseHtmlString(value, out Color parsed)
                ? parsed
                : Color.white;
        }

        readonly struct TagCardView
        {
            public readonly TextStudioTagBrowserItem Item;
            public readonly VisualElement Root;

            public TagCardView(TextStudioTagBrowserItem item, VisualElement root)
            {
                Item = item;
                Root = root;
            }
        }

        void InsertAndClose(string snippet)
        {
            if (string.IsNullOrEmpty(snippet))
                return;

            _insert?.Invoke(_insertion.CreateEdit(snippet));
            Close();
        }

        static void SetClass(VisualElement element, string className, bool enabled)
        {
            if (enabled)
                element.AddToClassList(className);
            else
                element.RemoveFromClassList(className);
        }

        static Rect ResolvePosition(Rect anchor)
        {
            float x = anchor.width > 0f ? anchor.xMax - WindowWidth : anchor.x;
            if (x < 0f)
                x = anchor.x;
            float y = anchor.yMax + 4f;
            if (x <= 0f && y <= 4f)
            {
                Vector2 mouse = GUIUtility.GUIToScreenPoint(
                    Event.current != null ? Event.current.mousePosition : Vector2.zero);
                x = mouse.x;
                y = mouse.y;
            }

            return new Rect(Mathf.Max(0f, x), Mathf.Max(0f, y), WindowWidth, WindowHeight);
        }
    }
}
