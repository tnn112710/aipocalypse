using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Unified registry for effects, actions, curves, playbacks, extension tags, and timings.
    ///
    /// Built-in defaults are registered from code during runtime initialization. Project
    /// catalogs are loaded from the canonical Text Studio project settings resource before
    /// the first scene becomes active. Other definitions can be registered explicitly at
    /// runtime by calling Register*.
    ///
    /// Lookup is case-insensitive and uses deterministic ownership tiers:
    /// runtime override, project catalog, then add-on/core built-in.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Subsystem registration resets every registry under its lock before built-ins are registered again.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioRegistry
    {
        public static readonly TextStudioDatabase<ITextStudioEffectDefinition> Effects =
            new TextStudioDatabase<ITextStudioEffectDefinition>("Effects", null, e => e.Id);

        public static readonly TextStudioDatabase<TextStudio2DStyleAsset> AppearanceStyles =
            new TextStudioDatabase<TextStudio2DStyleAsset>("Appearance Styles", null, a => a.Id);

        public static readonly TextStudioDatabase<ITextStudioAction> Actions =
            new TextStudioDatabase<ITextStudioAction>("Actions", null, a => a.Id);

        public static readonly TextStudioDatabase<ITextStudioCurve> Curves =
            new TextStudioDatabase<ITextStudioCurve>("Curves", null, GetCurveId);

        public static readonly TextStudioDatabase<IEffectPlayback> Playbacks =
            new TextStudioDatabase<IEffectPlayback>("Playbacks", null, GetPlaybackId);

        public static readonly TextStudioDatabase<TypingTimingsBase> Timings =
            new TextStudioDatabase<TypingTimingsBase>("Timings", null, t => t.name);

        public static readonly TextStudioExtensionTagRegistry ExtensionTags =
            new TextStudioExtensionTagRegistry();

        public static readonly TextStudioCharacterSubstitutionRegistry CharacterSubstitutions =
            new TextStudioCharacterSubstitutionRegistry();

        public static readonly TextStudioTagExpansionRegistry TagExpansions =
            new TextStudioTagExpansionRegistry();

        static bool s_initialized;
        static bool s_projectCatalogActivationPending;
        static readonly object s_lock = new object();
        static readonly List<ITextStudioBuiltIn> s_allBuiltIns = new List<ITextStudioBuiltIn>(64);

        internal static event Action ProjectCatalogChanged;
        internal static bool ShouldWarnOnMissingEntries => !s_projectCatalogActivationPending;

        /// <summary>Read-only view of every registered built-in for editor tooling.</summary>
        public static IReadOnlyList<ITextStudioBuiltIn> AllBuiltIns => s_allBuiltIns;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeSession()
        {
            ResetAll();
            s_projectCatalogActivationPending = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void InitializeRuntimeSession()
        {
            EnsureInitialized();
        }

        public static void EnsureInitialized()
        {
            EnsureInitialized(true);
        }

        static void EnsureInitialized(bool restoreActiveProjectCatalog)
        {
            if (s_initialized) return;
            lock (s_lock)
            {
                if (s_initialized) return;
                RegisterBuiltIns();
                s_initialized = true;
                if (restoreActiveProjectCatalog &&
                    TextStudioProjectSettings.Active != null)
                {
                    ReplaceProjectCatalogEntries(
                        TextStudioProjectSettings.Active);
                }
            }
        }

        static void RegisterBuiltIns()
        {
            s_allBuiltIns.Clear();

            TextStudio2DMaterialProperties.EnsureRegistered();

            foreach (CodeCurve curve in BuiltInCurves.All())
            {
                Curves.RegisterBuiltIn(curve.Id, curve);
                RegisterBuiltInMetadata(curve);
            }

            foreach (CodePlayback playback in BuiltInPlaybacks.All())
            {
                Playbacks.RegisterBuiltIn(playback.Id, playback);
                RegisterBuiltInMetadata(playback);
            }

            foreach (CodeEffectBase effect in BuiltInEffects.All())
            {
                Effects.RegisterBuiltIn(effect.Id, effect);
                RegisterBuiltInMetadata(effect);
            }

            foreach (CodeActionBase action in BuiltInActions.All())
            {
                Actions.RegisterBuiltIn(action.Id, action);
                RegisterBuiltInMetadata(action);
            }

            CharacterSubstitutions.RegisterBuiltIn(BuiltInCharacterSubstitutions.ScrambleSubstitution);

            foreach (CodeTimings timing in BuiltInTimings.All())
            {
                RegisterBuiltInMetadata(timing);
            }

            foreach (TextStudioBuiltInTypewriterPreset typewriterPreset in BuiltInTypewriterPresets.All())
            {
                RegisterBuiltInMetadata(typewriterPreset);
            }
        }

        internal static void RegisterAddOnBuiltInEffect(ITextStudioEffectDefinition effect)
        {
            if (effect == null || string.IsNullOrWhiteSpace(effect.Id))
                return;

            EnsureInitialized();
            Effects.RegisterBuiltIn(effect.Id, effect);
            RegisterBuiltInMetadata(effect as ITextStudioBuiltIn);
        }

        internal static bool IsBuiltInEffectRegistered(
            string id,
            ITextStudioEffectDefinition effect)
        {
            EnsureInitialized();
            return Effects.ContainsBuiltIn(id, effect);
        }

        /// <summary>
        /// Force a full reset. Used on domain reload and in tests. User registrations are lost.
        /// </summary>
        public static void ResetAll()
        {
            Effects.Reset();
            AppearanceStyles.Reset();
            Actions.Reset();
            Curves.Reset();
            Playbacks.Reset();
            Timings.Reset();
            ExtensionTags.Reset();
            CharacterSubstitutions.Reset();
            TagExpansions.Reset();
            TextStudioRenderPropertyRegistry.ResetForRuntimeSession();
            TextStudio2DMaterialProperties.ResetForRuntimeSession();
            s_initialized = false;
            s_allBuiltIns.Clear();
        }

        internal static void ReplaceProjectCatalog(TextStudioProjectSettings settings)
        {
            EnsureInitialized(false);
            ReplaceProjectCatalogEntries(settings);
        }

        static void ReplaceProjectCatalogEntries(TextStudioProjectSettings settings)
        {
            Dictionary<string, ITextStudioEffectDefinition> effects =
                new Dictionary<string, ITextStudioEffectDefinition>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TextStudio2DStyleAsset> appearances = TextStudioStyleCatalog.BuildProjectEntries(
                settings != null ? settings.appearanceStyleCatalog : null, style => style.Id, "2D Styles");
            Dictionary<string, ITextStudioAction> actions =
                new Dictionary<string, ITextStudioAction>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ITextStudioCurve> curves =
                new Dictionary<string, ITextStudioCurve>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, IEffectPlayback> playbacks =
                new Dictionary<string, IEffectPlayback>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, TextStudioTagExpansionEntry> tagExpansions =
                new Dictionary<string, TextStudioTagExpansionEntry>(StringComparer.OrdinalIgnoreCase);

            if (settings != null)
            {
                AddProjectEntries(
                    effects,
                    settings.effectCatalog,
                    effect => effect != null ? effect.Id : string.Empty,
                    effect => effect,
                    "Effects");
                AddProjectEntries(
                    actions,
                    settings.actionCatalog,
                    action => action != null ? action.Id : string.Empty,
                    action => action,
                    "Actions");
                AddProjectEntries(
                    curves,
                    settings.curveCatalog,
                    curve => curve != null ? curve.name : string.Empty,
                    curve => curve,
                    "Curves");
                AddProjectEntries(
                    playbacks,
                    settings.playbackCatalog,
                    playback => playback != null ? playback.name : string.Empty,
                    playback => playback,
                    "Playbacks");
                AddProjectTagExpansions(tagExpansions, settings.tagExpansionSheets);
            }

            Effects.ReplaceProjectEntries(effects);
            AppearanceStyles.ReplaceProjectEntries(appearances);
            Actions.ReplaceProjectEntries(actions);
            Curves.ReplaceProjectEntries(curves);
            Playbacks.ReplaceProjectEntries(playbacks);
            TagExpansions.ReplaceProjectEntries(tagExpansions);
        }

        internal static void ClearProjectCatalog()
        {
            Effects.ClearProjectEntries();
            AppearanceStyles.ClearProjectEntries();
            Actions.ClearProjectEntries();
            Curves.ClearProjectEntries();
            Playbacks.ClearProjectEntries();
            TagExpansions.ClearProjectEntries();
        }

        internal static void NotifyProjectCatalogChanged()
        {
            s_projectCatalogActivationPending = false;
            ProjectCatalogChanged?.Invoke();
            TextStudio2DStyles.NotifyChanged();
        }

        internal static void BeginProjectCatalogActivation()
        {
            s_projectCatalogActivationPending = true;
        }

        static void RegisterBuiltInMetadata(ITextStudioBuiltIn builtIn)
        {
            if (builtIn == null)
                return;

            for (int i = 0; i < s_allBuiltIns.Count; i++)
            {
                if (ReferenceEquals(s_allBuiltIns[i], builtIn))
                    return;
            }
            s_allBuiltIns.Add(builtIn);
        }

        static void AddProjectEntries<TAsset, TEntry>(
            Dictionary<string, TEntry> destination,
            IReadOnlyList<TAsset> source,
            Func<TAsset, string> keySelector,
            Func<TAsset, TEntry> valueSelector,
            string databaseName)
            where TAsset : class
            where TEntry : class
        {
            if (source == null)
                return;

            for (int i = 0; i < source.Count; i++)
            {
                TAsset entry = source[i];
                if (entry == null)
                    continue;

                string id = keySelector(entry);
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                if (destination.ContainsKey(id))
                {
                    Debug.LogWarning(
                        $"[TextStudio] {databaseName}: Duplicate project entry '{id}', using the later catalog item.");
                }
                destination[id] = valueSelector(entry);
            }
        }

        static void AddProjectTagExpansions(
            Dictionary<string, TextStudioTagExpansionEntry> destination,
            IReadOnlyList<TextStudioTagExpansionSheet> sheets)
        {
            if (sheets == null)
                return;

            for (int sheetIndex = 0; sheetIndex < sheets.Count; sheetIndex++)
            {
                TextStudioTagExpansionSheet sheet = sheets[sheetIndex];
                if (sheet == null || sheet.Entries == null)
                    continue;

                IReadOnlyList<TextStudioTagExpansionEntry> entries = sheet.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    TextStudioTagExpansionEntry entry = entries[entryIndex];
                    if (entry == null || string.IsNullOrWhiteSpace(entry.TagId))
                        continue;

                    string id = entry.TagId.Trim();
                    if (destination.ContainsKey(id))
                    {
                        Debug.LogWarning(
                            $"[TextStudio] Tag Expansions: Duplicate project entry '{id}', using the later catalog item.");
                    }
                    destination[id] = entry;
                }
            }
        }

        static string GetCurveId(ITextStudioCurve c)
        {
            if (c is ITextStudioBuiltIn b) return b.Id;
            if (c is UnityEngine.Object o && !string.IsNullOrEmpty(o.name)) return o.name;
            return c?.GetType().Name ?? "";
        }

        static string GetPlaybackId(IEffectPlayback p)
        {
            if (p is ITextStudioBuiltIn b) return b.Id;
            if (p is UnityEngine.Object o && !string.IsNullOrEmpty(o.name)) return o.name;
            return p?.GetType().Name ?? "";
        }
    }

}
