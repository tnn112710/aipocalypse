using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Skins the game UI with Layer Lab GUI Pro - Survival Clean (ASSETS.md): writes its sprites and navy / cyan palette into the
// UI style assets, then re-skins the images the setup scripts baked into the UI prefabs and the main menu scene (panels,
// slots, buttons, frames, key caps, clock plate). Fonts stay Saira (Layer Lab's Oxanium has no Vietnamese); icons, the
// minimap pieces and the QTE rings stay Synty. Safe to re-run: roles are found from the Synty and the Layer Lab sprites.
//   unity command script run --file AgentScripts/UI/LayerLabSetup.cs --entry LayerLabSetup.All
public static class LayerLabSetup
{
    const string Pack = "Assets/_StoreAssets/UI/Layer Lab/GUI Pro-SurvivalClean/ResourcesData/Sprites/Components/";
    const string ChamferPath = Pack + "Frame_Custom/Frame_ItemFrame04_n_White3.png";
    const string FramePath = Pack + "Frame_Custom/Frame_ItemFrame04_s_White2.png";
    const string KeyPath = Pack + "Sliders_Custom/Slider05_White7_1.png";
    const string BarShapePath = Pack + "Sliders_Custom/Slider05_White2_Frame2.png";
    const string TagPath = Pack + "Label/Label_Tag01_White1.png";
    const string WhitePath = Pack + "Frame_Custom/Common_4pixel_white.png";

    const string DataUi = "Assets/_Game/Data/UI/";
    const string Prefabs = "Assets/_Game/_Core/UI/Prefabs/";
    const string MainMenuPath = "Assets/_Game/Scenes/MainMenu.unity";

    // Palette
    static readonly Color Navy = Hex("0C1828");
    static readonly Color Slot = Hex("16304D");
    static readonly Color Accent = Hex("3FD6FF");
    static readonly Color Hover = Hex("1D8FC4F0");
    static readonly Color Pressed = Hex("8CE6FF");
    static readonly Color KeyCap = Hex("D6E9FFF2");
    static readonly Color Panel = Hex("0C1828F0");
    static readonly Color Shade = Hex("06101C");

    // Corner size = sprite border / multiplier.
    const float PanelCorners = 2.5f, SlotCorners = 3f, ButtonCorners = 3f, FrameCorners = 2.2f, TagCorners = 1.5f;

    static Sprite _chamfer, _frame, _key, _barShape, _tag, _white;

    public static string All() => string.Join("\n", Styles(), PrefabAssets(), MainMenu());

    static void Load()
    {
        _chamfer = Sprite(ChamferPath);
        _frame = Sprite(FramePath);
        _key = Sprite(KeyPath);
        _barShape = Sprite(BarShapePath);
        _tag = Sprite(TagPath);
        _white = Sprite(WhitePath);
    }

    // ---------------------------------------------------------------- style assets

    public static string Styles()
    {
        Load();
        var log = new List<string>();

        foreach ((string name, Color fill) in new[] { ("HealthBarStyle", Hex("2FE08A")), ("StaminaBarStyle", Hex("3FA9FF")) })
        {
            var bar = Asset<StatBarStyle>(name);
            bar.BackgroundSprite = _white;
            bar.BackgroundColor = WithAlpha(Navy, 0.85f);
            bar.ShapeSprite = _barShape;
            bar.FillSprite = _white;
            bar.FillColor = fill;
            bar.IconFrameSprite = _key;
            bar.IconFrameColor = WithAlpha(Navy, 0.85f);
            Save(bar, log);
        }

        var hotbar = Asset<HotbarSlotStyle>("HotbarSlotStyle");
        hotbar.BackgroundSprite = _chamfer;
        hotbar.BackgroundColor = WithAlpha(Slot, 0.65f);
        hotbar.EmptyBackgroundColor = WithAlpha(Slot, 0.4f);
        hotbar.FrameSprite = _frame;
        hotbar.FrameColor = Accent;
        hotbar.KeySprite = _key;
        hotbar.KeyColor = KeyCap;
        hotbar.SelectedKeyColor = Accent;
        hotbar.KeyLabelColor = Navy;
        Save(hotbar, log);

        var prompt = Asset<InteractPromptStyle>("InteractPromptStyle");
        prompt.KeySprite = _key;
        prompt.KeyColor = KeyCap;
        prompt.KeyLabelColor = Navy;
        Save(prompt, log);

        var qte = Asset<QteStyle>("RepairQteStyle");
        qte.KeyCap = _key;
        qte.KeyLabelColor = Navy;
        Save(qte, log);

        var inventory = Asset<InventoryStyle>("InventoryStyle");
        inventory.SlotSprite = _chamfer;
        inventory.SlotColor = WithAlpha(Hex("1A3555"), 0.92f);
        inventory.EmptySlotColor = WithAlpha(Hex("12243C"), 0.75f);
        inventory.HighlightSprite = _frame;
        inventory.HighlightColor = Accent;
        inventory.PanelSprite = _chamfer;
        inventory.PanelColor = WithAlpha(Navy, 0.9f);
        Save(inventory, log);

        var clock = Asset<ClockStyle>("ClockStyle");
        clock.BackingSprite = _chamfer;
        clock.BackingColor = WithAlpha(Navy, 0.82f);
        Save(clock, log);

        var minimap = Asset<MinimapStyle>("MinimapStyle");
        minimap.BackgroundColor = WithAlpha(Navy, 0.9f);
        minimap.RingColor = WithAlpha(Accent, 0.8f);
        Save(minimap, log);

        var menu = Asset<MenuStyle>("MenuStyle");
        _oldMenu = Object.Instantiate(menu);
        menu.ButtonSprite = _chamfer;
        menu.CornerScale = ButtonCorners;
        // Menu colours (Button, Highlight, Panel) are now owned by MenuStyle / MenuTheme.cs: not reset here.
        Save(menu, log);
        return string.Join("\n", log);
    }

    // MenuStyle as it was before Styles(): button colour blocks baked from it are mapped to the new values.
    static MenuStyle _oldMenu;

    // ---------------------------------------------------------------- prefabs and the main menu

    public static string PrefabAssets()
    {
        Load();
        var menu = Asset<MenuStyle>("MenuStyle");
        var log = new List<string>();
        // Nested prefabs first, so HUD.prefab sees their new values and records no overrides.
        foreach (string path in new[] { "Widgets/StatBar", "Widgets/HotbarSlot", "Widgets/ItemSlot", "Widgets/InteractPrompt", "HUD" })
        {
            string file = Prefabs + path + ".prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(file);
            try
            {
                int changed = Skin(root, menu);
                if (path.EndsWith("StatBar"))
                    changed += ShapeBar(root);
                if (changed > 0)
                    PrefabUtility.SaveAsPrefabAsset(root, file);
                log.Add($"{path}: {changed} changes");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return string.Join("\n", log);
    }

    public static string MainMenu()
    {
        Load();
        var menu = Asset<MenuStyle>("MenuStyle");
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty)
            return "MainMenu: skipped, the open scene has unsaved changes";
        Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        int changed = scene.GetRootGameObjects().Sum(root => Skin(root, menu));
        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous) && previous != MainMenuPath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        return $"MainMenu: {changed} changes";
    }

    static int Skin(GameObject root, MenuStyle menu)
    {
        int changed = 0;
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
            changed += SkinImage(image);
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
            changed += SkinButton(button, menu);
        return changed;
    }

    static int SkinImage(Image image)
    {
        string sprite = image.sprite != null ? image.sprite.name : "";
        string name = image.name;
        int changed = 0;
        if (Is(sprite, "Box_Small_02", "Box_Large_01", _chamfer, _tag))
        {
            if (image.GetComponent<Button>() != null)
                changed += SetSprite(image, _chamfer, ButtonCorners);
            else if (name == "Background")
                changed += SetSprite(image, _chamfer, SlotCorners);
            else if (name.EndsWith("Tag"))
                changed += SetSprite(image, _tag, TagCorners);
            else
            {
                changed += SetSprite(image, _chamfer, PanelCorners);
                changed += SetColor(image, WithAlpha(Panel, Mathf.Max(image.color.a, Panel.a)));
            }
        }
        else if (Is(sprite, "Frame_Large_04", _frame))
            changed += SetSprite(image, _frame, FrameCorners);
        else if (name == "IconFrame" && Is(sprite, "Square_White_01", _white, _key))
            changed += SetSprite(image, _key, 1f);
        else if (Is(sprite, "Input_PC_Button", _key))
            changed += SetSprite(image, _key, 1f);
        else if (Is(sprite, "Square_White_01", _white))
            changed += SetSprite(image, _white, 1f);
        else if (sprite == "UISprite" && name == "Backing" && image.GetComponentInParent<ClockView>(true) != null)
            changed += SetSprite(image, _chamfer, SlotCorners);
        else if (sprite.Contains("Gradient_Horizontal"))
            changed += SetColor(image, WithAlpha(Shade, image.color.a));
        return changed;
    }

    static bool Is(string sprite, string synty, params Sprite[] ours) => sprite.Contains(synty) || ours.Any(s => s != null && s.name == sprite);

    static bool Is(string sprite, string synty, string synty2, params Sprite[] ours) => sprite.Contains(synty2) || Is(sprite, synty, ours);

    // Sliced with corners scaled down until they fit the rect; a sprite without borders is drawn whole (Filled stays Filled).
    static int SetSprite(Image image, Sprite sprite, float corners)
    {
        int changed = 0;
        if (image.sprite != sprite) { image.sprite = sprite; changed++; }
        Vector4 b = sprite.border;
        if (image.type == Image.Type.Filled)
            return changed;
        Image.Type type = b == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
        if (image.type != type) { image.type = type; changed++; }
        if (type == Image.Type.Sliced)
        {
            Rect r = image.rectTransform.rect;
            float ppu = corners;
            if (r.width > 1f && r.height > 1f)
                ppu = Mathf.Max(ppu, (b.x + b.z) / r.width, (b.y + b.w) / r.height);
            ppu = Mathf.Round(ppu * 100f) / 100f;
            if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, ppu)) { image.pixelsPerUnitMultiplier = ppu; changed++; }
        }
        if (changed > 0)
            EditorUtility.SetDirty(image);
        return changed;
    }

    static int SetColor(Graphic graphic, Color color)
    {
        if (Near(graphic.color, color, 0.004f))
            return 0;
        graphic.color = color;
        EditorUtility.SetDirty(graphic);
        return 1;
    }

    // Colour blocks baked by the setup scripts from MenuStyle: old menu colours become the new ones. Light buttons
    // (inventory Sort, Take all) keep their own normal colour and only get the new hover.
    static int SkinButton(Button button, MenuStyle menu)
    {
        ColorBlock c = button.colors;
        ColorBlock before = c;
        MenuStyle old = _oldMenu != null ? _oldMenu : menu;
        if (Near(c.normalColor, old.Button)) c.normalColor = menu.Button;
        if (Near(c.normalColor, old.Accent)) c.normalColor = menu.Accent;
        c.highlightedColor = c.selectedColor = Near(c.normalColor, menu.Accent) ? Color.Lerp(menu.Accent, Color.white, 0.2f) : menu.ButtonHover;
        c.pressedColor = menu.ButtonPressed;
        if (c == before)
            return 0;
        button.colors = c;
        EditorUtility.SetDirty(button);
        return 1;
    }

    // The bar's own Image + Mask clip background, trail and fill to StatBarStyle.ShapeSprite.
    static int ShapeBar(GameObject root)
    {
        var statBar = root.GetComponent<StatBar>();
        Transform bar = root.transform.Find("Bar");
        if (statBar == null || bar == null)
            return 0;
        int changed = 0;
        var image = bar.GetComponent<Image>();
        if (image == null) { image = bar.gameObject.AddComponent<Image>(); changed++; }
        image.raycastTarget = false;
        changed += SetSprite(image, _barShape, 1f);
        var mask = bar.GetComponent<Mask>();
        if (mask == null) { mask = bar.gameObject.AddComponent<Mask>(); changed++; }
        mask.showMaskGraphic = false;
        var so = new SerializedObject(statBar);
        SerializedProperty shape = so.FindProperty("_shape");
        if (shape.objectReferenceValue != mask)
        {
            shape.objectReferenceValue = mask;
            so.ApplyModifiedPropertiesWithoutUndo();
            changed++;
        }
        return changed;
    }

    // ---------------------------------------------------------------- helpers

    static T Asset<T>(string name) where T : ScriptableObject =>
        AssetDatabase.LoadAssetAtPath<T>(DataUi + name + ".asset") ?? throw new System.Exception("missing " + DataUi + name);

    static Sprite Sprite(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault() ?? throw new System.Exception("missing sprite " + path);

    static void Save(ScriptableObject asset, List<string> log)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
        log.Add(asset.name + " styled");
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

    static bool Near(Color a, Color b, float tolerance = 0.02f) =>
        Mathf.Abs(a.r - b.r) < tolerance && Mathf.Abs(a.g - b.g) < tolerance && Mathf.Abs(a.b - b.b) < tolerance && Mathf.Abs(a.a - b.a) < tolerance;
}
