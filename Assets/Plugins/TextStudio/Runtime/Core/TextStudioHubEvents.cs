using System;
using System.Collections.Generic;

namespace TextStudio
{
    /// <summary>
    /// Forwards project-wide change notifications to every enabled hub. A multicast subscription per hub and event made
    /// each enable and disable linear in the number of enabled hubs, so enabling a scene of labels took quadratic time;
    /// hubs now register in constant time and each event keeps one forwarder.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Enabled hubs also serve Edit Mode; registration and code unload own the event forwarders.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioHubEvents
    {
        static readonly List<TextStudioText> s_hubs = new List<TextStudioText>();
        static bool s_subscribed;

        internal static int RegisteredCount => s_hubs.Count;

        internal static bool IsRegistered(TextStudioText hub) =>
            hub != null && hub.HubEventSlot > 0 && hub.HubEventSlot <= s_hubs.Count &&
            ReferenceEquals(s_hubs[hub.HubEventSlot - 1], hub);

        internal static void Register(TextStudioText hub)
        {
            if (IsRegistered(hub))
                return;
            s_hubs.Add(hub);
            hub.HubEventSlot = s_hubs.Count;
            if (s_subscribed)
                return;
            s_subscribed = true;
            TextStudio2DStyleAsset.Changed += ForwardAppearanceStyleChanged;
            TextStudio2DStyles.Changed += ForwardAppearanceCatalogChanged;
            TextStudioWritingStyleAsset.Changed += ForwardWritingStyleChanged;
            WritingFontAsset.Changed += ForwardWritingFontChanged;
            WritingPhraseAsset.Changed += ForwardWritingPhraseChanged;
            TextStudioRegistry.ProjectCatalogChanged += ForwardProjectCatalogChanged;
            TextStudioMotionSettings.Changed += ForwardMotionPolicyChanged;
        }

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseSubscriptions()
        {
            TextStudio2DStyleAsset.Changed -= ForwardAppearanceStyleChanged;
            TextStudio2DStyles.Changed -= ForwardAppearanceCatalogChanged;
            TextStudioWritingStyleAsset.Changed -= ForwardWritingStyleChanged;
            WritingFontAsset.Changed -= ForwardWritingFontChanged;
            WritingPhraseAsset.Changed -= ForwardWritingPhraseChanged;
            TextStudioRegistry.ProjectCatalogChanged -= ForwardProjectCatalogChanged;
            TextStudioMotionSettings.Changed -= ForwardMotionPolicyChanged;
            s_subscribed = false;
            foreach (TextStudioText hub in s_hubs)
                if (hub != null)
                    hub.HubEventSlot = 0;
            s_hubs.Clear();
        }
#endif

        internal static void Unregister(TextStudioText hub)
        {
            if (!IsRegistered(hub))
            {
                if (hub != null)
                    hub.HubEventSlot = 0;
                return;
            }
            int index = hub.HubEventSlot - 1;
            int last = s_hubs.Count - 1;
            TextStudioText moved = s_hubs[last];
            s_hubs[index] = moved;
            moved.HubEventSlot = index + 1;
            s_hubs.RemoveAt(last);
            hub.HubEventSlot = 0;
        }

        // Like a multicast invocation, each notification reaches the hubs enabled when it was raised, even if a handler
        // enables or disables hubs.
        static TextStudioText[] Snapshot() => s_hubs.Count == 0 ? Array.Empty<TextStudioText>() : s_hubs.ToArray();

        static void ForwardAppearanceStyleChanged(TextStudio2DStyleAsset style)
        {
            foreach (TextStudioText hub in Snapshot())
                hub.HandleAppearanceStyleAssetChanged(style);
        }

        static void ForwardAppearanceCatalogChanged()
        {
            foreach (TextStudioText hub in Snapshot())
                hub.HandleAppearanceCatalogChanged();
        }

        static void ForwardWritingStyleChanged(TextStudioWritingStyleAsset style)
        {
            foreach (TextStudioText hub in Snapshot())
                hub.HandleWritingStyleAssetChanged(style);
        }

        static void ForwardWritingFontChanged(WritingFontAsset font)
        {
            foreach (TextStudioText hub in Snapshot())
                hub.HandleWritingFontAssetChanged(font);
        }

        static void ForwardWritingPhraseChanged(WritingPhraseAsset phrase)
        {
            foreach (TextStudioText hub in Snapshot())
                hub.HandleWritingPhraseAssetChanged(phrase);
        }

        static void ForwardProjectCatalogChanged()
        {
            foreach (TextStudioText hub in Snapshot())
                hub.HandleProjectCatalogChanged();
        }

        static void ForwardMotionPolicyChanged()
        {
            foreach (TextStudioText hub in Snapshot())
                hub.HandleMotionPolicyChanged();
        }
    }
}
