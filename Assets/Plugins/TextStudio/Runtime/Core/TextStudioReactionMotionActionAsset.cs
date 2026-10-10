using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Analytic retained-unit motion; the capture provider owns the visible output.</summary>
    [CreateAssetMenu(fileName = "Tween Motion Reaction", menuName = "Text Studio/Reactions/Tween Retained Motion", order = 107)]
    public sealed class TextStudioReactionMotionActionAsset : TextStudioReactionActionAsset
    {
        [SerializeField, Min(0f)] float _dropDistance = 1f;
        [SerializeField] float _horizontalDrift;
        [SerializeField] float _rotationDegrees = 20f;
        [SerializeField, Min(0f)] float _endScale = .8f;
        [SerializeField, Range(0f, 1f)] float _endAlpha;

        public float DropDistance { get => _dropDistance; set { _dropDistance = value; NotifyChanged(); } }
        public float HorizontalDrift { get => _horizontalDrift; set { _horizontalDrift = value; NotifyChanged(); } }
        public float RotationDegrees { get => _rotationDegrees; set { _rotationDegrees = value; NotifyChanged(); } }
        public float EndScale { get => _endScale; set { _endScale = value; NotifyChanged(); } }
        public float EndAlpha { get => _endAlpha; set { _endAlpha = value; NotifyChanged(); } }

        public override bool TryCompile(out ITextStudioReactionCommand command, out string error)
        {
            if (!FiniteNonnegative(_dropDistance) || !Finite(_horizontalDrift) ||
                !Finite(_rotationDegrees) || !FiniteNonnegative(_endScale) ||
                !FiniteNonnegative(_endAlpha) || _endAlpha > 1f)
            {
                command = null;
                error = "Retained motion values must be finite and nonnegative where required.";
                return false;
            }
            command = new Command(_dropDistance, _horizontalDrift, _rotationDegrees,
                _endScale, _endAlpha);
            error = null;
            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool FiniteNonnegative(float value) => value >= 0f && Finite(value);

        sealed class Command : ITextStudioReactionCommand
        {
            readonly float _dropDistance, _horizontalDrift, _rotationDegrees, _endScale, _endAlpha;
            internal Command(float dropDistance, float horizontalDrift, float rotationDegrees,
                float endScale, float endAlpha)
            {
                _dropDistance = dropDistance;
                _horizontalDrift = horizontalDrift;
                _rotationDegrees = rotationDegrees;
                _endScale = endScale;
                _endAlpha = endAlpha;
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
            public ITextStudioReactionActionState CreateState() => new State(_dropDistance,
                _horizontalDrift, _rotationDegrees, _endScale, _endAlpha);
        }

        sealed class State : ITextStudioReactionActionState
        {
            readonly float _dropDistance, _horizontalDrift, _rotationDegrees, _endScale, _endAlpha;
            TextStudioReactionLease _lease;
            ITextStudioReactionActionHost _host;

            internal State(float dropDistance, float horizontalDrift, float rotationDegrees,
                float endScale, float endAlpha)
            {
                _dropDistance = dropDistance;
                _horizontalDrift = horizontalDrift;
                _rotationDegrees = rotationDegrees;
                _endScale = endScale;
                _endAlpha = endAlpha;
            }

            public void Start(in TextStudioReactionContext context,
                ITextStudioReactionActionHost host, in TextStudioReactionLease lease)
            {
                _lease = lease;
                _host = host;
            }

            public TextStudioReactionActionSample Sample(in TextStudioReactionContext context,
                float progress, double hostTime)
            {
                float t = Mathf.Clamp01(progress);
                TextStudioMotionPolicy policy = TextStudioMotionSettings.Resolve(
                    _host != null ? _host.MotionPolicy : TextStudioMotionPolicy.Inherit);
                float amplitude = Mathf.Clamp01(context.Intensity) *
                    (policy == TextStudioMotionPolicy.Full ? 1f :
                        policy == TextStudioMotionPolicy.Reduced ? .2f : 0f);
                float scale = Mathf.LerpUnclamped(1f, _endScale, t * amplitude);
                float alpha = policy == TextStudioMotionPolicy.None ? 0f :
                    Mathf.LerpUnclamped(1f, _endAlpha,
                        policy == TextStudioMotionPolicy.Reduced ? Mathf.Clamp01(t * 2f) : t);
                TextStudioReactionVisualSample sample = new TextStudioReactionVisualSample(
                    new Vector3(_horizontalDrift * t * amplitude,
                        -_dropDistance * t * t * amplitude, 0f),
                    Quaternion.Euler(0f, 0f, _rotationDegrees * t * amplitude),
                    new Vector3(scale, scale, scale),
                    alpha);
                if (!_lease.TrySubmit(in sample))
                    throw new InvalidOperationException("The retained visual output is no longer available.");
                return default;
            }

            public void Complete(in TextStudioReactionContext context)
            {
                _lease = default;
                _host = null;
            }
            public void Cancel(in TextStudioReactionContext context,
                TextStudioReactionCancelReason reason)
            {
                _lease = default;
                _host = null;
            }
            public void Dispose()
            {
                _lease = default;
                _host = null;
            }
        }
    }
}
