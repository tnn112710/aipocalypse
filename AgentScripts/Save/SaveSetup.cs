using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.DefenseSystem;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Core.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the save system's content: Data/Save/SaveSettings, Data/Equipment/ItemCatalog (collected), Data/UI/MenuStyle, the
// root scope's references, the PauseMenu and SaveToast in HUD.prefab, WorldSaveInstaller + SaveIds in WorldMap, the MainMenu
// scene, and the Build Settings scene list (MainMenu first). Safe to re-run: UI nodes are rebuilt, assets and ids kept.
public static class SaveSetup
{
    const string SettingsPath = "Assets/_Game/Data/Save/SaveSettings.asset";
    const string CatalogPath = "Assets/_Game/Data/Equipment/ItemCatalog.asset";
    const string StylePath = "Assets/_Game/Data/UI/MenuStyle.asset";
    const string FontPath = "Assets/_Game/Data/UI/DeathScreen/DeathScreenFont.asset";
    const string RootPath = "Assets/_Game/_Core/_Bootstrap/ProjectLifetimeScope.prefab";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string MainMenuPath = "Assets/_Game/Scenes/MainMenu.unity";
    const string Title = "Hasta la Vista, Maybe";
    // Over the HUD (10), the inventory (50) and the death screen (100).
    const int PauseSorting = 200;

    public static object All()
    {
        var log = new List<string> { Assets(), Hud(), World(), MainMenu(), BuildScenes() };
        return string.Join("\n", log);
    }

    public static string Assets()
    {
        var log = new List<string>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new Exception("missing " + FontPath);

        var settings = LoadOrCreate<SaveSettings>(SettingsPath, log);
        if (settings.LoadingFont == null)
        {
            settings.LoadingFont = font;
            EditorUtility.SetDirty(settings);
        }
        AssetDatabase.SaveAssetIfDirty(settings);

        var catalog = LoadOrCreate<ItemCatalog>(CatalogPath, log);
        log.Add($"ItemCatalog: {catalog.Collect()} items");
        AssetDatabase.SaveAssetIfDirty(catalog);

        var style = LoadOrCreate<MenuStyle>(StylePath, log);
        style.Font = font;
        style.ButtonSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
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
            TMP_Text toastText = Text("Text", toastNode, style, 26f, TextAlignmentOptions.BottomRight, style.Text);
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

            RectTransform page = Ui("Page", node);
            Stretch(page);
            TMP_Text title = Text("Title", page, style, 72f, TextAlignmentOptions.MidlineLeft, style.Text);
            title.text = "Paused";
            Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -140f), new Vector2(900f, 96f));
            RectTransform column = Column("Buttons", page, new Vector2(160f, -260f), 440f);
            Button resume = Btn("Resume", "Resume", column, style, 64f);
            Button save = Btn("Save", "Save Game", column, style, 64f);
            Button load = Btn("Load", "Load Game", column, style, 64f);
            Button settings = Btn("Settings", "Settings (soon)", column, style, 64f);
            Button mainMenu = Btn("MainMenu", "Main Menu", column, style, 64f);
            Button quit = Btn("Quit", "Quit Game", column, style, 64f);
            TMP_Text hint = Text("Hint", page, style, 24f, TextAlignmentOptions.TopLeft, style.InfoText);
            Pin(hint.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -740f), new Vector2(1200f, 40f));

            SaveSlotsPanel slots = SlotsPanel(node, style);
            ConfirmDialog confirm = Confirm(node, style);
            SetRef(slots, "_confirm", confirm);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(node, false);
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
            Set(so, "_quit", quit);
            Set(so, "_hint", hint);
            Set(so, "_slots", slots);
            Set(so, "_confirm", confirm);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return "HUD.prefab: SaveToast (bottom right) + PauseMenu (own canvas, sorting 200, page / slots / confirm, EventSystem off)";
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

    public static string MainMenu()
    {
        MenuStyle style = Style();
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            var cameraObject = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
            var camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 1f);

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

            Box("Background", node, style, new Color(0.07f, 0.11f, 0.17f, 1f), false, stretch: true);
            RectTransform page = Ui("Page", node);
            Stretch(page);
            TMP_Text title = Text("Title", page, style, 84f, TextAlignmentOptions.MidlineLeft, style.Text);
            title.text = Title;
            Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -130f), new Vector2(1500f, 110f));
            RectTransform column = Column("Buttons", page, new Vector2(160f, -290f), 440f);
            Button continueButton = Btn("Continue", "Continue", column, style, 64f);
            TMP_Text continueInfo = Text("ContinueInfo", column, style, 22f, TextAlignmentOptions.TopLeft, style.InfoText);
            continueInfo.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            Button newGame = Btn("NewGame", "New Game", column, style, 64f);
            Button load = Btn("Load", "Load Game", column, style, 64f);
            Button settings = Btn("Settings", "Settings (soon)", column, style, 64f);
            Button quit = Btn("Quit", "Quit", column, style, 64f);
            TMP_Text status = Text("Status", page, style, 24f, TextAlignmentOptions.TopLeft, style.InfoText);
            Pin(status.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -760f), new Vector2(1200f, 40f));

            SaveSlotsPanel slots = SlotsPanel(node, style);
            ConfirmDialog confirm = Confirm(node, style);
            SetRef(slots, "_confirm", confirm);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(node, false);

            var screen = canvasObject.AddComponent<MainMenuScreen>();
            var so = new SerializedObject(screen);
            Set(so, "_eventSystem", events);
            Set(so, "_page", page.gameObject);
            Set(so, "_continue", continueButton);
            Set(so, "_continueInfo", continueInfo);
            Set(so, "_newGame", newGame);
            Set(so, "_load", load);
            Set(so, "_settings", settings);
            Set(so, "_quit", quit);
            Set(so, "_status", status);
            Set(so, "_slots", slots);
            Set(so, "_confirm", confirm);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, MainMenuPath);
            return "MainMenu.unity: camera, canvas (HudScope, MainMenuScreen, slots, confirm, EventSystem)";
        }
        finally
        {
            if (previous.IsValid())
                SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

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
        Button back = Btn("Back", "<", node, style, 0f);
        Pin((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(60f, -50f), new Vector2(84f, 84f));
        back.GetComponent<Image>().pixelsPerUnitMultiplier = 0.12f;
        back.GetComponentInChildren<TMP_Text>().fontSize = 44f;
        TMP_Text title = Text("Title", node, style, 56f, TextAlignmentOptions.MidlineLeft, style.Text);
        title.text = "Select Save Slot";
        Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(170f, -50f), new Vector2(560f, 84f));
        Button copy = Btn("CopyPath", "Copy Save File Path", node, style, 0f);
        Pin((RectTransform)copy.transform, new Vector2(0f, 1f), new Vector2(740f, -68f), new Vector2(290f, 48f));
        copy.GetComponentInChildren<TMP_Text>().fontSize = 20f;

        RectTransform rows = Ui("Rows", node);
        Pin(rows, new Vector2(0f, 1f), new Vector2(160f, -170f), new Vector2(820f, 600f));
        var layout = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        SaveSlotRow row = Row(rows, style);

        TMP_Text status = Text("Status", node, style, 24f, TextAlignmentOptions.TopLeft, style.InfoText);
        Pin(status.rectTransform, new Vector2(0f, 1f), new Vector2(160f, -700f), new Vector2(1400f, 40f));

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
        TMP_Text title = Text("Title", button.transform, style, 34f, TextAlignmentOptions.MidlineLeft, style.Text);
        title.fontStyle = FontStyles.Bold;
        Pin(title.rectTransform, new Vector2(0f, 1f), new Vector2(26f, 0f), new Vector2(200f, 86f));
        Image info = Box("Info", button.transform, style, style.InfoBox, false, stretch: false);
        Pin(info.rectTransform, new Vector2(0f, 1f), new Vector2(226f, -10f), new Vector2(420f, 66f));
        TMP_Text progress = Text("Progress", info.transform, style, 22f, TextAlignmentOptions.MidlineLeft, style.Text);
        Pin(progress.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -4f), new Vector2(396f, 30f));
        TMP_Text date = Text("Date", info.transform, style, 20f, TextAlignmentOptions.MidlineLeft, style.InfoText);
        Pin(date.rectTransform, new Vector2(0f, 1f), new Vector2(16f, -34f), new Vector2(396f, 28f));
        Button delete = Btn("Delete", "Delete", node, style, 0f, style.Danger);
        Pin((RectTransform)delete.transform, new Vector2(0f, 1f), new Vector2(676f, -15f), new Vector2(120f, 56f));
        delete.GetComponentInChildren<TMP_Text>().fontSize = 22f;

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
        Box("Dim", node, style, new Color(0f, 0f, 0f, 0.55f), true, stretch: true);
        Image panel = Box("Panel", node, style, new Color(0.15f, 0.18f, 0.23f, 0.98f), true, stretch: false);
        Pin(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 300f), new Vector2(0.5f, 0.5f));
        TMP_Text message = Text("Message", panel.transform, style, 30f, TextAlignmentOptions.Center, style.Text);
        message.textWrappingMode = TextWrappingModes.Normal;
        Pin(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(640f, 150f), new Vector2(0.5f, 1f));
        Button yes = Btn("Yes", "Yes", panel.transform, style, 0f, style.Danger);
        Pin((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 36f), new Vector2(260f, 64f), new Vector2(0.5f, 0f));
        Button no = Btn("No", "Cancel", panel.transform, style, 0f);
        Pin((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(150f, 36f), new Vector2(260f, 64f), new Vector2(0.5f, 0f));

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

    // A vertical list of buttons from its top-left corner.
    static RectTransform Column(string name, Transform parent, Vector2 position, float width)
    {
        RectTransform column = Ui(name, parent);
        Pin(column, new Vector2(0f, 1f), position, new Vector2(width, 520f));
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return column;
    }

    // A rounded button with a centred label; `height` > 0 sizes it in a layout group.
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
        if (height > 0f)
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = Text("Label", rect, style, 30f, TextAlignmentOptions.Center, style.Text);
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

    static TMP_Text Text(string name, Transform parent, MenuStyle style, float size, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = Ui(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = style.Font;
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
