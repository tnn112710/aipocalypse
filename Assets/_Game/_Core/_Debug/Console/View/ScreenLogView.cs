using System.Text;
using TMPro;
using UnityEngine;

namespace Aipocalypse.Core.Debugging
{
    // Draws the ScreenLog in the top-left corner, newest message on top, each on a dark band so it reads over a bright sky.
    // Bound by ConsoleView; redraws only when the log changed.
    public sealed class ScreenLogView : MonoBehaviour
    {
        [SerializeField] TMP_Text _text;

        readonly StringBuilder _builder = new(1024);
        ScreenLog _log;
        ConsoleStyle _style;
        int _shownVersion = -1;

        public void Bind(ScreenLog log, ConsoleStyle style)
        {
            _log = log;
            _style = style;
            _shownVersion = -1;
            if (style == null)
                return;
            if (style.Font != null)
                _text.font = style.Font;
            _text.fontSize = style.ScreenFontSize;
        }

        void LateUpdate()
        {
            if (_log == null || _style == null || _log.Version == _shownVersion)
                return;
            _shownVersion = _log.Version;
            _builder.Clear();
            for (int i = _log.Count - 1; i >= 0; i--)
            {
                ScreenMessage message = _log[i];
                if (_builder.Length > 0)
                    _builder.Append('\n');
                _builder.AppendMark(_style.ScreenBacking).AppendColor(message.Color ?? _style.ColorOf(message.Kind))
                    .Append(' ').AppendLiteral(message.Text).Append(" </color></mark>");
            }
            _text.SetText(_builder);
            _text.enabled = _log.Count > 0;
        }
    }
}
