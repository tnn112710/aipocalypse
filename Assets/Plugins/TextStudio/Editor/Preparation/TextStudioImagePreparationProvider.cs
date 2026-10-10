using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TextStudio.Editor
{
    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // Provider presentation is immutable editor metadata.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioImagePreparationProvider : ITextStudioRuntimePreparationProvider, ITextStudioPreparationPresentationProvider
    {
        static TextStudioImagePreparationProvider()
        {
            TextStudioRuntimePreparation.Register(new TextStudioImagePreparationProvider());
            TextStudioRuntimePreparationBuildProcessor.BindPreparedAddons += BindForBuild;
        }
        internal static readonly TextStudioPreparationPresentation Display = new TextStudioPreparationPresentation("text-studio", "Text Studio", "inline-images", "Inline image silhouettes", "Saves the outlines of inline images, so effects on them appear without reading back and processing the image at runtime.");
        public TextStudioPreparationPresentation Presentation => Display;
        public string Id => "Inline Images";
        internal static bool HasApplicableSources(TextStudioText text)
        {
            if (!HasFieldRequirements(text)) return false;
            for (int i = 0; i < text.ColorSources.Length; i++)
                if (IsApplicableSource(text.ColorSources[i])) return true;
            return false;
        }
        static bool HasFieldRequirements(TextStudioText text) => text.SupportsActiveTextStudio2DAppearance &&
            TextStudioAppearancePreparationProvider.Programs(text).Any(style => !style.IsPassThrough && NeedsFields(style));
        static bool IsApplicableSource(TextStudioColorSource source) => source != null && !source.IsEmpty &&
            source.Revision == 0 && source.Texture is Texture2D && EditorUtility.IsPersistent(source.Texture);
        static List<TextStudioColorSource> Sources(TextStudioText text)
        {
            List<TextStudioColorSource> sources = new List<TextStudioColorSource>();
            if (!HasFieldRequirements(text)) return sources;
            for (int i = 0; i < text.ColorSources.Length; i++)
            {
                TextStudioColorSource source = text.ColorSources[i];
                if (IsApplicableSource(source)) sources.Add(source);
            }
            return sources;
        }
        static bool IsFieldOperation(in TextStudio2DCompiledOperation operation) =>
            operation.SurfaceOwnerOperationIndex < 0 && operation.Kind != TextStudio2DLayerKind.Group &&
            operation.Kind != TextStudio2DLayerKind.Decoration && operation.Kind != TextStudio2DLayerKind.ColorAdjustment &&
            (operation.Kind != TextStudio2DLayerKind.Fill || operation.Fill.ColorFace == TextStudio2DColorFaceMode.SilhouetteFill) &&
            (operation.Kind != TextStudio2DLayerKind.Stroke || operation.Stroke.Join == TextStudio2DStrokeJoin.Round && operation.Stroke.DashCount == 0);

        static bool NeedsFields(CompiledTextStudio2DStyle style)
        {
            for (int i = 0; i < style.OperationCount; i++)
                if (IsFieldOperation(style.GetOperation(i))) return true;
            return false;
        }

        internal static void Requirements(TextStudioColorSource source, CompiledTextStudio2DStyle style, out float density, out float padding)
        {
            density = Mathf.NextPowerOfTwo(Mathf.CeilToInt(Mathf.Clamp(Mathf.Max(
                TextStudio2DHeroCompositor.ResolveColorDensity(source), style.SourceFieldRequest.MinimumPixelsPerEm), 16, 1024)));
            padding = 2 / density;
            for (int i = 0; i < style.OperationCount; i++)
            {
                TextStudio2DCompiledOperation operation = style.GetOperation(i);
                if (!IsFieldOperation(in operation)) continue;
                Vector4 bounds = operation.BoundsExpansion.Vector;
                float extent = Mathf.Max(Mathf.Max(bounds.x, bounds.y), Mathf.Max(bounds.z, bounds.w));
                if (TextStudio2DFieldPadding.UsesPixelUnits(in operation)) extent /= density;
                padding = Mathf.Max(padding, extent);
            }
            if (!float.IsFinite(padding) || padding > 16)
                throw new InvalidOperationException("This image appearance exceeds the supported silhouette padding. Reduce its effect reach before preparing.");
            padding = Mathf.Ceil(padding * 32) / 32;
        }

        static void Requirements(TextStudioText text, TextStudioColorSource source, out float density, out float padding)
        {
            density = 16;
            padding = 0;
            foreach (CompiledTextStudio2DStyle style in TextStudioAppearancePreparationProvider.Programs(text))
            {
                if (style.IsPassThrough || !NeedsFields(style)) continue;
                Requirements(source, style, out float programDensity, out float programPadding);
                density = Mathf.Max(density, programDensity);
                padding = Mathf.Max(padding, programPadding);
            }
        }

        static bool CurrentForText(TextStudioText text, TextStudioColorSource source, out TextStudioColorAppearanceData data)
        {
            Requirements(text, source, out float density, out float padding);
            // Preserve hand-assigned companions, including their asset GUIDs and shared coverage.
            for (int i = 0; i < text.AppearanceImageCount; i++)
            {
                data = text.GetAppearanceImages(i);
                TextStudioPreparedColorField field = data != null ? data.Find(source) : null;
                if (field != null && field.RequestedPixelsPerEm >= density && field.PaddingEm >= padding) return true;
            }
            return Current(source, density, padding, out data);
        }
        internal static string PathFor(TextStudioColorSource source)
        {
            StringBuilder key = new StringBuilder(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source.Texture)));
            key.Append(source.AlphaThreshold.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            key.Append(source.PremultipliedAlpha);
            for (int i = 0; i < source.Positions.Length; i++)
                key.Append(FormattableString.Invariant($"/{source.Positions[i].x:R},{source.Positions[i].y:R},{source.Uv[i].x:R},{source.Uv[i].y:R}"));
            for (int i = 0; i < source.Triangles.Length; i++) key.Append('/').Append(source.Triangles[i]);
            return TextStudioAppearancePreparationProvider.GeneratedFolder + "/Image-" + Hash128.Compute(key.ToString()) + ".asset";
        }
        static string OutputPath(TextStudioText text, TextStudioColorSource source)
        {
            for (int i = 0; i < text.AppearanceImageCount; i++)
            {
                TextStudioColorAppearanceData data = text.GetAppearanceImages(i);
                if (data == null) continue;
                for (int field = 0; field < data.FieldCount; field++)
                {
                    TextStudioPreparedColorField prepared = data.GetField(field);
                    if (prepared != null && prepared.SourceTexture == source.Texture && prepared.CreateSource().Identity == source.Identity)
                    {
                        string path = AssetDatabase.GetAssetPath(data);
                        if (!string.IsNullOrEmpty(path)) return path;
                    }
                }
            }
            return PathFor(source);
        }
        static bool Current(TextStudioColorSource source, float density, float padding, out TextStudioColorAppearanceData data)
        {
            data = AssetDatabase.LoadAssetAtPath<TextStudioColorAppearanceData>(PathFor(source));
            TextStudioPreparedColorField field = data != null ? data.Find(source) : null;
            return field != null && field.RequestedPixelsPerEm >= density && field.PaddingEm >= padding;
        }
        public void Analyze(TextStudioText text, TextStudioPreparationReport report)
        {
            List<TextStudioColorSource> sources = Sources(text);
            if (sources.Count == 0) return;
            bool ready = sources.All(source => CurrentForText(text, source, out _));
            report.Add(new TextStudioPreparationItem(Id, text.name, ready ? TextStudioPreparationState.Ready : TextStudioPreparationState.RuntimeGenerated,
                ready ? "Known static image silhouettes are prepared." : "Static silhouettes can be prepared to avoid runtime readback and generation. Dynamic images remain runtime generated.", false)
                { DataPaths = TextStudioRuntimePreparation.ExistingAssets(sources.Select(source => OutputPath(text, source))) });
        }
        public IEnumerator Prepare(TextStudioText text, BuildTarget target, TextStudioPreparationReport report)
        {
            List<TextStudioColorSource> sources = Sources(text);
            if (sources.Count == 0) yield break;
            TextStudioPreparationItem item = new TextStudioPreparationItem(Id, text.name, TextStudioPreparationState.Updating, "Preparing static image silhouettes...", target == BuildTarget.WebGL);
            report.Add(item);
            foreach (TextStudioColorSource source in sources)
            {
                if (CurrentForText(text, source, out TextStudioColorAppearanceData data)) { Bind(text, data); continue; }
                Requirements(text, source, out float density, out float padding);
                bool ownsJob = false;
                try
                {
                    if (!TextStudioColorAppearancePreparation.Start(new[] { source }, OutputPath(text, source), density, padding, completed: prepared => Bind(text, prepared), useCpu: true))
                    { item.State = TextStudioPreparationState.NeedsAttention; item.Message = TextStudioColorAppearancePreparation.LastError; yield break; }
                    ownsJob = true;
                    while (TextStudioColorAppearancePreparation.IsBusy) { TextStudioColorAppearancePreparation.Update(); yield return null; }
                    ownsJob = false;
                    if (TextStudioColorAppearancePreparation.LastError.Length > 0)
                    { item.State = TextStudioPreparationState.NeedsAttention; item.Message = TextStudioColorAppearancePreparation.LastError; yield break; }
                }
                finally { if (ownsJob) TextStudioColorAppearancePreparation.Cancel(); }
            }
            item.State = TextStudioPreparationState.Ready; item.Message = "Known static image silhouettes are prepared.";
            item.DataPaths = TextStudioRuntimePreparation.ExistingAssets(sources.Select(source => OutputPath(text, source)));
        }
        static void Bind(TextStudioText text, TextStudioColorAppearanceData data)
        {
            if (text == null || data == null) return;
            for (int i = 0; i < text.AppearanceImageCount; i++) if (text.GetAppearanceImages(i) == data) return;
            if (!EditorSceneManager.IsPreviewScene(text.gameObject.scene)) Undo.RecordObject(text, "Assign Prepared Image");
            text.AddAppearanceImages(data); EditorUtility.SetDirty(text); PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
        static void BindForBuild(TextStudioText text)
        {
            foreach (TextStudioColorSource source in Sources(text))
                if (CurrentForText(text, source, out TextStudioColorAppearanceData data)) Bind(text, data);
        }
    }
}
