using System;
using System.Collections.Generic;
using DG.Tweening;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.LocalizationSystem;
using MB13.Aipocalypse.Core.PlayerSystem;
using MB13.Aipocalypse.Core.SleepSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Editor.Localization;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds sleeping: Data/Sleep/SleepSettings and the two pose sequences (lie down, get up, in the bed anchor's space), the
// SleepScreen node in HUD.prefab, and in WorldMap the SleepInstaller, the bed in the house ("Home Interior" scene root:
// a map rebuild of Landmarks doesn't remove it) and the house door standing open. Safe to re-run.
// Run: unity command script run --file AgentScripts/Sleep/SleepSetup.cs --entry SleepSetup.All
// After a Landmarks rebuild (new house, door shut again): SleepSetup.World.
public static class SleepSetup
{
    const string SettingsPath = "Assets/_Game/Data/Sleep/SleepSettings.asset";
    const string LieDownPath = "Assets/_Game/Data/Player/Sequences/SleepLieDown.asset";
    const string GetUpPath = "Assets/_Game/Data/Player/Sequences/SleepGetUp.asset";
    const string CameraTuningPath = "Assets/_Game/Data/Player/PlayerCameraTuning.asset";
    const string StylePath = "Assets/_Game/Data/UI/MenuStyle.asset";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string BedModelPath = "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Prefabs/Environment/Bed_2.prefab";
    const string HousePath = "Environment/Landmarks/Home Base/House_1";
    const string InteriorName = "Home Interior";
    // Over the inventory (50) and the death screen (100), under the pause menu (200).
    const int Sorting = 150;

    // The bed in the house's space: against the west wall, pillow towards the front; you get in from the room side (+x).
    static readonly Vector3 BedLocalPosition = new(-4.02f, 1f, -1.35f);
    const float BedLocalYaw = 0f;
    // Degrees the door leaf stands open (inwards).
    const float DoorOpenYaw = 105f;

    // Lying: eye over the pillow, looking up along the bed. Heights are above the floor.
    static readonly Vector2 LyingOffset = new(0f, 0.5f);
    const float LyingHeight = 0.78f;
    static readonly Vector2 SittingOffset = new(0.5f, -0.05f);
    const float SittingHeight = 1.12f;
    static readonly Vector2 StandingOffset = new(1.05f, -0.05f);

    public static string All() => string.Join("\n", Assets(), Hud(), World(), LocalizationKit.ApplyLabels());

    // ---- settings and sequences ----

    public static string Assets()
    {
        Folder("Assets/_Game/Data/Sleep");
        float eye = AssetDatabase.LoadAssetAtPath<PlayerCameraTuning>(CameraTuningPath).EyeHeight;

        PoseSequence lieDown = Sequence(LieDownPath, 0.25f, new[]
        {
            Beat("Sit", 0.95f, SittingOffset, SittingHeight, 18f, -90f, 0f),
            Beat("Lie", 1.5f, LyingOffset, LyingHeight, -76f, 180f, 0f),
        });
        PoseSequence getUp = Sequence(GetUpPath, 0f, new[]
        {
            Beat("Lying", 0f, LyingOffset, LyingHeight, -76f, 180f, 1.1f),
            Beat("Sit", 1.2f, SittingOffset, SittingHeight, 8f, 90f, 0f),
            Beat("Stand", 0.85f, StandingOffset, eye, 0f, 90f, 0f),
        });

        var settings = AssetDatabase.LoadAssetAtPath<SleepSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<SleepSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        settings.LieDown = lieDown;
        settings.GetUp = getUp;
        Save(settings);
        return $"assets: {SettingsPath}, {LieDownPath}, {GetUpPath} (standing eye {eye} m)";
    }

    static PoseBeat Beat(string name, float duration, Vector2 offset, float height, float pitch, float yaw, float hold) => new()
    {
        Name = name, Duration = duration, Ease = Ease.InOutSine, Offset = offset, Height = height, Pitch = pitch, Yaw = yaw, FovScale = 1f, Hold = hold,
    };

    // One smooth path with a little hand-held wander, like the death sequence.
    static PoseSequence Sequence(string path, float endAfter, PoseBeat[] beats)
    {
        var sequence = AssetDatabase.LoadAssetAtPath<PoseSequence>(path);
        if (sequence == null)
        {
            sequence = ScriptableObject.CreateInstance<PoseSequence>();
            AssetDatabase.CreateAsset(sequence, path);
        }
        var so = new SerializedObject(sequence);
        so.FindProperty("_lowerHands").boolValue = true;
        so.FindProperty("_continuous").boolValue = true;
        so.FindProperty("_handheldAngles").vector3Value = new Vector3(0.5f, 0.5f, 0.4f);
        so.FindProperty("_handheldOffset").floatValue = 0.008f;
        so.FindProperty("_handheldSpeed").floatValue = 0.3f;
        so.FindProperty("_endAfter").floatValue = endAfter;
        SerializedProperty list = so.FindProperty("_beats");
        list.arraySize = beats.Length;
        for (int i = 0; i < beats.Length; i++)
        {
            SerializedProperty beat = list.GetArrayElementAtIndex(i);
            beat.FindPropertyRelative("Name").stringValue = beats[i].Name;
            beat.FindPropertyRelative("Duration").floatValue = beats[i].Duration;
            beat.FindPropertyRelative("Ease").intValue = (int)beats[i].Ease;
            beat.FindPropertyRelative("Height").floatValue = beats[i].Height;
            beat.FindPropertyRelative("Offset").vector2Value = beats[i].Offset;
            beat.FindPropertyRelative("Pitch").floatValue = beats[i].Pitch;
            beat.FindPropertyRelative("Yaw").floatValue = beats[i].Yaw;
            beat.FindPropertyRelative("Roll").floatValue = beats[i].Roll;
            beat.FindPropertyRelative("FovScale").floatValue = beats[i].FovScale;
            beat.FindPropertyRelative("Hold").floatValue = beats[i].Hold;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(sequence);
        return sequence;
    }

    // ---- HUD ----

    public static string Hud()
    {
        MenuStyle style = AssetDatabase.LoadAssetAtPath<MenuStyle>(StylePath) ?? throw new Exception("no " + StylePath + " (run SaveSetup.Assets)");
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform old = root.transform.Find("SleepScreen");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            Transform pause = root.transform.Find("PauseMenu");

            RectTransform node = Ui("SleepScreen", root.transform);
            Stretch(node);
            if (pause != null)
                node.SetSiblingIndex(pause.GetSiblingIndex());
            var canvas = node.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = Sorting;
            node.gameObject.AddComponent<GraphicRaycaster>();
            // The HUD fades out on death; this doesn't follow it.
            node.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;

            Image darkness = Fill("Darkness", node, Color.black, false);
            TMP_Text asleepTime = Text("AsleepTime", node, style.TitleFont, 150f, TextAlignmentOptions.Center, style.Text);
            Pin(asleepTime.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(700f, 180f));
            TMP_Text asleepDay = Text("AsleepDay", node, style.Font, 40f, TextAlignmentOptions.Center, style.InfoText);
            Pin(asleepDay.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(700f, 60f));

            RectTransform chooser = Ui("Chooser", node);
            Stretch(chooser);
            Fill("Backdrop", chooser, style.Backdrop, true);
            Image panel = Strip("Panel", chooser, style, style.Panel);
            Pin(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 560f));
            TMP_Text title = Text("Title", panel.transform, style.Font, 36f, TextAlignmentOptions.Center, style.Tape);
            title.text = "SLEEP UNTIL";
            Pin(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(700f, 50f));
            TMP_Text time = Text("Time", panel.transform, style.TitleFont, 170f, TextAlignmentOptions.Center, style.Text);
            time.text = "07:00";
            Pin(time.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(460f, 190f));
            Button earlier = Btn("Earlier", "-1 H", panel.transform, style, 30f);
            Pin((RectTransform)earlier.transform, new Vector2(0.5f, 1f), new Vector2(-300f, -160f), new Vector2(130f, 70f), new Vector2(0.5f, 1f));
            Button later = Btn("Later", "+1 H", panel.transform, style, 30f);
            Pin((RectTransform)later.transform, new Vector2(0.5f, 1f), new Vector2(300f, -160f), new Vector2(130f, 70f), new Vector2(0.5f, 1f));
            TMP_Text detail = Text("Detail", panel.transform, style.Font, 30f, TextAlignmentOptions.Center, style.Text);
            Pin(detail.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -300f), new Vector2(700f, 44f));
            TMP_Text limit = Text("Limit", panel.transform, style.BodyFont, 24f, TextAlignmentOptions.Center, style.InfoText);
            Pin(limit.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -346f), new Vector2(700f, 36f));
            Button sleep = Btn("Sleep", "SLEEP", panel.transform, style, 34f);
            Pin((RectTransform)sleep.transform, new Vector2(0.5f, 0f), new Vector2(-150f, 40f), new Vector2(270f, 80f), new Vector2(0.5f, 0f));
            Button cancel = Btn("Cancel", "CANCEL", panel.transform, style, 34f);
            Pin((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(150f, 40f), new Vector2(270f, 80f), new Vector2(0.5f, 0f));
            Navigate(earlier, right: later, down: sleep);
            Navigate(later, left: earlier, down: cancel);
            Navigate(sleep, up: earlier, right: cancel);
            Navigate(cancel, up: later, left: sleep);

            TMP_Text message = Text("Message", node, style.Font, 34f, TextAlignmentOptions.Center, style.Text);
            Pin(message.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -200f), new Vector2(1400f, 60f));

            var sounds = node.gameObject.AddComponent<MenuSounds>();
            SetRef(sounds, "_style", style);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule), typeof(MenuSelectionKeeper));
            events.transform.SetParent(node, false);
            events.SetActive(false);

            var screen = node.gameObject.AddComponent<SleepScreen>();
            var so = new SerializedObject(screen);
            Set(so, "_eventSystem", events);
            Set(so, "_chooser", chooser.gameObject);
            Set(so, "_wakeTime", time);
            Set(so, "_detail", detail);
            Set(so, "_limit", limit);
            Set(so, "_earlier", earlier);
            Set(so, "_later", later);
            Set(so, "_sleep", sleep);
            Set(so, "_cancel", cancel);
            Set(so, "_darkness", darkness);
            Set(so, "_asleepTime", asleepTime);
            Set(so, "_asleepDay", asleepDay);
            Set(so, "_message", message);
            Set(so, "_sounds", sounds);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            return $"HUD.prefab: SleepScreen (own canvas, sorting {Sorting}: wake-up clock, darkness, asleep clock, message)";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- WorldMap ----

    public static string World()
    {
        // From disk: the open copy may hold editor noise from Play.
        Scene scene = EditorSceneManager.OpenScene(WorldMapPath, OpenSceneMode.Single);
        var log = new List<string>();
        GameLifetimeScope scope = Object.FindAnyObjectByType<GameLifetimeScope>() ?? throw new Exception("WorldMap has no GameLifetimeScope");
        var installer = scope.GetComponent<SleepInstaller>();
        if (installer == null)
        {
            installer = scope.gameObject.AddComponent<SleepInstaller>();
            log.Add("SleepInstaller added");
        }
        SetRef(installer, "_settings", AssetDatabase.LoadAssetAtPath<SleepSettings>(SettingsPath) ?? throw new Exception("run SleepSetup.Assets first"));

        Transform house = GameObject.Find(HousePath)?.transform ?? throw new Exception("no " + HousePath);
        GameObject interior = GameObject.Find(InteriorName);
        if (interior != null)
            Object.DestroyImmediate(interior);
        interior = new GameObject(InteriorName);
        interior.transform.SetPositionAndRotation(house.position, house.rotation);

        var bed = new GameObject("Bed");
        bed.transform.SetParent(interior.transform, false);
        bed.transform.localPosition = BedLocalPosition;
        bed.transform.localRotation = Quaternion.Euler(0f, BedLocalYaw, 0f);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(BedModelPath) ?? throw new Exception("no " + BedModelPath));
        model.name = "Model";
        model.transform.SetParent(bed.transform, false);
        // The model has its pillow at -z; the anchor wants it at +z.
        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var anchor = new GameObject("Anchor").transform;
        anchor.SetParent(bed.transform, false);
        var component = bed.AddComponent<Bed>();
        SetRef(component, "_anchor", anchor);
        log.Add($"bed at house-local {BedLocalPosition} (world {bed.transform.position:F1})");

        foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "Door")
                continue;
            t.localRotation = Quaternion.Euler(0f, DoorOpenYaw, 0f);
            // The open leaf still narrows the doorway: it stops nothing.
            if (t.TryGetComponent(out Collider leaf))
                leaf.enabled = false;
            log.Add($"door open {DoorOpenYaw} degrees, its collider off");
        }

        installer.CollectBeds();
        EditorUtility.SetDirty(installer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return "WorldMap: " + string.Join(", ", log);
    }

    // ---- helpers (as AgentScripts/Save/SaveSetup) ----

    static void Navigate(Button button, Button up = null, Button down = null, Button left = null, Button right = null)
    {
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down, selectOnLeft = left, selectOnRight = right };
    }

    static Button Btn(string name, string label, Transform parent, MenuStyle style, float size)
    {
        Image image = Strip(name, parent, style, Color.white);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.colors = style.Colors();
        image.gameObject.AddComponent<MenuButton>();
        TMP_Text text = Text("Label", image.transform, style.Font, size, TextAlignmentOptions.Center, style.Text);
        Stretch(text.rectTransform);
        text.text = label;
        return button;
    }

    static Image Strip(string name, Transform parent, MenuStyle style, Color color)
    {
        RectTransform rect = Ui(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = style.ButtonSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = style.CornerScale;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Image Fill(string name, Transform parent, Color color, bool raycast)
    {
        RectTransform rect = Ui(name, parent);
        Stretch(rect);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = raycast;
        return image;
    }

    static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rect = Ui(name, parent);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        text.text = string.Empty;
        return text;
    }

    static RectTransform Ui(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
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

    static void Pin(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot ?? anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
