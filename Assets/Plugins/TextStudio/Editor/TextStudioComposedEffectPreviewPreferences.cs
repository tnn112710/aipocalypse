using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    [FilePath(
        "UserSettings/TextStudioComposedEffectPreviewPreferences.asset",
        FilePathAttribute.Location.ProjectFolder)]
    internal sealed class TextStudioComposedEffectPreviewPreferences :
        ScriptableSingleton<TextStudioComposedEffectPreviewPreferences>
    {
        const TextStudioPreviewSegment DefaultSegments = TextStudioPreviewSegment.Active;

        [SerializeField] int _segments = (int)DefaultSegments;

        internal static TextStudioPreviewSegment Segments
        {
            get => Normalize((TextStudioPreviewSegment)instance._segments);
            set
            {
                TextStudioPreviewSegment normalized = Normalize(value);
                if (instance._segments == (int)normalized)
                    return;

                instance._segments = (int)normalized;
                instance.Save(true);
            }
        }

        static TextStudioPreviewSegment Normalize(TextStudioPreviewSegment segments)
        {
            TextStudioPreviewSegment normalized = segments & TextStudioPreviewSegment.All;
            return normalized == TextStudioPreviewSegment.None
                ? DefaultSegments
                : normalized;
        }
    }
}
