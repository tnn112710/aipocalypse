using System.Collections.Generic;
using System.IO;
using System.Linq;
using MB13.Aipocalypse.Editor.Localization;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;

// One-time (re-runnable) Localization setup: the settings asset, the locales (English first, then Vietnamese and
// French, both falling back to English), startup selectors (command line, saved choice, English), synchronous init with
// the selected locale's tables preloaded, Addressables built with the player, then every CSV imported.
public static class LocalizationSetup
{
    const string Root = "Assets/_Game/Data/Localization";
    const string LocalesDir = Root + "/Locales";
    const string SettingsPath = Root + "/LocalizationSettings.asset";
    public const string PlayerPrefsKey = "selected-locale";

    // Code and asset name (Addressables names a group per locale after it, so plain ASCII). The language option shows
    // each locale's own UI/language.name instead ("Tiếng Việt").
    static readonly (string code, string name)[] Locales = { ("en", "English (en)"), ("vi", "Vietnamese (vi)"), ("fr", "French (fr)") };
    static readonly Dictionary<string, string> OldGroupSuffixes = new() { { "English", "en" }, { "Tiếng Việt", "vi" }, { "Français", "fr" } };

    public static string All()
    {
        var log = new List<string>();
        foreach (string dir in new[] { Root, LocalesDir, LocalizationCsv.CsvFolder, LocalizationCsv.TablesFolder })
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.ImportAsset(dir);
                log.Add("created " + dir);
            }

        LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.name = "LocalizationSettings";
            AssetDatabase.CreateAsset(settings, SettingsPath);
            log.Add("created " + SettingsPath);
        }
        LocalizationEditorSettings.ActiveLocalizationSettings = settings;

        var locales = new List<Locale>();
        for (int i = 0; i < Locales.Length; i++)
        {
            (string code, string name) = Locales[i];
            string path = $"{LocalesDir}/{code}.asset";
            Locale locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(code);
                AssetDatabase.CreateAsset(locale, path);
                log.Add("created locale " + code);
            }
            locale.name = code;
            locale.LocaleName = name;
            locale.SortOrder = (ushort)i;
            EditorUtility.SetDirty(locale);
            if (!LocalizationEditorSettings.GetLocales().Contains(locale))
                LocalizationEditorSettings.AddLocale(locale);
            locales.Add(locale);
        }
        Locale english = locales[0];
        foreach (Locale locale in locales.Skip(1))
        {
            var fallback = locale.Metadata.GetMetadata<FallbackLocale>();
            if (fallback == null)
                locale.Metadata.AddMetadata(new FallbackLocale(english));
            else
                fallback.Locale = english;
            EditorUtility.SetDirty(locale);
        }
        foreach (Locale locale in locales)
            AssetDatabase.SaveAssetIfDirty(locale);

        // Saved choice first (the language option writes the same key), never the OS language: English unless picked.
        List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();
        selectors.Clear();
        selectors.Add(new CommandLineLocaleSelector());
        selectors.Add(new PlayerPrefLocaleSelector { PlayerPreferenceKey = PlayerPrefsKey });
        selectors.Add(new SpecificLocaleSelector { LocaleId = english.Identifier });

        var so = new SerializedObject(settings);
        so.FindProperty("m_ProjectLocaleIdentifier").FindPropertyRelative("m_Code").stringValue = english.Identifier.Code;
        so.FindProperty("m_InitializeSynchronously").boolValue = true;
        so.FindProperty("m_PreloadBehavior").enumValueIndex = (int)PreloadBehavior.PreloadSelectedLocaleAndFallbacks;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
        log.Add($"settings: {string.Join(", ", locales.Select(l => $"{l.Identifier.Code} '{l.LocaleName}'"))}; " +
                "selectors command line > saved > en; synchronous init; preload selected + fallbacks");

        log.Add(LocalizationCsv.ImportAll());

        AddressableAssetSettings addressables = AddressableAssetSettingsDefaultObject.Settings;
        if (addressables != null)
        {
            // File > Build Profiles builds them too; Autobuild builds them itself first (BUILD.md).
            addressables.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            EditorUtility.SetDirty(addressables);
            AssetDatabase.SaveAssetIfDirty(addressables);
            foreach (AddressableAssetGroup group in addressables.groups.Where(g => g != null && g.Name.StartsWith("Localization")).ToList())
            {
                foreach ((string oldSuffix, string code) in OldGroupSuffixes)
                {
                    string localeName = Locales.First(l => l.code == code).name;
                    if (group.Name.EndsWith("-" + oldSuffix) && oldSuffix != localeName)
                    {
                        string renamed = group.Name.Substring(0, group.Name.Length - oldSuffix.Length) + localeName;
                        log.Add($"group {group.Name} -> {renamed}");
                        group.Name = renamed;
                    }
                }
                NoHash(group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>(), log, group.Name);
                AssetDatabase.SaveAssetIfDirty(group);
            }
            foreach (ScriptableObject template in addressables.GroupTemplateObjects)
                if (template is AddressableAssetGroupTemplate t)
                {
                    NoHash(t.GetSchemaByType(typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema))
                        as UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema, log, "template " + t.Name);
                    AssetDatabase.SaveAssetIfDirty(t);
                }
            log.Add("addressables: built with the player; groups " + string.Join(", ", addressables.groups.Select(g => g.Name)));
        }
        else
            log.Add("addressables: no settings yet (created with the first table)");
        return string.Join("\n", log);
    }

    // Hashed bundle names get long enough to hit Windows' path limit (the Addressables warning).
    static void NoHash(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema schema, List<string> log, string owner)
    {
        if (schema == null || schema.BundleNaming == UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.BundleNamingStyle.NoHash)
            return;
        schema.BundleNaming = UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema.BundleNamingStyle.NoHash;
        EditorUtility.SetDirty(schema);
        AssetDatabase.SaveAssetIfDirty(schema);
        log.Add(owner + ": bundle names NoHash");
    }
}
