using System;
using System.Collections.Generic;
using System.IO;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using Ami.BroAudio.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Builds the test ammo crate prefab (Build) and places it in WorldMap's yard (Place); both re-runnable.
// Run: unity command script run --file AgentScripts/Props/AmmoCrateSetup.cs --entry AmmoCrateSetup.Build (then AmmoCrateSetup.Place)
public static class AmmoCrateSetup
{
    const string PrefabDir = "Assets/_Game/Content/Props/AmmoCrate";
    const string PrefabPath = PrefabDir + "/AmmoCrate.prefab";
    const string ModelPath = "Assets/_StoreAssets/Env/Animpic/Polygon-Mega Weapons Kit/Prefabs/SM_Ammo_Box_Wood_Open.prefab";
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string TakeSoundPath = "Assets/_Game/Data/Audio/Player/AmmoCrate_Take.asset";
    const string ScenePath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string ModelName = "Model";
    const string HomeName = "Home Base";
    const string HousePrefix = "House";
    const string YardPropsName = "Yard Props";

    // Metres: crate centre out from the house's front, in from the house's side, free room kept around it, ground probe height.
    const float FrontGap = 2.5f;
    const float CornerInset = 1.5f;
    const float Clearance = 0.3f;
    const float ProbeHeight = 20f;

    // Spots tried in order, in the house's frame: side -1 = its -X corner (the gate side), +1 = the other; extra = metres
    // further out than FrontGap.
    static readonly (float side, float extra)[] Spots = { (-1f, 0f), (1f, 0f), (-1f, 1.5f), (1f, 1.5f), (-1f, 3f), (1f, 3f) };

    public static object Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "leave Play mode first";

        var log = new List<string>();
        var model = Load<GameObject>(ModelPath);
        if (!Load<GameObject>(PlayerPath).TryGetComponent(out Player player))
            throw new Exception("no Player on " + PlayerPath);
        var playerFields = new SerializedObject(player);
        var ammo = playerFields.FindProperty("_ammoConfig").objectReferenceValue as AmmoConfig;
        if (ammo == null)
            throw new Exception("the Player prefab has no _ammoConfig");
        var tuning = playerFields.FindProperty("_tuning").objectReferenceValue as PlayerTuning;
        if (tuning == null || tuning.Interaction == null)
            throw new Exception("the Player prefab has no interaction tuning");
        int layer = InteractLayer(tuning.Interaction.InteractMask);
        var takeSound = AssetDatabase.LoadAssetAtPath<AudioEntity>(TakeSoundPath);

        EnsureFolder(PrefabDir);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            CreateEmpty(PrefabPath);
            log.Add("created " + PrefabPath);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            root.layer = layer;
            Transform modelRoot = Model(root.transform, model, log);
            Bounds bounds = Seat(root.transform, modelRoot);

            if (!root.TryGetComponent(out BoxCollider box))
                box = root.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.center = bounds.center;
            box.size = bounds.size;

            // Carves the baked navmesh cache at runtime, so raiders walk around the crate without a rebake; it turns with the crate.
            if (!root.TryGetComponent(out Pathfinding.NavmeshCut cut))
                cut = root.AddComponent<Pathfinding.NavmeshCut>();
            cut.type = Pathfinding.NavmeshCut.MeshType.Box;
            cut.rectangleSize = new Vector2(bounds.size.x, bounds.size.z);
            cut.height = bounds.size.y;
            cut.center = bounds.center;
            cut.useRotationAndScale = true;
            cut.radiusExpansionMode = Pathfinding.NavmeshCut.RadiusExpansionMode.ExpandByAgentRadius;

            if (!root.TryGetComponent(out AmmoCrate crate))
                crate = root.AddComponent<AmmoCrate>();
            var fields = new SerializedObject(crate);
            fields.FindProperty("_ammo").objectReferenceValue = ammo;
            if (takeSound != null)
                fields.FindProperty("_takeSound._entity").objectReferenceValue = takeSound;
            fields.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
            if (!saved)
                return "failed to save " + PrefabPath;
            log.Add($"{PrefabPath}: layer {layer} ({LayerMask.LayerToName(layer)}), box {box.size.ToString("F2")} at {box.center.ToString("F2")}, " +
                $"navmesh cut {cut.rectangleSize.ToString("F2")} x {cut.height:F2}, model lifted {modelRoot.localPosition.y:F3}, ammo {AssetDatabase.GetAssetPath(ammo)}");
            log.Add(takeSound != null ? "sound " + TakeSoundPath : "sound NOT set: " + TakeSoundPath + " is missing (run RaidNightSetup.Audio, then Build again)");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return string.Join("\n", log);
    }

    public static object Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return "leave Play mode first";
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            return "open WorldMap first: " + ScenePath;
        if (scene.isDirty)
            return "WorldMap has unsaved changes: save or discard them, then run Place again (nothing changed)";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || !prefab.TryGetComponent(out BoxCollider footprint))
            return "no crate prefab: run AmmoCrateSetup.Build first";

        Transform home = Find(scene, HomeName);
        if (home == null)
            return "no '" + HomeName + "' in WorldMap";
        Transform house = House(home);
        if (house == null)
            return $"no '{HousePrefix}*' under {HomeName}";
        Transform parent = home.Find(YardPropsName);

        List<AmmoCrate> crates = Crates(scene);
        AmmoCrate crate = crates.Count > 0 ? crates[0] : null;
        Transform ignore = crate != null ? crate.transform : null;

        // The house's frame without scale, so offsets are metres; its front (door) is +Z.
        Physics.SyncTransforms();
        float yaw = house.eulerAngles.y;
        Matrix4x4 houseFrame = Matrix4x4.TRS(house.position, Quaternion.Euler(0f, yaw, 0f), Vector3.one);
        Bounds houseBounds = MeshBounds(houseFrame.inverse, house);
        Vector3 size = footprint.size;
        float reach = 0.5f * new Vector2(size.x, size.z).magnitude + new Vector2(footprint.center.x, footprint.center.z).magnitude + Clearance;

        var skipped = new List<string>();
        Vector3 ground = default;
        string spot = null;
        foreach ((float side, float extra) in Spots)
        {
            float x = side < 0f ? houseBounds.min.x + CornerInset : houseBounds.max.x - CornerInset;
            float z = houseBounds.max.z + FrontGap + extra;
            Vector3 point = houseFrame.MultiplyPoint3x4(new Vector3(x, 0f, z));
            string blocker = Blocker(point, house.position.y, reach, size.y, home, house, ignore, out ground);
            if (blocker == null)
            {
                spot = $"house-local ({x:F1}, {z:F1})";
                break;
            }
            skipped.Add($"house-local ({x:F1}, {z:F1}) {blocker}");
        }
        if (spot == null)
            return "no free spot in front of the house, nothing placed: " + string.Join("; ", skipped);

        if (crate == null)
            crate = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, scene)).GetComponent<AmmoCrate>();
        crate.name = prefab.name;
        crate.transform.SetParent(parent, true);
        crate.transform.SetPositionAndRotation(ground, Quaternion.Euler(0f, yaw, 0f));
        string injection = ListForInjection(scene, crate.gameObject);

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);
        var log = new List<string>
        {
            $"{PathOf(crate.transform)} at {crate.transform.position.ToString("F2")} yaw {yaw:F0}, {spot} (house front z {houseBounds.max.z:F2}, " +
            $"sides x {houseBounds.min.x:F2}..{houseBounds.max.x:F2})",
            injection,
            saved ? "scene saved" : "scene NOT saved",
        };
        if (crates.Count > 1)
            log.Add($"{crates.Count} crates in the scene: moved the first only");
        if (skipped.Count > 0)
            log.Add("skipped: " + string.Join("; ", skipped));
        return string.Join("\n", log);
    }

    // Default when the interact cast hits it (pickups use it too), else the lowest layer it hits.
    static int InteractLayer(LayerMask mask)
    {
        if ((mask.value & 1) != 0)
            return 0;
        for (int layer = 1; layer < 32; layer++)
            if ((mask.value & (1 << layer)) != 0)
                return layer;
        throw new Exception("the interact mask hits no layer");
    }

    // An empty root saved as the prefab, made in a preview scene so the open scene isn't touched.
    static void CreateEmpty(string path)
    {
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject root = ObjectFactory.CreateGameObject(preview, HideFlags.None, Path.GetFileNameWithoutExtension(path));
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
            if (!saved)
                throw new Exception("failed to save " + path);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }
    }

    // The vendor box nested as a child (our overrides only, the pack stays untouched); its own collider is turned off
    // because the root's box covers the cast, the player and bullets.
    static Transform Model(Transform root, GameObject model, List<string> log)
    {
        Transform child = root.Find(ModelName);
        if (child != null && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != model)
        {
            Object.DestroyImmediate(child.gameObject);
            child = null;
        }
        if (child == null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root);
            instance.name = ModelName;
            child = instance.transform;
            log.Add("nested " + model.name + " as " + ModelName);
        }
        child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        child.localScale = Vector3.one;
        foreach (Collider collider in child.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        return child;
    }

    // Stands the model on the root's origin; returns its bounds in the root's space.
    static Bounds Seat(Transform root, Transform model)
    {
        Bounds bounds = MeshBounds(root.worldToLocalMatrix, model);
        var lift = new Vector3(0f, -bounds.min.y, 0f);
        model.localPosition = lift;
        bounds.center += lift;
        return bounds;
    }

    // Bounds of every mesh under `of`, in the space `toFrame` maps world points into.
    static Bounds MeshBounds(Matrix4x4 toFrame, Transform of)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in of.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            Bounds mesh = filter.sharedMesh.bounds;
            Matrix4x4 toLocal = toFrame * filter.transform.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f);
                Vector3 point = toLocal.MultiplyPoint3x4(mesh.center + Vector3.Scale(mesh.extents, corner));
                if (any)
                    bounds.Encapsulate(point);
                else
                    bounds = new Bounds(point, Vector3.zero);
                any = true;
            }
        }
        if (!any)
            throw new Exception("no mesh under " + of.name);
        return bounds;
    }

    // Why the crate can't stand at `point` (null = free): the first hit below must be terrain, no other Home Base mesh may overlap it.
    // The house is excluded: its turned world box is far too big.
    static string Blocker(Vector3 point, float baseY, float reach, float height, Transform home, Transform house, Transform ignore, out Vector3 ground)
    {
        ground = default;
        var origin = new Vector3(point.x, baseY + ProbeHeight, point.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, ProbeHeight * 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        Collider first = null;
        foreach (RaycastHit hit in hits)
        {
            if (ignore != null && hit.collider.transform.IsChildOf(ignore))
                continue;
            first = hit.collider;
            ground = hit.point;
            break;
        }
        if (first == null)
            return "has no ground";
        if (first is not TerrainCollider)
            return "is on " + first.name;

        var box = new Bounds(ground + Vector3.up * (0.5f * height + 0.05f), new Vector3(2f * reach, height, 2f * reach));
        foreach (MeshRenderer renderer in home.GetComponentsInChildren<MeshRenderer>())
        {
            if (renderer.transform.IsChildOf(house) || (ignore != null && renderer.transform.IsChildOf(ignore)))
                continue;
            if (renderer.bounds.Intersects(box))
                return "overlaps " + renderer.name;
        }
        return null;
    }

    // VContainer injects the GameObjects listed on a scope when it builds it: how the scene-placed crate gets AudioService.
    // Entries left missing by a deleted crate (a map rebuild) are dropped.
    static string ListForInjection(Scene scene, GameObject crate)
    {
        GameLifetimeScope scope = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            scope = root.GetComponentInChildren<GameLifetimeScope>(true);
            if (scope != null)
                break;
        }
        if (scope == null)
            return "no GameLifetimeScope in the scene: the crate works but stays silent";

        var fields = new SerializedObject(scope);
        SerializedProperty list = fields.FindProperty("autoInjectGameObjects");
        int dropped = 0;
        for (int i = list.arraySize - 1; i >= 0; i--)
        {
            if (list.GetArrayElementAtIndex(i).objectReferenceValue != null)
                continue;
            list.DeleteArrayElementAtIndex(i);
            dropped++;
        }
        bool listed = false;
        for (int i = 0; i < list.arraySize && !listed; i++)
            listed = list.GetArrayElementAtIndex(i).objectReferenceValue == crate;
        if (!listed)
        {
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = crate;
        }
        fields.ApplyModifiedPropertiesWithoutUndo();
        return $"{scope.name} Auto Inject Game Objects: {(listed ? "already listed" : "added")}" + (dropped > 0 ? $", dropped {dropped} missing" : "");
    }

    static Transform Find(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == name)
                    return transform;
        return null;
    }

    // The house under the home base: a direct child named House*, else the first such descendant.
    static Transform House(Transform home)
    {
        foreach (Transform child in home)
            if (child.name.StartsWith(HousePrefix, StringComparison.Ordinal))
                return child;
        foreach (Transform transform in home.GetComponentsInChildren<Transform>(true))
            if (transform != home && transform.name.StartsWith(HousePrefix, StringComparison.Ordinal))
                return transform;
        return null;
    }

    static List<AmmoCrate> Crates(Scene scene)
    {
        var crates = new List<AmmoCrate>();
        foreach (GameObject root in scene.GetRootGameObjects())
            crates.AddRange(root.GetComponentsInChildren<AmmoCrate>(true));
        return crates;
    }

    static string PathOf(Transform transform) =>
        transform.parent == null ? transform.name : PathOf(transform.parent) + "/" + transform.name;

    static T Load<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("missing " + path);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
