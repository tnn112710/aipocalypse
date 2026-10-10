using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    /// <summary>
    /// Implemented by optional editor packages that add authoring support for another text render domain.
    /// Providers must have a parameterless constructor.
    /// </summary>
    public interface ITextStudioAuthoringCapabilityProvider
    {
        TextStudioRenderDomain AvailableRenderDomains { get; }
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioAuthoringCapabilities
    {
        static bool s_resolved;
        static TextStudioRenderDomain s_availableRenderDomains;

        internal static TextStudioRenderDomain AvailableRenderDomains
        {
            get
            {
                if (!s_resolved)
                {
                    s_availableRenderDomains = ResolveAvailableRenderDomains(
                        TypeCache.GetTypesDerivedFrom<ITextStudioAuthoringCapabilityProvider>());
                    s_resolved = true;
                }

                return s_availableRenderDomains;
            }
        }

        internal static bool Supports(TextStudioRenderDomain requiredDomains)
        {
            return requiredDomains == TextStudioRenderDomain.None ||
                   (AvailableRenderDomains & requiredDomains) == requiredDomains;
        }

        internal static bool Intersects(TextStudioRenderDomain supportedDomains)
        {
            return (AvailableRenderDomains & supportedDomains) != TextStudioRenderDomain.None;
        }

        internal static TextStudioRenderDomain ResolveAvailableRenderDomains(
            IEnumerable<Type> providerTypes)
        {
            TextStudioRenderDomain domains = TextStudioRenderDomain.FlatText;
            if (providerTypes == null)
                return domains;

            foreach (Type providerType in providerTypes)
            {
                if (providerType == null || providerType.IsAbstract || providerType.IsInterface)
                    continue;

                try
                {
                    if (Activator.CreateInstance(providerType, true) is ITextStudioAuthoringCapabilityProvider provider)
                        domains |= provider.AvailableRenderDomains & TextStudioRenderDomain.All;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "[Text Studio] Authoring capability provider failed: " +
                        providerType.FullName + ". " + exception.Message);
                }
            }

            return domains;
        }
    }
}
