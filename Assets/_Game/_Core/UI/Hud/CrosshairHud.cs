using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // Moves the crosshair to where the active player's aim sway points (shots go there), and fades the dot while aiming
    // down sights (the gun's sights take over) or sprinting. Spread ticks open out to the held item's spread cone
    // (airborne spread, a shotgun's pellets) and hide when it's tight. The hit marker is a child, so it follows without fading.
    public sealed class CrosshairHud : PlayerHudView
    {
        static readonly Vector2[] TickDirections = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        [SerializeField, Tooltip("Moved each frame; parent of the dot and the hit marker")] RectTransform _crosshair;
        [SerializeField, Tooltip("Faded while aiming down sights or sprinting")] Graphic _dot;
        [SerializeField, Tooltip("Up, right, down, left: pushed out to the spread cone's edge")] Graphic[] _spreadTicks;
        [SerializeField, Min(0f), Tooltip("Pixels: ticks hide when the cone is smaller than this")] float _minSpread = 8f;
        [SerializeField, Min(0.1f), Tooltip("Pixels over which the ticks fade in past the minimum")] float _spreadFade = 6f;
        [SerializeField, Min(0.1f), Tooltip("How fast the ticks follow the spread")] float _spreadSpeed = 20f;
        [SerializeField, Min(0.1f), Tooltip("Alpha per second when sprinting starts or stops")] float _fadeSpeed = 8f;

        RectTransform _canvas;
        Vector2 _position;
        float _alpha = 1f;
        float _sprintAlpha = 1f;
        float _spread;
        float _shownSpread = -1f;
        float _tickAlpha = -1f;

        void Awake()
        {
            Canvas canvas = _crosshair.GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        }

        protected override void Unbind(Player player)
        {
            _sprintAlpha = 1f;
            _spread = 0f;
            Show(Vector2.zero, 1f);
            ShowSpread(0f, 0f);
        }

        // Polled after Player.Update, which places the aim sway for this frame.
        void LateUpdate()
        {
            Player player = Player;
            if (player == null || _canvas == null)
                return;

            float pixels = _canvas.rect.height * 0.5f;
            HeldItem held = player.HeldItem;
            float aimed = held != null ? held.AimWeight : 0f;
            float deltaTime = Time.unscaledDeltaTime;
            _sprintAlpha = Mathf.MoveTowards(_sprintAlpha, player.IsSprinting ? 0f : 1f, _fadeSpeed * deltaTime);
            float alpha = Mathf.Min(_sprintAlpha, 1f - aimed);
            Show(player.CrosshairPosition * pixels, alpha);

            _spread = Mathf.Lerp(_spread, player.CrosshairSpread * pixels, Damping.ByRate(_spreadSpeed, deltaTime));
            ShowSpread(_spread, alpha * Mathf.Clamp01((_spread - _minSpread) / _spreadFade));
        }

        void Show(Vector2 position, float alpha)
        {
            // Under half a pixel of change isn't worth a canvas rebuild.
            // Except a return to the exact centre, so the crosshair never rests a fraction of a pixel off.
            if (position != _position && ((position - _position).sqrMagnitude > 0.25f || position == Vector2.zero))
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

        void ShowSpread(float radius, float alpha)
        {
            if (_spreadTicks == null)
                return;
            bool move = Mathf.Abs(radius - _shownSpread) > 0.5f;
            bool fade = !Mathf.Approximately(alpha, _tickAlpha);
            if (!move && !fade)
                return;
            if (move)
                _shownSpread = radius;
            _tickAlpha = alpha;
            for (int i = 0; i < _spreadTicks.Length && i < TickDirections.Length; i++)
            {
                Graphic tick = _spreadTicks[i];
                if (tick == null)
                    continue;
                if (move)
                    tick.rectTransform.anchoredPosition = TickDirections[i] * radius;
                tick.canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}
