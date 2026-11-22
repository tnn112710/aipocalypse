using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A robot's face screen: its mood (the expression it rests on), a held expression for a moment, blinks, and a brighter
    // glow at night. Frames are picked by shifting the eye and mouth textures (a property block each, so the material
    // asset is never written). Advanced by RobotManager: no Update.
    public sealed class RobotFace : MonoBehaviour
    {
        static readonly int BaseMapSt = Shader.PropertyToID("_BaseMap_ST");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField] RobotFaceSet _set;
        [SerializeField, Min(0f), Tooltip("At full night the screen glows this much brighter (1 = twice)")] float _nightGlow = 1.2f;

        Renderer _eyes;
        Renderer _mouth;
        MaterialPropertyBlock _block;
        Vector4 _eyesSt;
        Vector4 _mouthSt;
        Color _eyesEmission;
        Color _mouthEmission;
        RobotExpression _mood = RobotExpression.Neutral;
        RobotExpression _held;
        float _heldUntil = float.NegativeInfinity;
        float _nextBlink;
        float _blinkEnd;
        float _night;
        int _shownEyes = -1;
        int _shownMouth = -1;
        float _shownNight = -1f;

        public RobotFaceSet Set => _set;
        public RobotExpression Mood => _mood;

        void Awake()
        {
            if (_set == null)
                return;
            foreach (Renderer candidate in GetComponentsInChildren<Renderer>(true))
                if (candidate.name == _set.EyesRenderer)
                    _eyes = candidate;
                else if (candidate.name == _set.MouthRenderer)
                    _mouth = candidate;
            _block = new MaterialPropertyBlock();
            Read(_eyes, out _eyesSt, out _eyesEmission);
            Read(_mouth, out _mouthSt, out _mouthEmission);
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

        public void Tick(float now, RobotSettings settings)
        {
            if (_set == null || _eyes == null || _mouth == null)
                return;
            if (now >= _nextBlink)
            {
                _blinkEnd = now + settings.BlinkTime;
                _nextBlink = now + Random.Range(settings.BlinkInterval.x, settings.BlinkInterval.y);
            }
            RobotExpression shown = now < _heldUntil ? _held : _mood;
            if (!_set.Frames.TryGetValue(shown, out RobotFaceSet.Frame frame))
                return;
            int eyes = frame.Eyes;
            // Closed or sleepy eyes don't blink.
            if (now < _blinkEnd && shown != RobotExpression.Sleepy && shown != RobotExpression.Blink
                && _set.Frames.TryGetValue(RobotExpression.Blink, out RobotFaceSet.Frame blink))
                eyes = blink.Eyes;
            Show(eyes, frame.Mouth);
        }

        void Show(int eyes, int mouth)
        {
            if (eyes == _shownEyes && mouth == _shownMouth && Mathf.Abs(_night - _shownNight) < 0.01f)
                return;
            _shownEyes = eyes;
            _shownMouth = mouth;
            _shownNight = _night;
            float glow = 1f + _nightGlow * _night;
            Apply(_eyes, _eyesSt, eyes, _eyesEmission * glow);
            Apply(_mouth, _mouthSt, mouth, _mouthEmission * glow);
        }

        void Apply(Renderer renderer, Vector4 st, int frame, Color emission)
        {
            renderer.GetPropertyBlock(_block);
            _block.SetVector(BaseMapSt, new Vector4(st.x, st.y, st.z + frame / (float)_set.Columns, st.w));
            _block.SetColor(EmissionColor, emission);
            renderer.SetPropertyBlock(_block);
        }

        public void Setup(RobotFaceSet set) => _set = set;
    }
}
