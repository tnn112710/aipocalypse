using UnityEngine;

namespace TextStudio
{
    /// <summary>Settings for fill dilation, edge softness, measurement units, and preservation of native text appearance.</summary>
    public partial struct TextStudio2DFillSpec
    {
        [SerializeField, Tooltip("Preserve the text material's native face, outline and underlay instead of painting a new fill.")]
        bool _preservesNativeAppearance;
        internal bool PreservesNativeAppearance => _preservesNativeAppearance;
        internal TextStudio2DFillSpec WithNativeAppearance()
        {
            TextStudio2DFillSpec copy = this;
            copy._preservesNativeAppearance = true;
            return copy;
        }
    }
}
