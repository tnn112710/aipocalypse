using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // A full scope's view (4x scope): the screen goes black around a circle with the scope's reticle, fading in at the end
    // of raising the sights (HeldItem.Scope). It sits on the crosshair, so the aim sway moves it like the sights. Its
    // canvas is off while no scope view shows.
    public sealed class ScopeHud : PlayerHudView
    {
        [SerializeField, Tooltip("Nested canvas: off while nothing shows")] Canvas _canvas;
        [SerializeField] CanvasGroup _group;
        [SerializeField, Tooltip("Square, one screen height: the mask, the black bars beside it and the reticle")] RectTransform _view;
        [SerializeField] Image _reticle;

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
            Vector2 position = player.CrosshairPosition * (height * 0.5f);
            if ((position - _position).sqrMagnitude > 0.25f)
            {
                _position = position;
                _view.anchoredPosition = position;
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
