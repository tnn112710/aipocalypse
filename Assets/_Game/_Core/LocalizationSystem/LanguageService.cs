using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.Events;
using MessagePipe;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.LocalizationSystem
{
    // Root: the languages the game offers, the current one and switching it. The choice is kept in PlayerPrefs under the key
    // LocalizationSettings' startup selector reads, so it comes back on the next launch. Views that build text from several
    // strings listen to LanguageChanged; LocalizeStringEvent labels refresh by themselves.
    public sealed class LanguageService : IInitializable, IDisposable
    {
        public const string PlayerPrefsKey = "selected-locale";
        const string NameKey = "language.name";

        readonly IPublisher<LanguageChanged> _changed;
        readonly Dictionary<Locale, string> _names = new();

        public LanguageService(IPublisher<LanguageChanged> changed) => _changed = changed;

        public IReadOnlyList<Locale> Languages => LocalizationSettings.AvailableLocales.Locales;
        public Locale Current => LocalizationSettings.SelectedLocale;

        public void Initialize()
        {
            // Synchronous init (LocalizationSettings): the selected language's tables are loaded before any scene starts.
            LocalizationSettings.InitializationOperation.WaitForCompletion();
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        public void Dispose() => LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;

        public void Select(Locale locale)
        {
            if (locale == null || locale == Current)
                return;
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(PlayerPrefsKey, locale.Identifier.Code);
            PlayerPrefs.Save();
        }

        public bool Select(string code)
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
            Select(locale);
            return locale != null;
        }

        // The next (step 1) or previous (-1) language, wrapping around.
        public void Step(int step)
        {
            IReadOnlyList<Locale> languages = Languages;
            if (languages.Count == 0)
                return;
            int index = 0;
            for (int i = 0; i < languages.Count; i++)
                if (languages[i] == Current)
                    index = i;
            Select(languages[((index + step) % languages.Count + languages.Count) % languages.Count]);
        }

        // A language's name in itself ("Tiếng Việt"), whatever the current language.
        public string NameOf(Locale locale)
        {
            if (locale == null)
                return string.Empty;
            if (!_names.TryGetValue(locale, out string name))
                _names[locale] = name = LocalizationSettings.StringDatabase.GetLocalizedString(Loc.UITable, NameKey, locale);
            return name;
        }

        void OnSelectedLocaleChanged(Locale locale) => _changed.Publish(default);
    }
}
