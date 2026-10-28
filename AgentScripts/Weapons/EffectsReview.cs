using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.WorldTimeSystem;
using UnityEditor;
using UnityEngine;
using VContainer;

// Play-mode check of the shot effects: freezes the clock at an hour, fires real bullets (tracers) through BulletSystem
// from a fake muzzle with its flash, then pauses the editor a few frames later so a capture shows them in flight.
// Unpause with EditorApplication.isPaused = false.
public static class EffectsReview
{
    const string BulletEffectsPath = "Assets/_Game/Data/Weapons/BulletEffects.asset";
    static int _framesLeft;

    public static object Fire(float hour, int bullets, float spread, float pitch, int pauseAfterFrames)
    {
        IObjectResolver container = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var clock = container.Resolve<WorldClock>();
        clock.Freeze("EffectsReview");
        clock.SetTime(WorldTime.At(clock.Now.Day, hour));

        var effects = AssetDatabase.LoadAssetAtPath<BulletEffects>(BulletEffectsPath);
        var pool = container.Resolve<EffectPool>();
        var system = container.Resolve<BulletSystem>();
        Transform cam = Camera.main.transform;
        Vector3 muzzle = cam.position + cam.forward * 0.6f - cam.up * 0.18f + cam.right * 0.2f;
        pool.Spawn(effects.MuzzleFlash, muzzle, cam.rotation);
        for (int i = 0; i < bullets; i++)
        {
            float yaw = bullets == 1 ? 0f : Mathf.Lerp(-spread, spread, i / (bullets - 1f));
            Vector3 direction = Quaternion.AngleAxis(yaw, cam.up) * Quaternion.AngleAxis(pitch, cam.right) * cam.forward;
            system.Fire(new BulletShot
            {
                Origin = cam.position,
                VisualOrigin = muzzle,
                Direction = direction,
                Speed = 300f,
                Range = 150f,
                Layers = Physics.DefaultRaycastLayers,
                Ignore = Camera.main.transform.root,
                Effects = effects,
            });
        }

        _framesLeft = pauseAfterFrames;
        EditorApplication.update -= CountDown;
        EditorApplication.update += CountDown;
        return $"fired {bullets} at {clock.Now}";
    }

    // A 4x2 grid in front of the camera, top row then bottom, left to right: our MuzzleFlash, then Hovl's "Muzzle flash 1..7" (For bloom) with
    // throwaway URP copies of their built-in-pipeline materials. Each is frozen `time` seconds in, seen at 3/4.
    public static object MuzzleRow(float time, float spacing, float distance)
    {
        var effects = AssetDatabase.LoadAssetAtPath<BulletEffects>(BulletEffectsPath);
        Transform cam = Camera.main.transform;
        var prefabs = new System.Collections.Generic.List<GameObject> { effects.MuzzleFlash.gameObject };
        for (int i = 1; i <= 7; i++)
            prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_StoreAssets/VFX/Hovl Studio/Muzzle flashes/Prefabs/For bloom/Muzzle flash {i}.prefab"));

        var urp = new System.Collections.Generic.Dictionary<Material, Material>();
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < prefabs.Count; i++)
        {
            float x = (i % 4 - 1.5f) * spacing - 0.4f;
            float y = i < 4 ? 0.6f : -0.7f;
            Vector3 position = cam.position + cam.forward * distance + cam.right * x + cam.up * y;
            GameObject go = Object.Instantiate(prefabs[i], position, Quaternion.LookRotation(cam.right * 0.8f + cam.forward * 0.6f, cam.up));
            go.name = "Preview_" + prefabs[i].name;
            if (go.TryGetComponent(out PooledEffect pooled))
                Object.Destroy(pooled);
            foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int m = 0; m < mats.Length; m++)
                    if (mats[m] != null && !mats[m].shader.name.StartsWith("GAP_SG") && !mats[m].shader.name.StartsWith("Universal"))
                        mats[m] = Urp(mats[m], urp);
                r.sharedMaterials = mats;
            }
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Simulate(time, false, true, false);
                ps.Pause(false);
            }
            log.Append($"{i}:{prefabs[i].name} ");
        }
        return log.ToString();
    }

    // Our wrapped flash prefabs (Effects folder, ';'-separated names) in a row, frozen `time` s in, seen side-on (pointing right).
    public static object FlashGrid(string names, float time, float spacing, float distance)
    {
        Transform cam = Camera.main.transform;
        string[] list = names.Split(';');
        var log = new System.Text.StringBuilder();
        for (int i = 0; i < list.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Content/Weapons/_Shared/Effects/{list[i]}.prefab");
            float x = (i - (list.Length - 1) * 0.5f) * spacing;
            Vector3 position = cam.position + cam.forward * distance + cam.right * x;
            GameObject go = Object.Instantiate(prefab, position, Quaternion.LookRotation(cam.right, cam.up));
            go.name = "Preview_" + list[i];
            Object.Destroy(go.GetComponent<PooledEffect>());
            int alive = 0;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Simulate(time, false, true, false);
                ps.Pause(false);
                alive += ps.particleCount;
            }
            log.Append($"{i}:{list[i]} n={alive}; ");
        }
        return log.ToString();
    }

    static Material Urp(Material source, System.Collections.Generic.Dictionary<Material, Material> made)
    {
        if (made.TryGetValue(source, out Material copy))
            return copy;
        copy = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = source.name + "_URP" };
        copy.SetTexture("_BaseMap", source.GetTexture("_MainTex"));
        copy.SetColor("_BaseColor", source.GetColor("_Color"));
        copy.SetFloat("_Surface", 1f);
        copy.SetFloat("_Blend", 0f);
        copy.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        copy.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        copy.SetFloat("_ZWrite", 0f);
        copy.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        copy.SetOverrideTag("RenderType", "Transparent");
        copy.renderQueue = 3000;
        made.Add(source, copy);
        return copy;
    }

    public static object ClearRow()
    {
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude))
            if (t.parent == null && t.name.StartsWith("Preview_"))
            {
                Object.Destroy(t.gameObject);
                n++;
            }
        return n;
    }

    // Points BulletEffects at another muzzle flash prefab in the shared Effects folder. Saved on the asset and kept after
    // Play: set it back when done comparing (ShotEffectsSetup.All restores the chosen one).
    public static object SetMuzzle(string prefabName)
    {
        var effects = AssetDatabase.LoadAssetAtPath<BulletEffects>(BulletEffectsPath);
        var flash = AssetDatabase.LoadAssetAtPath<PooledEffect>($"Assets/_Game/Content/Weapons/_Shared/Effects/{prefabName}.prefab");
        var so = new SerializedObject(effects);
        so.FindProperty("_muzzleFlash").objectReferenceValue = flash;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(effects);
        return $"muzzle flash = {(flash != null ? flash.name : "missing")}";
    }

    // Night at `hour`, enemies switched off (this play session only), the named gun picked up.
    public static object Prepare(float hour, string gun)
    {
        IObjectResolver container = Object.FindAnyObjectByType<GameLifetimeScope>().Container;
        var clock = container.Resolve<WorldClock>();
        clock.Freeze("EffectsReview");
        clock.SetTime(WorldTime.At(clock.Now.Day, hour));
        foreach (var brain in Object.FindObjectsByType<Aipocalypse.Core.EnemySystem.EnemyBrain>(FindObjectsInactive.Exclude))
            brain.transform.root.gameObject.SetActive(false);
        Application.runInBackground = true;
        return PickUp(gun);
    }

    // Picks up the first world item whose name contains `name` (e.g. "AK47") as if the player pressed Interact on it.
    public static object PickUp(string name)
    {
        GameObject player = Object.FindAnyObjectByType<Aipocalypse.Core.PlayerSystem.Player>().gameObject;
        foreach (var item in Object.FindObjectsByType<Aipocalypse.Core.EquipmentSystem.WorldItem>(FindObjectsInactive.Exclude))
        {
            if (!item.name.Contains(name))
                continue;
            item.Interact(player);
            return $"picked up {item.name}";
        }
        return "not found";
    }

    // Releases, then holds a virtual gamepad's West button (Attack) for `holdFrames`, then pauses the editor `pauseAfterFrames` in.
    public static object Shoot(int holdFrames, int pauseAfterFrames)
    {
        var pad = UnityEngine.InputSystem.InputSystem.GetDevice<UnityEngine.InputSystem.Gamepad>()
            ?? UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
        int frame = 0;
        void Step()
        {
            frame++;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(pad, new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.West, frame > 1 && frame <= holdFrames + 1));
            if (frame < pauseAfterFrames)
                return;
            EditorApplication.update -= Step;
            EditorApplication.isPaused = true;
        }
        EditorApplication.update += Step;
        return $"shooting on {pad.name}";
    }

    static void CountDown()
    {
        if (--_framesLeft > 0)
            return;
        EditorApplication.update -= CountDown;
        EditorApplication.isPaused = true;
    }
}
