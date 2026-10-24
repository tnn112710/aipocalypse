using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioBufferCapacity
    {
        const int MinimumSmallBufferCapacity = 8;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int ForCount(int requiredCount)
        {
            return requiredCount > 0
                ? Mathf.NextPowerOfTwo(Mathf.Max(MinimumSmallBufferCapacity, requiredCount))
                : 0;
        }

        /// <summary>
        /// Returns <paramref name="array"/> when it holds <paramref name="requiredCount"/> elements, otherwise a new array of
        /// <see cref="ForCount"/> capacity holding its first <paramref name="keepCount"/> elements.
        /// </summary>
        internal static T[] EnsureKeeping<T>(T[] array, int requiredCount, int keepCount)
        {
            if (array != null && array.Length >= requiredCount)
                return array;

            T[] grown = new T[ForCount(requiredCount)];
            if (array != null && keepCount > 0)
                Array.Copy(array, grown, Math.Min(Math.Min(keepCount, array.Length), grown.Length));
            return grown;
        }
    }
}
