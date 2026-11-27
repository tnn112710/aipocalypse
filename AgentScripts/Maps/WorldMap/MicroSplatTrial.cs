using System.Collections.Generic;
using System.Linq;
using System.Text;
using JBooth.MicroSplat;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// MicroSplat Core trial on the WorldMap terrain: before/after views, conversion with the layer tints carried over.
public static class MicroSplatTrial
{
    // Look at a view: 0 spawn eye, 1 spawn ground close-up, 2 spawn aerial, 3 pebble shore, 4 wet mud bank, 5 rock slope.
    public static object View(int index)
    {
        var terrain = Terrain.activeTerrain;
        var player = SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "Player").transform;
        Vector3 p = player.position;
        float yaw = player.eulerAngles.y;
        switch (index)
        {
            case 0: return Look(p + Vector3.up * 1.7f, 8, yaw);
            case 1: return Look(p + Vector3.up * 1.7f, 50, yaw);
            case 2: return Look(p + new Vector3(0, 60, 0) - player.forward * 60, 35, yaw);
            default:
                int layer = index == 3 ? 4 : index == 4 ? 5 : 7;
                var spot = FindLayerSpot(terrain, layer, p, 250f);
                spot.y = terrain.SampleHeight(spot) + terrain.transform.position.y;
                var dir = (spot - p); dir.y = 0;
                float y = dir.sqrMagnitude > 1 ? Quaternion.LookRotation(dir).eulerAngles.y : yaw;
                var eye = spot - Quaternion.Euler(0, y, 0) * Vector3.forward * 6f;
                eye.y = terrain.SampleHeight(eye) + terrain.transform.position.y + 1.7f;
                return Look(eye, 25, y);
        }
    }

    static Vector3 FindLayerSpot(Terrain t, int layer, Vector3 near, float radius)
    {
        var td = t.terrainData;
        int res = td.alphamapResolution;
        float cell = td.size.x / res;
        int r = Mathf.CeilToInt(radius / cell);
        int cx = Mathf.RoundToInt((near.x - t.transform.position.x) / cell), cz = Mathf.RoundToInt((near.z - t.transform.position.z) / cell);
        int x0 = Mathf.Clamp(cx - r, 0, res - 1), z0 = Mathf.Clamp(cz - r, 0, res - 1);
        int w = Mathf.Clamp(cx + r, 0, res - 1) - x0, h = Mathf.Clamp(cz + r, 0, res - 1) - z0;
        var a = td.GetAlphamaps(x0, z0, w, h);
        float best = -1; int bx = 0, bz = 0;
        for (int z = 0; z < h; z += 2)
            for (int x = 0; x < w; x += 2)
            {
                float d = (x + x0 - cx) * (x + x0 - cx) + (z + z0 - cz) * (z + z0 - cz);
                float score = a[z, x, layer] - d / (r * r) * 0.3f;
                if (score > best) { best = score; bx = x; bz = z; }
            }
        return new Vector3((bx + x0) * cell, 0, (bz + z0) * cell) + t.transform.position;
    }

    static object Look(Vector3 pos, float pitch, float yaw)
    {
        var view = SceneView.lastActiveSceneView;
        view.orthographic = false;
        view.drawGizmos = false;
        var state = view.sceneViewState;
        state.showSkybox = true; state.showFog = true; state.showImageEffects = true;
        var cs = view.cameraSettings;
        cs.fieldOfView = 60; cs.dynamicClip = false; cs.nearClip = 0.3f; cs.farClip = 4000f;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == "HUD" || root.name == "Player") SceneVisibilityManager.instance.Hide(root, true);
        // LookAtDirect: AlignViewToObject animates, and a capture right after it still sees the old pose
        var rot = Quaternion.Euler(pitch, yaw, 0);
        const float dist = 10f;
        view.LookAtDirect(pos + rot * Vector3.forward * dist, rot, dist * Mathf.Tan(30f * Mathf.Deg2Rad));
        // the camera only follows the pivot on repaint, which doesn't happen while Unity is unfocused
        view.camera.transform.SetPositionAndRotation(pos, rot);
        view.camera.fieldOfView = 60;
        view.camera.nearClipPlane = 0.3f;
        view.camera.farClipPlane = 4000f;
        SceneView.RepaintAll();
        EditorApplication.QueuePlayerLoopUpdate();
        return pos.ToString("0.0") + " pitch " + pitch + " yaw " + yaw.ToString("0");
    }

    // Converts the terrain, then carries over each TerrainLayer tint (MicroSplat ignores diffuseRemapMax) and caps the
    // shader at 8 textures. The scene itself is not saved here.
    public static object Convert()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain.GetComponent<MicroSplatTerrain>() != null) return "already converted";
        var layers = terrain.terrainData.terrainLayers;
        var config = MicroSplatTerrainEditor.ConvertTerrains(new[] { terrain }, layers);
        var mst = terrain.GetComponent<MicroSplatTerrain>();
        var mat = mst.templateMaterial;

        var kw = mst.keywordSO.keywords;
        if (!kw.Contains("_PERTEXTINT")) kw.Add("_PERTEXTINT");
        if (layers.Length <= 8 && !kw.Contains("_MAX8TEXTURES")) kw.Add("_MAX8TEXTURES");
        for (int i = 0; i < layers.Length; i++)
            mst.propData.SetValue(i, 1, (Color)layers[i].diffuseRemapMax);
        EditorUtility.SetDirty(mst.propData);
        EditorUtility.SetDirty(mst.keywordSO);
        new MicroSplatShaderGUI.MicroSplatCompiler().Compile(mat);
        MicroSplatTerrain.SyncAll();

        foreach (var o in new Object[] { config, mat, mst.propData, mst.keywordSO })
            AssetDatabase.SaveAssetIfDirty(o);

        var sb = new StringBuilder();
        sb.AppendLine("config " + AssetDatabase.GetAssetPath(config));
        sb.AppendLine("material " + AssetDatabase.GetAssetPath(mat) + " shader " + mat.shader.name + " supported " + mat.shader.isSupported);
        sb.AppendLine("keywords " + string.Join(" ", kw));
        sb.AppendLine("terrain material " + (terrain.materialTemplate ? terrain.materialTemplate.shader.name : "null"));
        sb.AppendLine("layers kept " + terrain.terrainData.terrainLayers.SequenceEqual(layers));
        return sb.ToString();
    }

    // Undo for the trial: removes the component and restores Unity's terrain material. Generated assets stay on disk.
    public static object Revert(string terrainLitMaterialPath)
    {
        var terrain = Terrain.activeTerrain;
        var mst = terrain.GetComponent<MicroSplatTerrain>();
        if (mst != null) Object.DestroyImmediate(mst);
        terrain.materialTemplate = AssetDatabase.LoadAssetAtPath<Material>(terrainLitMaterialPath);
        terrain.basemapDistance = 1000;
        return "reverted to " + (terrain.materialTemplate ? terrain.materialTemplate.shader.name : "null");
    }
}
