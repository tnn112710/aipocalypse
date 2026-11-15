using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Shared shot effects in Content/Weapons/_Shared/Effects: the guns' muzzle flash is a wrapped vendor flash (GunFlash*)
// with a short flash of point light (particle Lights module), the older Gabriel Aguiar MuzzleFlash keeps its vendor look
// plus the same light, and the tracer is a stylized glowing streak (Tracer()) instead of a bullet model. Safe to re-run.
public static class ShotEffectsSetup
{
    const string Dir = "Assets/_Game/Content/Weapons/_Shared/Effects";
    const string Gabriel = "Assets/_StoreAssets/VFX/GabrielAguiarProductions";
    const string VendorMaterials = Gabriel + "/UniqueProjectilesVol_1/Materials";
    const string Hovl = "Assets/_StoreAssets/VFX/Hovl Studio/Muzzle flashes";
    const string HovlTextures = "Assets/_StoreAssets/VFX/Hovl Studio/HSFiles/Textures";
    const string LightPrefabPath = Dir + "/MuzzleFlashLight.prefab";
    const string TracerPath = Dir + "/Tracer.prefab";
    const string TracerLinePath = Dir + "/Tracer_Line.mat";
    const string TracerHaloPath = Dir + "/Tracer_Halo.mat";
    const string TracerHeadPath = Dir + "/Tracer_Head.mat";
    const string TracerGlowPath = Dir + "/Tracer_Glow.png";
    const float DefaultTracerTime = 0.025f;
    const string LightChild = "FlashLight";
    const string BulletEffectsPath = "Assets/_Game/Data/Weapons/BulletEffects.asset";
    // The guns' flash, wrapped from a vendor prefab at a size that reads in first person.
    const string GunFlashSource = Hovl + "/Prefabs/For bloom/Muzzle flash 3.prefab";
    const string GunFlashName = "MuzzleFlash_Hovl3";
    const float GunFlashScale = 0.25f;

    static readonly Color FlashLightColor = new(1f, 0.62f, 0.3f);

    public static object All()
    {
        var log = new List<string>();
        foreach (string old in new[] { "MuzzleFlash_Core", "MuzzleFlash_Glow", "Tracer_Glow", "Tracer_Trail", "Tracer_Bullet" })
            if (AssetDatabase.DeleteAsset($"{Dir}/{old}.mat"))
                log.Add($"deleted {old}.mat");

        var flashCore = AssetDatabase.LoadAssetAtPath<Material>(VendorMaterials + "/Impact02_SGAddScroll.mat");
        var vendorGlow = AssetDatabase.LoadAssetAtPath<Material>(VendorMaterials + "/UniqueProjectiles_Add_8x8.mat");
        Light lightTemplate = LightPrefab(log);
        AssetDatabase.SaveAssets();

        EditPrefab(Dir + "/MuzzleFlash.prefab", root =>
        {
            Swap(root, "Muzzle", flashCore, log);
            Swap(root, "Beam", vendorGlow, log);
            FlashLight(root, lightTemplate, log);
        });
        log.Add(Tracer().ToString());

        log.Add(WrapFlash(GunFlashSource, GunFlashName, GunFlashScale).ToString());
        var effects = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.WeaponSystem.BulletEffects>(BulletEffectsPath);
        var so = new SerializedObject(effects);
        so.FindProperty("_muzzleFlash").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.Effects.PooledEffect>($"{Dir}/{GunFlashName}.prefab");
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(effects);
        log.Add($"BulletEffects muzzle flash = {GunFlashName}");
        return string.Join("\n", log);
    }

    // <name>.prefab in the Effects folder: a vendor muzzle flash prefab unpacked under our pooled root, scaled for first
    // person (every system scales with the hierarchy), built-in-pipeline particle materials swapped for URP copies, plus
    // the same flash light.
    public static object WrapFlash(string sourcePath, string name, float scale)
    {
        var log = new List<string>();
        string path = $"{Dir}/{name}.prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
            return $"missing {sourcePath}";
        var oldFlash = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + "/MuzzleFlash.prefab");

        var root = new GameObject(name);
        root.layer = oldFlash.layer;
        var pooled = root.AddComponent<MB13.Aipocalypse.Core.Effects.PooledEffect>();
        var so = new SerializedObject(pooled);
        so.FindProperty("_lifetime").floatValue = 0.3f;
        so.FindProperty("_maxActive").intValue = 8;
        so.ApplyModifiedPropertiesWithoutUndo();

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        visual.name = "Flash";
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = Vector3.one * scale;
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = root.layer;
        foreach (var renderer in visual.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
                if (materials[i] != null && materials[i].shader.name == "Particles/Standard Unlit")
                    materials[i] = HovlUrp(materials[i], log);
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }

        FlashLight(root, AssetDatabase.LoadAssetAtPath<GameObject>(LightPrefabPath).GetComponent<Light>(), log);
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        log.Add($"{path}: scale {scale}");
        return string.Join("\n", log);
    }

    // URP Particles/Unlit copy of a built-in Particles/Standard Unlit material (Hovl's, Fade mode), in the Effects folder.
    static Material HovlUrp(Material source, List<string> log)
    {
        string path = $"{Dir}/MuzzleFlash_{source.name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
        material.SetColor("_BaseColor", source.GetColor("_Color"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, path);
        log.Add($"{path}: from {source.name}, colour {source.GetColor("_Color")}");
        return material;
    }

    // Tracer.prefab rebuilt from scratch: a thin white-hot core (Hovl's soft-edged Line3) over a wide, dim amber halo
    // that fades smoothly from the middle (our generated Tracer_Glow.png), plus a head spark (Hovl's Glow1) so a shot
    // flying straight away still reads as a bright point. The halo stays below white so its colour shows across its whole
    // width instead of only as a reddish rim. Keeps the prefab's tuned trail time. Touches only the tracer's assets.
    public static object Tracer()
    {
        var log = new List<string>();
        var line = AssetDatabase.LoadAssetAtPath<Texture2D>(HovlTextures + "/Line3.png");
        var glow = AssetDatabase.LoadAssetAtPath<Texture2D>(HovlTextures + "/Glow1.png");
        Material core = Additive(TracerLinePath, line, new Color(6f, 5f, 3.6f, 1f), log);
        Material halo = Additive(TracerHaloPath, GlowProfile(log), new Color(0.85f, 0.7f, 0.45f, 1f), log);
        Material head = Additive(TracerHeadPath, glow, new Color(4f, 2.6f, 1.2f, 1f), log);

        EditPrefab(TracerPath, root =>
        {
            TrailRenderer tuned = root.GetComponentInChildren<TrailRenderer>(true);
            float time = tuned != null ? tuned.time : DefaultTracerTime;
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

            Trail(root, "Core", core, time, 0.03f, 0.4f,
                new[] { new GradientColorKey(new Color(1f, 1f, 0.9f), 0f), new GradientColorKey(new Color(1f, 0.7f, 0.35f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) }, log);
            Trail(root, "Halo", halo, time * 1.2f, 0.3f, 0.25f,
                new[] { new GradientColorKey(new Color(1f, 0.9f, 0.75f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.5f), 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0.4f, 0.5f), new GradientAlphaKey(0f, 1f) }, log);
            Head(root, head, log);
        });
        log.Add($"{TracerPath}: Core, Halo, Head");
        return string.Join("\n", log);
    }

    static void Trail(GameObject root, string name, Material material, float time, float width, float tailWidth,
        GradientColorKey[] colors, GradientAlphaKey[] alphas, List<string> log)
    {
        var go = new GameObject(name);
        go.layer = root.layer;
        go.transform.SetParent(root.transform, false);
        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = material;
        trail.time = time;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = width;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, tailWidth));
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.lightProbeUsage = LightProbeUsage.Off;
        trail.reflectionProbeUsage = ReflectionProbeUsage.Off;
        var gradient = new Gradient();
        gradient.SetKeys(colors, alphas);
        trail.colorGradient = gradient;
        log.Add($"{name}: {time:0.###} s, width {width}");
    }

    // One camera-facing glow that rides with the tracer; size and tint vary a little per shot so bursts don't repeat.
    static void Head(GameObject root, Material material, List<string> log)
    {
        var go = new GameObject("Head");
        go.layer = root.layer;
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = 5f; // outlives any flight: the bullet code returns the tracer
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.42f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.7f), new Color(1f, 0.7f, 0.45f));
        main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
        var shape = ps.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        log.Add("Head: 1 glow, 0.3-0.42 m");
    }

    // A trail's cross-section: white, alpha falling smoothly from 1 in the middle to exactly 0 at both edges, the same
    // along the whole length. Generated because URP particle shaders ignore texture tiling, so a round glow can't be used.
    static Texture2D GlowProfile(List<string> log)
    {
        const int width = 8, height = 64;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        float edge = Mathf.Exp(-4.5f);
        for (int y = 0; y < height; y++)
        {
            float d = (y + 0.5f) / height * 2f - 1f;
            float alpha = Mathf.Clamp01((Mathf.Exp(-4.5f * d * d) - edge) / (1f - edge));
            for (int x = 0; x < width; x++)
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        System.IO.File.WriteAllBytes(TracerGlowPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TracerGlowPath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(TracerGlowPath);
        importer.textureType = TextureImporterType.Default;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        EditorUtility.SetDirty(importer);
        AssetDatabase.WriteImportSettingsIfDirty(TracerGlowPath);
        AssetDatabase.ImportAsset(TracerGlowPath);
        log.Add($"{TracerGlowPath}: {width}x{height} soft profile");
        return AssetDatabase.LoadAssetAtPath<Texture2D>(TracerGlowPath);
    }

    // Additive URP Particles/Unlit, HDR colour so bloom picks it up; trails and particles tint it per vertex.
    static Material Additive(string path, Texture2D texture, Color color, List<string> log)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", texture);
        material.SetTextureScale("_BaseMap", Vector2.one);
        material.SetTextureOffset("_BaseMap", Vector2.zero);
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 2f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.One);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
        log.Add($"{path}: additive {texture.name}, colour {color}");
        return material;
    }

    // Template the Lights module copies per particle; its intensity is scaled by the module's curve.
    static Light LightPrefab(List<string> log)
    {
        var go = new GameObject("MuzzleFlashLight");
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = FlashLightColor;
        light.intensity = 1f;
        light.range = 6f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.ForcePixel;
        light.lightmapBakeType = LightmapBakeType.Realtime;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, LightPrefabPath);
        Object.DestroyImmediate(go);
        log.Add($"{LightPrefabPath}: point, range 6");
        return prefab.GetComponent<Light>();
    }

    // One invisible particle per shot that lives 0.07 s and carries a point light fading from 6 to 0.
    internal static void FlashLight(GameObject root, Light template, List<string> log)
    {
        Transform old = root.transform.Find(LightChild);
        if (old != null)
            Object.DestroyImmediate(old.gameObject);

        var go = new GameObject(LightChild);
        go.layer = root.layer;
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.1f;
        main.loop = false;
        main.playOnAwake = true;
        main.startLifetime = 0.07f;
        main.startSpeed = 0f;
        main.startSize = 0f;
        main.maxParticles = 1;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        var shape = ps.shape;
        shape.enabled = false;

        var lights = ps.lights;
        lights.enabled = true;
        lights.light = template;
        lights.ratio = 1f;
        lights.maxLights = 1;
        lights.useParticleColor = false;
        lights.sizeAffectsRange = false;
        lights.alphaAffectsIntensity = false;
        lights.useRandomDistribution = false;
        lights.range = 1f;
        lights.intensity = new ParticleSystem.MinMaxCurve(6f, new AnimationCurve(new Keyframe(0f, 1f, 0f, -2f), new Keyframe(1f, 0f, 0f, 0f)));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = null;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        log.Add($"{LightChild}: 1 particle, 0.07 s, light x6 fading");
    }

    static void Swap(GameObject root, string child, Material material, List<string> log)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.name != child)
                continue;
            renderer.sharedMaterial = material;
            log.Add($"{root.name}/{child} -> {material.name}");
        }
    }

    static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
