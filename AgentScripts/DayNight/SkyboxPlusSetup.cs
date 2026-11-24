using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Skybox Plus (Kronnect) sky for the day/night cycle. The director feeds it the sun, moon and moon phase (DAYNIGHT.md → Sky).
public static class SkyboxPlusSetup
{
    const string PresetDir = "Assets/_StoreAssets/Rendering/Kronnect/SkyboxPlus/Materials/";
    const string SkyPath = "Assets/_Game/Data/Environment/TimeOfDay/SkyboxPlus_Sky.mat";
    const string TimeOfDaySkyPath = "Assets/_Game/Data/Environment/TimeOfDay/TimeOfDay_Sky.mat";
    // The star map SkyboxPlus_Sky.mat uses (DAYNIGHT.md → Sky).
    const string StarMapPath = "Assets/_Game/Data/Environment/TimeOfDay/StarMap/StarMap_NASA2020_Stylized3.png";
    // First-party fork of the vendor Clouds shader with a star map layer (SkyStarMap.shader).
    const string StarMapShader = "Aipocalypse/Skybox/Skybox Plus Star Map";

    // Copies a vendor preset (e.g. "Clouds-B1") to the first-party sky with night, stars and the scene's fog colour on,
    // on the star map fork (StarMapPath when it exists, else the procedural stars). Overwrites SkyboxPlus_Sky.mat: hand tuning is lost.
    public static object Create(string preset)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(PresetDir + preset + ".mat");
        if (source == null)
            return "no preset " + preset;
        var sky = new Material(source) { name = "SkyboxPlus_Sky", shader = Shader.Find(StarMapShader) };
        sky.SetFloat("_SkyboxNight", 1f);
        sky.EnableKeyword("SKYBOX_NIGHT");
        // Horizon blends into the look's fog colour (RenderSettings.fogColor, set by the director).
        sky.SetFloat("_UseUnityFogColor", 1f);
        sky.EnableKeyword("USE_UNITY_FOG_COLOR");
        // Bluer day than the presets' violet; a dense starfield with a Milky Way band (the night grade darkens it a lot).
        sky.SetColor("_SkyTint", new Color(0.4f, 0.6f, 1f));
        sky.SetColor("_SkyNightColor", new Color(0.012f, 0.02f, 0.05f));
        sky.SetFloat("_StarSize", 150f);
        sky.SetFloat("_StarDensity", 0.09f);
        sky.SetFloat("_StarBrightness", 0.9f);
        sky.SetFloat("_StarClustering", 0.9f);
        sky.SetFloat("_StarBandWidth", 0.35f);
        sky.SetColor("_StarDarkColor", new Color(1f, 0.7f, 0.5f));
        sky.SetColor("_StarBrightColor", new Color(0.7f, 0.85f, 1f));
        sky.SetFloat("_MoonSize", 4f);
        sky.SetFloat("_MoonFlare", 0.08f);
        if (sky.GetTexture("_MoonTex") == null)
            sky.SetTexture("_MoonTex", AssetDatabase.LoadAssetAtPath<Material>(PresetDir + "StarryNight.mat").GetTexture("_MoonTex"));
        if (sky.GetTexture("_NoiseTex") == null)
            sky.SetTexture("_NoiseTex", AssetDatabase.LoadAssetAtPath<Material>(PresetDir + "StarryNight.mat").GetTexture("_NoiseTex"));
        SetStarMap(sky, true, 1f);

        if (AssetDatabase.LoadAssetAtPath<Material>(SkyPath) is { } existing)
        {
            existing.shader = sky.shader;
            existing.CopyPropertiesFromMaterial(sky);
            existing.shaderKeywords = sky.shaderKeywords;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(sky);
        }
        else
            AssetDatabase.CreateAsset(sky, SkyPath);
        AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadAssetAtPath<Material>(SkyPath));
        return "sky from " + preset + ": " + string.Join(" ", AssetDatabase.LoadAssetAtPath<Material>(SkyPath).shaderKeywords);
    }

    // Imports the StarMapPath texture as a cubemap and turns the star map on (or back to the procedural stars) on SkyboxPlus_Sky.mat.
    public static object StarMap(bool on, float intensity)
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        string result = SetStarMap(sky, on, intensity);
        EditorUtility.SetDirty(sky);
        AssetDatabase.SaveAssetIfDirty(sky);
        return result;
    }

    static string SetStarMap(Material sky, bool on, float intensity)
    {
        var importer = AssetImporter.GetAtPath(StarMapPath) as TextureImporter;
        if (importer == null)
            on = false;
        else
            ImportStarMap(importer);
        sky.SetFloat("_StarMapOn", on ? 1f : 0f);
        sky.SetKeyword(new UnityEngine.Rendering.LocalKeyword(sky.shader, "STAR_MAP"), on);
        sky.SetFloat("_StarMapIntensity", intensity);
        sky.SetTexture("_StarMap", on ? AssetDatabase.LoadAssetAtPath<Cubemap>(StarMapPath) : null);
        return on ? "star map on, intensity " + intensity : importer == null ? "no " + StarMapPath + ": procedural stars" : "procedural stars";
    }

    // Equirectangular PNG → cubemap (Unity's latitude-longitude layout). BC7 keeps the coloured single-pixel stars clean.
    static void ImportStarMap(TextureImporter importer)
    {
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        bool changed = settings.textureShape != TextureImporterShape.TextureCube || settings.generateCubemap != TextureImporterGenerateCubemap.Cylindrical;
        settings.textureShape = TextureImporterShape.TextureCube;
        settings.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;
        settings.cubemapConvolution = TextureImporterCubemapConvolution.None;
        settings.seamlessCubemap = true;
        settings.sRGBTexture = true;
        settings.mipmapEnabled = true;
        settings.wrapMode = TextureWrapMode.Clamp;
        settings.filterMode = FilterMode.Trilinear;
        importer.SetTextureSettings(settings);
        var platform = importer.GetDefaultPlatformTextureSettings();
        changed |= platform.format != TextureImporterFormat.BC7 || platform.maxTextureSize != 2048;
        // Max size is the cube face: 2048 ≈ 23 texels per degree, sharper than 1080p shows the sky; 4096 costs 4× the memory.
        platform.maxTextureSize = 2048;
        platform.format = TextureImporterFormat.BC7;
        platform.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SetPlatformTextureSettings(platform);
        if (changed)
            importer.SaveAndReimport();
    }

    // Sets the open scene's skybox: true = Skybox Plus, false = back to the Time Of Day sky. Saves the scene.
    public static object Use(bool skyboxPlus)
    {
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(skyboxPlus ? SkyPath : TimeOfDaySkyPath);
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return scene.name + " skybox " + AssetDatabase.GetAssetPath(RenderSettings.skybox);
    }

    // Play mode: tries another vendor preset on the running sky without touching assets (the director copies it at runtime).
    public static object Try(string preset)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(PresetDir + preset + ".mat");
        if (source == null)
            return "no preset " + preset;
        RenderSettings.skybox = source;
        return "trying " + preset;
    }

    // Play mode: sets a property on the running sky (the runtime copy) to tune values before writing them to the asset.
    public static object Set(string property, float value)
    {
        RenderSettings.skybox.SetFloat(property, value);
        return RenderSettings.skybox.name + " " + property + " = " + value;
    }
}
