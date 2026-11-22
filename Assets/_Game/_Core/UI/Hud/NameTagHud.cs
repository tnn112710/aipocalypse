using MB13.Aipocalypse.Core.Interaction;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Input;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // The name and job icon over what the player aims at (an INameTagged: what E reaches, else what the whistle would call),
    // fading in and out and lingering a moment so a wobbling aim doesn't flicker it.
    public sealed class NameTagHud : PlayerHudView
    {
        [SerializeField] CanvasGroup _group;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _name;
        [SerializeField, Min(0.01f), Tooltip("Seconds to fade in or out")] float _fade = 0.15f;
        [SerializeField, Min(0f), Tooltip("Seconds it stays after the aim leaves")] float _linger = 0.3f;
        [SerializeField, Tooltip("Pixels above the tag point on screen")] float _lift = 12f;

        InputService _input;
        INameTagged _target;
        float _lostAt = float.NegativeInfinity;
        string _shownName;
        Sprite _shownIcon;

        [Inject]
        void Construct(InputService input) => _input = input;

        protected override void Start()
        {
            if (_group != null)
                _group.alpha = 0f;
            base.Start();
        }

        protected override void Unbind(IPlayer player)
        {
            _target = null;
            if (_group != null)
                _group.alpha = 0f;
        }

        void LateUpdate()
        {
            if (_group == null)
                return;
            float now = Time.unscaledTime;
            INameTagged aimed = Aimed();
            if (aimed != null)
            {
                _target = aimed;
                _lostAt = float.PositiveInfinity;
            }
            else if (_lostAt == float.PositiveInfinity)
                _lostAt = now;

            Camera view = Camera.main;
            bool show = false;
            if (_target != null && now - _lostAt < _linger && view != null && _target.TryGetNameTag(out string name, out Sprite icon, out Vector3 point))
            {
                Vector3 screen = view.WorldToScreenPoint(point);
                show = screen.z > 0f;
                if (show)
                {
                    Fill(name, icon);
                    _group.transform.position = new Vector3(screen.x, screen.y + _lift, 0f);
                }
            }
            float step = Time.unscaledDeltaTime / _fade;
            _group.alpha = Mathf.MoveTowards(_group.alpha, show ? 1f : 0f, step);
            if (!show && _group.alpha <= 0f)
                _target = null;
        }

        INameTagged Aimed()
        {
            IPlayer player = Player;
            if (player == null || player.IsDead || (_input != null && _input.IsUIRequested))
                return null;
            if (player.Interactor?.Focus is INameTagged near)
                return near;
            return player.Whistle?.Target as INameTagged;
        }

        // Text and icon change only when the target's do (no rebuild every frame).
        void Fill(string name, Sprite icon)
        {
            if (!ReferenceEquals(name, _shownName))
            {
                _shownName = name;
                _name.SetText(name);
            }
            if (icon != _shownIcon)
            {
                _shownIcon = icon;
                _icon.sprite = icon;
                _icon.enabled = icon != null;
            }
        }

        public void Setup(CanvasGroup group, Image icon, TMP_Text name)
        {
            _group = group;
            _icon = icon;
            _name = name;
        }
    }
}
