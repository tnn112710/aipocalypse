// MicroVerse terrain kit for the Unity CLI (`unity command run_script`, package com.unity.pipeline). Editor-only; keep it
// outside Assets/ so running it triggers no import or domain reload. Checked against MicroVerse 1.7.0 on Unity 6000.6.
// Usage: unity command run_script --file <absolute path>/MicroVerseKit.cs --entry MicroVerseKit.Status
// Entry points and argument order are documented in the unity-microverse skill's SKILL.md ("The kit").
using System;
using System.Collections.Generic;
using System.Linq;
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class MicroVerseKit
{
    // Scene overview: MicroVerse state, terrains and the stamp stack in evaluation (hierarchy) order.
    public static object Status()
    {
        var scene = SceneManager.GetActiveScene();
        var mv = MicroVerse.instance;
        if (mv == null) return new { scene = scene.path, scene.isDirty, microVerse = (object)null, terrains = Terrain.activeTerrains.Select(DescribeTerrain).ToArray() };
        mv.SyncTerrainList();
        var stamps = mv.GetComponentsInChildren<Stamp>(true).Where(s => !(s is HeightStamp h && h.hideFlags.HasFlag(HideFlags.HideInHierarchy)));
        return new
        {
            scene = scene.path,
            scene.isDirty,
            microVerse = new { mv.name, mv.enabled, modifying = mv.IsModifyingTerrain, heightSynced = mv.IsHeightSyncd, search = mv.options.settings.terrainSearchMethod.ToString(), explicitTerrains = mv.explicitTerrains?.Length ?? 0 },
            terrains = mv.terrains.Select(DescribeTerrain).ToArray(),
            stamps = stamps.Select(DescribeStamp).ToArray()
        };
    }

    // Height stamp library: every MicroVerse ContentCollection of type Height (Rowlan packs, MicroVerse examples...).
    // filter matches pack name or texture name (case-insensitive, "" = all).
    public static object Library(string filter)
    {
        filter = filter ?? "";
        var packs = AssetDatabase.FindAssets("t:ContentCollection")
            .Select(g => AssetDatabase.LoadAssetAtPath<ContentCollection>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.contentType == ContentType.Height);
        return packs.Select(c => new
        {
            pack = c.packName,
            c.author,
            collection = AssetDatabase.GetAssetPath(c),
            stamps = Contents(c).Select(cd => string.IsNullOrEmpty(cd.stamp) ? null : AssetDatabase.GUIDToAssetPath(cd.stamp))
                .Where(p => !string.IsNullOrEmpty(p)).Distinct()
                .Where(p => Contains(c.packName, filter) || Contains(System.IO.Path.GetFileNameWithoutExtension(p), filter))
                .ToArray()
        }).Where(p => p.stamps.Length > 0).ToArray();
    }

    // Content Browser presets (prefab entries) of a type: Texture, Vegetation, Biomes, Objects, Global, Paths, Height.
    // Only packs the browser would show as installed (no unresolved ContentAd) are listed. filter matches pack or prefab name.
    public static object Presets(string contentType, string filter)
    {
        var type = (ContentType)Enum.Parse(typeof(ContentType), contentType, true);
        // The browser hides a collection only when an unresolved ad shares its id; some BK ads use a store URL as id
        // (e.g. Highlands), so also match pack name + type, or presets of uninstalled art packs slip through.
        var missing = AssetDatabase.FindAssets("t:ContentAd")
            .Select(g => AssetDatabase.LoadAssetAtPath<ContentAd>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(a => a != null && a.requireInstalledObject && a.installedObject == null).ToList();
        var blockedIds = new HashSet<string>(missing.Select(a => a.id));
        var blockedPacks = new HashSet<string>(missing.Select(a => a.packName + "|" + a.contentType));
        filter = filter ?? "";
        return AssetDatabase.FindAssets("t:ContentCollection")
            .Select(g => AssetDatabase.LoadAssetAtPath<ContentCollection>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.contentType == type && !blockedIds.Contains(c.id) && !blockedPacks.Contains(c.packName + "|" + c.contentType))
            .Select(c => new
            {
                pack = c.packName,
                c.author,
                prefabs = Contents(c).Where(cd => cd.prefab != null).Select(cd => AssetDatabase.GetAssetPath(cd.prefab))
                    .Where(p => Contains(c.packName, filter) || Contains(System.IO.Path.GetFileNameWithoutExtension(p), filter)).ToArray()
            })
            .Where(p => p.prefabs.Length > 0).ToArray();
    }

    // Drops a Content Browser preset prefab (path from Presets()) like the browser does: instantiated, fully unpacked,
    // placed on the terrain surface at (x,z), footprint sizeX x sizeZ (0 = keep the prefab's scale).
    // Tree/Object stamps get the default poisson disk, which only their inspector assigns otherwise (null = spawns nothing).
    // Drop actions only run with runDropActions: BK ones set the skybox and fog or replace every terrain's material.
    public static object AddPreset(string prefabPath, string group, float x, float z, float sizeX, float sizeZ, float rotationY, bool runDropActions = false)
    {
        var mv = RequireMicroVerse();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new ArgumentException("No prefab at " + prefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Undo.RegisterCreatedObjectUndo(go, "Add preset");
        go.transform.SetParent(EnsureGroup(mv, group), false);
        var ground = new Vector3(x, 0, z);
        var terrain = TerrainAt(ground);
        ground.y = terrain != null ? terrain.SampleHeight(ground) + terrain.transform.position.y : 0;
        go.transform.position = ground;
        go.transform.rotation = Quaternion.Euler(0, rotationY, 0);
        if (sizeX > 0 && sizeZ > 0) go.transform.localScale = new Vector3(sizeX, go.transform.localScale.y, sizeZ);
        var disk = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.jbooth.microverse/Data/microverse_default_poissondisk.poissondisk");
        foreach (var tree in go.GetComponentsInChildren<TreeStamp>(true)) if (tree.poissonDisk == null) { tree.poissonDisk = disk; EditorUtility.SetDirty(tree); }
        foreach (var obj in go.GetComponentsInChildren<ObjectStamp>(true)) if (obj.poissonDisk == null) { obj.poissonDisk = disk; EditorUtility.SetDirty(obj); }
        bool destroy = false;
        var actions = go.GetComponentsInChildren<JBooth.MicroVerseCore.Browser.IContentBrowserDropAction>(true);
        foreach (var action in runDropActions ? actions : Array.Empty<JBooth.MicroVerseCore.Browser.IContentBrowserDropAction>())
        {
            action.Execute(out bool d);
            destroy |= d;
        }
        if (destroy)
        {
            UnityEngine.Object.DestroyImmediate(go);
            mv.Invalidate();
            return new { preset = prefabPath, droppedAndDestroyed = true };
        }
        mv.Invalidate();
        return new
        {
            path = PathUnderRoot(go.transform),
            position = go.transform.position.ToString("0.#"),
            scale = go.transform.lossyScale.ToString("0.#"),
            stamps = go.GetComponentsInChildren<Stamp>(true).Select(s => s.GetType().Name + ": " + s.name).ToArray(),
            skippedDropActions = runDropActions ? new string[0] : actions.Select(a => a.GetType().Name).ToArray()
        };
    }

    // Terrain layers available for texture stamps. filter matches name or path.
    public static object Layers(string filter)
    {
        return AssetDatabase.FindAssets("t:TerrainLayer").Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Contains(p, filter ?? "")).OrderBy(p => p).ToArray();
    }

    // Creates a MicroVerse root with a tilesX x tilesZ grid of fresh terrains (TerrainData assets under dataFolder).
    // Details use coverage scatter mode (what the skill's density numbers assume) at detailResolution per tile.
    public static object Setup(string dataFolder, int tilesX, int tilesZ, float tileSize, float height, int heightmapResolution, int alphamapResolution, int detailResolution = 1024)
    {
        if (MicroVerse.instance != null) throw new InvalidOperationException("Scene already has a MicroVerse (" + MicroVerse.instance.name + ").");
        dataFolder = (dataFolder ?? "").Replace('\\', '/').TrimEnd('/');
        if (!dataFolder.StartsWith("Assets/")) throw new ArgumentException("dataFolder must be a project folder under Assets/ (e.g. Assets/Maps/<Map>/Terrain).");
        EnsureFolder(dataFolder);
        var root = new GameObject("MicroVerse");
        Undo.RegisterCreatedObjectUndo(root, "Create MicroVerse");
        var mv = root.AddComponent<MicroVerse>();
        mv.enabled = false;
        var material = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial : null;
        var created = new List<string>();
        for (int z = 0; z < tilesZ; z++)
            for (int x = 0; x < tilesX; x++)
            {
                var data = new TerrainData { heightmapResolution = heightmapResolution };
                data.size = new Vector3(tileSize, height, tileSize);
                data.baseMapResolution = 1024;
                data.alphamapResolution = alphamapResolution;
                data.SetDetailScatterMode(DetailScatterMode.CoverageMode);
                data.SetDetailResolution(detailResolution, data.detailResolutionPerPatch);
                var path = AssetDatabase.GenerateUniqueAssetPath(dataFolder + "/Terrain_" + x + "_" + z + ".asset");
                AssetDatabase.CreateAsset(data, path);
                var go = Terrain.CreateTerrainGameObject(data);
                go.name = "Terrain_" + x + "_" + z;
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = new Vector3(x * tileSize, 0, z * tileSize);
                var t = go.GetComponent<Terrain>();
                t.allowAutoConnect = true;
                t.groupingID = 0;
                if (material != null) t.materialTemplate = material;
                created.Add(path);
            }
        mv.SyncTerrainList();
        mv.enabled = true;
        EditorSceneManager.MarkSceneDirty(root.scene);
        return new { root = root.name, terrains = created, material = material != null ? material.name : "none (assign a terrain material)" };
    }

    // Adds a HeightStamp. stamp = texture name / asset path from Library(), or "" for a flat pad at y.
    // group = parent path under the MicroVerse root (created if missing, e.g. "Height/Mountains").
    // mode = HeightStamp.CombineMode name (Max, Add, Subtract, Override, Min, Blend...). falloff = Box | Range | Global.
    // Footprint is sizeX x sizeZ metres; texture white = y + height metres.
    public static object AddHeight(string stamp, string group, float x, float y, float z, float sizeX, float height, float sizeZ, float rotationY, string mode, string falloff, float edge)
    {
        var mv = RequireMicroVerse();
        Texture2D tex = string.IsNullOrEmpty(stamp) ? null : FindStampTexture(stamp);
        var parent = EnsureGroup(mv, group);
        var go = new GameObject(tex != null ? tex.name + " (Height Stamp)" : "Pad (Height Stamp)");
        Undo.RegisterCreatedObjectUndo(go, "Add height stamp");
        go.transform.SetParent(parent, false);
        var hs = go.AddComponent<HeightStamp>();   // OnEnable may zero position.y, so place after adding
        hs.stamp = tex;
        hs.mode = (HeightStamp.CombineMode)Enum.Parse(typeof(HeightStamp.CombineMode), mode, true);
        hs.stampVersion = 1;                       // skip MicroVerse's one-time noise rescale on non-1000m terrains
        ApplyFalloff(hs.falloff, falloff, edge);
        go.transform.position = new Vector3(x, y, z);
        go.transform.rotation = Quaternion.Euler(0, rotationY, 0);
        go.transform.localScale = new Vector3(sizeX, height, sizeZ);
        EditorUtility.SetDirty(hs);
        mv.Invalidate();
        return DescribeStamp(hs);
    }

    // Adds a HeightAreaEffectStamp (Terrace | Beach | RemapCurve | Noise) acting on the heights of stamps above it.
    public static object AddHeightEffect(string effect, string group, float x, float y, float z, float sizeX, float sizeZ, string falloff, float edge)
    {
        var mv = RequireMicroVerse();
        var go = new GameObject(effect + " (Height Effect)");
        Undo.RegisterCreatedObjectUndo(go, "Add height effect");
        go.transform.SetParent(EnsureGroup(mv, group), false);
        var s = go.AddComponent<HeightAreaEffectStamp>();
        s.effectType = (HeightAreaEffectStamp.EffectType)Enum.Parse(typeof(HeightAreaEffectStamp.EffectType), effect, true);
        s.stampVersion = 1;
        ApplyFalloff(s.falloff, falloff, edge);
        go.transform.position = new Vector3(x, y, z);
        go.transform.localScale = new Vector3(sizeX, 1, sizeZ);
        EditorUtility.SetDirty(s);
        mv.Invalidate();
        return DescribeStamp(s);
    }

    // Adds a TextureStamp painting a TerrainLayer (path or name from Layers()). falloff Global = whole terrain (base layer).
    // Height/slope filters: tune afterwards with Edit(path, json) on filterSet.
    public static object AddTexture(string layer, string group, float x, float z, float sizeX, float sizeZ, string falloff, float edge)
    {
        var mv = RequireMicroVerse();
        var tl = FindAsset<TerrainLayer>(layer, "t:TerrainLayer");
        var go = new GameObject(tl.name + " (Texture Stamp)");
        Undo.RegisterCreatedObjectUndo(go, "Add texture stamp");
        go.transform.SetParent(EnsureGroup(mv, group), false);
        var s = go.AddComponent<TextureStamp>();
        s.layer = tl;
        s.stampVersion = 1;
        s.filterSet.version = 2;                   // slope values set from code are taken literally (no legacy x1.58 migration)
        ApplyFalloff(s.filterSet.falloffFilter, falloff, edge);
        go.transform.position = new Vector3(x, 0, z);
        go.transform.localScale = new Vector3(sizeX, 1, sizeZ);
        EditorUtility.SetDirty(s);
        mv.Invalidate(null, MicroVerse.InvalidateType.Splats);
        return DescribeStamp(s);
    }

    // Moves/scales/rotates any object under the MicroVerse root. Path is relative to the root ("Height/Hill (Height Stamp)").
    public static object Place(string path, float x, float y, float z, float sizeX, float sizeY, float sizeZ, float rotationY)
    {
        var mv = RequireMicroVerse();
        var t = FindUnder(mv, path);
        var oldBounds = TerrainUtil.GetBounds(t);
        Undo.RecordObject(t, "Place stamp");
        t.position = new Vector3(x, y, z);
        t.rotation = Quaternion.Euler(0, rotationY, 0);
        t.localScale = new Vector3(sizeX, sizeY, sizeZ);
        var b = TerrainUtil.GetBounds(t);
        b.Encapsulate(oldBounds);
        mv.Invalidate(b);
        var stamp = t.GetComponent<Stamp>();
        return stamp != null ? DescribeStamp(stamp) : (object)t.name;
    }

    // Overwrites serialized fields of the first Stamp on the object with JSON (EditorJsonUtility format), e.g.
    // {"mode":3,"erosion":120,"falloff":{"filterType":2,"falloffRange":{"x":0.4,"y":1.0}}}. Enums are ints.
    public static object Edit(string path, string json)
    {
        var mv = RequireMicroVerse();
        var stamp = FindUnder(mv, path).GetComponent<Stamp>();
        if (stamp == null) throw new ArgumentException("No Stamp on " + path);
        Undo.RecordObject(stamp, "Edit stamp");
        // EditorJsonUtility wraps component JSON in {"MonoBehaviour":{...}} and silently ignores unwrapped input.
        if (!json.TrimStart().StartsWith("{\"MonoBehaviour\"")) json = "{\"MonoBehaviour\":" + json + "}";
        EditorJsonUtility.FromJsonOverwrite(json, stamp);
        if (stamp is HeightStamp hs) hs.ClearRemapCurve();
        EditorUtility.SetDirty(stamp);
        mv.Invalidate();
        return EditorJsonUtility.ToJson(stamp, true);
    }

    // Dumps a stamp's serialized fields (to read before Edit).
    public static object Inspect(string path)
    {
        var stamp = FindUnder(RequireMicroVerse(), path).GetComponent<Stamp>();
        return stamp == null ? "No Stamp on " + path : EditorJsonUtility.ToJson(stamp, true);
    }

    public static object SetEnabled(string path, bool enabled)
    {
        var mv = RequireMicroVerse();
        var go = FindUnder(mv, path).gameObject;
        Undo.RecordObject(go, "Toggle stamp");
        go.SetActive(enabled);
        mv.Invalidate();
        return new { path, enabled };
    }

    // Moves an object to a sibling index (hierarchy order = evaluation order; for texture stamps, lower wins).
    public static object Reorder(string path, int siblingIndex)
    {
        var mv = RequireMicroVerse();
        var t = FindUnder(mv, path);
        Undo.SetSiblingIndex(t, siblingIndex, "Reorder stamp");
        mv.Invalidate();
        return new { path, index = t.GetSiblingIndex() };
    }

    public static object Remove(string path)
    {
        var mv = RequireMicroVerse();
        var t = FindUnder(mv, path);
        var b = TerrainUtil.GetBounds(t);
        Undo.DestroyObjectImmediate(t.gameObject);
        mv.Invalidate(b);
        return new { removed = path };
    }

    // Regenerates synchronously and writes heights to the CPU copy (colliders, SampleHeight). bake=true also
    // syncs alphamaps/holes/vegetation into the TerrainData assets and saves them (the inspector's "Sync to Terrain").
    public static object Rebuild(bool bake)
    {
        var mv = RequireMicroVerse();
        if (!mv.enabled) throw new InvalidOperationException("MicroVerse component is disabled (baked/hand-edit mode). Enable it first if regenerating from stamps is intended.");
        mv.SyncTerrainList();
        mv.Invalidate();
        mv.Modify(true, true);
        // Modify doesn't clear the pending Invalidate, which would start a second, async rebuild on the next editor tick
        // (double cost, and a SaveScene right after could catch vegetation mid-respawn).
        typeof(MicroVerse).GetField("needUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(mv, false);
        if (bake)
        {
            mv.SaveBackToTerrain(true);
            SaveTerrainData(mv);
        }
        EditorSceneManager.MarkSceneDirty(mv.gameObject.scene);
        return new { baked = bake, terrains = mv.terrains.Select(DescribeHeights).ToArray() };
    }

    // Height grid between two corners (world XZ), steps x steps samples. Run Rebuild first so the CPU copy is current.
    public static object Sample(float x0, float z0, float x1, float z1, int steps)
    {
        var rows = new List<string>();
        for (int j = steps - 1; j >= 0; j--)
        {
            var row = new List<string>();
            for (int i = 0; i < steps; i++)
            {
                var p = new Vector3(Mathf.Lerp(x0, x1, steps == 1 ? 0.5f : i / (steps - 1f)), 0, Mathf.Lerp(z0, z1, steps == 1 ? 0.5f : j / (steps - 1f)));
                var t = TerrainAt(p);
                row.Add(t == null ? "  -  " : (t.SampleHeight(p) + t.transform.position.y).ToString("0.0").PadLeft(6));
            }
            rows.Add(string.Join(" ", row));
        }
        return new { northUp = rows };
    }

    // Puts the Scene-view camera exactly at (x,y,z) looking along pitch/yaw, for `unity command screenshot --view scene`.
    // aboveGround: y is metres above the terrain (1.7 = a typical FPS eye). Gizmos off, sky/fog/effects on.
    // hideRoots: comma-separated scene root names to hide from the view (e.g. "HUD,Player"; scene visibility is per user,
    // not saved in the scene).
    public static object Look(float x, float y, float z, float pitch, float yaw, float fov = 60, bool aboveGround = true, string hideRoots = "")
    {
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new InvalidOperationException("No Scene view open.");
        var terrain = TerrainAt(new Vector3(x, 0, z));
        if (aboveGround && terrain != null) y += terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
        view.orthographic = false;
        view.drawGizmos = false;
        view.sceneLighting = true;
        var state = view.sceneViewState;
        state.showSkybox = true; state.showFog = true; state.showImageEffects = true;
        var cs = view.cameraSettings;
        cs.fieldOfView = fov; cs.dynamicClip = false; cs.nearClip = 0.3f; cs.farClip = 4000f;
        var hide = new HashSet<string>((hideRoots ?? "").Split(',').Select(s => s.Trim()).Where(s => s.Length > 0));
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (hide.Contains(root.name)) SceneVisibilityManager.instance.Hide(root, true);
        // SceneView.LookAt puts the camera a fov-dependent distance behind the pivot; aligning to a temp transform doesn't
        var tmp = new GameObject("Look (temp)") { hideFlags = HideFlags.HideAndDontSave };
        tmp.transform.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(pitch, yaw, 0));
        view.AlignViewToObject(tmp.transform);
        UnityEngine.Object.DestroyImmediate(tmp);
        SceneView.RepaintAll();
        return new { camera = new Vector3(x, y, z).ToString("0.0"), pitch, yaw, fov };
    }

    // Frames the whole MicroVerse terrain set from above at an angle.
    public static object ViewAll(float pitch, float yaw)
    {
        var mv = RequireMicroVerse();
        var b = TerrainUtil.ComputeTerrainBounds(mv.terrains);
        var view = SceneView.lastActiveSceneView;
        if (view == null) throw new InvalidOperationException("No Scene view open.");
        view.orthographic = false;
        view.LookAtDirect(b.center, Quaternion.Euler(pitch, yaw, 0), Mathf.Max(b.size.x, b.size.z) * 0.6f);
        view.sceneLighting = true;
        view.Repaint();
        return new { center = b.center.ToString(), size = b.size.ToString() };
    }

    // Saves the active scene and the terrain assets. path = "" keeps the scene's path; a never-saved scene needs one (Assets/.../<Map>.unity).
    public static object SaveScene(string path)
    {
        var scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(path)) path = scene.path;
        if (string.IsNullOrEmpty(path)) throw new InvalidOperationException("Scene has never been saved; pass a path under Assets/ ending in .unity.");
        if (MicroVerse.instance != null && MicroVerse.instance.IsModifyingTerrain) throw new InvalidOperationException("MicroVerse is still rebuilding; wait until Status shows modifying: false.");
        EnsureFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/'));
        // MicroVerse syncs heights back to the terrains on scene save
        if (!EditorSceneManager.SaveScene(scene, path)) throw new InvalidOperationException("Saving " + path + " failed (see the console).");
        if (MicroVerse.instance != null) SaveTerrainData(MicroVerse.instance);
        return new { saved = scene.path };
    }

    // ---- helpers ----

    // Saves only the terrain assets: AssetDatabase.SaveAssets() would also write every other dirty asset in the
    // editor (e.g. vendor materials Unity upgraded in memory), which must not happen behind the user's back.
    static void SaveTerrainData(MicroVerse mv)
    {
        foreach (var t in mv.terrains)
        {
            EditorUtility.SetDirty(t.terrainData);
            AssetDatabase.SaveAssetIfDirty(t.terrainData);
        }
    }

    static MicroVerse RequireMicroVerse()
    {
        var mv = MicroVerse.instance;
        if (mv == null) throw new InvalidOperationException("No MicroVerse in the open scene. Run Setup first.");
        return mv;
    }

    static Transform EnsureGroup(MicroVerse mv, string group)
    {
        var parent = mv.transform;
        if (string.IsNullOrEmpty(group)) return parent;
        foreach (var part in group.Split('/'))
        {
            var child = parent.Find(part);
            if (child == null)
            {
                child = new GameObject(part).transform;
                Undo.RegisterCreatedObjectUndo(child.gameObject, "Create stamp group");
                child.SetParent(parent, false);
            }
            parent = child;
        }
        return parent;
    }

    static Transform FindUnder(MicroVerse mv, string path)
    {
        var t = mv.transform.Find(path);
        if (t == null) throw new ArgumentException("Nothing at '" + path + "' under " + mv.name + ". Paths are relative to the MicroVerse root; see Status().");
        return t;
    }

    static void ApplyFalloff(FalloffFilter f, string type, float edge)
    {
        f.filterType = (FalloffFilter.FilterType)Enum.Parse(typeof(FalloffFilter.FilterType), string.IsNullOrEmpty(type) ? "Box" : type, true);
        // edge: Box -> falloffRange.y (1 = hard edge, lower = softer); Range -> falloffRange.x, radius (0..1) where the radial fade starts.
        f.falloffRange = f.filterType == FalloffFilter.FilterType.Box ? new Vector2(0.8f, Mathf.Clamp01(edge)) : new Vector2(Mathf.Clamp01(edge), 1f);
    }

    static Texture2D FindStampTexture(string query)
    {
        if (query.StartsWith("Assets/") || query.StartsWith("Packages/"))
        {
            var direct = AssetDatabase.LoadAssetAtPath<Texture2D>(query);
            if (direct == null) throw new ArgumentException("No texture at " + query);
            return direct;
        }
        var candidates = AssetDatabase.FindAssets("t:ContentCollection")
            .Select(g => AssetDatabase.LoadAssetAtPath<ContentCollection>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(c => c != null && c.contentType == ContentType.Height)
            .SelectMany(c => Contents(c).Select(cd => AssetDatabase.GUIDToAssetPath(cd.stamp)))
            .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        var exact = candidates.Where(p => string.Equals(System.IO.Path.GetFileNameWithoutExtension(p), query, StringComparison.OrdinalIgnoreCase)).ToList();
        var hits = exact.Count > 0 ? exact : candidates.Where(p => Contains(p, query)).ToList();
        if (hits.Count == 0) throw new ArgumentException("No height stamp matches '" + query + "'. Use Library(\"\") to list them.");
        if (hits.Count > 1) throw new ArgumentException("'" + query + "' is ambiguous: " + string.Join(", ", hits.Take(10)));
        return AssetDatabase.LoadAssetAtPath<Texture2D>(hits[0]);
    }

    static T FindAsset<T>(string query, string search) where T : UnityEngine.Object
    {
        var direct = AssetDatabase.LoadAssetAtPath<T>(query);
        if (direct != null) return direct;
        var paths = AssetDatabase.FindAssets(search).Select(AssetDatabase.GUIDToAssetPath).ToList();
        var exact = paths.Where(p => string.Equals(System.IO.Path.GetFileNameWithoutExtension(p), query, StringComparison.OrdinalIgnoreCase)).ToList();
        var hits = exact.Count > 0 ? exact : paths.Where(p => Contains(p, query)).ToList();
        if (hits.Count != 1) throw new ArgumentException(hits.Count == 0 ? "No " + typeof(T).Name + " matches '" + query + "'" : "'" + query + "' is ambiguous: " + string.Join(", ", hits.Take(10)));
        return AssetDatabase.LoadAssetAtPath<T>(hits[0]);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var split = path.LastIndexOf('/');
        EnsureFolder(path.Substring(0, split));
        AssetDatabase.CreateFolder(path.Substring(0, split), path.Substring(split + 1));
    }

    static bool Contains(string s, string part) => s != null && s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;

    // ContentCollection.contents isn't initialised, so an empty collection would throw
    static IEnumerable<ContentData> Contents(ContentCollection c) => c.contents ?? Enumerable.Empty<ContentData>();

    static Terrain TerrainAt(Vector3 p)
    {
        foreach (var t in Terrain.activeTerrains)
        {
            var o = t.transform.position;
            var s = t.terrainData.size;
            if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z) return t;
        }
        return null;
    }

    static string PathUnderRoot(Transform t)
    {
        var mv = MicroVerse.instance;
        var parts = new List<string>();
        for (var c = t; c != null && (mv == null || c != mv.transform); c = c.parent) parts.Insert(0, c.name);
        return string.Join("/", parts);
    }

    static object DescribeTerrain(Terrain t)
    {
        var d = t.terrainData;
        return new
        {
            t.name,
            position = t.transform.position.ToString(),
            size = d.size.ToString(),
            heightmap = d.heightmapResolution,
            alphamap = d.alphamapResolution,
            layers = d.terrainLayers.Where(l => l != null).Select(l => l.name).ToArray(),
            t.allowAutoConnect,
            material = t.materialTemplate != null ? t.materialTemplate.name : null,
            data = AssetDatabase.GetAssetPath(d)
        };
    }

    static object DescribeStamp(Stamp s)
    {
        var t = s.transform;
        string detail = s switch
        {
            HeightStamp h => h.mode + " " + (h.stamp != null ? h.stamp.name : "flat pad") + " falloff " + h.falloff.filterType + " " + h.falloff.falloffRange,
            HeightAreaEffectStamp e => e.effectType + " falloff " + e.falloff.filterType,
            TextureStamp tx => (tx.layer != null ? tx.layer.name : "no layer") + " falloff " + tx.filterSet.falloffFilter.filterType + " weight " + tx.filterSet.weight,
            _ => ""
        };
        return new
        {
            path = PathUnderRoot(t),
            type = s.GetType().Name,
            active = s.isActiveAndEnabled,
            detail,
            position = t.position.ToString("0.#"),
            scale = t.lossyScale.ToString("0.#"),
            rotationY = Mathf.Round(t.eulerAngles.y)
        };
    }

    static object DescribeHeights(Terrain t)
    {
        var d = t.terrainData;
        var r = d.heightmapResolution;
        var h = d.GetHeights(0, 0, r, r);
        float min = 1, max = 0;
        foreach (var v in h) { if (v < min) min = v; if (v > max) max = v; }
        return new
        {
            t.name, minHeight = min * d.size.y, maxHeight = max * d.size.y, sizeY = d.size.y,
            layers = d.terrainLayers.Where(l => l != null).Select(l => l.name).ToArray(),
            trees = d.treeInstanceCount, detailPrototypes = d.detailPrototypes.Length,
            data = AssetDatabase.GetAssetPath(d)
        };
    }
}
