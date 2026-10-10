using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextStudioAppearanceAssetIdentity
    {
        internal static VisualElement Create(TextStudio2DStyleAsset asset) => TextStudioAppearanceCatalogUtility.Instance.CreateControls(asset);
    }
}
