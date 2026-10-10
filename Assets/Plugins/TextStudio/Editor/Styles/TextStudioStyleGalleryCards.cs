using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextStudioStyleGalleryCards
    {
        internal static TextGalleryCard Create(TextStudioStyleCatalogEntry entry, TextGalleryCategory category,
            Func<TextStudioStyleCatalogProvider> providerFactory, Action createNew)
        {
            TextGalleryCardActionState ResolveAction()
            {
                TextStudioStyleCatalogProvider provider = providerFactory();
                if (provider.CanUnfreeze)
                    return new TextGalleryCardActionState(provider.Unfreeze, "Unfreeze to Edit", "Restore live editing before applying an appearance.", createNew, "Create New", "Create separate text using this appearance.");
                bool canApply = provider.CanApply(entry, out string reason);
                return new TextGalleryCardActionState(entry.Asset == null ? null : canApply ? () => provider.Apply(entry) : createNew,
                    canApply ? provider.ApplyLabel : "Create Text", entry.Asset == null || canApply && !string.IsNullOrEmpty(reason) ? reason : "Use this reusable style. Editing the asset updates its text objects.",
                    canApply ? createNew : null, canApply ? "Create New" : string.Empty, "Create separate text using this appearance.");
            }
            TextGalleryCard card = new TextGalleryCard(entry.Id, entry.Title, entry.Description, category, () => ResolveAction().PrimaryAction?.Invoke(),
                _ => CreateThumbnail(entry, providerFactory), TextGalleryPreviewQuality.RenderedStatic,
                new[] { entry.Category, entry.Keywords, entry.BuiltIn ? "Built-in" : "Custom" }, "Apply Style", "Apply a reusable style.",
                createNew, "Create New", "Create separate text.", ResolveAction,
                additionalBadges: new[] { entry.BuiltIn ? "Built-in" : "Custom", entry.Category },
                productionMetadata: new TextGalleryProductionMetadata(TextGalleryWorkflow.Appearance,
                    category == TextGalleryCategory.ThreeD ? TextGalleryRendererSupport.Extruded3D : TextGalleryRendererSupport.FlatText,
                    TextGalleryRenderCost.RendererManaged,
                    category == TextGalleryCategory.ThreeD ? TextGalleryResourceRequirements.RendererPackage : TextGalleryResourceRequirements.None,
                    category == TextGalleryCategory.ThreeD ? TextGalleryReducedMotionSupport.MotionFree : TextGalleryReducedMotionSupport.Reduced));
            return entry.BuiltIn ? card : card.WithCustomOrigin();
        }

        static VisualElement CreateThumbnail(TextStudioStyleCatalogEntry entry, Func<TextStudioStyleCatalogProvider> providerFactory)
        {
            if (entry.BuiltIn && !string.IsNullOrEmpty(entry.PreviewFileName) && TextGalleryRenderedPreview.TryLoadTexture(entry.PackageName, TextGalleryRenderedPreview.BuildPreviewPath(entry.PreviewFileName), out _, out _))
                return TextGalleryRenderedPreview.Create(entry.PackageName, TextGalleryRenderedPreview.BuildPreviewPath(entry.PreviewFileName), entry.Title, Color.gray);
            Image image = new Image { scaleMode = ScaleMode.ScaleToFit };
            image.style.flexGrow = 1;
            image.style.backgroundColor = new Color(.12f, .13f, .15f);
            Texture2D texture = null;
            IVisualElementScheduledItem pending = null;
            image.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                pending = image.schedule.Execute(() =>
                {
                    if (entry.Asset == null || image.panel == null) return;
                    try
                    {
                        using TextStudioStylePreviewSession preview = providerFactory().CreatePreview(entry, entry.SampleText, false, 384, 216);
                        preview.Render(.75f);
                        texture = preview.Capture();
                        image.image = texture;
                    }
                    catch (Exception error) { image.tooltip = "Preview unavailable: " + error.Message; }
                });
            });
            image.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                pending?.Pause();
                image.image = null;
                if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                texture = null;
            });
            return image;
        }
    }
}
