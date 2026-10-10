using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio
{
    public sealed partial class TextStudioText
    {
        [SerializeField] TextStudioFontAppearanceData[] _appearanceFonts = Array.Empty<TextStudioFontAppearanceData>();

        public int AppearanceFontCount => _appearanceFonts?.Length ?? 0;
        /// <summary>Returns the prepared font appearance data at the specified binding index.</summary>
        public TextStudioFontAppearanceData GetAppearanceFont(int index) => _appearanceFonts[index];

        /// <summary>Assigns prepared data for one native font while preserving independent fallback font bindings.</summary>
        public void SetAppearanceFont(TextStudioFontAppearanceData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (!data.Validate(out string error)) throw new ArgumentException(error, nameof(data));
            for (int index = 0; index < AppearanceFontCount; index++)
            {
                if (_appearanceFonts[index] != null &&
                    (data.Font != null && _appearanceFonts[index].Font == data.Font || data.Font == null &&
                        _appearanceFonts[index].Font == null && _appearanceFonts[index].SourceHash == data.SourceHash && _appearanceFonts[index].FaceIndex == data.FaceIndex))
                {
                    _appearanceFonts[index] = data;
                    ScheduleTopologyRebuild();
                    return;
                }
            }
            int count = AppearanceFontCount;
            Array.Resize(ref _appearanceFonts, count + 1);
            _appearanceFonts[count] = data;
            ScheduleTopologyRebuild();
        }

        /// <summary>Finds current prepared appearance data for the supplied native font object, or returns null when none matches.</summary>
        public TextStudioFontAppearanceData FindAppearanceFont(Object font)
        {
            if (font == null) return null;
            for (int index = 0; index < AppearanceFontCount; index++)
                if (_appearanceFonts[index] != null && _appearanceFonts[index].Font == font && _appearanceFonts[index].IsSourceCurrent) return _appearanceFonts[index];
            return null;
        }

        /// <summary>Resolves a detached or system font by immutable source content and collection face.</summary>
        public TextStudioFontAppearanceData FindAppearanceFont(string sourceHash, int faceIndex)
        {
            if (string.IsNullOrEmpty(sourceHash)) return null;
            for (int index = 0; index < AppearanceFontCount; index++)
                if (_appearanceFonts[index] != null && _appearanceFonts[index].SourceHash == sourceHash && _appearanceFonts[index].FaceIndex == faceIndex && _appearanceFonts[index].IsSourceCurrent)
                    return _appearanceFonts[index];
            return null;
        }
    }
}
