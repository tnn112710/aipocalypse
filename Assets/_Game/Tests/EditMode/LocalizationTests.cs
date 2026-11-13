using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Editor.Localization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace MB13.Aipocalypse.Tests
{
    // The string tables against their CSVs, every reference into them (code keys, assets, prefab labels) and the fonts.
    public sealed class LocalizationTests
    {
        static readonly string[] Languages = { "en", "vi", "fr" };

        // Static atlases: a character outside them shows as a missing glyph (AgentScripts/UI/FontSetup).
        static readonly string[] StaticFonts =
        {
            "Assets/_Game/Data/UI/Fonts/SairaHeader.asset", "Assets/_Game/Data/UI/Fonts/SairaBold.asset",
            "Assets/_Game/Data/UI/Fonts/SairaBody.asset", "Assets/_Game/Data/UI/DeathScreen/DeathScreenFont.asset",
            "Assets/_Game/Data/UI/RepairQteFont.asset",
        };

        [Test]
        public void Csvs_MatchTheirTables()
        {
            List<string> diffs = LocalizationCsv.CsvPaths().SelectMany(LocalizationCsv.Compare).ToList();
            Assert.IsNotEmpty(LocalizationCsv.CsvPaths());
            Assert.IsEmpty(diffs, "Run Aipocalypse > Localization > Import All CSVs:\n" + string.Join("\n", diffs));
        }

        [Test]
        public void Csvs_EveryKeyHasEveryLanguage()
        {
            var gaps = new List<string>();
            foreach (string csv in LocalizationCsv.CsvPaths())
            {
                List<string[]> rows = LocalizationCsv.Parse(File.ReadAllText(csv));
                string[] header = rows[0].Select(h => h.Trim()).ToArray();
                int key = Array.IndexOf(header, LocalizationCsv.KeyColumn);
                foreach (string language in Languages)
                {
                    int column = Array.IndexOf(header, language);
                    Assert.GreaterOrEqual(column, 0, $"{csv} has no '{language}' column");
                    foreach (string[] row in rows.Skip(1).Where(r => key < r.Length && r[key].Trim().Length > 0))
                        if (column >= row.Length || row[column].Length == 0)
                            gaps.Add($"{Path.GetFileName(csv)} {row[key]} [{language}]");
                }
            }
            Assert.IsEmpty(gaps, string.Join("\n", gaps));
        }

        [Test]
        public void CodeKeys_ExistInTheirTables()
        {
            var missing = new List<string>();
            int checkedKeys = 0;
            const BindingFlags statics = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (Type type in typeof(Loc).Assembly.GetTypes())
            foreach (FieldInfo field in type.GetFields(statics))
            {
                IEnumerable<LocalizedString> values = field.FieldType == typeof(LocalizedString) ? new[] { (LocalizedString)field.GetValue(null) }
                    : field.FieldType == typeof(LocalizedString[]) ? (LocalizedString[])field.GetValue(null) ?? Array.Empty<LocalizedString>()
                    : Enumerable.Empty<LocalizedString>();
                foreach (LocalizedString value in values)
                {
                    checkedKeys++;
                    if (!Resolves(value))
                        missing.Add($"{type.Name}.{field.Name}: {value.TableReference} / {value.TableEntryReference}");
                }
            }
            Assert.Greater(checkedKeys, 50);
            Assert.IsEmpty(missing, string.Join("\n", missing));
        }

        [Test]
        public void Assets_EveryTextReferencePointsAtAnEntry()
        {
            var broken = new List<string>();
            int references = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject t:Prefab", new[] { "Assets/_Game/Content", "Assets/_Game/Data", "Assets/_Game/_Core" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Localization/"))
                    continue;
                var targets = new List<UnityEngine.Object>();
                if (path.EndsWith(".prefab"))
                    targets.AddRange(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<Component>(true).Where(c => c is MonoBehaviour));
                else if (AssetDatabase.LoadMainAssetAtPath(path) is ScriptableObject asset)
                    targets.Add(asset);
                foreach (UnityEngine.Object target in targets)
                {
                    SerializedProperty property = new SerializedObject(target).GetIterator();
                    while (property.Next(true))
                    {
                        if (property.type != nameof(LocalizedString))
                            continue;
                        string table = property.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue;
                        if (string.IsNullOrEmpty(table))
                            continue;
                        references++;
                        long id = property.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue;
                        string key = property.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue;
                        StringTableCollection collection = Collection(table);
                        bool found = collection != null && (id != 0 ? collection.SharedData.GetEntry(id) : collection.SharedData.GetEntry(key)) != null;
                        if (!found)
                            broken.Add($"{path} {target.GetType().Name}.{property.propertyPath}: {table} / {(id != 0 ? id.ToString() : key)}");
                    }
                }
            }
            Assert.Greater(references, 60);
            Assert.IsEmpty(broken, string.Join("\n", broken));
        }

        [Test]
        public void ItemsAndContainers_AllHaveTheirText()
        {
            var missing = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game" }))
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (new SerializedObject(item).FindProperty("_displayName.m_TableEntryReference.m_KeyId").longValue == 0)
                    missing.Add(item.name);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                foreach (LootContainer container in AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<LootContainer>(true))
                    if (new SerializedObject(container).FindProperty("_label.m_TableEntryReference.m_KeyId").longValue == 0)
                        missing.Add(path);
            }
            Assert.IsEmpty(missing, "No text: " + string.Join(", ", missing));
        }

        [Test]
        public void HudLabels_AreBoundToTheirEntries()
        {
            GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(LocalizationKit.Hud);
            foreach ((string _, string path, string table, string key) in LocalizationKit.Labels.Where(l => l.asset == LocalizationKit.Hud))
            {
                Transform label = hud.transform.Find(path);
                Assert.IsNotNull(label, path);
                Assert.IsTrue(label.TryGetComponent(out LocalizeStringEvent localize), $"{path} has no LocalizeStringEvent");
                Assert.AreEqual(Entry(table, key), localize.StringReference.TableEntryReference.KeyId, path);
                Assert.AreEqual(1, localize.OnUpdateString.GetPersistentEventCount(), path);
                Assert.AreEqual(label.GetComponent<TMP_Text>(), localize.OnUpdateString.GetPersistentTarget(0), path);
                Assert.AreEqual("set_text", localize.OnUpdateString.GetPersistentMethodName(0), path);
            }
        }

        [Test]
        public void LanguageButtons_AreInThePauseMenu()
        {
            GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(LocalizationKit.Hud);
            Transform button = hud.transform.Find("PauseMenu/Page/Buttons/Language");
            Assert.IsNotNull(button);
            Assert.IsNotNull(button.GetComponent<LanguageButton>());
        }

        [Test]
        public void Fonts_HoldEveryCharacterOfTheTables()
        {
            string text = string.Concat(LocalizationCsv.CsvPaths().Select(File.ReadAllText));
            char[] characters = text.Where(c => !char.IsControl(c) && c != '﻿').Distinct().ToArray();
            foreach (string path in StaticFonts)
            {
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                Assert.AreEqual(AtlasPopulationMode.Static, font.atlasPopulationMode, path);
                string missing = new(characters.Where(c => !font.characterLookupTable.ContainsKey(c)).ToArray());
                Assert.IsEmpty(missing, $"{font.name} lacks '{missing}': re-run AgentScripts/UI/FontSetup.All");
            }
        }

        [Test]
        public void RarityNames_CoverEveryRarity()
        {
            FieldInfo field = typeof(InventoryScreen).GetField("RarityNames", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.AreEqual(Enum.GetValues(typeof(ItemRarity)).Length, ((LocalizedString[])field.GetValue(null)).Length);
        }

        [Test]
        public void Text_KeepsOneStringPerLanguage_AndFollowsTheSwitch()
        {
            Locale before = LocalizationSettings.SelectedLocale;
            LocalizedString resume = Loc.UI("menu.resume");
            try
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                string english = resume.Text();
                Assert.AreEqual("RESUME", english);
                Assert.AreSame(english, resume.Text());
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("vi");
                Assert.AreEqual("CHƠI TIẾP", resume.Text());
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("fr");
                Assert.AreEqual("REPRENDRE", resume.Text());
            }
            finally
            {
                LocalizationSettings.SelectedLocale = before;
            }
        }

        static bool Resolves(LocalizedString value)
        {
            if (value == null || value.IsEmpty)
                return false;
            StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(value.TableReference);
            if (collection == null)
                return false;
            TableEntryReference entry = value.TableEntryReference;
            return (entry.ReferenceType == TableEntryReference.Type.Id ? collection.SharedData.GetEntry(entry.KeyId) : collection.SharedData.GetEntry(entry.Key)) != null;
        }

        // Serialized references hold "GUID:<32 hex digits>", which the editor lookup takes as a name.
        static StringTableCollection Collection(string serialized) =>
            serialized.StartsWith("GUID:") && Guid.TryParse(serialized.Substring(5), out Guid guid)
                ? LocalizationEditorSettings.GetStringTableCollection(guid)
                : LocalizationEditorSettings.GetStringTableCollection(serialized);

        static long Entry(string table, string key) => LocalizationEditorSettings.GetStringTableCollection(table).SharedData.GetEntry(key).Id;
    }
}
