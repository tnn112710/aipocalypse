using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TextStudio.Editor
{
    /// <summary>Add-on metadata for a renderer's named styles. Runtime programs remain in their owning package.</summary>
    internal interface ITextStudioAppearanceAuthoringProvider
    {
        TextStudioRenderDomain Domain { get; }
        TextStudioTagDefinition ReferenceTagDefinition { get; }
        ulong Revision { get; }
        uint GetOwnerRevision(TextStudioText owner);
        void Bind(TextStudioTagAnalysisContext context, TextStudioText owner, string source, TextStudioDiagnosticReport report, TextStudioTagAnalysisContext previous);
        IReadOnlyList<TextStudioTagBrowserItem> AtCaret(TextStudioTagAnalysisContext context, int caret);
    }

#if UNITY_6000_7_OR_NEWER
    // Authoring providers and context caches belong to the editor session.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioAppearanceAuthoringProviders
    {
        sealed class Composition
        {
            internal IReadOnlyList<TextStudioTagBrowserItem>[] Sources;
            internal IReadOnlyList<TextStudioTagBrowserItem> Items;
        }

        sealed class ContextCache
        {
            internal IReadOnlyList<TextStudioTagBrowserItem>[] Scratch;
            internal List<Composition> Compositions = new List<Composition>();
            internal ulong Revision;
        }

        static readonly List<ITextStudioAppearanceAuthoringProvider> Providers = new List<ITextStudioAppearanceAuthoringProvider>();
        static readonly ConditionalWeakTable<TextStudioTagAnalysisContext, ContextCache> Contexts = new ConditionalWeakTable<TextStudioTagAnalysisContext, ContextCache>();
        static readonly ConditionalWeakTable<object, TextStudioTagAnalysisContext.SuggestionSnapshot> Suggestions = new ConditionalWeakTable<object, TextStudioTagAnalysisContext.SuggestionSnapshot>();
        static readonly TextStudioTagAnalysisContext GlobalContext = new TextStudioTagAnalysisContext();
        static int s_generation;

        internal static int Generation => unchecked(TextStudioAppearanceAuthoringMetadata.Generation + s_generation);
        internal static ulong RegistryRevision
        {
            get
            {
                ulong revision = TextStudioAppearanceAuthoringMetadata.RegistryRevision;
                foreach (ITextStudioAppearanceAuthoringProvider provider in Providers) revision = unchecked(revision * 397 ^ provider.Revision ^ (ulong)provider.Domain);
                return revision;
            }
        }

        internal static void Register(ITextStudioAppearanceAuthoringProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (provider.Domain == TextStudioRenderDomain.None || (provider.Domain & TextStudioRenderDomain.FlatText) != 0)
                throw new ArgumentException("An add-on provider must own a renderer domain other than Flat Text.", nameof(provider));
            for (int i = 0; i < Providers.Count; i++)
            {
                if (ReferenceEquals(Providers[i], provider)) return;
                if ((Providers[i].Domain & provider.Domain) != 0)
                    throw new InvalidOperationException("An appearance authoring provider already owns this renderer domain.");
            }
            Providers.Add(provider);
            unchecked { s_generation++; }
        }

        internal static uint OwnerRevision(TextStudioText owner)
        {
            uint revision = owner != null ? owner.AppearanceAuthoringRevision : 0;
            foreach (ITextStudioAppearanceAuthoringProvider provider in Providers) revision = unchecked(revision * 397 ^ provider.GetOwnerRevision(owner));
            return revision;
        }

        internal static TextStudioTagDefinition ForContext(TextStudioTagDefinition definition, TextStudioTagAnalysisContext context)
        {
            if (definition == null || definition.Kind != TextStudioTagKind.Appearance || definition.Id != "style" ||
                context == null || (context.RenderDomains & TextStudioRenderDomain.FlatText) != 0) return definition;
            ITextStudioAppearanceAuthoringProvider selected = null;
            foreach (ITextStudioAppearanceAuthoringProvider provider in Providers)
            {
                if ((provider.Domain & context.RenderDomains) == 0) continue;
                if (selected != null) return definition;
                selected = provider;
            }
            return selected?.ReferenceTagDefinition ?? definition;
        }

        internal static void Bind(TextStudioTagAnalysisContext context, TextStudioText owner, string source, TextStudioDiagnosticReport report, TextStudioTagAnalysisContext previous)
        {
            // Source/literal/closing analysis is shared even when only an add-on renderer is active.
            TextStudioAppearanceAuthoringMetadata.Bind(context, owner != null ? owner.ActiveAppearanceSettings : default, source, report, previous);
            foreach (ITextStudioAppearanceAuthoringProvider provider in Providers)
                if ((context.RenderDomains & provider.Domain) != 0) provider.Bind(context, owner, source, report, previous);
            if (previous != null && Contexts.TryGetValue(previous, out ContextCache prior))
            {
                ContextCache cache = Contexts.GetOrCreateValue(context);
                cache.Compositions = prior.Compositions;
                cache.Revision = prior.Revision;
            }
            IReadOnlyList<TextStudioTagBrowserItem> items = AtCaret(context, 0);
            context.SetSuggestionSnapshot(TextStudioTagSuggestionKeys.AppearanceStyle, Suggestions.GetValue(items, CreateSuggestions));
        }

        static TextStudioTagAnalysisContext.SuggestionSnapshot CreateSuggestions(object source)
        {
            IReadOnlyList<TextStudioTagBrowserItem> items = (IReadOnlyList<TextStudioTagBrowserItem>)source;
            List<string> ids = new List<string>(items.Count);
            foreach (TextStudioTagBrowserItem item in items) ids.Add(item.Definition.Id.TrimStart('$'));
            return new TextStudioTagAnalysisContext.SuggestionSnapshot(ids);
        }

        internal static IReadOnlyList<TextStudioTagBrowserItem> AtCaret(TextStudioTagAnalysisContext context, int caret)
        {
            context ??= GlobalContext;
            ContextCache cache = Contexts.GetOrCreateValue(context);
            int slots = Providers.Count + 1;
            ulong revision = RegistryRevision ^ unchecked((ulong)Generation * 397);
            if (cache.Scratch == null || cache.Scratch.Length != slots) cache.Scratch = new IReadOnlyList<TextStudioTagBrowserItem>[slots];
            if (cache.Revision != revision)
            {
                cache.Compositions = new List<Composition>();
                cache.Revision = revision;
            }
            int count = 0;
            if ((context.RenderDomains & TextStudioRenderDomain.FlatText) != 0)
                cache.Scratch[count++] = TextStudioAppearanceAuthoringMetadata.AtCaret(context, caret);
            foreach (ITextStudioAppearanceAuthoringProvider provider in Providers)
                if ((context.RenderDomains & provider.Domain) != 0) cache.Scratch[count++] = provider.AtCaret(context, caret);
            if (count == 0) return Array.Empty<TextStudioTagBrowserItem>();
            if (count == 1) return cache.Scratch[0];
            foreach (Composition composition in cache.Compositions)
            {
                if (composition.Sources.Length != count) continue;
                bool matches = true;
                for (int i = 0; i < count; i++) if (!ReferenceEquals(composition.Sources[i], cache.Scratch[i])) { matches = false; break; }
                if (matches) return composition.Items;
            }
            List<TextStudioTagBrowserItem> combined = new List<TextStudioTagBrowserItem>();
            IReadOnlyList<TextStudioTagBrowserItem>[] sources = new IReadOnlyList<TextStudioTagBrowserItem>[count];
            for (int i = 0; i < count; i++)
            {
                sources[i] = cache.Scratch[i];
                if (sources[i] != null) combined.AddRange(sources[i]);
            }
            combined.Sort((left, right) => left.ScopeOrder != right.ScopeOrder ? left.ScopeOrder.CompareTo(right.ScopeOrder) : StringComparer.OrdinalIgnoreCase.Compare(left.SortName, right.SortName));
            IReadOnlyList<TextStudioTagBrowserItem> result = combined.ToArray();
            cache.Compositions.Add(new Composition { Sources = sources, Items = result });
            return result;
        }
    }
}
