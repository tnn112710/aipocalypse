using System;
using System.Collections.Generic;
using TextStudio.BuiltIns;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>
    /// Authoring context for reaction inspectors: which outputs a host is configured to render and which
    /// recipe it effectively uses. Derived from configuration only, never from Play Mode availability.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // This editor scratch list is cleared after each inspection.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudioReactionInspectorContext
    {
        static readonly List<MonoBehaviour> s_components = new List<MonoBehaviour>(16);

        /// <summary>
        /// Union of the host's configured outputs. Enabled external renderers suppress the flat text backend,
        /// matching Play Mode. Unknown configurations return Renderer Independent.
        /// </summary>
        internal static TextStudioReactionOutputKinds ResolveOutputs(TextStudioText host)
        {
            if (host == null)
                return TextStudioReactionOutputKinds.RendererIndependent;

            TextStudioReactionOutputKinds outputs = TextStudioReactionOutputKinds.RendererIndependent;
            bool hasExternalRenderer = false;
            host.GetComponents(s_components);
            try
            {
                for (int i = 0; i < s_components.Count; i++)
                {
                    MonoBehaviour component = s_components[i];
                    if (component == null || !component.enabled || component is not ITextStudioRenderer renderer)
                        continue;
                    hasExternalRenderer = true;
                    outputs |= ClassifyRenderer(renderer, component);
                }
            }
            finally
            {
                s_components.Clear();
            }

            IReadOnlyList<ITextStudioRenderer> registered = host.Renderers;
            for (int i = 0; i < registered.Count; i++)
            {
                ITextStudioRenderer renderer = registered[i];
                if (renderer == null || renderer is Object)
                    continue;
                hasExternalRenderer = true;
                outputs |= ClassifyRenderer(renderer, null);
            }

            if (!hasExternalRenderer)
                outputs |= ClassifyFlatTarget(ResolveTextBackendTarget(host));
            return outputs;
        }

        /// <summary>Whether a recipe suits every host. Standalone editing passes no hosts and keeps every recipe.</summary>
        internal static bool Suits(in TextStudioBuiltInReactionPreset preset,
            IReadOnlyList<TextStudioReactionOutputKinds> hostOutputs)
        {
            if (hostOutputs == null)
                return true;
            for (int i = 0; i < hostOutputs.Count; i++)
                if (!preset.SuitsOutputs(hostOutputs[i]))
                    return false;
            return true;
        }

        internal static bool IsProfileBacked(TextStudioText host)
        {
            TextStudioBehaviorProfile profile = host != null ? host.BehaviorProfile : null;
            return profile != null && profile.Provides(TextStudioBehaviorDomain.Reactions);
        }

        /// <summary>The recipe the runtime would run, honoring Behavior Profile precedence.</summary>
        internal static TextStudioReactionRecipe ResolveEffectiveRecipe(TextStudioText host)
        {
            if (host == null)
                return TextStudioReactionRecipe.None;
            return IsProfileBacked(host) ? host.BehaviorProfile.Reactions : host.Reactions;
        }

        internal static IReadOnlyList<TextStudioReactionLayer> ResolveEffectiveLayers(TextStudioText host)
        {
            ResolveEffectiveRecipe(host).Resolve(out IReadOnlyList<TextStudioReactionLayer> layers, out _);
            return layers;
        }

        /// <summary>
        /// Collects exact names that directly start enabled Signal reactions, in first authored order.
        /// Dependent layers, output callbacks, and other triggers are excluded.
        /// </summary>
        internal static void CollectSignalTriggers(IReadOnlyList<TextStudioReactionLayer> layers,
            List<string> names, out bool acceptsAnyName)
        {
            names.Clear();
            acceptsAnyName = false;
            if (layers == null)
                return;
            for (int i = 0; i < layers.Count; i++)
            {
                TextStudioReactionLayer layer = layers[i];
                if (layer == null || !layer.Enabled || layer.When != TextStudioReactionTrigger.Signal ||
                    !string.IsNullOrEmpty(layer.DependsOnId))
                    continue;
                string name = layer.TriggerName;
                if (string.IsNullOrEmpty(name))
                {
                    acceptsAnyName = true;
                    continue;
                }
                if (IndexOfOrdinal(names, name) < 0)
                    names.Add(name);
            }
        }

        /// <summary>Whether any layer, including a temporarily disabled one, names a scene target.</summary>
        internal static bool ReferencesTargetKey(IReadOnlyList<TextStudioReactionLayer> layers)
        {
            if (layers == null)
                return false;
            for (int i = 0; i < layers.Count; i++)
                if (layers[i] != null && !string.IsNullOrEmpty(layers[i].TargetBindingKey))
                    return true;
            return false;
        }

        internal static bool NeedsTargetObjects(TextStudioText host) =>
            host != null && (host.ReactionTargets.Count > 0 || ReferencesTargetKey(ResolveEffectiveLayers(host)));

        internal static int IndexOfOrdinal(List<string> values, string value)
        {
            for (int i = 0; i < values.Count; i++)
                if (string.Equals(values[i], value, StringComparison.Ordinal))
                    return i;
            return -1;
        }

        internal static string Describe(TextStudioReactionOutputKinds outputs)
        {
            if (outputs == TextStudioReactionOutputKinds.RendererIndependent)
                return "any output";
            List<string> parts = new List<string>(3);
            if ((outputs & TextStudioReactionOutputKinds.UI) != 0) parts.Add("UI");
            if ((outputs & TextStudioReactionOutputKinds.World) != 0) parts.Add("World");
            if ((outputs & TextStudioReactionOutputKinds.Extruded3D) != 0) parts.Add("3D");
            return string.Join(" and ", parts);
        }

        static TextStudioReactionOutputKinds ClassifyRenderer(ITextStudioRenderer renderer, Component component)
        {
            TextStudioRenderDomain domain = renderer is ITextStudioRenderDomainProvider provider
                ? provider.RenderDomain
                : TextStudioRenderDomain.All;
            if (domain == TextStudioRenderDomain.Extruded3D)
                return TextStudioReactionOutputKinds.Extruded3D;
            return domain == TextStudioRenderDomain.FlatText
                ? ClassifyFlatTarget(component)
                : TextStudioReactionOutputKinds.RendererIndependent;
        }

        static Object ResolveTextBackendTarget(TextStudioText host)
        {
            ITextStudioTextBackend backend = host.Backends.ActiveBackend;
            if (backend != null)
                return backend.TargetObject;
            TMP_Text text = host.TMPText;
            return text != null ? text : host.GetComponent<TMP_Text>();
        }

        static TextStudioReactionOutputKinds ClassifyFlatTarget(Object target)
        {
            if (target is not Component component || component == null ||
                component is Behaviour behaviour && !behaviour.enabled)
                return TextStudioReactionOutputKinds.RendererIndependent;
            // World TextMeshPro derives from Graphic, so only Canvas-rendered graphics count as UI.
            bool canvasGraphic = component is UnityEngine.UI.Graphic && component is not TextMeshPro;
            return canvasGraphic || component.GetComponentInParent<Canvas>(true) != null
                ? TextStudioReactionOutputKinds.UI
                : TextStudioReactionOutputKinds.World;
        }
    }
}
