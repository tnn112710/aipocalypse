// Adds a Stylized Grass bender at the player's feet (child "Grass Bender" of Player.prefab). Idempotent.
// Run: unity command script run --file AgentScripts/Grass/PlayerGrassBender.cs --entry PlayerGrassBender.Run
using System.Linq;
using sc.stylizedgrass.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class PlayerGrassBender
{
    const string PlayerPrefab = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";

    public static object Run(float radius, float height)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            var old = root.transform.Find("Grass Bender");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var go = new GameObject("Grass Bender");
            go.SetActive(false);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0, height, 0);
            go.transform.localScale = Vector3.one * radius * 2;
            var sphere = Resources.GetBuiltinResource<Mesh>("New-Sphere.fbx");
            go.AddComponent<MeshFilter>().sharedMesh = sphere;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            mr.allowOcclusionWhenDynamic = false;
            var bender = go.AddComponent<GrassBender>();
            bender.renderer = mr;
            bender.meshFilter = go.GetComponent<MeshFilter>();
            bender.flattenStrength = 0.6f;
            bender.pushStrength = 1f;
            go.SetActive(true);
            mr.sharedMaterial = null;   // the bender assigns its own runtime material in OnEnable
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
            return new { mesh = sphere != null ? sphere.name : "missing", children = root.transform.Cast<Transform>().Select(t => t.name).ToArray() };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
