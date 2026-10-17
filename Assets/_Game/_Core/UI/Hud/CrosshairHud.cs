using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // Moves the crosshair to where the active player's aim sway points (shots go there), and fades the dot while aiming
    // down sights (the gun's sights take over) or sprinting. The hit marker is a child, so it follows without fading.
    public sealed class CrosshairHud : PlayerHudView
    {
        [SerializeField, Tooltip("Moved each frame; parent of the dot and the hit marker")] RectTransform _crosshair;
        [SerializeField, Tooltip("Faded while aiming down sights or sprinting")] Graphic _dot;
        [SerializeField, Min(0.1f), Tooltip("Alpha per second when sprinting starts or stops")] float _fadeSpeed = 8f;

        RectTransform _canvas;
        Vector2 _position;
        float _alpha = 1f;
        float _sprintAlpha = 1f;

        void Awake()
        {
            Canvas canvas = _crosshair.GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        }

        protected override void Bind(Player player) { }
        protected override void Unbind(Player player)
        {
            _sprintAlpha = 1f;
            Show(Vector2.zero, 1f);
        }

        // Polled after Player.Update, which places the aim sway for this frame.
        void LateUpdate()
        {
            Player player = Player;
            if (player == null || _canvas == null)
                return;

            HeldItem held = player.HeldItem;
            float aimed = held != null ? held.AimWeight : 0f;
            _sprintAlpha = Mathf.MoveTowards(_sprintAlpha, player.IsSprinting ? 0f : 1f, _fadeSpeed * Time.unscaledDeltaTime);
            Show(player.CrosshairPosition * (_canvas.rect.height * 0.5f), Mathf.Min(_sprintAlpha, 1f - aimed));
        }

        void Show(Vector2 position, float alpha)
        {
            // Under half a pixel of change isn't worth a canvas rebuild.
            if ((position - _position).sqrMagnitude > 0.25f || position == Vector2.zero)
            {
                _position = position;
                _crosshair.anchoredPosition = position;
            }
            if (!Mathf.Approximately(alpha, _alpha))
            {
                _alpha = alpha;
                _dot.canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}
