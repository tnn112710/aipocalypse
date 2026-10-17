using Aipocalypse.Core.AudioSystem;
using Ami.BroAudio;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
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
            BreathingSettings settings = _tuning.Breathing;
            if (breathing == null || !breathing.IsActive || silent)
            {
                Stop(settings.FadeOut);
                return;
            }

            float volume = breathing.Volume;
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

        public void Stop(float fadeOut = 0f)
        {
            if (_loop != null)
                _audio?.Stop(_loop, fadeOut);
            _loop = null;
        }
    }
}
