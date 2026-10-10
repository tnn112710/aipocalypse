using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // Provider presentation is immutable editor metadata.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed class TextStudioAppearancePreparationProvider : ITextStudioRuntimePreparationProvider, ITextStudioPreparationPresentationProvider
    {
        internal const string GeneratedFolder = "Assets/Text Studio/Resources/TextStudio/Prepared Appearance";
        internal static readonly TextStudioPreparationPresentation Display = new TextStudioPreparationPresentation("text-studio", "Text Studio", "font-appearance", "Font data for 2D effects", "Saves the font outlines that effects such as outlines, glows and depth trace, so they render in builds without the original font file.") { UsesCharacters = true };
        public TextStudioPreparationPresentation Presentation => Display;
        public string Id => "2D Appearance";

        internal static bool NeedsWritingSources(TextStudioText text) => text.WritingStyle != null &&
            TextStudioWritingModeUtility.RequestedDataLevel(text.WritingStyle.Configuration.Mode) >= TextStudioWritingDataLevel.PreparedOutline;

        internal static IEnumerable<CompiledTextStudio2DStyle> Programs(TextStudioText text)
        {
            if (text.CompiledAppearancePlan != null)
            {
                foreach (CompiledTextStudio2DStyle program in text.CompiledAppearancePlan.Programs) yield return program;
            }
            else if (text.CompiledAppearanceStyle != null) yield return text.CompiledAppearanceStyle;
        }

        internal static bool NeedsSources(TextStudioText text) => text.TMPText != null &&
            (NeedsWritingSources(text) || (text.SupportsActiveTextStudio2DAppearance &&
             Programs(text).Any(style => style.RequiresContourGeometry || NeedsFields(style))));

        static bool NeedsFields(CompiledTextStudio2DStyle style) => style.SourceFieldRequest.RequiresMultiDistanceField ||
            style.SourceFieldRequest.Quality == TextStudio2DSourceFieldQuality.High ||
            style.SourceFieldRequest.Quality == TextStudio2DSourceFieldQuality.Ultra;

        internal static bool NeedsFields(TextStudioText text) => Programs(text).Any(NeedsFields);

        internal static List<TextStudioFontAppearanceRequest> Requests(TextStudioText text)
        {
            float density = 256;
            float reach = .3f;
            foreach (CompiledTextStudio2DStyle program in Programs(text))
            {
                density = Mathf.Max(density, program.SourceFieldRequest.MinimumPixelsPerEm);
                reach = Mathf.Max(reach, program.SourceFieldRequest.MinimumSafeDistanceEm);
            }
            if (NeedsFields(text) && (float.IsNaN(density) || float.IsNaN(reach) || density > 1024 || reach > 2))
                throw new InvalidOperationException("This appearance exceeds the supported preparation quality or distance range. Reduce its field quality or effect reach before preparing.");
            if (!NeedsFields(text)) { density = 256; reach = .3f; }
            List<TextStudioFontAppearanceRequest> requests = TextStudioFontAppearancePreparation.CreateTextRequests(text, false, null,
                GeneratedFolder, TextStudioFontAppearanceTierRequest.Adaptive(density, reach));
            TextStudioSurfaceGroup group = text.GetComponent<TextStudioSurfaceGroup>();
            if (group != null && group.isActiveAndEnabled)
            {
                for (int sourceIndex = 0; sourceIndex < group.SourceCount; sourceIndex++)
                {
                    TextStudioText source = group.GetSource(sourceIndex);
                    if (source == null || source == text || !source.isActiveAndEnabled) continue;
                    foreach (TextStudioFontAppearanceRequest additional in TextStudioFontAppearancePreparation.CreateTextRequests(source, false, null,
                                 GeneratedFolder, TextStudioFontAppearanceTierRequest.Adaptive(density, reach)))
                    {
                        int existing = requests.FindIndex(candidate => candidate.NativeFont == additional.NativeFont && candidate.FaceIndex == additional.FaceIndex);
                        if (existing < 0) requests.Add(additional);
                        else
                        {
                            TextStudioFontAppearanceRequest previous = requests[existing];
                            requests[existing] = new TextStudioFontAppearanceRequest(previous.ReadSource, previous.FaceIndex, previous.OutputPath,
                                previous.GlyphIds.Concat(additional.GlyphIds), previous.SourceName, previous.NativeFont, previous.Tiers);
                        }
                    }
                }
            }
            for (int i = 0; i < requests.Count; i++)
            {
                TextStudioFontAppearanceRequest request = requests[i];
                string existing = string.Empty;
                for (int binding = 0; binding < text.AppearanceFontCount; binding++)
                {
                    TextStudioFontAppearanceData data = text.GetAppearanceFont(binding);
                    if (data != null && data.Font == request.NativeFont) { existing = AssetDatabase.GetAssetPath(data); break; }
                }
                string path = existing.Length > 0 ? existing : ManagedPath(request.NativeFont);
                requests[i] = new TextStudioFontAppearanceRequest(request.ReadSource, request.FaceIndex, path, request.GlyphIds,
                    request.SourceName, request.NativeFont, request.Tiers);
            }
            return requests;
        }

        internal static string ManagedPath(UnityEngine.Object font)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(font, out string guid, out long localId))
                throw new InvalidOperationException("Save the font as an asset before preparing runtime data.");
            return GeneratedFolder + "/" + guid + "-" + localId + ".asset";
        }

        internal static bool Covers(TextStudioFontAppearanceData data, TextStudioFontAppearanceRequest request, bool fields)
        {
            if (data == null || data.Font != request.NativeFont || data.FaceIndex != request.FaceIndex ||
                !TextStudioFontAppearanceBuildValidation.ValidateAsset(data, out _)) return false;
            if (!fields) return true;
            foreach (TextStudioFontAppearanceTierRequest requested in request.Tiers)
            {
                bool found = false;
                for (int i = 0; i < data.TierCount; i++)
                {
                    TextStudioPreparedFieldTier tier = data.GetTier(i);
                    if (tier.PixelsPerEm < requested.PixelsPerEm || tier.DistanceRangeEm < requested.DistanceRangeEm) continue;
                    if (request.GlyphIds.All(id => tier.TryGetGlyph(id, out _))) { found = true; break; }
                }
                if (!found) return false;
            }
            return true;
        }

        public void Analyze(TextStudioText text, TextStudioPreparationReport report)
        {
            if (!NeedsSources(text)) return;
            List<TextStudioFontAppearanceRequest> requests = Requests(text);
            bool ready = requests.All(request => Covers(AssetDatabase.LoadAssetAtPath<TextStudioFontAppearanceData>(request.OutputPath), request, NeedsFields(text)));
            report.Add(new TextStudioPreparationItem(Id, text.name, ready ? TextStudioPreparationState.Ready : TextStudioPreparationState.NeedsAttention,
                ready ? "Known font coverage is prepared." : "Font appearance data needs an update. Preparation runs automatically before a build.")
                { RequiresFontEmbedding = !ready, DataPaths = TextStudioRuntimePreparation.ExistingAssets(requests.Select(request => request.OutputPath)) });
        }

        public IEnumerator Prepare(TextStudioText text, BuildTarget target, TextStudioPreparationReport report)
        {
            if (!NeedsSources(text)) yield break;
            TextStudioPreparationItem item = new TextStudioPreparationItem(Id, text.name, TextStudioPreparationState.Updating, "Checking known glyph coverage...");
            report.Add(item);
            bool fields = NeedsFields(text);
            List<TextStudioFontAppearanceRequest> requests = Requests(text);
            foreach (TextStudioFontAppearanceRequest request in requests)
            {
                TextStudioFontAppearanceData current = AssetDatabase.LoadAssetAtPath<TextStudioFontAppearanceData>(request.OutputPath);
                if (Covers(current, request, fields)) { Bind(text, current); continue; }
                if (!TextStudioRuntimePreparation.FontEmbeddingAllowed)
                    throw new InvalidOperationException("Choose whether this project may embed font source data in Tools > Text Studio > Runtime Preparation. This choice also applies to automated builds.");
                if (!fields)
                {
                    Bind(text, TextStudioFontAppearancePreparation.SaveSourceOnly(request));
                    continue;
                }
                item.Message = "Preparing " + request.SourceName + "...";
                bool ownsJob = false;
                try
                {
                    if (!TextStudioFontAppearancePreparation.StartBatch(new[] { request }, data => Bind(text, data)))
                        throw new InvalidOperationException(TextStudioFontAppearancePreparation.LastError);
                    ownsJob = true;
                    while (TextStudioFontAppearancePreparation.IsBusy)
                    { TextStudioFontAppearancePreparation.Update(); yield return null; }
                    ownsJob = false;
                    if (TextStudioFontAppearancePreparation.LastError.Length > 0) throw new InvalidOperationException(TextStudioFontAppearancePreparation.LastError);
                }
                finally { if (ownsJob) TextStudioFontAppearancePreparation.Cancel(); }
            }
            item.State = TextStudioPreparationState.Ready;
            item.Message = "Known font coverage is prepared. New runtime text may require additional declared coverage.";
            item.DataPaths = TextStudioRuntimePreparation.ExistingAssets(requests.Select(request => request.OutputPath));
        }

        internal static void Bind(TextStudioText text, TextStudioFontAppearanceData data)
        {
            if (text == null || data == null || text.FindAppearanceFont(data.Font) == data) return;
            if (!EditorSceneManager.IsPreviewScene(text.gameObject.scene)) Undo.RecordObject(text, "Assign Runtime Preparation");
            text.SetAppearanceFont(data);
            EditorUtility.SetDirty(text);
            PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
    }
}
