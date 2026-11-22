using System.Collections.Generic;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // How often the dog may make noise, so barking never gets annoying: one sound per VoiceGap, and per-sound gaps
    // (barks also capped per minute).
    public sealed class DogVoice
    {
        const float Minute = 60f;

        readonly DogSettings _settings;
        readonly Dictionary<DogSound, float> _last = new();
        readonly Queue<float> _barks = new();
        float _lastAny = float.NegativeInfinity;

        public DogVoice(DogSettings settings) => _settings = settings;

        public bool TryPlay(DogSound sound, float now)
        {
            if (now - _lastAny < _settings.VoiceGap)
                return false;
            if (_last.TryGetValue(sound, out float last) && now - last < Gap(sound))
                return false;
            if (sound == DogSound.Bark)
            {
                while (_barks.Count > 0 && now - _barks.Peek() >= Minute)
                    _barks.Dequeue();
                if (_barks.Count >= _settings.BarkPerMinute)
                    return false;
                _barks.Enqueue(now);
            }
            _last[sound] = now;
            _lastAny = now;
            return true;
        }

        float Gap(DogSound sound) => sound switch
        {
            DogSound.Bark => _settings.BarkGap,
            DogSound.Whine => _settings.WhineGap,
            DogSound.Yelp => _settings.YelpGap,
            _ => 0f,
        };
    }
}
