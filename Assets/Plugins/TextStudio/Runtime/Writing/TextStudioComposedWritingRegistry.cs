using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Connects composed Writing layers to the renderer-property pipeline without introducing
    /// a second tag resolver or writing compiler. Property IDs are invocation-stable and carry
    /// only progress and scoped influence. The referenced Writing Style remains authoritative.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Composed registrations reset for each Play session, matching ResetRuntimeState.
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal static partial class TextStudioComposedWritingRegistry
    {
        internal const string PropertyPrefix = "textstudio.writing.";

        static readonly Dictionary<string, TextStudioCompositionLayer> s_layers =
            new Dictionary<string, TextStudioCompositionLayer>(StringComparer.Ordinal);
        static readonly Dictionary<TextStudioCompositionLayer, string> s_propertyIds =
            new Dictionary<TextStudioCompositionLayer, string>();
#if UNITY_6000_7_OR_NEWER
        // Dependency publishers also serve Edit Mode, so the one forwarder remains subscribed until code unload.
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static bool s_subscribed;

        internal static string BuildPropertyId(
            string compositionId,
            string stableId)
        {
            string owner = string.IsNullOrWhiteSpace(compositionId)
                ? "unowned"
                : compositionId.Trim().ToLowerInvariant();
            string identity = string.IsNullOrWhiteSpace(stableId)
                ? "layer"
                : stableId.Trim().ToLowerInvariant();
            return PropertyPrefix + owner + "." + identity;
        }

        internal static bool IsWritingProperty(string propertyId)
        {
            return !string.IsNullOrEmpty(propertyId) &&
                   propertyId.StartsWith(PropertyPrefix, StringComparison.Ordinal);
        }

        internal static void EnsureRegistered(TextStudioCompositionLayer layer)
        {
            if (layer == null || layer.Kind != TextStudioCompositionLayerKind.Writing)
                return;
            EnsureSubscribed();
            string propertyId = layer.WritingPropertyId;
            if (s_propertyIds.TryGetValue(layer, out string previousPropertyId) &&
                !string.Equals(
                    previousPropertyId,
                    propertyId,
                    StringComparison.Ordinal))
            {
                Unregister(layer);
            }
            TextStudioRenderPropertyDescriptor descriptor =
                new TextStudioRenderPropertyDescriptor(
                    propertyId,
                    "Writing Progress",
                    "Writing",
                    "Scoped writing progress and influence for one composed Writing layer.",
                    TextStudioRenderPropertyValueType.Vector4,
                    TextStudioRenderPropertyStage.Geometry,
                    TextStudioRenderPropertyPerformanceClass.PreparedDeformation,
                    Vector4.zero,
                    Vector4.zero,
                    Vector4.one,
                    blendModes: TextStudioRenderPropertyBlendModes.Override |
                                TextStudioRenderPropertyBlendModes.Lerp,
                    supportedRenderDomains:
                        TextStudioRenderDomain.FlatText |
                        TextStudioRenderDomain.Extruded3D);
            TextStudioRenderPropertyRegistry.Register(in descriptor);
            s_layers[propertyId] = layer;
            s_propertyIds[layer] = propertyId;
        }

        internal static void Unregister(TextStudioCompositionLayer layer)
        {
            if (layer == null ||
                !s_propertyIds.TryGetValue(layer, out string propertyId))
            {
                return;
            }
            s_propertyIds.Remove(layer);
            if (!s_layers.TryGetValue(
                    propertyId,
                    out TextStudioCompositionLayer registered) ||
                !ReferenceEquals(registered, layer))
            {
                return;
            }
            s_layers.Remove(propertyId);
            TextStudioRenderPropertyRegistry.Unregister(propertyId);
        }

        internal static bool TryGetLayer(
            string propertyId,
            out TextStudioCompositionLayer layer)
        {
            layer = null;
            return IsWritingProperty(propertyId) &&
                   s_layers.TryGetValue(propertyId, out layer) &&
                   layer != null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeState()
        {
            s_layers.Clear();
            s_propertyIds.Clear();
        }

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseSubscriptions()
        {
            TextStudioWritingStyleAsset.Changed -= HandleWritingDependencyChanged;
            WritingFontAsset.Changed -= HandleWritingDependencyChanged;
            WritingPhraseAsset.Changed -= HandleWritingDependencyChanged;
            s_subscribed = false;
            ResetRuntimeState();
        }
#endif

        static void EnsureSubscribed()
        {
            if (s_subscribed)
                return;
            TextStudioWritingStyleAsset.Changed += HandleWritingDependencyChanged;
            WritingFontAsset.Changed += HandleWritingDependencyChanged;
            WritingPhraseAsset.Changed += HandleWritingDependencyChanged;
            s_subscribed = true;
        }

        static void HandleWritingDependencyChanged(TextStudioWritingStyleAsset _)
        {
            TextStudioEffectDefinitionAsset.NotifyEffectContentChanged();
        }

        static void HandleWritingDependencyChanged(WritingFontAsset _)
        {
            TextStudioEffectDefinitionAsset.NotifyEffectContentChanged();
        }

        static void HandleWritingDependencyChanged(WritingPhraseAsset _)
        {
            TextStudioEffectDefinitionAsset.NotifyEffectContentChanged();
        }
    }
}
