using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Shared shot effects in Content/Weapons/_Shared/Effects: the guns' muzzle flash is a wrapped vendor flash (GunFlash*)
// with a short flash of point light (particle Lights module), the older Gabriel Aguiar MuzzleFlash keeps its vendor look
// plus the same light, and the tracer is a stylized glowing line (a trail with Gabriel Aguiar's
// Line001) instead of a bullet model. Safe to re-run.
public static class ShotEffectsSetup
{
    const string Dir = "Assets/_Game/Content/Weapons/_Shared/Effects";
    const string Gabriel = "Assets/_StoreAssets/VFX/GabrielAguiarProductions";
    const string VendorMaterials = Gabriel + "/UniqueProjectilesVol_1/Materials";
    const string Hovl = "Assets/_StoreAssets/VFX/Hovl Studio/Muzzle flashes";
    const string LightPrefabPath = Dir + "/MuzzleFlashLight.prefab";
    const string TracerLinePath = Dir + "/Tracer_Line.mat";
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
        Material line = TracerLine(log);
        AssetDatabase.SaveAssets();

        EditPrefab(Dir + "/MuzzleFlash.prefab", root =>
        {
            Swap(root, "Muzzle", flashCore, log);
            Swap(root, "Beam", vendorGlow, log);
            FlashLight(root, lightTemplate, log);
        });
        EditPrefab(Dir + "/Tracer.prefab", root =>
        {
            Swap(root, "vfx_Projectile_BulletOrange", vendorGlow, log);
            SetActive(root, "Bullet_B_Part", false, log);
            SetActive(root, "Beam", false, log);
            TracerTrail(root.GetComponentInChildren<TrailRenderer>(true), line, log);
        });

        log.Add(WrapFlash(GunFlashSource, GunFlashName, GunFlashScale).ToString());
        var effects = AssetDatabase.LoadAssetAtPath<Aipocalypse.Core.WeaponSystem.BulletEffects>(BulletEffectsPath);
        var so = new SerializedObject(effects);
        so.FindProperty("_muzzleFlash").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<Aipocalypse.Core.WeaponSystem.PooledEffect>($"{Dir}/{GunFlashName}.prefab");
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
        var pooled = root.AddComponent<Aipocalypse.Core.WeaponSystem.PooledEffect>();
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

    // Additive, HDR so bloom picks it up; the trail's colour gradient tints it from a hot head to an orange tail.
    static Material TracerLine(List<string> log)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TracerLinePath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(material, TracerLinePath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Gabriel + "/Textures/Line001.PNG"));
        material.SetColor("_BaseColor", new Color(4.5f, 3.4f, 1.6f, 1f));
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
        log.Add($"{TracerLinePath}: additive Line001, colour {material.GetColor("_BaseColor")}");
        return material;
    }

    static void TracerTrail(TrailRenderer trail, Material line, List<string> log)
    {
        trail.sharedMaterial = line;
        trail.time = 0.08f;
        trail.minVertexDistance = 0.05f;
        trail.widthMultiplier = 0.14f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f));
        trail.textureMode = LineTextureMode.Stretch;
        trail.alignment = LineAlignment.View;
        trail.shadowCastingMode = ShadowCastingMode.Off;
        trail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 1f, 0.85f), 0f), new GradientColorKey(new Color(1f, 0.75f, 0.3f), 0.4f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.4f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        log.Add($"Trail: {trail.time} s, width {trail.widthMultiplier}");
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

    static void SetActive(GameObject root, string child, bool active, List<string> log)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != child)
                continue;
            t.gameObject.SetActive(active);
            log.Add($"{root.name}/{child} active {active}");
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
