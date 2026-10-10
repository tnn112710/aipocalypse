using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Reusable renderer-neutral behavior asset. Each enabled domain replaces the matching
    /// component-local fallback while all other host and renderer settings remain local.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Text Studio Behavior Profile",
        menuName = "Text Studio/Setup/Behavior Profile",
        order = 300)]
    public sealed class TextStudioBehaviorProfile : ScriptableObject
    {
        [SerializeField]
        TextStudioBehaviorDomain _domains =
            TextStudioBehaviorDomain.DefaultEffects |
            TextStudioBehaviorDomain.Typewriter;
        [SerializeField] TextStudioDefaultEffectMode _defaultEffectMode = TextStudioDefaultEffectMode.None;
        [SerializeField, TextArea(1, 3)] string _defaultEffects = string.Empty;
        [SerializeField, TextArea(1, 3)] string _defaultRevealEffects = string.Empty;
        [SerializeField, TextArea(1, 3)] string _defaultActiveEffects = string.Empty;
        [SerializeField, TextArea(1, 3)] string _defaultHideEffects = string.Empty;
        [SerializeField] TextStudioTypewriterRecipe _typewriter = TextStudioTypewriterRecipe.None;

        [SerializeField] TextStudioReactionRecipe _reactions = TextStudioReactionRecipe.None;
        [NonSerialized] uint _reactionSerializationRevision = 1;

        public TextStudioReactionRecipe Reactions => _reactions;
        internal uint ReactionSerializationRevision => _reactionSerializationRevision;
        public TextStudioBehaviorDomain Domains => _domains;
        /// <summary>Returns whether this profile supplies all requested behavior domains.</summary>
        public bool Provides(TextStudioBehaviorDomain domain) =>
            domain != TextStudioBehaviorDomain.None &&
            (_domains & domain) == domain;
        public TextStudioDefaultEffectMode DefaultEffectMode => _defaultEffectMode;
        public string DefaultEffects => _defaultEffects ?? string.Empty;
        public string DefaultRevealEffects => _defaultRevealEffects ?? string.Empty;
        public string DefaultActiveEffects => _defaultActiveEffects ?? string.Empty;
        public string DefaultHideEffects => _defaultHideEffects ?? string.Empty;
        public TextStudioDefaultEffectSettings DefaultEffectSettings
        {
            get
            {
                return new TextStudioDefaultEffectSettings
                {
                    Mode = _defaultEffectMode,
                    AllPhaseEffects = DefaultEffects,
                    RevealEffects = DefaultRevealEffects,
                    ActiveEffects = DefaultActiveEffects,
                    HideEffects = DefaultHideEffects
                };
            }
        }

        public TextStudioTypewriterRecipe Typewriter => _typewriter;
        public TextStudioTypewriterSource TypewriterSource => _typewriter.Source;
        public string TypewriterPresetId => _typewriter.BuiltInPresetId ?? string.Empty;
        public TextStudioTypewriterPreset TypewriterPreset => _typewriter.CustomPreset;
        public TextStudioBehaviorSettings BehaviorSettings
        {
            get
            {
                return new TextStudioBehaviorSettings
                {
                    Domains = _domains,
                    DefaultEffects = DefaultEffectSettings,
                    Typewriter = _typewriter,
                    Reactions = _reactions
                };
            }
        }

        /// <summary>Resolves and returns this profile's current typewriter settings.</summary>
        public TextStudioTypewriterSettings GetTypewriterSettings()
        {
            return _typewriter.Resolve();
        }

        /// <summary>Copies the enabled renderer-neutral behavior domains into this asset.</summary>
        public void ApplyBehaviorSettings(TextStudioBehaviorSettings settings)
        {
            _domains = settings.Domains;
            _defaultEffectMode = settings.DefaultEffects.Mode;
            _defaultEffects = settings.DefaultEffects.AllPhaseEffects ?? string.Empty;
            _defaultRevealEffects = settings.DefaultEffects.RevealEffects ?? string.Empty;
            _defaultActiveEffects = settings.DefaultEffects.ActiveEffects ?? string.Empty;
            _defaultHideEffects = settings.DefaultEffects.HideEffects ?? string.Empty;
            _typewriter = settings.Typewriter;
            _reactions = settings.Reactions;
            BumpReactionSerializationRevision();
            Normalize();
        }

        internal void InitializeForTests(
            TextStudioBehaviorDomain domains,
            TextStudioDefaultEffectMode defaultEffectMode,
            string defaultEffects = null,
            string reveal = null,
            string active = null,
            string hide = null)
        {
            _domains = domains;
            _defaultEffectMode = defaultEffectMode;
            _defaultEffects = defaultEffects ?? string.Empty;
            _defaultRevealEffects = reveal ?? string.Empty;
            _defaultActiveEffects = active ?? string.Empty;
            _defaultHideEffects = hide ?? string.Empty;
            BumpReactionSerializationRevision();
            Normalize();
        }

        internal void InitializeTypewriterForTests(
            TextStudioTypewriterSource source,
            string builtInPresetId = null,
            TextStudioTypewriterPreset customPreset = null,
            TextStudioTypewriterSettings? inlineSettings = null)
        {
            switch (source)
            {
                case TextStudioTypewriterSource.BuiltIn:
                    _typewriter = TextStudioTypewriterRecipe.BuiltIn(builtInPresetId);
                    break;
                case TextStudioTypewriterSource.Custom:
                    _typewriter = TextStudioTypewriterRecipe.Custom(customPreset);
                    break;
                case TextStudioTypewriterSource.Inline:
                    _typewriter = TextStudioTypewriterRecipe.Inline(inlineSettings ?? TextStudioTypewriterSettings.Default);
                    break;
                default:
                    _typewriter = TextStudioTypewriterRecipe.None;
                    break;
            }
            BumpReactionSerializationRevision();
        }

        void OnValidate()
        {
            BumpReactionSerializationRevision();
            Normalize();
        }

        void BumpReactionSerializationRevision()
        {
            unchecked { _reactionSerializationRevision++; }
            if (_reactionSerializationRevision == 0)
                _reactionSerializationRevision = 1;
        }

        void Normalize()
        {
            _domains &= TextStudioBehaviorDomain.DefaultEffects |
                        TextStudioBehaviorDomain.Typewriter |
                        TextStudioBehaviorDomain.Reactions;
            _defaultEffects ??= string.Empty;
            _defaultRevealEffects ??= string.Empty;
            _defaultActiveEffects ??= string.Empty;
            _defaultHideEffects ??= string.Empty;
            if (string.IsNullOrEmpty(_typewriter.BuiltInPresetId))
                _typewriter.BuiltInPresetId = BuiltIns.BuiltInTypewriterPresets.Default.Id;
            _typewriter.InlineSettings = TextStudioTypewriterRecipe.Inline(_typewriter.InlineSettings).InlineSettings;
            if (_reactions.Source == TextStudioReactionSource.BuiltIn &&
                string.IsNullOrEmpty(_reactions.BuiltInPresetId))
                _reactions.BuiltInPresetId = BuiltIns.BuiltInReactionPresets.Default.Id;
            if (_reactions.InlineLayers == null)
                _reactions.InlineLayers = System.Array.Empty<TextStudioReactionLayer>();
        }
    }
}
