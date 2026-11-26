using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.ForecastSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds the forecast board: painted mark and period textures (Layer Lab picto icons composited on the CPU, worn like old
// paint), their alpha-clipped materials, Content/Structures/ForecastBoard/ForecastBoard.prefab (Highlands log frame, three
// Lakefell-wood plaques on ropes), Data/Forecast/ForecastSettings, and in WorldMap the ForecastInstaller plus the board in
// front of the house, right side ("Forecast Board" scene root: a Landmarks rebuild doesn't remove it). Safe to re-run.
// Run: unity command script run --file AgentScripts/Forecast/ForecastBoardSetup.cs --entry ForecastBoardSetup.All
public static class ForecastBoardSetup
{
    const string Root = "Assets/_Game/Content/Structures/ForecastBoard";
    const string PrefabPath = Root + "/ForecastBoard.prefab";
    const string SettingsPath = "Assets/_Game/Data/Forecast/ForecastSettings.asset";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string HousePath = "Environment/Landmarks/Home Base/House_1";
    const string SceneRootName = "Forecast Board";
    const string Icons = "Assets/_StoreAssets/UI/Layer Lab/2D Icons-SimplePictoPack/Icons/PNG/512/Pictoicon_";
    const string LogPath = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/Forest/Log_1.prefab";
    const string PlaqueWoodPath = "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Materials/Modules/Wood_Trim.mat";
    const int Res = 512;

    // Frame: two log posts 3 m apart, the crossbar log on top.
    const float BeamY = 2.05f, BeamThick = 0.2f, PostThick = 0.24f, PostX = 1.5f, BeamLength = 3.45f;
    // Plaques: two planks each, hung under the crossbar 0.9 m apart.
    const float PlaqueW = 0.66f, PlaqueH = 0.6f, PlaqueGap = 0.9f, RopeDrop = 0.18f;
    // Where the board stands in the house's space (x right, z towards the gate; clear of the robots' Spot_Garden and the
    // dog's Visit_Garden) and the point its plaques face (the path from the door to the gate).
    static readonly Vector3 BoardLocal = new(10.5f, 0f, 5f);
    static readonly Vector3 FacingLocal = new(2.3f, 0f, 10f);

    static readonly Color SwordRed = new(0.8f, 0.16f, 0.1f);
    static readonly Color SunColor = new(1f, 0.72f, 0.18f);
    static readonly Color MoonColor = new(0.78f, 0.87f, 1f);
    static readonly Color StarColor = new(0.72f, 0.66f, 1f);
    static readonly Color RopeColor = new(0.62f, 0.5f, 0.32f);

    static readonly NightPeriod[] Periods = { NightPeriod.Evening, NightPeriod.Midnight, NightPeriod.LateNight };

    public static string All() => string.Join("\n", Textures(), Prefab(), Data(), World());

    // ---- textures and materials ----

    readonly struct Mark
    {
        public readonly string Icon; public readonly Vector2 At; public readonly float Size, Angle; public readonly Color Color;
        public Mark(string icon, Vector2 at, float size, Color color, float angle = 0f) { Icon = icon; At = at; Size = size; Color = color; Angle = angle; }
    }

    public static string Textures()
    {
        Folder(Root + "/Textures");
        Folder(Root + "/Materials");
        var log = new List<string>();
        // Fight marks over the whole plaque face, below its period icon. Small and medium are the same crossed swords.
        var below = new Vector2(0.03f, -0.07f);
        log.Add(Bake("Mark_Small", new[] { new Mark("battle", below, 0.18f, SwordRed, -3f) }, PlaqueW, PlaqueH, 1234, 0.27f));
        log.Add(Bake("Mark_Medium", new[] { new Mark("battle", below, 0.3f, SwordRed, -3f) }, PlaqueW, PlaqueH, 1311, 0.27f));
        log.Add(Bake("Mark_Boss", new[] { new Mark("boss", below, 0.34f, SwordRed) }, PlaqueW, PlaqueH, 1388, 0.27f));
        // Period icons, lightly worn so they stay readable.
        log.Add(Bake("Period_Evening", new[] { new Mark("sun", new Vector2(0f, -0.06f), 0.5f, SunColor) }, 0.6f, 0.6f, 905, 0.12f, Sunset));
        log.Add(Bake("Period_Midnight", new[] { new Mark("moon", Vector2.zero, 0.46f, MoonColor) }, 0.6f, 0.6f, 901, 0.12f));
        log.Add(Bake("Period_LateNight", new[] { new Mark("star", Vector2.zero, 0.46f, StarColor) }, 0.6f, 0.6f, 902, 0.12f));

        var rope = LoadOrCreate<Material>(Root + "/Materials/Rope.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
        rope.SetColor("_BaseColor", RopeColor);
        rope.SetFloat("_Smoothness", 0.1f);
        Save(rope);
        return "textures and materials: " + string.Join(", ", log);
    }

    // Composites the marks into a w x h (metres) area, wears the paint, writes a PNG and its alpha-clipped material.
    static string Bake(string name, Mark[] marks, float w, float h, int seed, float wear, Action<Color32[], Color> after = null)
    {
        var px = new Color32[Res * Res];
        foreach (Mark mark in marks)
        {
            var icon = new Texture2D(2, 2);
            icon.LoadImage(File.ReadAllBytes(Icons + mark.Icon + ".png"));
            float cos = Mathf.Cos(-mark.Angle * Mathf.Deg2Rad), sin = Mathf.Sin(-mark.Angle * Mathf.Deg2Rad);
            for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
            {
                var p = new Vector2(((x + 0.5f) / Res - 0.5f) * w, ((y + 0.5f) / Res - 0.5f) * h) - mark.At;
                float u = (p.x * cos - p.y * sin) / mark.Size + 0.5f, v = (p.x * sin + p.y * cos) / mark.Size + 0.5f;
                if (u < 0f || u > 1f || v < 0f || v > 1f)
                    continue;
                float a = icon.GetPixelBilinear(u, v).a;
                int i = y * Res + x;
                if (a * 255f <= px[i].a)
                    continue;
                px[i] = new Color(mark.Color.r, mark.Color.g, mark.Color.b, a);
            }
            Object.DestroyImmediate(icon);
        }
        after?.Invoke(px, marks[0].Color);
        Wear(px, seed, wear);

        string path = $"{Root}/Textures/{name}.png";
        var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = Res;
        importer.SaveAndReimport();

        var mat = LoadOrCreate<Material>($"{Root}/Materials/{name}.mat", () => new Material(Shader.Find("Universal Render Pipeline/Lit")));
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.5f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetFloat("_Smoothness", 0.15f);
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        Save(mat);
        return name;
    }

    // Evening: the sun cut by a horizon line, so it reads as a setting sun rather than noon.
    static void Sunset(Color32[] px, Color color)
    {
        Color32 c = color;
        for (int y = 0; y < Res; y++)
        for (int x = 0; x < Res; x++)
        {
            float u = x / (float)Res, v = y / (float)Res;
            int i = y * Res + x;
            if (v < 0.47f)
                px[i].a = 0;
            if (v > 0.4f && v < 0.45f && u > 0.06f && u < 0.94f)
                px[i] = c;
        }
    }

    // Blotchy, specked, grain-streaked loss of paint, like the mock-ups.
    static void Wear(Color32[] px, int seed, float cut)
    {
        float ox = seed * 0.37f, oy = seed * 0.71f;
        for (int y = 0; y < Res; y++)
        for (int x = 0; x < Res; x++)
        {
            int i = y * Res + x;
            if (px[i].a == 0)
                continue;
            float u = x / (float)Res, v = y / (float)Res;
            float blotch = Mathf.PerlinNoise(ox + u * 6f, oy + v * 6f);
            float speck = Mathf.PerlinNoise(ox + u * 60f, oy + v * 60f);
            float grain = Mathf.PerlinNoise(ox + u * 2.5f, oy + v * 90f);
            float keep = blotch * 0.55f + speck * 0.3f + grain * 0.35f;
            float t = Mathf.Clamp01((keep - cut) / 0.13f);
            float tone = 0.85f + 0.25f * speck;
            px[i].r = (byte)Mathf.Clamp(px[i].r * tone, 0, 255);
            px[i].g = (byte)Mathf.Clamp(px[i].g * tone, 0, 255);
            px[i].b = (byte)Mathf.Clamp(px[i].b * tone, 0, 255);
            px[i].a = (byte)(px[i].a * t * t * (3f - 2f * t));
        }
    }

    // ---- prefab ----

    public static string Prefab()
    {
        var wood = AssetDatabase.LoadAssetAtPath<Material>(PlaqueWoodPath) ?? throw new Exception("no " + PlaqueWoodPath);
        var rope = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Rope.mat") ?? throw new Exception("run Textures first");
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("ForecastBoard");
            SceneManager.MoveGameObjectToScene(root, preview);
            Transform t = root.transform;

            var frame = new GameObject("Frame").transform;
            frame.SetParent(t, false);
            FitLog(frame, preview, "Post_Left", Vector3.up, BeamY + 0.22f, PostThick, new Vector3(-PostX, 0f, 0f), false);
            FitLog(frame, preview, "Post_Right", Vector3.up, BeamY + 0.22f, PostThick, new Vector3(PostX, 0f, 0f), true);
            FitLog(frame, preview, "Crossbar", Vector3.right, BeamLength, BeamThick, new Vector3(0f, BeamY, 0f), false);

            float beamBottom = BeamY - BeamThick * 0.5f;
            var plaques = new ForecastBoard.Plaque[Periods.Length];
            for (int i = 0; i < Periods.Length; i++)
            {
                float x = (i - 1) * PlaqueGap;
                var p = new GameObject("Plaque_" + Periods[i]).transform;
                p.SetParent(t, false);
                p.localPosition = new Vector3(x, beamBottom - RopeDrop - PlaqueH * 0.5f, -0.02f);
                // A little hand-hung skew.
                p.localRotation = Quaternion.Euler(0f, (i - 1) * 2f, i == 1 ? -1.5f : 1f);
                for (int k = 0; k < 2; k++)
                {
                    Transform plank = Primitive(PrimitiveType.Cube, "Plank_" + (k + 1), p, preview, wood).transform;
                    plank.localPosition = new Vector3(0f, k == 0 ? PlaqueH * 0.25f + 0.005f : -PlaqueH * 0.25f - 0.005f, 0f);
                    plank.localRotation = Quaternion.Euler(0f, 0f, k == 0 ? 0.6f : -0.8f);
                    plank.localScale = new Vector3(PlaqueW, PlaqueH * 0.5f, 0.045f);
                }
                var period = Primitive(PrimitiveType.Quad, "Period", p, preview, Load<Material>($"{Root}/Materials/Period_{Periods[i]}.mat"));
                period.transform.localPosition = new Vector3(-0.215f, 0.175f, -0.0236f);
                period.transform.localScale = new Vector3(0.2f, 0.2f, 1f);
                var mark = Primitive(PrimitiveType.Quad, "Mark", p, preview, Load<Material>($"{Root}/Materials/Mark_Small.mat"));
                mark.transform.localPosition = new Vector3(0f, 0f, -0.0235f);
                mark.transform.localScale = new Vector3(PlaqueW, PlaqueH, 1f);
                foreach (GameObject paint in new[] { period, mark })
                    paint.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                plaques[i] = new ForecastBoard.Plaque { Period = Periods[i], Mark = mark.GetComponent<Renderer>() };

                float rx = PlaqueW * 0.5f - 0.08f;
                Rope(t, p, preview, rope, new Vector3(x - rx, beamBottom + 0.02f, 0f), new Vector3(-rx, PlaqueH * 0.5f - 0.02f, 0f), "Rope_L" + i);
                Rope(t, p, preview, rope, new Vector3(x + rx, beamBottom + 0.02f, 0f), new Vector3(rx, PlaqueH * 0.5f - 0.02f, 0f), "Rope_R" + i);
            }

            var board = root.AddComponent<ForecastBoard>();
            SetField(board, "_plaques", plaques);
            SetField(board, "_marks", new Dictionary<FightSize, Material>
            {
                { FightSize.Small, Load<Material>(Root + "/Materials/Mark_Small.mat") },
                { FightSize.Medium, Load<Material>(Root + "/Materials/Mark_Medium.mat") },
                { FightSize.Boss, Load<Material>(Root + "/Materials/Mark_Boss.mat") },
            });
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            return "prefab: " + PrefabPath;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // A log prefab turned so its long axis runs along `axis`, that axis scaled to `length` and its cross-section to
    // `thick`; posts stand on y = at.y, the crossbar is centred on `at`.
    static void FitLog(Transform parent, Scene scene, string name, Vector3 axis, float length, float thick, Vector3 at, bool mirror)
    {
        var holder = new GameObject(name).transform;
        SceneManager.MoveGameObjectToScene(holder.gameObject, scene);
        var log = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(LogPath), scene);
        log.transform.SetParent(holder, false);
        Vector3 size = Bounds(log, holder).size;
        int longest = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        bool upright = axis == Vector3.up;
        Vector3 from = longest == 0 ? Vector3.right : longest == 1 ? Vector3.up : Vector3.forward;
        log.transform.localRotation = Quaternion.FromToRotation(from, upright ? Vector3.up : Vector3.right);
        if (mirror)
            log.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * log.transform.localRotation;
        size = Bounds(log, holder).size;
        float c = thick / (upright ? Mathf.Max(size.x, size.z) : Mathf.Max(size.y, size.z));
        holder.localScale = upright ? new Vector3(c, length / size.y, c) : new Vector3(length / size.x, c, c);
        // The holder is still a root at the origin, so world bounds are its parent-space bounds.
        Bounds b = Bounds(holder.gameObject, null);
        Vector3 offset = upright ? new Vector3(-b.center.x, -b.min.y, -b.center.z) : -b.center;
        holder.SetParent(parent, false);
        holder.localPosition = at + offset;
    }

    // Renderer bounds in `space` (world when null); LOD0 only.
    static Bounds Bounds(GameObject go, Transform space)
    {
        Renderer[] renderers = go.GetComponentsInChildren<LODGroup>().Length > 0
            ? go.GetComponentInChildren<LODGroup>().GetLODs()[0].renderers.Where(r => r != null).ToArray()
            : go.GetComponentsInChildren<MeshRenderer>();
        var b = new Bounds();
        bool first = true;
        foreach (Renderer r in renderers)
        {
            var rb = new Bounds(space != null ? space.InverseTransformPoint(r.bounds.center) : r.bounds.center, r.bounds.size);
            if (first) { b = rb; first = false; }
            else b.Encapsulate(rb);
        }
        return b;
    }

    static GameObject Primitive(PrimitiveType type, string name, Transform parent, Scene scene, Material material)
    {
        var go = GameObject.CreatePrimitive(type);
        SceneManager.MoveGameObjectToScene(go, scene);
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    // A straight rope from the crossbar (board space) into a plaque corner (plaque space), with a knot at the plaque.
    static void Rope(Transform board, Transform plaque, Scene scene, Material material, Vector3 top, Vector3 corner, string name)
    {
        Vector3 a = board.TransformPoint(top), b = plaque.TransformPoint(corner);
        Transform rope = Primitive(PrimitiveType.Cylinder, name, board, scene, material).transform;
        rope.position = (a + b) * 0.5f;
        rope.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
        rope.localScale = new Vector3(0.024f, (b - a).magnitude * 0.5f, 0.024f);
        Transform knot = Primitive(PrimitiveType.Sphere, name + "_Knot", board, scene, material).transform;
        knot.position = b + Vector3.up * 0.01f;
        knot.localScale = Vector3.one * 0.05f;
    }

    // ---- data ----

    public static string Data()
    {
        Folder("Assets/_Game/Data/Forecast");
        var settings = LoadOrCreate<ForecastSettings>(SettingsPath, ScriptableObject.CreateInstance<ForecastSettings>);
        Save(settings);
        return "data: " + SettingsPath + " (" + string.Join(", ", settings.Periods.Select(p => $"{p.Key} {p.Value.StartHour:00}-{p.Value.EndHour:00} {p.Value.Fight}")) + ")";
    }

    // ---- WorldMap ----

    public static string World()
    {
        Scene scene = EditorSceneManager.OpenScene(WorldMapPath, OpenSceneMode.Single);
        GameLifetimeScope scope = Object.FindAnyObjectByType<GameLifetimeScope>() ?? throw new Exception("WorldMap has no GameLifetimeScope");
        var installer = scope.GetComponent<ForecastInstaller>() ?? scope.gameObject.AddComponent<ForecastInstaller>();
        var so = new SerializedObject(installer);
        so.FindProperty("_settings").objectReferenceValue = Load<ForecastSettings>(SettingsPath);
        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == SceneRootName);
        if (old != null)
            Object.DestroyImmediate(old);
        Transform house = GameObject.Find(HousePath)?.transform ?? throw new Exception("no " + HousePath);
        var holder = new GameObject(SceneRootName);
        var board = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath), scene);
        board.transform.SetParent(holder.transform, false);

        Vector3 at = Ground(house.TransformPoint(BoardLocal));
        Vector3 toViewer = house.TransformPoint(FacingLocal) - at;
        toViewer.y = 0f;
        // The plaques' painted faces point down -z.
        board.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-toViewer.normalized));

        installer.CollectBoards();
        EditorUtility.SetDirty(installer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"WorldMap: ForecastInstaller, board at {at:F1} facing {toViewer.normalized:F2}";
    }

    static Vector3 Ground(Vector3 p)
    {
        Terrain terrain = Terrain.activeTerrains.FirstOrDefault(t =>
        {
            Vector3 o = t.transform.position, s = t.terrainData.size;
            return p.x >= o.x && p.z >= o.z && p.x <= o.x + s.x && p.z <= o.z + s.z;
        });
        if (terrain != null)
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
        return p;
    }

    // ---- helpers ----

    static void SetField(object target, string field, object value) =>
        (target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new Exception("no field " + field)).SetValue(target, value);

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("no " + path);

    static T LoadOrCreate<T>(string path, Func<T> create) where T : Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = create();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
