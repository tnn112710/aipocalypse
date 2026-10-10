using UnityEngine;

namespace TextStudio
{
    public enum TextStudioEmissionMergeMode
    {
        None,
        Sum,
        Replace
    }

    /// <summary>
    /// Reusable presentation and lifecycle policy for temporary text.
    /// Gameplay meaning remains in the asset name and caller, not the runtime engine.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Text Studio Emission Preset",
        menuName = "Text Studio/Temporary Text/Emission Preset",
        order = 106)]
    public sealed class TextStudioEmissionPreset : ScriptableObject
    {
        const string ValueToken = "{value}";

        [SerializeField, Tooltip("Optional complete host prefab. Empty uses the emitter's default host.")]
        GameObject _hostPrefabOverride;
        [SerializeField, Tooltip("Optional renderer-neutral behavior profile applied while this emission is active.")]
        TextStudioBehaviorProfile _behaviorProfile;
        [SerializeField, Tooltip("Markup containing exactly one {value} placeholder.")]
        string _numericTemplate = ValueToken;
        [SerializeField, Tooltip("Shared grouping, decimal, sign, suffix, compact, and culture settings.")]
        TextStudioNumberFormat _numberFormat = default;
        [SerializeField, Tooltip("Keep the emission active until its handle is dismissed.")]
        bool _manualDismissal;
        [SerializeField, Min(0f), Tooltip("Seconds in the Active phase after reveal completes.")]
        float _activeDuration = 1f;
        [SerializeField, Tooltip("How matching emissions combine within the merge window.")]
        TextStudioEmissionMergeMode _mergeMode;
        [SerializeField, Min(0f), Tooltip("Seconds after an emission during which a matching request may merge.")]
        float _mergeWindow = 0.15f;
        [SerializeField, Tooltip("Restart the existing profile effects when a request merges.")]
        bool _restartEffectsOnMerge = true;

        public GameObject HostPrefabOverride
        {
            get => _hostPrefabOverride;
            set => _hostPrefabOverride = value;
        }

        public TextStudioBehaviorProfile BehaviorProfile
        {
            get => _behaviorProfile;
            set => _behaviorProfile = value;
        }

        public string NumericTemplate
        {
            get => string.IsNullOrEmpty(_numericTemplate) ? ValueToken : _numericTemplate;
            set => _numericTemplate = string.IsNullOrEmpty(value) ? ValueToken : value;
        }

        public TextStudioNumberFormat NumberFormat
        {
            get => _numberFormat.Normalized();
            set => _numberFormat = value.Normalized();
        }

        public bool ManualDismissal
        {
            get => _manualDismissal;
            set => _manualDismissal = value;
        }

        public float ActiveDuration
        {
            get => Mathf.Max(0f, _activeDuration);
            set => _activeDuration = Mathf.Max(0f, value);
        }

        public TextStudioEmissionMergeMode MergeMode
        {
            get => _mergeMode;
            set => _mergeMode = value;
        }

        public float MergeWindow
        {
            get => Mathf.Max(0f, _mergeWindow);
            set => _mergeWindow = Mathf.Max(0f, value);
        }

        public bool RestartEffectsOnMerge
        {
            get => _restartEffectsOnMerge;
            set => _restartEffectsOnMerge = value;
        }

        public bool HasValidNumericTemplate
        {
            get => IsValidNumericTemplate(NumericTemplate);
        }

        /// <summary>Formats a number and substitutes it into the preset's single <c>{value}</c> token.</summary>
        public string FormatValue(double value)
        {
            string formatted = _numberFormat.Format(value);
            string template = NumericTemplate;
            if (string.Equals(
                    template,
                    ValueToken,
                    System.StringComparison.Ordinal))
            {
                return formatted;
            }
            if (!IsValidNumericTemplate(template))
                return formatted;
            return template.Replace(ValueToken, formatted);
        }

        static bool IsValidNumericTemplate(string template)
        {
            int first = template.IndexOf(
                ValueToken,
                System.StringComparison.Ordinal);
            return first >= 0 &&
                   first == template.LastIndexOf(
                       ValueToken,
                       System.StringComparison.Ordinal);
        }

        void OnValidate()
        {
            _activeDuration = Mathf.Max(0f, _activeDuration);
            _mergeWindow = Mathf.Max(0f, _mergeWindow);
            _numericTemplate ??= ValueToken;
            _numberFormat = _numberFormat.Normalized();
        }
    }
}
