using UnityEngine;

namespace TextStudio
{
    /// <summary>A bounded, deterministic spark burst drawn by the active UI renderer.</summary>
    [CreateAssetMenu(fileName = "UI Spark Reaction",
        menuName = "Text Studio/Reactions/UI Spark Burst", order = 106)]
    public sealed class TextStudioReactionUISparkActionAsset : TextStudioReactionActionAsset
    {
        [SerializeField, Range(1, 32)] int _particleCount = 8;
        [SerializeField, Min(0f)] float _travel = 28f;
        [SerializeField, Min(0f)] float _size = 4f;
        [SerializeField] Color _color = new Color(1f, .78f, .38f, 1f);
        [SerializeField, Tooltip("Capture the glyph position when the event fires, so delayed hide and outgoing bursts stay on the old glyph.")]
        bool _captureEventAnchor;
        [SerializeField, Range(1, 32), Tooltip("Maximum simultaneous bursts from this action, shared by every layer that references it.")]
        int _maxConcurrent = 32;

        public int ParticleCount { get => _particleCount; set { _particleCount = value; NotifyChanged(); } }
        public float Travel { get => _travel; set { _travel = value; NotifyChanged(); } }
        public float Size { get => _size; set { _size = value; NotifyChanged(); } }
        public Color Color { get => _color; set { _color = value; NotifyChanged(); } }
        public bool CaptureEventAnchor { get => _captureEventAnchor; set { _captureEventAnchor = value; NotifyChanged(); } }
        public int MaxConcurrent { get => _maxConcurrent; set { _maxConcurrent = value; NotifyChanged(); } }

        public override bool TryCompile(out ITextStudioReactionCommand command, out string error)
        {
            if (_particleCount < 1 || _particleCount > 32 ||
                _maxConcurrent < 1 || _maxConcurrent > 32 ||
                !FiniteNonnegative(_travel) || !FiniteNonnegative(_size) ||
                !FiniteUnit(_color.r) || !FiniteUnit(_color.g) ||
                !FiniteUnit(_color.b) || !FiniteUnit(_color.a))
            {
                command = null;
                error = "UI sparks need 1-32 particles, 1-32 simultaneous bursts, finite nonnegative travel and size, and a 0-1 color.";
                return false;
            }
            command = new Command(_particleCount, _travel, _size, (Color32)_color,
                _captureEventAnchor, _maxConcurrent);
            error = null;
            return true;
        }

        static bool FiniteNonnegative(float value) =>
            value >= 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        static bool FiniteUnit(float value) =>
            value >= 0f && value <= 1f && !float.IsNaN(value) && !float.IsInfinity(value);

        sealed class Command : ITextStudioReactionCommand, ITextStudioReactionStartPolicy,
            ITextStudioReactionTriggerAnchorCommand, ITextStudioReactionConcurrencyCommand
        {
            readonly int _count;
            readonly float _travel, _size;
            readonly Color32 _color;
            readonly bool _captureEventAnchor;

            internal Command(int count, float travel, float size, Color32 color,
                bool captureEventAnchor, int maxConcurrent)
            {
                _count = count;
                _travel = travel;
                _size = size;
                _color = color;
                _captureEventAnchor = captureEventAnchor;
                MaxConcurrent = maxConcurrent;
            }

            public TextStudioReactionActionRequirements Requirements =>
                TextStudioReactionActionRequirements.LiveAnchor |
                TextStudioReactionActionRequirements.UIParticles;
            public TextStudioReactionActionEvents SupportedEvents => TextStudioReactionActionEvents.None;
            public TextStudioReactionPreviewCapability PreviewCapability =>
                TextStudioReactionPreviewCapability.PlayModeOnly;
            public TextStudioReactionCaptureIntent CaptureIntent => TextStudioReactionCaptureIntent.Copy;
            public TextStudioReactionResourceClass CaptureResources => TextStudioReactionResourceClass.None;
            public bool CaptureTriggerAnchor => _captureEventAnchor;
            public int MaxConcurrent { get; }

            public bool CanStart(in TextStudioReactionContext context,
                ITextStudioReactionActionHost host) =>
                host != null && host.MotionPolicy != TextStudioMotionPolicy.None &&
                host is ITextStudioReactionUIParticleHost ui &&
                ui.CanAcquireReactionUIParticles(_count) &&
                (_captureEventAnchor ||
                 host.TryGetReactionAnchor(in context, out TextStudioReactionAnchor anchor) &&
                 (anchor.Space == TextStudioReactionAnchorSpace.Panel ||
                  anchor.Space == TextStudioReactionAnchorSpace.World));

            public ITextStudioReactionActionState CreateState() =>
                new State(_count, _travel, _size, _color, _captureEventAnchor);
        }

        sealed class State : ITextStudioReactionActionState
        {
            readonly Vector2[] _directions;
            readonly float _travel, _size;
            readonly Color32 _color;
            readonly bool _captureEventAnchor;
            TextStudioReactionUIParticleLease _lease;
            float _intensity;

            internal State(int count, float travel, float size, Color32 color,
                bool captureEventAnchor)
            {
                _directions = new Vector2[count];
                _travel = travel;
                _size = size;
                _color = color;
                _captureEventAnchor = captureEventAnchor;
            }

            public void Start(in TextStudioReactionContext context,
                ITextStudioReactionActionHost host, in TextStudioReactionLease lease)
            {
                _lease = default;
                if (!(host is ITextStudioReactionUIParticleHost ui)) return;
                TextStudioReactionAnchor anchor;
                bool anchored = _captureEventAnchor
                    ? lease.TryGetAnchor(out anchor)
                    : host.TryGetReactionAnchor(in context, out anchor);
                if (!anchored ||
                    (anchor.Space != TextStudioReactionAnchorSpace.Panel &&
                     anchor.Space != TextStudioReactionAnchorSpace.World) ||
                    !ui.TryAcquireReactionUIParticles(in anchor,
                        _directions.Length, out _lease)) return;
                _intensity = Mathf.Clamp01(context.Intensity) *
                    (host.MotionPolicy == TextStudioMotionPolicy.Reduced ? .35f : 1f);
                for (int i = 0; i < _directions.Length; i++)
                {
                    uint seed = context.RandomSeed ^ ((uint)(i + 1) * 0x9E3779B9u);
                    float jitter = TextStudioDeterministicHash.SignedValue(
                        unchecked((int)seed), 19) * .16f;
                    float angle = (i + jitter) * (Mathf.PI * 2f / _directions.Length);
                    _directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                }
            }

            public TextStudioReactionActionSample Sample(in TextStudioReactionContext context,
                float progress, double hostTime)
            {
                if (!_lease.IsValid) return default;
                float time = Mathf.Clamp01(progress);
                float fade = 1f - time;
                float travel = _travel * _intensity * (1f - fade * fade);
                float size = _size * _intensity * fade;
                Color32 color = _color;
                color.a = (byte)Mathf.RoundToInt(_color.a * fade * fade);
                for (int i = 0; i < _directions.Length; i++)
                {
                    TextStudioReactionUIParticle particle =
                        new TextStudioReactionUIParticle(_directions[i] * travel,
                            size * (i % 3 == 0 ? 1.2f : i % 3 == 1 ? .8f : 1f),
                            color);
                    _lease.TrySet(i, in particle);
                }
                _lease.Commit();
                return default;
            }

            public void Complete(in TextStudioReactionContext context) => Release();
            public void Cancel(in TextStudioReactionContext context,
                TextStudioReactionCancelReason reason) => Release();
            public void Dispose() => Release();

            void Release()
            {
                _lease.Release();
                _lease = default;
            }
        }
    }
}
