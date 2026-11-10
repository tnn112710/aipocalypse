using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using TMPro;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // A short list of what was just picked up ("+30 Rifle Ammo"), newest at the bottom, each line fading out; a warning line
    // when something didn't fit. Repeated pickups of the same item add up on one line while it's showing. Plays the pickup
    // sounds (InventoryStyle).
    public sealed class PickupFeedHud : PlayerHudView
    {
        public const string NoRoom = "Backpack full";
        // Several piles collected in one go (the magnet) sound once.
        const float SoundGap = 0.06f;

        [SerializeField, Tooltip("Pickup lines, bottom one first; reused round-robin")] TMP_Text[] _lines;
        [SerializeField, Tooltip("The 'Backpack full' line")] TMP_Text _warning;
        [SerializeField, Min(0.1f), Tooltip("Seconds a line stays before fading")] float _hold = 2.5f;
        [SerializeField, Min(0.01f), Tooltip("Seconds it takes to fade out")] float _fade = 0.6f;
        [SerializeField] Color _color = new(0.95f, 0.95f, 0.95f, 1f);
        [SerializeField] Color _warningColor = new(1f, 0.45f, 0.35f, 1f);
        [SerializeField, Tooltip("Its pickup, Tokens and refused sounds")] InventoryStyle _style;

        AudioService _audio;
        float _nextSound;

        Equipment _equipment;
        ItemDefinition[] _items;
        int[] _counts;
        float[] _shownAt;
        float _warningAt = float.NegativeInfinity;
        // Nothing is drawn after this: LateUpdate has nothing to fade.
        float _quietAt = float.NegativeInfinity;

        [Inject]
        void Construct(AudioService audio) => _audio = audio;

        void Awake()
        {
            _items = new ItemDefinition[_lines.Length];
            _counts = new int[_lines.Length];
            _shownAt = new float[_lines.Length];
            for (int i = 0; i < _lines.Length; i++)
            {
                _shownAt[i] = float.NegativeInfinity;
                _lines[i].enabled = false;
            }
            if (_warning != null)
            {
                _warning.enabled = false;
                _warning.SetText(NoRoom);
            }
        }

        protected override void Bind(IPlayer player)
        {
            _equipment = player.Equipment;
            if (_equipment == null)
                return;
            _equipment.PickedUp += OnPickedUp;
            _equipment.PickUpBlocked += OnBlocked;
        }

        protected override void Unbind(IPlayer player)
        {
            if (_equipment == null)
                return;
            _equipment.PickedUp -= OnPickedUp;
            _equipment.PickUpBlocked -= OnBlocked;
            _equipment = null;
        }

        void OnPickedUp(ItemDefinition item, int count)
        {
            if (_style != null)
                Play(item.Category == ItemCategory.Currency ? _style.TokensSound : _style.PickupSound);
            // Same item still showing: add to its line.
            int line = System.Array.IndexOf(_items, item);
            if (line >= 0 && Visible(line))
            {
                _counts[line] += count;
            }
            else
            {
                // Shift the lines up, newest goes at the bottom (index 0).
                for (int i = _lines.Length - 1; i > 0; i--)
                {
                    _items[i] = _items[i - 1];
                    _counts[i] = _counts[i - 1];
                    _shownAt[i] = _shownAt[i - 1];
                }
                line = 0;
                _items[0] = item;
                _counts[0] = count;
            }
            _shownAt[line] = Time.unscaledTime;
            _quietAt = Time.unscaledTime + _hold + _fade;
            for (int i = 0; i < _lines.Length; i++)
                Write(i);
        }

        void OnBlocked(ItemDefinition item)
        {
            if (_style != null)
                Play(_style.RefusedSound);
            _warningAt = Time.unscaledTime;
            _quietAt = Mathf.Max(_quietAt, _warningAt + _hold + _fade);
        }

        void Play(Ami.BroAudio.SoundID sound)
        {
            if (_audio == null || Time.unscaledTime < _nextSound)
                return;
            _nextSound = Time.unscaledTime + SoundGap;
            _audio.Play(sound);
        }

        void Write(int line)
        {
            if (_items[line] == null)
                return;
            _lines[line].SetText($"+{_counts[line]:N0}  {_items[line].DisplayName}");
        }

        bool Visible(int line) => Time.unscaledTime - _shownAt[line] < _hold + _fade;

        void LateUpdate()
        {
            // One extra frame past the last fade hides the lines; then nothing to do until the next pickup.
            if (Time.unscaledTime > _quietAt + 0.1f)
                return;
            for (int i = 0; i < _lines.Length; i++)
                Paint(_lines[i], _shownAt[i], _color);
            if (_warning != null)
                Paint(_warning, _warningAt, _warningColor);
        }

        void Paint(TMP_Text text, float shownAt, Color color)
        {
            float age = Time.unscaledTime - shownAt;
            bool visible = age < _hold + _fade;
            if (text.enabled != visible)
                text.enabled = visible;
            if (!visible)
                return;
            color.a *= age <= _hold ? 1f : 1f - (age - _hold) / _fade;
            text.color = color;
        }
    }
}
