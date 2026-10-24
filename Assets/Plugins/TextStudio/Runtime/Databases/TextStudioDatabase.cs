using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Lookup interface for typed registries with ownership-tier support.
    /// </summary>
    internal interface IDatabaseProvider<T>
    {
        bool TryGet(string id, out T item);
    }

    /// <summary>
    /// Case-insensitive registry with deterministic runtime, project, and built-in tiers.
    /// Project catalogs can be replaced without disturbing explicit runtime registrations.
    /// </summary>
    internal sealed class TextStudioDatabase<T> : IDatabaseProvider<T> where T : class
    {
        // Markup parsing hashes these keys: StringComparer.OrdinalIgnoreCase allocates an upper-case copy per lookup on Mono.
        readonly Dictionary<string, T> _builtInEntries = new Dictionary<string, T>(TextStudioIgnoreCaseComparer.Instance);
        readonly Dictionary<string, T> _projectEntries = new Dictionary<string, T>(TextStudioIgnoreCaseComparer.Instance);
        readonly Dictionary<string, T> _runtimeEntries = new Dictionary<string, T>(TextStudioIgnoreCaseComparer.Instance);
        bool _built;
        readonly string _databaseName;
        Func<IEnumerable<T>> _sourceProvider;
        Func<T, string> _keySelector;
        uint _revision;

        public TextStudioDatabase(string databaseName, Func<IEnumerable<T>> sourceProvider, Func<T, string> keySelector)
        {
            _databaseName = databaseName;
            _sourceProvider = sourceProvider;
            _keySelector = keySelector;
        }

        public int Count
        {
            get
            {
                EnsureBuilt();
                int count = _builtInEntries.Count;
                foreach (string key in _projectEntries.Keys)
                {
                    if (!_builtInEntries.ContainsKey(key))
                        count++;
                }
                foreach (string key in _runtimeEntries.Keys)
                {
                    if (!_builtInEntries.ContainsKey(key) &&
                        !_projectEntries.ContainsKey(key))
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        public bool TryGet(string id, out T item)
        {
            return TryGet(id, out item, true);
        }

        public bool TryGetQuiet(string id, out T item)
        {
            return TryGet(id, out item, false);
        }

        internal uint Revision => _revision;

        public bool TryGet<TItem>(string id, out TItem item) where TItem : class
        {
            if (TryGet(id, out T registered) && registered is TItem typed)
            {
                item = typed;
                return true;
            }

            item = null;
            return false;
        }

        public bool TryGetQuiet<TItem>(string id, out TItem item) where TItem : class
        {
            if (TryGet(id, out T registered, false) && registered is TItem typed)
            {
                item = typed;
                return true;
            }

            item = null;
            return false;
        }

        bool TryGet(string id, out T item, bool warnOnMissing)
        {
            EnsureBuilt();

            if (_runtimeEntries.TryGetValue(id, out item))
                return true;
            if (_projectEntries.TryGetValue(id, out item))
                return true;
            if (_builtInEntries.TryGetValue(id, out item))
                return true;

            if (warnOnMissing &&
                TextStudioRegistry.ShouldWarnOnMissingEntries &&
                TextStudioDefaultSettings.Instance.warnOnMissingTags)
            {
                Debug.LogWarning($"[TextStudio] {_databaseName}: No entry found for '{id}'. " +
                    $"Available: {string.Join(", ", GetAvailableKeys())}");
            }
            return false;
        }

        public void Register(string id, T item, bool warnOnReplace = true)
        {
            EnsureBuilt();

            if (_runtimeEntries.TryGetValue(id, out T existingItem))
            {
                if (ReferenceEquals(existingItem, item))
                    return;

                if (warnOnReplace)
                    Debug.LogWarning($"[TextStudio] {_databaseName}: Overwriting existing runtime entry '{id}'.");
            }
            _runtimeEntries[id] = item;
            _revision++;
        }

        public void RegisterProject(string id, T item)
        {
            EnsureBuilt();

            if (_projectEntries.TryGetValue(id, out T existingItem))
            {
                if (ReferenceEquals(existingItem, item))
                    return;

                Debug.LogWarning($"[TextStudio] {_databaseName}: Overwriting existing project entry '{id}'.");
            }
            _projectEntries[id] = item;
            _revision++;
        }

        public void RegisterBuiltIn(string id, T item)
        {
            EnsureBuilt();

            if (_builtInEntries.TryGetValue(id, out T existingItem))
            {
                if (ReferenceEquals(existingItem, item))
                    return;

                Debug.LogWarning($"[TextStudio] {_databaseName}: Overwriting existing built-in entry '{id}'.");
            }
            _builtInEntries[id] = item;
            _revision++;
        }

        public bool Unregister(string id)
        {
            EnsureBuilt();
            if (_runtimeEntries.Remove(id))
            {
                _revision++;
                return true;
            }
            if (_projectEntries.Remove(id))
            {
                _revision++;
                return true;
            }
            return false;
        }

        internal bool UnregisterRuntime(string id)
        {
            EnsureBuilt();
            if (string.IsNullOrEmpty(id) || !_runtimeEntries.Remove(id)) return false;
            _revision++;
            return true;
        }

        public void ClearProjectEntries()
        {
            EnsureBuilt();
            if (_projectEntries.Count == 0)
                return;

            _projectEntries.Clear();
            _revision++;
        }

        internal void ReplaceProjectEntries(Dictionary<string, T> entries)
        {
            EnsureBuilt();
            if (DictionaryMatches(_projectEntries, entries))
                return;

            _projectEntries.Clear();
            if (entries != null)
            {
                foreach (KeyValuePair<string, T> pair in entries)
                    _projectEntries.Add(pair.Key, pair.Value);
            }
            _revision++;
        }

        public void Rebuild()
        {
            _runtimeEntries.Clear();
            _projectEntries.Clear();
            _built = false;
            _revision++;
            EnsureBuilt();
        }

        public IEnumerable<KeyValuePair<string, T>> GetAll()
        {
            EnsureBuilt();
            foreach (KeyValuePair<string, T> pair in _builtInEntries)
            {
                if (!_runtimeEntries.ContainsKey(pair.Key) &&
                    !_projectEntries.ContainsKey(pair.Key))
                {
                    yield return pair;
                }
            }

            foreach (KeyValuePair<string, T> pair in _projectEntries)
            {
                if (!_runtimeEntries.ContainsKey(pair.Key))
                    yield return pair;
            }

            foreach (KeyValuePair<string, T> pair in _runtimeEntries)
                yield return pair;
        }

        // The first entry of GetAll's order whose value matches, without allocating GetAll's enumerator. Markup parsing asks this
        // for every tag that is not a registered key.
        internal bool TryFindFirst(Func<T, string, bool> match, string name, out T item)
        {
            EnsureBuilt();
            foreach (KeyValuePair<string, T> pair in _builtInEntries)
            {
                if (!_runtimeEntries.ContainsKey(pair.Key) &&
                    !_projectEntries.ContainsKey(pair.Key) &&
                    match(pair.Value, name))
                {
                    item = pair.Value;
                    return true;
                }
            }

            foreach (KeyValuePair<string, T> pair in _projectEntries)
            {
                if (!_runtimeEntries.ContainsKey(pair.Key) && match(pair.Value, name))
                {
                    item = pair.Value;
                    return true;
                }
            }

            foreach (KeyValuePair<string, T> pair in _runtimeEntries)
            {
                if (match(pair.Value, name))
                {
                    item = pair.Value;
                    return true;
                }
            }

            item = null;
            return false;
        }

        internal bool IsRuntime(string id, T item)
        {
            EnsureBuilt();
            return !string.IsNullOrEmpty(id) && _runtimeEntries.TryGetValue(id, out T current) && ReferenceEquals(current, item);
        }

        public bool IsBuiltIn(string id, T item)
        {
            EnsureBuilt();
            return !string.IsNullOrWhiteSpace(id) &&
                _builtInEntries.TryGetValue(id, out T builtIn) &&
                ReferenceEquals(builtIn, item) &&
                !_projectEntries.ContainsKey(id) &&
                !_runtimeEntries.ContainsKey(id);
        }

        internal bool ContainsBuiltIn(string id, T item)
        {
            EnsureBuilt();
            return !string.IsNullOrWhiteSpace(id) &&
                _builtInEntries.TryGetValue(id, out T builtIn) &&
                ReferenceEquals(builtIn, item);
        }

        void EnsureBuilt()
        {
            if (_built) return;
            _built = true;

            if (_sourceProvider == null) return;

            foreach (T item in _sourceProvider())
            {
                string key = _keySelector(item);
                if (string.IsNullOrEmpty(key))
                {
                    Debug.LogWarning($"[TextStudio] {_databaseName}: Skipping entry with null/empty key.");
                    continue;
                }
                if (_runtimeEntries.ContainsKey(key))
                {
                    Debug.LogWarning($"[TextStudio] {_databaseName}: Duplicate key '{key}', keeping first.");
                    continue;
                }
                _runtimeEntries[key] = item;
            }
        }

        IEnumerable<string> GetAvailableKeys()
        {
            foreach (string key in _builtInEntries.Keys)
            {
                if (!_runtimeEntries.ContainsKey(key) &&
                    !_projectEntries.ContainsKey(key))
                {
                    yield return key;
                }
            }

            foreach (string key in _projectEntries.Keys)
            {
                if (!_runtimeEntries.ContainsKey(key))
                    yield return key;
            }

            foreach (string key in _runtimeEntries.Keys)
                yield return key;
        }

        /// <summary>
        /// Clear on domain reload.
        /// </summary>
        public void Reset()
        {
            _builtInEntries.Clear();
            _projectEntries.Clear();
            _runtimeEntries.Clear();
            _built = false;
            _revision++;
        }

        static bool DictionaryMatches(
            Dictionary<string, T> current,
            Dictionary<string, T> replacement)
        {
            int replacementCount = replacement != null ? replacement.Count : 0;
            if (current.Count != replacementCount)
                return false;

            if (replacement == null)
                return true;

            foreach (KeyValuePair<string, T> pair in replacement)
            {
                if (!current.TryGetValue(pair.Key, out T currentItem) ||
                    !ReferenceEquals(currentItem, pair.Value))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
