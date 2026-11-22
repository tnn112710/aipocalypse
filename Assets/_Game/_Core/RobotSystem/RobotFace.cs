using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A robot's face screen: its mood, a held expression for a moment, blinks, a scrolling speech line while it talks, and a
    // brighter glow at night. Frames are picked by shifting the textures in property blocks (the material asset is never
    // written). Advanced by Robot: no Update.
    public sealed class RobotFace : MonoBehaviour
    {
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] RobotFaceSet _set;
        [SerializeField, Min(0f), Tooltip("At full night the screen glows this much brighter (1 = twice)")] float _nightGlow = 1.2f;

        Renderer _eyes;
        Renderer _mouth;
        Renderer _speech;
        MaterialPropertyBlock _block;
        Vector4 _eyesSt;
        Vector4 _mouthSt;
        Vector4 _speechSt;
        Color _eyesEmission;
        Color _mouthEmission;
        Color _speechEmission;
        RobotExpression _mood = RobotExpression.Neutral;
        RobotExpression _held;
        float _heldUntil = float.NegativeInfinity;
        float _nextBlink;
        float _blinkEnd;
        float _night;
        float _speechScroll;
        int _shownEyes = -1;
        int _shownMouth = -1;
        float _shownNight = -1f;
        bool _speaking;

        public RobotFaceSet Set => _set;
        public RobotExpression Mood => _mood;
        // What the screen shows right now (a held expression, else the mood).
        public RobotExpression Shown { get; private set; }

        void Awake()
        {
            if (_set == null)
                return;
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
                if (candidate.name == _set.EyesRenderer)
                    _eyes = candidate;
                else if (candidate.name == _set.MouthRenderer)
                    _mouth = candidate;
                else if (!string.IsNullOrEmpty(_set.SpeechRenderer) && candidate.name == _set.SpeechRenderer)
                    _speech = candidate;
            _block = new MaterialPropertyBlock();
            Read(_eyes, out _eyesSt, out _eyesEmission);
            Read(_mouth, out _mouthSt, out _mouthEmission);
            Read(_speech, out _speechSt, out _speechEmission);
            if (_speech != null)
            {
                // The vendor model keeps it switched off; shown by turning the renderer on instead.
                _speech.gameObject.SetActive(true);
                _speech.enabled = false;
            }
        }

        static void Read(Renderer renderer, out Vector4 st, out Color emission)
        {
            Material material = renderer != null ? renderer.sharedMaterial : null;
            st = material != null && material.HasProperty(BaseMapSt) ? material.GetVector(BaseMapSt) : new Vector4(1f, 1f, 0f, 0f);
            emission = material != null && material.HasProperty(EmissionColor) ? material.GetColor(EmissionColor) : Color.black;
        }

        public void SetMood(RobotExpression mood) => _mood = mood;

        // Shows `expression` for `seconds`, then back to the mood.
        public void Hold(RobotExpression expression, float seconds, float now)
        {
            _held = expression;
            _heldUntil = now + seconds;
        }

        public void SetNight(float night) => _night = night;

        public void Tick(float now, float deltaTime, RobotSettings settings)
        {
            if (_set == null || _eyes == null || _mouth == null)
                return;
            if (now >= _nextBlink)
            {
                _blinkEnd = now + settings.BlinkTime;
                _nextBlink = now + Random.Range(settings.BlinkInterval.x, settings.BlinkInterval.y);
            }
            Shown = now < _heldUntil ? _held : _mood;
            if (!_set.Frames.TryGetValue(Shown, out RobotFaceSet.Frame frame))
                return;
            int eyes = frame.Eyes;
            // Closed eyes don't blink, nor do crossed or heart ones.
            if (now < _blinkEnd && Blinks(Shown) && _set.Frames.TryGetValue(RobotExpression.Blink, out RobotFaceSet.Frame blink))
                eyes = blink.Eyes;
            Show(eyes, frame.Mouth);
            Speak(Shown == RobotExpression.Talk && _speech != null, deltaTime);
        }

        static bool Blinks(RobotExpression shown) => shown is not (RobotExpression.Sleepy or RobotExpression.Blink or RobotExpression.Yawn
            or RobotExpression.Dizzy or RobotExpression.Love or RobotExpression.Excited);

        void Show(int eyes, int mouth)
        {
            if (eyes == _shownEyes && mouth == _shownMouth && Mathf.Abs(_night - _shownNight) < 0.01f)
                return;
            _shownEyes = eyes;
            _shownMouth = mouth;
            _shownNight = _night;
            float glow = Glow;
            Apply(_eyes, new Vector4(_eyesSt.x, _eyesSt.y, _eyesSt.z + eyes / (float)_set.Columns, _eyesSt.w), _eyesEmission * glow);
            Apply(_mouth, new Vector4(_mouthSt.x, _mouthSt.y, _mouthSt.z + mouth / (float)_set.Columns, _mouthSt.w), _mouthEmission * glow);
        }

        float Glow => 1f + _nightGlow * _night;

        // While it talks the mouth gives way to the speech line, scrolling.
        void Speak(bool speaking, float deltaTime)
        {
            if (speaking != _speaking)
            {
                _speaking = speaking;
                _mouth.enabled = !speaking;
                if (_speech != null)
                    _speech.enabled = speaking;
            }
            if (!speaking)
                return;
            _speechScroll = Mathf.Repeat(_speechScroll + _set.SpeechScroll * deltaTime, 1f);
            Apply(_speech, new Vector4(_speechSt.x, _speechSt.y, _speechSt.z + _speechScroll, _speechSt.w + _set.SpeechTrack / (float)_set.SpeechTracks),
                _speechEmission * Glow);
        }

        void Apply(Renderer renderer, Vector4 st, Color emission)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapSt, st);
            _block.SetColor(EmissionColor, emission);
            renderer.SetPropertyBlock(_block);
        }

        public void Setup(RobotFaceSet set) => _set = set;
    }
}
