using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Editor.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Builds the new guns of the design spec's roster (docs/superpowers/specs/2026-11-14-gun-system-design.md, section 6) as
// block-outs from Castle Bravo models: a Gun_Base variant without clips (it poses, kicks and shoots), its aim pose worked
// out from the sight height, hand grips copied from a reference gun of its class, the world model, and the item asset
// copied from that reference gun. The AK-74M is instead a variant of the AK47 prefab with the AK-74M's meshes, so it keeps
// the AK47's clips (the spec's experiment: do same-layout models reuse clips?). Also the two new ammo types.
// Full order: GunBlockoutSetup.All, AttachmentSetup.Mounts (part mounts), GunBlockoutSetup.Ak74m again (Mounts rebuilds the
// AK47's mount nodes, which drops the variant's overrides on them), AttachmentSetup.GunIcons, RealisticGunStats.Apply (first
// stats), WeaponLootSetup.Apply, WeaponsTestSetup.Build.
// Run: unity command script run --file AgentScripts/Weapons/GunBlockoutSetup.cs --entry GunBlockoutSetup.All
public static class GunBlockoutSetup
{
    const string Weapons = "Assets/_Game/Content/Weapons/";
    const string AmmoDir = "Assets/_Game/Content/Weapons/_Shared/Ammo/";
    const string GunBase = "Assets/_Game/_Core/WeaponSystem/Prefabs/Gun_Base.prefab";
    const string AmmoConfigPath = "Assets/_Game/Data/Weapons/AmmoConfig.asset";
    const string LensScope = "Assets/_Game/Content/Weapons/_Shared/Optics/Lens_Scope.mat";
    const string SniperFire = "Assets/_Game/Data/Audio/Guns/Fire_Sniper1Fire.asset";
    const string AmmoIcons = "Assets/Synty/InterfaceApocalypseHUD/Sprites/Icons_Ammo/";
    const string CB1 = "Assets/_StoreAssets/Weapons/Castle Bravo/Low Poly Weapon Bundle Pack 1/Prefabs/Weapons/";
    const string CB3 = "Assets/_StoreAssets/Weapons/Castle Bravo/Low Poly Weapon Bundle Pack 3/Prefabs/Weapons/";
    const string CB5 = "Assets/_StoreAssets/Weapons/Castle Bravo/Low Poly Weapon Bundle Pack 5/Prefabs/Low Poly Series V 4/Weapons/";
    // The player's held-item hand anchor, from the eye (Player prefab: HeldItems/Hand): an aim pose puts the sight line on
    // the eye's axis by sitting the model this much lower than the anchor's height.
    const float HandHeight = 0.21f;
    const float AimX = -0.17f;

    sealed class Gun
    {
        public string Name, Key, Source, Reference, Ammo;
        // Height of the iron sights' line (or a built-in scope's lens) in the model's space; Castle Bravo models point +Z.
        public float Sight;
        // Shots leave here (the barrel's tip, or a built-in muzzle device's).
        public Vector3 Muzzle;
        // The support hand's grip point; null = a pistol's two-handed hold, copied from the reference relative to the right hand.
        public Vector3? Left;
        public FireMode? Mode;
        public ReloadType? Reload;
        public float? CycleTime, AimZoom;
        // Metres the aim pose brings the model closer to the eye than the reference does (negative = further): how far its rear
        // sight or eyepiece sits from the reference gun's.
        public float AimCloser;
        public bool SniperShot, BoxReload;
        // A built-in scope: this lens gets the reticle lens material.
        public string ScopeLens;
        // A built-in tube scope: its sight window's radius (m; see AttachmentSetup.Windows), 0 = none.
        public float ScopeWindow;
    }

    // Sight heights, muzzles and hand points read off each model's mesh bounds (first guesses to tune in the prefab).
    static readonly Gun[] Every =
    {
        new() { Name = "Glock17", Key = "item.glock17", Source = CB1 + "Pistol Weapons/Pistol_B.prefab", Reference = "Pistol",
            Sight = 0.062f, Muzzle = new Vector3(0f, 0.040f, 0.153f) },
        new() { Name = "DesertEagle", Key = "item.desert_eagle", Source = CB1 + "Pistol Weapons/Pistol_D.prefab", Reference = "Pistol",
            Ammo = "MagnumAmmo", Sight = 0.062f, Muzzle = new Vector3(0f, 0.034f, 0.204f) },
        new() { Name = "Uzi", Key = "item.uzi", Source = CB3 + "Weapons_SMG/SMG_M.prefab", Reference = "MP5",
            Sight = 0.112f, Muzzle = new Vector3(0f, 0.042f, 0.380f), Left = new Vector3(0.007f, -0.010f, 0.170f), AimCloser = 0.16f },
        new() { Name = "M1Carbine", Key = "item.m1_carbine", Source = CB5 + "WWII_Rifle_A.prefab", Reference = "AK47",
            Mode = FireMode.SemiAuto, Sight = 0.095f, Muzzle = new Vector3(0f, 0.065f, 0.785f), Left = new Vector3(0.007f, 0.020f, 0.380f),
            AimCloser = -0.06f },
        new() { Name = "M4", Key = "item.m4", Source = CB1 + "AR Weapons/AR_A.prefab", Reference = "AK47",
            Sight = 0.178f, Muzzle = new Vector3(0f, 0.080f, 0.765f), Left = new Vector3(0.007f, 0.069f, 0.330f), AimCloser = -0.18f },
        new() { Name = "FAL", Key = "item.fal", Source = CB3 + "Weapons_DMR/DMR_L.prefab", Reference = "AK47", Ammo = "HeavyAmmo",
            Mode = FireMode.SemiAuto, Sight = 0.120f, Muzzle = new Vector3(0f, 0.053f, 0.838f), Left = new Vector3(0.007f, 0.063f, 0.400f),
            AimCloser = -0.22f },
        new() { Name = "HuntingRifle", Key = "item.hunting_rifle", Source = CB1 + "Recon Weapons/Recon_E.prefab", Reference = "PumpShotgun",
            Ammo = "HeavyAmmo", Sight = 0.135f, Muzzle = new Vector3(0f, 0.070f, 1.028f), Left = new Vector3(0.007f, 0.030f, 0.420f),
            CycleTime = 1f, AimZoom = 0.4f, SniperShot = true, ScopeLens = "Recon_E_Optic/Recon_E_Optic_Lens", AimCloser = -0.06f,
            ScopeWindow = 0.0265f },
        new() { Name = "AA12", Key = "item.aa12", Source = CB1 + "ShotGun Weapons/ShotGun_A.prefab", Reference = "PumpShotgun",
            Mode = FireMode.Auto, Reload = ReloadType.Magazine, CycleTime = 0f, BoxReload = true, Sight = 0.180f,
            Muzzle = new Vector3(0f, 0.055f, 0.568f), Left = new Vector3(0.007f, 0.045f, 0.330f), AimCloser = -0.04f },
        new() { Name = "RPD", Key = "item.rpd", Source = CB1 + "LMG Weapons/LMG_A.prefab", Reference = "AK47",
            Sight = 0.120f, Muzzle = new Vector3(0f, 0.046f, 0.983f), Left = new Vector3(0.007f, 0.020f, 0.385f), AimCloser = -0.02f },
    };

    // Parked: not in the game for now (user, 2026-11-16). Their rows stay; take a name out to bring that gun back.
    static readonly string[] Parked = { "Uzi", "M1Carbine", "FAL", "HuntingRifle", "AA12" };
    static readonly Gun[] Guns = Every.Where(gun => !Parked.Contains(gun.Name)).ToArray();

    public static string All() => Ammo() + "\n" + Blockouts() + "\n" + Ak74m();

    // ---- The two new calibres: Heavy (FAL, hunting rifle) and Magnum (Desert Eagle), one quality each until ammo tiers ----

    public static string Ammo()
    {
        AmmoType heavy = AmmoItem("HeavyAmmo", "RifleAmmo", "item.heavy_ammo", "ICON_SM_Item_Bullet_Large_01.png", 60, 0.025f);
        AmmoType magnum = AmmoItem("MagnumAmmo", "PistolAmmo", "item.magnum_ammo", "ICON_SM_Wep_Pistol_Ammo_01.png", 42, 0.02f);
        // Not in the starting rounds (AmmoConfig): they would fill the bare backpack's slots at every spawn. The test scene
        // places boxes instead, and goblins drop them (WeaponLootSetup).
        var config = AssetDatabase.LoadAssetAtPath<AmmoConfig>(AmmoConfigPath) ?? throw new Exception("missing " + AmmoConfigPath);
        var starting = (Dictionary<AmmoType, int>)typeof(AmmoConfig).GetField("_starting", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(config);
        if (starting.Remove(heavy) | starting.Remove(magnum))
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }
        return "ammo: HeavyAmmo (60 a stack), MagnumAmmo (42 a stack)";
    }

    static AmmoType AmmoItem(string name, string from, string key, string icon, int maxStack, float weight)
    {
        string path = $"{AmmoDir}{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<AmmoType>(path) == null && !AssetDatabase.CopyAsset($"{AmmoDir}{from}.asset", path))
            throw new Exception("could not copy " + from);
        var ammo = AssetDatabase.LoadAssetAtPath<AmmoType>(path);
        string boxPath = $"{AmmoDir}{name}Box.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(boxPath) == null && !AssetDatabase.CopyAsset($"{AmmoDir}{from}Box.prefab", boxPath))
            throw new Exception("could not copy the box of " + from);
        GameObject box = PrefabUtility.LoadPrefabContents(boxPath);
        try
        {
            var item = new SerializedObject(box.GetComponent<WorldItem>());
            item.FindProperty("_definition").objectReferenceValue = ammo;
            item.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(box, boxPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(box);
        }

        var so = new SerializedObject(ammo);
        LocalizationKit.Set(so.FindProperty("_displayName"), "Items", key);
        so.FindProperty("_icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(AmmoIcons + icon) ?? throw new Exception("missing icon " + icon);
        so.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldItem>(boxPath);
        so.FindProperty("_maxStack").intValue = maxStack;
        so.FindProperty("_weight").floatValue = weight;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(ammo);
        AssetDatabase.SaveAssetIfDirty(ammo);
        return ammo;
    }

    // ---- Block-outs ----

    public static string Blockouts()
    {
        var report = new StringBuilder();
        foreach (Gun gun in Guns)
            report.AppendLine(Build(gun));
        return report.ToString();
    }

    static string Build(Gun gun)
    {
        string folder = Weapons + gun.Name;
        Directory.CreateDirectory(folder);
        GunDefinition definition = CopiedAsset(gun.Name, gun.Reference);
        string path = $"{folder}/{gun.Name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            Variant(GunBase, path);

        GameObject reference = PrefabUtility.LoadPrefabContents($"{Weapons}{gun.Reference}/{gun.Reference}.prefab");
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(gun.Source) ?? throw new Exception("missing " + gun.Source);
            Transform recoil = root.transform.Find("PosePivot/RecoilPivot");
            Remove(recoil.Find("AnimationRoot"));
            Remove(root.transform.Find("World"));

            // The held model: Castle Bravo's parts with their own names, shadows off, on the reference's held layer.
            Transform refWeapon = reference.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon");
            Transform motion = Node("Motion", Node("AnimationRoot", recoil));
            Transform weapon = Node("Weapon", motion);
            CopyTree(source.transform, weapon, shadows: false);
            if (gun.ScopeLens != null)
                weapon.Find(gun.ScopeLens).GetComponent<MeshRenderer>().sharedMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(LensScope) ?? throw new Exception("run AttachmentSetup.Optics first");
            if (gun.ScopeWindow > 0f)
                ScopeWindow(weapon.Find(gun.ScopeLens), gun.ScopeWindow);
            Transform muzzle = Node("Muzzle", weapon);
            muzzle.localPosition = gun.Muzzle;
            Transform ejection = EjectionPort(weapon);

            // Hands: the reference's grips (finger poses), the right one where the reference holds its trigger.
            Transform hands = Node("HandTargets", motion);
            Transform refHands = reference.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/HandTargets");
            Vector3 trigger = Part(weapon, "_Trigger").localPosition;
            Vector3 refTrigger = refWeapon.Find("Trigger").localPosition;
            HandGrip right = Grip(refHands.Find("Right"), hands, "Right", trigger + refHands.Find("Right").localPosition - refTrigger);
            Vector3 left = gun.Left ?? right.transform.localPosition + refHands.Find("Left").localPosition - refHands.Find("Right").localPosition;
            HandGrip leftGrip = Grip(refHands.Find("Left"), hands, "Left", left);
            SetLayer(recoil.Find("AnimationRoot"), refWeapon.gameObject.layer);

            // Poses: hip and sprint as the reference holds them, lowered by how much taller its sights are (so a tall rail or
            // optic doesn't fill the view); aim with the sight line on the eye's axis.
            Transform poses = root.transform.Find("Poses");
            Transform refPoses = reference.transform.Find("Poses");
            var lower = new Vector3(0f, gun.Sight - refWeapon.Find("IronSight").localPosition.y, 0f);
            Copy(refPoses.Find("Hip"), poses.Find("Hip"), lower);
            Copy(refPoses.Find("Sprint"), poses.Find("Sprint"), lower);
            poses.Find("Aim").SetLocalPositionAndRotation(new Vector3(AimX, HandHeight - gun.Sight, refPoses.Find("Aim").localPosition.z - gun.AimCloser),
                Quaternion.identity);
            Copy(poses.Find("Hip"), root.transform.Find("PosePivot"));

            var held = new SerializedObject(root.GetComponent<HeldGun>());
            held.FindProperty("_muzzle").objectReferenceValue = muzzle;
            held.FindProperty("_animation").objectReferenceValue = null;
            held.FindProperty("_ejectionPort").objectReferenceValue = ejection;
            held.ApplyModifiedPropertiesWithoutUndo();
            if (!root.TryGetComponent(out ItemHands itemHands))
                itemHands = root.AddComponent<ItemHands>();
            var handsSo = new SerializedObject(itemHands);
            handsSo.FindProperty("_right").objectReferenceValue = right;
            handsSo.FindProperty("_left").objectReferenceValue = leftGrip;
            handsSo.FindProperty("_handScale").floatValue = reference.GetComponent<ItemHands>().HandScale;
            handsSo.FindProperty("_bodyAnchor").objectReferenceValue = root.transform.Find("PosePivot");
            handsSo.FindProperty("_bodyRest").objectReferenceValue = poses.Find("Hip");
            handsSo.ApplyModifiedPropertiesWithoutUndo();

            // The pickup: the model lying on its side, a trigger box round it.
            Transform world = Node("World", root.transform);
            Transform model = Node("Model", world);
            CopyTree(source.transform, model, shadows: true);
            model.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Bounds lying = Bounds(world, world);
            model.localPosition = new Vector3(-lying.center.x, 0.003f - lying.min.y, -lying.center.z);
            Bounds placed = Bounds(world, root.transform);
            world.gameObject.SetActive(false);
            if (!root.TryGetComponent(out BoxCollider box))
                box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = placed.center;
            box.size = Vector3.Max(placed.size + Vector3.one * 0.05f, Vector3.one * 0.3f);
            if (!root.TryGetComponent(out WorldItem worldItem))
                worldItem = root.AddComponent<WorldItem>();
            var item = new SerializedObject(worldItem);
            item.FindProperty("_definition").objectReferenceValue = definition;
            item.FindProperty("_worldModel").objectReferenceValue = world.gameObject;
            item.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            PrefabUtility.UnloadPrefabContents(reference);
        }

        Define(definition, gun, path);
        return $"{gun.Name}: from {Path.GetFileNameWithoutExtension(gun.Source)} like {gun.Reference}, sight {gun.Sight:0.000}";
    }

    // The gun's item, copied from the reference gun's (handling, recoil, sounds, effects), then its own name, prefab, ammo
    // and action. No jam repair profile: that one plays clips the block-out doesn't have, so a jam clears on its own.
    static void Define(GunDefinition definition, Gun gun, string prefabPath)
    {
        var so = new SerializedObject(definition);
        LocalizationKit.Set(so.FindProperty("_displayName"), "Items", gun.Key);
        so.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldItem>(prefabPath);
        if (gun.Ammo != null)
            so.FindProperty("_magazine.AmmoType").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AmmoType>($"{AmmoDir}{gun.Ammo}.asset") ?? throw new Exception("run Ammo first: no " + gun.Ammo);
        if (gun.Mode != null)
            so.FindProperty("_fire.Mode").enumValueIndex = (int)gun.Mode.Value;
        if (gun.Reload != null)
            so.FindProperty("_magazine.Reload").enumValueIndex = (int)gun.Reload.Value;
        if (gun.CycleTime != null)
            so.FindProperty("_fire.CycleTime").floatValue = gun.CycleTime.Value;
        if (gun.AimZoom != null)
            so.FindProperty("_handling.AimZoom").floatValue = gun.AimZoom.Value;
        so.FindProperty("_jam.Repair").objectReferenceValue = null;
        if (gun.SniperShot)
        {
            // A rifle round from a shotgun's bolt-like action: one bullet, the sniper shot.
            so.FindProperty("_fire.Pellets").intValue = 1;
            so.FindProperty("_fire.SpreadAngle").floatValue = 0f;
            so.FindProperty("_sounds.Fire._entity").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioEntity>(SniperFire);
        }
        if (gun.BoxReload)
        {
            // A box magazine on a shotgun reference: the rifle's reload sounds, no pump.
            var ak = new SerializedObject(AssetDatabase.LoadAssetAtPath<GunDefinition>($"{Weapons}AK47/AK47.asset"));
            foreach (string sound in new[] { "ReloadStart", "ReloadInsert", "ReloadEnd", "CycleStart", "CycleEnd" })
                so.CopyFromSerializedProperty(ak.FindProperty("_sounds." + sound));
            so.FindProperty("_ejection.OnCycle").boolValue = false;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
    }

    // ---- Built-in tube scopes: a sight window just inside the rear opening (AttachmentSetup.Windows does the same for
    // optic parts; AttachmentSetup.Mounts wires it into the held GunMounts) ----

    const string SightWindowMaterial = "Assets/_Game/Content/Weapons/_Shared/Optics/SightWindow.mat";
    const string SightWindowMesh = "Assets/_Game/Content/Weapons/_Shared/Optics/SightWindowDisc.asset";

    // Adds the windows to the existing block-out prefabs (and wires them if GunMounts is there already).
    public static string ScopeWindows()
    {
        var report = new System.Text.StringBuilder();
        foreach (Gun gun in Guns)
        {
            if (gun.ScopeWindow <= 0f)
                continue;
            string path = $"{Weapons}{gun.Name}/{gun.Name}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform weapon = root.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon");
                Renderer window = ScopeWindow(weapon.Find(gun.ScopeLens), gun.ScopeWindow);
                if (weapon.TryGetComponent(out GunMounts mounts))
                {
                    var so = new SerializedObject(mounts);
                    so.FindProperty("_sightWindow").objectReferenceValue = window;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Append($"{gun.Name}: window at z {window.transform.localPosition.z:F3}, wired {mounts != null}; ");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return report.ToString();
    }

    // Beside the lens (same pose, so the reticle drawn on it lines up), 2 mm inside the scope body's rear face.
    static Renderer ScopeWindow(Transform lens, float radius)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SightWindowMesh) ?? throw new Exception("run AttachmentSetup.Optics first");
        var material = AssetDatabase.LoadAssetAtPath<Material>(SightWindowMaterial) ?? throw new Exception("run AttachmentSetup.Optics first");
        float rear = 0f;
        foreach (MeshFilter filter in lens.parent.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || filter.transform == lens)
                continue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 p = lens.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (p.x * p.x + p.y * p.y < radius * radius * 1.69f)
                    rear = Mathf.Min(rear, p.z);
            }
        }
        Transform existing = lens.parent.Find("SightWindow");
        GameObject go = existing != null ? existing.gameObject : new GameObject("SightWindow", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(lens.parent, false);
        go.layer = lens.gameObject.layer;
        go.transform.SetPositionAndRotation(lens.TransformPoint(new Vector3(0f, 0f, rear + 0.002f)), lens.rotation);
        // Uniform scale: the reticle shader reads the view direction in object space.
        go.transform.localScale = Vector3.one * (radius / lens.parent.lossyScale.x);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { material, lens.GetComponent<MeshRenderer>().sharedMaterial };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }

    // ---- AK-74M: the AK47's prefab and clips with the AK-74M's meshes (same layout), and its muzzle device ----

    public static string Ak74m()
    {
        const string name = "AK74M";
        Directory.CreateDirectory(Weapons + name);
        GunDefinition definition = CopiedAsset(name, "AK47");
        string path = $"{Weapons}{name}/{name}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            Variant($"{Weapons}AK47/AK47.prefab", path);

        var source = AssetDatabase.LoadAssetAtPath<GameObject>(CB1 + "AR Weapons/AR_B_2.prefab") ?? throw new Exception("missing AR_B_2");
        Dictionary<string, MeshFilter> meshes = source.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null)
            .GroupBy(f => f.sharedMesh.name).ToDictionary(g => g.Key, g => g.First());
        MeshFilter device = meshes["Muzzle_Device_G"];
        // The device's place in the model (it is a child of the Castle Bravo root).
        Vector3 deviceAt = source.transform.InverseTransformPoint(device.transform.position);
        float barrelTip = Bounds(meshes["AR_B_2_Barrel"].transform, source.transform).max.z;
        float deviceTip = Bounds(device.transform, source.transform).max.z;
        int swapped = 0;

        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform weapon = root.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon");
            Transform model = root.transform.Find("World/Model");
            foreach (MeshFilter filter in weapon.GetComponentsInChildren<MeshFilter>(true).Concat(model.GetComponentsInChildren<MeshFilter>(true)))
            {
                string mesh = filter.sharedMesh != null ? filter.sharedMesh.name : "";
                if (!mesh.StartsWith("AR_B_") || mesh.StartsWith("AR_B_2_"))
                    continue;
                if (!meshes.TryGetValue("AR_B_2_" + mesh.Substring("AR_B_".Length), out MeshFilter twin))
                    continue;
                filter.sharedMesh = twin.sharedMesh;
                swapped++;
            }

            foreach ((Transform on, bool held) in new[] { (weapon, true), (model, false) })
            {
                Transform node = on.Find("MuzzleDevice") ?? new GameObject("MuzzleDevice").transform;
                node.SetParent(on, false);
                node.localPosition = deviceAt;
                node.gameObject.layer = on.gameObject.layer;
                if (!node.TryGetComponent(out MeshFilter filter))
                    filter = node.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = device.sharedMesh;
                if (!node.TryGetComponent(out MeshRenderer renderer))
                    renderer = node.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = device.GetComponent<MeshRenderer>().sharedMaterials;
                renderer.shadowCastingMode = held ? ShadowCastingMode.Off : ShadowCastingMode.On;
                // A fitted muzzle part replaces the device, on the bare barrel's tip.
                on.Find("Mount_Muzzle").localPosition = new Vector3(0f, deviceAt.y, barrelTip - 0.005f);
                var gunMounts = new SerializedObject(on.GetComponent<GunMounts>());
                SerializedProperty list = gunMounts.FindProperty("_mounts");
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty entry = list.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("Slot").enumValueIndex != (int)GunSlot.Muzzle)
                        continue;
                    SerializedProperty hide = entry.FindPropertyRelative("HideWhenFitted");
                    hide.arraySize = 1;
                    hide.GetArrayElementAtIndex(0).objectReferenceValue = node.gameObject;
                }
                gunMounts.ApplyModifiedPropertiesWithoutUndo();
            }
            weapon.Find("Muzzle").localPosition = new Vector3(0f, deviceAt.y, deviceTip);

            var item = new SerializedObject(root.GetComponent<WorldItem>());
            item.FindProperty("_definition").objectReferenceValue = definition;
            item.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        var so = new SerializedObject(definition);
        LocalizationKit.Set(so.FindProperty("_displayName"), "Items", "item.ak74m");
        so.FindProperty("_prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<WorldItem>(path);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
        return $"{name}: AK47 variant, {swapped} meshes swapped to AR_B_2, muzzle device at {deviceAt.z:0.000}-{deviceTip:0.000}";
    }

    // ---- Helpers ----

    static GunDefinition CopiedAsset(string name, string reference)
    {
        string path = $"{Weapons}{name}/{name}.asset";
        if (AssetDatabase.LoadAssetAtPath<GunDefinition>(path) == null && !AssetDatabase.CopyAsset($"{Weapons}{reference}/{reference}.asset", path))
            throw new Exception($"could not copy {reference}.asset");
        return AssetDatabase.LoadAssetAtPath<GunDefinition>(path);
    }

    // A new prefab variant of `basePath` at `path`.
    static void Variant(string basePath, string path)
    {
        var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath) ?? throw new Exception("missing " + basePath);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
        try
        {
            instance.name = Path.GetFileNameWithoutExtension(path);
            PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // Removes an object this script added on an earlier run (never an inherited one).
    static void Remove(Transform node)
    {
        if (node != null && !PrefabUtility.IsPartOfPrefabInstance(node.gameObject))
            Object.DestroyImmediate(node.gameObject);
    }

    static Transform Node(string name, Transform parent)
    {
        Transform node = parent.Find(name);
        if (node != null)
            return node;
        node = new GameObject(name).transform;
        node.SetParent(parent, false);
        return node;
    }

    // The Castle Bravo model's parts under `target`, same names, poses and on / off state (rails shown only with a part).
    static void CopyTree(Transform source, Transform target, bool shadows)
    {
        foreach (Transform child in source)
        {
            var node = new GameObject(child.name).transform;
            node.SetParent(target, false);
            node.SetLocalPositionAndRotation(child.localPosition, child.localRotation);
            node.localScale = child.localScale;
            if (child.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null && child.TryGetComponent(out MeshRenderer renderer))
            {
                node.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copy = node.gameObject.AddComponent<MeshRenderer>();
                copy.sharedMaterials = renderer.sharedMaterials;
                copy.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            CopyTree(child, node, shadows);
            node.gameObject.SetActive(child.gameObject.activeSelf);
        }
    }

    static Transform Part(Transform weapon, string suffix) =>
        weapon.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith(suffix)) ?? throw new Exception($"{weapon.root.name}: no *{suffix}");

    // Where spent cases leave: the right side of the bolt (or of the receiver above the trigger without one).
    static Transform EjectionPort(Transform weapon)
    {
        Transform bolt = weapon.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("_Bolt"));
        Bounds at = bolt != null ? Bounds(bolt, weapon) : Bounds(Part(weapon, "_Trigger"), weapon);
        Transform port = Node("EjectionPort", weapon);
        port.localPosition = bolt != null ? new Vector3(0.025f, at.center.y, at.center.z) : new Vector3(0.025f, at.max.y + 0.05f, at.center.z + 0.1f);
        return port;
    }

    static HandGrip Grip(Transform reference, Transform parent, string name, Vector3 position)
    {
        Transform node = Node(name, parent);
        node.SetLocalPositionAndRotation(position, reference.localRotation);
        if (!node.TryGetComponent(out HandGrip grip))
            grip = node.gameObject.AddComponent<HandGrip>();
        // The reference's grip shape comes along; with no clips to key it, it stays as it is (a closed grip).
        EditorUtility.CopySerialized(reference.GetComponent<HandGrip>(), grip);
        return grip;
    }

    static void Copy(Transform from, Transform to, Vector3 lower = default) =>
        to.SetLocalPositionAndRotation(from.localPosition - lower, from.localRotation);

    static void SetLayer(Transform root, int layer)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            node.gameObject.layer = layer;
    }

    // Bounds of the meshes under `of`, in `space`.
    static Bounds Bounds(Transform of, Transform space)
    {
        var bounds = new Bounds();
        bool any = false;
        foreach (MeshFilter filter in of.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = mesh.center + Vector3.Scale(mesh.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 point = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!any)
                    bounds = new Bounds(point, Vector3.zero);
                else
                    bounds.Encapsulate(point);
                any = true;
            }
        }
        return bounds;
    }
}
