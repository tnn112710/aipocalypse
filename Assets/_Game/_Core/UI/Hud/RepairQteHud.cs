using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.TimeSystem;
using Aipocalypse.Input;
using TMPro;
using UnityEngine;
using VContainer;

namespace Aipocalypse.Core.UI
{
    public sealed class RepairQteHud : PlayerHudView
    {
        [SerializeField] QteStyle _style;
        [SerializeField] GameObject _content;
        [SerializeField] UnityEngine.UI.Image _keyCap;
        [SerializeField] TMP_Text _key;
        [SerializeField] QteView[] _views;

        QuickTimeService _qte;
        TimeService _time;
        InputService _input;

        [Inject]
        void Construct(QuickTimeService qte, TimeService time, InputService input)
        {
            _qte = qte;
            _time = time;
            _input = input;
        }

        protected override void Start()
        {
            ApplyStyle();
            _key.SetText(_input?.GetReloadKeyLabel() ?? "R");
            _content.SetActive(false);
            base.Start();
        }

        public void ApplyStyle()
        {
            if (_style == null)
                return;
            _keyCap.sprite = _style.KeyCap;
            _key.font = _style.Font;
            _key.fontSize = _style.FontSize;
            _key.color = Color.black;
        }

        protected override void Bind(Player player) { }
        protected override void Unbind(Player player) => _content.SetActive(false);

        void LateUpdate()
        {
            bool visible = _qte != null && _qte.IsActive && _input != null && _input.GameplayEnabled &&
                !_time.IsPaused && Player != null && !Player.IsDead;
            _content.SetActive(visible);
            if (!visible)
                return;

            QteView selected = null;
            foreach (QteView view in _views)
                if (view.CanDisplay(_qte.Session) && (selected == null || view.PresentationId == _qte.Presentation))
                    selected = view;
            foreach (QteView view in _views)
            {
                view.gameObject.SetActive(view == selected);
                if (view == selected)
                    view.Display(_qte.Session);
            }
            if (selected != null)
                _keyCap.rectTransform.anchoredPosition = new Vector2(0f, -((RectTransform)selected.transform).rect.height * 0.5f - 22f);
        }
    }
}
