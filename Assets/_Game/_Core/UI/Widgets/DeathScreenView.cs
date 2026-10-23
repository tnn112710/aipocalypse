using DG.Tweening;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // Eyelids closing over the view, then the full-screen death menu (title, a slot for an illustration, a status line in the
    // footer). Knows nothing about death: the presenter says what to show.
    public sealed class DeathScreenView : MonoBehaviour
    {
        [SerializeField, InlineEditor] DeathScreenStyle _style;
        [Header("Eyes")]
        [SerializeField] RectTransform _eyes;
        [SerializeField] Image _darkness;
        [SerializeField] RawImage _upperLid;
        [SerializeField] RawImage _lowerLid;
        [Header("Menu")]
        [SerializeField] CanvasGroup _menu;
        [SerializeField] Image _background;
        [SerializeField] RawImage _glow;
        [SerializeField] TMP_Text _title;
        [SerializeField, Tooltip("Empty for now: where an image or animation goes")] RectTransform _illustration;
        [SerializeField] Image _footer;
        [SerializeField] Image _footerLine;
        [SerializeField] TMP_Text _status;

        const float EnterShare = 0.1f;

        float _closed;
        float _dark;
        Sequence _eyesTween;
        Tween _menuTween;
        Tween _pulse;

        public DeathScreenStyle Style => _style;
        public RectTransform Illustration => _illustration;

        void Awake()
        {
            ApplyStyle();
            HideAll();
        }

        void OnDestroy() => KillTweens();

        // Plays the style's eye steps from open; the tween completes with the lids shut and the view black.
        public Tween CloseEyes()
        {
            _eyesTween?.Kill();
            _eyes.gameObject.SetActive(true);
            SetEyes(0f, 0f);
            _eyesTween = DOTween.Sequence().SetLink(gameObject);
            foreach (EyeStep step in _style.EyeSteps)
            {
                _eyesTween.Append(DOTween.To(() => _closed, v => SetEyes(v, _dark), step.Closed, step.Duration).SetEase(step.Ease));
                _eyesTween.Join(DOTween.To(() => _dark, v => SetEyes(_closed, v), step.Darkness, step.Duration).SetEase(step.Ease));
            }
            return _eyesTween;
        }

        public Tween ShowMenu()
        {
            _menuTween?.Kill();
            _menu.gameObject.SetActive(true);
            _menu.alpha = 0f;
            _menuTween = _menu.DOFade(1f, _style.MenuFadeIn).SetLink(gameObject);
            return _menuTween;
        }

        // Fades the menu out over whatever is behind it; the eyes are put away at once.
        public Tween HideMenu()
        {
            _eyesTween?.Kill();
            _eyes.gameObject.SetActive(false);
            _menuTween?.Kill();
            _menuTween = _menu.DOFade(0f, _style.MenuFadeOut).SetLink(gameObject).OnComplete(() => _menu.gameObject.SetActive(false));
            return _menuTween;
        }

        // `pulse` breathes the line's alpha (an invitation to click).
        public void SetStatus(string text, bool pulse)
        {
            _status.SetText(text);
            _pulse?.Kill();
            _status.alpha = 1f;
            if (pulse)
                _pulse = _status.DOFade(_style.ContinuePulseAlpha, _style.ContinuePulsePeriod * 0.5f).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo).SetLink(gameObject);
        }

        public void HideAll()
        {
            KillTweens();
            SetEyes(0f, 0f);
            _eyes.gameObject.SetActive(false);
            _menu.alpha = 0f;
            _menu.gameObject.SetActive(false);
        }

        [Button]
        public void ApplyStyle()
        {
            if (_style == null)
                return;
            _upperLid.texture = _style.EyelidTexture;
            _lowerLid.texture = _style.EyelidTexture;
            // The lower lid is the upper one upside down.
            _lowerLid.uvRect = new Rect(0f, 1f, 1f, -1f);
            _darkness.color = Color.black;
            _background.color = _style.BackgroundColor;
            _glow.texture = _style.GlowTexture;
            _glow.color = _style.GlowColor;
            _glow.enabled = _style.GlowTexture != null;
            _footer.color = _style.FooterColor;
            _footerLine.color = _style.FooterLineColor;

            if (_style.TitleFont != null)
                _title.font = _style.TitleFont;
            if (_style.TitleMaterial != null)
                _title.fontSharedMaterial = _style.TitleMaterial;
            _title.fontSize = _style.TitleSize;
            _title.color = _style.TitleColor;
            _title.SetText(_style.Title);

            if (_style.StatusFont != null)
                _status.font = _style.StatusFont;
            _status.fontSize = _style.StatusSize;
            _status.color = _style.StatusColor;
        }

        // Lids: each one is as tall as the screen and anchored at its middle. `closed` is the share of the view covered in the
        // middle (where the arched lid turns solid): the first EnterShare brings the edge in from off-screen until the solid part
        // touches the screen's edge, then it covers linearly, ending past the middle so a shut pair overlaps everywhere.
        void SetEyes(float closed, float darkness)
        {
            _closed = closed;
            _dark = darkness;
            float height = _eyes.rect.height;
            float solidAt = _style != null ? _style.EyelidSolidAt : 0f;
            float open = height * 0.5f;
            float touch = open - height * solidAt;
            float edge = Mathf.Lerp(open, touch, closed / EnterShare) - closed * (height * 0.5f + 2f);
            Place(_upperLid.rectTransform, height, edge, upper: true);
            Place(_lowerLid.rectTransform, height, edge, upper: false);
            Color color = _darkness.color;
            color.a = darkness;
            _darkness.color = color;
        }

        static void Place(RectTransform lid, float height, float edge, bool upper)
        {
            lid.anchorMin = new Vector2(0f, 0.5f);
            lid.anchorMax = new Vector2(1f, 0.5f);
            lid.pivot = new Vector2(0.5f, upper ? 0f : 1f);
            lid.sizeDelta = new Vector2(0f, height);
            lid.anchoredPosition = new Vector2(0f, upper ? edge : -edge);
        }

        void KillTweens()
        {
            _eyesTween?.Kill();
            _menuTween?.Kill();
            _pulse?.Kill();
        }
    }
}
