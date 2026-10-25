using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Skybox Plus (Kronnect) sky for the day/night cycle. The director feeds it the sun, moon and moon phase (DAYNIGHT.md → Sky).
public static class SkyboxPlusSetup
{
    const string PresetDir = "Assets/_StoreAssets/Rendering/Kronnect/SkyboxPlus/Materials/";
    const string SkyPath = "Assets/_Game/Data/Environment/TimeOfDay/SkyboxPlus_Sky.mat";
    const string TimeOfDaySkyPath = "Assets/_Game/Data/Environment/TimeOfDay/TimeOfDay_Sky.mat";

    // Copies a vendor preset (e.g. "Clouds-B1") to the first-party sky with night, stars and the scene's fog colour on.
    // Overwrites SkyboxPlus_Sky.mat: hand tuning is lost.
    public static object Create(string preset)
    {
        var source = AssetDatabase.LoadAssetAtPath<Material>(PresetDir + preset + ".mat");
        if (source == null)
            return "no preset " + preset;
        var sky = new Material(source) { name = "SkyboxPlus_Sky" };
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
        AssetDatabase.SaveAssets();
        return "sky from " + preset + ": " + string.Join(" ", AssetDatabase.LoadAssetAtPath<Material>(SkyPath).shaderKeywords);
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
