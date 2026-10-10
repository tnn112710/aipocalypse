using System;
using System.Collections.Generic;

namespace TextStudio
{
    internal enum TextStudioEffectInvocationSourceKind : byte
    {
        DefaultAll = 0,
        DefaultReveal = 1,
        DefaultActive = 2,
        DefaultHide = 3,
        TypewriterReveal = 4,
        TypewriterHide = 5,
        Supplemental = 6
    }

#if UNITY_6000_7_OR_NEWER
    // The shared comparer or empty metadata has no Play Mode state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioCompiledEffectInvocationSource
    {
        static readonly TextStudioEffectInvocation[] EmptyInvocations =
            Array.Empty<TextStudioEffectInvocation>();

        readonly TextStudioEffectInvocation[] _invocations;

        TextStudioCompiledEffectInvocationSource(
            TextStudioEffectInvocationSourceKind kind,
            TextStudioPhaseMask phaseMask,
            string expression,
            TextStudioEffectInvocation[] invocations,
            ITextStudioEffectDefinition directEffect,
            string error)
        {
            Kind = kind;
            PhaseMask = phaseMask;
            Expression = expression ?? string.Empty;
            _invocations = invocations ?? EmptyInvocations;
            DirectEffect = directEffect;
            Error = error ?? string.Empty;
        }

        public TextStudioEffectInvocationSourceKind Kind { get; }
        public TextStudioPhaseMask PhaseMask { get; }
        public string Expression { get; }
        public string Error { get; }
        public ITextStudioEffectDefinition DirectEffect { get; }
        public bool HasDirectEffect => DirectEffect != null;
        public bool HasContent => HasDirectEffect || _invocations.Length > 0;
        public bool IsValid => Error.Length == 0;
        public int Count => _invocations.Length;

        public TextStudioEffectInvocation this[int index] => _invocations[index];

        public static TextStudioCompiledEffectInvocationSource Empty(
            TextStudioEffectInvocationSourceKind kind,
            TextStudioPhaseMask phaseMask)
        {
            return new TextStudioCompiledEffectInvocationSource(
                kind,
                phaseMask,
                string.Empty,
                EmptyInvocations,
                null,
                string.Empty);
        }

        public static TextStudioCompiledEffectInvocationSource CompileEffect(
            TextStudioEffectInvocationSourceKind kind,
            TextStudioPhaseMask phaseMask,
            ITextStudioEffectDefinition effect)
        {
            return new TextStudioCompiledEffectInvocationSource(
                kind,
                phaseMask,
                string.Empty,
                EmptyInvocations,
                effect,
                string.Empty);
        }

        public static TextStudioCompiledEffectInvocationSource Compile(
            TextStudioEffectInvocationSourceKind kind,
            TextStudioPhaseMask phaseMask,
            string expression,
            List<TextStudioEffectInvocation> scratch)
        {
            if (scratch == null)
                throw new ArgumentNullException(nameof(scratch));

            string normalizedExpression = expression ?? string.Empty;
            if (!TextStudioTransitionStyleUtility.TryParseInvocations(
                    normalizedExpression,
                    scratch,
                    out string error))
            {
                return new TextStudioCompiledEffectInvocationSource(
                    kind,
                    phaseMask,
                    normalizedExpression,
                    EmptyInvocations,
                    null,
                    error);
            }

            TextStudioEffectInvocation[] invocations;
            if (scratch.Count > 0)
            {
                invocations = scratch.ToArray();
                scratch.Clear();
            }
            else
            {
                invocations = EmptyInvocations;
            }

            return new TextStudioCompiledEffectInvocationSource(
                kind,
                phaseMask,
                normalizedExpression,
                invocations,
                null,
                string.Empty);
        }
    }
}
