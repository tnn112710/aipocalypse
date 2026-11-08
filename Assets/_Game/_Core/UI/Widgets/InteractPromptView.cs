using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // "[E] Pick up Rifle": a key cap and a line of text, plus a smaller second line for a hold use ("Hold: Repair Gate 63%").
    // Knows nothing about interaction or input.
    public sealed class InteractPromptView : MonoBehaviour
    {
        [SerializeField, InlineEditor] InteractPromptStyle _style;
        [SerializeField, Tooltip("Shown and hidden as a whole")] GameObject _content;
        [SerializeField] Image _keyCap;
        [SerializeField] TMP_Text _keyLabel;
        [SerializeField] TMP_Text _text;

        string _verb;
        string _target;
        string _holdVerb;
        string _holdTarget;

        public InteractPromptStyle Style => _style;

        void Awake() => ApplyStyle();

        public void SetKey(string key) => _keyLabel.SetText(key);

        // Rebuilds the text only when the prompt changes, so it can be called every frame.
        public void Show(string verb, string target, string holdVerb = null, string holdTarget = null)
        {
            _content.SetActive(true);
            if (ReferenceEquals(verb, _verb) && ReferenceEquals(target, _target) && ReferenceEquals(holdVerb, _holdVerb)
                && ReferenceEquals(holdTarget, _holdTarget))
                return;

            _verb = verb;
            _target = target;
            _holdVerb = holdVerb;
            _holdTarget = holdTarget;
            RefreshText();
        }

        public void Hide() => _content.SetActive(false);

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null)
                return;

            _keyCap.sprite = _style.KeySprite;
            _keyCap.type = StyleUtils.ImageTypeFor(_style.KeySprite);
            _keyCap.color = _style.KeyColor;
            if (_style.KeyFont != null)
                _keyLabel.font = _style.KeyFont;
            _keyLabel.color = _style.KeyLabelColor;
            _keyLabel.fontSize = _style.KeyFontSize;
            _keyLabel.fontStyle = _style.KeyBold ? FontStyles.Bold : FontStyles.Normal;

            if (_style.Font != null)
                _text.font = _style.Font;
            _text.color = _style.VerbColor;
            _text.fontSize = _style.FontSize;
            RefreshText();
        }

        void RefreshText()
        {
            if (_style == null || (_verb == null && _holdVerb == null))
                return;
            string target = ColorUtility.ToHtmlStringRGBA(_style.TargetColor);
            string line = _verb != null ? $"{_verb} <color=#{target}>{_target}</color>" : string.Empty;
            if (_holdVerb != null)
            {
                string separator = line.Length > 0 ? "\n" : string.Empty;
                int size = Mathf.RoundToInt(_style.HoldLineScale * 100f);
                line += $"{separator}<size={size}%>{_style.HoldPrefix}{_holdVerb} <color=#{target}>{_holdTarget}</color></size>";
            }
            _text.text = line;
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
