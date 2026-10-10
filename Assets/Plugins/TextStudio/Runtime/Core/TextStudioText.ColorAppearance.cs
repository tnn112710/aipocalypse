using System;
using UnityEngine;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        [SerializeField] TextStudioColorAppearanceData[] _appearanceImages = Array.Empty<TextStudioColorAppearanceData>();
        public int AppearanceImageCount => _appearanceImages?.Length ?? 0;
        /// <summary>Returns the prepared image appearance data at the specified binding index.</summary>
        public TextStudioColorAppearanceData GetAppearanceImages(int index) => _appearanceImages[index];
        /// <summary>Current native image sources, borrowed until the next text or layout update.</summary>
        public TextStudioReadOnlyBuffer<TextStudioColorSource> ColorSources => new TextStudioReadOnlyBuffer<TextStudioColorSource>(_layout.ColorSources, _layout.ColorSourceCount);

        /// <summary>Validates and registers prepared image appearance data, then schedules a topology rebuild even when already registered.</summary>
        public void AddAppearanceImages(TextStudioColorAppearanceData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!data.Validate(out string error)) throw new ArgumentException(error, nameof(data));
            for (int i = 0; i < AppearanceImageCount; i++)
                if (_appearanceImages[i] == data) { ScheduleTopologyRebuild(); return; }
            int count = AppearanceImageCount;
            Array.Resize(ref _appearanceImages, count + 1);
            _appearanceImages[count] = data;
            ScheduleTopologyRebuild();
        }

        internal TextStudioPreparedColorField FindAppearanceImage(TextStudioColorSource source)
        {
            for (int i = 0; i < AppearanceImageCount; i++)
            {
                TextStudioPreparedColorField field = _appearanceImages[i] != null ? _appearanceImages[i].Find(source) : null;
                if (field != null) return field;
            }
            return null;
        }
    }
}
