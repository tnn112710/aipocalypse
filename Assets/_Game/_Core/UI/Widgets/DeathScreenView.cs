using DG.Tweening;
using MB13.Aipocalypse.Core.LocalizationSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // Eyelids closing over the view, then the full-screen death menu: a sunset scene (sky gradient, sun, clouds, mountains, trees,
    // the dog dragging the player home), title, subtitle, a status line and a progress bar. Knows nothing about death: the
    // presenter says what to show.
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
        [SerializeField] RawImage _sky;
        [SerializeField] Image _sun;
        [SerializeField] Image[] _clouds;
        [SerializeField] Image[] _mountains;
        [SerializeField] Image[] _trees;
        [SerializeField] Image _ground;
        [SerializeField] Image _dog;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _subtitle;
        [SerializeField] TMP_Text _status;
        [SerializeField] Image _barFrame;
        [SerializeField] Image _barTrack;
        [SerializeField, Tooltip("Clips the fill: its right anchor is the progress")] RectTransform _barClip;
        [SerializeField] Image _barFill;

        const float EnterShare = 0.1f;

        float _closed;
        float _dark;
        float _progress;
        Texture2D _skyTexture;
        Vector2[] _cloudHome;
        Sequence _eyesTween;
        Tween _menuTween;
        Tween _pulse;
        Tween _barTween;
        Tween[] _drift;

        public DeathScreenStyle Style => _style;

        void Awake()
        {
            CacheCloudHome();
            ApplyStyle();
            HideAll();
        }

        void OnDestroy()
        {
            KillTweens();
            if (_skyTexture != null)
                Destroy(_skyTexture);
        }

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
            _title.SetText(_style.Title.Text());
            _subtitle.SetText(_style.Subtitle.Text());
            SetProgress(0f);
            _menuTween?.Kill();
            _menu.gameObject.SetActive(true);
            _menu.alpha = 0f;
            _menuTween = _menu.DOFade(1f, _style.MenuFadeIn).SetLink(gameObject);
            StartDrift();
            return _menuTween;
        }

        // Fades the menu out over whatever is behind it; the eyes are put away at once.
        public Tween HideMenu()
        {
            _eyesTween?.Kill();
            _eyes.gameObject.SetActive(false);
            _menuTween?.Kill();
            _menuTween = _menu.DOFade(0f, _style.MenuFadeOut).SetLink(gameObject).OnComplete(() =>
            {
                StopDrift();
                _menu.gameObject.SetActive(false);
            });
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

        // Fills the bar to `to` (0..1) over `duration`; a new call replaces the one running.
        public Tween FillBar(float to, float duration, Ease ease)
        {
            _barTween?.Kill();
            _barTween = DOTween.To(() => _progress, SetProgress, Mathf.Clamp01(to), duration).SetEase(ease).SetLink(gameObject);
            return _barTween;
        }

        public void SetProgress(float value)
        {
            _progress = Mathf.Clamp01(value);
            _barClip.anchorMax = new Vector2(_progress, 1f);
        }

        public void HideAll()
        {
            KillTweens();
            StopDrift();
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

            BuildSky();
            _sun.color = _style.SunTint;
            Tint(_clouds, _style.CloudTint);
            Tint(_mountains, _style.MountainTint);
            Tint(_trees, _style.TreeTint);
            _ground.color = _style.GroundColor;
            _dog.color = _style.DogTint;
            _barFrame.color = _style.BarFrameTint;
            _barTrack.color = _style.BarTrackColor;
            _barFill.color = _style.BarFillTint;

            if (_style.TitleFont != null)
                _title.font = _style.TitleFont;
            if (_style.TitleMaterial != null)
                _title.fontSharedMaterial = _style.TitleMaterial;
            Fit(_title, _style.TitleSize, _style.TitleColor);
            _title.fontStyle |= FontStyles.UpperCase;
            _title.SetText(_style.Title.Text());

            if (_style.TitleFont != null)
                _subtitle.font = _style.TitleFont;
            Fit(_subtitle, _style.SubtitleSize, _style.SubtitleColor);
            _subtitle.SetText(_style.Subtitle.Text());

            if (_style.StatusFont != null)
                _status.font = _style.StatusFont;
            Fit(_status, _style.StatusSize, _style.StatusColor);
        }

        // Long translations shrink to the text's box instead of overflowing it.
        static void Fit(TMP_Text text, float size, Color color)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.45f;
            text.fontSize = size;
            text.color = color;
        }

        static void Tint(Image[] images, Color color)
        {
            foreach (Image image in images)
                image.color = color;
        }

        // A 1 x 64 strip of the style's gradient (top to horizon), stretched over the screen. Built at runtime: the prefab keeps a
        // flat colour so no generated texture gets serialized.
        void BuildSky()
        {
            if (!Application.isPlaying)
            {
                _sky.texture = null;
                _sky.color = _style.Sky.Evaluate(0.5f);
                return;
            }
            const int height = 64;
            if (_skyTexture == null)
            {
                _skyTexture = new Texture2D(1, height, TextureFormat.RGBA32, false)
                {
                    name = "DeathScreenSky",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }
            for (int y = 0; y < height; y++)
                _skyTexture.SetPixel(0, y, _style.Sky.Evaluate(1f - y / (height - 1f)));
            _skyTexture.Apply(false);
            _sky.texture = _skyTexture;
            _sky.color = Color.white;
        }

        void CacheCloudHome()
        {
            if (_cloudHome != null)
                return;
            _cloudHome = new Vector2[_clouds.Length];
            for (int i = 0; i < _clouds.Length; i++)
                _cloudHome[i] = _clouds[i].rectTransform.anchoredPosition;
        }

        // Each cloud sways around its place, out of step with the others.
        void StartDrift()
        {
            StopDrift();
            if (_style.CloudDrift <= 0f)
                return;
            _drift = new Tween[_clouds.Length];
            for (int i = 0; i < _clouds.Length; i++)
            {
                RectTransform cloud = _clouds[i].rectTransform;
                float sign = i % 2 == 0 ? 1f : -1f;
                cloud.anchoredPosition = _cloudHome[i] - new Vector2(_style.CloudDrift * sign, 0f);
                _drift[i] = cloud.DOAnchorPosX(_cloudHome[i].x + _style.CloudDrift * sign, _style.CloudDriftPeriod * (0.8f + 0.1f * i))
                    .SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetLink(cloud.gameObject);
            }
        }

        void StopDrift()
        {
            if (_drift == null)
                return;
            foreach (Tween tween in _drift)
                tween?.Kill();
            _drift = null;
            for (int i = 0; i < _clouds.Length; i++)
                _clouds[i].rectTransform.anchoredPosition = _cloudHome[i];
        }

        // Each lid is screen-tall, anchored at its middle. `closed` is the covered share of the view: the first EnterShare slides
        // the edge in from off-screen, then it covers linearly, ending past the middle so a shut pair overlaps.
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
            _barTween?.Kill();
        }
    }
}
