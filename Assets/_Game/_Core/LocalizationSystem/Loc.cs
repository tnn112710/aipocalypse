using System.Runtime.CompilerServices;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace MB13.Aipocalypse.Core.LocalizationSystem
{
    // Text from the string tables (edited as CSVs: LOCALIZATION.md). `Text()` caches each string per language: one table
    // lookup (~12 µs) when the language changes, then the same string instance, so views that skip unchanged text by
    // reference keep skipping. Keys owned by code: `static readonly LocalizedString X = Loc.UI("key")`.
    public static class Loc
    {
        public const string UITable = "UI";
        public const string HudTable = "HUD";
        public const string ItemsTable = "Items";
        public const string InteractionTable = "Interaction";
        public const string CreaturesTable = "Creatures";

        sealed class Cached
        {
            public Locale Locale;
            public string Text;
        }

        static readonly ConditionalWeakTable<LocalizedString, Cached> Cache = new();

        public static LocalizedString UI(string key) => new(UITable, key);
        public static LocalizedString Hud(string key) => new(HudTable, key);
        public static LocalizedString Items(string key) => new(ItemsTable, key);
        public static LocalizedString Interaction(string key) => new(InteractionTable, key);
        public static LocalizedString Creatures(string key) => new(CreaturesTable, key);

        // The language text is shown in; compare it to rebuild text composed from several strings.
        public static Locale Current => LocalizationSettings.SelectedLocale;

        public static string Text(this LocalizedString text)
        {
            if (text == null || text.IsEmpty)
                return string.Empty;
            Locale locale = LocalizationSettings.SelectedLocale;
            Cached cached = Cache.GetOrCreateValue(text);
            if (cached.Text == null || cached.Locale != locale)
            {
                cached.Text = text.GetLocalizedString();
                cached.Locale = locale;
            }
            return cached.Text;
        }

        // string.Format with the translated pattern ({0} style). Allocates: for text built on events, not every frame.
        public static string Format(this LocalizedString text, object arg0) => string.Format(text.Text(), arg0);
        public static string Format(this LocalizedString text, object arg0, object arg1) => string.Format(text.Text(), arg0, arg1);
        public static string Format(this LocalizedString text, object arg0, object arg1, object arg2) =>
            string.Format(text.Text(), arg0, arg1, arg2);
    }
}
