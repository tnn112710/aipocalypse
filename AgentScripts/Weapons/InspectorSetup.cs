using System;
using System.Linq;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Adds the inventory inspector (centre panel: 3D view, a gun's part slots, stats) and the full scope view to HUD.prefab,
// without rebuilding the rest of the inventory screen. Also the ItemPreview layer and Preview_Renderer (PC_Renderer
// without its features) the 3D view renders with. Safe to re-run: the two nodes are rebuilt.
// Run: unity command script run --file AgentScripts/Weapons/InspectorSetup.cs --entry InspectorSetup.All
public static class InspectorSetup
{
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string SlotPrefabPath = "Assets/_Game/_Core/UI/Prefabs/Widgets/ItemSlot.prefab";
    const string StylePath = "Assets/_Game/Data/UI/InventoryStyle.asset";
    const string FontPath = "Assets/_Game/Data/UI/Fonts/SairaBold.asset";
    const string PipelinePath = "Assets/_Game/Settings/PC_RPAsset.asset";
    const string RendererPath = "Assets/_Game/Settings/PC_Renderer.asset";
    const string PreviewRendererPath = "Assets/_Game/Settings/Preview_Renderer.asset";
    const string ScopeMaskPath = "Assets/_Game/Content/Weapons/_Shared/Optics/ScopeMask.png";
    const string ScopeShadowPath = "Assets/_Game/Content/Weapons/_Shared/Optics/ScopeShadow.png";
    const string PreviewLayer = "ItemPreview";
    const int StatRows = 14;

    public static string All() => Layer() + "\n" + PreviewRenderer() + "\n" + Hud();

    public static string Layer()
    {
        Object tagManager = AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset");
        var tags = new SerializedObject(tagManager);
        SerializedProperty layers = tags.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == PreviewLayer)
                return $"layer {PreviewLayer} = {i}";
        for (int i = 12; i < layers.arraySize; i++)
        {
            if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                continue;
            layers.GetArrayElementAtIndex(i).stringValue = PreviewLayer;
            tags.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(tagManager);
            return $"layer {PreviewLayer} added at {i}";
        }
        throw new Exception("no free layer");
    }

    // A copy of PC_Renderer without renderer features (fog, water, grass, post effects, first-person pass), added to the
    // pipeline's renderer list. Returns its index.
    public static string PreviewRenderer()
    {
        if (AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PreviewRendererPath) == null)
            AssetDatabase.CopyAsset(RendererPath, PreviewRendererPath);
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PreviewRendererPath);
        foreach (Object feature in AssetDatabase.LoadAllAssetsAtPath(PreviewRendererPath).Where(o => o != null && o != data).ToArray())
        {
            AssetDatabase.RemoveObjectFromAsset(feature);
            Object.DestroyImmediate(feature, true);
        }
        var so = new SerializedObject(data);
        so.FindProperty("m_RendererFeatures").arraySize = 0;
        so.FindProperty("m_RendererFeatureMap").arraySize = 0;
        so.FindProperty("m_OpaqueLayerMask").intValue = ~0;
        so.FindProperty("m_TransparentLayerMask").intValue = ~0;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssetIfDirty(data);

        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        var pso = new SerializedObject(pipeline);
        SerializedProperty list = pso.FindProperty("m_RendererDataList");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == data)
                return $"preview renderer at index {i}";
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
        pso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssetIfDirty(pipeline);
        return $"preview renderer added at index {list.arraySize - 1}";
    }

    // Rebuilds only HUD.prefab's ScopeView.
    public static string ScopeOnly()
    {
        GameObject hud = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Scope(hud.transform);
            PrefabUtility.SaveAsPrefabAsset(hud, HudPath, out bool saved);
            return saved ? "HUD.prefab: ScopeView" : "failed to save HUD.prefab";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hud);
        }
    }

    public static string Hud()
    {
        var style = AssetDatabase.LoadAssetAtPath<InventoryStyle>(StylePath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var slotPrefab = AssetDatabase.LoadAssetAtPath<ItemSlotView>(SlotPrefabPath);
        GameObject hud = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform inventory = hud.transform.Find("Inventory") ?? throw new Exception("HUD has no Inventory");
            ItemInspectorView inspector = Inspector(inventory, style, font, slotPrefab);
            var screen = new SerializedObject(inventory.GetComponent<InventoryScreen>());
            screen.FindProperty("_inspector").objectReferenceValue = inspector;
            screen.ApplyModifiedPropertiesWithoutUndo();
            Scope(hud.transform);
            PrefabUtility.SaveAsPrefabAsset(hud, HudPath, out bool saved);
            return saved ? "HUD.prefab: Inventory/Inspector, ScopeView" : "failed to save HUD.prefab";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hud);
        }
    }

    // Centre column between the equipment panel (x 40-580) and the loot panel (from x 1340), above the quick slots.
    static ItemInspectorView Inspector(Transform inventory, InventoryStyle style, TMP_FontAsset font, ItemSlotView slotPrefab)
    {
        Transform old = inventory.Find("Inspector");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        RectTransform root = Ui("Inspector", inventory);
        // Under the tooltip, drag icon and message, which follow it.
        root.SetSiblingIndex(inventory.Find("Quick").GetSiblingIndex() + 1);
        Place(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(720f, 740f));
        var panel = root.gameObject.AddComponent<Image>();
        panel.sprite = style.PanelSprite;
        panel.type = StyleUtils.ImageTypeFor(style.PanelSprite);
        // More opaque than the other panels: the held gun and the world behind would muddy the 3D view.
        Color panelColor = style.PanelColor;
        panelColor.a = Mathf.Max(panelColor.a, 0.95f);
        panel.color = panelColor;
        panel.raycastTarget = true;

        RectTransform content = Ui("Content", root);
        Place(content, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var backing = content.gameObject.AddComponent<Image>();
        backing.color = new Color(0f, 0f, 0f, 0.55f);
        backing.raycastTarget = false;
        TMP_Text title = Label("Title", content, font, 26f, TextAlignmentOptions.Left, style.TextColor);
        Top(title.rectTransform, 20f, -14f, 34f);
        TMP_Text info = Label("Info", content, font, 15f, TextAlignmentOptions.Left, style.DimTextColor);
        Top(info.rectTransform, 20f, -50f, 22f);

        RectTransform frame = Ui("StageFrame", content);
        Top(frame, 16f, -80f, 330f);
        var frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0f, 0f, 0f, 0.28f);
        frameImage.raycastTarget = false;
        RectTransform stageRect = Ui("Stage", frame);
        Place(stageRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var stageImage = stageRect.gameObject.AddComponent<RawImage>();
        stageImage.raycastTarget = true;
        var stage = stageRect.gameObject.AddComponent<ItemPreviewStage>();
        var stageInput = stageRect.gameObject.AddComponent<ItemPreviewInput>();
        var stageSo = new SerializedObject(stage);
        stageSo.FindProperty("_image").objectReferenceValue = stageImage;
        stageSo.FindProperty("_renderer").intValue = RendererIndex();
        stageSo.ApplyModifiedPropertiesWithoutUndo();
        TMP_Text turn = Label("TurnHint", frame, font, 13f, TextAlignmentOptions.BottomLeft, style.DimTextColor);
        Place(turn.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(10f, 6f), new Vector2(-20f, 20f));

        // Six part slots in GunSlot order, centred, captions under them.
        var slots = new ItemSlotView[GunSlots.Count];
        const float tile = 88f, gap = 20f;
        float startX = (720f - (GunSlots.Count * tile + (GunSlots.Count - 1) * gap)) * 0.5f;
        for (int i = 0; i < GunSlots.Count; i++)
        {
            var slot = (ItemSlotView)PrefabUtility.InstantiatePrefab(slotPrefab, content);
            slot.name = "Slot_" + (GunSlot)i;
            Place((RectTransform)slot.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(startX + i * (tile + gap), -424f), new Vector2(tile, tile));
            slots[i] = slot;
        }

        var names = new TMP_Text[StatRows];
        var values = new TMP_Text[StatRows];
        const float rowHeight = 22f, statsTop = -552f, column = 328f;
        for (int i = 0; i < StatRows; i++)
        {
            int col = i / (StatRows / 2), row = i % (StatRows / 2);
            float x = 24f + col * (column + 16f);
            float y = statsTop - row * rowHeight;
            names[i] = Label("Stat" + i, content, font, 15f, TextAlignmentOptions.Left, style.DimTextColor);
            Place(names[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(column, rowHeight));
            values[i] = Label("Value" + i, content, font, 15f, TextAlignmentOptions.Right, style.TextColor);
            values[i].richText = true;
            Place(values[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(column, rowHeight));
        }

        TMP_Text hint = Label("Hint", root, font, 18f, TextAlignmentOptions.Center, style.DimTextColor);
        Place(hint.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-40f, -40f));
        hint.textWrappingMode = TextWrappingModes.Normal;

        RectTransform leaderRect = Ui("Leader", root);
        Place(leaderRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(10f, 2f));
        var leader = leaderRect.gameObject.AddComponent<Image>();
        leader.raycastTarget = false;
        leader.color = style.LeaderColor;
        leader.enabled = false;

        var view = root.gameObject.AddComponent<ItemInspectorView>();
        var so = new SerializedObject(view);
        so.FindProperty("_style").objectReferenceValue = style;
        so.FindProperty("_content").objectReferenceValue = content.gameObject;
        so.FindProperty("_title").objectReferenceValue = title;
        so.FindProperty("_info").objectReferenceValue = info;
        so.FindProperty("_hint").objectReferenceValue = hint;
        so.FindProperty("_turnHint").objectReferenceValue = turn;
        so.FindProperty("_stage").objectReferenceValue = stage;
        so.FindProperty("_stageInput").objectReferenceValue = stageInput;
        so.FindProperty("_leader").objectReferenceValue = leader;
        Fill(so.FindProperty("_slots"), slots);
        Fill(so.FindProperty("_statNames"), names);
        Fill(so.FindProperty("_statValues"), values);
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    static int RendererIndex()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        SerializedProperty list = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
        var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PreviewRendererPath);
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == data)
                return i;
        throw new Exception("run PreviewRenderer first");
    }

    // Full screen, under the HUD widgets: a square of one screen height (the circle mask and the reticle) with black bars
    // past each side, so moving it with the aim sway never shows the world around it.
    static void Scope(Transform hud)
    {
        Transform old = hud.Find("ScopeView");
        if (old != null)
            Object.DestroyImmediate(old.gameObject);
        RectTransform root = Ui("ScopeView", hud);
        root.SetSiblingIndex(1);
        Place(root, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var canvas = root.gameObject.AddComponent<Canvas>();
        var group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        RectTransform view = Ui("View", root);
        Place(view, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1080f, 1080f));
        // Under the mask, so they only show through the circle: the dark while the eye finds the scope, and the shadow
        // (twice the circle's size) that slides off it.
        Image dim = Black("Dim", view);
        Place(dim.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image shadow = Black("Shadow", view);
        Place(shadow.rectTransform, new Vector2(-0.5f, -0.5f), new Vector2(1.5f, 1.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        shadow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ScopeShadowPath) ?? throw new Exception("run AttachmentSetup.Optics first");
        // The circle and its bars move and open together.
        RectTransform pupil = Ui("Pupil", view);
        Place(pupil, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Image mask = Black("Mask", pupil);
        Place(mask.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        mask.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ScopeMaskPath) ?? throw new Exception("run AttachmentSetup.Optics first");
        // The bars reach a few pixels into the square, so no seam shows between them and the mask.
        const float far = 4000f, overlap = 4f;
        Place(Black("Left", pupil).rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 0.5f), new Vector2(overlap, 0f), new Vector2(far, far * 2f));
        Place(Black("Right", pupil).rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), new Vector2(-overlap, 0f), new Vector2(far, far * 2f));
        Place(Black("Top", pupil).rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, -overlap), new Vector2(0f, far));
        Place(Black("Bottom", pupil).rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, overlap), new Vector2(0f, far));
        Image reticle = Black("Reticle", view);
        Place(reticle.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        reticle.preserveAspect = true;
        reticle.enabled = false;

        var hudView = root.gameObject.AddComponent<ScopeHud>();
        var so = new SerializedObject(hudView);
        so.FindProperty("_canvas").objectReferenceValue = canvas;
        so.FindProperty("_group").objectReferenceValue = group;
        so.FindProperty("_view").objectReferenceValue = view;
        so.FindProperty("_pupil").objectReferenceValue = pupil;
        so.FindProperty("_shadow").objectReferenceValue = shadow.rectTransform;
        so.FindProperty("_dim").objectReferenceValue = dim;
        so.FindProperty("_reticle").objectReferenceValue = reticle;
        so.ApplyModifiedPropertiesWithoutUndo();
        canvas.enabled = false;
    }

    static Image Black(string name, Transform parent)
    {
        var image = Ui(name, parent).gameObject.AddComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = false;
        return image;
    }

    static void Fill<T>(SerializedProperty array, T[] values) where T : Object
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Top(RectTransform rect, float inset, float y, float height) =>
        Place(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(-inset * 2f, height));

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = Ui(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        // No Ellipsis overflow: with these static fonts TMP then draws no characters at all.
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = string.Empty;
        // What TMP sets on its own when an instance wakes: set here, the scene's HUD instance shows no override for it.
        text.textStyle = TMP_Style.NormalStyle;
        return text;
    }

    static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
