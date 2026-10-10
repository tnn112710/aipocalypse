using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    internal struct TextStudioTMPProductionMaterialLease
    {
        internal Material Source;
        internal Material Replacement;
        internal Shader Shader;
        internal bool PayloadFromTangent;
        internal TextStudio2DFieldPaddingTier PaddingTier;
        internal bool WritingSource;
        internal float PixelsPerEm;

        internal bool HasIdentity =>
            !ReferenceEquals(Source, null) &&
            !ReferenceEquals(Replacement, null) &&
            !ReferenceEquals(Shader, null);
        internal bool IsValid =>
            HasIdentity && Source != null && Replacement != null && Shader != null;
    }

    /// <summary>
    /// Reference-counted production-material cache. Text objects that use the same source TMP
    /// material and transport path share one replacement material, preserving renderer batching.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioTMPProductionMaterialCache
    {
        const string PayloadEnabledProperty = "_TextStudioPayloadEnabled";
        const string PayloadFromTangentProperty = "_TextStudioPayloadFromTangent";
        const string WritingSourceEnabledProperty =
            "_TextStudioWritingSourceEnabled";

        static readonly Dictionary<CacheKey, CacheEntry> s_entries =
            new Dictionary<CacheKey, CacheEntry>();

        internal static int ActiveEntryCount => s_entries.Count;

        readonly struct CacheKey : IEquatable<CacheKey>
        {
            internal readonly Material Source;
            internal readonly Shader Shader;
            internal readonly bool PayloadFromTangent;
            internal readonly TextStudio2DFieldPaddingTier PaddingTier;
            internal readonly bool WritingSource;
            internal readonly float PixelsPerEm;

            internal CacheKey(
                Material source,
                Shader shader,
                bool payloadFromTangent,
                TextStudio2DFieldPaddingTier paddingTier,
                bool writingSource, float pixelsPerEm)
            {
                Source = source;
                Shader = shader;
                PayloadFromTangent = payloadFromTangent;
                PaddingTier = paddingTier;
                WritingSource = writingSource;
                PixelsPerEm = pixelsPerEm;
            }

            public bool Equals(CacheKey other)
            {
                return ReferenceEquals(Source, other.Source) &&
                       ReferenceEquals(Shader, other.Shader) &&
                       PayloadFromTangent == other.PayloadFromTangent &&
                       PaddingTier == other.PaddingTier &&
                       WritingSource == other.WritingSource && PixelsPerEm.Equals(other.PixelsPerEm);
            }

            public override bool Equals(object obj)
            {
                return obj is CacheKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = !ReferenceEquals(Source, null)
                        ? RuntimeHelpers.GetHashCode(Source)
                        : 0;
                    hash = hash * 397 ^ (!ReferenceEquals(Shader, null)
                        ? RuntimeHelpers.GetHashCode(Shader)
                        : 0);
                    hash = hash * 397 ^ (PayloadFromTangent ? 1 : 0);
                    hash = hash * 397 ^ (int)PaddingTier;
                    hash = hash * 397 ^ (WritingSource ? 1 : 0);
                    return hash * 397 ^ PixelsPerEm.GetHashCode();
                }
            }
        }

        sealed class CacheEntry
        {
            internal readonly CacheKey Key;
            internal readonly Material Material;
            internal int ReferenceCount;
            internal int SourceCrc;

            internal CacheEntry(CacheKey key, Material material, int sourceCrc)
            {
                Key = key;
                Material = material;
                SourceCrc = sourceCrc;
                ReferenceCount = 1;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetOnSubsystemRegistration()
        {
            ResetForTests();
        }

        internal static TextStudioTMPProductionMaterialLease Acquire(
            Material source,
            Shader shader,
            bool payloadFromTangent,
            TextStudio2DFieldPaddingTier paddingTier,
            bool writingSource = false, float pixelsPerEm = 0f)
        {
            if (source == null || !TextStudio2DShaderResources.IsUsable(shader))
                return default;

            CacheKey key = new CacheKey(
                source,
                shader,
                payloadFromTangent,
                paddingTier,
                writingSource, pixelsPerEm);
            if (s_entries.TryGetValue(key, out CacheEntry entry) &&
                entry.Material != null)
            {
                entry.ReferenceCount++;
                Synchronize(entry);
                return CreateLease(in key, entry.Material);
            }

            if (entry != null)
                s_entries.Remove(key);

            Material replacement = new Material(source)
            {
                name = source.name + " (Text Studio 2D Production)",
                shader = shader,
                hideFlags = HideFlags.HideAndDontSave
            };
            Configure(
                source,
                replacement,
                payloadFromTangent,
                paddingTier,
                writingSource, pixelsPerEm);
            entry = new CacheEntry(key, replacement, source.ComputeCRC());
            s_entries.Add(key, entry);
            return CreateLease(in key, replacement);
        }

        internal static void Synchronize(
            in TextStudioTMPProductionMaterialLease lease)
        {
            if (!lease.IsValid)
                return;
            CacheKey key = new CacheKey(
                lease.Source,
                lease.Shader,
                lease.PayloadFromTangent,
                lease.PaddingTier,
                lease.WritingSource, lease.PixelsPerEm);
            if (s_entries.TryGetValue(key, out CacheEntry entry) &&
                ReferenceEquals(entry.Material, lease.Replacement))
            {
                Synchronize(entry);
            }
        }

        internal static void Release(ref TextStudioTMPProductionMaterialLease lease)
        {
            if (!lease.HasIdentity)
            {
                lease = default;
                return;
            }

            CacheKey key = new CacheKey(
                lease.Source,
                lease.Shader,
                lease.PayloadFromTangent,
                lease.PaddingTier,
                lease.WritingSource, lease.PixelsPerEm);
            if (s_entries.TryGetValue(key, out CacheEntry entry) &&
                ReferenceEquals(entry.Material, lease.Replacement))
            {
                entry.ReferenceCount--;
                if (entry.ReferenceCount <= 0)
                {
                    s_entries.Remove(key);
                    Destroy(entry.Material);
                }
            }
            lease = default;
        }

        static void Synchronize(CacheEntry entry)
        {
            if (entry.Key.Source == null || entry.Material == null)
                return;

            int sourceCrc = entry.Key.Source.ComputeCRC();
            if (sourceCrc == entry.SourceCrc)
                return;

            Configure(
                entry.Key.Source,
                entry.Material,
                entry.Key.PayloadFromTangent,
                entry.Key.PaddingTier,
                entry.Key.WritingSource, entry.Key.PixelsPerEm);
            entry.SourceCrc = sourceCrc;
        }

        static void Configure(
            Material source,
            Material replacement,
            bool payloadFromTangent,
            TextStudio2DFieldPaddingTier paddingTier,
            bool writingSource, float pixelsPerEm)
        {
            Shader shader = replacement.shader;
            string name = replacement.name;
            replacement.CopyPropertiesFromMaterial(source);
            replacement.shader = shader;
            replacement.name = name;
            replacement.hideFlags = HideFlags.HideAndDontSave;
            replacement.SetFloat(TextStudioTMPMaterialCompatibility.NativeMaterialModelProperty,
                (float)TextStudioTMPMaterialCompatibility.Analyze(source).NativeMaterialModel);
            replacement.SetFloat(TextStudioGlyphFieldContract.PixelsPerEmProperty, pixelsPerEm);
            SetWhiteTextureWhenMissing(replacement, "_FaceTex");
            SetWhiteTextureWhenMissing(replacement, "_OutlineTex");
            if (replacement.HasProperty(PayloadEnabledProperty))
                replacement.SetFloat(PayloadEnabledProperty, 1f);
            if (replacement.HasProperty(PayloadFromTangentProperty))
            {
                replacement.SetFloat(
                    PayloadFromTangentProperty,
                    payloadFromTangent ? 1f : 0f);
            }
            if (replacement.HasProperty(WritingSourceEnabledProperty))
            {
                replacement.SetFloat(
                    WritingSourceEnabledProperty,
                    writingSource ? 1f : 0f);
            }
            if (replacement.HasProperty("_OutlineWidth"))
            {
                float sourceOutline = source.HasProperty("_OutlineWidth")
                    ? source.GetFloat("_OutlineWidth")
                    : 0f;
                replacement.SetFloat(
                    "_OutlineWidth",
                    Mathf.Max(
                        sourceOutline,
                        TextStudio2DFieldPadding.GetNormalizedCapacity(paddingTier)));
            }

            bool outlineEnabled = source.IsKeywordEnabled("OUTLINE_ON") ||
                                  source.HasProperty("_OutlineWidth") &&
                                  source.GetFloat("_OutlineWidth") > 0.0001f;
            if (outlineEnabled)
                replacement.EnableKeyword("OUTLINE_ON");
            else
                replacement.DisableKeyword("OUTLINE_ON");

            bool innerUnderlay = source.IsKeywordEnabled("UNDERLAY_INNER");
            bool outerUnderlay = source.IsKeywordEnabled("UNDERLAY_ON") && !innerUnderlay;
            if (innerUnderlay)
                replacement.EnableKeyword("UNDERLAY_INNER");
            else
                replacement.DisableKeyword("UNDERLAY_INNER");
            if (outerUnderlay)
                replacement.EnableKeyword("UNDERLAY_ON");
            else
                replacement.DisableKeyword("UNDERLAY_ON");
        }

        static void SetWhiteTextureWhenMissing(
            Material material,
            string propertyName)
        {
            if (material.HasProperty(propertyName) &&
                material.GetTexture(propertyName) == null)
            {
                material.SetTexture(propertyName, Texture2D.whiteTexture);
            }
        }

        static TextStudioTMPProductionMaterialLease CreateLease(
            in CacheKey key,
            Material material)
        {
            return new TextStudioTMPProductionMaterialLease
            {
                Source = key.Source,
                Replacement = material,
                Shader = key.Shader,
                PayloadFromTangent = key.PayloadFromTangent,
                PaddingTier = key.PaddingTier,
                WritingSource = key.WritingSource,
                PixelsPerEm = key.PixelsPerEm
            };
        }

        static void Destroy(Material material)
        {
            if (material == null)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(material);
            else
                UnityEngine.Object.DestroyImmediate(material);
        }

        internal static void ResetForTests()
        {
            foreach (KeyValuePair<CacheKey, CacheEntry> pair in s_entries)
                Destroy(pair.Value.Material);
            s_entries.Clear();
        }
    }
}
