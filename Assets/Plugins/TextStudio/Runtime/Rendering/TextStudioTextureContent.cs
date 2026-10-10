using ImpossibleRobert.Common;
using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioTextureContent
    {
        internal static Hash128 ImportHash(Texture texture)
        {
#if UNITY_EDITOR
            return texture != null ? texture.imageContentsHash : default;
#else
            return default;
#endif
        }

        internal static void HashRevision(Texture texture, ref TextStudioDeterministicHash64 hash)
        {
            hash.Add(texture != null ? texture.GetStableId() : 0);
            hash.Add(texture != null ? texture.updateCount : 0u);
            // Import hashes supplement the runtime revision; they never establish shared ownership.
            hash.Add(ImportHash(texture).GetHashCode());
        }
    }
}
