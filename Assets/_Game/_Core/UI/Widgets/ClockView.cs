using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // Digital clock as in Escape from Duckov: a big "HH:MM" with two small lines beside it (phase, day). Knows nothing about time.
    public sealed class ClockView : MonoBehaviour
    {
        [SerializeField, InlineEditor] ClockStyle _style;
        [SerializeField, Tooltip("Shown and hidden as a whole")] GameObject _content;
        [SerializeField] Image _backing;
        [SerializeField] TMP_Text _time;
        [SerializeField] TMP_Text _phase;
        [SerializeField] TMP_Text _day;

        // Monospaced, so "11:11" and "08:08" are the same width and the labels beside them don't jitter every minute.
        const string Monospace = "<mspace=0.5em>";
        char[] _digits;
        int _shownMinute = -1;
        int _shownDay = -1;
        string _shownPhase;

        public ClockStyle Style => _style;

        void Awake() => ApplyStyle();

        public void Show(bool visible) => _content.SetActive(visible);

        // Each setter rebuilds its text only when the value changes, so all of them can be called every frame.
        public void SetTime(int hour, int minute)
        {
            int key = hour * 60 + minute;
            if (key == _shownMinute)
                return;
            _shownMinute = key;
            _digits ??= (Monospace + "00:00").ToCharArray();
            int at = Monospace.Length;
            _digits[at] = (char)('0' + hour / 10 % 10);
            _digits[at + 1] = (char)('0' + hour % 10);
            _digits[at + 3] = (char)('0' + minute / 10);
            _digits[at + 4] = (char)('0' + minute % 10);
            _time.SetCharArray(_digits, 0, _digits.Length);
        }

        public void SetPhase(string label, Color color)
        {
            if (ReferenceEquals(label, _shownPhase) && _phase.color == color)
                return;
            _shownPhase = label;
            _phase.SetText(label);
            _phase.color = color;
        }

        public void SetDay(int day)
        {
            if (day == _shownDay || _style == null)
                return;
            _shownDay = day;
            _day.SetText(_style.DayFormat, day);
        }

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null)
                return;

            if (_style.TimeFont != null)
                _time.font = _style.TimeFont;
            if (_style.TimeMaterial != null)
                _time.fontSharedMaterial = _style.TimeMaterial;
            _time.fontSize = _style.TimeFontSize;
            _time.color = _style.TimeColor;

            TMP_FontAsset labelFont = _style.LabelFont != null ? _style.LabelFont : _style.TimeFont;
            foreach (TMP_Text label in new[] { _phase, _day })
            {
                if (labelFont != null)
                    label.font = labelFont;
                if (_style.LabelMaterial != null)
                    label.fontSharedMaterial = _style.LabelMaterial;
                label.fontSize = _style.LabelFontSize;
            }
            _day.color = _style.DayColor;

            if (_backing != null)
            {
                _backing.sprite = _style.BackingSprite;
                _backing.type = StyleUtils.ImageTypeFor(_style.BackingSprite);
                _backing.color = _style.BackingColor;
                _backing.enabled = _style.BackingSprite != null || _style.BackingColor.a > 0f;
            }

            if (_shownDay >= 0)
                _day.SetText(_style.DayFormat, _shownDay);
        }

#if UNITY_EDITOR
        void OnValidate() => UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null)
                ApplyStyle();
        };
#endif
    }
}
