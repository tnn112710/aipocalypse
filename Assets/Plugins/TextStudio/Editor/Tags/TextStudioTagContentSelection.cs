using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal sealed class TextStudioTagContentSelection : IDisposable
    {
        const EventModifiers SelectionModifiers = EventModifiers.Shift | EventModifiers.Control | EventModifiers.Alt | EventModifiers.Command;
        readonly TextField _field;
        readonly VisualElement _pointerTarget;
        readonly Func<TextStudioMarkupSyntax> _syntax;
        readonly TextStudioTagContentSelectionModel _model = new TextStudioTagContentSelectionModel();
        string _sourceAtPointerDown;
        int _anchor;
        Vector3 _pointerDownPosition;
        bool _dragged;

        public TextStudioTagContentSelection(TextField field, Func<TextStudioMarkupSyntax> syntax)
        {
            _field = field;
            _pointerTarget = field.Q<TextElement>(className: TextElement.selectableUssClassName);
            _syntax = syntax;
            _field.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            // Captured drag events can bypass ancestors. Observe them on the native input.
            _pointerTarget?.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            _pointerTarget?.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            _field.RegisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
        }

        public void Dispose()
        {
            _field.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _pointerTarget?.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            _pointerTarget?.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            _field.UnregisterCallback<ClickEvent>(OnClick, TrickleDown.TrickleDown);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            _sourceAtPointerDown = null;
            _dragged = false;
            _pointerDownPosition = evt.position;
            if (evt.button != 0 || (evt.modifiers & SelectionModifiers) != 0 ||
                !(evt.target is TextElement text) || !text.ClassListContains(TextElement.selectableUssClassName))
                return;

            // The third native pointer-down expands the word to a paragraph. Keep an
            // index inside the preceding word, before Unity changes that selection.
            _sourceAtPointerDown = _field.value;
            int start = Math.Min(_field.cursorIndex, _field.selectIndex);
            int end = Math.Max(_field.cursorIndex, _field.selectIndex);
            _anchor = start + (end - start) / 2;
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (_sourceAtPointerDown != null && (evt.position - _pointerDownPosition).sqrMagnitude >= 16f)
                _dragged = true;
        }

        void OnPointerCancel(PointerCancelEvent evt)
        {
            _sourceAtPointerDown = null;
        }

        void OnClick(ClickEvent evt)
        {
            if (evt.button != 0 || evt.clickCount != 3 || (evt.modifiers & SelectionModifiers) != 0 ||
                _sourceAtPointerDown == null || _dragged ||
                (evt.position - _pointerDownPosition).sqrMagnitude >= 16f ||
                !string.Equals(_sourceAtPointerDown, _field.value, StringComparison.Ordinal))
                return;

            if (_model.TryGetRange(_field.value, _anchor, _syntax?.Invoke(), out int start, out int end))
                _field.SelectRange(end, start);
        }
    }

    internal sealed class TextStudioTagContentSelectionModel
    {
        readonly TextStudioSourceHighlightBuilder _spans = new TextStudioSourceHighlightBuilder();
        readonly List<OpenTag> _openTags = new List<OpenTag>();

        readonly struct OpenTag
        {
            public readonly string Name;
            public readonly char Delimiter;
            public readonly int ContentStart;

            public OpenTag(string name, char delimiter, int contentStart)
            {
                Name = name;
                Delimiter = delimiter;
                ContentStart = contentStart;
            }
        }

        public bool TryGetRange(string source, int index, TextStudioMarkupSyntax syntax, out int start, out int end)
        {
            start = end = 0;
            if (string.IsNullOrEmpty(source) || index < 0 || index >= source.Length)
                return false;

            _openTags.Clear();
            _spans.Analyze(source, syntax);
            TextStudioTagCatalog catalog = TextStudioTagCatalog.BuildDefault();
            bool found = false;
            foreach (TextStudioSourceTagSpan span in _spans.TagSpans)
            {
                char delimiter = source[span.Start];
                char closing = syntax != null && syntax.Profile == TextStudioAuthoringProfile.Custom &&
                               delimiter == syntax.OpeningDelimiter[0]
                    ? syntax.ClosingDelimiter[0]
                    : delimiter == '<' ? '>' : '}';
                string body = source.Substring(span.Start + 1, Math.Max(0, span.Length - 2)).Trim();
                bool isClosing = body.StartsWith("/", StringComparison.Ordinal);
                if (isClosing)
                    body = body.Substring(1).TrimStart();
                int nameEnd = 0;
                while (nameEnd < body.Length && !char.IsWhiteSpace(body[nameEnd]) && body[nameEnd] != '=' && body[nameEnd] != '/')
                    nameEnd++;
                string name = body.Substring(0, nameEnd);

                bool inNoParse = _openTags.Count > 0 &&
                    string.Equals(_openTags[_openTags.Count - 1].Name, "noparse", StringComparison.OrdinalIgnoreCase);
                if (inNoParse && !(isClosing && string.Equals(name, "noparse", StringComparison.OrdinalIgnoreCase)))
                    continue;

                if (index >= span.Start && index < span.End)
                    return false;
                if (source[span.End - 1] != closing || (name.Length == 0 && !isClosing))
                {
                    _openTags.Clear();
                    continue;
                }

                if (isClosing)
                {
                    if (_openTags.Count == 0)
                        continue;
                    OpenTag open = _openTags[_openTags.Count - 1];
                    if (open.Delimiter != delimiter ||
                        (name.Length > 0 && !string.Equals(open.Name, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        _openTags.Clear();
                        continue;
                    }
                    _openTags.RemoveAt(_openTags.Count - 1);
                    if (index >= open.ContentStart && index < span.Start && (!found || open.ContentStart > start))
                    {
                        start = open.ContentStart;
                        end = span.Start;
                        found = true;
                    }
                    continue;
                }

                bool hasParameter = nameEnd < body.Length && body.Substring(nameEnd).Trim().TrimEnd('/').Length > 0;
                if (body.EndsWith("/", StringComparison.Ordinal) ||
                    BuiltInStyles.IsSelfContainedNativeRichTextFormatting(name, hasParameter) ||
                    (catalog.TryGetDefinition(name, hasParameter, out TextStudioTagDefinition definition) &&
                     definition.Shape != TextStudioTagShape.Range))
                    continue;

                _openTags.Add(new OpenTag(name, delimiter, span.End));
            }

            // Complete opening tags may leave their range open through the end of the source.
            for (int i = _openTags.Count - 1; i >= 0; i--)
            {
                OpenTag open = _openTags[i];
                if (index >= open.ContentStart && (!found || open.ContentStart > start))
                {
                    start = open.ContentStart;
                    end = source.Length;
                    found = true;
                    break;
                }
            }
            return found;
        }
    }
}
