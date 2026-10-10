using UnityEngine;

namespace Rowlan.Genesis
{
    /// <summary>
    /// Whether to have the noise offset distinct per stamp or all stamps have the same offset.
    /// Same offset is eg useful if you have multiple flowers as details in clusters and want them all in the same clusters combined.
    /// </summary>
    public enum NoiseRandomization
    {
        [Tooltip("A single offset is created and all stamps get the same offset assigned")]
        SharedOffset,

        [Tooltip("Every stamp gets a random offset")]
        FullRandom,
    }
}