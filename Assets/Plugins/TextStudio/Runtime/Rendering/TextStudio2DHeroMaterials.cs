using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TextStudio
{
    internal struct TextStudio2DHeroLayerMaterialLease
    {
        internal Material Source;
        internal Texture Atlas;
        internal float PixelsPerEm;
        internal int ColorMode;
        internal float DistanceRange;
        internal Material Material;

        internal bool HasIdentity => !ReferenceEquals(Material, null);
        internal bool IsValid =>
            HasIdentity &&
            Material != null &&
            TextStudio2DShaderResources.IsUsable(Material.shader, requiresDataTables: true);
    }

#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DHeroMaterials
    {
        static readonly Dictionary<CacheKey, CacheEntry> s_layerEntries =
            new Dictionary<CacheKey, CacheEntry>();
        static Material s_composite;
        static Material s_effects;
        static Material s_output;
        static Material s_outputCanvas;
        static bool s_missingLayerWarning;
        static bool s_missingCompositeWarning;
        static bool s_missingEffectsWarning;
        static bool s_missingOutputWarning;
        internal static int LayerMaterialCount => s_layerEntries.Count;

        readonly struct CacheKey : IEquatable<CacheKey>
        {
            internal readonly Material Source;
            internal readonly Texture Atlas;
            internal readonly float PixelsPerEm;
            internal readonly int ColorMode;
            internal readonly float DistanceRange;

            internal CacheKey(Material source, Texture atlas, float pixelsPerEm, int colorMode, float distanceRange)
            {
                Source = source;
                Atlas = atlas;
                PixelsPerEm = pixelsPerEm;
                ColorMode = colorMode;
                DistanceRange = distanceRange;
            }

            public bool Equals(CacheKey other)
            {
                return ReferenceEquals(Source, other.Source) &&
                       ReferenceEquals(Atlas, other.Atlas) && PixelsPerEm.Equals(other.PixelsPerEm) &&
                       ColorMode == other.ColorMode && DistanceRange.Equals(other.DistanceRange);
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int source = ReferenceEquals(Source, null) ? 0 : RuntimeHelpers.GetHashCode(Source);
                    int atlas = ReferenceEquals(Atlas, null) ? 0 : RuntimeHelpers.GetHashCode(Atlas);
                    return (((source * 397 ^ atlas) * 397 ^ PixelsPerEm.GetHashCode()) * 397 ^ ColorMode) * 397 ^ DistanceRange.GetHashCode();
                }
            }
        }

        sealed class CacheEntry
        {
            internal readonly CacheKey Key;
            internal readonly Material Material;
            internal int SourceCrc;
            internal int References;
            // Per-row variants (heavy family, solid paint, material effects) sharing this material's properties; created on use.
            internal Material[] Variants;
            // Uniform row materials, one per packed record, held by the batches that draw them (TextStudio2DHeroMaterials.UniformRows).
            internal Dictionary<UniformRowKey, UniformRowMaterial> UniformRows;

            internal CacheEntry(
                in CacheKey key,
                Material material,
                int sourceCrc)
            {
                Key = key;
                Material = material;
                SourceCrc = sourceCrc;
                References = 1;
            }
        }

        internal static bool TryAcquireLayer(
            Material source,
            Texture atlas,
            out TextStudio2DHeroLayerMaterialLease lease, float pixelsPerEm = 0f, int colorMode = 0, float distanceRange = 0)
        {
            lease = default;
            if (source == null || atlas == null)
                return false;
            CacheKey key = new CacheKey(source, atlas, pixelsPerEm, colorMode, distanceRange);
            if (s_layerEntries.TryGetValue(key, out CacheEntry existing) &&
                existing.Material != null &&
                TextStudio2DShaderResources.IsUsable(existing.Material.shader, requiresDataTables: true))
            {
                Synchronize(existing);
                existing.References++;
                lease = new TextStudio2DHeroLayerMaterialLease
                {
                    Source = source,
                    Atlas = atlas,
                    PixelsPerEm = pixelsPerEm,
                    ColorMode = colorMode,
                    DistanceRange = distanceRange,
                    Material = existing.Material
                };
                return true;
            }
            if (existing != null)
            {
                s_layerEntries.Remove(key);
                DestroyVariants(existing);
                Destroy(existing.Material);
            }

            Shader shader = TextStudio2DShaderResources.LoadSupported(
                TextStudio2DResourceKeys.HeroLayerShader,
                "Hero title composition",
                ref s_missingLayerWarning,
                out string _);
            if (shader == null)
                return false;

            Material replacement = new Material(source)
            {
                name = source.name + " (Text Studio Hero Layer)",
                shader = shader,
                hideFlags = HideFlags.HideAndDontSave
            };
            Configure(in key, replacement);
            CacheEntry created = new CacheEntry(
                in key,
                replacement,
                source.ComputeCRC());
            s_layerEntries.Add(key, created);
            lease = new TextStudio2DHeroLayerMaterialLease
            {
                Source = source,
                Atlas = atlas,
                PixelsPerEm = pixelsPerEm,
                ColorMode = colorMode,
                DistanceRange = distanceRange,
                Material = replacement
            };
            return true;
        }

        internal static void ReleaseLayer(
            ref TextStudio2DHeroLayerMaterialLease lease)
        {
            if (!lease.HasIdentity)
            {
                lease = default;
                return;
            }
            CacheKey key = new CacheKey(lease.Source, lease.Atlas, lease.PixelsPerEm, lease.ColorMode, lease.DistanceRange);
            if (s_layerEntries.TryGetValue(key, out CacheEntry entry))
            {
                entry.References--;
                if (entry.References <= 0)
                {
                    s_layerEntries.Remove(key);
                    DestroyVariants(entry);
                    Destroy(entry.Material);
                }
            }
            lease = default;
        }

        internal static void SynchronizeLayer(
            in TextStudio2DHeroLayerMaterialLease lease)
        {
            if (!lease.IsValid)
                return;
            CacheKey key = new CacheKey(lease.Source, lease.Atlas, lease.PixelsPerEm, lease.ColorMode, lease.DistanceRange);
            if (s_layerEntries.TryGetValue(key, out CacheEntry entry))
                Synchronize(entry);
        }

        /// <summary>
        /// The source CRC a layer material was last configured from: it changes whenever a source property change reconfigures the
        /// layer material, its row variants and its uniform rows.
        /// </summary>
        internal static int LayerRevision(in TextStudio2DHeroLayerMaterialLease lease) =>
            lease.HasIdentity && s_layerEntries.TryGetValue(new CacheKey(lease.Source, lease.Atlas, lease.PixelsPerEm, lease.ColorMode,
                lease.DistanceRange), out CacheEntry entry) ? entry.SourceCrc : 0;

        internal static Material GetComposite()
        {
            return GetShared(
                ref s_composite,
                TextStudio2DResourceKeys.HeroCompositeShader,
                "Text Studio Hero Composite",
                ref s_missingCompositeWarning);
        }

        internal static Material GetOutput()
        {
            return GetShared(
                ref s_output,
                TextStudio2DResourceKeys.HeroOutputShader,
                "Text Studio Hero Output",
                ref s_missingOutputWarning);
        }

        /// <summary>The Canvas output material: the display pass only, since CanvasRenderer submits every pass.</summary>
        internal static Material GetOutputCanvas()
        {
            return GetShared(
                ref s_outputCanvas,
                TextStudio2DResourceKeys.HeroOutputCanvasShader,
                "Text Studio Hero Canvas Output",
                ref s_missingOutputWarning);
        }

        internal static Material GetEffects()
        {
            return GetShared(
                ref s_effects,
                TextStudio2DResourceKeys.HeroEffectsShader,
                "Text Studio Hero Effects",
                ref s_missingEffectsWarning);
        }

        static Material GetShared(
            ref Material material,
            string resourceKey,
            string name,
            ref bool missingWarning)
        {
            if (material != null &&
                TextStudio2DShaderResources.IsUsable(material.shader, requiresDataTables: false))
                return material;
            Destroy(material);
            material = null;
            Shader shader = TextStudio2DShaderResources.LoadSupported(
                resourceKey,
                "Hero title composition",
                ref missingWarning,
                out string _);
            if (shader == null)
                return null;
            material = new Material(shader)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave
            };
            return material;
        }

        static void Synchronize(CacheEntry entry)
        {
            if (entry.Key.Source == null || entry.Material == null)
                return;
            int sourceCrc = entry.Key.Source.ComputeCRC();
            if (sourceCrc == entry.SourceCrc)
                return;
            Configure(in entry.Key, entry.Material);
            entry.SourceCrc = sourceCrc;
            for (int index = 0; entry.Variants != null && index < entry.Variants.Length; index++)
                if (entry.Variants[index] != null)
                    ConfigureRowVariant(entry.Variants[index], entry.Material, index);
            SynchronizeUniformRows(entry);
        }

        // Row variant families: 0 light, 1 to 4 material, graphic, animated and depth, 5 to 7 plain material, graphic and animated.
        const int RowFamilies = 8;
        const int PlainRowFamilyOffset = 4;
        static readonly string[] s_plainRowKeywords = { "TEXT_STUDIO_PLAIN_MATERIAL", "TEXT_STUDIO_PLAIN_GRAPHIC", "TEXT_STUDIO_PLAIN_ANIMATED" };
        static readonly string[] s_plainRowFamilyKeywords = { "TEXT_STUDIO_FAMILY_MATERIAL", "TEXT_STUDIO_FAMILY_GRAPHIC", "TEXT_STUDIO_FAMILY_ANIMATED" };
        static bool s_plainRowsEnabled = true;

        /// <summary>Tests and lab A/B runs force plain rows off to compare them with the general family rows.</summary>
        internal static bool PlainRowsEnabled
        {
            get => s_plainRowsEnabled;
            set => s_plainRowsEnabled = value;
        }

        /// <summary>
        /// Whether a family row draw takes its plain variant: a material, graphic or animated row of a font-field batch drawn in
        /// Operation source mode over the full source range without glyph filter or coverage replay (<paramref name="plainDraw"/>).
        /// The variant compiles exactly those per-draw values as constants. Faux depth draws only generated or painted sources.
        /// </summary>
        internal static bool UsesPlainRow(in TextStudio2DHeroLayerMaterialLease lease, TextStudio2DShaderTier familyTier, bool plainDraw) =>
            plainDraw && s_plainRowsEnabled && lease.ColorMode == 0 &&
            familyTier >= TextStudio2DShaderTier.IsolatedMaterial && familyTier <= TextStudio2DShaderTier.IsolatedAnimated;

        /// <summary>
        /// The layer material of one Hero row: it compiles only that row's heavy family, solid paint when the row's paint is
        /// solid, and material-tier effects only when the text uses one. Every row sharing the program would otherwise run the
        /// complete single-operation program, which costs tens of milliseconds per title on mobile GPUs. A plain draw of a
        /// material, graphic or animated row takes the plain variant of its family (<see cref="UsesPlainRow"/>).
        /// </summary>
        internal static Material ResolveRowMaterial(in TextStudio2DHeroLayerMaterialLease lease, TextStudio2DShaderTier familyTier,
            bool solidPaint, bool materialEffects, bool plainDraw = false)
        {
            if (!lease.IsValid)
                return lease.Material;
            CacheKey key = new CacheKey(lease.Source, lease.Atlas, lease.PixelsPerEm, lease.ColorMode, lease.DistanceRange);
            if (!s_layerEntries.TryGetValue(key, out CacheEntry entry) || entry.Material == null)
                return lease.Material;
            int family = TextStudio2DShaderTiers.IsIsolated(familyTier)
                ? (int)familyTier - (int)TextStudio2DShaderTier.IsolatedMaterial + 1
                : 0;
            // The Hero Layer compiles solid paint in the family keyword set, so heavy rows keep their general paint.
            solidPaint &= family == 0;
            if (UsesPlainRow(in lease, familyTier, plainDraw))
                family += PlainRowFamilyOffset;
            int index = family + RowFamilies * ((solidPaint ? 1 : 0) + (materialEffects ? 2 : 0));
            entry.Variants ??= new Material[RowFamilies * 4];
            Material variant = entry.Variants[index];
            if (variant != null)
                return variant;
            variant = new Material(entry.Material) { name = entry.Material.name + " (Row " + index + ")", hideFlags = HideFlags.HideAndDontSave };
            ConfigureRowVariant(variant, entry.Material, index);
            entry.Variants[index] = variant;
            return variant;
        }

        static void ConfigureRowVariant(Material variant, Material layerMaterial, int index)
        {
            int family = index % RowFamilies;
            bool plain = family > PlainRowFamilyOffset;
            int heavy = plain ? family - PlainRowFamilyOffset : family;
            TextStudio2DShaderTier tier = heavy == 0
                ? TextStudio2DShaderTier.Full
                : (TextStudio2DShaderTier)((int)TextStudio2DShaderTier.IsolatedMaterial + heavy - 1);
            TextStudio2DShaderOptions options = TextStudio2DShaderOptions.None;
            if ((index / RowFamilies & 1) != 0) options |= TextStudio2DShaderOptions.SolidPaint;
            if ((index / RowFamilies & 2) != 0) options |= TextStudio2DShaderOptions.MaterialEffects;
            variant.CopyPropertiesFromMaterial(layerMaterial);
            TextStudio2DShaderTiers.Configure(variant, tier, options);
            // A plain row replaces its family keyword, which shares the keyword set; a shader without it keeps the general family row.
            for (int plainFamily = 0; plainFamily < s_plainRowKeywords.Length; plainFamily++)
            {
                LocalKeyword keyword = variant.shader.keywordSpace.FindKeyword(s_plainRowKeywords[plainFamily]);
                if (!keyword.isValid)
                    continue;
                bool enabled = plain && plainFamily == heavy - 1;
                variant.SetKeyword(keyword, enabled);
                LocalKeyword familyKeyword = variant.shader.keywordSpace.FindKeyword(s_plainRowFamilyKeywords[plainFamily]);
                if (enabled && familyKeyword.isValid)
                    variant.SetKeyword(familyKeyword, false);
            }
        }

        static void DestroyVariants(CacheEntry entry)
        {
            for (int index = 0; entry.Variants != null && index < entry.Variants.Length; index++)
                Destroy(entry.Variants[index]);
            entry.Variants = null;
            DestroyUniformRows(entry);
        }

        static void Configure(in CacheKey key, Material replacement)
        {
            Shader shader = replacement.shader;
            string name = replacement.name;
            replacement.CopyPropertiesFromMaterial(key.Source);
            replacement.shader = shader;
            replacement.name = name;
            replacement.hideFlags = HideFlags.HideAndDontSave;
            replacement.SetFloat(TextStudioTMPMaterialCompatibility.NativeMaterialModelProperty,
                (float)TextStudioTMPMaterialCompatibility.Analyze(key.Source).NativeMaterialModel);
            replacement.SetTexture(
                "_MainTex",
                key.Atlas != null ? key.Atlas : Texture2D.whiteTexture);
            replacement.SetFloat("_TextureWidth", Mathf.Max(1, key.Atlas.width));
            replacement.SetFloat("_TextureHeight", Mathf.Max(1, key.Atlas.height));
            replacement.SetFloat(TextStudioGlyphFieldContract.PixelsPerEmProperty, key.PixelsPerEm);
            replacement.SetFloat("_TextStudioColorSourceMode", key.ColorMode);
            if (key.ColorMode > 0)
            {
                replacement.SetColor("_FaceColor", key.Source.HasProperty("_Color") ? key.Source.GetColor("_Color") : Color.white);
                replacement.SetFloat("_WeightNormal", 0); replacement.SetFloat("_WeightBold", 0);
                replacement.SetFloat("_FaceDilate", 0); replacement.SetFloat("_OutlineWidth", 0);
                replacement.SetFloat("_OutlineSoftness", 0); replacement.SetFloat("_ScaleRatioA", 1);
                replacement.SetFloat("_ScaleX", 1); replacement.SetFloat("_ScaleY", 1);
                replacement.SetFloat("_GradientScale", key.ColorMode == 3 ? key.DistanceRange * key.PixelsPerEm : 1);
                replacement.SetFloat(TextStudioGlyphFieldContract.SafeDistanceProperty, key.DistanceRange);
            }
            replacement.SetFloat("_TextStudioPayloadEnabled", 1f);
            // Hero transports each glyph's Writing index explicitly; zero keeps ordinary glyphs unchanged.
            replacement.SetFloat("_TextStudioWritingSourceEnabled", 1f);
            replacement.SetFloat("_TextStudioPayloadFromTangent", 1f);
            // The shader consumes native material effects only for a native fill operation.
            // Other operations keep their existing isolated source-coverage behavior.
            bool nativeSdf = key.ColorMode == 0;
            bool outline = nativeSdf && (key.Source.IsKeywordEnabled("OUTLINE_ON") ||
                key.Source.HasProperty("_OutlineWidth") && key.Source.GetFloat("_OutlineWidth") > .0001f);
            bool innerUnderlay = nativeSdf && key.Source.IsKeywordEnabled("UNDERLAY_INNER");
            bool outerUnderlay = nativeSdf && !innerUnderlay && key.Source.IsKeywordEnabled("UNDERLAY_ON");
            if (outline) replacement.EnableKeyword("OUTLINE_ON"); else replacement.DisableKeyword("OUTLINE_ON");
            if (outerUnderlay) replacement.EnableKeyword("UNDERLAY_ON"); else replacement.DisableKeyword("UNDERLAY_ON");
            if (innerUnderlay) replacement.EnableKeyword("UNDERLAY_INNER"); else replacement.DisableKeyword("UNDERLAY_INNER");
            replacement.DisableKeyword("UNITY_UI_CLIP_RECT");
            replacement.DisableKeyword("UNITY_UI_ALPHACLIP");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetRuntimeState()
        {
            Reset();
            s_plainRowsEnabled = true;
        }

        internal static void ResetForTests()
        {
            Reset();
        }

        static void Reset()
        {
            foreach (KeyValuePair<CacheKey, CacheEntry> pair in s_layerEntries)
            {
                DestroyVariants(pair.Value);
                Destroy(pair.Value.Material);
            }
            s_layerEntries.Clear();
            Destroy(s_composite);
            Destroy(s_effects);
            Destroy(s_output);
            Destroy(s_outputCanvas);
            s_composite = null;
            s_effects = null;
            s_output = null;
            s_outputCanvas = null;
            s_missingLayerWarning = false;
            s_missingCompositeWarning = false;
            s_missingEffectsWarning = false;
            s_missingOutputWarning = false;
        }

        static void Destroy(Object target)
        {
            if (target == null)
                return;
            TextStudio2DHeroSubmission.Flush();
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }
    }
}
