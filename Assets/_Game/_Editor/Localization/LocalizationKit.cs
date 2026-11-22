using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MB13.Aipocalypse.Core.LocalizationSystem;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace MB13.Aipocalypse.Editor.Localization
{
    // Editor helpers for wiring text to the string tables (LOCALIZATION.md): references by table GUID and entry Id (they
    // survive key renames in the CSV), and LocalizeStringEvent on fixed labels. Setup scripts in AgentScripts call these, and
    // ApplyLabels() at their end, so re-running one keeps its labels localized.
    public static class LocalizationKit
    {
        // Fixed labels in prefabs and scenes: (asset, path from the root, table, key). Labels set from code aren't here.
        public static readonly (string asset, string path, string table, string key)[] Labels =
        {
            (Hud, "SafeFrame/TopRight/Minimap/Content/Directions/N", Loc.HudTable, "compass.n"),
            (Hud, "SafeFrame/TopRight/Minimap/Content/Directions/E", Loc.HudTable, "compass.e"),
            (Hud, "SafeFrame/TopRight/Minimap/Content/Directions/S", Loc.HudTable, "compass.s"),
            (Hud, "SafeFrame/TopRight/Minimap/Content/Directions/W", Loc.HudTable, "compass.w"),
            (Hud, "SafeFrame/Center/RepairQte/Content/Title", Loc.HudTable, "qte.title"),
            (Hud, "SafeFrame/Center/RepairQte/Content/Hint", Loc.HudTable, "qte.hint"),
            (Hud, "SleepScreen/Chooser/Panel/Title", Loc.HudTable, "sleep.title"),
            (Hud, "SleepScreen/Chooser/Panel/Sleep/Label", Loc.HudTable, "sleep.sleep"),
            (Hud, "SleepScreen/Chooser/Panel/Cancel/Label", Loc.UITable, "menu.cancel"),
            (Hud, "Inventory/Left/Header/TokensLabel", Loc.UITable, "inventory.tokens"),
            (Hud, "Inventory/Left/EquipmentTitle", Loc.UITable, "inventory.equipment"),
            (Hud, "Inventory/Left/BackpackRow/Sort/Label", Loc.UITable, "inventory.sort"),
            (Hud, "Inventory/Loot/LootRow/Takeall/Label", Loc.UITable, "inventory.take_all"),
            (Hud, "PauseMenu/Page/Title", Loc.UITable, "menu.paused"),
            (Hud, "PauseMenu/Page/Buttons/Resume/Label", Loc.UITable, "menu.resume"),
            (Hud, "PauseMenu/Page/Buttons/Save/Label", Loc.UITable, "menu.save_game"),
            (Hud, "PauseMenu/Page/Buttons/Load/Label", Loc.UITable, "menu.load_game"),
            (Hud, "PauseMenu/Page/Buttons/Settings/Label", Loc.UITable, "menu.settings"),
            (Hud, "PauseMenu/Page/Buttons/MainMenu/Label", Loc.UITable, "menu.main_menu"),
            (Hud, "PauseMenu/Page/Buttons/SaveQuit/Label", Loc.UITable, "menu.save_quit"),
            (Hud, "PauseMenu/Page/Buttons/Quit/Label", Loc.UITable, "menu.quit_game"),
            (Hud, "PauseMenu/Slots/Back/Label", Loc.UITable, "menu.back"),
            (Hud, "PauseMenu/Slots/CopyPath/Label", Loc.UITable, "menu.copy_path"),
            (Hud, "PauseMenu/Slots/Rows/Row/Delete/Label", Loc.UITable, "menu.delete"),
            (Hud, "PauseMenu/Confirm/Panel/No/Label", Loc.UITable, "menu.cancel"),
            (MainMenu, "Page/PrototypeTag/Label", Loc.UITable, "menu.prototype_tag"),
            (MainMenu, "Page/Buttons/Continue/Label", Loc.UITable, "menu.continue"),
            (MainMenu, "Page/Buttons/Prototype/Label", Loc.UITable, "menu.prototype"),
            (MainMenu, "Page/Buttons/Load/Label", Loc.UITable, "menu.load_game"),
            (MainMenu, "Page/Buttons/Settings/Label", Loc.UITable, "menu.settings"),
            (MainMenu, "Page/Buttons/Quit/Label", Loc.UITable, "menu.quit"),
            (MainMenu, "Slots/Back/Label", Loc.UITable, "menu.back"),
            (MainMenu, "Slots/CopyPath/Label", Loc.UITable, "menu.copy_path"),
            (MainMenu, "Slots/Rows/Row/Delete/Label", Loc.UITable, "menu.delete"),
            (MainMenu, "Confirm/Panel/No/Label", Loc.UITable, "menu.cancel"),
        };

        public const string Hud = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
        // The scene's root GameObject is named after the file (MainMenu); paths start below it.
        public const string MainMenu = "Assets/_Game/Scenes/MainMenu.unity";
        const string EnglishCode = "en";

        public static SharedTableData.SharedTableEntry Entry(string table, string key, out StringTableCollection collection)
        {
            collection = LocalizationEditorSettings.GetStringTableCollection(table) ??
                         throw new ArgumentException($"No string table '{table}': add Data/Localization/Csv/{table}.csv");
            return collection.SharedData.GetEntry(key) ?? throw new ArgumentException($"No key '{key}' in {table}.csv");
        }

        // A reference by table GUID and entry Id.
        public static LocalizedString Ref(string table, string key)
        {
            SharedTableData.SharedTableEntry entry = Entry(table, key, out StringTableCollection collection);
            return new LocalizedString(collection.SharedData.TableCollectionNameGuid, entry.Id);
        }

        // The English text of an entry (what prefabs show while editing).
        public static string English(string table, string key)
        {
            SharedTableData.SharedTableEntry entry = Entry(table, key, out StringTableCollection collection);
            return (collection.GetTable(EnglishCode) as StringTable)?.GetEntry(entry.Id)?.Value ?? key;
        }

        // Points a serialized LocalizedString property (a field, a list element's field) at an entry.
        public static void Set(SerializedProperty property, string table, string key)
        {
            SharedTableData.SharedTableEntry entry = Entry(table, key, out StringTableCollection collection);
            property.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue =
                "GUID:" + collection.SharedData.TableCollectionNameGuid.ToString("N");
            property.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = entry.Id;
            property.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
        }

        // Points a serialized LocalizedString property at nothing (no text).
        public static void Clear(SerializedProperty property)
        {
            property.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = string.Empty;
            property.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = 0;
            property.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = string.Empty;
        }

        // Sets a LocalizedString field on an asset or component by its property path; true when it changed.
        public static bool Set(UnityEngine.Object target, string propertyPath, string table, string key)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyPath) ??
                                          throw new ArgumentException($"{target.name} has no '{propertyPath}'");
            Set(property, table, key);
            if (!so.ApplyModifiedPropertiesWithoutUndo())
                return false;
            EditorUtility.SetDirty(target);
            return true;
        }

        // A LocalizeStringEvent on the label's GameObject that writes the entry into it at runtime (every language switch
        // too). The label keeps the English text for editing; the event runs at runtime only, so no prefab or scene is
        // rewritten when the editor's language preview changes.
        public static void BindLabel(TMP_Text label, string table, string key)
        {
            if (!label.TryGetComponent(out LocalizeStringEvent localize))
                localize = label.gameObject.AddComponent<LocalizeStringEvent>();
            var so = new SerializedObject(localize);
            Set(so.FindProperty("m_StringReference"), table, key);
            SerializedProperty calls = so.FindProperty("m_UpdateString.m_PersistentCalls.m_Calls");
            calls.arraySize = 1;
            SerializedProperty call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = label;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue =
                $"{typeof(TMP_Text).FullName}, {typeof(TMP_Text).Assembly.GetName().Name}";
            call.FindPropertyRelative("m_MethodName").stringValue = "set_text";
            call.FindPropertyRelative("m_Mode").enumValueIndex = (int)PersistentListenerMode.EventDefined;
            call.FindPropertyRelative("m_CallState").enumValueIndex = (int)UnityEventCallState.RuntimeOnly;
            so.ApplyModifiedPropertiesWithoutUndo();
            label.text = English(table, key);
            EditorUtility.SetDirty(localize);
            EditorUtility.SetDirty(label);
        }

        // Binds every label in Labels; prefabs are saved, scenes opened additively, saved and closed again.
        public static string ApplyLabels()
        {
            var log = new List<string>();
            foreach (IGrouping<string, (string asset, string path, string table, string key)> group in Labels.GroupBy(l => l.asset))
            {
                if (group.Key.EndsWith(".prefab"))
                {
                    GameObject root = PrefabUtility.LoadPrefabContents(group.Key);
                    try
                    {
                        log.Add($"{group.Key}: {Bind(root.transform, group, log)} labels");
                        PrefabUtility.SaveAsPrefabAsset(root, group.Key);
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                    continue;
                }
                Scene scene = SceneManager.GetSceneByPath(group.Key);
                bool opened = !scene.isLoaded;
                if (!opened && scene.isDirty)
                {
                    log.Add($"{group.Key}: open with unsaved changes, skipped (save or close it, then run again)");
                    continue;
                }
                if (opened)
                    scene = EditorSceneManager.OpenScene(group.Key, OpenSceneMode.Additive);
                GameObject sceneRoot = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Path.GetFileNameWithoutExtension(group.Key));
                if (sceneRoot == null)
                    log.Add($"{group.Key}: no root '{Path.GetFileNameWithoutExtension(group.Key)}'");
                else
                {
                    log.Add($"{group.Key}: {Bind(sceneRoot.transform, group, log)} labels");
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                }
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
            }
            return string.Join("\n", log);
        }

        static int Bind(Transform root, IEnumerable<(string asset, string path, string table, string key)> labels, List<string> log)
        {
            int bound = 0;
            foreach ((string _, string path, string table, string key) in labels)
            {
                Transform target = root.Find(path);
                if (target == null || !target.TryGetComponent(out TMP_Text label))
                {
                    log.Add($"  missing label {root.name}/{path}");
                    continue;
                }
                BindLabel(label, table, key);
                bound++;
            }
            return bound;
        }
    }
}
