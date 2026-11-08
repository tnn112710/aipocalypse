using System;
using System.Collections.Generic;
using MB13.Aipocalypse.Core.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the hurt flash: Data/UI/HurtFeedbackStyle.asset (if missing) and the full-screen HurtFeedback canvas in HUD.prefab (four
// edge RawImages + HurtFeedbackHud). Safe to re-run; the HUD part is rebuilt, an existing style is kept.
public static class HurtFeedbackSetup
{
    const string StylePath = "Assets/_Game/Data/UI/HurtFeedbackStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string NodeName = "HurtFeedback";

    public static object Build()
    {
        var log = new List<string>();
        HurtFeedbackStyle style = Style(log);
        log.Add(Hud(style));
        return string.Join("\n", log);
    }

    static HurtFeedbackStyle Style(List<string> log)
    {
        var style = AssetDatabase.LoadAssetAtPath<HurtFeedbackStyle>(StylePath);
        if (style != null)
        {
            log.Add("kept " + StylePath);
            return style;
        }
        style = ScriptableObject.CreateInstance<HurtFeedbackStyle>();
        AssetDatabase.CreateAsset(style, StylePath);
        AssetDatabase.SaveAssetIfDirty(style);
        log.Add("created " + StylePath);
        return style;
    }

    static string Hud(HurtFeedbackStyle style)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform old = root.transform.Find(NodeName);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            // Stretched over the whole screen (not the SafeFrame inset). First child, so the HUD widgets and the death screen
            // draw over it. No ignoreParentGroups: the HUD's fade on death hides it too.
            RectTransform node = UiObject(NodeName, root.transform);
            Stretch(node);
            node.SetAsFirstSibling();
            // Its own canvas: the edges change alpha every frame while flashing.
            node.gameObject.AddComponent<Canvas>();

            RawImage top = Edge("Top", node);
            RawImage bottom = Edge("Bottom", node);
            RawImage left = Edge("Left", node);
            RawImage right = Edge("Right", node);

            var hud = node.gameObject.AddComponent<HurtFeedbackHud>();
            var so = new SerializedObject(hud);
            Set(so, "_style", style);
            Set(so, "_top", top);
            Set(so, "_bottom", bottom);
            Set(so, "_left", left);
            Set(so, "_right", right);
            so.ApplyModifiedPropertiesWithoutUndo();
            // The prefab's HUD root has no size: edges get placeholder sizes for the reference height; runtime re-sizes them.
            float height = ReferenceHeight(root);
            hud.ApplyStyle(height);

            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return $"HUD.prefab: {NodeName} (child 0 of {root.transform.childCount}, nested canvas, Top/Bottom/Left/Right, " +
                $"HurtFeedbackHud; edges sized for a {height}px HUD, re-sized at runtime)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Off in the prefab: HurtFeedbackHud gives it its gradient and turns it on per hit.
    static RawImage Edge(string name, Transform parent)
    {
        var image = UiObject(name, parent).gameObject.AddComponent<RawImage>();
        image.raycastTarget = false;
        image.enabled = false;
        return image;
    }

    static float ReferenceHeight(GameObject root)
    {
        var scaler = root.GetComponent<CanvasScaler>();
        return scaler != null && scaler.referenceResolution.y > 0f ? scaler.referenceResolution.y : 1080f;
    }

    static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty property = so.FindProperty(field)
            ?? throw new Exception($"{so.targetObject.GetType().Name} has no serialized field {field}");
        property.objectReferenceValue = value;
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
}
