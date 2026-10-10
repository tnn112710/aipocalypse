using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
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
        }

        internal static void ResetForTests()
        {
            Reset();
        }

        static void Reset()
        {
            foreach (KeyValuePair<CacheKey, CacheEntry> pair in s_layerEntries)
                Destroy(pair.Value.Material);
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
