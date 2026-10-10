using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>
    /// Ordinal ignore-case comparer whose hash does not allocate. Mono hashes ignore-case keys by copying them to upper
    /// case, so registry lookups made while parsing markup allocated a string for every tag they classified.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // The shared comparer or empty metadata has no Play Mode state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioIgnoreCaseComparer : IEqualityComparer<string>
    {
        internal static readonly TextStudioIgnoreCaseComparer Instance = new TextStudioIgnoreCaseComparer();

        TextStudioIgnoreCaseComparer()
        {
        }

        public bool Equals(string x, string y) => string.Equals(x, y, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            int hash = value.Length;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                // Characters hash by the invariant upper case that ordinal ignore-case equality compares. Surrogate halves
                // hash as one constant, so a supplementary character cannot split keys that compare equal.
                int folded = character < 0x80
                    ? character >= 'a' && character <= 'z' ? character - 32 : character
                    : char.IsSurrogate(character) ? 0xD800 : char.ToUpperInvariant(character);
                hash = unchecked(hash * 31 + folded);
            }
            return hash;
        }
    }
}
