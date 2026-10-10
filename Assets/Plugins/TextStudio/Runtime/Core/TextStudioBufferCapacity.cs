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
    }
}
