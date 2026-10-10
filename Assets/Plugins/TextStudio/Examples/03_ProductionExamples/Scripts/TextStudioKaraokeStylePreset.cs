using UnityEngine;

namespace TextStudio.Examples.Production
{
    public enum TextStudioKaraokeHighlightCoverage
    {
        CurrentWord,
        Progressive
    }

    public sealed class TextStudioKaraokeStylePreset : ScriptableObject
    {
        [SerializeField] string _displayName = "Karaoke Style";
        [SerializeField] string _buttonLabel = "Style";
        [SerializeField, TextArea(2, 4)] string _description = string.Empty;
        [SerializeField, TextArea(1, 3)] string _recipe = string.Empty;
        [SerializeField] TextStudioKaraokeHighlightCoverage _coverage;
        [SerializeField, Min(0f)] float _releaseDuration = 0.2f;
        [SerializeField] TextStudioEffectDefinitionAsset _activeEffect;
        [SerializeField] TextStudioEffectDefinitionAsset _completedEffect;
        [SerializeField] TextStudioEffectDefinitionAsset _reducedMotionActiveEffect;
        [SerializeField] TextStudioEffectDefinitionAsset _reducedMotionCompletedEffect;

        public string DisplayName => _displayName ?? string.Empty;
        public string ButtonLabel => _buttonLabel ?? string.Empty;
        public string Description => _description ?? string.Empty;
        public string Recipe => _recipe ?? string.Empty;
        public TextStudioKaraokeHighlightCoverage Coverage => _coverage;
        public float ReleaseDuration => Mathf.Max(0f, _releaseDuration);
        public TextStudioEffectDefinitionAsset ActiveEffect => _activeEffect;
        public TextStudioEffectDefinitionAsset CompletedEffect => _completedEffect;
        public TextStudioEffectDefinitionAsset ReducedMotionActiveEffect => _reducedMotionActiveEffect;
        public TextStudioEffectDefinitionAsset ReducedMotionCompletedEffect => _reducedMotionCompletedEffect;

        public TextStudioEffectDefinitionAsset ResolveActiveEffect(bool reducedMotion)
        {
            return reducedMotion && _reducedMotionActiveEffect != null
                ? _reducedMotionActiveEffect
                : _activeEffect;
        }

        public TextStudioEffectDefinitionAsset ResolveCompletedEffect(bool reducedMotion)
        {
            if (reducedMotion && _reducedMotionCompletedEffect != null)
                return _reducedMotionCompletedEffect;
            return _completedEffect;
        }

        public void Configure(
            string displayName,
            string buttonLabel,
            string description,
            string recipe,
            TextStudioKaraokeHighlightCoverage coverage,
            float releaseDuration,
            TextStudioEffectDefinitionAsset activeEffect,
            TextStudioEffectDefinitionAsset completedEffect,
            TextStudioEffectDefinitionAsset reducedMotionActiveEffect,
            TextStudioEffectDefinitionAsset reducedMotionCompletedEffect)
        {
            _displayName = displayName ?? string.Empty;
            _buttonLabel = buttonLabel ?? string.Empty;
            _description = description ?? string.Empty;
            _recipe = recipe ?? string.Empty;
            _coverage = coverage;
            _releaseDuration = Mathf.Max(0f, releaseDuration);
            _activeEffect = activeEffect;
            _completedEffect = completedEffect;
            _reducedMotionActiveEffect = reducedMotionActiveEffect;
            _reducedMotionCompletedEffect = reducedMotionCompletedEffect;
        }
    }
}
