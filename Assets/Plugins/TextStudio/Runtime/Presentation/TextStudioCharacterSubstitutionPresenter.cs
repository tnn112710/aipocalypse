using System;

namespace TextStudio
{
    internal sealed class TextStudioCharacterSubstitutionPresenter
    {
        RuntimeBinding[] _runtimeBindings;
        RuntimeBinding[] _previousBindings;
        int _runtimeBindingCount;
        bool _hasActiveSubstitutions;
        bool _hasPendingCompletion;

        public bool HasActiveSubstitutions => _hasActiveSubstitutions;
        public bool HasPendingCompletion => _hasPendingCompletion;

        struct RuntimeBinding
        {
            public int BindingIndex;
            public CharacterSubstitutionBinding Binding;
            public float TimeOffset;
            public float LastTime;
            public bool RequiresCompletion;
            public ITextStudioCharacterSubstitutionRuntime Runtime;
        }

        public void OnTextChanged(TextStudioCompiledPlan plan, TextStudioGlyphChangeSet changes = default)
        {
            int previousCount = _runtimeBindingCount;
            (_previousBindings, _runtimeBindings) = (_runtimeBindings, _previousBindings);
            if (_runtimeBindings != null)
                Array.Clear(_runtimeBindings, 0, _runtimeBindings.Length);
            _runtimeBindingCount = 0;
            _hasActiveSubstitutions = false;
            _hasPendingCompletion = false;

            if (plan == null ||
                plan.CharacterSubstitutionBindings == null ||
                plan.CharacterSubstitutionBindingCount <= 0)
            {
                return;
            }

            EnsureCapacity(plan.CharacterSubstitutionBindingCount);
            for (int i = 0; i < plan.CharacterSubstitutionBindingCount; i++)
            {
                CharacterSubstitutionBinding binding = plan.CharacterSubstitutionBindings[i];
                if (!TextStudioRegistry.CharacterSubstitutions.TryGetQuiet(
                        binding.effectId,
                        out ITextStudioCharacterSubstitution substitution) ||
                    substitution == null)
                {
                    continue;
                }

                ITextStudioCharacterSubstitutionRuntime runtime = substitution.CreateRuntime();
                if (runtime == null)
                    continue;

                runtime.OnTextChanged(new TextStudioPlanView(plan));
                bool requiresCompletion = substitution.GetCompletionDuration(new TextStudioPlanView(plan), in binding, null) > 0f;
                _hasPendingCompletion |= requiresCompletion;
                _runtimeBindings[_runtimeBindingCount++] = new RuntimeBinding
                {
                    BindingIndex = i,
                    Binding = binding,
                    TimeOffset = FindRetainedTime(in binding, in changes, previousCount),
                    RequiresCompletion = requiresCompletion,
                    Runtime = runtime
                };
            }

            _hasActiveSubstitutions = _runtimeBindingCount > 0;
        }

        float FindRetainedTime(in CharacterSubstitutionBinding binding, in TextStudioGlyphChangeSet changes, int previousCount)
        {
            if (!changes.IsDefined || _previousBindings == null) return 0f;
            for (int i = 0; i < previousCount; i++)
            {
                RuntimeBinding previous = _previousBindings[i];
                CharacterSubstitutionBinding old = previous.Binding;
                if (binding.effectId != old.effectId || binding.glyphCount != old.glyphCount ||
                    !SameParameters(binding.parameters, old.parameters)) continue;
                bool retained = true;
                for (int glyph = 0; glyph < binding.glyphCount; glyph++)
                {
                    if (changes.GetOutgoingGlyphForIncoming(binding.glyphStart + glyph) == old.glyphStart + glyph) continue;
                    retained = false;
                    break;
                }
                if (retained) return previous.LastTime;
            }
            return 0f;
        }

        static bool SameParameters(TextStudioTagParameters a, TextStudioTagParameters b)
        {
            if (a.PositionalCount != b.PositionalCount || a.NamedCount != b.NamedCount) return false;
            for (int i = 0; i < a.PositionalCount; i++)
                if (a.Positional[i] != b.Positional[i]) return false;
            for (int i = 0; i < a.NamedCount; i++)
                if (!a.Named[i].Equals(b.Named[i])) return false;
            return true;
        }

        public bool Update(
            TextStudioCompiledPlan plan,
            PresentationGlyph[] glyphs,
            int glyphCount,
            float deltaTime,
            float time,
            bool isDiscontinuous,
            ITextStudioRevealState reveal,
            TextStudioMotionPolicy motionPolicy = TextStudioMotionPolicy.Inherit)
        {
            if (plan == null ||
                glyphs == null ||
                glyphCount <= 0 ||
                _runtimeBindingCount <= 0 ||
                plan.CharacterSubstitutionBindings == null)
            {
                _hasActiveSubstitutions = false;
                _hasPendingCompletion = false;
                return false;
            }

            bool changed = false;
            if (TextStudioMotionSettings.Resolve(motionPolicy) != TextStudioMotionPolicy.Full)
            {
                for (int i = 0; i < glyphCount; i++)
                {
                    ref PresentationGlyph glyph = ref glyphs[i];
                    if (!glyph.HasFlag(PresentationFlags.Substituted))
                        continue;
                    glyph.glyphId = plan.GetPrimaryCodePoint(i);
                    glyph.flags = (glyph.flags & ~PresentationFlags.Substituted) | PresentationFlags.Dirty;
                    glyph.substitutionState = SubstitutionState.Resolved;
                    glyph.substitutionProgress = 1f;
                    changed = true;
                }
                _hasActiveSubstitutions = false;
                _hasPendingCompletion = false;
                return changed;
            }
            bool anyActive = false;
            bool pendingCompletion = false;
            int bindingCount = plan.CharacterSubstitutionBindingCount;
            for (int i = 0; i < _runtimeBindingCount; i++)
            {
                ref RuntimeBinding runtimeBinding = ref _runtimeBindings[i];
                if (runtimeBinding.Runtime == null ||
                    runtimeBinding.BindingIndex < 0 ||
                    runtimeBinding.BindingIndex >= bindingCount)
                {
                    continue;
                }

                CharacterSubstitutionBinding binding = plan.CharacterSubstitutionBindings[runtimeBinding.BindingIndex];
                runtimeBinding.LastTime = time + runtimeBinding.TimeOffset;
                TextStudioCharacterSubstitutionContext context = new TextStudioCharacterSubstitutionContext(
                    plan,
                    in binding,
                    glyphs,
                    glyphCount,
                    deltaTime,
                    runtimeBinding.LastTime,
                    isDiscontinuous,
                    reveal);
                TextStudioCharacterSubstitutionResult result = runtimeBinding.Runtime.Update(in context);
                changed |= result.Changed;
                anyActive |= result.HasActiveSubstitutions;
                pendingCompletion |= result.HasActiveSubstitutions && runtimeBinding.RequiresCompletion;
            }

            _hasActiveSubstitutions = anyActive;
            _hasPendingCompletion = pendingCompletion;
            return changed;
        }

        void EnsureCapacity(int count)
        {
            if (_runtimeBindings == null || _runtimeBindings.Length < count)
                _runtimeBindings = new RuntimeBinding[Math.Max(count, 4)];
        }
    }
}
