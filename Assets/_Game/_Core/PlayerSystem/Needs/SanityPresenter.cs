using Ami.BroAudio;
using MB13.Aipocalypse.Core.AudioSystem;
using MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks;
using MB13.Aipocalypse.Core.TimeSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.PlayerSystem.Needs
{
    // Scene service: turns the active player's sanity into screen looks (ScreenLookService, optional, eased) and sounds (a
    // drone loop at the Low look's strength, whispers now and then, a heartbeat). No player, no needs or sanity off: every
    // look fades to 0 and the sounds stop. The drone pauses with the game.
    public sealed class SanityPresenter : ITickable, System.IDisposable
    {
        static readonly ScreenLookId[] Looks = { ScreenLookId.SanityUneasy, ScreenLookId.SanityLow, ScreenLookId.SanityCritical };

        readonly PlayerRegistry _players;
        readonly AudioService _audio;
        readonly TimeService _time;
        readonly ScreenLookService _looks;
        readonly float[] _shown = new float[Looks.Length];
        // The volume to step by: BroAudio restarts a fade on every SetVolume, so it's sent only when it really moved.
        const float DroneStep = 0.02f;

        IAudioPlayer _drone;
        VolumeStep _droneVolume;
        bool _dronePaused;
        float _nextWhisper;
        float _nextBeat;

        public SanityPresenter(PlayerRegistry players, AudioService audio, TimeService time, IObjectResolver resolver)
        {
            _players = players;
            _audio = audio;
            _time = time;
            resolver.TryResolve(out _looks);
        }

        public void Tick()
        {
            PlayerNeeds needs = _players.Current?.Needs;
            SanityEffectSettings effects = needs?.Settings.SanityEffects;
            bool on = needs != null && needs.Sanity.Settings.Enabled && effects != null && !_players.Current.IsDead;
            float share = on ? needs.Sanity.Normalized : 1f;
            float ease = on && effects.EaseTime > 0f ? Time.unscaledDeltaTime / effects.EaseTime : 1f;
            for (int i = 0; i < Looks.Length; i++)
            {
                float target = on ? effects.Strength(Looks[i], share) : 0f;
                _shown[i] = Mathf.MoveTowards(_shown[i], target, ease);
                _looks?.Request(Looks[i], _shown[i]);
            }
            Sounds(on ? effects : null, share);
        }

        void Sounds(SanityEffectSettings effects, float share)
        {
            bool paused = _time != null && _time.IsPaused;
            if (_drone != null && _drone.IsActive && paused != _dronePaused)
            {
                _dronePaused = paused;
                if (paused)
                    _audio.Pause(_drone);
                else
                    _audio.UnPause(_drone);
            }
            float droneVolume = effects != null ? _shown[1] * effects.DroneVolume : 0f;
            if (droneVolume > 0.01f && effects.Drone.IsValid())
            {
                if (_drone == null || !_drone.IsActive)
                {
                    // Starts silent and fades up to the first value sent below.
                    _drone = _audio.Start(effects.Drone, 0f, 0f);
                    _dronePaused = false;
                    _droneVolume.Reset();
                }
                if (_droneVolume.Next(droneVolume, DroneStep, out float send))
                    _audio.SetVolume(_drone, send, 0.5f);
            }
            else
                StopDrone();
            if (effects == null || paused)
                return;

            float now = Time.time;
            if (_shown[0] > 0.01f && effects.Whisper.IsValid() && now >= _nextWhisper)
            {
                if (_nextWhisper > 0f)
                    _audio.Play(effects.Whisper, effects.WhisperVolume * Mathf.Clamp01(_shown[0] + _shown[2]));
                _nextWhisper = now + Random.Range(effects.WhisperEveryMin, Mathf.Max(effects.WhisperEveryMin, effects.WhisperEveryMax));
            }
            if (share < effects.HeartbeatFrom && effects.Heartbeat.IsValid() && now >= _nextBeat)
            {
                _audio.Play(effects.Heartbeat, effects.HeartbeatVolume);
                float depth = effects.HeartbeatFrom > 0f ? Mathf.Clamp01((effects.HeartbeatFrom - share) / effects.HeartbeatFrom) : 1f;
                _nextBeat = now + effects.HeartbeatEvery * Mathf.Lerp(1f, 0.6f, depth);
            }
        }

        void StopDrone()
        {
            if (_drone != null && _drone.IsActive)
                _audio.Stop(_drone, 1f);
            _drone = null;
        }

        public void Dispose() => StopDrone();
    }
}
