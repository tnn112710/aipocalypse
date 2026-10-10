using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio.Editor
{
    internal readonly struct TextStudioPreviewExportRendererOption
    {
        internal readonly string Id;
        internal readonly string DisplayName;
        internal readonly string Description;
        internal readonly int SortOrder;

        internal TextStudioPreviewExportRendererOption(
            string id,
            string displayName,
            string description,
            int sortOrder)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Description = description ?? string.Empty;
            SortOrder = sortOrder;
        }
    }

    internal interface ITextStudioPreviewExportFrameRenderer : IDisposable
    {
        Texture TargetTexture { get; }
        void RenderToTarget(float lifecycleTime);
        void RenderToTarget(float continuousTime, float lifecycleTime);
        Texture2D Render(float lifecycleTime);
        void ResetEvaluation();
    }

    internal interface ITextStudioPreviewExportRendererFactory
    {
        TextStudioPreviewExportRendererOption Option { get; }
        bool IsAvailable { get; }
        ITextStudioPreviewExportFrameRenderer Create(
            TextStudioPreviewExportSnapshot snapshot,
            int width,
            int height,
            bool transparentBackground);
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioPreviewExportRendererRegistry
    {
        internal const string FlatRendererId = "textstudio.flat2d";

        static readonly Dictionary<string, ITextStudioPreviewExportRendererFactory> s_factories =
            new Dictionary<string, ITextStudioPreviewExportRendererFactory>(StringComparer.Ordinal);

        internal static string DefaultRendererId
        {
            get
            {
                string extensionRendererId = GetFirstExtensionRendererId();
                return string.IsNullOrEmpty(extensionRendererId)
                    ? FlatRendererId
                    : extensionRendererId;
            }
        }

        internal static string GetFirstExtensionRendererId()
        {
            ITextStudioPreviewExportRendererFactory first = null;
            foreach (ITextStudioPreviewExportRendererFactory factory in s_factories.Values)
            {
                if (factory == null || !factory.IsAvailable)
                    continue;
                if (first == null ||
                    factory.Option.SortOrder < first.Option.SortOrder ||
                    (factory.Option.SortOrder == first.Option.SortOrder &&
                     string.Compare(factory.Option.Id, first.Option.Id, StringComparison.Ordinal) < 0))
                {
                    first = factory;
                }
            }
            return first != null ? first.Option.Id : string.Empty;
        }

        internal static void Register(ITextStudioPreviewExportRendererFactory factory)
        {
            if (factory == null ||
                string.IsNullOrWhiteSpace(factory.Option.Id) ||
                string.Equals(factory.Option.Id, FlatRendererId, StringComparison.Ordinal))
                return;
            s_factories[factory.Option.Id] = factory;
        }

        internal static void GetOptions(List<TextStudioPreviewExportRendererOption> options)
        {
            if (options == null)
                return;
            options.Clear();
            options.Add(new TextStudioPreviewExportRendererOption(
                FlatRendererId,
                "Flat Text",
                "Runtime TextMeshPro glyph output with the shared effect channels.",
                0));
            foreach (ITextStudioPreviewExportRendererFactory factory in s_factories.Values)
            {
                if (factory != null && factory.IsAvailable)
                    options.Add(factory.Option);
            }
            options.Sort((first, second) =>
            {
                int order = first.SortOrder.CompareTo(second.SortOrder);
                return order != 0
                    ? order
                    : string.Compare(first.DisplayName, second.DisplayName, StringComparison.Ordinal);
            });
        }

        internal static ITextStudioPreviewExportFrameRenderer Create(
            string rendererId,
            TextStudioPreviewExportSnapshot snapshot,
            int width,
            int height,
            bool transparentBackground)
        {
            if (!string.IsNullOrEmpty(rendererId) &&
                !string.Equals(rendererId, FlatRendererId, StringComparison.Ordinal) &&
                s_factories.TryGetValue(rendererId, out ITextStudioPreviewExportRendererFactory factory) &&
                factory != null &&
                factory.IsAvailable)
            {
                return factory.Create(snapshot, width, height, transparentBackground);
            }

            return new TextStudioPreviewFrameRenderer(
                snapshot,
                width,
                height,
                transparentBackground);
        }
    }
}
