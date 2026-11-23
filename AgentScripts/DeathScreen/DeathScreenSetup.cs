using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Editor.Localization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the death screen: the eyelid texture and font in Data/UI/DeathScreen, Data/UI/DeathScreenStyle.asset, and the DeathScreen
// canvas inside HUD.prefab (eyes + the "Walk it off!" menu, DeathScreenView + DeathScreenHud). The menu's art layers come from
// Assets/_Placeholder/UI/DeathScreen (AI-generated, see SOURCES.txt there). Safe to re-run; the HUD part is rebuilt.
//   unity command script run --file AgentScripts/DeathScreen/DeathScreenSetup.cs --entry DeathScreenSetup.All
public static class DeathScreenSetup
{
    const string Dir = "Assets/_Game/Data/UI/DeathScreen";
    const string Art = "Assets/_Placeholder/UI/DeathScreen/";
    const string FontSourcePath = Dir + "/VarelaRound-Regular.ttf";
    const string FontPath = Dir + "/DeathScreenFont.asset";
    const string TitleMaterialPath = Dir + "/DeathScreenFont_Title.mat";
    const string EyelidPath = Dir + "/Eyelid.png";
    const string StylePath = "Assets/_Game/Data/UI/DeathScreenStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    // Over the HUD (10) and its nested canvases.
    const int SortingOrder = 100;

    // Reference layout, in canvas units (1920 x 1080): scene layers sit on the ground line and stay put on any aspect, the
    // text hangs from the top and the progress bar from the bottom.
    const float GroundHeight = 162f;
    static readonly Vector2 BarSize = new(869f, 72f);
    const float BarInsetX = 12f, BarInsetY = 10f;

    public static object All()
    {
        var log = new List<string>();
        Texture2D eyelid = Texture(EyelidPath);
        TMP_FontAsset font = Font(log);
        Material title = TitleMaterial(font);

        var style = AssetDatabase.LoadAssetAtPath<DeathScreenStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<DeathScreenStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
            log.Add("created " + StylePath);
        }
        style.EyelidTexture = eyelid;
        style.TitleFont = font;
        style.TitleMaterial = title;
        style.StatusFont = font;
        // The text look the asset predates: the style's own defaults, read from a throwaway instance.
        var fresh = ScriptableObject.CreateInstance<DeathScreenStyle>();
        style.TitleSize = fresh.TitleSize;
        style.TitleColor = fresh.TitleColor;
        style.StatusSize = fresh.StatusSize;
        style.StatusColor = fresh.StatusColor;
        Object.DestroyImmediate(fresh);
        style.Subtitle = LocalizationKit.Ref("HUD", "death.subtitle");
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);

        log.Add(Hud(style));
        return string.Join("\n", log);
    }

    static Texture2D Texture(string path)
    {
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path) ?? throw new Exception("missing " + path);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // A UI sprite; `border` (left, bottom, right, top) makes it 9-sliceable.
    static Sprite Sprite(string file, Vector4 border = default)
    {
        string path = Art + file;
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path) ?? throw new Exception("missing " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spriteBorder = border;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static TMP_FontAsset Font(List<string> log)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (existing != null)
            return existing;

        AssetDatabase.ImportAsset(FontSourcePath);
        var source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath) ?? throw new Exception("missing " + FontSourcePath);
        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        font.name = "DeathScreenFont";
        AssetDatabase.CreateAsset(font, FontPath);
        font.atlasTextures[0].name = "DeathScreenFont Atlas";
        AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        font.material.name = "DeathScreenFont Material";
        AssetDatabase.AddObjectToAsset(font.material, font);
        font.TryAddCharacters(" !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~’…");
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssetIfDirty(font);
        log.Add("created " + FontPath);
        return font;
    }

    // The font's material with a soft, slightly offset underlay: the title's drop shadow (a warm dark orange).
    static Material TitleMaterial(TMP_FontAsset font)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TitleMaterialPath);
        if (material == null)
        {
            material = new Material(font.material) { name = "DeathScreenFont_Title" };
            AssetDatabase.CreateAsset(material, TitleMaterialPath);
        }
        material.shader = font.material.shader;
        material.SetTexture("_MainTex", font.atlasTexture);
        material.EnableKeyword("UNDERLAY_ON");
        material.SetColor("_UnderlayColor", new Color(0.42f, 0.1f, 0.02f, 0.4f));
        material.SetFloat("_UnderlayOffsetX", 0.35f);
        material.SetFloat("_UnderlayOffsetY", -0.6f);
        material.SetFloat("_UnderlayDilate", 0.15f);
        material.SetFloat("_UnderlaySoftness", 0.55f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    static string Hud(DeathScreenStyle style)
    {
        Sprite sun = Sprite("sun.png");
        Sprite[] cloudSprites = { Sprite("cloud_1.png"), Sprite("cloud_2.png"), Sprite("cloud_3.png") };
        Sprite mountains = Sprite("mountains.png");
        Sprite trees = Sprite("trees.png");
        Sprite dog = Sprite("dog_player.png");
        Sprite frame = Sprite("progress_bar_background.png", new Vector4(36f, 0f, 36f, 0f));
        Sprite fillSprite = Sprite("progress_bar_fill.png", new Vector4(30f, 0f, 30f, 0f));

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform old = root.transform.Find("DeathScreen");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform screen = UiObject("DeathScreen", root.transform);
            Stretch(screen);
            var canvas = screen.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            // The HUD root's CanvasGroup hides the HUD while dying; the death screen stays out of it.
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform eyes = UiObject("Eyes", screen);
            Stretch(eyes);
            Image darkness = Img(UiObject("Darkness", eyes));
            Stretch(darkness.rectTransform);
            RawImage upper = Raw(UiObject("UpperLid", eyes));
            RawImage lower = Raw(UiObject("LowerLid", eyes));

            RectTransform menuRect = UiObject("Menu", screen);
            Stretch(menuRect);
            var menu = menuRect.gameObject.AddComponent<CanvasGroup>();
            menu.interactable = false;
            menu.blocksRaycasts = false;

            RawImage sky = Raw(UiObject("Sky", menuRect));
            Stretch(sky.rectTransform);

            Image sunImage = Pic("Sun", menuRect, sun);
            Place(sunImage.rectTransform, Bottom, new Vector2(-196f, 403f), new Vector2(464f, 370f), middle: true);

            // Clouds: sprite, position from the top-centre, size, mirrored.
            var clouds = new List<Image>();
            foreach ((int sprite, float x, float y, float w, bool flip) in new[]
            {
                (0, -859f, -250f, 300f, false), (1, -719f, -373f, 270f, false), (2, -558f, -516f, 200f, false),
                (1, 877f, -264f, 270f, true), (2, 739f, -448f, 200f, false), (0, 579f, -540f, 300f, true),
            })
            {
                Sprite s = cloudSprites[sprite];
                Image cloud = Pic("Cloud" + (clouds.Count + 1), menuRect, s);
                Place(cloud.rectTransform, Top, new Vector2(x, y), new Vector2(w, w * s.rect.height / s.rect.width), middle: true);
                if (flip)
                    cloud.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                clouds.Add(cloud);
            }

            // Three copies side by side overlap a little; the middle one is mirrored so the edges meet.
            var mountainImages = new List<Image>();
            foreach (float x in new[] { -720f, 0f, 720f })
            {
                Image mountain = Pic("Mountains" + (mountainImages.Count + 1), menuRect, mountains);
                Place(mountain.rectTransform, Bottom, new Vector2(x, 150f), new Vector2(937f, 243f), middle: false);
                if (x == 0f)
                    mountain.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                mountainImages.Add(mountain);
            }

            var treeImages = new List<Image>();
            Image treesBack = Pic("TreesBack", menuRect, trees);
            Place(treesBack.rectTransform, BottomLeft, new Vector2(300f, 150f), new Vector2(200f, 173f), middle: false);
            Image treesFront = Pic("Trees", menuRect, trees);
            Place(treesFront.rectTransform, BottomLeft, new Vector2(-10f, 150f), new Vector2(367f, 318f), middle: false);
            treeImages.Add(treesBack);
            treeImages.Add(treesFront);

            Image ground = Img(UiObject("Ground", menuRect));
            ground.rectTransform.anchorMin = Vector2.zero;
            ground.rectTransform.anchorMax = new Vector2(1f, 0f);
            ground.rectTransform.pivot = new Vector2(0.5f, 0f);
            ground.rectTransform.anchoredPosition = Vector2.zero;
            ground.rectTransform.sizeDelta = new Vector2(0f, GroundHeight);

            Image dogImage = Pic("Dog", menuRect, dog);
            Place(dogImage.rectTransform, Bottom, new Vector2(38f, 158f), new Vector2(960f, 227f), middle: false);

            TextMeshProUGUI title = Text("Title", menuRect, style.Title.Text());
            title.fontStyle = FontStyles.Bold;
            Place(title.rectTransform, Top, new Vector2(0f, -253f), new Vector2(1700f, 260f), middle: true);
            TextMeshProUGUI subtitle = Text("Subtitle", menuRect, style.Subtitle.Text());
            Place(subtitle.rectTransform, Top, new Vector2(0f, -407f), new Vector2(1500f, 90f), middle: true);
            TextMeshProUGUI status = Text("Status", menuRect, style.LoadingText.Text());
            status.fontStyle = FontStyles.Bold;
            Place(status.rectTransform, Bottom, new Vector2(0f, 132f), new Vector2(1200f, 60f), middle: true);

            // Frame > FillArea > Track + Clip (RectMask2D, its right anchor is the progress) > Fill (full width, so the stripes don't stretch).
            Image barFrame = Pic("ProgressBar", menuRect, frame);
            barFrame.type = Image.Type.Sliced;
            Place(barFrame.rectTransform, Bottom, new Vector2(0f, 70f), BarSize, middle: true);
            RectTransform area = UiObject("FillArea", barFrame.rectTransform);
            area.anchorMin = Vector2.zero;
            area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(BarInsetX, BarInsetY);
            area.offsetMax = new Vector2(-BarInsetX, -BarInsetY);
            Image track = Pic("Track", area, fillSprite);
            track.type = Image.Type.Sliced;
            Stretch(track.rectTransform);
            RectTransform clip = UiObject("Clip", area);
            clip.gameObject.AddComponent<RectMask2D>();
            clip.anchorMin = Vector2.zero;
            clip.anchorMax = new Vector2(0f, 1f);
            clip.offsetMin = clip.offsetMax = Vector2.zero;
            Image fill = Pic("Fill", clip, fillSprite);
            fill.type = Image.Type.Sliced;
            fill.rectTransform.anchorMin = fill.rectTransform.anchorMax = fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            fill.rectTransform.anchoredPosition = Vector2.zero;
            fill.rectTransform.sizeDelta = new Vector2(BarSize.x - 2f * BarInsetX, BarSize.y - 2f * BarInsetY);

            var view = screen.gameObject.AddComponent<DeathScreenView>();
            var so = new SerializedObject(view);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_eyes").objectReferenceValue = eyes;
            so.FindProperty("_darkness").objectReferenceValue = darkness;
            so.FindProperty("_upperLid").objectReferenceValue = upper;
            so.FindProperty("_lowerLid").objectReferenceValue = lower;
            so.FindProperty("_menu").objectReferenceValue = menu;
            so.FindProperty("_sky").objectReferenceValue = sky;
            so.FindProperty("_sun").objectReferenceValue = sunImage;
            SetArray(so.FindProperty("_clouds"), clouds);
            SetArray(so.FindProperty("_mountains"), mountainImages);
            SetArray(so.FindProperty("_trees"), treeImages);
            so.FindProperty("_ground").objectReferenceValue = ground;
            so.FindProperty("_dog").objectReferenceValue = dogImage;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_subtitle").objectReferenceValue = subtitle;
            so.FindProperty("_status").objectReferenceValue = status;
            so.FindProperty("_barFrame").objectReferenceValue = barFrame;
            so.FindProperty("_barTrack").objectReferenceValue = track;
            so.FindProperty("_barClip").objectReferenceValue = clip;
            so.FindProperty("_barFill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyStyle();
            title.ForceMeshUpdate();
            subtitle.ForceMeshUpdate();
            status.ForceMeshUpdate();
            // Hidden in the prefab; the view also hides everything on Awake.
            eyes.gameObject.SetActive(false);
            menu.alpha = 0f;
            menuRect.gameObject.SetActive(false);

            var hud = screen.gameObject.AddComponent<DeathScreenHud>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("_view").objectReferenceValue = view;
            hso.ApplyModifiedPropertiesWithoutUndo();

            HudVisibility(root);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return "HUD.prefab: DeathScreen (sort order " + SortingOrder + ")";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // HUD root: a CanvasGroup + HudVisibilityHud, so the HUD fades out while the player dies. Without rebuilding the death screen.
    public static object HideHudOnDeath()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform screen = root.transform.Find("DeathScreen");
            if (!screen.TryGetComponent(out CanvasGroup group))
                group = screen.gameObject.AddComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            group.interactable = false;
            group.blocksRaycasts = false;
            HudVisibility(root);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return "HUD.prefab: HudVisibilityHud on the root, DeathScreen ignores it";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void HudVisibility(GameObject root)
    {
        if (!root.TryGetComponent(out CanvasGroup group))
            group = root.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        if (!root.TryGetComponent(out HudVisibilityHud visibility))
            visibility = root.AddComponent<HudVisibilityHud>();
        var so = new SerializedObject(visibility);
        so.FindProperty("_group").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static readonly Vector2 Top = new(0.5f, 1f), Bottom = new(0.5f, 0f), BottomLeft = new(0f, 0f);

    // Anchored to `anchor`, pivot in the middle (`middle`) or on the anchor's own edge (the bottom for a bottom anchor).
    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, bool middle)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = middle ? new Vector2(0.5f, 0.5f) : new Vector2(anchor.x, 0f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void SetArray(SerializedProperty array, List<Image> images)
    {
        array.arraySize = images.Count;
        for (int i = 0; i < images.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = images[i];
    }

    static RectTransform UiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Image Img(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static Image Pic(string name, Transform parent, Sprite sprite)
    {
        Image image = Img(UiObject(name, parent));
        image.sprite = sprite;
        return image;
    }

    static RawImage Raw(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI Text(string name, Transform parent, string text)
    {
        var tmp = UiObject(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;
        return tmp;
    }
}
