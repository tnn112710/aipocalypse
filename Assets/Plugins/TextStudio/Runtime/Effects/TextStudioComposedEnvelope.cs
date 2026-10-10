using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Immutable binding-time chain for Existing Effect layer envelopes. Each authored layer is
    /// sampled once per leaf evaluation and nested envelope influences multiply.
    /// </summary>
    internal sealed class TextStudioComposedEnvelopeStack
    {
        readonly TextStudioComposedEnvelopeStack _parent;
        readonly TextStudioCompositionLayer _layer;

        TextStudioComposedEnvelopeStack(
            TextStudioComposedEnvelopeStack parent,
            TextStudioCompositionLayer layer)
        {
            _parent = parent;
            _layer = layer;
        }

        internal static TextStudioComposedEnvelopeStack Append(
            TextStudioComposedEnvelopeStack parent,
            TextStudioCompositionLayer layer)
        {
            return layer == null ? parent : new TextStudioComposedEnvelopeStack(parent, layer);
        }

        internal bool IsConstantFullEnvelope =>
            (_parent == null || _parent.IsConstantFullEnvelope) &&
            (_layer == null || _layer.HasConstantFullEnvelope);

        internal float Evaluate(in EffectEvaluationContext context)
        {
            float influence = _parent != null ? _parent.Evaluate(in context) : 1f;
            if (influence <= 0f || _layer == null)
                return 0f;
            return Mathf.Clamp01(influence * _layer.EvaluateBoundEnvelope(in context));
        }

        internal float EvaluateProgress(
            in EffectEvaluationContext context,
            out float progress)
        {
            float influence = _parent != null ? _parent.Evaluate(in context) : 1f;
            progress = _layer != null ? _layer.EvaluateBoundProgress(in context) : 0f;
            return Mathf.Clamp01(influence);
        }
    }
}
