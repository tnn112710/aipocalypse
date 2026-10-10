using UnityEngine;

namespace TextStudio
{
    /// <summary>One finite, deterministic launch of a renderer-owned retained semantic unit.</summary>
    [CreateAssetMenu(fileName = "Ballistic Motion Reaction",
        menuName = "Text Studio/Reactions/Ballistic Retained Motion", order = 108)]
    public sealed class TextStudioReactionBallisticMotionActionAsset : TextStudioReactionActionAsset
    {
        [SerializeField, Tooltip("Initial velocity in the captured text's local space, per second.")]
        Vector3 _initialVelocity = new Vector3(0f, 1.5f, 0f);
        [SerializeField, Tooltip("Acceleration in the captured text's local space, per second squared.")]
        Vector3 _gravity = new Vector3(0f, -9.81f, 0f);
        [SerializeField, Min(0f), Tooltip("Deterministic per-unit launch variation in local units per second.")]
        float _scatter = 1f;
        [SerializeField, Min(0f), Tooltip("Maximum deterministic angular speed in degrees per second.")]
        float _spin = 180f;
        [SerializeField, Min(0f), Tooltip("Velocity damping per second. Zero preserves momentum.")]
        float _drag = .1f;
        [SerializeField, Range(0f, 1f), Tooltip("Normalized response time when shrinking starts. One keeps full size until release.")]
        float _shrinkStart = .65f;
        [SerializeField, Tooltip("Multiplies the retained unit's captured vertex colors.")]
        Color _tint = Color.white;
        [SerializeField, Min(0), Tooltip("Maximum simultaneous responses using this action asset. Zero uses the recipe budget.")]
        int _maxConcurrent;
        [SerializeField, Min(0), Tooltip("Maximum outgoing units from one text change using this action asset. Zero has no per-change limit.")]
        int _maxUnitsPerChange;

        public Vector3 InitialVelocity { get => _initialVelocity; set { _initialVelocity = value; NotifyChanged(); } }
        public Vector3 Gravity { get => _gravity; set { _gravity = value; NotifyChanged(); } }
        public float Scatter { get => _scatter; set { _scatter = value; NotifyChanged(); } }
        public float Spin { get => _spin; set { _spin = value; NotifyChanged(); } }
        public float Drag { get => _drag; set { _drag = value; NotifyChanged(); } }
        public float ShrinkStart { get => _shrinkStart; set { _shrinkStart = value; NotifyChanged(); } }
        public Color Tint { get => _tint; set { _tint = value; NotifyChanged(); } }
        public int MaxConcurrent { get => _maxConcurrent; set { _maxConcurrent = value; NotifyChanged(); } }
        public int MaxUnitsPerChange { get => _maxUnitsPerChange; set { _maxUnitsPerChange = value; NotifyChanged(); } }

        public override bool TryCompile(out ITextStudioReactionCommand command, out string error)
        {
            if (!Finite(_initialVelocity) || !Finite(_gravity) ||
                !FiniteNonnegative(_scatter) || !FiniteNonnegative(_spin) ||
                !FiniteNonnegative(_drag) || !FiniteNonnegative(_shrinkStart) ||
                _shrinkStart > 1f || !Finite(_tint.r) || !Finite(_tint.g) ||
                !Finite(_tint.b) || !Finite(_tint.a) ||
                _tint.r < 0f || _tint.r > 1f ||
                _tint.g < 0f || _tint.g > 1f ||
                _tint.b < 0f || _tint.b > 1f ||
                _tint.a < 0f || _tint.a > 1f ||
                _maxConcurrent < 0 || _maxConcurrent > 4096 ||
                _maxUnitsPerChange < 0 || _maxUnitsPerChange > 4096)
            {
                command = null;
                error = "Ballistic motion needs finite velocities and gravity, 0-1 tint, nonnegative scatter, spin and drag, and limits between 0 and 4096.";
                return false;
            }
            command = new Command(_initialVelocity, _gravity, _scatter, _spin,
                _drag, _shrinkStart, _tint, _maxConcurrent, _maxUnitsPerChange);
            error = null;
            return true;
        }

        static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        static bool FiniteNonnegative(float v) => v >= 0f && Finite(v);

        sealed class Command : ITextStudioReactionCommand, ITextStudioReactionConcurrencyCommand,
            ITextStudioReactionChangeLimitCommand
        {
            readonly Vector3 _velocity, _gravity;
            readonly float _scatter, _spin, _drag, _shrinkStart;
            readonly Color32 _tint;
            public int MaxConcurrent { get; }
            public int MaxUnitsPerChange { get; }

            internal Command(Vector3 velocity, Vector3 gravity, float scatter, float spin,
                float drag, float shrinkStart, Color tint, int maxConcurrent,
                int maxUnitsPerChange)
            {
                _velocity = velocity;
                _gravity = gravity;
                _scatter = scatter;
                _spin = spin;
                _drag = drag;
                _shrinkStart = shrinkStart;
                _tint = tint;
                MaxConcurrent = maxConcurrent;
                MaxUnitsPerChange = maxUnitsPerChange;
            }

            public TextStudioReactionActionRequirements Requirements =>
                TextStudioReactionActionRequirements.RetainedGeometry;
            public TextStudioReactionActionEvents SupportedEvents => TextStudioReactionActionEvents.None;
            public TextStudioReactionPreviewCapability PreviewCapability =>
                TextStudioReactionPreviewCapability.DeterministicSeek;
            public TextStudioReactionCaptureIntent CaptureIntent => TextStudioReactionCaptureIntent.Copy;
            public TextStudioReactionResourceClass CaptureResources =>
                TextStudioReactionResourceClass.GlyphGeometry |
                TextStudioReactionResourceClass.MaterialState;
            public ITextStudioReactionActionState CreateState() =>
                new State(_velocity, _gravity, _scatter, _spin, _drag,
                    _shrinkStart, _tint);
        }

        sealed class State : ITextStudioReactionActionState
        {
            readonly Vector3 _velocity, _gravity;
            readonly float _scatter, _spin, _drag, _shrinkStart;
            readonly Color32 _tint;
            TextStudioReactionLease _lease;
            ITextStudioReactionActionHost _host;
            Vector3 _launchVelocity, _angularVelocity;

            internal State(Vector3 velocity, Vector3 gravity, float scatter, float spin,
                float drag, float shrinkStart, Color32 tint)
            {
                _velocity = velocity;
                _gravity = gravity;
                _scatter = scatter;
                _spin = spin;
                _drag = drag;
                _shrinkStart = shrinkStart;
                _tint = tint;
            }

            public void Start(in TextStudioReactionContext context,
                ITextStudioReactionActionHost host, in TextStudioReactionLease lease)
            {
                _lease = lease;
                _host = host;
                int unit = context.UnitIndex;
                int variation = context.RandomSeed == 0
                    ? unit
                    : unchecked((int)(context.RandomSeed ^
                        ((uint)(unit + 1) * 0x9E3779B9u)));
                _launchVelocity = _velocity + _scatter * new Vector3(
                    TextStudioDeterministicHash.SignedValue(variation, 19),
                    TextStudioDeterministicHash.SignedValue(variation, 43),
                    TextStudioDeterministicHash.SignedValue(variation, 79));
                _angularVelocity = _spin * new Vector3(
                    TextStudioDeterministicHash.SignedValue(variation, 103),
                    TextStudioDeterministicHash.SignedValue(variation, 139),
                    TextStudioDeterministicHash.SignedValue(variation, 181));
            }

            public TextStudioReactionActionSample Sample(in TextStudioReactionContext context,
                float progress, double hostTime)
            {
                float normalized = Mathf.Clamp01(progress);
                float time = normalized * context.ResponseDuration;
                TextStudioMotionPolicy policy = TextStudioMotionSettings.Resolve(
                    _host != null ? _host.MotionPolicy : TextStudioMotionPolicy.Inherit);
                float amplitude = Mathf.Clamp01(context.Intensity) *
                    (policy == TextStudioMotionPolicy.Full ? 1f :
                        policy == TextStudioMotionPolicy.Reduced ? .35f : 0f);
                float dampingIntegral;
                float gravityIntegral;
                float dampingTime = _drag * time;
                if (dampingTime < .02f)
                {
                    dampingIntegral = time * (1f + dampingTime *
                        (-.5f + dampingTime * (1f / 6f - dampingTime / 24f)));
                    gravityIntegral = time * time * (.5f + dampingTime *
                        (-1f / 6f + dampingTime * (1f / 24f - dampingTime / 120f)));
                }
                else
                {
                    float decay = Mathf.Exp(-_drag * time);
                    dampingIntegral = (1f - decay) / _drag;
                    gravityIntegral = (time - dampingIntegral) / _drag;
                }
                Vector3 position = amplitude *
                    (_launchVelocity * dampingIntegral + _gravity * gravityIntegral);
                float scale = normalized <= _shrinkStart || _shrinkStart >= 1f
                    ? 1f
                    : 1f - (normalized - _shrinkStart) / (1f - _shrinkStart);
                if (policy == TextStudioMotionPolicy.Reduced)
                    scale = Mathf.LerpUnclamped(1f, scale, .35f);
                TextStudioReactionVisualSample sample = new TextStudioReactionVisualSample(
                    position, Quaternion.Euler(_angularVelocity * (time * amplitude)),
                    Vector3.one * scale, policy == TextStudioMotionPolicy.None ? 0f : 1f,
                    _tint);
                if (!_lease.TrySubmit(in sample))
                    throw new System.InvalidOperationException(
                        "The retained visual output is no longer available.");
                return default;
            }

            public void Complete(in TextStudioReactionContext context) => Reset();
            public void Cancel(in TextStudioReactionContext context,
                TextStudioReactionCancelReason reason) => Reset();
            public void Dispose() => Reset();

            void Reset()
            {
                _lease = default;
                _host = null;
            }
        }
    }
}
