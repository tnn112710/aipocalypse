using System;
using System.Collections.Generic;
using System.IO;
using ImpossibleRobert.Common;

namespace TextStudio.Editor
{
    /// <summary>Presentation-only filtering. Never use a visible subset to select preparation work.</summary>
    internal static class TextStudioPreparationView
    {
        internal static string ProductId(TextStudioPreparationItem item) => !string.IsNullOrWhiteSpace(item.Presentation?.ProductId) ? item.Presentation.ProductId : "other";
        internal static string ProductName(TextStudioPreparationItem item) => !string.IsNullOrWhiteSpace(item.Presentation?.ProductName) ? item.Presentation.ProductName : "Other preparation";
        internal static string KindId(TextStudioPreparationItem item) => !string.IsNullOrWhiteSpace(item.Presentation?.KindId) ? item.Presentation.KindId : item.Provider;
        internal static string KindName(TextStudioPreparationItem item) => !string.IsNullOrWhiteSpace(item.Presentation?.KindName) ? item.Presentation.KindName : item.Provider;
        internal static bool Pending(TextStudioPreparationItem item) => item.State == TextStudioPreparationState.NeedsAttention || item.State == TextStudioPreparationState.RuntimeGenerated;
        internal static bool CanPrepare(TextStudioPreparationItem item) => Pending(item) && !item.RequiresManualAction;
        /// <summary>New font sources that the project declined to save cannot be prepared, so they need a change instead.</summary>
        internal static bool Blocked(TextStudioPreparationItem item, bool embeddingDenied) => embeddingDenied && item.RequiresFontEmbedding && Pending(item);
        internal static bool Preparable(TextStudioPreparationItem item, bool embeddingDenied) => CanPrepare(item) && !Blocked(item, embeddingDenied);
        internal static bool NotReferenced(TextStudioPreparationItem item) => item.BuildReference == TextStudioPreparationReference.NotFound || item.State == TextStudioPreparationState.Unused;
        /// <summary>The scene or file that owns the text: live open-scene text, or saved build content.</summary>
        internal static string Location(TextStudioPreparationItem item)
        {
            if (!string.IsNullOrEmpty(item.SourcePath)) return Path.GetFileNameWithoutExtension(item.SourcePath);
            return item.SourceText != null ? item.SourceText.gameObject.scene.name : string.Empty;
        }
        internal static bool Matches(TextStudioPreparationItem item, bool saved, bool includeUnreferenced, string product, string kind, int condition, string query)
        {
            if (saved && !includeUnreferenced && NotReferenced(item)) return false;
            if (!string.IsNullOrEmpty(product) && ProductId(item) != product) return false;
            if (!string.IsNullOrEmpty(kind) && KindId(item) != kind) return false;
            if (condition == 1 && !Pending(item)) return false;
            if (condition == 2 && item.State != TextStudioPreparationState.Ready) return false;
            return MatchesQuery(item, query);
        }
        internal static bool MatchesQuery(TextStudioPreparationItem item, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            string value = query.Trim();
            return Contains(item.Target, value) || Contains(item.SourcePath, value) || Contains(Location(item), value) || Contains(ProductName(item), value) || Contains(KindName(item), value);
        }
        static bool Contains(string source, string value) => source != null && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Stable row identities: a live text instance, or a saved build file and text name with its occurrence order.</summary>
        internal static string[] Keys(IReadOnlyList<TextStudioPreparationItem> items)
        {
            string[] keys = new string[items.Count];
            Dictionary<string, int> occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < items.Count; i++)
            {
                TextStudioPreparationItem item = items[i];
                string owner = !string.IsNullOrEmpty(item.SourcePath) ? item.SourcePath + "/" + item.Target
                    : item.SourceText != null ? "#" + item.SourceText.GetStableId() : "?" + item.Target;
                string key = KindId(item) + "|" + owner;
                occurrences.TryGetValue(key, out int occurrence);
                occurrences[key] = occurrence + 1;
                keys[i] = key + "|" + occurrence;
            }
            return keys;
        }

        /// <summary>Combines live open-scene checks with saved build-content checks outside open scenes.
        /// Rows of an active operation replace matching rows in place, so progress never reorders the list.</summary>
        internal static List<TextStudioPreparationItem> Combine(IReadOnlyList<TextStudioPreparationItem> live, IReadOnlyList<TextStudioPreparationItem> build,
            ICollection<string> openScenes, IReadOnlyList<TextStudioPreparationItem> operation, out string[] keys)
        {
            List<TextStudioPreparationItem> rows = new List<TextStudioPreparationItem>(live);
            foreach (TextStudioPreparationItem item in build)
                if (string.IsNullOrEmpty(item.SourcePath) || !openScenes.Contains(item.SourcePath)) rows.Add(item);
            keys = Keys(rows);
            if (operation == null || operation.Count == 0) return rows;
            string[] active = Keys(operation);
            Dictionary<string, int> index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Length; i++) index[keys[i]] = i;
            List<string> extra = new List<string>();
            for (int i = 0; i < operation.Count; i++)
            {
                if (index.TryGetValue(active[i], out int row)) rows[row] = operation[i];
                else if (operation[i].SourceText != null || !string.IsNullOrEmpty(operation[i].SourcePath)) { rows.Add(operation[i]); extra.Add(active[i]); }
            }
            if (extra.Count > 0)
            {
                string[] combined = new string[keys.Length + extra.Count];
                keys.CopyTo(combined, 0);
                extra.CopyTo(combined, keys.Length);
                keys = combined;
            }
            return rows;
        }
    }

    /// <summary>Overview counts. Can-prepare work excludes manual steps, which preparation cannot resolve. Optional items that
    /// cannot be prepared are generated at runtime and are excluded from progress, so prepared projects can reach completion.</summary>
    internal readonly struct TextStudioPreparationSummary
    {
        internal readonly int Total, Prepared, Preparing, CanPrepare, Required, Manual, RuntimeOnly;

        internal TextStudioPreparationSummary(IEnumerable<TextStudioPreparationItem> items, bool embeddingDenied = false)
        {
            Total = Prepared = Preparing = CanPrepare = Required = Manual = RuntimeOnly = 0;
            foreach (TextStudioPreparationItem item in items)
            {
                if (item.State == TextStudioPreparationState.Unused) continue;
                Total++;
                if (item.State == TextStudioPreparationState.Ready) Prepared++;
                else if (item.State == TextStudioPreparationState.Updating) Preparing++;
                else if (TextStudioPreparationView.Blocked(item, embeddingDenied) || item.RequiresManualAction && item.Required) Manual++;
                else if (item.RequiresManualAction) RuntimeOnly++;
                else
                {
                    CanPrepare++;
                    if (item.Required) Required++;
                }
            }
        }

        /// <summary>Items that preparation or a change from the user can complete.</summary>
        internal int Preparable => Total - RuntimeOnly;
        internal float Progress => Preparable == 0 ? 0f : (float)Prepared / Preparable;
        internal bool Complete => Preparable > 0 && Prepared == Preparable;
    }
}
