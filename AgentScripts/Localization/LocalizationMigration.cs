using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.AnimalSystem;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WorldTimeSystem;
using MB13.Aipocalypse.Editor.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Moves the game's text onto the string tables (LOCALIZATION.md), re-runnable: points every display-text field of the
// data assets and prefabs at its entry (by table GUID + Id), adds the Language button to the pause and main menus, and
// binds the fixed labels (LocalizationKit.Labels). `All` runs everything; each step also runs alone.
public static class LocalizationMigration
{
    const string MainMenuScene = "Assets/_Game/Scenes/MainMenu.unity";

    static readonly (string path, string key)[] Items =
    {
        ("Assets/_Game/Content/Items/Adrenaline/Adrenaline.asset", "item.adrenaline"),
        ("Assets/_Game/Content/Items/Backpack/Backpack.asset", "item.backpack"),
        ("Assets/_Game/Content/Items/Bandage/Bandage.asset", "item.bandage"),
        ("Assets/_Game/Content/Items/Lantern/Lantern.asset", "item.lantern"),
        ("Assets/_Game/Content/Items/MedicBox/MedicBox.asset", "item.first_aid_kit"),
        ("Assets/_Game/Content/Items/StaminaShot/StaminaShot.asset", "item.stamina_shot"),
        ("Assets/_Game/Content/Items/Tokens/Tokens.asset", "item.tokens"),
        ("Assets/_Game/Content/Tools/Hammer/Hammer.asset", "item.hammer"),
        ("Assets/_Game/Content/Weapons/AK47/AK47.asset", "item.ak47"),
        ("Assets/_Game/Content/Weapons/DoubleBarrel/DoubleBarrel.asset", "item.double_barrel"),
        ("Assets/_Game/Content/Weapons/FireAxe/FireAxe.asset", "item.fire_axe"),
        ("Assets/_Game/Content/Weapons/MP5/MP5.asset", "item.mp5"),
        ("Assets/_Game/Content/Weapons/Pistol/Pistol.asset", "item.pistol"),
        ("Assets/_Game/Content/Weapons/PumpShotgun/PumpShotgun.asset", "item.pump_shotgun"),
        ("Assets/_Game/Content/Weapons/_Shared/Ammo/PistolAmmo.asset", "item.pistol_ammo"),
        ("Assets/_Game/Content/Weapons/_Shared/Ammo/RifleAmmo.asset", "item.rifle_ammo"),
        ("Assets/_Game/Content/Weapons/_Shared/Ammo/ShotgunShells.asset", "item.shotgun_shells"),
    };

    static readonly (string path, string key)[] Animals =
    {
        ("Assets/_Game/Content/Animals/Chicken/Chicken.asset", "animal.chicken"),
        ("Assets/_Game/Content/Animals/Cow/Cow.asset", "animal.cow"),
        ("Assets/_Game/Content/Animals/Pig/Pig.asset", "animal.pig"),
        ("Assets/_Game/Content/Animals/Sheep/Sheep.asset", "animal.sheep"),
    };

    static readonly (string path, string key)[] Barriers =
    {
        ("Assets/_Game/Content/Structures/WoodFence/WoodFence.asset", "barrier.fence"),
        ("Assets/_Game/Content/Structures/WoodFence/WoodFence_Spiked.asset", "barrier.spiked_fence"),
        ("Assets/_Game/Content/Structures/WoodGate/WoodGate.asset", "barrier.gate"),
        ("Assets/_Game/Content/Structures/WoodGate/WoodGate_Double.asset", "barrier.gate"),
    };

    // LootContainer labels on prefabs (variants inherit them).
    static readonly (string path, string table, string key)[] Containers =
    {
        ("Assets/_Game/Content/Props/LootCrate/LootCrate.prefab", Loc.InteractionTable, "container.crate"),
        ("Assets/_Game/Content/Enemies/Goblin/Goblin.prefab", Loc.CreaturesTable, "enemy.goblin"),
    };

    // EquipmentConfig captions by the slot's Name; quick slots have none.
    static readonly Dictionary<string, string> SlotCaptions = new()
    {
        { "Firearm", "slot.firearm" }, { "Melee", "slot.melee" }, { "Head", "slot.head" }, { "Body", "slot.body" },
        { "Face", "slot.face" }, { "Backpack", "slot.backpack" }, { "Light", "slot.light" },
    };

    static readonly Dictionary<DayPhase, string> PhaseKeys = new()
    {
        { DayPhase.Dawn, "phase.dawn" }, { DayPhase.Morning, "phase.morning" }, { DayPhase.Afternoon, "phase.afternoon" },
        { DayPhase.Dusk, "phase.dusk" }, { DayPhase.Evening, "phase.evening" }, { DayPhase.Midnight, "phase.midnight" },
    };

    public static string All() => string.Join("\n", Assets(), LanguageButtons(), LocalizationKit.ApplyLabels(), Check());

    public static string Assets()
    {
        var log = new List<string>();
        foreach ((string path, string key) in Items)
            Field(Load<ItemDefinition>(path), "_displayName", Loc.ItemsTable, key, log);
        foreach ((string path, string key) in Animals)
            Field(Load<AnimalDefinition>(path), "DisplayName", Loc.CreaturesTable, key, log);
        foreach ((string path, string key) in Barriers)
            Field(Load<BarrierDefinition>(path), "DisplayName", Loc.InteractionTable, key, log);

        foreach ((string path, string table, string key) in Containers)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                LootContainer container = root.GetComponentInChildren<LootContainer>(true);
                if (LocalizationKit.Set(container, "_label", table, key))
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    log.Add($"{path}: label {key}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        var config = Load<EquipmentConfig>("Assets/_Game/Data/Equipment/EquipmentConfig.asset");
        var so = new SerializedObject(config);
        foreach (string list in new[] { "_slots", "_gear" })
        {
            SerializedProperty slots = so.FindProperty(list);
            for (int i = 0; i < slots.arraySize; i++)
            {
                SerializedProperty slot = slots.GetArrayElementAtIndex(i);
                if (SlotCaptions.TryGetValue(slot.FindPropertyRelative("Name").stringValue, out string key))
                    LocalizationKit.Set(slot.FindPropertyRelative("Caption"), Loc.ItemsTable, key);
            }
        }
        if (so.ApplyModifiedPropertiesWithoutUndo())
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
            log.Add("EquipmentConfig: slot captions");
        }

        var clock = Load<ClockStyle>("Assets/_Game/Data/UI/ClockStyle.asset");
        clock.DayFormat = LocalizationKit.Ref(Loc.HudTable, "clock.day");
        foreach ((DayPhase phase, string key) in PhaseKeys)
        {
            Color color = clock.Phases.TryGetValue(phase, out PhaseLabel old) ? old.Color : clock.DayColor;
            clock.Phases[phase] = new PhaseLabel(LocalizationKit.Ref(Loc.HudTable, key), color);
        }
        Save(clock, log);

        var breach = Load<BreachAlertStyle>("Assets/_Game/Data/UI/BreachAlertStyle.asset");
        breach.FenceText = LocalizationKit.Ref(Loc.HudTable, "breach.fence");
        breach.GateText = LocalizationKit.Ref(Loc.HudTable, "breach.gate");
        breach.AheadSuffix = LocalizationKit.Ref(Loc.HudTable, "breach.ahead");
        breach.BehindSuffix = LocalizationKit.Ref(Loc.HudTable, "breach.behind");
        breach.LeftSuffix = LocalizationKit.Ref(Loc.HudTable, "breach.left");
        breach.RightSuffix = LocalizationKit.Ref(Loc.HudTable, "breach.right");
        Save(breach, log);

        var raid = Load<RaidBannerStyle>("Assets/_Game/Data/UI/RaidBannerStyle.asset");
        raid.StartTitle = LocalizationKit.Ref(Loc.HudTable, "raid.start_title");
        raid.StartSubtitle = LocalizationKit.Ref(Loc.HudTable, "raid.start_subtitle");
        raid.VictoryTitle = LocalizationKit.Ref(Loc.HudTable, "raid.cleared_title");
        raid.VictorySubtitle = LocalizationKit.Ref(Loc.HudTable, "raid.cleared_subtitle");
        raid.DawnTitle = LocalizationKit.Ref(Loc.HudTable, "raid.dawn_title");
        raid.DawnSubtitle = LocalizationKit.Ref(Loc.HudTable, "raid.dawn_subtitle");
        Save(raid, log);

        var death = Load<DeathScreenStyle>("Assets/_Game/Data/UI/DeathScreenStyle.asset");
        death.Title = LocalizationKit.Ref(Loc.HudTable, "death.title");
        death.LoadingText = LocalizationKit.Ref(Loc.HudTable, "death.loading");
        death.ContinueText = LocalizationKit.Ref(Loc.HudTable, "death.continue");
        Save(death, log);

        var prompt = Load<InteractPromptStyle>("Assets/_Game/Data/UI/InteractPromptStyle.asset");
        prompt.HoldPrefix = LocalizationKit.Ref(Loc.InteractionTable, "hold_prefix");
        Save(prompt, log);

        var save = Load<SaveSettings>("Assets/_Game/Data/Save/SaveSettings.asset");
        save.LoadingText = LocalizationKit.Ref(Loc.UITable, "loading");
        Save(save, log);
        return string.Join("\n", log);
    }

    // A Language button under Settings in the pause menu (HUD.prefab) and the main menu scene: a copy of the Settings
    // button with a LanguageButton. The pause menu's column grows by a button and its hint moves down with it.
    public static string LanguageButtons()
    {
        var log = new List<string>();
        GameObject hud = PrefabUtility.LoadPrefabContents(LocalizationKit.Hud);
        try
        {
            Transform buttons = hud.transform.Find("PauseMenu/Page/Buttons");
            if (AddLanguageButton(buttons, log))
            {
                var column = (RectTransform)buttons;
                column.sizeDelta = new Vector2(column.sizeDelta.x, 640f);
                var hint = (RectTransform)hud.transform.Find("PauseMenu/Page/Hint");
                hint.anchoredPosition = new Vector2(hint.anchoredPosition.x, -890f);
                PrefabUtility.SaveAsPrefabAsset(hud, LocalizationKit.Hud);
                log.Add("HUD.prefab: Language button in the pause menu, hint moved down");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hud);
        }

        Scene scene = SceneManager.GetSceneByPath(MainMenuScene);
        bool opened = !scene.isLoaded;
        if (opened)
            scene = EditorSceneManager.OpenScene(MainMenuScene, OpenSceneMode.Additive);
        GameObject root = scene.GetRootGameObjects().First(g => g.name == "MainMenu");
        Transform mainButtons = root.transform.Find("Page/Buttons");
        if (AddLanguageButton(mainButtons, log))
        {
            ((RectTransform)mainButtons).sizeDelta = new Vector2(((RectTransform)mainButtons).sizeDelta.x, 640f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.Add("MainMenu.unity: Language button");
        }
        if (opened)
            EditorSceneManager.CloseScene(scene, true);
        return string.Join("\n", log);
    }

    static bool AddLanguageButton(Transform buttons, List<string> log)
    {
        if (buttons.Find("Language") != null)
        {
            log.Add($"{buttons.root.name}: Language button already there");
            return false;
        }
        Transform settings = buttons.Find("Settings");
        GameObject copy = Object.Instantiate(settings.gameObject, buttons);
        copy.name = "Language";
        copy.transform.SetSiblingIndex(settings.GetSiblingIndex() + 1);
        var button = copy.GetComponent<Button>();
        button.interactable = true;
        button.onClick = new Button.ButtonClickedEvent();
        foreach (UnityEngine.Localization.Components.LocalizeStringEvent bound in copy.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))
            Object.DestroyImmediate(bound);
        TMP_Text label = copy.GetComponentInChildren<TMP_Text>(true);
        label.text = "LANGUAGE: ENGLISH";
        var language = copy.AddComponent<LanguageButton>();
        var so = new SerializedObject(language);
        so.FindProperty("_label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
        return true;
    }

    // Every LootContainer under _Game with an empty label, and every item definition without a name.
    public static string Check()
    {
        var problems = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (LootContainer container in prefab.GetComponentsInChildren<LootContainer>(true))
                if (new SerializedObject(container).FindProperty("_label.m_TableReference.m_TableCollectionName").stringValue == string.Empty)
                    problems.Add($"{path}: LootContainer without a label");
        }
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDefinition", new[] { "Assets/_Game" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (new SerializedObject(item).FindProperty("_displayName.m_TableReference.m_TableCollectionName").stringValue == string.Empty)
                problems.Add($"{item.name}: no display name");
        }
        return problems.Count == 0 ? "check: every container and item has its text" : "check:\n  " + string.Join("\n  ", problems);
    }

    static void Field(Object target, string field, string table, string key, List<string> log)
    {
        if (LocalizationKit.Set(target, field, table, key))
        {
            AssetDatabase.SaveAssetIfDirty(target);
            log.Add($"{target.name}: {key}");
        }
    }

    static void Save(Object asset, List<string> log)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        log.Add(asset.name);
    }

    static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new System.Exception($"missing {typeof(T).Name} {path}");
}
