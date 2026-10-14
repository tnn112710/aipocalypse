using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // "[E] Pick up Rifle": a key cap and a line of text. Knows nothing about interaction or input.
    public sealed class InteractPromptView : MonoBehaviour
    {
        [SerializeField, InlineEditor] InteractPromptStyle _style;
        [SerializeField, Tooltip("Shown and hidden as a whole")] GameObject _content;
        [SerializeField] Image _keyCap;
        [SerializeField] TMP_Text _keyLabel;
        [SerializeField] TMP_Text _text;

        string _verb;
        string _target;

        public InteractPromptStyle Style => _style;

        void Awake() => ApplyStyle();

        public void SetKey(string key) => _keyLabel.SetText(key);

        // Rebuilds the text only when the prompt changes, so it can be called every frame.
        public void Show(string verb, string target)
        {
            _content.SetActive(true);
            if (ReferenceEquals(verb, _verb) && ReferenceEquals(target, _target))
                return;

            _verb = verb;
            _target = target;
            RefreshText();
        }

        public void Hide() => _content.SetActive(false);

        [Button, PropertyOrder(-1)]
        public void ApplyStyle()
        {
            if (_style == null)
                return;

            _keyCap.sprite = _style.KeySprite;
            _keyCap.type = _style.KeySprite != null && _style.KeySprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            _keyCap.color = _style.KeyColor;
            if (_style.KeyFont != null)
                _keyLabel.font = _style.KeyFont;
            _keyLabel.color = _style.KeyLabelColor;
            _keyLabel.fontSize = _style.KeyFontSize;

            if (_style.Font != null)
                _text.font = _style.Font;
            _text.color = _style.VerbColor;
            _text.fontSize = _style.FontSize;
            RefreshText();
        }

        void RefreshText()
        {
            if (_style == null || _verb == null)
                return;
            _text.text = $"{_verb} <color=#{ColorUtility.ToHtmlStringRGBA(_style.TargetColor)}>{_target}</color>";
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
