using System;
using System.Collections.Generic;
using Aipocalypse.Core.Debugging;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the dev console: Data/Debug/ConsoleSettings.asset + ConsoleStyle.asset, _Core/_Debug/Console/Prefabs/DebugConsole.prefab,
// and assigns both to ProjectLifetimeScope.prefab. Safe to re-run: existing assets keep their values, the prefab is rebuilt.
public static class DebugConsoleSetup
{
    const string DataDir = "Assets/_Game/Data/Debug";
    const string SettingsPath = DataDir + "/ConsoleSettings.asset";
    const string StylePath = DataDir + "/ConsoleStyle.asset";
    const string PrefabDir = "Assets/_Game/_Core/_Debug/Console/Prefabs";
    const string PrefabPath = PrefabDir + "/DebugConsole.prefab";
    const string RootScopePath = "Assets/_Game/_Core/_Bootstrap/ProjectLifetimeScope.prefab";
    const string FontPath = "Assets/Samples/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    // Over the HUD (10) and the death screen (100).
    const int SortingOrder = 1000;
    // Share of the screen height the open console covers, from the top.
    const float PanelHeight = 0.45f;
    const float InputHeight = 40f;
    const float Margin = 14f;

    public static object All()
    {
        var log = new List<string>();
        Folder("Assets/_Game/Data", "Debug");
        Folder("Assets/_Game/_Core/_Debug/Console", "Prefabs");

        var settings = Asset<ConsoleSettings>(SettingsPath, log);
        var style = Asset<ConsoleStyle>(StylePath, log);
        if (style.Font == null)
        {
            style.Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new Exception("missing " + FontPath);
            EditorUtility.SetDirty(style);
            AssetDatabase.SaveAssetIfDirty(style);
        }

        ConsoleView overlay = Prefab(style);
        log.Add("built " + PrefabPath);
        log.Add(AssignToRootScope(settings, overlay));
        return string.Join("\n", log);
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    static T Asset<T>(string path, List<string> log) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        log.Add("created " + path);
        return asset;
    }

    static ConsoleView Prefab(ConsoleStyle style)
    {
        var root = new GameObject("DebugConsole", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        try
        {
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Screen log: top-left, newest on top.
            TextMeshProUGUI screenText = Text("ScreenLog", root.transform, style);
            RectTransform screenRect = screenText.rectTransform;
            screenRect.anchorMin = screenRect.anchorMax = screenRect.pivot = new Vector2(0f, 1f);
            screenRect.anchoredPosition = new Vector2(16f, -16f);
            screenRect.sizeDelta = new Vector2(1100f, 700f);
            screenText.alignment = TextAlignmentOptions.TopLeft;
            screenText.textWrappingMode = TextWrappingModes.Normal;
            screenText.fontSize = style.ScreenFontSize;
            screenText.text = string.Empty;
            var screenView = screenText.gameObject.AddComponent<ScreenLogView>();
            Set(screenView, "_text", screenText);

            // Console panel: the top of the screen.
            RectTransform panel = UiObject("Panel", root.transform);
            panel.anchorMin = new Vector2(0f, 1f - PanelHeight);
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;

            Image background = Img(UiObject("Background", panel));
            Stretch(background.rectTransform, 0f, 0f, 0f, 0f);

            RectTransform logArea = UiObject("LogArea", panel);
            Stretch(logArea, Margin, Margin, 8f, InputHeight + 6f);
            logArea.gameObject.AddComponent<RectMask2D>();
            TextMeshProUGUI logText = Text("Log", logArea, style);
            Stretch(logText.rectTransform, 0f, 0f, 0f, 0f);
            logText.alignment = TextAlignmentOptions.BottomLeft;
            logText.textWrappingMode = TextWrappingModes.Normal;
            logText.overflowMode = TextOverflowModes.Overflow;
            logText.text = string.Empty;

            Image inputBar = Img(UiObject("InputBar", panel));
            RectTransform inputRect = inputBar.rectTransform;
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.anchoredPosition = Vector2.zero;
            inputRect.sizeDelta = new Vector2(0f, InputHeight);
            TextMeshProUGUI inputText = Text("Input", inputRect, style);
            Stretch(inputText.rectTransform, Margin, Margin, 0f, 0f);
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            inputText.textWrappingMode = TextWrappingModes.NoWrap;
            inputText.text = "> ";
            // Placed by ConsoleView at the caret's character.
            Image caret = Img(UiObject("Caret", inputText.rectTransform));
            caret.rectTransform.anchorMin = caret.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            caret.rectTransform.pivot = new Vector2(0f, 0.5f);
            caret.rectTransform.sizeDelta = new Vector2(2f, style.FontSize);

            // Suggestions: under the panel's bottom-left corner, sized to their text.
            Image suggestions = Img(UiObject("Suggestions", panel));
            RectTransform suggestionsRect = suggestions.rectTransform;
            suggestionsRect.anchorMin = suggestionsRect.anchorMax = Vector2.zero;
            suggestionsRect.pivot = new Vector2(0f, 1f);
            suggestionsRect.anchoredPosition = new Vector2(Margin, -2f);
            var layout = suggestions.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = suggestions.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TextMeshProUGUI suggestionsText = Text("Text", suggestionsRect, style);
            suggestionsText.alignment = TextAlignmentOptions.TopLeft;
            suggestionsText.textWrappingMode = TextWrappingModes.NoWrap;
            suggestionsText.text = string.Empty;
            suggestions.gameObject.SetActive(false);

            TextMeshProUGUI buildLabel = BuildLabel(root.transform, style);

            var view = root.AddComponent<ConsoleView>();
            var so = new SerializedObject(view);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_panel").objectReferenceValue = panel;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_log").objectReferenceValue = logText;
            so.FindProperty("_inputBackground").objectReferenceValue = inputBar;
            so.FindProperty("_input").objectReferenceValue = inputText;
            so.FindProperty("_caret").objectReferenceValue = caret;
            so.FindProperty("_suggestionsBackground").objectReferenceValue = suggestions;
            so.FindProperty("_suggestions").objectReferenceValue = suggestionsText;
            so.FindProperty("_screenLog").objectReferenceValue = screenView;
            so.FindProperty("_buildLabel").objectReferenceValue = buildLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyStyle();
            // Hidden in the prefab; the view also hides it on Awake.
            panel.gameObject.SetActive(false);

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return saved.GetComponent<ConsoleView>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Adds the build label to the existing prefab without rebuilding the rest. Safe to re-run.
    public static string AddBuildLabel()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var view = root.GetComponent<ConsoleView>();
            if (root.transform.Find("BuildLabel") != null)
                return "BuildLabel already there";
            TextMeshProUGUI label = BuildLabel(root.transform, view.Style);
            Set(view, "_buildLabel", label);
            view.ApplyStyle();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "added BuildLabel to " + PrefabPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // Bottom-right corner, small and dim: which build this is (ConsoleView sets the text).
    static TextMeshProUGUI BuildLabel(Transform root, ConsoleStyle style)
    {
        TextMeshProUGUI label = Text("BuildLabel", root, style);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-12f, 8f);
        rect.sizeDelta = new Vector2(1200f, 30f);
        label.alignment = TextAlignmentOptions.BottomRight;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.fontSize = Mathf.Max(12f, style.ScreenFontSize * 0.75f);
        label.text = string.Empty;
        return label;
    }

    static string AssignToRootScope(ConsoleSettings settings, ConsoleView overlay)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RootScopePath);
        try
        {
            Component scope = root.GetComponent("ProjectLifetimeScope") ?? throw new Exception("no ProjectLifetimeScope on " + RootScopePath);
            var so = new SerializedObject(scope);
            so.FindProperty("_consoleSettings").objectReferenceValue = settings;
            so.FindProperty("_consoleOverlay").objectReferenceValue = overlay;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, RootScopePath);
            return "ProjectLifetimeScope.prefab: console settings + overlay assigned";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform UiObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // Stretched over the parent, inset by left / right / top / bottom.
    static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    static Image Img(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static TextMeshProUGUI Text(string name, Transform parent, ConsoleStyle style)
    {
        var tmp = UiObject(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = style.Font;
        tmp.fontSize = style.FontSize;
        tmp.richText = true;
        tmp.raycastTarget = false;
        return tmp;
    }
}
