using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.UI
{
    // The hip crosshair: moves the reticle (dot and cross, one HipReticle prefab) to where the barrel points, so it wobbles
    // with the gun, and opens the cross to the wobble envelope (how far the barrel may wander). The reticle fades while
    // aiming or sprinting; the hit marker is a sibling, so it follows without fading.
    public sealed class CrosshairHud : PlayerHudView
    {
        [SerializeField, Tooltip("Moved to the aim each frame; parent of the reticle and the hit marker")] RectTransform _crosshair;
        [SerializeField, Tooltip("The reticle design: a HipReticle prefab from UI/Prefabs/Widgets/Reticles, spawned under the crosshair")]
        HipReticle _reticlePrefab;
        [SerializeField, InlineEditor, Tooltip("How the cross opens, closes and fades")] CrosshairStyle _style;

        RectTransform _canvas;
        HipReticle _reticle;
        Vector2 _position;
        float _sprintAlpha = 1f;
        float _gap;

        void Awake()
        {
            if (_style == null)
            {
                Debug.LogWarning($"{name}: no CrosshairStyle assigned; using defaults.", this);
                _style = ScriptableObject.CreateInstance<CrosshairStyle>();
            }
            Canvas canvas = _crosshair.GetComponentInParent<Canvas>();
            _canvas = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (_reticlePrefab != null)
                SetReticle(_reticlePrefab);
            else
                Debug.LogWarning($"{name}: no reticle prefab assigned; the crosshair is hidden.", this);
        }

        // Swaps the reticle design (e.g. one the player unlocked or picked in settings).
        public void SetReticle(HipReticle prefab)
        {
            if (_reticle != null)
                Destroy(_reticle.gameObject);
            _reticle = Instantiate(prefab, _crosshair, false);
            _reticle.transform.SetAsFirstSibling();
        }

        protected override void Unbind(IPlayer player)
        {
            _sprintAlpha = 1f;
            _gap = _style.MinGap;
            Move(Vector2.zero);
            if (_reticle != null)
                _reticle.Show(_gap, 1f, 0f);
        }

        // Polled after Player.Update, which places the aim sway for this frame.
        void LateUpdate()
        {
            IPlayer player = Player;
            if (player == null || _canvas == null || _reticle == null)
                return;

            float pixels = _canvas.rect.height * 0.5f;
            HeldItem held = player.HeldItem;
            float aimed = held != null ? held.AimWeight : 0f;
            float deltaTime = Time.unscaledDeltaTime;
            _sprintAlpha = Mathf.MoveTowards(_sprintAlpha, player.IsSprinting ? 0f : 1f, _style.FadeSpeed * deltaTime);
            float alpha = Mathf.Min(_sprintAlpha, 1f - aimed);
            Move(player.CrosshairPosition * pixels);

            float gap = Mathf.Max(_style.MinGap, player.CrosshairSpread * pixels);
            _gap = Mathf.Lerp(_gap, gap, Damping.ByRate(gap > _gap ? _style.OpenSpeed : _style.CloseSpeed, deltaTime));
            // Only items that wobble or spread (guns) open the cross; an axe or empty hands keep just the dot.
            _reticle.Show(_gap, alpha, held != null && held.Spread > 0f ? alpha : 0f);
        }

        void Move(Vector2 position)
        {
            // Under half a pixel of change isn't worth a canvas rebuild,
            // except a return to the exact centre, so it never rests a fraction of a pixel off.
            if (position != _position && ((position - _position).sqrMagnitude > 0.25f || position == Vector2.zero))
                _crosshair.anchoredPosition = _position = position;
        }
    }
}
