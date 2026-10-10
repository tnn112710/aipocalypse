using System;
using System.Collections.Generic;

namespace TextStudio
{
    internal sealed class TextStudioTagExpansionRegistry
    {
        // Markup parsing hashes these keys: StringComparer.OrdinalIgnoreCase allocates an upper-case copy per lookup on Mono.
        readonly Dictionary<string, TextStudioTagExpansionEntry> _projectEntries =
            new Dictionary<string, TextStudioTagExpansionEntry>(TextStudioIgnoreCaseComparer.Instance);
        readonly Dictionary<string, TextStudioTagExpansionEntry> _runtimeEntries =
            new Dictionary<string, TextStudioTagExpansionEntry>(TextStudioIgnoreCaseComparer.Instance);
        uint _revision;

        public int Count
        {
            get
            {
                int count = _projectEntries.Count;
                foreach (string key in _runtimeEntries.Keys)
                {
                    if (!_projectEntries.ContainsKey(key))
                        count++;
                }
                return count;
            }
        }

        internal uint Revision => _revision;

        public bool TryGet(string tagId, out TextStudioTagExpansionEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(tagId))
                return false;

            string normalizedTagId = tagId.Trim();
            return _runtimeEntries.TryGetValue(normalizedTagId, out entry) ||
                _projectEntries.TryGetValue(normalizedTagId, out entry);
        }

        public void Register(TextStudioTagExpansionEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.TagId))
                return;

            string tagId = entry.TagId.Trim();
            if (_runtimeEntries.TryGetValue(tagId, out TextStudioTagExpansionEntry existing) &&
                ReferenceEquals(existing, entry))
            {
                return;
            }
            _runtimeEntries[tagId] = entry;
            _revision++;
        }

        public void RegisterSheet(TextStudioTagExpansionSheet sheet)
        {
            if (sheet == null || sheet.Entries == null)
                return;

            IReadOnlyList<TextStudioTagExpansionEntry> entries = sheet.Entries;
            for (int i = 0; i < entries.Count; i++)
                Register(entries[i]);
        }

        internal void ReplaceProjectEntries(
            Dictionary<string, TextStudioTagExpansionEntry> entries)
        {
            if (DictionaryMatches(_projectEntries, entries))
                return;

            _projectEntries.Clear();
            if (entries != null)
            {
                foreach (KeyValuePair<string, TextStudioTagExpansionEntry> pair in entries)
                    _projectEntries.Add(pair.Key, pair.Value);
            }
            _revision++;
        }

        public bool Unregister(string tagId)
        {
            if (string.IsNullOrWhiteSpace(tagId))
                return false;

            string normalizedTagId = tagId.Trim();
            if (_runtimeEntries.Remove(normalizedTagId))
            {
                _revision++;
                return true;
            }
            if (_projectEntries.Remove(normalizedTagId))
            {
                _revision++;
                return true;
            }
            return false;
        }

        public void ClearProjectEntries()
        {
            if (_projectEntries.Count == 0)
                return;

            _projectEntries.Clear();
            _revision++;
        }

        public IEnumerable<KeyValuePair<string, TextStudioTagExpansionEntry>> GetAll()
        {
            foreach (KeyValuePair<string, TextStudioTagExpansionEntry> pair in _projectEntries)
            {
                if (!_runtimeEntries.ContainsKey(pair.Key))
                    yield return pair;
            }

            foreach (KeyValuePair<string, TextStudioTagExpansionEntry> pair in _runtimeEntries)
                yield return pair;
        }

        public void Reset()
        {
            _projectEntries.Clear();
            _runtimeEntries.Clear();
            _revision++;
        }

        static bool DictionaryMatches(
            Dictionary<string, TextStudioTagExpansionEntry> current,
            Dictionary<string, TextStudioTagExpansionEntry> replacement)
        {
            int replacementCount = replacement != null ? replacement.Count : 0;
            if (current.Count != replacementCount)
                return false;

            if (replacement == null)
                return true;

            foreach (KeyValuePair<string, TextStudioTagExpansionEntry> pair in replacement)
            {
                if (!current.TryGetValue(
                        pair.Key,
                        out TextStudioTagExpansionEntry currentEntry) ||
                    !ReferenceEquals(currentEntry, pair.Value))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
