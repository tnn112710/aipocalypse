using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Immutable context describing the host and source of one markup preprocessing pass.</summary>
    public readonly struct TextStudioMarkupPreprocessContext
    {
        public readonly Object Owner;
        public readonly TextStudioText Target;
        public readonly string SourceId;
        public readonly bool IsAppend;

        /// <summary>Creates context for a replacement or append operation.</summary>
        public TextStudioMarkupPreprocessContext(
            Object owner,
            TextStudioText target,
            string sourceId,
            bool isAppend)
        {
            Owner = owner;
            Target = target;
            SourceId = sourceId ?? string.Empty;
            IsAppend = isAppend;
        }
    }

    /// <summary>Transforms authored markup before Text Studio parses tags and builds its semantic plan.</summary>
    public interface ITextStudioMarkupPreprocessor
    {
        /// <summary>Transforms one markup value without retaining the transient context.</summary>
        /// <param name="markup">Input markup from the previous preprocessor, or the original source.</param>
        /// <param name="context">Operation context valid only for the duration of the call.</param>
        /// <returns>Markup for the next stage. A null result is treated as empty text.</returns>
        string PreprocessMarkup(string markup, in TextStudioMarkupPreprocessContext context);
    }

    internal static class TextStudioMarkupPreprocessorChain
    {
        public static string Apply(
            string markup,
            IReadOnlyList<ITextStudioMarkupPreprocessor> preprocessors,
            in TextStudioMarkupPreprocessContext context)
        {
            string result = markup ?? string.Empty;
            if (preprocessors == null)
                return result;

            for (int i = 0; i < preprocessors.Count; i++)
            {
                ITextStudioMarkupPreprocessor preprocessor = preprocessors[i];
                if (IsNull(preprocessor))
                    continue;

                result = preprocessor.PreprocessMarkup(result, in context) ?? string.Empty;
            }

            return result;
        }

        public static bool IsNull(ITextStudioMarkupPreprocessor preprocessor)
        {
            return preprocessor == null ||
                   preprocessor is Object unityObject && unityObject == null;
        }
    }
}
