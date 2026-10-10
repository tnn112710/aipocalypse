using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio.Editor
{
#if UNITY_6000_7_OR_NEWER
    // Inspector caches follow their owners and authoring revisions in the editor.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioTagInspectorContext
    {
        sealed class OwnerCache
        {
            internal int Generation = -1;
            internal ulong Registry;
            internal uint AppearanceRevision;
            internal TextStudioTagBackend Backend;
            internal TextStudioRenderDomain Domains;
            internal TextStudioTagCatalog Catalog;
            internal TextStudioTagAnalysisContext Context;
            internal string Source;
            internal readonly Dictionary<string, TextStudioTagAnalysisContext> Sources = new Dictionary<string, TextStudioTagAnalysisContext>(StringComparer.Ordinal);
            internal readonly Queue<string> SourceOrder = new Queue<string>();
        }
        static readonly ConditionalWeakTable<TextStudioText, OwnerCache> Owners = new ConditionalWeakTable<TextStudioText, OwnerCache>();
        static readonly OwnerCache Global = new OwnerCache();
        static readonly Dictionary<int, OwnerCache> BackendCaches = new Dictionary<int, OwnerCache>();

        public static TextStudioTagAnalysisContext Build(TextStudioText hub, string source)
        {
            OwnerCache cached = hub == null ? Global : Owners.GetOrCreateValue(hub);
            TextStudioTagBackend backend = ResolveBackend(hub);
            TextStudioRenderDomain domains = ResolveRenderDomains(hub);
            return BuildCached(cached, hub, source, backend, domains);
        }

        internal static TextStudioTagAnalysisContext BuildForBackend(string source, TextStudioTagBackend backend, TextStudioRenderDomain domains)
        {
            int key = (int)backend * 397 ^ (int)domains;
            if (!BackendCaches.TryGetValue(key, out OwnerCache cached)) BackendCaches[key] = cached = new OwnerCache();
            return BuildCached(cached, null, source, backend, domains);
        }

        static TextStudioTagAnalysisContext BuildCached(OwnerCache cached, TextStudioText hub, string source, TextStudioTagBackend backend, TextStudioRenderDomain domains)
        {
            source ??= string.Empty;
            int generation = TextStudioAppearanceAuthoringProviders.Generation;
            ulong registry = TextStudioAppearanceAuthoringProviders.RegistryRevision;
            uint appearanceRevision = TextStudioAppearanceAuthoringProviders.OwnerRevision(hub);
            bool metadataChanged = cached.Generation != generation || cached.Registry != registry || cached.AppearanceRevision != appearanceRevision || cached.Backend != backend || cached.Domains != domains;
            if (!metadataChanged && cached.Sources.TryGetValue(source, out TextStudioTagAnalysisContext existing))
            {
                cached.Context = existing; cached.Source = source;
                return existing;
            }
            if (metadataChanged) { cached.Sources.Clear(); cached.SourceOrder.Clear(); }
            if (metadataChanged || cached.Catalog == null)
            {
                List<ITextStudioAction> actions = new List<ITextStudioAction>(4);
                CollectLocalActions(hub, actions);
                cached.Catalog = actions.Count > 0 ? TextStudioTagCatalog.BuildDefault(actions) : TextStudioTagCatalog.BuildDefault();
            }
            TextStudioTagAnalysisContext context = new TextStudioTagAnalysisContext(cached.Catalog, backend, domains);
            TextStudioDiagnosticReport report = TextStudioMarkupDiagnostics.Analyze(source, context);
            List<string> routes = new List<string>(16);
            CollectAuthoredEventRoutes(report, routes);
            CollectMarkerRouterRoutes(hub, routes);
            CollectAudioPlayerRoutes(hub, routes);
            context.SetSuggestionValues(TextStudioTagSuggestionKeys.EventRoute, routes);
            context.SetSuggestionValues(TextStudioTagSuggestionKeys.MarkerRoute, routes);
            TextStudioAppearanceAuthoringProviders.Bind(context, hub, source, report, metadataChanged ? null : cached.Context);
            cached.Generation = generation;
            cached.Registry = TextStudioAppearanceAuthoringProviders.RegistryRevision;
            cached.AppearanceRevision = appearanceRevision;
            cached.Backend = backend;
            cached.Domains = domains;
            cached.Context = context;
            cached.Source = source;
            cached.Sources[source] = context;
            cached.SourceOrder.Enqueue(source);
            while (cached.SourceOrder.Count > 8) cached.Sources.Remove(cached.SourceOrder.Dequeue());
            return context;
        }

        static TextStudioTagBackend ResolveBackend(TextStudioText hub)
        {
            ITextStudioTextBackend backend = hub != null
                ? hub.Backends.ActiveBackend
                : null;
            if (backend == null)
                return TextStudioTagBackend.TextMeshPro;

            bool supportsRichText =
                (backend.Capabilities &
                 TextStudioTextBackendCapabilities.RichText) != 0;
            if (string.Equals(
                    backend.BackendId,
                    TextStudioTextBackendIds.UnityUIText,
                    StringComparison.Ordinal))
            {
                return supportsRichText
                    ? TextStudioTagBackend.UnityUITextRichText
                    : TextStudioTagBackend.UnityUIText;
            }
            return supportsRichText
                ? TextStudioTagBackend.UniTextRichText
                : TextStudioTagBackend.UniText;
        }

        internal static TextStudioRenderDomain ResolveRenderDomains(TextStudioText hub)
        {
            if (hub == null)
                return TextStudioRenderDomain.All;
            IReadOnlyList<ITextStudioRenderer> renderers = hub.Renderers;
            TextStudioRenderDomain domains = TextStudioRenderDomain.None;
            bool found = false;
            for (int i = 0; i < renderers.Count; i++)
            {
                ITextStudioRenderer renderer = renderers[i];
                if (renderer == null || !renderer.IsAvailable)
                    continue;
                TextStudioRenderDomain rendererDomain =
                    renderer is ITextStudioRenderDomainProvider provider
                        ? provider.RenderDomain
                        : TextStudioRenderDomain.FlatText;
                domains |= rendererDomain;
                found = true;
            }
            return found ? domains : TextStudioRenderDomain.FlatText;
        }

        static void CollectLocalActions(TextStudioText hub, List<ITextStudioAction> actions)
        {
            if (hub == null)
                return;

            ITextStudioAction[] components = hub.GetComponents<ITextStudioAction>();
            for (int i = 0; i < components.Length; i++)
            {
                ITextStudioAction action = components[i];
                if (!IsUsableLocalAction(action) || Contains(actions, action))
                    continue;

                actions.Add(action);
            }
        }

        static bool IsUsableLocalAction(ITextStudioAction action)
        {
            if (action == null)
                return false;

            if (action is UnityEngine.Object unityObject && unityObject == null)
                return false;

            if (action is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                return false;

            return true;
        }

        static void CollectAuthoredEventRoutes(TextStudioDiagnosticReport report, List<string> routes)
        {
            if (report == null) return;
            for (int i = 0; i < report.Tags.Length; i++)
            {
                TextStudioTagDiagnostic tag = report.Tags[i];
            if (tag.IsClosing ||
                !string.Equals(tag.Name, TextStudioBuiltInActionIds.Event, StringComparison.OrdinalIgnoreCase))
                    continue;

                TextStudioTagParameters parameters = TextStudioTagParameterParser.Parse(tag.RawParameter);
                string route = parameters.GetString(0);
                AddDistinct(routes, route);
            }
        }

        static void CollectMarkerRouterRoutes(TextStudioText hub, List<string> routes)
        {
            if (hub == null)
                return;

            TextStudioMarkerEventRouter[] routers = hub.GetComponents<TextStudioMarkerEventRouter>();
            for (int i = 0; i < routers.Length; i++)
            {
                TextStudioMarkerEventRouter router = routers[i];
                if (router == null)
                    continue;

                IReadOnlyList<TextStudioMarkerBinding> bindings = router.Bindings;
                for (int b = 0; b < bindings.Count; b++)
                {
                    TextStudioMarkerBinding binding = bindings[b];
                    if (binding != null)
                        AddDistinct(routes, binding.RouteId);
                }
            }
        }

        static void CollectAudioPlayerRoutes(TextStudioText hub, List<string> routes)
        {
            if (hub == null)
                return;

            TextStudioAudioPlayer[] players = hub.GetComponents<TextStudioAudioPlayer>();
            for (int i = 0; i < players.Length; i++)
            {
                TextStudioAudioPlayer player = players[i];
                if (player == null)
                    continue;

                IReadOnlyList<TextStudioAudioPlayerMarkerCue> cues = player.MarkerCues;
                for (int c = 0; c < cues.Count; c++)
                {
                    TextStudioAudioPlayerMarkerCue cue = cues[c];
                    if (cue != null)
                        AddDistinct(routes, cue.RouteId);
                }
            }
        }

        static void AddDistinct(List<string> values, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < values.Count; i++)
            {
                if (string.Equals(values[i], value, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            values.Add(value);
        }

        static bool Contains<T>(List<T> values, T value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(values[i], value))
                    return true;
            }

            return false;
        }
    }
}
