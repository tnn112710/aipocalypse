using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.TimeSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace MB13.Aipocalypse.Core.UI
{
    // The active player badly hurt: a red vignette that pulses, stronger and faster the lower the health, with a heartbeat
    // at the top of every pulse. Real time, frozen while the game is paused; nothing while dead.
    public sealed class LowHealthHud : PlayerHudView
    {
        const int TextureSize = 128;
        const float FullCircle = Mathf.PI * 2f;

        [SerializeField, Tooltip("Stretched over the screen; its texture is made in code")] RawImage _vignette;
        [SerializeField, InlineEditor] LowHealthStyle _style;

        TimeService _time;
        AudioService _audio;
        Texture2D _texture;
        float _builtClearRadius = -1f;
        float _strength;
        // Radians into the pulse; the beat falls on its peak, a quarter turn in.
        float _phase;
        bool _beaten;

        [Inject]
        void Construct(TimeService time, AudioService audio)
        {
            _time = time;
            _audio = audio;
        }

        void Awake()
        {
            if (_style == null)
            {
                Debug.LogWarning($"{name}: no LowHealthStyle assigned; using defaults.", this);
                _style = ScriptableObject.CreateInstance<LowHealthStyle>();
            }
            _vignette.enabled = false;
        }

        protected override void Unbind(IPlayer player) => Hide();

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_texture != null)
                Destroy(_texture);
        }

        void Update()
        {
            if (_time != null && _time.IsPaused)
                return;

            IPlayer player = Player;
            float target = player != null && !player.Health.IsDead ? _style.Strength(player.Health.Normalized) : 0f;
            float deltaTime = Time.unscaledDeltaTime;
            _strength = Mathf.MoveTowards(_strength, target, deltaTime / _style.Ease);
            if (_strength <= 0f)
            {
                if (_vignette.enabled)
                    Hide();
                return;
            }

            if (_texture == null || !Mathf.Approximately(_builtClearRadius, _style.ClearRadius))
                Rebuild();
            _phase += deltaTime * Mathf.Lerp(_style.PulseRate, _style.PulseRateFull, _strength) * FullCircle;
            if (_phase >= FullCircle)
            {
                _phase -= FullCircle;
                _beaten = false;
            }
            if (!_beaten && _phase >= FullCircle * 0.25f && target > 0f)
            {
                _beaten = true;
                _audio?.Play(_style.Heartbeat, Mathf.Lerp(_style.MinVolume, 1f, _strength));
            }

            float pulse = Mathf.Lerp(_style.PulseFloor, 1f, 0.5f + 0.5f * Mathf.Sin(_phase));
            _vignette.color = _style.Color;
            _vignette.enabled = true;
            _vignette.canvasRenderer.SetAlpha(_style.MaxAlpha * _strength * pulse);
        }

        void Hide()
        {
            _strength = 0f;
            _phase = 0f;
            _beaten = false;
            if (_vignette != null)
                _vignette.enabled = false;
        }

        // White, clear inside ClearRadius of the half-diagonal and opaque at the corners. Made in code, never saved.
        void Rebuild()
        {
            _builtClearRadius = _style.ClearRadius;
            if (_texture == null)
            {
                _texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
                {
                    name = "LowHealthVignette",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.DontSave,
                };
                _vignette.texture = _texture;
            }
            var pixels = new Color32[TextureSize * TextureSize];
            float half = (TextureSize - 1) * 0.5f;
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = new Vector2((x - half) / half, (y - half) / half).magnitude / Mathf.Sqrt(2f);
                    float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_style.ClearRadius, 1f, distance));
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(fade * 255f));
                }
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false, false);
        }
    }
}
