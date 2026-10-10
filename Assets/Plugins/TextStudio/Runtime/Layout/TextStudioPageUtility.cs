using TMPro;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Shared TMP page-overflow helpers for renderers and decorations.</summary>
    public static class TextStudioPageUtility
    {
        /// <summary>Sentinel meaning page filtering is inactive.</summary>
        public const int NoPageFilter = -1;

        /// <summary>
        /// Returns the zero-based displayed page when overflow is <see cref="TextOverflowModes.Page"/>;
        /// otherwise <see cref="NoPageFilter"/>.
        /// </summary>
        public static int GetDisplayedPageIndex(TMP_Text tmpText)
        {
            if (tmpText == null || tmpText.overflowMode != TextOverflowModes.Page)
                return NoPageFilter;

            int pageCount = Mathf.Max(1, tmpText.textInfo != null ? tmpText.textInfo.pageCount : 1);
            int page = Mathf.Max(1, tmpText.pageToDisplay);
            return Mathf.Clamp(page - 1, 0, pageCount - 1);
        }

        /// <summary>Reports whether a character belongs to the displayed page, or always true when filtering is inactive.</summary>
        public static bool IsDisplayedPage(in TMP_CharacterInfo character, int displayedPage)
        {
            return displayedPage < 0 || character.pageNumber == displayedPage;
        }

        /// <summary>Reports whether a stored page index belongs to the displayed page, or always true when filtering is inactive or the page is unknown.</summary>
        public static bool IsDisplayedPage(int pageNumber, int displayedPage)
        {
            return displayedPage < 0 || pageNumber < 0 || pageNumber == displayedPage;
        }

        /// <summary>Returns TMP's laid-out page count, clamped to at least one.</summary>
        public static int GetPageCount(TMP_Text tmpText)
        {
            if (tmpText == null || tmpText.textInfo == null)
                return 1;

            return Mathf.Max(1, tmpText.textInfo.pageCount);
        }
    }
}
