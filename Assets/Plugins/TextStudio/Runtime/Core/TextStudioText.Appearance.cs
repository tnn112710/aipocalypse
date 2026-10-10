using System;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        internal uint AppearanceAuthoringRevision { get; private set; }

        public float InlineColorAlphaThreshold
        {
            get => float.IsFinite(_inlineColorAlphaThreshold) ? UnityEngine.Mathf.Clamp01(_inlineColorAlphaThreshold) : .5f;
            set
            {
                if (!float.IsFinite(value) || value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(value));
                if (_inlineColorAlphaThreshold.Equals(value)) return;
                _inlineColorAlphaThreshold = value;
                _runtime.Pipeline.Invalidate();
                _runtime.SetDirty(DirtyFlags.Layout | DirtyFlags.AppearanceTopology);
                FlushVisualStateImmediate();
            }
        }

        /// <summary>Selects a live reusable style or component-owned settings. Empty Inline preserves native appearance.</summary>
        public TextStudioAppearanceSource AppearanceSource
        {
            get => _appearanceSource;
            set
            {
                if ((uint)value > (uint)TextStudioAppearanceSource.Style)
                    throw new ArgumentOutOfRangeException(nameof(value));
                if (_appearanceSource == value)
                    return;
                _appearanceSource = value;
                unchecked { AppearanceAuthoringRevision++; }
                EnsureAppearanceStyleCurrent(true);
                FlushVisualStateImmediate();
            }
        }

        /// <summary>Returns an independent copy of the retained inline settings, including an empty default stack.</summary>
        public TextStudio2DStyleSettings InlineAppearanceSettings => _inlineAppearanceSettings.Clone();

        /// <summary>Copies appearance settings into this component and selects Inline. An explicit BaseStyle remains a live reference.</summary>
        public void ApplyAppearanceSettings(TextStudio2DStyleSettings settings)
        {
            settings.Validate();
            _inlineAppearanceSettings = settings;
            _appearanceSource = TextStudioAppearanceSource.Inline;
            unchecked { AppearanceAuthoringRevision++; }
            EnsureAppearanceStyleCurrent(true);
            FlushVisualStateImmediate();
        }
    }
}
