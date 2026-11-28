using System;
using MB13.Aipocalypse.Core.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the carried-weight gauge (docs/technicals/NEEDS.md → Carried weight, UI.md): Data/UI/BagGaugeStyle and the Bag node in
// HUD.prefab (SafeFrame/BottomRight, under the pickup feed: a bag icon that fills from the bottom, a notch at Comfortable,
// "kg / max kg" under it). Safe to re-run: the style is updated in place, the node rebuilt.
// Run: unity command script run --file AgentScripts/Hud/LoadHudSetup.cs --entry LoadHudSetup.Build
// After changing the bag sprite: --entry LoadHudSetup.MeasureFill (the drawn rows the fill and notch map onto).
public static class LoadHudSetup
{
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string StylePath = "Assets/_Game/Data/UI/BagGaugeStyle.asset";
    const string BagIcon = "Assets/Synty/InterfaceApocalypseHUD/Sprites/Icons_Inventory/ICON_Apocalpyse_Inventory_Backpack_01.png";
    const string FontPath = "Assets/_Game/Data/UI/Fonts/SairaBold.asset";
    const string NodeName = "Bag";
    const float IconSize = 56f;
    const float LabelHeight = 26f;
    const float Width = 130f;
    // Bottom edge 40 px up; the pickup feed starts 150 px up, so the bag and its label stay under it.
    const float Y = 40f;

    public static string Build()
    {
        var style = AssetDatabase.LoadAssetAtPath<BagGaugeStyle>(StylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<BagGaugeStyle>();
            AssetDatabase.CreateAsset(style, StylePath);
        }
        style.Icon = AssetDatabase.LoadAssetAtPath<Sprite>(BagIcon) ?? throw new Exception("missing sprite " + BagIcon);
        style.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new Exception("missing font " + FontPath);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        string measured = MeasureFill();

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform corner = root.transform.Find("SafeFrame/BottomRight") ?? throw new Exception("no SafeFrame/BottomRight in HUD.prefab");
            Transform old = corner.Find(NodeName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            RectTransform node = UiObject(NodeName, corner);
            node.anchorMin = node.anchorMax = node.pivot = new Vector2(1f, 0f);
            node.anchoredPosition = new Vector2(0f, Y);
            node.sizeDelta = new Vector2(Width, IconSize + LabelHeight);
            // Own canvas: the gauge changes while the player picks things up and fades.
            node.gameObject.AddComponent<Canvas>();
            // The presenter stays on Bag; the gauge below it is what hides without a player.
            RectTransform gaugeNode = Stretch(UiObject("Gauge", node));
            var group = gaugeNode.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            RectTransform iconArea = UiObject("Icon", gaugeNode);
            iconArea.anchorMin = iconArea.anchorMax = new Vector2(0.5f, 1f);
            iconArea.pivot = new Vector2(0.5f, 1f);
            iconArea.anchoredPosition = Vector2.zero;
            iconArea.sizeDelta = new Vector2(IconSize, IconSize);
            Image empty = Stretch(UiObject("Empty", iconArea)).gameObject.AddComponent<Image>();
            Image fill = Stretch(UiObject("Fill", iconArea)).gameObject.AddComponent<Image>();
            RectTransform notch = UiObject("Notch", iconArea);
            notch.anchorMin = new Vector2(0.12f, 0.8f);
            notch.anchorMax = new Vector2(0.88f, 0.8f);
            notch.pivot = new Vector2(0.5f, 0.5f);
            notch.sizeDelta = new Vector2(0f, 2f);
            Image notchImage = notch.gameObject.AddComponent<Image>();
            foreach (Image image in new[] { empty, fill, notchImage })
                image.raycastTarget = false;

            RectTransform labelRect = UiObject("Label", gaugeNode);
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(0f, LabelHeight);
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            label.text = "0.0 / 35 kg";

            var gauge = gaugeNode.gameObject.AddComponent<BagGauge>();
            var so = new SerializedObject(gauge);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_empty").objectReferenceValue = empty;
            so.FindProperty("_fill").objectReferenceValue = fill;
            so.FindProperty("_notch").objectReferenceValue = notch;
            so.FindProperty("_label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            gauge.ApplyStyle();

            var hud = node.gameObject.AddComponent<PlayerLoadHud>();
            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("_gauge").objectReferenceValue = gauge;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? $"HUD.prefab: {NodeName} bottom right at y {Y}, {StylePath}; {measured}" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The bag's body as shares of the sprite's height, into BagGaugeStyle.FillRange: from its lowest drawn row (any pixel over
    // a tenth alpha) up to the last row at least half as wide as the widest, which leaves out the thin carry handle on top.
    public static string MeasureFill()
    {
        var style = AssetDatabase.LoadAssetAtPath<BagGaugeStyle>(StylePath) ?? throw new Exception("missing " + StylePath);
        Sprite sprite = style.Icon != null ? style.Icon : throw new Exception("BagGaugeStyle has no icon");
        Rect rect = sprite.textureRect;
        Color32[] pixels = Pixels(sprite.texture, rect, out int width, out int height);
        var spans = new int[height];
        int widest = 0;
        for (int y = 0; y < height; y++)
        {
            int left = -1, right = -1;
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].a <= 25)
                    continue;
                if (left < 0)
                    left = x;
                right = x;
            }
            spans[y] = left < 0 ? 0 : right - left + 1;
            widest = Mathf.Max(widest, spans[y]);
        }
        int low = Array.FindIndex(spans, span => span > 0);
        int high = Array.FindLastIndex(spans, span => span * 2 >= widest);
        if (low < 0)
            throw new Exception(sprite.name + " has no opaque pixel");
        style.FillRange = new Vector2(low / (float)height, (high + 1) / (float)height);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        // The square icon area fits the sprite by its height only when it is no wider than tall; otherwise the rows shift.
        string fit = width <= height ? "fills the icon's height" : "WIDER than tall: the fill and notch rows are off";
        return $"FillRange {style.FillRange.x:0.###}..{style.FillRange.y:0.###} of {sprite.name} ({width}x{height}, {fit})";
    }

    // A texture's pixels in a rect, bottom row first, whether or not the texture is readable.
    static Color32[] Pixels(Texture2D texture, Rect rect, out int width, out int height)
    {
        width = Mathf.RoundToInt(rect.width);
        height = Mathf.RoundToInt(rect.height);
        RenderTexture target = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture previous = RenderTexture.active;
        Graphics.Blit(texture, target);
        RenderTexture.active = target;
        var copy = new Texture2D(width, height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(rect.x, rect.y, width, height), 0, 0);
        copy.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target);
        Color32[] pixels = copy.GetPixels32();
        Object.DestroyImmediate(copy);
        return pixels;
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static RectTransform UiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
