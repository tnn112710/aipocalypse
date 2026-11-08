using MB13.Aipocalypse.Core.AudioSystem;
using Ami.BroAudio;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    [System.Serializable]
    public sealed class BreathSoundSettings
    {
        [Range(0f, 1f), Tooltip("Breath volume just under the breathing threshold; full at 0 / exhausted")]
        public float MinVolume = 0.4f;
        [Min(0f), Tooltip("Seconds the breath loop fades in when heavy breathing starts")]
        public float FadeIn = 0.4f;
        [Min(0f), Tooltip("Seconds it fades out once you've caught your breath")]
        public float FadeOut = 1.5f;
    }

    // Loops the breath sound while out of breath, louder the more tired, and pauses it while time is stopped (a loop
    // would otherwise keep playing over the pause menu).
    public sealed class PlayerBreathSound
    {
        // Volume changes smaller than this aren't worth a fade.
        const float VolumeStep = 0.03f;
        const float VolumeFade = 0.2f;

        readonly AudioService _audio;
        readonly PlayerSoundTuning _tuning;
        IAudioPlayer _loop;
        float _volume;
        bool _paused;

        public PlayerBreathSound(AudioService audio, PlayerSoundTuning tuning)
        {
            _audio = audio;
            _tuning = tuning;
        }

        public void SetPaused(bool paused)
        {
            if (paused == _paused)
                return;
            _paused = paused;
            if (paused)
                _audio?.Pause(_loop);
            else
                _audio?.UnPause(_loop);
        }

        // `breathing` null or inactive (or `silent`) fades the loop out.
        public void Tick(PlayerBreathing breathing, bool silent)
        {
            BreathSoundSettings settings = _tuning.Breath;
            if (breathing == null || !breathing.IsActive || silent)
            {
                Stop(settings.FadeOut);
                return;
            }

            float volume = VolumeFor(breathing.Tiredness, settings.MinVolume);
            if (_loop == null || !_loop.IsActive)
            {
                _loop = _audio?.Start(_tuning.BreathSound, volume, settings.FadeIn);
                _volume = volume;
            }
            else if (Mathf.Abs(volume - _volume) > VolumeStep)
            {
                _audio.SetVolume(_loop, volume, VolumeFade);
                _volume = volume;
            }
        }

        public static float VolumeFor(float tiredness, float minVolume) => Mathf.Lerp(minVolume, 1f, tiredness);

        public void Stop(float fadeOut = 0f)
        {
            if (_loop != null)
                _audio?.Stop(_loop, fadeOut);
            _loop = null;
        }
    }
}
