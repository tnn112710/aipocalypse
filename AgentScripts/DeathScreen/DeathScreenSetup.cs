using System;
using System.Collections.Generic;
using Aipocalypse.Core.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the death screen: textures and font in Data/UI/DeathScreen, Data/UI/DeathScreenStyle.asset, and the DeathScreen
// canvas inside HUD.prefab (eyes + menu, DeathScreenView + DeathScreenHud). Safe to re-run; the HUD part is rebuilt.
public static class DeathScreenSetup
{
    const string Dir = "Assets/_Game/Data/UI/DeathScreen";
    const string FontSourcePath = Dir + "/VarelaRound-Regular.ttf";
    const string FontPath = Dir + "/DeathScreenFont.asset";
    const string TitleMaterialPath = Dir + "/DeathScreenFont_Title.mat";
    const string EyelidPath = Dir + "/Eyelid.png";
    const string GlowPath = Dir + "/Glow.png";
    const string StylePath = "Assets/_Game/Data/UI/DeathScreenStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    // Over the HUD (10) and its nested canvases.
    const int SortingOrder = 100;

    public static object All()
    {
        var log = new List<string>();
        Texture2D eyelid = Texture(EyelidPath);
        Texture2D glow = Texture(GlowPath);
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
        style.GlowTexture = glow;
        style.TitleFont = font;
        style.TitleMaterial = title;
        style.StatusFont = font;
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

    // The font's material with a soft, slightly offset underlay: the title's drop shadow.
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
        material.SetColor("_UnderlayColor", new Color(0.02f, 0.27f, 0.36f, 0.35f));
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
            Image background = Img(UiObject("Background", menuRect));
            Stretch(background.rectTransform);
            RawImage glowImage = Raw(UiObject("Glow", menuRect));
            Center(glowImage.rectTransform, new Vector2(0f, 90f), new Vector2(2300f, 1500f));

            TextMeshProUGUI title = Text("Title", menuRect, style.Title);
            Center(title.rectTransform, new Vector2(0f, 175f), new Vector2(1840f, 320f));

            RectTransform illustration = UiObject("Illustration", menuRect);
            Center(illustration, new Vector2(0f, -195f), new Vector2(640f, 230f));

            Image footer = Img(UiObject("Footer", menuRect));
            RectTransform footerRect = footer.rectTransform;
            footerRect.anchorMin = Vector2.zero;
            footerRect.anchorMax = new Vector2(1f, 0f);
            footerRect.pivot = new Vector2(0.5f, 0f);
            footerRect.anchoredPosition = Vector2.zero;
            footerRect.sizeDelta = new Vector2(0f, 228f);
            Image line = Img(UiObject("Line", footerRect));
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = Vector2.one;
            line.rectTransform.pivot = new Vector2(0.5f, 1f);
            line.rectTransform.anchoredPosition = Vector2.zero;
            line.rectTransform.sizeDelta = new Vector2(0f, 3f);
            TextMeshProUGUI status = Text("Status", footerRect, style.LoadingText);
            status.fontStyle = FontStyles.Bold;
            status.rectTransform.anchorMin = new Vector2(0f, 1f);
            status.rectTransform.anchorMax = Vector2.one;
            status.rectTransform.pivot = new Vector2(0.5f, 1f);
            status.rectTransform.anchoredPosition = new Vector2(0f, -14f);
            status.rectTransform.sizeDelta = new Vector2(0f, 72f);

            var view = screen.gameObject.AddComponent<DeathScreenView>();
            var so = new SerializedObject(view);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_eyes").objectReferenceValue = eyes;
            so.FindProperty("_darkness").objectReferenceValue = darkness;
            so.FindProperty("_upperLid").objectReferenceValue = upper;
            so.FindProperty("_lowerLid").objectReferenceValue = lower;
            so.FindProperty("_menu").objectReferenceValue = menu;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_glow").objectReferenceValue = glowImage;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_illustration").objectReferenceValue = illustration;
            so.FindProperty("_footer").objectReferenceValue = footer;
            so.FindProperty("_footerLine").objectReferenceValue = line;
            so.FindProperty("_status").objectReferenceValue = status;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyStyle();
            title.ForceMeshUpdate();
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

    static void Center(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static Image Img(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
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
