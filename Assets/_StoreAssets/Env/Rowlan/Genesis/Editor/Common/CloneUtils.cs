using System.Collections.Generic;
using UnityEngine;
using static JBooth.MicroVerseCore.FilterSet;

namespace Rowlan.Genesis
{
    /// <summary>
    /// Various methods to clone objects.
    /// </summary>
    public class CloneUtils
    {
        /// <summary>
        /// Clone a filter
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static Filter Clone( Filter filter)
        {
            string json = JsonUtility.ToJson(filter);
            return JsonUtility.FromJson<Filter>(json);
        }

        /// <summary>
        /// Clone list of texture filters
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        public static List<TextureFilter> Clone(List<TextureFilter> filters)
        {
            List<TextureFilter> clonedFilters = new List<TextureFilter>();

            foreach( TextureFilter filter in filters)
            {
                string json = JsonUtility.ToJson(filter);
                TextureFilter clone = JsonUtility.FromJson<TextureFilter>(json);

                clonedFilters.Add( clone);
            }

            return clonedFilters;
        }

    }
}