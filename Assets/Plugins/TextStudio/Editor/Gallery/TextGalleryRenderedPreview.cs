using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextGalleryRenderedPreview
    {
        public const string PreviewFolder = "Editor/GalleryPreviews";
        public const string ManifestFileName = "gallery-preview-manifest.json";

        public static VisualElement Create(
            string packageName,
            string relativeImagePath,
            string fallbackTitle,
            Color accent)
        {
            VisualElement root = CreatePreviewRoot();
            if (TryLoadTexture(packageName, relativeImagePath, out Texture2D texture, out _))
            {
                Image image = new Image
                {
                    image = texture,
                    scaleMode = ScaleMode.ScaleToFit
                };
                image.style.flexGrow = 1f;
                image.style.width = Length.Percent(100f);
                image.style.height = Length.Percent(100f);
                root.Add(image);
                return root;
            }

            AddFallback(root, fallbackTitle, accent);
            return root;
        }

        public static string BuildPreviewPath(string fileName)
        {
            return PreviewFolder + "/" + fileName;
        }

        public static bool TryLoadTexture(string packageName, string relativeImagePath, out Texture2D texture, out string assetPath)
        {
            texture = null;
            assetPath = null;

            if (string.IsNullOrEmpty(packageName) || string.IsNullOrEmpty(relativeImagePath))
                return false;

            string assetRoot = TextStudioPackagePaths.FindAssetRoot(packageName);
            if (string.IsNullOrEmpty(assetRoot))
                return false;

            assetPath = assetRoot.TrimEnd('/') + "/" + relativeImagePath.Replace('\\', '/').TrimStart('/');
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            return texture != null;
        }

        static VisualElement CreatePreviewRoot()
        {
            VisualElement root = new VisualElement();
            root.style.flexGrow = 1f;
            root.style.overflow = Overflow.Hidden;
            root.style.borderTopLeftRadius = 4f;
            root.style.borderTopRightRadius = 4f;
            root.style.borderBottomLeftRadius = 4f;
            root.style.borderBottomRightRadius = 4f;
            root.style.backgroundColor = new Color(0.095f, 0.1f, 0.105f, 1f);
            return root;
        }

        static void AddFallback(VisualElement root, string title, Color accent)
        {
            VisualElement band = new VisualElement();
            band.style.position = Position.Absolute;
            band.style.left = 0f;
            band.style.right = 0f;
            band.style.top = 0f;
            band.style.bottom = 0f;
            band.style.backgroundColor = new Color(0.085f, 0.09f, 0.1f, 1f);
            root.Add(band);

            VisualElement line = new VisualElement();
            line.style.position = Position.Absolute;
            line.style.left = 0f;
            line.style.right = 0f;
            line.style.bottom = 0f;
            line.style.height = 3f;
            line.style.backgroundColor = accent;
            root.Add(line);

            Label label = new Label(string.IsNullOrEmpty(title) ? "Preview pending" : title);
            label.style.position = Position.Absolute;
            label.style.left = 12f;
            label.style.right = 12f;
            label.style.top = 0f;
            label.style.bottom = 0f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 16f;
            label.style.color = new Color(0.88f, 0.91f, 0.96f, 1f);
            root.Add(label);
        }

    }
}
