using System;

namespace TextStudio
{
    /// <summary>Registers reusable appearances by their stable Tag ID. Runtime entries override the project catalog.</summary>
#if UNITY_6000_7_OR_NEWER
    // Catalog notifications and the asset forwarder live until code unload, including Edit Mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudio2DStyles
    {
        static TextStudio2DStyles() => TextStudio2DStyleAsset.Changed += OnStyleChanged;

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void ReleaseChangeSubscriptions()
        {
            TextStudio2DStyleAsset.Changed -= OnStyleChanged;
            Changed = null;
        }
#endif
        internal static uint Revision => TextStudioRegistry.AppearanceStyles.Revision;
        internal static event Action Changed;

        /// <summary>Registers a style by its stable tag identifier, optionally replacing an existing runtime registration.</summary>
        public static void Register(TextStudio2DStyleAsset style, bool replaceExisting = false)
        {
            if (style == null) throw new ArgumentNullException(nameof(style));
            TextStudioRegistry.EnsureInitialized();
            uint previous = Revision;
            TextStudioStyleCatalog.Register(TextStudioRegistry.AppearanceStyles, style, style.Id, replaceExisting);
            if (Revision != previous) NotifyChanged();
        }

        /// <summary>Attempts to resolve a non-null style asset by its registered tag identifier.</summary>
        public static bool TryGetDefinition(string id, out TextStudio2DStyleAsset style)
        {
            style = null;
            if (string.IsNullOrEmpty(id)) return false;
            TextStudioRegistry.EnsureInitialized();
            return TextStudioRegistry.AppearanceStyles.TryGetQuiet(id, out style) && style != null;
        }

        /// <summary>Removes only a runtime override, revealing the project entry when present.</summary>
        public static bool Unregister(string id)
        {
            bool removed = TextStudioRegistry.AppearanceStyles.UnregisterRuntime(id);
            if (removed) NotifyChanged();
            return removed;
        }

        internal static void NotifyChanged() => Changed?.Invoke();
        static void OnStyleChanged(TextStudio2DStyleAsset _) => NotifyChanged();
    }
}
