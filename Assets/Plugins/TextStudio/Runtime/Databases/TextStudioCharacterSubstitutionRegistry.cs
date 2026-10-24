using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    internal sealed class TextStudioCharacterSubstitutionRegistry
    {
        // Markup parsing hashes these keys: StringComparer.OrdinalIgnoreCase allocates an upper-case copy per lookup on Mono.
        readonly Dictionary<string, ITextStudioCharacterSubstitution> _builtInEntries =
            new Dictionary<string, ITextStudioCharacterSubstitution>(TextStudioIgnoreCaseComparer.Instance);
        readonly Dictionary<string, ITextStudioCharacterSubstitution> _entries =
            new Dictionary<string, ITextStudioCharacterSubstitution>(TextStudioIgnoreCaseComparer.Instance);
        readonly List<string> _removeBuffer = new List<string>(8);
        uint _revision;

        // Advances whenever a lookup can resolve differently, so compiled plans can tell whether their bindings still hold.
        internal uint Revision => _revision;

        public bool TryGet(string tagName, out ITextStudioCharacterSubstitution substitution)
        {
            return TryGet(tagName, out substitution, true);
        }

        public bool TryGetQuiet(string tagName, out ITextStudioCharacterSubstitution substitution)
        {
            return TryGet(tagName, out substitution, false);
        }

        bool TryGet(string tagName, out ITextStudioCharacterSubstitution substitution, bool warnOnMissing)
        {
            TextStudioRegistry.EnsureInitialized();

            if (!string.IsNullOrEmpty(tagName))
            {
                if (_entries.TryGetValue(tagName, out substitution))
                    return true;
                if (_builtInEntries.TryGetValue(tagName, out substitution))
                    return true;
            }

            substitution = null;
            if (warnOnMissing && TextStudioDefaultSettings.Instance.warnOnMissingTags)
                Debug.LogWarning("[TextStudio] Character Substitutions: No entry found for '" + tagName + "'.");
            return false;
        }

        public void Register(ITextStudioCharacterSubstitution substitution)
        {
            Register(substitution, false);
        }

        public void RegisterBuiltIn(ITextStudioCharacterSubstitution substitution)
        {
            Register(substitution, true);
        }

        void Register(ITextStudioCharacterSubstitution substitution, bool builtIn)
        {
            if (substitution == null || string.IsNullOrWhiteSpace(substitution.Id))
            {
                Debug.LogWarning("[TextStudio] Character Substitutions: Skipping null or empty substitution.");
                return;
            }

            Dictionary<string, ITextStudioCharacterSubstitution> target = builtIn ? _builtInEntries : _entries;
            _revision++;
            RegisterName(target, substitution.Id, substitution);

            TextStudioCharacterSubstitutionDefinition definition = substitution.Definition;
            if (definition == null)
                return;

            IReadOnlyList<string> aliases = definition.Aliases;
            for (int i = 0; i < aliases.Count; i++)
                RegisterName(target, aliases[i], substitution);
        }

        static void RegisterName(
            Dictionary<string, ITextStudioCharacterSubstitution> target,
            string tagName,
            ITextStudioCharacterSubstitution substitution)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return;

            target[tagName.Trim()] = substitution;
        }

        public bool Unregister(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            _removeBuffer.Clear();
            foreach (KeyValuePair<string, ITextStudioCharacterSubstitution> pair in _entries)
            {
                ITextStudioCharacterSubstitution substitution = pair.Value;
                if (substitution == null)
                    continue;

                if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(substitution.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    _removeBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < _removeBuffer.Count; i++)
                _entries.Remove(_removeBuffer[i]);

            bool removed = _removeBuffer.Count > 0;
            _removeBuffer.Clear();
            if (removed)
                _revision++;
            return removed;
        }

        public void Reset()
        {
            _builtInEntries.Clear();
            _entries.Clear();
            _removeBuffer.Clear();
            _revision++;
        }

        public IEnumerable<ITextStudioCharacterSubstitution> GetAll()
        {
            HashSet<string> seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ITextStudioCharacterSubstitution> pair in _builtInEntries)
            {
                ITextStudioCharacterSubstitution substitution = pair.Value;
                if (substitution == null ||
                    _entries.ContainsKey(pair.Key) ||
                    string.IsNullOrEmpty(substitution.Id) ||
                    !seenIds.Add(substitution.Id))
                {
                    continue;
                }

                yield return substitution;
            }

            foreach (KeyValuePair<string, ITextStudioCharacterSubstitution> pair in _entries)
            {
                ITextStudioCharacterSubstitution substitution = pair.Value;
                if (substitution == null ||
                    string.IsNullOrEmpty(substitution.Id) ||
                    !seenIds.Add(substitution.Id))
                {
                    continue;
                }

                yield return substitution;
            }
        }
    }
}
