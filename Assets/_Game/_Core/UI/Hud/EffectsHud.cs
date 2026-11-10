using MB13.Aipocalypse.Core.PlayerSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MB13.Aipocalypse.Core.UI
{
    // A row of what the player's consumables are doing (Player.Effects): the item's icon and the seconds left, dimmed while a
    // hit pauses healing. Labels are rebuilt only when the whole seconds change.
    public sealed class EffectsHud : PlayerHudView
    {
        [SerializeField, Tooltip("Inactive template: an Image (icon) with a TMP_Text child (seconds); cloned per entry")]
        Image _template;
        [SerializeField, Min(1)] int _maxEntries = 6;
        [SerializeField, Range(0f, 1f), Tooltip("Alpha of healing entries while a hit pauses healing")] float _pausedAlpha = 0.4f;

        Image[] _icons;
        TMP_Text[] _labels;
        int[] _seconds;

        void Awake()
        {
            _icons = new Image[_maxEntries];
            _labels = new TMP_Text[_maxEntries];
            _seconds = new int[_maxEntries];
            if (_template == null)
                return;
            _template.gameObject.SetActive(false);
            for (int i = 0; i < _maxEntries; i++)
            {
                _icons[i] = Instantiate(_template, _template.transform.parent, false);
                _icons[i].name = $"Effect{i}";
                _labels[i] = _icons[i].GetComponentInChildren<TMP_Text>(true);
                _seconds[i] = -1;
            }
        }

        protected override void Unbind(IPlayer player) => Show(0);

        void LateUpdate()
        {
            PlayerEffects effects = Player?.Effects;
            if (effects == null || _template == null)
            {
                Show(0);
                return;
            }
            int count = Mathf.Min(effects.Effects.Count, _maxEntries);
            for (int i = 0; i < count; i++)
            {
                PlayerEffects.Active effect = effects.Effects[i];
                Image icon = _icons[i];
                if (icon.sprite != effect.Source.Icon)
                    icon.sprite = effect.Source.Icon;
                Color color = icon.color;
                color.a = effect.Heals && effects.HealingPaused ? _pausedAlpha : 1f;
                icon.color = color;
                int seconds = Mathf.CeilToInt(effect.Remaining);
                if (_labels[i] != null && seconds != _seconds[i])
                {
                    _seconds[i] = seconds;
                    _labels[i].SetText("{0}s", (float)seconds);
                }
            }
            Show(count);
        }

        void Show(int count)
        {
            if (_icons == null)
                return;
            for (int i = 0; i < _icons.Length; i++)
            {
                if (_icons[i] == null)
                    continue;
                bool show = i < count;
                if (_icons[i].gameObject.activeSelf != show)
                    _icons[i].gameObject.SetActive(show);
                if (!show)
                    _seconds[i] = -1;
            }
        }
    }
}
