using Aipocalypse.Core.EquipmentSystem;
using Aipocalypse.Core.PlayerSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace Aipocalypse.Core.UI
{
    // Moves the crosshair to where the aim sway points and fades the dot while aiming or sprinting. Spread ticks open to the
    // held item's spread cone and hide when it's tight; the hit marker is a child, so it follows without fading.
    public sealed class CrosshairHud : PlayerHudView
    {
        static readonly Vector2[] TickDirections = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

        [SerializeField, Tooltip("Moved each frame; parent of the dot and the hit marker")] RectTransform _crosshair;
        [SerializeField, Tooltip("Faded while aiming down sights or sprinting")] Graphic _dot;
        [SerializeField, Tooltip("Up, right, down, left: pushed out to the spread cone's edge")] Graphic[] _spreadTicks;
        [SerializeField, InlineEditor, Tooltip("How the dot and the spread ticks fade and follow")] CrosshairStyle _style;

        RectTransform _canvas;
        Vector2 _position;
        float _alpha = 1f;
        float _sprintAlpha = 1f;
        float _spread;
        float _shownSpread = -1f;
        float _tickAlpha = -1f;

        void Awake()
        {
            if (_style == null)
            {
                Debug.LogWarning($"{name}: no CrosshairStyle assigned; using defaults.", this);
                _style = ScriptableObject.CreateInstance<CrosshairStyle>();
            }
            Canvas canvas = _crosshair.GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
        }

        protected override void Unbind(IPlayer player)
        {
            _sprintAlpha = 1f;
            _spread = 0f;
            Show(Vector2.zero, 1f);
            ShowSpread(0f, 0f);
        }

        // Polled after Player.Update, which places the aim sway for this frame.
        void LateUpdate()
        {
            IPlayer player = Player;
            if (player == null || _canvas == null)
                return;

            float pixels = _canvas.rect.height * 0.5f;
            HeldItem held = player.HeldItem;
            float aimed = held != null ? held.AimWeight : 0f;
            float deltaTime = Time.unscaledDeltaTime;
            _sprintAlpha = Mathf.MoveTowards(_sprintAlpha, player.IsSprinting ? 0f : 1f, _style.FadeSpeed * deltaTime);
            float alpha = Mathf.Min(_sprintAlpha, 1f - aimed);
            Show(player.CrosshairPosition * pixels, alpha);

            _spread = Mathf.Lerp(_spread, player.CrosshairSpread * pixels, Damping.ByRate(_style.SpreadSpeed, deltaTime));
            ShowSpread(_spread, alpha * Mathf.Clamp01((_spread - _style.MinSpread) / _style.SpreadFade));
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
