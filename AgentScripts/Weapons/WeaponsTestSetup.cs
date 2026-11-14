using System;
using System.IO;
using System.Linq;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.Debugging;
using MB13.Aipocalypse.Core.EquipmentSystem;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Builds Scenes/Prototypes/WeaponsTest.unity: grid ground, the Player and HUD (with the dev gun readout), three tables of
// guns (with a backpack and the ammo crate), two tables of gun parts, a loot crate, and a range down +Z with target
// dummies at 10-100 m and a backstop.
// Built in an additive scene and closed again, so the open scene (WorldMap) is never touched. Safe to re-run.
// Run: unity command script run --file AgentScripts/Weapons/WeaponsTestSetup.cs --entry WeaponsTestSetup.Build
public static class WeaponsTestSetup
{
    const string ScenePath = "Assets/_Game/Scenes/Prototypes/WeaponsTest.unity";
    const string AssetsDir = "Assets/_Game/Scenes/Prototypes/WeaponsTest";
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string TablePath = "Assets/_StoreAssets/Env/Animpic/POLY-SurvivalWorkshop/Models/Interiors/SM_Table.fbx";
    const string AmmoCratePath = "Assets/_Game/Content/Props/AmmoCrate/AmmoCrate.prefab";
    const string BackpackPath = "Assets/_Game/Content/Items/Backpack/Backpack.prefab";
    const string LootCratePath = "Assets/_Game/Content/Props/LootCrate/LootCrate.prefab";
    const string FontPath = "Assets/_Game/Data/UI/Fonts/SairaBold.asset";
    const string Weapons = "Assets/_Game/Content/Weapons/";
    const string Parts = "Assets/_Game/Content/Weapons/_Shared/Attachments/";

    // Three tables of guns: the five built first, then the block-outs (GunBlockoutSetup).
    static readonly string[][] GunTables =
    {
        new[] { "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel" },
        new[] { "AK74M", "M4", "M1Carbine", "FAL", "HuntingRifle", "RPD" },
        new[] { "Glock17", "DesertEagle", "Uzi", "P90", "AA12" },
    };
    static readonly string[] PartNames =
    {
        "RedDot", "Holo", "Scope2x", "Scope4x", "PistolRedDot", "Compensator", "Suppressor", "PistolSuppressor",
        "Choke", "VerticalGrip", "AngledGrip", "Laser", "Flashlight", "PistolLight", "RifleMag", "PistolMag",
    };
    static readonly int[] Distances = { 10, 25, 50, 75, 100 };

    public static string Build()
    {
        Directory.CreateDirectory(AssetsDir);
        Material ground = GridMaterial("Grid_Ground", new Vector2(60f, 140f));
        Material block = Lit("Block", new Color(0.55f, 0.57f, 0.6f));
        Material target = Lit("Target", new Color(0.85f, 0.42f, 0.18f));
        Material post = Lit("Post", new Color(0.3f, 0.3f, 0.32f));

        Scene active = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        try
        {
            SceneManager.SetActiveScene(scene);
            Lighting();

            var environment = new GameObject("Environment").transform;
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Ground";
            floor.transform.SetParent(environment, false);
            floor.transform.SetPositionAndRotation(new Vector3(0f, 0f, 50f), Quaternion.identity);
            floor.transform.localScale = new Vector3(6f, 1f, 14f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = ground;
            floor.isStatic = true;

            Instance(PlayerPath, null, new Vector3(0f, 0.05f, -4f), Quaternion.identity);
            GameObject hud = Instance(HudPath, null, Vector3.zero, Quaternion.identity);
            Readout(hud.transform, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath));

            var armoury = new GameObject("Armoury").transform;
            float top = 0f;
            for (int t = 0; t < GunTables.Length; t++)
            {
                float x = -3.2f * (t + 1);
                top = Table(armoury, t == 0 ? "GunsTable" : "GunsTable" + (t + 1), new Vector3(x, 0f, 0f));
                string[] guns = GunTables[t];
                for (int i = 0; i < guns.Length; i++)
                    ShowWorldSide(Instance($"{Weapons}{guns[i]}/{guns[i]}.prefab", armoury,
                        new Vector3(x + (i - (guns.Length - 1) * 0.5f) * 0.34f, top + 0.03f, 0f), Quaternion.identity));
            }
            // The new calibres aren't in the starting rounds or the ammo crate: full boxes on the floor in front of their guns.
            AmmoBox(armoury, "HeavyAmmoBox", 60, new Vector3(-6.6f, 0.02f, -0.8f));
            AmmoBox(armoury, "HeavyAmmoBox", 60, new Vector3(-6.1f, 0.02f, -0.8f));
            AmmoBox(armoury, "MagnumAmmoBox", 42, new Vector3(-9.6f, 0.02f, -0.8f));
            float edge = -3.2f * GunTables.Length - 1.4f;
            ShowWorldSide(Instance(BackpackPath, armoury, new Vector3(edge, top, 0.35f), Quaternion.Euler(0f, 30f, 0f)));
            Instance(AmmoCratePath, armoury, new Vector3(edge - 0.7f, 0f, 0.4f), Quaternion.Euler(0f, 90f, 0f));
            // A loot crate rolls a part from its table (CrateLoot) when first opened; the scene scope injects it (the
            // random stream, the loot marker).
            GameObject crate = Instance(LootCratePath, armoury, new Vector3(5.6f, 0f, 0.4f), Quaternion.Euler(0f, -90f, 0f));
            SceneScope(crate);

            for (int t = 0; t < 2; t++)
            {
                float x = t == 0 ? 0f : 3.2f;
                top = Table(armoury, "PartsTable" + (t + 1), new Vector3(x, 0f, 0f));
                for (int i = 0; i < 8; i++)
                {
                    int index = t * 8 + i;
                    var position = new Vector3(x - 0.7f + (i % 4) * 0.46f, top, i < 4 ? -0.18f : 0.2f);
                    ShowWorldSide(Instance($"{Parts}{PartNames[index]}/{PartNames[index]}.prefab", armoury, position, Quaternion.Euler(0f, -90f, 0f)));
                }
            }

            var range = new GameObject("Range").transform;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            foreach (int distance in Distances)
            {
                Dummy(range, $"Dummy_{distance}m", new Vector3(0f, 0f, distance), target, post);
                Board(range, distance, font, post);
            }
            Dummy(range, "Dummy_10m_Left", new Vector3(-2.5f, 0f, 10f), target, post);
            Dummy(range, "Dummy_10m_Right", new Vector3(2.5f, 0f, 10f), target, post);
            Box(range, "Backstop", new Vector3(0f, 4f, 115f), new Vector3(40f, 8f, 1f), block);
            Box(range, "FiringLine", new Vector3(0f, 0.01f, 3f), new Vector3(12f, 0.02f, 0.08f), post);

            EditorSceneManager.SaveScene(scene, ScenePath);
            return $"saved {ScenePath}: {GunTables.Sum(t => t.Length)} guns, {PartNames.Length} parts, a loot crate, {Distances.Length + 2} dummies";
        }
        finally
        {
            if (active.IsValid())
                SceneManager.SetActiveScene(active);
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    static void AmmoBox(Transform parent, string name, int count, Vector3 position)
    {
        GameObject box = Instance($"{Weapons}_Shared/Ammo/{name}.prefab", parent, position, Quaternion.identity);
        var so = new SerializedObject(box.GetComponent<WorldItem>());
        so.FindProperty("_count").intValue = count;
        so.ApplyModifiedPropertiesWithoutUndo();
        ShowWorldSide(box);
    }

    // A prototype scene scope (not strict, no installers): it injects the listed scene objects.
    static void SceneScope(params GameObject[] inject)
    {
        var scope = new GameObject("SceneScope").AddComponent<GameLifetimeScope>();
        var so = new SerializedObject(scope);
        SerializedProperty list = so.FindProperty("autoInjectGameObjects");
        list.arraySize = inject.Length;
        for (int i = 0; i < inject.Length; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = inject[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The dev readout (GunReadout) top left, under the HUD so its scope injects it, drawn below the inventory screen.
    static void Readout(Transform hud, TMP_FontAsset font)
    {
        var root = new GameObject("GunReadout", typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(hud, false);
        Transform inventory = hud.Find("Inventory");
        root.SetSiblingIndex(inventory != null ? inventory.GetSiblingIndex() : hud.childCount - 1);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(24f, -24f);
        root.sizeDelta = new Vector2(520f, 640f);
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.55f);
        background.raycastTarget = false;

        var text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(root, false);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(14f, 12f);
        text.rectTransform.offsetMax = new Vector2(-14f, -12f);
        text.font = font;
        text.fontSize = 17f;
        text.color = new Color(0.92f, 0.93f, 0.95f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        var readout = root.gameObject.AddComponent<GunReadout>();
        var so = new SerializedObject(readout);
        so.FindProperty("_text").objectReferenceValue = text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Lighting()
    {
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.4f;
        sun.color = new Color(1f, 0.96f, 0.9f);
        sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.68f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.55f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.31f, 0.3f);
        RenderSettings.fog = false;
    }

    static GameObject Instance(string path, Transform parent, Vector3 position, Quaternion rotation)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new Exception("missing " + path);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, SceneManager.GetActiveScene());
        if (parent != null)
            instance.transform.SetParent(parent, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        return instance;
    }

    // In the editor the pickup shows as play will: only its World child (WorldItem does the same on Awake).
    static void ShowWorldSide(GameObject item)
    {
        GameObject world = item.TryGetComponent(out WorldItem worldItem) ? worldItem.WorldModel : null;
        if (world == null)
            return;
        foreach (Transform child in item.transform)
            child.gameObject.SetActive(child.gameObject == world);
    }

    // A table; returns the height of its top.
    static float Table(Transform parent, string name, Vector3 position)
    {
        GameObject table = Instance(TablePath, parent, position, Quaternion.identity);
        table.name = name;
        table.isStatic = true;
        Bounds bounds = table.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        if (table.GetComponentInChildren<Collider>() == null)
            table.AddComponent<BoxCollider>();
        return bounds.max.y;
    }

    static void Dummy(Transform parent, string name, Vector3 position, Material body, Material post)
    {
        var root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shape.name = "Body";
        shape.transform.SetParent(root.transform, false);
        shape.transform.localPosition = new Vector3(0f, 1f, 0f);
        shape.transform.localScale = new Vector3(0.6f, 0.8f, 0.6f);
        shape.GetComponent<MeshRenderer>().sharedMaterial = body;
        Object.DestroyImmediate(shape.GetComponent<Collider>());
        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 1f, 0f);
        collider.height = 1.6f;
        collider.radius = 0.3f;
        var dummy = root.AddComponent<TargetDummy>();
        var so = new SerializedObject(dummy);
        so.FindProperty("_body").objectReferenceValue = root.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
        Box(parent, name + "_Base", position + new Vector3(0f, 0.05f, 0f), new Vector3(0.7f, 0.1f, 0.7f), post);
    }

    // The distance on a post beside the lane, sized to stay readable from the firing line.
    static void Board(Transform parent, int distance, TMP_FontAsset font, Material post)
    {
        float size = 0.5f + distance * 0.012f;
        Box(parent, $"Post_{distance}m", new Vector3(1.6f, size + 0.6f, distance), new Vector3(0.08f, (size + 0.6f) * 2f, 0.08f), post);
        var label = new GameObject($"Label_{distance}m").AddComponent<TextMeshPro>();
        label.transform.SetParent(parent, false);
        label.transform.position = new Vector3(1.6f, size * 2f + 1.2f, distance - 0.05f);
        label.font = font;
        label.fontSize = size * 10f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.rectTransform.sizeDelta = new Vector2(size * 4f, size * 1.6f);
        label.text = distance + " m";
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        box.isStatic = true;
    }

    static Material Lit(string name, Color color)
    {
        string path = $"{AssetsDir}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }

    // A first-party 1 m grid (lighter line every 5 m), tiled over the ground.
    static Material GridMaterial(string name, Vector2 tiling)
    {
        string texturePath = $"{AssetsDir}/Grid.png";
        const int size = 256;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            bool line = x < 3 || y < 3;
            pixels[y * size + x] = line ? new Color32(78, 82, 88, 255) : new Color32(118, 122, 128, 255);
        }
        texture.SetPixels32(pixels);
        File.WriteAllBytes(texturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(texturePath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.anisoLevel = 8;
        importer.SaveAndReimport();

        Material material = Lit(name, Color.white);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
        material.SetTextureScale("_BaseMap", tiling);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        return material;
    }
}
