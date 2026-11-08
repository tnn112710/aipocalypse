// Adds the scene's GrassBendField ("Environment/Grass Bending": one particle-system Stylized Grass bender that enemies and
// animals join) to the open scene and wires it into the EnvironmentInstaller. Idempotent; saves the scene.
// Run: unity command script run --file AgentScripts/Grass/GrassBendingSetup.cs --entry GrassBendingSetup.Run --args '[1, 2]'
using MB13.Aipocalypse.Core.Bootstrap;
using MB13.Aipocalypse.Core.EnvironmentSystem;
using sc.stylizedgrass.runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class GrassBendingSetup
{
    const string Name = "Grass Bending";

    public static object Run(float flatten, float push)
    {
        var scene = EditorSceneManager.GetActiveScene();
        var environment = GameObject.Find("Environment");
        if (environment == null) throw new System.Exception("no Environment root in " + scene.name);
        var old = environment.transform.Find(Name);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject(Name);
        go.SetActive(false);
        go.transform.SetParent(environment.transform, false);

        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = 1e6f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 512;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;

        var bender = go.AddComponent<GrassBender>();
        bender.renderer = renderer;
        bender.flattenStrength = flatten;
        bender.pushStrength = push;
        go.AddComponent<GrassBendField>();
        go.SetActive(true);
        renderer.sharedMaterial = null;   // the bender assigns its own runtime material in OnEnable

        var scope = Object.FindAnyObjectByType<GameLifetimeScope>();
        if (scope == null || !scope.TryGetComponent(out EnvironmentInstaller installer))
            throw new System.Exception("no EnvironmentInstaller on the GameLifetimeScope");
        var so = new SerializedObject(installer);
        so.FindProperty("_grassBending").objectReferenceValue = go.GetComponent<GrassBendField>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return $"{scene.name}: {environment.name}/{Name} wired into {scope.name}'s EnvironmentInstaller";
    }
}
