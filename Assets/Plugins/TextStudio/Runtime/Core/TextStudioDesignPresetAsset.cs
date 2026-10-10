using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Reusable one-click design recipe that assigns established Text Studio appearance, writing,
    /// and behavior assets without adding another runtime ownership layer to the text component.
    /// </summary>
    [CreateAssetMenu(
        fileName = "TextStudioDesignPreset",
        menuName = "Text Studio/Design Preset", order = 100)]
    public sealed class TextStudioDesignPresetAsset : ScriptableObject
    {
        [SerializeField] string _displayName = "Text Studio Design";
        [SerializeField, TextArea(2, 4)] string _description = string.Empty;
        [SerializeField, TextArea(1, 3)] string _sampleText = "Text Studio";
        [SerializeField] TextStudio2DStyleAsset _appearanceStyle;
        [SerializeField] TextStudioBehaviorProfile _behaviorProfile;

        public string DisplayName => string.IsNullOrWhiteSpace(_displayName)
            ? name
            : _displayName.Trim();
        public string Description => _description ?? string.Empty;
        public string SampleText => string.IsNullOrWhiteSpace(_sampleText)
            ? "Text Studio"
            : _sampleText;
        public TextStudio2DStyleAsset AppearanceStyle => _appearanceStyle;
        public TextStudioWritingStyleAsset WritingStyle => _behaviorProfile != null ? _behaviorProfile.BehaviorSettings.Typewriter.Resolve().WritingStyle : null;
        public TextStudioBehaviorProfile BehaviorProfile => _behaviorProfile;

        /// <summary>
        /// Assigns the recipe's reusable assets to an existing component. Source text, inline tags,
        /// tag stacks, placement, renderer selection, and component-local settings remain untouched.
        /// Backends that own glyph styling retain the Appearance assignment without rendering it.
        /// </summary>
        public void ApplyTo(TextStudioText target)
        {
            if (target == null)
                return;

            target.SetAppearanceStyle(_appearanceStyle);
            target.SetBehaviorProfile(_behaviorProfile);
        }

        /// <summary>Replaces the complete authored recipe definition.</summary>
        public void Configure(
            string displayName,
            string description,
            string sampleText,
            TextStudio2DStyleAsset appearanceStyle,
            TextStudioBehaviorProfile behaviorProfile)
        {
            _displayName = displayName ?? string.Empty;
            _description = description ?? string.Empty;
            _sampleText = sampleText ?? string.Empty;
            _appearanceStyle = appearanceStyle;
            _behaviorProfile = behaviorProfile;
            Normalize();
        }

        void OnValidate()
        {
            Normalize();
        }

        void Normalize()
        {
            _displayName = _displayName?.Trim() ?? string.Empty;
            _description ??= string.Empty;
            _sampleText ??= string.Empty;
        }
    }
}
