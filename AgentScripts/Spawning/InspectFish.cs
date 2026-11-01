using System.Text;
using UnityEditor;
using UnityEngine;

public static class InspectFish
{
    public static string Run()
    {
        var sb = new StringBuilder();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab Fish_", new[] { "Assets/_StoreAssets/Characters/polyperfect/Low Poly Animated Freshwater Fish/_Prefabs" }))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin == null) continue;
            var t = skin.transform;
            sb.AppendLine($"{go.name}: mesh {skin.sharedMesh.name} bounds {skin.sharedMesh.bounds.size:F2} tris {skin.sharedMesh.triangles.Length / 3} mats {skin.sharedMaterials.Length} skinScale {t.lossyScale:F2} rootScale {go.transform.localScale:F2} skinRot {t.localEulerAngles:F0}");
        }
        return sb.ToString();
    }
}
