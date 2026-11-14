using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // A full scope's view (4x scope): the screen goes black around a circle with the scope's reticle, fading in at the end
    // of raising the sights (HeldItem.Scope). It sits on the crosshair, so the aim sway moves it like the sights. The circle
    // opens from dark as the eye finds the scope, and slides with the scope off the eye's line (sway, kick) while a shadow
    // darkens the edge it leaves. Its canvas is off while no scope view shows.
    public sealed class ScopeHud : PlayerHudView
    {
        [SerializeField, Tooltip("Nested canvas: off while nothing shows")] Canvas _canvas;
        [SerializeField] CanvasGroup _group;
        [SerializeField, Tooltip("Square, one screen height: follows the crosshair; holds everything below")] RectTransform _view;
        [SerializeField, Tooltip("The circle mask and the black bars beside it: slides and opens")] RectTransform _pupil;
        [SerializeField, Tooltip("Soft dark rim under the mask (twice the view's size): slides further than the circle")] RectTransform _shadow;
        [SerializeField, Tooltip("Black over the circle while the eye is still finding the scope")] Image _dim;
        [SerializeField] Image _reticle;

        [SerializeField, Tooltip("The circle's slide per eye box off the line, as a share of the view's half height")] float _pupilShift = 0.06f;
        [SerializeField, Tooltip("The shadow's slide per eye box off the line, as a share of the view's half height")] float _shadowShift = 0.22f;
        [SerializeField, Range(0.3f, 1f), Tooltip("The circle's size when the scope view starts to show")] float _openFrom = 0.7f;
        [SerializeField, Range(0f, 1f), Tooltip("How dark the circle starts")] float _dimFrom = 0.85f;

        RectTransform _root;
        float _shownWeight = -1f;
        Vector2 _position;
        float _height;

        void Awake()
        {
            Canvas root = GetComponentInParent<Canvas>();
            _root = root != null ? root.rootCanvas.transform as RectTransform : null;
            Show(0f);
        }

        protected override void Unbind(IPlayer player) => Show(0f);

        // After Player.Update, which places the aim sway for this frame.
        void LateUpdate()
        {
            IPlayer player = Player;
            HeldItem held = player?.HeldItem;
            ScopeView scope = held != null ? held.Scope : default;
            Show(scope.Weight);
            if (scope.Weight <= 0f || _root == null)
                return;

            if (_reticle != null && _reticle.sprite != scope.Reticle)
            {
                _reticle.sprite = scope.Reticle;
                _reticle.enabled = scope.Reticle != null;
            }
            float height = _root.rect.height;
            if (!Mathf.Approximately(height, _height))
            {
                _height = height;
                _view.sizeDelta = new Vector2(height, height);
            }
            float half = height * 0.5f;
            Vector2 position = player.CrosshairPosition * half;
            if ((position - _position).sqrMagnitude > 0.25f)
            {
                _position = position;
                _view.anchoredPosition = position;
            }

            float open = Mathf.SmoothStep(0f, 1f, scope.Weight);
            if (_pupil != null)
            {
                _pupil.anchoredPosition = scope.EyeOffset * (_pupilShift * half);
                _pupil.localScale = Vector3.one * Mathf.Lerp(_openFrom, 1f, open);
            }
            if (_shadow != null)
                _shadow.anchoredPosition = scope.EyeOffset * (_shadowShift * half);
            if (_dim != null)
            {
                Color color = _dim.color;
                color.a = (1f - open) * _dimFrom;
                _dim.color = color;
            }
        }

        void Show(float weight)
        {
            if (Mathf.Approximately(weight, _shownWeight))
                return;
            _shownWeight = weight;
            if (_canvas != null && _canvas.enabled != weight > 0f)
                _canvas.enabled = weight > 0f;
            if (_group != null)
                _group.alpha = weight;
        }
    }
}
