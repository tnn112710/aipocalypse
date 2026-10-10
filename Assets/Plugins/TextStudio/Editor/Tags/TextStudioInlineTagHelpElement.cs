using System;
using System.Collections.Generic;
using ImpossibleRobert.Common;
using TextStudio.BuiltIns;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioInlineTagHelpElement : VisualElement
    {
        internal const float SuggestionRowHeight = 22f;
        const int VisibleSuggestionRows = 6;
        const float SuggestionViewportHeight = VisibleSuggestionRows * SuggestionRowHeight;
        const int SuggestionRowPoolSize = 8;
        const float CompactParameterLayoutWidth = 340f;
        const string SelectedRowClass = "ts-tag-inline-help-row-selected";
        const string DetailsOnlyClass = "ts-tag-inline-help--details-only";
        const string KeyboardHint = "↑↓ choose · Enter or Tab inserts · Esc hides tag help.";

        readonly Func<string, TextStudioTagAnalysisContext> _contextFactory;
        readonly Func<bool> _canRunScheduledUpdate;
        readonly Action<TextStudioTagSourceEditor.EditResult> _applyEdit;
        readonly Func<TextStudioMarkupSyntax> _syntaxFactory;
        readonly Action<TextStudioInlineTagHelpState> _convertAppearance;
        readonly Func<string> _authoredSource;
        readonly Action _sourceRestored;
        TextStudioTagContentSelection _contentSelection;

        TextField _sourceField;
        IVisualElementScheduledItem _caretPoll;
        string _lastSource;
        int _lastCursor = -1;
        int _lastSelection = -1;
        bool _isHelpPointerDown;
        readonly List<TextStudioInlineTagHelpItem> _visibleItems = new List<TextStudioInlineTagHelpItem>();
        ScrollView _suggestions;
        VisualElement _leadingSpace;
        VisualElement _trailingSpace;
        readonly List<SuggestionRow> _rowPool = new List<SuggestionRow>();
        VisualElement _details;
        TextStudioInlineTagHelpState _state;
        bool _showSuggestions;
        int _selectedIndex;
        string _dismissedSource;
        bool _refreshQueued;
        string _undoBefore;
        string _undoAfter;
        int _undoCursor;
        int _undoSelection;
        int _redoCursor;
        int _redoSelection;
        int _metadataGeneration = -1;
        ulong _registryRevision;

        sealed class SuggestionRow : VisualElement
        {
            internal readonly Button Action = new Button { focusable = false };
            internal readonly Label Origin = new Label { enableRichText = false };
            internal readonly Label Name = new Label { enableRichText = false };
            internal TextStudioInlineTagHelpItem Item;
            internal SuggestionRow(Action<SuggestionRow> accept)
            {
                AddToClassList("ts-tag-inline-help-row");
                style.height = SuggestionRowHeight;
                style.flexShrink = 0;
                // Clipping keeps the virtualized scroll extent exact even where host styles enlarge the row content.
                style.overflow = Overflow.Hidden;
                Action.AddToClassList("ts-tag-inline-help-tag-action");
                Name.AddToClassList("ts-tag-inline-help-token");
                TextStudioInspectorElements.ApplyCodeFont(Name);
                Origin.AddToClassList("ts-tag-inline-help-origin");
                Action.Add(Name);
                Action.Add(Origin);
                Add(Action);
                Action.clicked += () => accept(this);
            }

            // Rows show the token a completion inserts; the details header shows the full usage example.
            internal void Bind(TextStudioInlineTagHelpItem item, bool selected, bool closing)
            {
                Item = item;
                Action.name = "ts-tag-inline-help-complete-" + item.Definition.Id;
                Name.text = (closing ? "</" : "<") + item.Definition.Id + ">";
                if (!closing && item.Definition.Kind == TextStudioTagKind.Appearance && item.Example.StartsWith("<style", StringComparison.Ordinal))
                    Name.text = item.Example.Substring(0, item.Example.IndexOf('>') + 1);
                Action.tooltip = item.Definition.Description + "\n" + (closing
                    ? "Complete this closing token."
                    : item.Definition.Kind == TextStudioTagKind.Appearance
                        ? "Complete this appearance tag. An unfinished opening can insert a matching closing tag."
                        : "Complete this opening token without adding a closing tag.") + "\n" + KeyboardHint;
                Origin.text = item.IsExactMatch && item.Definition.Kind != TextStudioTagKind.Appearance ? string.Empty : item.GroupDisplayName;
                EnableInClassList(SelectedRowClass, selected);
            }

            internal void Unbind()
            {
                Item = default;
                Action.name = string.Empty;
                Action.tooltip = string.Empty;
                Action.SetEnabled(false);
                Name.text = string.Empty;
                Origin.text = string.Empty;
                EnableInClassList(SelectedRowClass, false);
            }
        }

        public TextStudioInlineTagHelpElement(
            Func<string, TextStudioTagAnalysisContext> contextFactory,
            Func<bool> canRunScheduledUpdate = null,
            Action<TextStudioTagSourceEditor.EditResult> applyEdit = null,
            Func<TextStudioMarkupSyntax> syntaxFactory = null,
            Action<TextStudioInlineTagHelpState> convertAppearance = null,
            Func<string> authoredSource = null,
            Action sourceRestored = null)
        {
            _contextFactory = contextFactory;
            _convertAppearance = convertAppearance;
            _authoredSource = authoredSource;
            _sourceRestored = sourceRestored;
            _canRunScheduledUpdate = canRunScheduledUpdate;
            _applyEdit = applyEdit;
            _syntaxFactory = syntaxFactory ?? (() => TextStudioProjectSettings.ActiveMarkupSyntax);
            AddToClassList("ts-tag-inline-help");
            style.display = DisplayStyle.None;
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            RegisterCallback<PointerDownEvent>(OnHelpPointerDown, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(OnHelpPointerUp, TrickleDown.TrickleDown);
            RegisterCallback<PointerCancelEvent>(OnHelpPointerCanceled, TrickleDown.TrickleDown);
            RegisterCallback<PointerCaptureOutEvent>(OnHelpPointerCaptureOut, TrickleDown.TrickleDown);
            RegisterCallback<FocusOutEvent>(OnHelpFocusOut);
            RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);
        }

        void OnGeometryChanged(GeometryChangedEvent evt)
        {
            EnableInClassList(
                "ts-tag-inline-help-compact-parameters",
                evt.newRect.width > 0f && evt.newRect.width < CompactParameterLayoutWidth);
        }

        public void AttachSourceField(TextField sourceField)
        {
            if (ReferenceEquals(_sourceField, sourceField))
                return;

            DetachSourceField();
            _sourceField = sourceField;
            if (_sourceField == null)
                return;

            _sourceField.selectAllOnFocus = false;
            _sourceField.selectAllOnMouseUp = false;
            if (string.IsNullOrEmpty(_sourceField.tooltip))
                _sourceField.tooltip = "Double-click to select a word. Triple-click inside a tag to select its contents across all lines, up to its closing tag or the end of the field. Use Select All (Ctrl+A / Cmd+A) for the whole field.";
            _contentSelection = new TextStudioTagContentSelection(_sourceField, _syntaxFactory);
            _sourceField.RegisterValueChangedCallback(OnSourceChanged);
            _sourceField.RegisterCallback<KeyUpEvent>(OnKeyUp);
            _sourceField.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Undo.undoRedoPerformed += OnUndoRedo;
            _sourceField.RegisterCallback<AttachToPanelEvent>(OnSourceAttachedToPanel);
            _sourceField.RegisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _sourceField.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _sourceField.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _sourceField.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            _sourceField.RegisterCallback<FocusOutEvent>(OnFocusOut);
            if (_sourceField.panel != null)
                EnsureCaretPolling();
        }

        public void DetachSourceField()
        {
            _contentSelection?.Dispose();
            _contentSelection = null;
            StopCaretPolling();
            if (_sourceField == null)
                return;

            _sourceField.UnregisterValueChangedCallback(OnSourceChanged);
            _sourceField.UnregisterCallback<KeyUpEvent>(OnKeyUp);
            _sourceField.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            Undo.undoRedoPerformed -= OnUndoRedo;
            _sourceField.UnregisterCallback<AttachToPanelEvent>(OnSourceAttachedToPanel);
            _sourceField.UnregisterCallback<FocusInEvent>(OnFocusIn, TrickleDown.TrickleDown);
            _sourceField.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _sourceField.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _sourceField.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            _sourceField.UnregisterCallback<FocusOutEvent>(OnFocusOut);
            _sourceField = null;
        }

        public void Refresh(bool allowEndFallback = false)
        {
            if (_isHelpPointerDown)
                return;

            if (_sourceField == null)
            {
                Hide();
                return;
            }

            string source = _sourceField.value ?? string.Empty;
            int cursor = Mathf.Clamp(_sourceField.cursorIndex, 0, source.Length);
            int selection = Mathf.Clamp(_sourceField.selectIndex, 0, source.Length);
            if (_sourceField.panel == null && cursor == 0 && selection == 0 && source.Length > 0)
            {
                cursor = source.Length;
                selection = cursor;
            }
            TextStudioTagAnalysisContext context = _contextFactory?.Invoke(source);
            TextStudioInlineTagHelpState state = TextStudioInlineTagHelpModel.Build(source, cursor, context);
            if (allowEndFallback && !state.Visible && cursor != source.Length)
                state = TextStudioInlineTagHelpModel.Build(source, source.Length, context);

            Render(state);
            CaptureCaretSnapshot(source, cursor, selection);
        }

        public void RefreshIfCaretChanged()
        {
            if (_sourceField == null)
                return;

            string source = _sourceField.value ?? string.Empty;
            int cursor = Mathf.Clamp(_sourceField.cursorIndex, 0, source.Length);
            int selection = Mathf.Clamp(_sourceField.selectIndex, 0, source.Length);
            if (_metadataGeneration == TextStudioAppearanceAuthoringProviders.Generation &&
                _registryRevision == TextStudioAppearanceAuthoringProviders.RegistryRevision &&
                cursor == _lastCursor &&
                selection == _lastSelection &&
                string.Equals(source, _lastSource, StringComparison.Ordinal))
            {
                return;
            }

            Refresh();
        }

        public void RefreshAfterCaretEvent()
        {
            if (_sourceField == null || _refreshQueued)
                return;
            _refreshQueued = true;
            TextField scheduledField = _sourceField;
            scheduledField.schedule.Execute(() =>
            {
                _refreshQueued = false;
                if (!ReferenceEquals(_sourceField, scheduledField) || !CanRunScheduledUpdate())
                    return;

                RefreshIfCaretChanged();
            }).StartingIn(0);
        }

        public void StartCaretPolling()
        {
            RefreshAfterCaretEvent();
            EnsureCaretPolling();
        }

        void EnsureCaretPolling()
        {
            if (_sourceField == null || _caretPoll != null)
                return;

            _caretPoll = TextStudioInspectorElements.SchedulePeriodicRefresh(
                _sourceField,
                PollCaret,
                PollCaret,
                100);
        }

        void PollCaret()
        {
            if (!CanRunScheduledUpdate())
            {
                StopCaretPolling();
                return;
            }

            if (!ContainsFocusedElement(_sourceField) && !ContainsFocusedElement(this))
                return;

            RefreshIfCaretChanged();
        }

        public void StopCaretPolling()
        {
            if (_caretPoll != null)
            {
                _caretPoll.Pause();
                _caretPoll = null;
            }

            _lastSource = null;
            _lastCursor = -1;
            _lastSelection = -1;
        }

        public void Hide()
        {
            style.display = DisplayStyle.None;
        }

        void EnsureRows()
        {
            if (_suggestions != null) return;
            _suggestions = new ScrollView(ScrollViewMode.Vertical) { name = "ts-tag-inline-suggestions", focusable = false };
            _suggestions.AddToClassList("ts-tag-inline-help-suggestions");
            _suggestions.style.maxHeight = SuggestionViewportHeight;
            _leadingSpace = new VisualElement(); _leadingSpace.style.flexShrink = 0;
            _trailingSpace = new VisualElement(); _trailingSpace.style.flexShrink = 0;
            _suggestions.Add(_leadingSpace);
            for (int i = 0; i < SuggestionRowPoolSize; i++)
            {
                SuggestionRow row = new SuggestionRow(selected => CompleteTag(_state, selected.Item.Definition));
                _rowPool.Add(row); _suggestions.Add(row);
            }
            _suggestions.Add(_trailingSpace);
            _suggestions.verticalScroller.valueChanged += _ => BindRows();
            Add(_suggestions);
            _details = new VisualElement();
            _details.name = "ts-tag-inline-selected-details";
            _details.AddToClassList("ts-tag-inline-help-details");
            Add(_details);
        }

        public void Render(TextStudioInlineTagHelpState state)
        {
            if (!state.Visible || string.Equals(_dismissedSource, state.SourceText, StringComparison.Ordinal)) { Hide(); return; }
            EnsureRows();
            style.display = DisplayStyle.Flex;
            bool same = _visibleItems.Count == state.Items.Count && _state.TagNameStart == state.TagNameStart;
            for (int i = 0; same && i < _visibleItems.Count; i++) same = ReferenceEquals(_visibleItems[i].Definition, state.Items[i].Definition) && _visibleItems[i].IsExactMatch == state.Items[i].IsExactMatch;
            bool sourceChanged = !string.Equals(_state.SourceText, state.SourceText, StringComparison.Ordinal);
            _state = state;
            if (!same)
            {
                _visibleItems.Clear();
                foreach (TextStudioInlineTagHelpItem item in state.Items) _visibleItems.Add(item);
                _selectedIndex = 0;
                _suggestions.style.height = Math.Min(VisibleSuggestionRows, _visibleItems.Count) * SuggestionRowHeight;
                _suggestions.scrollOffset = Vector2.zero;
            }
            if (same && !sourceChanged) return;
            BindRows();
            UpdateSuggestionVisibility();
            UpdateDetails();
        }

        // A lone match that cannot change the source is documentation, not a choice, so only its details remain.
        void UpdateSuggestionVisibility()
        {
            _showSuggestions = _visibleItems.Count > 1 ||
                _visibleItems.Count == 1 && TextStudioInlineTagEditModel.CanCompleteTag(_state.SourceText, _state, _visibleItems[0].Definition);
            _suggestions.style.display = _showSuggestions ? DisplayStyle.Flex : DisplayStyle.None;
            EnableInClassList(DetailsOnlyClass, !_showSuggestions);
        }

        void BindRows()
        {
            if (_suggestions == null) return;
            int first = Mathf.Clamp((int)(_suggestions.scrollOffset.y / SuggestionRowHeight), 0, Math.Max(0, _visibleItems.Count - 1));
            _leadingSpace.style.height = first * SuggestionRowHeight;
            for (int i = 0; i < _rowPool.Count; i++)
            {
                int index = first + i;
                SuggestionRow row = _rowPool[i];
                row.style.display = index < _visibleItems.Count ? DisplayStyle.Flex : DisplayStyle.None;
                if (index < _visibleItems.Count)
                {
                    row.Bind(_visibleItems[index], index == _selectedIndex, _state.IsClosingTag);
                    row.Action.SetEnabled(TextStudioInlineTagEditModel.CanCompleteTag(_state.SourceText, _state, _visibleItems[index].Definition));
                }
                else row.Unbind();
            }
            _trailingSpace.style.height = Math.Max(0, _visibleItems.Count - first - _rowPool.Count) * SuggestionRowHeight;
        }

        void UpdateDetails()
        {
            if (_details == null) return;
            _details.Clear();
            if (_visibleItems.Count == 0) return;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, _visibleItems.Count - 1);
            TextStudioInlineTagHelpItem item = _visibleItems[_selectedIndex];
            if (item.IsExactMatch)
            {
                VisualElement header = new VisualElement();
                header.AddToClassList("ts-tag-inline-help-header");
                string usage = _state.IsClosingTag
                    ? "</" + item.Definition.Id + ">"
                    : string.IsNullOrEmpty(item.Example) ? "<" + item.Definition.Id + ">" : item.Example;
                Label syntax = new Label(usage) { enableRichText = false };
                syntax.AddToClassList("ts-tag-inline-help-syntax");
                TextStudioInspectorElements.ApplyCodeFont(syntax);
                header.Add(syntax);
                if (TryResolvePersistentEffectAsset(item, out TextStudioEffectDefinitionAsset effectAsset)) header.Add(BuildEditEffectButton(effectAsset));
                _details.Add(header);
            }
            if (!string.IsNullOrEmpty(item.Definition.Description))
            {
                Label description = new Label(item.Definition.Description) { enableRichText = false };
                description.AddToClassList("ts-tag-inline-help-description");
                _details.Add(description);
            }
            if (item.IsExactMatch && !_state.IsClosingTag) AddParameterDetails(_details, _state, item.Definition);
            if (_convertAppearance != null && TextStudioAppearanceConversion.IsAdHoc(_state.SourceText, _state.TagStart, _contextFactory?.Invoke(_state.SourceText)))
            {
                Button convert = new Button(() => _convertAppearance(_state)) { text = "Convert to Inline Style", tooltip = "Move this ad-hoc appearance into an editable tagged group and preserve its rendered settings in one Undo operation." };
                convert.AddToClassList("ts-tag-inline-help-convert");
                _details.Add(convert);
            }
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (style.display == DisplayStyle.None || _visibleItems.Count == 0) return;
            if (evt.keyCode == KeyCode.Escape)
            {
                _dismissedSource = _sourceField.value;
                Hide();
            }
            else if ((evt.keyCode == KeyCode.UpArrow || evt.keyCode == KeyCode.DownArrow) && _showSuggestions)
            {
                _selectedIndex = (_selectedIndex + (evt.keyCode == KeyCode.DownArrow ? 1 : _visibleItems.Count - 1)) % _visibleItems.Count;
                float rowTop = _selectedIndex * SuggestionRowHeight;
                float offset = _suggestions.scrollOffset.y;
                if (rowTop < offset) offset = rowTop;
                else if (rowTop + SuggestionRowHeight > offset + SuggestionViewportHeight) offset = rowTop + SuggestionRowHeight - SuggestionViewportHeight;
                _suggestions.scrollOffset = new Vector2(0, offset);
                BindRows();
                UpdateDetails();
            }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter || evt.keyCode == KeyCode.Tab && !evt.shiftKey)
            {
                TextStudioTagDefinition definition = _visibleItems[_selectedIndex].Definition;
                if (!TextStudioInlineTagEditModel.CanCompleteTag(_sourceField.value, _state, definition)) return;
                CompleteTag(_state, definition);
            }
            else return;
            evt.StopImmediatePropagation();
        }

        void OnUndoRedo()
        {
            if (_sourceField == null) return;
            TextField field = _sourceField;
            field.schedule.Execute(() =>
            {
                if (!ReferenceEquals(field, _sourceField)) return;
                if (_authoredSource != null) field.SetValueWithoutNotify(_authoredSource() ?? string.Empty);
                if (string.Equals(field.value, _undoBefore, StringComparison.Ordinal))
                {
                    field.Focus();
                    field.cursorIndex = _undoCursor;
                    field.selectIndex = _undoSelection;
                }
                else if (string.Equals(field.value, _undoAfter, StringComparison.Ordinal))
                {
                    field.Focus();
                    field.cursorIndex = _redoCursor;
                    field.selectIndex = _redoSelection;
                }
                _dismissedSource = null;
                Refresh();
                _sourceRestored?.Invoke();
            });
        }

        void OnSourceChanged(ChangeEvent<string> evt)
        {
            _dismissedSource = null;
            RefreshAfterCaretEvent();
        }

        void OnKeyUp(KeyUpEvent evt)
        {
            RefreshIfCaretChanged();
        }

        void OnFocusIn(FocusInEvent evt)
        {
            StartCaretPolling();
        }

        void OnSourceAttachedToPanel(AttachToPanelEvent evt)
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            EnsureCaretPolling();
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            RefreshAfterCaretEvent();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            RefreshAfterCaretEvent();
        }

        void OnMouseUp(MouseUpEvent evt)
        {
            RefreshAfterCaretEvent();
        }

        void OnHelpPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            VisualElement current = evt.target as VisualElement;
            while (current != null && !ReferenceEquals(current, this))
            {
                if (current is Button)
                {
                    _isHelpPointerDown = true;
                    return;
                }

                current = current.parent;
            }
        }

        void OnHelpPointerUp(PointerUpEvent evt)
        {
            if (evt.button != 0 || !_isHelpPointerDown)
                return;

            EndHelpPointerInteraction();
        }

        void OnHelpPointerCanceled(PointerCancelEvent evt)
        {
            EndHelpPointerInteraction();
        }

        void OnHelpPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            EndHelpPointerInteraction();
        }

        void EndHelpPointerInteraction()
        {
            if (!_isHelpPointerDown)
                return;

            _isHelpPointerDown = false;
            ScheduleHideIfFocusLeft();
        }

        void OnFocusOut(FocusOutEvent evt)
        {
            VisualElement next = evt.relatedTarget as VisualElement;
            if (IsDescendantOf(next, _sourceField))
                return;

            if (IsDescendantOf(next, this))
                return;

            ScheduleHideIfFocusLeft();
        }

        void OnHelpFocusOut(FocusOutEvent evt)
        {
            VisualElement next = evt.relatedTarget as VisualElement;
            if (IsDescendantOf(next, this) || IsDescendantOf(next, _sourceField))
                return;

            ScheduleHideIfFocusLeft();
        }

        void OnDetachedFromPanel(DetachFromPanelEvent evt)
        {
            _isHelpPointerDown = false;
            Undo.undoRedoPerformed -= OnUndoRedo;
            StopCaretPolling();
        }

        void ScheduleHideIfFocusLeft()
        {
            schedule.Execute(() =>
            {
                if (_isHelpPointerDown ||
                    ContainsFocusedElement(_sourceField) ||
                    ContainsFocusedElement(this))
                {
                    return;
                }

                Hide();
            }).StartingIn(0);
        }

        bool CanRunScheduledUpdate()
        {
            if (_sourceField == null || _sourceField.panel == null)
                return false;

            return _canRunScheduledUpdate == null || _canRunScheduledUpdate();
        }

        static bool ContainsFocusedElement(VisualElement element)
        {
            Focusable focused = element?.focusController?.focusedElement;
            VisualElement focusedElement = focused as VisualElement;
            return IsDescendantOf(focusedElement, element);
        }

        void CaptureCaretSnapshot(string source, int cursor, int selection)
        {
            _lastSource = source;
            _lastCursor = cursor;
            _lastSelection = selection;
            _metadataGeneration = TextStudioAppearanceAuthoringProviders.Generation;
            _registryRevision = TextStudioAppearanceAuthoringProviders.RegistryRevision;
        }

        void CompleteTag(
            TextStudioInlineTagHelpState state,
            TextStudioTagDefinition definition)
        {
            if (_sourceField == null ||
                !TextStudioInlineTagEditModel.TryCompleteTag(
                    _sourceField.value,
                    state,
                    definition,
                    out TextStudioTagSourceEditor.EditResult edit))
            {
                Refresh(true);
                return;
            }

            ApplyEdit(edit);
        }

        void InsertParameter(
            TextStudioInlineTagHelpState state,
            TextStudioTagDefinition definition,
            TextStudioTagParameterDefinition parameter)
        {
            if (_sourceField == null ||
                !TextStudioInlineTagEditModel.TryInsertParameter(
                    _sourceField.value,
                    state,
                    definition,
                    parameter,
                    out TextStudioTagSourceEditor.EditResult edit))
            {
                Refresh(true);
                return;
            }

            ApplyEdit(edit);
        }

        void ApplyEdit(TextStudioTagSourceEditor.EditResult edit)
        {
            TextField sourceField = _sourceField;
            if (sourceField == null)
                return;

            string currentText = sourceField.value ?? string.Empty;
            _undoBefore = currentText;
            _undoAfter = edit.Text;
            _redoCursor = edit.SelectionEnd;
            _redoSelection = edit.SelectionStart;
            _undoCursor = sourceField.cursorIndex;
            _undoSelection = sourceField.selectIndex;
            if (!string.Equals(currentText, edit.Text, StringComparison.Ordinal))
            {
                if (_applyEdit != null)
                    _applyEdit(edit);
                else
                    sourceField.value = edit.Text;
            }

            if (!ReferenceEquals(_sourceField, sourceField) ||
                !string.Equals(sourceField.value, edit.Text, StringComparison.Ordinal))
            {
                Refresh(true);
                return;
            }

            Refresh(true);
            sourceField.Focus();
            RestoreSelection(sourceField, edit);
            if (sourceField.panel == null)
                return;

            sourceField.schedule.Execute(() =>
            {
                if (!ReferenceEquals(_sourceField, sourceField) ||
                    !string.Equals(sourceField.value, edit.Text, StringComparison.Ordinal))
                {
                    return;
                }

                Refresh(true);
                sourceField.Focus();
                RestoreSelection(sourceField, edit);
            }).StartingIn(0);
        }

        static void RestoreSelection(
            TextField sourceField,
            TextStudioTagSourceEditor.EditResult edit)
        {
            int textLength = (sourceField.value ?? string.Empty).Length;
            sourceField.cursorIndex = Mathf.Clamp(edit.SelectionEnd, 0, textLength);
            sourceField.selectIndex = Mathf.Clamp(edit.SelectionStart, 0, textLength);
        }

        static Button BuildEditEffectButton(TextStudioEffectDefinitionAsset effectAsset)
        {
            Button edit = CommonUITK.CreateIconButton(
                "Edit effect",
                "editicon.sml",
                () => SelectEffectAsset(effectAsset),
                "ts-tag-inline-help-edit-effect",
                null,
                "ts-tag-inline-help-edit-effect-icon");
            edit.name = "ts-tag-inline-help-edit-effect";
            edit.tooltip = effectAsset is TextStudioComposedEffectAsset
                ? "Select this effect asset and open it in the Effect Composer."
                : "Select this effect asset and open it in its Text Studio inspector.";
            if (edit.Q<Image>() == null)
            {
                edit.text = "Edit";
                edit.AddToClassList("ts-tag-inline-help-edit-effect-text");
            }
            return edit;
        }

        static bool TryResolvePersistentEffectAsset(
            TextStudioInlineTagHelpItem item,
            out TextStudioEffectDefinitionAsset effectAsset)
        {
            effectAsset = null;
            if (!item.IsExactMatch ||
                item.Definition == null ||
                item.Definition.Kind != TextStudioTagKind.TextStudioEffect ||
                !TextStudioPhaseTagUtility.TryResolveEffectId(item.Definition.Id, out string effectId) ||
                !TextStudioRegistry.Effects.TryGetQuiet(
                    effectId,
                    out ITextStudioEffectDefinition definition) ||
                definition is not TextStudioEffectDefinitionAsset candidate)
            {
                return false;
            }

            string assetPath = AssetDatabase.GetAssetPath(candidate);
            if (!AssetDatabase.Contains(candidate) || string.IsNullOrWhiteSpace(assetPath))
                return false;

            effectAsset = candidate;
            return true;
        }

        static void SelectEffectAsset(TextStudioEffectDefinitionAsset effectAsset)
        {
            if (effectAsset == null || !AssetDatabase.Contains(effectAsset))
                return;

            Selection.activeObject = effectAsset;
            EditorGUIUtility.PingObject(effectAsset);
        }

        void AddParameterDetails(
            VisualElement parent,
            TextStudioInlineTagHelpState state,
            TextStudioTagDefinition definition)
        {
            IReadOnlyList<TextStudioTagParameterDefinition> parameters = definition.Parameters;
            if (parameters.Count == 0)
            {
                if (definition.ParameterRequirement == TextStudioTagParameterRequirement.Required)
                {
                    Label requiredValue = new Label("A value is required.");
                    requiredValue.AddToClassList("ts-tag-inline-help-parameter-note");
                    parent.Add(requiredValue);
                }
                return;
            }

            VisualElement details = new VisualElement();
            details.AddToClassList("ts-tag-inline-help-parameters");

            bool hasRequiredParameter = false;
            for (int i = 0; i < parameters.Count; i++)
                hasRequiredParameter |= parameters[i].Required;

            Label heading = new Label(hasRequiredParameter ? "Parameters" : "Optional parameters")
            {
                tooltip = "Click a parameter to insert it in the active tag, or to select its value when the tag already sets it. Hover a parameter for details and aliases."
            };
            heading.AddToClassList("ts-tag-inline-help-parameters-title");
            details.Add(heading);

            for (int i = 0; i < parameters.Count; i++)
            {
                TextStudioTagParameterDefinition parameter = parameters[i];
                Button parameterRow = new Button
                {
                    name = "ts-tag-inline-help-parameter-" + parameter.Name,
                    focusable = true
                };
                parameterRow.AddToClassList("ts-tag-inline-help-parameter-row");
                ConfigureInteractiveFocusStyle(parameterRow);
                parameterRow.tooltip = BuildParameterTooltip(parameter);
                parameterRow.clicked += () => InsertParameter(state, definition, parameter);
                parameterRow.SetEnabled(
                    _sourceField != null &&
                    string.Equals(
                        _sourceField.value ?? string.Empty,
                        state.SourceText,
                        StringComparison.Ordinal));

                Label name = new Label(parameter.Name + "=");
                name.enableRichText = false;
                name.AddToClassList("ts-tag-inline-help-parameter-name");
                TextStudioInspectorElements.ApplyCodeFont(name);
                parameterRow.Add(name);

                Label value = new Label(GetParameterValueHint(definition, parameter));
                value.enableRichText = false;
                value.AddToClassList("ts-tag-inline-help-parameter-value");
                parameterRow.Add(value);

                if (parameter.Required)
                {
                    Label required = new Label("required");
                    required.AddToClassList("ts-tag-inline-help-parameter-required");
                    parameterRow.Add(required);
                }

                details.Add(parameterRow);
            }

            parent.Add(details);
        }

        static string GetParameterValueHint(
            TextStudioTagDefinition definition,
            TextStudioTagParameterDefinition parameter)
        {
            string hint;
            if (parameter.AllowedValues.Count > 0)
            {
                hint = string.Join(" | ", parameter.AllowedValues);
            }
            else
            {
                switch (parameter.Kind)
                {
                    case TextStudioTagParameterKind.Float:
                        hint = "number";
                        break;
                    case TextStudioTagParameterKind.Integer:
                        hint = "whole number";
                        break;
                    case TextStudioTagParameterKind.Boolean:
                        hint = "true | false";
                        break;
                    case TextStudioTagParameterKind.Color:
                        hint = "#RRGGBB or #RRGGBBAA";
                        break;
                    case TextStudioTagParameterKind.Pivot:
                        hint = string.Join(" | ", TextStudioPivotParser.NamedValues) + " | x,y";
                        break;
                    case TextStudioTagParameterKind.RouteId:
                        hint = "route id";
                        break;
                    case TextStudioTagParameterKind.AssetReference:
                        hint = "registered asset id";
                        break;
                    case TextStudioTagParameterKind.Enum:
                        hint = "value";
                        break;
                    default:
                        hint = "text";
                        break;
                }
            }

            if (!string.IsNullOrEmpty(parameter.DefaultValue))
                return hint + " (default: " + parameter.DefaultValue + ")";

            string example = ResolveExampleValue(definition, parameter);
            if (string.IsNullOrEmpty(example))
                example = GetGenericExample(parameter.Kind);
            return string.IsNullOrEmpty(example)
                ? hint
                : hint + " (example: " + example + ")";
        }

        static string ResolveExampleValue(
            TextStudioTagDefinition definition,
            TextStudioTagParameterDefinition parameter)
        {
            if (!string.IsNullOrEmpty(parameter.ExampleValue))
                return parameter.ExampleValue;
            if (definition == null)
                return string.Empty;

            for (int i = 0; i < definition.Examples.Count; i++)
            {
                string example = definition.Examples[i];
                int tagStart = example.IndexOf('<');
                int tagEnd = example.IndexOf('>', tagStart + 1);
                if (tagStart < 0 || tagEnd <= tagStart + 1 || example[tagStart + 1] == '/')
                    continue;

                int nameStart = tagStart + 1;
                int nameEnd = nameStart;
                while (nameEnd < tagEnd &&
                       !char.IsWhiteSpace(example[nameEnd]) &&
                       example[nameEnd] != '=')
                {
                    nameEnd++;
                }

                string raw = example.Substring(nameEnd, tagEnd - nameEnd).Trim();
                if (raw.StartsWith("=", StringComparison.Ordinal))
                    raw = raw.Substring(1);
                if (string.IsNullOrEmpty(raw))
                    continue;

                TextStudioTagParameters parsed = TextStudioTagParameterParser.Parse(raw);
                if (parsed.TryGetString(parameter.Name, out string value) &&
                    !string.IsNullOrEmpty(value))
                {
                    return value;
                }

                for (int aliasIndex = 0; aliasIndex < parameter.Aliases.Count; aliasIndex++)
                {
                    if (parsed.TryGetString(parameter.Aliases[aliasIndex], out value) &&
                        !string.IsNullOrEmpty(value))
                    {
                        return value;
                    }
                }

                if (parameter.PositionalIndex >= 0)
                {
                    value = parsed.GetString(parameter.PositionalIndex);
                    if (!string.IsNullOrEmpty(value))
                        return value;
                }
            }

            return string.Empty;
        }

        static string GetGenericExample(TextStudioTagParameterKind kind)
        {
            switch (kind)
            {
                case TextStudioTagParameterKind.Float:
                case TextStudioTagParameterKind.Integer:
                    return "1";
                case TextStudioTagParameterKind.RouteId:
                    return "eventRoute";
                case TextStudioTagParameterKind.AssetReference:
                    return "assetName";
                case TextStudioTagParameterKind.Enum:
                case TextStudioTagParameterKind.String:
                    return "value";
                default:
                    return string.Empty;
            }
        }

        static string BuildParameterTooltip(TextStudioTagParameterDefinition parameter)
        {
            const string Interaction = "Click to insert this parameter in the active tag or select its existing value.";
            string tooltip = string.IsNullOrEmpty(parameter.Description)
                ? Interaction
                : parameter.Description + "\n" + Interaction;
            if (parameter.Aliases.Count == 0)
                return tooltip;

            string aliases = "Also accepts: " + string.Join(", ", parameter.Aliases) + ".";
            return tooltip + "\n" + aliases;
        }

        static void ConfigureInteractiveFocusStyle(Button button)
        {
            button.RegisterCallback<FocusInEvent>(_ =>
                button.AddToClassList("ts-tag-inline-help-action-focused"));
            button.RegisterCallback<FocusOutEvent>(_ =>
                button.RemoveFromClassList("ts-tag-inline-help-action-focused"));
        }



        static bool IsDescendantOf(VisualElement element, VisualElement ancestor)
        {
            while (element != null)
            {
                if (ReferenceEquals(element, ancestor))
                    return true;
                element = element.parent;
            }
            return false;
        }
    }
}
