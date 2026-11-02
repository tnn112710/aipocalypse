using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Aipocalypse.Core.Bootstrap;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using VContainer;

namespace Aipocalypse.Core.Debugging
{
    // Root of DebugConsole.prefab, created by ConsoleInstaller and kept across scenes. Draws ConsoleService while it's open:
    // the scrollback (bottom-aligned, scrolled in text lines), the input line with a blinking caret, and the
    // suggestions under the panel. Hands the ScreenLog to its view. Redraws only what changed.
    public sealed class ConsoleView : MonoBehaviour
    {
        const string Prompt = "> ";
        const float CaretWidth = 2f;

        [SerializeField, InlineEditor] ConsoleStyle _style;
        [SerializeField, Tooltip("Shown while the console is open")] RectTransform _panel;
        [SerializeField] Image _background;
        [SerializeField, Tooltip("Bottom-aligned; its parent masks the lines that don't fit")] TMP_Text _log;
        [SerializeField] Image _inputBackground;
        [SerializeField] TMP_Text _input;
        [SerializeField, Tooltip("Thin bar, child of the input text, placed at the caret's character")] Image _caret;
        [SerializeField, Tooltip("Sized to its text; hidden when there's nothing to suggest")] Image _suggestionsBackground;
        [SerializeField] TMP_Text _suggestions;
        [SerializeField] ScreenLogView _screenLog;
        [SerializeField, Tooltip("Bottom-right corner, always shown in builds: which build this is (BuildStamp)")] TMP_Text _buildLabel;

        readonly StringBuilder _text = new(2048);
        ConsoleService _console;
        string _monospace = string.Empty;
        int _shownVersion = -1;
        int _shownLogVersion = -1;
        bool _shownCaret;
        float _blinkFrom;

        public ConsoleStyle Style => _style;

        [Inject]
        public void Construct(ConsoleService console, ScreenLog screenLog, BuildStamp stamp)
        {
            _console = console;
            _screenLog.Bind(screenLog, _style);
            if (_buildLabel == null)
                return;
            // Not in the editor: it would sit in every screenshot.
            _buildLabel.enabled = !Application.isEditor;
            _buildLabel.SetText(stamp.ToString());
        }

        void Awake()
        {
            ApplyStyle();
            _panel.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (_console == null)
                return;
            bool open = _console.IsOpen;
            if (_panel.gameObject.activeSelf != open)
            {
                _panel.gameObject.SetActive(open);
                // Everything on screen is in the scrollback too.
                _screenLog.gameObject.SetActive(!open);
                _shownVersion = _shownLogVersion = -1;
            }
            if (!open)
                return;

            float now = Time.unscaledTime;
            bool changed = _console.Version != _shownVersion;
            if (changed)
                _blinkFrom = now;
            bool caret = (now - _blinkFrom) % (_style.CaretBlink * 2f) < _style.CaretBlink;
            if (changed || _console.Logs.Version != _shownLogVersion)
                DrawLog();
            if (changed)
            {
                DrawInput();
                DrawSuggestions();
            }
            if (changed || caret != _shownCaret)
                _caret.enabled = caret;
            _shownVersion = _console.Version;
            _shownLogVersion = _console.Logs.Version;
            _shownCaret = caret;
        }

        void DrawLog()
        {
            LogBuffer logs = _console.Logs;
            int room = Mathf.Max(1, Mathf.FloorToInt(_log.rectTransform.rect.height / LineHeight(_log)));
            int total = 0;
            for (int i = 0; i < logs.Count; i++)
                total += LineCount(logs[i].Text);
            _console.PageLines = room;
            // Scrolled up, the last line says how much is below, so one more line is needed to reach the top.
            _console.ScrollLimit = total > room ? total - room + 1 : 0;

            int offset = Mathf.Min(_console.ScrollOffset, _console.ScrollLimit);
            if (offset > 0)
                room--;
            int last = total - offset;
            int first = Mathf.Max(0, last - room);

            _text.Clear().Append(_monospace);
            int line = 0;
            bool any = false;
            for (int i = 0; i < logs.Count && line < last; i++)
            {
                ConsoleEntry entry = logs[i];
                int count = LineCount(entry.Text);
                if (line + count > first)
                {
                    if (any)
                        _text.Append('\n');
                    AppendEntry(entry, first - line, last - line);
                    any = true;
                }
                line += count;
            }
            if (offset > 0)
                _text.Append('\n').AppendColor(_style.Dim).Append("-- ").Append(offset).Append(" more lines below (PageDown) --</color>");
            _log.SetText(_text);
        }

        // Lines [from, to) of one entry; the prompt goes on its first line, the repeat count on its last.
        void AppendEntry(ConsoleEntry entry, int from, int to)
        {
            string text = entry.Text;
            _text.AppendColor(_style.ColorOf(entry.Kind));
            if (from <= 0 && entry.Kind == ConsoleEntryKind.Input)
                _text.Append("> ");
            int line = 0;
            int start = 0;
            bool any = false;
            while (start <= text.Length)
            {
                int end = text.IndexOf('\n', start);
                if (end < 0)
                    end = text.Length;
                if (line >= from && line < to)
                {
                    if (any)
                        _text.Append('\n');
                    _text.AppendLiteral(text, start, end - start);
                    any = true;
                }
                line++;
                start = end + 1;
            }
            _text.Append("</color>");
            if (entry.Count > 1 && to >= line)
                _text.AppendColor(_style.Dim).Append("  x").Append(entry.Count).Append("</color>");
        }

        void DrawInput()
        {
            _text.Clear().Append(_monospace).AppendColor(_style.Dim).Append(Prompt).Append("</color>")
                .AppendColor(_style.Input).AppendLiteral(_console.Line.Text).Append("</color>");
            _input.SetText(_text);
            PlaceCaret();
        }

        // At the left edge of the character after the caret, from the laid-out text (spaces included, tags not).
        void PlaceCaret()
        {
            _input.ForceMeshUpdate();
            TMP_TextInfo info = _input.textInfo;
            if (info.characterCount == 0)
                return;
            int index = Prompt.Length + _console.Line.Caret;
            TMP_CharacterInfo character = info.characterInfo[Mathf.Min(index, info.characterCount - 1)];
            float x = index < info.characterCount ? character.origin : character.xAdvance;
            TMP_LineInfo line = info.lineInfo[0];
            RectTransform caret = _caret.rectTransform;
            caret.localPosition = new Vector3(x, (line.ascender + line.descender) * 0.5f, 0f);
            caret.sizeDelta = new Vector2(CaretWidth, line.ascender - line.descender);
        }

        void DrawSuggestions()
        {
            IReadOnlyList<string> list = _console.Suggestions;
            string hint = _console.Hint;
            bool show = list.Count > 0 || hint != null;
            if (_suggestionsBackground.gameObject.activeSelf != show)
                _suggestionsBackground.gameObject.SetActive(show);
            if (!show)
                return;

            _text.Clear().Append(_monospace);
            if (hint != null)
                _text.AppendColor(_style.Dim).AppendLiteral(hint).Append("</color>");
            int count = Mathf.Min(list.Count, _style.MaxSuggestions);
            int selected = _console.SelectedSuggestion;
            // Keep the selected one in view while Tab cycles past the shown ones.
            int first = selected >= count ? selected - count + 1 : 0;
            for (int i = first; i < first + count; i++)
            {
                if (_text.Length > _monospace.Length)
                    _text.Append('\n');
                bool isSelected = i == selected;
                _text.AppendColor(isSelected ? _style.SelectedSuggestion : _style.Suggestion).Append(isSelected ? "> " : "  ")
                    .AppendLiteral(list[i]).Append("</color>");
            }
            if (list.Count > count)
                _text.Append('\n').AppendColor(_style.Dim).Append("  +").Append(list.Count - count).Append(" more (Tab)</color>");
            _suggestions.SetText(_text);
        }

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null)
                return;
            _monospace = _style.Monospace > 0f ? "<mspace=" + _style.Monospace.ToString("0.###", CultureInfo.InvariantCulture) + "em>" : string.Empty;
            foreach (TMP_Text text in new[] { _log, _input, _suggestions })
            {
                if (_style.Font != null)
                    text.font = _style.Font;
                text.fontSize = _style.FontSize;
            }
            _background.color = _style.Background;
            _inputBackground.color = _style.InputBackground;
            _caret.color = _style.Caret;
            _suggestionsBackground.color = _style.SuggestionBackground;
            if (_buildLabel != null)
            {
                if (_style.Font != null)
                    _buildLabel.font = _style.Font;
                _buildLabel.color = _style.Dim;
            }
            _shownVersion = -1;
        }

        static float LineHeight(TMP_Text text)
        {
            FaceInfo face = text.font != null ? text.font.faceInfo : default;
            float ratio = face.pointSize > 0f ? face.lineHeight / face.pointSize : 1.2f;
            return Mathf.Max(1f, text.fontSize * ratio);
        }

        static int LineCount(string text)
        {
            int count = 1;
            for (int i = 0; i < text.Length; i++)
                if (text[i] == '\n')
                    count++;
            return count;
        }
    }
}
