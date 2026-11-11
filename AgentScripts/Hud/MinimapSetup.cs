using System;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.MapSystem;
using MB13.Aipocalypse.Core.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds the radar minimap: Data/UI/MinimapStyle.asset, the Home / Objective marker kinds in Data/Map/, the Minimap node at
// HUD.prefab SafeFrame/TopRight (the clock moves to SafeFrame/TopCenter), and the home's marker in the open scene.
// Look after Synty's HUD_Apocalypse_Minimap_Dial_01 (its sprites only, never the prefab). Safe to re-run.
// Run: unity command script run --file AgentScripts/Hud/MinimapSetup.cs --entry MinimapSetup.Build
public static class MinimapSetup
{
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string StylePath = "Assets/_Game/Data/UI/MinimapStyle.asset";
    const string MapDir = "Assets/_Game/Data/Map";
    const string HomePath = MapDir + "/Home.asset";
    const string ObjectivePath = MapDir + "/Objective.asset";
    const string Sprites = "Assets/Synty/InterfaceApocalypseHUD/Sprites/";
    const string FontPath = "Assets/_Game/Data/UI/Fonts/SairaHeader.asset";
    const string FontUnderlayPath = "Assets/_Game/Data/UI/Fonts/SairaHeader_Underlay.mat";
    const float Size = 220f;

    public static string Build() => string.Join("\n", Assets(), Hud(), HomeMarker());

    public static string Assets()
    {
        var style = LoadOrCreate<MinimapStyle>(StylePath);
        style.BackgroundSprite = Sprite("HUD/SPR_HUD_Apocalypse_Map_Circle_Large_02");
        style.RingSprite = Sprite("HUD/SPR_HUD_Apocalypse_Ring_Large_02_Clean");
        style.PlayerSprite = Sprite("HUD/SPR_HUD_Apocalypse_Map_Player_01");
        style.ViewConeSprite = Sprite("HUD/SPR_HUD_Apocalypse_Map_PlayerViewCone_01");
        style.EnemySprite = Sprite("HUD/SPR_HUD_Apocalypse_Map_Circle_Small_01_Underlay");
        style.LetterFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath) ?? throw new Exception("missing " + FontPath);
        style.LetterMaterial = AssetDatabase.LoadAssetAtPath<Material>(FontUnderlayPath);
        Save(style);

        Directory(MapDir);
        var home = LoadOrCreate<MapMarkerKind>(HomePath);
        home.Background = Sprite("HUD/SPR_HUD_Apocalypse_Map_Icon_Background_01");
        home.BackgroundColor = new Color(0.9f, 0.84f, 0.29f, 1f);
        home.Icon = Sprite("Icons_Map/ICON_Apocalpyse_Map_Home_01");
        home.IconColor = new Color(0.055f, 0.055f, 0.055f, 1f);
        home.Size = 30f;
        home.PinToEdge = true;
        Save(home);

        var objective = LoadOrCreate<MapMarkerKind>(ObjectivePath);
        objective.Background = Sprite("HUD/SPR_HUD_Apocalypse_Map_Objective_01_Clean");
        objective.BackgroundColor = new Color(0.9f, 0.84f, 0.29f, 1f);
        objective.Icon = null;
        objective.Size = 30f;
        objective.PinToEdge = true;
        Save(objective);
        return $"assets: {StylePath}, {HomePath}, {ObjectivePath}";
    }

    public static string Hud()
    {
        var style = AssetDatabase.LoadAssetAtPath<MinimapStyle>(StylePath) ?? throw new Exception("run Assets first");
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform safe = root.transform.Find("SafeFrame") ?? throw new Exception("no SafeFrame");
            string clock = MoveClock(safe);

            Transform topRight = safe.Find("TopRight") ?? throw new Exception("no SafeFrame/TopRight");
            Transform old = topRight.Find("Minimap");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            // Pulled in a little so the rim letters (centred on the ring) stay inside the safe frame.
            RectTransform minimap = UiObject("Minimap", topRight);
            minimap.anchorMin = minimap.anchorMax = minimap.pivot = Vector2.one;
            minimap.anchoredPosition = new Vector2(-10f, -10f);
            minimap.sizeDelta = new Vector2(Size, Size);
            minimap.gameObject.AddComponent<Canvas>();

            RectTransform content = Stretch(UiObject("Content", minimap));
            RectTransform disc = Stretch(UiObject("Disc", content));
            Image background = Img(Stretch(UiObject("Background", disc)));

            RectTransform cone = UiObject("ViewCone", disc);
            cone.anchorMin = cone.anchorMax = new Vector2(0.5f, 0.5f);
            cone.pivot = new Vector2(0.5f, 0f);
            // From the centre to the rim, so it never pokes out of the disc.
            cone.sizeDelta = new Vector2(Size * 0.5f, Size * 0.5f);
            Image viewCone = Img(cone);

            Image marker = Img(Centred(UiObject("MarkerTemplate", Stretch(UiObject("Markers", disc))), 30f));
            Img(Stretch(UiObject("Icon", marker.rectTransform)));
            Image enemy = Img(Centred(UiObject("EnemyTemplate", Stretch(UiObject("Enemies", disc))), style.EnemySize));
            Image player = Img(Centred(UiObject("Player", disc), 30f));
            Image ring = Img(Stretch(UiObject("Ring", disc)));

            RectTransform letters = Stretch(UiObject("Directions", content));
            TMP_Text north = Letter("N", letters), east = Letter("E", letters), south = Letter("S", letters), west = Letter("W", letters);

            var view = minimap.gameObject.AddComponent<MinimapView>();
            var so = new SerializedObject(view);
            so.FindProperty("_style").objectReferenceValue = style;
            so.FindProperty("_content").objectReferenceValue = content.gameObject;
            so.FindProperty("_disc").objectReferenceValue = disc;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_ring").objectReferenceValue = ring;
            so.FindProperty("_viewCone").objectReferenceValue = viewCone;
            so.FindProperty("_player").objectReferenceValue = player;
            so.FindProperty("_north").objectReferenceValue = north;
            so.FindProperty("_east").objectReferenceValue = east;
            so.FindProperty("_south").objectReferenceValue = south;
            so.FindProperty("_west").objectReferenceValue = west;
            so.FindProperty("_enemyTemplate").objectReferenceValue = enemy;
            so.FindProperty("_markerTemplate").objectReferenceValue = marker;
            so.ApplyModifiedPropertiesWithoutUndo();
            view.ApplyStyle();
            enemy.gameObject.SetActive(false);
            marker.gameObject.SetActive(false);
            var normal = TMP_Settings.defaultStyleSheet != null ? TMP_Settings.defaultStyleSheet.GetStyle("Normal") : null;
            foreach (TMP_Text text in new[] { north, east, south, west })
            {
                if (normal != null)
                    text.textStyle = normal;
                text.ForceMeshUpdate();
            }

            var hud = minimap.gameObject.AddComponent<MinimapHud>();
            var hso = new SerializedObject(hud);
            hso.FindProperty("_view").objectReferenceValue = view;
            hso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? $"HUD.prefab: SafeFrame/TopRight/Minimap; {clock}" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // The clock goes to a TopCenter corner anchor, hanging down from its middle.
    static string MoveClock(Transform safe)
    {
        Transform topCenter = safe.Find("TopCenter");
        if (topCenter == null)
        {
            RectTransform anchor = UiObject("TopCenter", safe);
            anchor.anchorMin = anchor.anchorMax = anchor.pivot = new Vector2(0.5f, 1f);
            anchor.sizeDelta = Vector2.zero;
            anchor.anchoredPosition = Vector2.zero;
            Transform topRight = safe.Find("TopRight");
            if (topRight != null)
                anchor.SetSiblingIndex(topRight.GetSiblingIndex());
            topCenter = anchor;
        }
        Transform clock = safe.Find("TopRight/Clock") ?? topCenter.Find("Clock");
        if (clock == null)
            return "no Clock found";
        clock.SetParent(topCenter, false);
        var rect = (RectTransform)clock;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        return "clock at SafeFrame/TopCenter/Clock";
    }

    // The home's marker: a child of Home Interior at the base, listed on the scene scope for injection.
    public static string HomeMarker()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.isDirty)
            return "the open scene has unsaved changes: home marker skipped";
        GameObject home = GameObject.Find("Home Interior");
        if (home == null)
            return "no Home Interior in " + scene.name + ": no home marker";
        Transform existing = home.transform.Find("Home Marker");
        GameObject go = existing != null ? existing.gameObject : new GameObject("Home Marker");
        go.transform.SetParent(home.transform, false);
        go.transform.localPosition = Vector3.zero;
        var marker = go.GetComponent<MapMarker>() ?? go.AddComponent<MapMarker>();
        var so = new SerializedObject(marker);
        so.FindProperty("_kind").objectReferenceValue = AssetDatabase.LoadAssetAtPath<MapMarkerKind>(HomePath);
        so.ApplyModifiedPropertiesWithoutUndo();
        string listed = ListForInjection(scene, go);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{scene.name}: Home Interior/Home Marker; {listed}";
    }

    static string ListForInjection(Scene scene, GameObject target)
    {
        GameLifetimeScope scope = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            scope = root.GetComponentInChildren<GameLifetimeScope>(true);
            if (scope != null)
                break;
        }
        if (scope == null)
            return "no GameLifetimeScope: the marker won't register";
        var fields = new SerializedObject(scope);
        SerializedProperty list = fields.FindProperty("autoInjectGameObjects");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == target)
                return "already in Auto Inject";
        list.arraySize++;
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = target;
        fields.ApplyModifiedPropertiesWithoutUndo();
        return "added to Auto Inject";
    }

    static TMP_Text Letter(string letter, Transform parent)
    {
        RectTransform rect = Centred(UiObject(letter, parent), 40f);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = letter;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    static Image Img(RectTransform rect)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    static RectTransform Centred(RectTransform rect, float size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);
        return rect;
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

    static Sprite Sprite(string path) =>
        AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + path + ".png") ?? throw new Exception("missing sprite " + path);

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void Directory(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder("Assets/_Game/Data", "Map");
    }

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
