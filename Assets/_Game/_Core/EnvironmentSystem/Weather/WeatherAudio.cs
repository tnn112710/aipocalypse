using System;
using System.Collections.Generic;
using Aipocalypse.Core.AudioSystem;
using Ami.BroAudio;
using UnityEngine;

namespace Aipocalypse.Core.EnvironmentSystem
{
    // The weather's looping beds: one kept player per distinct sound across all skies and winds, started when audible and
    // stopped when silent, so Windy → Gale fades one wind loop instead of crossfading two copies.
    public sealed class WeatherAudio : IDisposable
    {
        const float Silent = 0.005f;
        const float VolumeStep = 0.01f;

        readonly AudioService _audio;
        readonly SoundID[] _sounds;
        readonly IAudioPlayer[] _players;
        readonly float[] _shown;

        public WeatherAudio(WeatherSettings settings, AudioService audio)
        {
            _audio = audio;
            var sounds = new List<SoundID>();
            foreach (WeatherSettings.Sky sky in settings.Skies.Values)
                Add(sounds, sky.Bed);
            foreach (WeatherSettings.Wind wind in settings.Winds.Values)
                Add(sounds, wind.Bed);
            _sounds = sounds.ToArray();
            _players = new IAudioPlayer[_sounds.Length];
            _shown = new float[_sounds.Length];
        }

        public int Count => _sounds.Length;

        static void Add(List<SoundID> sounds, SoundID id)
        {
            if (id.IsValid() && !sounds.Contains(id))
                sounds.Add(id);
        }

        // Volume per bed for a sky and wind, into `volumes` (Count long).
        public void Targets(WeatherSettings.Sky sky, WeatherSettings.Wind wind, float[] volumes)
        {
            Array.Clear(volumes, 0, volumes.Length);
            if (sky != null)
                AddTo(volumes, sky.Bed, sky.BedVolume);
            if (wind != null)
                AddTo(volumes, wind.Bed, wind.BedVolume);
        }

        void AddTo(float[] volumes, SoundID id, float volume)
        {
            int index = Array.IndexOf(_sounds, id);
            if (index >= 0)
                volumes[index] += volume;
        }

        public void Show(float[] volumes, float fade)
        {
            for (int i = 0; i < _sounds.Length; i++)
            {
                float volume = Mathf.Clamp01(volumes[i]);
                IAudioPlayer player = _players[i];
                // Pooled players are recycled once they stop.
                bool playing = player != null && player.IsActive;
                if (volume <= Silent)
                {
                    if (playing)
                        _audio.Stop(player, fade);
                    _players[i] = null;
                    _shown[i] = 0f;
                }
                else if (!playing)
                {
                    _players[i] = _audio.Start(_sounds[i], volume, fade);
                    _shown[i] = volume;
                }
                else if (Mathf.Abs(volume - _shown[i]) >= VolumeStep)
                {
                    _audio.SetVolume(player, volume, fade);
                    _shown[i] = volume;
                }
            }
        }

        public void Stop(float fade)
        {
            for (int i = 0; i < _players.Length; i++)
            {
                _audio.Stop(_players[i], fade);
                _players[i] = null;
                _shown[i] = 0f;
            }
        }

        public void Dispose() => Stop(1f);
    }
}
