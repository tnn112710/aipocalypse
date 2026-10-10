using System;
using System.Collections.Generic;
using System.Text;

namespace TextStudio
{
    /// <summary>An owned effect layer. Dispose it to remove only this layer from the text.</summary>
    public sealed class TextStudioEffectLayer : IDisposable
    {
        TextStudioText _owner;

        internal TextStudioEffectLayer(TextStudioText owner, string expression)
        {
            _owner = owner;
            Expression = Normalize(expression);
        }

        public string Expression { get; private set; }
        public bool IsDisposed => _owner == null;

        /// <summary>Replaces this layer's invocations without changing markup or behavior profiles.</summary>
        public void SetEffects(string expression)
        {
            if (IsDisposed) throw new ObjectDisposedException(nameof(TextStudioEffectLayer));
            string normalized = Normalize(expression);
            if (string.Equals(Expression, normalized, StringComparison.Ordinal)) return;
            Expression = normalized;
            _owner.RefreshEffectLayers();
        }

        /// <summary>Removes this external effect layer from its owner and detaches the handle.</summary>
        public void Dispose()
        {
            TextStudioText owner = _owner;
            _owner = null;
            if (owner != null) owner.RemoveEffectLayer(this);
        }

        internal void Detach() => _owner = null;

        static string Normalize(string expression)
        {
            List<string> invocations = new List<string>();
            if (!TextStudioTransitionStyleUtility.TrySplitInvocations(expression, invocations, out string error))
                throw new ArgumentException(error, nameof(expression));
            StringBuilder result = new StringBuilder();
            for (int index = 0; index < invocations.Count; index++)
            {
                string invocation = invocations[index];
                if (invocation.StartsWith("<", StringComparison.Ordinal)) result.Append(invocation);
                else result.Append('<').Append(invocation).Append('>');
            }
            return result.ToString();
        }
    }

    public sealed partial class TextStudioText
    {
        readonly List<TextStudioEffectLayer> _effectLayers = new List<TextStudioEffectLayer>();
        string _supplementalEffects = string.Empty;

        /// <summary>A snapshot of active owned layers, suitable for an isolated preview or capture.</summary>
        public string SupplementalEffects => _supplementalEffects;

        /// <summary>Adds whole-text effects after authored fallback rules. The caller owns the returned layer.</summary>
        public TextStudioEffectLayer RegisterEffectLayer(string expression)
        {
            TextStudioEffectLayer layer = new TextStudioEffectLayer(this, expression);
            _effectLayers.Add(layer);
            RefreshEffectLayers();
            return layer;
        }

        internal void RemoveEffectLayer(TextStudioEffectLayer layer)
        {
            if (_effectLayers.Remove(layer)) RefreshEffectLayers();
        }

        internal void RefreshEffectLayers()
        {
            StringBuilder expression = new StringBuilder();
            for (int index = 0; index < _effectLayers.Count; index++)
                expression.Append(_effectLayers[index].Expression);
            _supplementalEffects = expression.ToString();
            _runtime.ApplySupplementalEffects(_supplementalEffects);
        }

        void OnDestroy()
        {
            for (int index = 0; index < _effectLayers.Count; index++) _effectLayers[index].Detach();
            _effectLayers.Clear();
            _supplementalEffects = string.Empty;
        }
    }
}
