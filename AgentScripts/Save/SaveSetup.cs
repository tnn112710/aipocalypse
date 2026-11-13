using System;
using System.Collections.Generic;
using System.Linq;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Editor.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the save system's content: Data/Save/SaveSettings, Data/Equipment/ItemCatalog (collected), Data/UI/MenuStyle (look
// and the UI sounds in Data/Audio/UI), the root scope's references, the PauseMenu and SaveToast in HUD.prefab,
// WorldSaveInstaller + SaveIds in WorldMap, the MainMenu scene, and the Build Settings scene list (MainMenu first). Safe to
// re-run: UI nodes are rebuilt, assets and ids kept. The menus share one look: Saira fonts (AgentScripts/UI/FontSetup),
// Synty Interface Apocalypse's tape strip and shade, text buttons that light up when hovered or selected.
public static class SaveSetup
{
    const string SettingsPath = "Assets/_Game/Data/Save/SaveSettings.asset";
    const string CatalogPath = "Assets/_Game/Data/Equipment/ItemCatalog.asset";
    const string StylePath = "Assets/_Game/Data/UI/MenuStyle.asset";
    const string FontDir = "Assets/_Game/Data/UI/Fonts/";
    const string RootPath = "Assets/_Game/_Core/_Bootstrap/ProjectLifetimeScope.prefab";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string MainMenuPath = "Assets/_Game/Scenes/MainMenu.unity";
    // The title screen's background: a shot of WorldMap at sunset.
    const string BackgroundPath = "Assets/_Game/Data/UI/MainMenu/Background.png";
    const string SyntyHud = "Assets/Synty/InterfaceApocalypseHUD/";
    const string TapePath = SyntyHud + "Sprites/HUD/SPR_HUD_Apocalypse_Box_Small_02.png";
    const string ShadePath = SyntyHud + "Sprites/FX/SPR_FX_Apocalypse_Gradient_Horizontal_01.png";
    const string UiAudio = "Assets/_Game/Data/Audio/UI";
    const string UiLibrary = "Assets/_Game/Data/Audio/UI.asset";
    const string UiClips = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Interface & Item Sounds Lite - Version 2/Interface/";
    // Over the HUD (10), the inventory (50) and the death screen (100).
    const int PauseSorting = 200;

    public static object All()
    {
        var log = new List<string> { Assets(), Hud(), World(), MainMenu(), BuildScenes(), LocalizationKit.ApplyLabels() };
        return string.Join("\n", log);
    }

    public static string Assets()
    {
        var log = new List<string>();
        var bold = Load<TMP_FontAsset>(FontDir + "SairaBold.asset");

        var settings = LoadOrCreate<SaveSettings>(SettingsPath, log);
        settings.LoadingFont = bold;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);

        var catalog = LoadOrCreate<ItemCatalog>(CatalogPath, log);
        log.Add($"ItemCatalog: {catalog.Collect()} items");
        AssetDatabase.SaveAssetIfDirty(catalog);

        // Every value from MenuStyle's defaults, then the references.
        var style = LoadOrCreate<MenuStyle>(StylePath, log);
        var defaults = ScriptableObject.CreateInstance<MenuStyle>();
        EditorUtility.CopySerialized(defaults, style);
        Object.DestroyImmediate(defaults);
        style.name = "MenuStyle";
        style.Font = bold;
        style.TitleFont = Load<TMP_FontAsset>(FontDir + "SairaHeader.asset");
        style.BodyFont = Load<TMP_FontAsset>(FontDir + "SairaBody.asset");
        style.ButtonSprite = Load<Sprite>(TapePath);
        style.ShadeSprite = Load<Sprite>(ShadePath);
        AudioAsset library = Library(log);
        style.HoverSound = Sound(library, "Menu_Hover", 0.35f, 0.04f, "Interface Click 8-1.wav", log);
        style.ClickSound = Sound(library, "Menu_Click", 0.6f, 0.03f, "Interface Click 15-1.wav", log);
        style.BackSound = Sound(library, "Menu_Back", 0.5f, 0.03f, "Interface Click 19-4.wav", log);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);

        GameObject root = PrefabUtility.LoadPrefabContents(RootPath);
        try
        {
            var scope = root.GetComponent<ProjectLifetimeScope>();
            var so = new SerializedObject(scope);
            Set(so, "_saveSettings", settings);
            Set(so, "_itemCatalog", catalog);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, RootPath);
            log.Add("ProjectLifetimeScope: SaveSettings + ItemCatalog assigned");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return string.Join("\n", log);
    }

    // ---- UI sounds: Data/Audio/UI (BroAudio library + one entity per sound, as DefenseSetup) ----

    static AudioAsset Library(List<string> log)
    {
        if (!AssetDatabase.IsValidFolder(UiAudio))
            AssetDatabase.CreateFolder("Assets/_Game/Data/Audio", "UI");
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(UiLibrary);
        if (library != null)
            return library;
        library = ScriptableObject.CreateInstance<AudioAsset>();
        AssetDatabase.CreateAsset(library, UiLibrary);
        var so = new SerializedObject(library);
        so.FindProperty("<AssetName>k__BackingField").stringValue = "UI";
        so.FindProperty("_assetGUID").stringValue = AssetDatabase.AssetPathToGUID(UiLibrary);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(library);
        log.Add("created library " + UiLibrary);
        return library;
    }

    // A 2D one-shot UI entity: one clip, a volume and a little random pitch.
    static SoundID Sound(AudioAsset library, string name, float volume, float pitchRandom, string clipFile, List<string> log)
    {
        string path = $"{UiAudio}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.UI);
            AssetDatabase.CreateAsset(entity, path);
            log.Add("created " + path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioAsset>k__BackingField").objectReferenceValue = library;
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)BroAudioType.UI;
        so.FindProperty("<Loop>k__BackingField").boolValue = false;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = 1f;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)MulticlipsPlayMode.Single;
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = pitchRandom > 0f ? 1 : 0;
        SerializedProperty clips = so.FindProperty("Clips");
        clips.arraySize = 1;
        SerializedProperty clip = clips.GetArrayElementAtIndex(0);
        clip.FindPropertyRelative("AudioClip").objectReferenceValue = Load<AudioClip>(UiClips + clipFile);
        clip.FindPropertyRelative("Volume").floatValue = 1f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entity);
        AssetDatabase.SaveAssetIfDirty(entity);
        return new SoundID(entity);
    }

    // ---- HUD: pause menu and the "game saved" line ----

    public static string Hud()
    {
        MenuStyle style = Style();
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Remove(root.transform, "PauseMenu");
            Remove(root.transform, "SaveToast");

            RectTransform toastNode = Ui("SaveToast", root.transform);
            Stretch(toastNode);
            toastNode.gameObject.AddComponent<Canvas>();
            TMP_Text toastText = Text("Text", toastNode, style, 28f, TextAlignmentOptions.BottomRight, style.Text, style.Font);
            Pin(toastText.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 230f), new Vector2(700f, 40f));
            var toast = toastNode.gameObject.AddComponent<SaveToastHud>();
            var toastSo = new SerializedObject(toast);
            Set(toastSo, "_text", toastText);
            toastSo.ApplyModifiedPropertiesWithoutUndo();

            RectTransform node = Ui("PauseMenu", root.transform);
            Stretch(node);
            node.SetAsLastSibling();
            var canvas = node.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = PauseSorting;
            node.gameObject.AddComponent<GraphicRaycaster>();
            // The HUD fades out on death; the menu doesn't follow it.
            node.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            Box("Backdrop", node, style, style.Backdrop, true, stretch: true);
            Shade(node, style);

            RectTransform page = Ui("Page", node);
            Stretch(page);
            TMP_Text title = Text("Title", page, style, 120f, TextAlignmentOptions.MidlineLeft, style.Text, style.TitleFont);
            title.text = "PAUSED";
            Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(140f, -100f), new Vector2(900f, 150f));
            RectTransform column = TextColumn(page, new Vector2(110f, -290f));
            Button resume = TextButton("Resume", "RESUME", column, style);
            Button save = TextButton("Save", "SAVE GAME", column, style);
            Button load = TextButton("Load", "LOAD GAME", column, style);
            Button settings = TextButton("Settings", "SETTINGS", column, style);
            LanguageButton(column, style);
            Button mainMenu = TextButton("MainMenu", "MAIN MENU", column, style);
            Button saveQuit = TextButton("SaveQuit", "SAVE & QUIT", column, style);
            Button quit = TextButton("Quit", "QUIT GAME", column, style);
            TMP_Text hint = Text("Hint", page, style, 26f, TextAlignmentOptions.TopLeft, style.InfoText, style.BodyFont);
            Pin(hint.rectTransform, new Vector2(0f, 1f), new Vector2(140f, -825f), new Vector2(1200f, 40f));
            // Eight buttons in the room seven had, so the column stays clear of the health bars.
            foreach (LayoutElement element in column.GetComponentsInChildren<LayoutElement>())
                element.preferredHeight = 62f;

            SaveSlotsPanel slots = SlotsPanel(node, style);
            ConfirmDialog confirm = Confirm(node, style);
            SetRef(slots, "_confirm", confirm);
            MenuSounds sounds = Sounds(node, style);

            GameObject events = Events(node);
            events.SetActive(false);

            var menu = node.gameObject.AddComponent<PauseMenu>();
            var so = new SerializedObject(menu);
            Set(so, "_eventSystem", events);
            Set(so, "_page", page.gameObject);
            Set(so, "_resume", resume);
            Set(so, "_save", save);
            Set(so, "_load", load);
            Set(so, "_settings", settings);
            Set(so, "_mainMenu", mainMenu);
            Set(so, "_saveQuit", saveQuit);
            Set(so, "_quit", quit);
            Set(so, "_hint", hint);
            Set(so, "_slots", slots);
            Set(so, "_confirm", confirm);
            Set(so, "_sounds", sounds);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return "HUD.prefab: SaveToast (bottom right) + PauseMenu (own canvas, sorting 200, page / slots / confirm, sounds, EventSystem off)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- WorldMap: the scene installer and an id on every fence segment and crate ----

    public static string World()
    {
        // Reopened from disk: the open copy only holds editor noise from Play (ambient colours, HUD layout overrides).
        Scene scene = EditorSceneManager.OpenScene(WorldMapPath, OpenSceneMode.Single);
        var log = new List<string>();
        GameLifetimeScope scope = Object.FindAnyObjectByType<GameLifetimeScope>();
        if (scope == null)
            throw new Exception("WorldMap has no GameLifetimeScope");
        if (scope.GetComponent<WorldSaveInstaller>() == null)
        {
            scope.gameObject.AddComponent<WorldSaveInstaller>();
            log.Add("WorldSaveInstaller added");
        }

        var targets = new List<GameObject>();
        targets.AddRange(Object.FindObjectsByType<BarrierSegment>(FindObjectsInactive.Include).Select(b => b.gameObject));
        targets.AddRange(Object.FindObjectsByType<LootContainer>(FindObjectsInactive.Include).Select(c => c.gameObject));

        // The component goes on the prefab (instances then share it, and a prefab rebuild that adds it can't double it);
        // only the id is per instance.
        var prefabs = new HashSet<string>();
        foreach (GameObject target in targets)
        {
            if (target.GetComponent<SaveId>() != null || !PrefabUtility.IsPartOfPrefabInstance(target))
                continue;
            GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(target);
            string path = AssetDatabase.GetAssetPath(source);
            if (!prefabs.Add(path))
                continue;
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform node = FindByPath(contents.transform, AnimationUtility.CalculateTransformPath(source.transform, source.transform.root));
                if (node != null && node.GetComponent<SaveId>() == null)
                {
                    node.gameObject.AddComponent<SaveId>();
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                    log.Add("SaveId added to " + path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        var ids = new HashSet<string>();
        int added = 0, generated = 0;
        foreach (GameObject target in targets)
        {
            if (!target.TryGetComponent(out SaveId id))
            {
                id = target.AddComponent<SaveId>();
                added++;
            }
            if (!id.HasId || !ids.Add(id.Id))
            {
                id.Regenerate();
                ids.Add(id.Id);
                generated++;
            }
        }
        log.Add($"SaveIds: {targets.Count} objects ({added} components added to non-prefab objects, {generated} ids generated)");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add("WorldMap saved");
        return string.Join("\n", log);
    }

    // ---- MainMenu scene ----

    // The classic survival title screen: the world at sunset, darkened on the left under a big title and a column of text
    // buttons.
    public static string MainMenu()
    {
        MenuStyle style = Style();
        Sprite background = Load<Sprite>(BackgroundPath);

        // Built as the only open scene, then the previous one is opened again: building next to an open scene marks that
        // one modified, and Unity won't overwrite the menu scene while it's open.
        Scene previous = SceneManager.GetActiveScene();
        if (previous.isDirty)
            throw new Exception($"{previous.path} has unsaved changes: save or discard them first");
        string previousPath = previous.path;
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            var cameraObject = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;

            var canvasObject = new GameObject("MainMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // Injects the menu from the root scope (no GameLifetimeScope here).
            canvasObject.AddComponent<HudScope>();
            Transform node = canvasObject.transform;

            // Fills the screen at any aspect, cropping the overflow.
            RectTransform backdrop = Ui("Background", node);
            Stretch(backdrop);
            var photo = backdrop.gameObject.AddComponent<Image>();
            photo.sprite = background;
            photo.raycastTarget = false;
            var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = background.rect.width / background.rect.height;
            Shade(node, style);

            RectTransform page = Ui("Page", node);
            Stretch(page);
            TMP_Text titleTop = Text("Title", page, style, 92f, TextAlignmentOptions.MidlineLeft, style.Text, style.TitleFont);
            titleTop.text = "HASTA LA VISTA,";
            Pin(titleTop.rectTransform, new Vector2(0f, 1f), new Vector2(140f, -110f), new Vector2(1100f, 110f));
            TMP_Text titleMaybe = Text("Maybe", page, style, 150f, TextAlignmentOptions.MidlineLeft, style.Tape, style.TitleFont);
            titleMaybe.text = "MAYBE";
            Pin(titleMaybe.rectTransform, new Vector2(0f, 1f), new Vector2(150f, -200f), new Vector2(600f, 170f));
            titleMaybe.rectTransform.localEulerAngles = new Vector3(0f, 0f, 4f);
            Image tag = Picture("PrototypeTag", page, style.ButtonSprite, style.Tape);
            tag.type = Image.Type.Sliced;
            tag.pixelsPerUnitMultiplier = 2f;
            Pin(tag.rectTransform, new Vector2(0f, 1f), new Vector2(560f, -300f), new Vector2(230f, 52f));
            tag.rectTransform.localEulerAngles = new Vector3(0f, 0f, -3f);
            TMP_Text tagText = Text("Label", tag.transform, style, 30f, TextAlignmentOptions.Center, new Color(0.08f, 0.08f, 0.08f, 1f), style.Font);
            tagText.text = "PROTOTYPE";
            Stretch(tagText.rectTransform);

            RectTransform column = TextColumn(page, new Vector2(110f, -390f));
            Button continueButton = TextButton("Continue", "CONTINUE", column, style);
            TMP_Text continueInfo = Text("ContinueInfo", column, style, 24f, TextAlignmentOptions.MidlineLeft, style.InfoText, style.BodyFont);
            continueInfo.margin = new Vector4(30f, -6f, 0f, 0f);
            continueInfo.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            Button prototype = TextButton("Prototype", "PROTOTYPE", column, style);
            Button load = TextButton("Load", "LOAD GAME", column, style);
            Button settings = TextButton("Settings", "SETTINGS", column, style);
            LanguageButton(column, style);
            Button quit = TextButton("Quit", "QUIT", column, style);
            TMP_Text status = Text("Status", page, style, 26f, TextAlignmentOptions.MidlineLeft, style.InfoText, style.BodyFont);
            Pin(status.rectTransform, new Vector2(0f, 1f), new Vector2(140f, -950f), new Vector2(1200f, 40f));
            TMP_Text version = Text("Version", page, style, 22f, TextAlignmentOptions.MidlineLeft, new Color(1f, 1f, 1f, 0.5f), style.BodyFont);
            Pin(version.rectTransform, new Vector2(0f, 0f), new Vector2(140f, 36f), new Vector2(800f, 32f));

            SaveSlotsPanel slots = SlotsPanel(node, style);
            ConfirmDialog confirm = Confirm(node, style);
            SetRef(slots, "_confirm", confirm);
            MenuSounds sounds = Sounds(node, style);
            GameObject events = Events(node);

            var screen = canvasObject.AddComponent<MainMenuScreen>();
            var so = new SerializedObject(screen);
            Set(so, "_eventSystem", events);
            Set(so, "_page", page.gameObject);
            Set(so, "_continue", continueButton);
            Set(so, "_continueInfo", continueInfo);
            Set(so, "_prototype", prototype);
            Set(so, "_load", load);
            Set(so, "_settings", settings);
            Set(so, "_quit", quit);
            Set(so, "_status", status);
            Set(so, "_version", version);
            Set(so, "_slots", slots);
            Set(so, "_confirm", confirm);
            Set(so, "_sounds", sounds);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (!EditorSceneManager.SaveScene(scene, MainMenuPath))
                throw new Exception($"couldn't save {MainMenuPath}");
            return "MainMenu.unity: world shot, shade, title, text buttons (Continue, Prototype, Load, Settings, Quit), slots, confirm, sounds, EventSystem";
        }
        finally
        {
            if (!string.IsNullOrEmpty(previousPath))
                EditorSceneManager.OpenScene(previousPath);
        }
    }

    static Image Picture(string name, Transform parent, Sprite sprite, Color color)
    {
        RectTransform rect = Ui(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception($"missing {typeof(T).Name} {path}");

    public static string BuildScenes()
    {
        var scenes = new List<EditorBuildSettingsScene> { new(MainMenuPath, true), new(WorldMapPath, true) };
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (existing.path != MainMenuPath && existing.path != WorldMapPath)
                scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
        return "Build Settings: " + string.Join(", ", scenes.Select(s => s.path));
    }

    // ---- shared pieces ----

    // "Select Save Slot": back button, title, copy-path button, a row template, a status line. Inactive.
    static SaveSlotsPanel SlotsPanel(Transform parent, MenuStyle style)
    {
        RectTransform node = Ui("Slots", parent);
        Stretch(node);
        Button back = TextButton("Back", "< BACK", node, style);
        Pin((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(40f, -50f), new Vector2(230f, 80f));
        TMP_Text title = Text("Title", node, style, 84f, TextAlignmentOptions.MidlineLeft, style.Text, style.TitleFont);
        title.text = "SAVE GAME";
        Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(300f, -40f), new Vector2(600f, 100f));
        Button copy = Btn("CopyPath", "Copy Save File Path", node, style, 0f);
        Pin((RectTransform)copy.transform, new Vector2(0f, 1f), new Vector2(900f, -68f), new Vector2(290f, 50f));
        TMP_Text copyLabel = copy.GetComponentInChildren<TMP_Text>();
        copyLabel.font = style.BodyFont;
        copyLabel.fontSize = 22f;

        // Six rows in the editor (Save 1-5 and the editor's slot).
        RectTransform rows = Ui("Rows", node);
        Pin(rows, new Vector2(0f, 1f), new Vector2(160f, -170f), new Vector2(820f, 620f));
        var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        SaveSlotRow row = Row(rows, style);

        TMP_Text status = Text("Status", node, style, 26f, TextAlignmentOptions.TopLeft, style.InfoText, style.BodyFont);
        Pin(status.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -790f), new Vector2(1400f, 40f));

        var panel = node.gameObject.AddComponent<SaveSlotsPanel>();
        var so = new SerializedObject(panel);
        Set(so, "_style", style);
        Set(so, "_title", title);
        Set(so, "_back", back);
        Set(so, "_copyPath", copy);
        Set(so, "_status", status);
        Set(so, "_rowTemplate", row);
        so.ApplyModifiedPropertiesWithoutUndo();
        node.gameObject.SetActive(false);
        return panel;
    }

    static SaveSlotRow Row(Transform parent, MenuStyle style)
    {
        RectTransform node = Ui("Row", parent);
        node.sizeDelta = new Vector2(800f, 86f);
        Button button = Btn("Button", string.Empty, node, style, 0f);
        Pin((RectTransform)button.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(660f, 86f));
        Object.DestroyImmediate(button.GetComponentInChildren<TMP_Text>().gameObject);
        TMP_Text title = Text("Title", button.transform, style, 40f, TextAlignmentOptions.MidlineLeft, style.Text, style.Font);
        // "Editor Save" shrinks to fit before the info box.
        title.enableAutoSizing = true;
        title.fontSizeMin = 26f;
        title.fontSizeMax = 40f;
        Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(26f, 0f), new Vector2(190f, 86f));
        Image info = Box("Info", button.transform, style, style.InfoBox, false, stretch: false);
        Pin(info.rectTransform, new Vector2(0f, 1f), new Vector2(226f, -10f), new Vector2(420f, 66f));
        TMP_Text progress = Text("Progress", info.transform, style, 26f, TextAlignmentOptions.MidlineLeft, style.Text, style.BodyFont);
        Pin(progress.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -2f), new Vector2(396f, 32f));
        TMP_Text date = Text("Date", info.transform, style, 22f, TextAlignmentOptions.MidlineLeft, style.InfoText, style.BodyFont);
        Pin(date.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -34f), new Vector2(396f, 28f));
        Button delete = Btn("Delete", "DELETE", node, style, 0f, style.Danger);
        Pin((RectTransform)delete.transform, new Vector2(0f, 1f), new Vector2(676f, -15f), new Vector2(124f, 56f));
        delete.GetComponentInChildren<TMP_Text>().fontSize = 26f;

        var row = node.gameObject.AddComponent<SaveSlotRow>();
        var so = new SerializedObject(row);
        Set(so, "_button", button);
        Set(so, "_title", title);
        Set(so, "_progress", progress);
        Set(so, "_date", date);
        Set(so, "_delete", delete);
        so.ApplyModifiedPropertiesWithoutUndo();
        node.gameObject.SetActive(false);
        return row;
    }

    // A yes / no box over a dimmed screen. Inactive.
    static ConfirmDialog Confirm(Transform parent, MenuStyle style)
    {
        RectTransform node = Ui("Confirm", parent);
        Stretch(node);
        node.SetAsLastSibling();
        Box("Dim", node, style, new Color(0f, 0f, 0f, 0.6f), true, stretch: true);
        Image panel = Box("Panel", node, style, style.Panel, true, stretch: false);
        Pin(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 320f), new Vector2(0.5f, 0.5f));
        TMP_Text message = Text("Message", panel.transform, style, 34f, TextAlignmentOptions.Center, style.Text, style.BodyFont);
        message.textWrappingMode = TextWrappingModes.Normal;
        Pin(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -34f), new Vector2(680f, 160f), new Vector2(0.5f, 1f));
        Button yes = Btn("Yes", "YES", panel.transform, style, 0f, style.Danger);
        Pin((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 38f), new Vector2(260f, 66f), new Vector2(0.5f, 0f));
        Button no = Btn("No", "CANCEL", panel.transform, style, 0f);
        Pin((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(150f, 38f), new Vector2(260f, 66f), new Vector2(0.5f, 0f));

        var dialog = node.gameObject.AddComponent<ConfirmDialog>();
        var so = new SerializedObject(dialog);
        Set(so, "_message", message);
        Set(so, "_yes", yes);
        Set(so, "_yesLabel", yes.GetComponentInChildren<TMP_Text>());
        Set(so, "_no", no);
        so.ApplyModifiedPropertiesWithoutUndo();
        node.gameObject.SetActive(false);
        return dialog;
    }

    // Dark on the left under the menu's column, clear by the middle of the screen.
    static void Shade(Transform parent, MenuStyle style)
    {
        Image shade = Picture("Shade", parent, style.ShadeSprite, new Color(0f, 0f, 0f, 0.92f));
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(0.62f, 1f);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;
    }

    static MenuSounds Sounds(Transform node, MenuStyle style)
    {
        var sounds = node.gameObject.AddComponent<MenuSounds>();
        SetRef(sounds, "_style", style);
        return sounds;
    }

    // The menu's EventSystem, keeping the focus for a gamepad.
    static GameObject Events(Transform parent)
    {
        var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(MenuSelectionKeeper));
        events.transform.SetParent(parent, false);
        return events;
    }

    // The column of text buttons from its top-left corner.
    static RectTransform TextColumn(Transform parent, Vector2 position)
    {
        RectTransform column = Ui("Buttons", parent);
        Pin(column, new Vector2(0f, 1f), position, new Vector2(470f, 640f));
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childControlWidth = layout.childControlHeight = layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return column;
    }

    // Switches the language on each press; LanguageButton writes its label ("LANGUAGE: ENGLISH") at runtime.
    static void LanguageButton(Transform column, MenuStyle style)
    {
        Button button = TextButton("Language", "LANGUAGE: ENGLISH", column, style);
        var language = button.gameObject.AddComponent<LanguageButton>();
        var so = new SerializedObject(language);
        so.FindProperty("_label").objectReferenceValue = button.GetComponentInChildren<TMP_Text>(true);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // A text button: the label over a strip of tape that only shows when hovered, selected or pressed.
    static Button TextButton(string name, string label, Transform parent, MenuStyle style)
    {
        RectTransform rect = Ui(name, parent);
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.ButtonSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = style.CornerScale;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = style.TextColors();
        rect.gameObject.AddComponent<MenuButton>();
        TMP_Text text = Text("Label", rect, style, 50f, TextAlignmentOptions.MidlineLeft, style.Text, style.Font);
        Stretch(text.rectTransform);
        text.margin = new Vector4(30f, 0f, 0f, 0f);
        text.text = label;
        return button;
    }

    // A filled button (tape strip) with a centred label; `height` > 0 sizes it in a layout group.
    static Button Btn(string name, string label, Transform parent, MenuStyle style, float height, Color? normal = null)
    {
        RectTransform rect = Ui(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.ButtonSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = style.CornerScale;
        image.color = Color.white;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = style.Colors(normal);
        rect.gameObject.AddComponent<MenuButton>();
        if (height > 0f)
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = Text("Label", rect, style, 32f, TextAlignmentOptions.Center, style.Text, style.Font);
        Stretch(text.rectTransform);
        text.text = label;
        return button;
    }

    static Image Box(string name, Transform parent, MenuStyle style, Color color, bool raycast, bool stretch)
    {
        RectTransform rect = Ui(name, parent);
        if (stretch)
            Stretch(rect);
        var image = rect.gameObject.AddComponent<Image>();
        if (!stretch)
        {
            image.sprite = style.ButtonSprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = style.CornerScale;
        }
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    static TMP_Text Text(string name, Transform parent, MenuStyle style, float size, TextAlignmentOptions alignment, Color color,
        TMP_FontAsset font = null)
    {
        RectTransform rect = Ui(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font != null ? font : style.Font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = string.Empty;
        text.textStyle = TMP_Style.NormalStyle;
        return text;
    }

    static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Anchored at one point; the pivot defaults to that point (top-left anchor → top-left pivot).
    static void Pin(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static Transform FindByPath(Transform root, string path) => string.IsNullOrEmpty(path) ? root : root.Find(path);

    static void Remove(Transform parent, string name)
    {
        Transform old = parent.Find(name);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
    }

    static MenuStyle Style() => AssetDatabase.LoadAssetAtPath<MenuStyle>(StylePath) ?? throw new Exception("run SaveSetup.Assets first: no " + StylePath);

    static T LoadOrCreate<T>(string path, List<string> log) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        string folder = System.IO.Path.GetDirectoryName(path)!.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder(System.IO.Path.GetDirectoryName(folder)!.Replace('\\', '/'), System.IO.Path.GetFileName(folder));
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        log.Add("created " + path);
        return asset;
    }

    static void SetRef(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        Set(so, field, value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty property = so.FindProperty(field) ?? throw new Exception($"{so.targetObject.GetType().Name} has no {field}");
        property.objectReferenceValue = value;
    }
}
