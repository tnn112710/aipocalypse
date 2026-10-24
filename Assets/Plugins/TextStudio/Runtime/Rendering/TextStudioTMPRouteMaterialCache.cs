using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    internal struct TextStudioTMPRouteMaterialLease
    {
        internal Material Source;
        internal Material Replacement;
        internal float PixelsPerEm;
        internal TextStudio2DTextMeshProStyle Style;

        internal bool HasIdentity => !ReferenceEquals(Source, null) && !ReferenceEquals(Replacement, null);
        internal bool IsValid => HasIdentity && Source != null && Replacement != null;
    }

    /// <summary>
    /// Reference-counted TextMeshPro route materials (<see cref="TextStudio2DTextMeshProRoute"/>): copies of a source TMP
    /// material with one style's face, outline and underlay values. Texts with the same source material, atlas density and
    /// style share one material, so their glyphs batch like TextMeshPro's own; the last release destroys it.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset destroys owned materials before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioTMPRouteMaterialCache
    {
        /// <summary>Name suffix of every route material; orphans and TMP instance copies are recognized by it.</summary>
        internal const string NameSuffix = " (Text Studio TMP Route)";

        static readonly Dictionary<CacheKey, CacheEntry> s_entries = new Dictionary<CacheKey, CacheEntry>();
        static readonly Dictionary<Material, CacheEntry> s_byMaterial = new Dictionary<Material, CacheEntry>(new MaterialReferenceComparer());

        internal static int ActiveEntryCount => s_entries.Count;

        readonly struct CacheKey : IEquatable<CacheKey>
        {
            internal readonly Material Source;
            internal readonly float PixelsPerEm;
            internal readonly TextStudio2DTextMeshProStyle Style;

            internal CacheKey(Material source, float pixelsPerEm, in TextStudio2DTextMeshProStyle style)
            {
                Source = source;
                PixelsPerEm = pixelsPerEm;
                Style = style;
            }

            public bool Equals(CacheKey other) =>
                ReferenceEquals(Source, other.Source) && PixelsPerEm.Equals(other.PixelsPerEm) && Style.Equals(other.Style);

            public override bool Equals(object obj) => obj is CacheKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = !ReferenceEquals(Source, null) ? RuntimeHelpers.GetHashCode(Source) : 0;
                    hash = hash * 397 ^ PixelsPerEm.GetHashCode();
                    return hash * 397 ^ Style.GetHashCode();
                }
            }
        }

        sealed class CacheEntry
        {
            internal readonly CacheKey Key;
            internal readonly Material Material;
            internal int ReferenceCount;
            internal int SourceCrc;
            // Whether the material draws the style on the source values SourceCrc identifies.
            internal bool Resolved;

            internal CacheEntry(CacheKey key, Material material, int sourceCrc)
            {
                Key = key;
                Material = material;
                SourceCrc = sourceCrc;
                Resolved = true;
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

        /// <summary>Shares or creates the route material of a style on a source material; default when the source cannot draw it.</summary>
        internal static TextStudioTMPRouteMaterialLease Acquire(Material source, float pixelsPerEm, in TextStudio2DTextMeshProStyle style)
        {
            if (source == null || !style.IsValid)
                return default;
            CacheKey key = new CacheKey(source, pixelsPerEm, in style);
            if (s_entries.TryGetValue(key, out CacheEntry entry) && entry.Material != null)
            {
                // A source edited beyond what the route draws keeps its last values for current leases and takes no new text.
                if (!Synchronize(entry))
                    return default;
                entry.ReferenceCount++;
                return CreateLease(in key, entry.Material);
            }
            if (entry != null)
                Remove(entry);

            if (!TextStudio2DTextMeshProRoute.TryResolveMaterial(in style, source, pixelsPerEm, out TextStudio2DTextMeshProMaterialValues values))
                return default;
            Material replacement = new Material(source)
            {
                name = source.name + NameSuffix,
                hideFlags = HideFlags.HideAndDontSave
            };
            TextStudio2DTextMeshProRoute.Apply(replacement, in values);
            entry = new CacheEntry(key, replacement, source.ComputeCRC());
            s_entries.Add(key, entry);
            s_byMaterial[replacement] = entry;
            return CreateLease(in key, replacement);
        }

        /// <summary>
        /// Reapplies the style after its source material changed, for example when its TMP material was edited. False when the
        /// lease is gone or its source can no longer draw the style; the material then keeps its last values.
        /// </summary>
        internal static bool Synchronize(in TextStudioTMPRouteMaterialLease lease) =>
            lease.IsValid && s_byMaterial.TryGetValue(lease.Replacement, out CacheEntry entry) && Synchronize(entry);

        internal static void Release(ref TextStudioTMPRouteMaterialLease lease)
        {
            if (lease.HasIdentity && s_byMaterial.TryGetValue(lease.Replacement, out CacheEntry entry) &&
                ReferenceEquals(entry.Material, lease.Replacement) && --entry.ReferenceCount <= 0)
            {
                Remove(entry);
                Destroy(entry.Material);
            }
            lease = default;
        }

        /// <summary>Whether a material is a live route material of this cache; a lookup without allocation.</summary>
        internal static bool IsRegistered(Material material) => !ReferenceEquals(material, null) && s_byMaterial.ContainsKey(material);

        /// <summary>
        /// Whether a material is a route material a code reload left behind: it keeps its hidden flags and name. Visible
        /// materials, such as TMP font materials, answer without reading the name; hidden ones, such as TMP's fallback and
        /// stencil materials, allocate it, so only restore and serialization paths ask.
        /// </summary>
        internal static bool IsOrphan(Material material) =>
            material != null && (material.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave && HasRouteName(material);

        /// <summary>Whether a material is a route material or a copy of one, such as TMP's instance material; reads the name.</summary>
        internal static bool HasRouteName(Material material) =>
            material != null && material.name.IndexOf(NameSuffix, StringComparison.Ordinal) >= 0;

        static bool Synchronize(CacheEntry entry)
        {
            Material source = entry.Key.Source;
            if (source == null || entry.Material == null)
                return false;
            int sourceCrc = source.ComputeCRC();
            if (sourceCrc == entry.SourceCrc)
                return entry.Resolved;
            entry.SourceCrc = sourceCrc;
            // A source that can no longer draw the style keeps its last values until its texts leave the route.
            entry.Resolved = TextStudio2DTextMeshProRoute.TryResolveMaterial(in entry.Key.Style, source, entry.Key.PixelsPerEm,
                out TextStudio2DTextMeshProMaterialValues values);
            if (!entry.Resolved)
                return false;
            Material replacement = entry.Material;
            string name = replacement.name;
            if (replacement.shader != source.shader)
                replacement.shader = source.shader;
            replacement.CopyPropertiesFromMaterial(source);
            replacement.shaderKeywords = source.shaderKeywords;
            replacement.name = name;
            replacement.hideFlags = HideFlags.HideAndDontSave;
            TextStudio2DTextMeshProRoute.Apply(replacement, in values);
            return true;
        }

        static void Remove(CacheEntry entry)
        {
            s_entries.Remove(entry.Key);
            if (!ReferenceEquals(entry.Material, null))
                s_byMaterial.Remove(entry.Material);
        }

        static TextStudioTMPRouteMaterialLease CreateLease(in CacheKey key, Material material)
        {
            return new TextStudioTMPRouteMaterialLease
            {
                Source = key.Source,
                Replacement = material,
                PixelsPerEm = key.PixelsPerEm,
                Style = key.Style
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
            s_byMaterial.Clear();
        }

        sealed class MaterialReferenceComparer : IEqualityComparer<Material>
        {
            public bool Equals(Material x, Material y) => ReferenceEquals(x, y);
            public int GetHashCode(Material obj) => RuntimeHelpers.GetHashCode(obj);
        }
    }
}
