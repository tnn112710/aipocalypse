// World Map atmosphere: post-processing profile (clone of Komorebi's), sun, sky, fog. Run via `unity command run_script`.
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class Atmosphere
{
    const string Folder = "Assets/_Game/Data/Environment/TimeOfDay";
    const string KomorebiProfile = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Demo/Settings/KomorebiNature_Demo_Post-Processing.asset";
    // Low sun from the east-south-east: the home->bridge view is side-lit and the meadow's east view back-lit (glowing grass and leaf edges).
    const float SunPitch = 32f, SunYaw = 300f;

    public static object Report()
    {
        var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsByType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        return new
        {
            skybox = RenderSettings.skybox ? AssetDatabase.GetAssetPath(RenderSettings.skybox) + " / " + RenderSettings.skybox.shader.name : "none",
            ambientMode = RenderSettings.ambientMode.ToString(), ambientSky = RenderSettings.ambientSkyColor.ToString(), ambientEquator = RenderSettings.ambientEquatorColor.ToString(), ambientGround = RenderSettings.ambientGroundColor.ToString(), ambientIntensity = RenderSettings.ambientIntensity,
            fog = RenderSettings.fog, fogMode = RenderSettings.fogMode.ToString(), fogColor = RenderSettings.fogColor.ToString(), fogDensity = RenderSettings.fogDensity, fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance,
            sun = sun ? $"{sun.name} rot {sun.transform.eulerAngles} color {sun.color} intensity {sun.intensity} temp {sun.useColorTemperature}/{sun.colorTemperature} shadows {sun.shadows}" : "none",
            volumes = Object.FindObjectsByType<Volume>().Select(v => $"{v.name} global {v.isGlobal} profile {(v.sharedProfile ? AssetDatabase.GetAssetPath(v.sharedProfile) : "MISSING")}").ToArray(),
        };
    }

    // Clones Komorebi's demo post-processing profile and assigns it to the scene's global volume.
    public static object ClonePostProcessing()
    {
        string dest = Folder + "/Base_PostProcessing.asset";
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(dest) == null && !AssetDatabase.CopyAsset(KomorebiProfile, dest))
            throw new System.InvalidOperationException("Copy failed");
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(dest);
        var vol = Object.FindObjectsByType<Volume>().FirstOrDefault(v => v.isGlobal);
        if (vol == null)
        {
            var go = new GameObject("Post-Processing");
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
        }
        Undo.RecordObject(vol, "Assign post-processing");
        vol.sharedProfile = profile;
        vol.gameObject.name = "Post-Processing (Komorebi grade)";
        EditorUtility.SetDirty(vol);
        EditorSceneManager.MarkSceneDirty(vol.gameObject.scene);
        return new { volume = vol.name, profile = dest, components = profile.components.Select(c => c.GetType().Name + (c.active ? "" : " (off)")).ToArray() };
    }
    // Softer grade than the Komorebi demo (its contrast 50 / saturation +10 crush our cliffs and push grass to neon).
    // Keeps its warm split-tone (cool lift, warm gain) and white balance.
    public static object TunePost(float contrast, float saturation, float exposure, float bloomIntensity, float bloomThreshold, float vignette, float temperature)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Base_PostProcessing.asset");
        if (profile.TryGet<ColorAdjustments>(out var ca)) { ca.contrast.Override(contrast); ca.saturation.Override(saturation); ca.postExposure.Override(exposure); }
        if (profile.TryGet<Bloom>(out var bloom)) { bloom.intensity.Override(bloomIntensity); bloom.threshold.Override(bloomThreshold); }
        if (profile.TryGet<Vignette>(out var v)) v.intensity.Override(vignette);
        if (profile.TryGet<WhiteBalance>(out var wb)) wb.temperature.Override(temperature);
        if (profile.TryGet<ScreenSpaceLensFlare>(out var flare)) flare.active = false;   // screen-space flares read as noise in an FPS
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssetIfDirty(profile);
        return profile.components.Select(c => c.GetType().Name + (c.active ? "" : " (off)")).ToArray();
    }
    // Cozy late-morning light (values from the atmosphere research; Komorebi demo as the base).
    public static object Apply()
    {
        var scene = SceneManager.GetActiveScene();
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "/Base_PostProcessing.asset");
        if (profile.TryGet<WhiteBalance>(out var wb)) { wb.temperature.Override(10f); wb.tint.Override(2f); }
        if (profile.TryGet<ColorAdjustments>(out var ca)) { ca.contrast.Override(20f); ca.saturation.Override(2f); ca.postExposure.Override(0.1f); }
        if (profile.TryGet<Bloom>(out var bloom)) { bloom.threshold.Override(0.8f); bloom.intensity.Override(0.3f); bloom.scatter.Override(0.7f); bloom.tint.Override(new Color(1f, 0.958f, 0.902f)); }
        if (profile.TryGet<Vignette>(out var vg)) { vg.intensity.Override(0.25f); vg.smoothness.Override(0.4f); }
        EditorUtility.SetDirty(profile); AssetDatabase.SaveAssetIfDirty(profile);

        var sun = Object.FindObjectsByType<Light>().First(l => l.type == LightType.Directional);
        Undo.RecordObject(sun, "Sun"); Undo.RecordObject(sun.transform, "Sun");
        sun.transform.rotation = Quaternion.Euler(SunPitch, SunYaw, 0f);
        sun.useColorTemperature = true; sun.colorTemperature = 5000f;
        sun.color = new Color(1f, 0.97f, 0.92f);
        sun.intensity = 2.8f;
        sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.8f;
        sun.cookie = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Textures/Nature/CloudShadows_Cookie.png");
        var urpLight = sun.GetComponent<UniversalAdditionalLightData>();
        if (urpLight == null) urpLight = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
        urpLight.lightCookieSize = new Vector2(500, 500);
        var moverType = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Raygeas.CloudMover")).First(t => t != null);
        var mover = sun.GetComponent(moverType);
        if (mover == null) mover = sun.gameObject.AddComponent(moverType);
        var so = new SerializedObject(mover); so.FindProperty("speed").floatValue = 6f; so.ApplyModifiedPropertiesWithoutUndo();
        RenderSettings.sun = sun;

        string skyPath = Folder + "/Base_Skybox.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(skyPath) == null)
            AssetDatabase.CopyAsset("Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Materials/Nature/Skybox_1.mat", skyPath);
        var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
        sky.SetFloat("_Rotation", 17f);
        EditorUtility.SetDirty(sky); AssetDatabase.SaveAssetIfDirty(sky);
        RenderSettings.skybox = sky;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0011f;   // thinner than Komorebi: the Raygeas mountain shaders skip scene fog, so near layers must not out-haze the peaks
        RenderSettings.fogColor = new Color(0.58f, 0.76f, 0.94f);
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.64f, 0.73f, 0.86f);
        RenderSettings.ambientEquatorColor = new Color(0.56f, 0.57f, 0.50f);
        RenderSettings.ambientGroundColor = new Color(0.42f, 0.39f, 0.31f);
        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(scene);
        return new { sun = sun.transform.eulerAngles.ToString(), sky = skyPath, fog = RenderSettings.fogDensity };
    }

    const string KNP = "Assets/_StoreAssets/Env/Raygeas/Komorebi Nature/Assets/Prefabs/Particle Systems/";
    const string GLP = "Assets/_StoreAssets/Env/Raygeas/Grasslands - Stylized Nature/Assets/Prefabs/Particle Effects/";
    const string HLP = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Prefabs/FX/";
    const string BKP = "Assets/_StoreAssets/Env/BK/PureNature_Meadows/Prefabs/Fx/";
    const string KRP = "Assets/_StoreAssets/GameFeel/Kroma/Content/Prefabs/";
    const string SFN = "Assets/_StoreAssets/Env/SICS/Toon Fantasy Nature/Particles/";
    const string SEM = "Assets/_StoreAssets/Env/SICS/Toon Series/Toon Enchanted Meadow/Particles/";

    // Ambient particles under Environment/Atmosphere (rebuilt each run). Heights are sampled from the terrain.
    public static object Particles()
    {
        var scene = SceneManager.GetActiveScene();
        var env = scene.GetRootGameObjects().First(g => g.name == "Environment");
        var old = env.transform.Find("Atmosphere");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var root = new GameObject("Atmosphere").transform; root.SetParent(env.transform, false);
        Undo.RegisterCreatedObjectUndo(root.gameObject, "Atmosphere");
        var terrain = Terrain.activeTerrain;
        float G(float x, float z) => terrain.SampleHeight(new Vector3(x, 0, z));
        var rng = new System.Random(3);
        int n = 0;
        Transform Grp(string name) { var t = new GameObject(name).transform; t.SetParent(root, false); return t; }
        GameObject Put(Transform parent, string path, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
            go.transform.localScale = scale;
            foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            n++;
            return go;
        }
        // Edits a placed vendor system; recorded as prefab overrides, otherwise the next prefab reload reverts them.
        void Tune(GameObject go, System.Action<ParticleSystem> edit)
        {
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.cullingMode = ParticleSystemCullingMode.Pause;   // the SICS and Kroma systems keep simulating off screen
                edit?.Invoke(ps);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ps);
            }
        }
        var rays = Grp("God Rays");
        foreach (var (x, z) in new[] { (332f, 430f), (346f, 437f), (357f, 425f), (340f, 448f), (362f, 556f), (384f, 544f), (368f, 522f), (416f, 522f), (434f, 498f), (444f, 516f), (508f, 484f), (520f, 448f), (536f, 480f), (598f, 436f), (616f, 424f), (584f, 404f), (420f, 598f), (444f, 642f) })
            Put(rays, GLP + "GodRays.prefab", new Vector3(x, G(x, z) - 0.5f, z), new Vector3(SunPitch - 90, SunYaw, 0), Vector3.one * 1.3f);   // beam (local +Y) points at the sun
        foreach (var (x, z) in new[] { (214f, 660f), (236f, 646f), (248f, 668f), (300f, 720f), (420f, 720f), (470f, 700f) })
            Put(rays, HLP + "GodRays.prefab", new Vector3(x, G(x, z) - 0.5f, z), new Vector3(SunPitch - 90, SunYaw, 0), Vector3.one * 1.1f);
        var mist = Grp("Mist");
        foreach (var (x, z) in new[] { (344f, 435f), (371f, 541f), (431f, 512f), (521f, 471f), (599f, 421f), (432f, 620f), (233f, 658f), (397f, 713f) })
            Put(mist, KNP + "Fog_1.prefab", new Vector3(x, G(x, z) + 0.3f, z), new Vector3(0, rng.Next(360), 0), new Vector3(2f, 0.56f, 2f));
        foreach (var (x, z) in new[] { (534f, 287f), (600f, 250f), (660f, 300f), (730f, 190f), (780f, 265f), (845f, 300f), (700f, 145f), (820f, 440f), (615f, 185f), (880f, 230f), (760f, 380f) })
            Put(mist, KNP + "Fog_1.prefab", new Vector3(x, 20.1f, z), new Vector3(0, rng.Next(360), 0), new Vector3(2.5f, 0.56f, 2.5f));
        foreach (var (x, y, z) in new[] { (290f, 19.85f, 371f), (348f, 19.85f, 339f), (430f, 19.85f, 303f), (468f, 19.85f, 289f), (668f, 20.2f, 362f), (782f, 19.9f, 452f), (826f, 19.9f, 548f), (192f, 50.4f, 640f), (386f, 44.4f, 806f) })
            Put(mist, KNP + "Fog_1.prefab", new Vector3(x, y, z), new Vector3(0, rng.Next(360), 0), new Vector3(1.3f, 0.5f, 1.3f));
        var swamp = Grp("Swamp Fog");
        foreach (var (x, z) in new[] { (580f, 775f), (612f, 830f), (655f, 872f), (715f, 880f), (770f, 845f), (800f, 790f), (790f, 730f), (730f, 700f), (660f, 705f), (700f, 790f) })
            Put(swamp, HLP + "Fog.prefab", new Vector3(x, 24.9f, z), new Vector3(270, rng.Next(360), 0), Vector3.one * 1.8f);
        var clouds = Grp("Clouds");
        foreach (var (x, y, z, yaw, kind, sc) in new[] { (666f, 160f, 1119f, 15f, 3, 2.6f), (1033f, 190f, 948f, 51f, 1, 2.8f), (1144f, 150f, 554f, 87f, 3, 2.4f), (1092f, 210f, 139f, 123f, 1, 3.0f), (727f, 140f, -59f, 159f, 3, 2.2f), (334f, 180f, -118f, 195f, 3, 2.8f), (-39f, 160f, 80f, 231f, 1, 2.6f), (-134f, 200f, 487f, 267f, 3, 3.2f), (-65f, 170f, 890f, 303f, 1, 2.6f), (283f, 140f, 1099f, 339f, 3, 2.4f) })
            Put(clouds, KNP + "Cloud_" + kind + ".prefab", new Vector3(x, y, z), new Vector3(0, yaw, 0), Vector3.one * sc);
        var birds = Grp("Birds");
        foreach (var (x, y, z, yaw) in new[] { (190f, 175f, 300f, 75f), (840f, 165f, 260f, 285f), (320f, 190f, 840f, 150f), (800f, 180f, 820f, 230f) })
            Put(birds, KNP + "Birds.prefab", new Vector3(x, y, z), new Vector3(0, yaw, 0), Vector3.one);
        foreach (var (x, y, z, yaw) in new[] { (300f, 40f, 290f, 45f), (360f, 50f, 410f, 200f), (520f, 36f, 320f, 110f) })
            Put(birds, HLP + "Birds.prefab", new Vector3(x, y, z), new Vector3(0, yaw, 0), Vector3.one * 2.75f);
        var bugs = Grp("Butterflies");
        foreach (var (x, z) in new[] { (322f, 252f), (352f, 270f), (300f, 300f), (390f, 262f), (450f, 255f), (495f, 230f), (280f, 340f), (305f, 330f), (300f, 228f), (395f, 226f), (470f, 230f), (350f, 232f) })
            Put(bugs, BKP + "Butterflies.prefab", new Vector3(x, G(x, z) + 1.8f, z), Vector3.zero, Vector3.one);
        var leaves = Grp("Leaf Fall");
        // under the home trees, the meadow's hero trees and two forest-edge broadleafs (hand-placed: re-check after layout changes)
        foreach (var (x, z) in new[] { (255.7f, 266f), (281f, 263.7f), (289f, 274.4f), (340f, 250f), (410f, 268f), (285f, 340f), (479.4f, 256.1f), (360f, 400f), (560f, 320f) })
            Put(leaves, GLP + "Leaffall_Green.prefab", new Vector3(x, G(x, z) + 7f, z), Vector3.zero, Vector3.one);
        Put(leaves, GLP + "Leaffall_Yellow.prefab", new Vector3(248.3f, G(248.3f, 295f) + 7f, 295f), Vector3.zero, Vector3.one);   // the autumn birch west of the yard
        // dandelion seeds drifting over the meadow and the yard (yaw 30 = the wind drivers' yaw; the force is local z)
        var seeds = Grp("Seed Fluff");
        foreach (var (x, z) in new[] { (266f, 284f), (290f, 306f), (304f, 270f), (250f, 316f), (334f, 262f), (372f, 246f), (420f, 258f), (466f, 236f) })
        {
            var go = Put(seeds, KRP + "Kroma_Particle_Dandelions_Prefab.prefab", new Vector3(x, G(x, z) + 2f, z), new Vector3(0, 30, 0), Vector3.one);
            foreach (var mb in go.GetComponents<MonoBehaviour>()) { mb.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(mb); }   // KromaCameraFollow drags the emitter along with the camera
            Tune(go, ps =>
            {
                var main = ps.main; main.startLifetime = 7f; main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.17f);
                var em = ps.emission; em.rateOverTime = 25f; em.rateOverDistance = 0f;
                var col = ps.collision; col.enabled = false;   // world collision raycasts every particle
            });
        }
        // gold motes in the god-ray clearings and over the home pond's island
        var motes = Grp("Sunbeam Motes");
        foreach (var (x, z) in new[] { (344f, 435f), (371f, 541f), (431f, 512f), (521f, 471f), (599f, 421f), (432f, 620f), (233f, 658f), (420f, 716f), (250f, 425f) })
            Tune(Put(motes, SEM + "TEM_Dust_01A.prefab", new Vector3(x, G(x, z), z), Vector3.zero, Vector3.one * 1.4f), null);
        // additive fireflies in shaded stands off the forest trails and at the swamp's wooded edge, knee to head height
        var fireflies = Grp("Fireflies");
        foreach (var (x, y, z) in new[] { (404f, 0f, 432f), (376f, 0f, 484f), (444f, 0f, 524f), (296f, 0f, 488f), (540f, 0f, 556f), (504f, 0f, 432f), (626f, 24.5f, 798f), (590f, 24.5f, 794f), (762f, 24.5f, 842f) })
            Tune(Put(fireflies, SFN + "TFF_Fireflies_01A.prefab", new Vector3(x, Mathf.Max(y, G(x, z)), z), Vector3.zero, Vector3.one), ps =>
            {
                var shape = ps.shape; shape.position = new Vector3(0, 1.5f, 0); shape.radius = 3f;
            });
        EditorSceneManager.MarkSceneDirty(scene);
        return new { particleSystems = n };
    }

    // Shared Raygeas wind (Grasslands/Lakefell shaders, incl. the house ivy and the meadow birches), aligned with the other drivers.
    public static object SharedWind()
    {
        var env = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Environment");
        var wind = env.transform.Find("Wind");
        var type = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Raygeas.WindSettings")).First(t => t != null);
        var existing = wind.Find("Shared Wind Settings");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);
        var go = new GameObject("Shared Wind Settings");
        go.transform.SetParent(wind, false);
        go.AddComponent(type);
        foreach (Transform t in wind) t.SetPositionAndRotation(new Vector3(505, 40, 520), Quaternion.Euler(0, 30, 0));   // sun yaw + 90, as Komorebi does
        EditorSceneManager.MarkSceneDirty(env.scene);
        return wind.GetComponentsInChildren<MonoBehaviour>().Select(m => m.GetType().FullName).ToArray();
    }
}
