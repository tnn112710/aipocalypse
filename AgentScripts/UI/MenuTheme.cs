using System.Collections.Generic;
using MB13.Aipocalypse.Core.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Moves the menus from the blue / navy look to MenuStyle's orange highlight and warm brown: every button in the HUD prefab and
// the main menu gets a MenuButtonColors (the style's colours applied on wake, and baked here), and the navy panels of the pause
// menu, the sleep chooser and the main menu get their red and blue channels swapped (navy -> brown). Safe to re-run: a swapped
// colour is no longer blue. After changing MenuStyle, re-run Bake to refresh the colours stored in the prefabs.
//   unity command script run --file AgentScripts/UI/MenuTheme.cs --entry MenuTheme.All
public static class MenuTheme
{
    const string StylePath = "Assets/_Game/Data/UI/MenuStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string MainMenuPath = "Assets/_Game/Scenes/MainMenu.unity";
    static readonly string[] HudMenuRoots = { "PauseMenu", "SleepScreen" };

    public static string All()
    {
        var log = new List<string>();
        MenuStyle style = AssetDatabase.LoadAssetAtPath<MenuStyle>(StylePath) ?? throw new System.Exception("missing " + StylePath);
        var defaults = ScriptableObject.CreateInstance<MenuStyle>();
        style.Button = defaults.Button;
        style.Highlight = defaults.Highlight;
        style.Panel = defaults.Panel;
        Object.DestroyImmediate(defaults);
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        log.Add("MenuStyle: brown Button / Panel, orange Highlight");

        log.Add(Hud(style));
        log.Add(MainMenu(style));
        return string.Join("\n", log);
    }

    public static string Bake() => string.Join("\n", Hud(AssetDatabase.LoadAssetAtPath<MenuStyle>(StylePath)),
        MainMenu(AssetDatabase.LoadAssetAtPath<MenuStyle>(StylePath)));

    static string Hud(MenuStyle style)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            int buttons = Buttons(root, style, out string kinds);
            int panels = 0;
            foreach (string name in HudMenuRoots)
            {
                Transform menu = root.transform.Find(name);
                if (menu != null)
                    panels += Panels(menu.gameObject);
            }
            if (buttons + panels > 0)
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return $"HUD.prefab: {buttons} buttons ({kinds}), {panels} navy images -> brown";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string MainMenu(MenuStyle style)
    {
        string previous = SceneManager.GetActiveScene().path;
        if (SceneManager.GetActiveScene().isDirty)
            return "MainMenu: skipped, the open scene has unsaved changes";
        Scene scene = EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        int buttons = 0, panels = 0;
        string kinds = "";
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            buttons += Buttons(root, style, out string k);
            kinds += k;
            panels += Panels(root);
        }
        if (buttons + panels > 0)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        if (!string.IsNullOrEmpty(previous) && previous != MainMenuPath)
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
        return $"MainMenu: {buttons} buttons ({kinds}), {panels} navy images -> brown";
    }

    static int Buttons(GameObject root, MenuStyle style, out string kinds)
    {
        int changed = 0, filled = 0, text = 0, keep = 0;
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            bool added = false;
            if (!button.TryGetComponent(out MenuButtonColors colors))
            {
                colors = button.gameObject.AddComponent<MenuButtonColors>();
                added = true;
                Color normal = button.colors.normalColor;
                MenuButtonColors.ButtonLook look = normal.a < 0.05f ? MenuButtonColors.ButtonLook.Text
                    : IsStripColour(normal, style) ? MenuButtonColors.ButtonLook.Filled : MenuButtonColors.ButtonLook.Keep;
                var so = new SerializedObject(colors);
                so.FindProperty("_style").objectReferenceValue = style;
                so.FindProperty("_look").enumValueIndex = (int)look;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            ColorBlock before = button.colors;
            colors.Apply();
            if (added || button.colors != before)
            {
                changed++;
                EditorUtility.SetDirty(button);
                EditorUtility.SetDirty(colors);
            }
            switch (new SerializedObject(colors).FindProperty("_look").enumValueIndex)
            {
                case 0: filled++; break;
                case 1: text++; break;
                default: keep++; break;
            }
        }
        kinds = $"{filled} filled, {text} text, {keep} keep";
        return changed;
    }

    // The navy strip of the old style, or the brown it was given.
    static bool IsStripColour(Color c, MenuStyle style) =>
        Near(c, new Color(0.086f, 0.188f, 0.302f, 0.9f)) || Near(c, style.Button);

    // Navy (blue above red) -> brown by swapping red and blue; alpha and green stay.
    static int Panels(GameObject root)
    {
        int changed = 0;
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            Color c = image.color;
            if (c.a < 0.05f || c.b < c.r + 0.03f || c.b > 0.9f)
                continue;
            image.color = new Color(c.b, c.g, c.r, c.a);
            EditorUtility.SetDirty(image);
            changed++;
        }
        return changed;
    }

    static bool Near(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.02f && Mathf.Abs(a.g - b.g) < 0.02f && Mathf.Abs(a.b - b.b) < 0.02f && Mathf.Abs(a.a - b.a) < 0.02f;
}
