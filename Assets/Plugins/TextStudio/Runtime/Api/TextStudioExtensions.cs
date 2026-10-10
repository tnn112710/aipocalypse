namespace TextStudio
{
    /// <summary>
    /// Registers and resolves extension tags, character substitutions, and tag expansions.
    /// </summary>
    public static class TextStudioExtensions
    {
        /// <summary>Register an extension tag definition.</summary>
        public static bool RegisterTag(TextStudioExtensionTagDefinition definition)
        {
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.ExtensionTags.Register(definition);
        }

        /// <summary>Resolve an extension tag definition by identifier or alias.</summary>
        public static bool TryGetTag(string id, out TextStudioExtensionTagDefinition definition)
        {
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.ExtensionTags.TryGet(id, out definition);
        }

        /// <summary>Remove every extension tag registered by one owner.</summary>
        public static void UnregisterTags(string ownerId)
        {
            TextStudioRegistry.ExtensionTags.UnregisterOwner(ownerId);
        }

        /// <summary>Register a presentation-layer character substitution in the runtime tier.</summary>
        public static void RegisterCharacterSubstitution(ITextStudioCharacterSubstitution substitution)
        {
            if (substitution == null || string.IsNullOrEmpty(substitution.Id)) return;
            TextStudioRegistry.EnsureInitialized();
            TextStudioRegistry.CharacterSubstitutions.Register(substitution);
        }

        /// <summary>Resolve a character substitution by identifier or alias without logging.</summary>
        public static bool TryGetCharacterSubstitution(
            string id,
            out ITextStudioCharacterSubstitution substitution)
        {
            return TextStudioRegistry.CharacterSubstitutions.TryGetQuiet(id, out substitution);
        }

        /// <summary>Remove the highest-priority user-owned character substitution.</summary>
        public static bool UnregisterCharacterSubstitution(string id)
        {
            return TextStudioRegistry.CharacterSubstitutions.Unregister(id);
        }

        /// <summary>Register every tag expansion entry from a sheet in the runtime tier.</summary>
        public static void RegisterTagExpansionSheet(TextStudioTagExpansionSheet sheet)
        {
            TextStudioRegistry.TagExpansions.RegisterSheet(sheet);
        }

        /// <summary>Register one tag expansion entry in the runtime tier.</summary>
        public static void RegisterTagExpansion(TextStudioTagExpansionEntry entry)
        {
            TextStudioRegistry.TagExpansions.Register(entry);
        }

        /// <summary>Remove the highest-priority user-owned tag expansion.</summary>
        public static bool UnregisterTagExpansion(string tagId)
        {
            return TextStudioRegistry.TagExpansions.Unregister(tagId);
        }
    }
}
