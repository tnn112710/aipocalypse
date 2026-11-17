using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using MB13.Aipocalypse.Core.EquipmentSystem;
using MB13.Aipocalypse.Core.UI;
using MB13.Aipocalypse.Core.WeaponSystem;
using MB13.Aipocalypse.Editor.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Builds the gun parts: optic reticle textures and lens materials (Optics), the part models, pickups, item assets and
// icons from Castle Bravo attachments (Parts), and the part mounts on the five guns from the Castle Bravo presets
// (Mounts). Safe to re-run: assets are updated in place and prefabs rebuilt with the same GUIDs.
// Run: unity command script run --file AgentScripts/Weapons/AttachmentSetup.cs --entry AttachmentSetup.All
public static class AttachmentSetup
{
    const string Dir = "Assets/_Game/Content/Weapons/_Shared/Attachments";
    const string OpticsDir = "Assets/_Game/Content/Weapons/_Shared/Optics";
    const string ReticleShader = "Aipocalypse/Weapons/OpticReticle";
    const string Weapons = "Assets/_Game/Content/Weapons/";
    const string AmmoDir = "Assets/_Game/Content/Weapons/_Shared/Ammo/";
    const string CatalogPath = "Assets/_Game/Data/Equipment/ItemCatalog.asset";
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string CB = "Assets/_StoreAssets/Weapons/Castle Bravo/Low Poly Weapon Bundle Pack 1/Prefabs/";
    const string Parts1 = CB + "Attachments/";
    const string WeaponRoot = "PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon";

    const GunFamily Long = GunFamily.Smg | GunFamily.Rifle | GunFamily.Marksman | GunFamily.MachineGun | GunFamily.Shotgun;
    const GunFamily Rifles = GunFamily.Rifle | GunFamily.Marksman | GunFamily.MachineGun;

    sealed class Part
    {
        public string Name, Key, Source, Lens, MagazineFrom, Ammo;
        // Sound entity names (Sounds below): a suppressor's shot, a light or laser's switch click.
        public string FireSound, SwitchSound;
        public GunSlot Slot;
        public GunFamily Families;
        public (GunStat stat, float percent)[] Mods = Array.Empty<(GunStat, float)>();
        public float Weight, Magnification = 1f, EyeDistance = 0.22f, SwayScale = 1f, Loudness = 1f, EyeBox = 0.012f;
        // Tube optics: the sight window's radius (m), between the rear opening's inner rim and the housing's outer edge.
        public float WindowRadius;
        public int Value;
        public ItemRarity Rarity;
        public bool FullScope, HidesFlash, Light, Laser;
        public Vector3 MagazineScale = Vector3.one;
    }

    const string GM = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string Silenced = GM + "Guns_Weapons/Guns_Silenced/";
    const string GunsLibrary = "Assets/_Game/Data/Audio/Guns.asset";
    const string PlayerLibrary = "Assets/_Game/Data/Audio/Player.asset";
    const string InventoryStylePath = "Assets/_Game/Data/UI/InventoryStyle.asset";

    // 2D one-shot entities (library, name, volume, random pitch, clips). Gamemaster Audio (Could): no Should pack has
    // suppressed shots, a gun light switch or part latches (ASSETS.md).
    static readonly (string library, string name, float volume, float pitch, string[] clips)[] Sounds =
    {
        (GunsLibrary, "Fire_SuppressedRifle", 0.75f, 0.06f, Numbered(Silenced + "gun_silenced_rifle1_shot_", 4)),
        (GunsLibrary, "Fire_SuppressedPistol", 0.75f, 0.06f, Numbered(Silenced + "gun_silenced_pistol1_shot_", 4)),
        (GunsLibrary, "GunLight_Switch", 0.6f, 0.05f, Numbered(GM + "Switches_Buttons_Gears_Levers/switch_button_push_on_off_", 3)),
        (PlayerLibrary, "Part_Attach", 0.6f, 0.06f, Numbered(GM + "Guns_Weapons/Guns/gun_pistol_insert_mag_", 3)),
        (PlayerLibrary, "Part_Detach", 0.55f, 0.06f, Numbered(GM + "Guns_Weapons/Guns/gun_pistol_remove_mag_", 3)),
    };

    static string[] Numbered(string prefix, int count) => Enumerable.Range(1, count).Select(i => $"{prefix}{i:00}.wav").ToArray();

    // Numbers from the design spec (docs/superpowers/specs/2026-11-14-gun-system-design.md, section 3), first guesses.
    static readonly Part[] Specs =
    {
        new() { Name = "RedDot", Key = "item.part_red_dot", Source = "Optics/Optics/Optic_H", Slot = GunSlot.Optic, Families = Long,
            Mods = new[] { (GunStat.AimTime, -5f) }, Weight = 0.2f, Value = 120, Rarity = ItemRarity.Uncommon, Lens = "Dot", Magnification = 1.5f, EyeDistance = 0.22f,
            SwayScale = 0.85f, WindowRadius = 0.022f },
        new() { Name = "Holo", Key = "item.part_holo", Source = "Optics/Optics/Optic_I", Slot = GunSlot.Optic, Families = Long,
            Mods = new[] { (GunStat.Spread, -15f), (GunStat.AimTime, 5f) }, Weight = 0.3f, Value = 200, Rarity = ItemRarity.Rare, Lens = "Holo",
            Magnification = 1.5f, EyeDistance = 0.22f, SwayScale = 0.85f },
        new() { Name = "Scope2x", Key = "item.part_scope_2x", Source = "Optics/Optics/Optic_C", Slot = GunSlot.Optic, Families = Rifles,
            Mods = new[] { (GunStat.Range, 15f), (GunStat.AimTime, 15f) }, Weight = 0.45f, Value = 350, Rarity = ItemRarity.Rare,
            Lens = "Chevron", Magnification = 2f, EyeDistance = 0.2f, SwayScale = 0.7f, WindowRadius = 0.02f },
        new() { Name = "Scope4x", Key = "item.part_scope_4x", Source = "Optics/Optics_Recon/Optic_Recon_D", Slot = GunSlot.Optic,
            Families = GunFamily.Rifle | GunFamily.Marksman | GunFamily.Sniper,
            Mods = new[] { (GunStat.Range, 25f), (GunStat.AimTime, 25f), (GunStat.HipSpread, 15f) }, Weight = 0.6f, Value = 600,
            Rarity = ItemRarity.Epic, Lens = "Scope", Magnification = 4f, FullScope = true, SwayScale = 0.35f, EyeBox = 0.006f, EyeDistance = 0.28f,
            WindowRadius = 0.0265f },
        new() { Name = "PistolRedDot", Key = "item.part_pistol_red_dot", Source = "Optics/Optics_Pistol/Optic_Pistol_A", Slot = GunSlot.Optic,
            Families = GunFamily.Pistol, Mods = new[] { (GunStat.AimTime, -8f) }, Weight = 0.1f, Value = 100, Rarity = ItemRarity.Uncommon,
            Lens = "PistolDot", EyeDistance = 0.18f },
        new() { Name = "Compensator", Key = "item.part_compensator", Source = "Muzzles/Muzzles/Muzzle_Brake_C", Slot = GunSlot.Muzzle,
            Families = GunFamily.Smg | Rifles, Mods = new[] { (GunStat.VerticalRecoil, -10f) }, Weight = 0.15f, Value = 150,
            Rarity = ItemRarity.Uncommon },
        new() { Name = "Suppressor", Key = "item.part_suppressor", Source = "Muzzles/Muzzles/Muzzle_Suppressor_A", Slot = GunSlot.Muzzle,
            Families = Rifles, Mods = new[] { (GunStat.Range, -10f), (GunStat.BulletSpeed, -10f), (GunStat.VerticalRecoil, -5f) },
            Weight = 0.45f, Value = 300, Rarity = ItemRarity.Rare, HidesFlash = true, FireSound = "Fire_SuppressedRifle", Loudness = 0.3f },
        new() { Name = "PistolSuppressor", Key = "item.part_pistol_suppressor", Source = "Muzzles/Muzzles_Pistol/Muzzle_Pistol_Suppressor_A",
            Slot = GunSlot.Muzzle, Families = GunFamily.Pistol | GunFamily.Smg, Mods = new[] { (GunStat.Range, -10f), (GunStat.BulletSpeed, -10f) },
            Weight = 0.25f, Value = 250, Rarity = ItemRarity.Rare, HidesFlash = true, FireSound = "Fire_SuppressedPistol", Loudness = 0.3f },
        new() { Name = "Choke", Key = "item.part_choke", Source = "Muzzles/Muzzles_ShotGun/Muzzle_ShotGun_Shoke_A", Slot = GunSlot.Muzzle,
            Families = GunFamily.Shotgun, Mods = new[] { (GunStat.Spread, -20f) }, Weight = 0.1f, Value = 120, Rarity = ItemRarity.Uncommon },
        new() { Name = "VerticalGrip", Key = "item.part_vertical_grip", Source = "UnderBarrels/ForwardGrip_C", Slot = GunSlot.Grip,
            Families = GunFamily.Smg | GunFamily.Rifle | GunFamily.Marksman | GunFamily.Shotgun,
            Mods = new[] { (GunStat.VerticalRecoil, -12f), (GunStat.AimTime, 5f) }, Weight = 0.15f, Value = 150, Rarity = ItemRarity.Uncommon },
        new() { Name = "AngledGrip", Key = "item.part_angled_grip", Source = "UnderBarrels/ForwardGrip_A", Slot = GunSlot.Grip,
            Families = GunFamily.Smg | GunFamily.Rifle | GunFamily.Marksman | GunFamily.Shotgun,
            Mods = new[] { (GunStat.AimTime, -10f), (GunStat.HorizontalRecoil, -5f) }, Weight = 0.12f, Value = 150, Rarity = ItemRarity.Uncommon },
        new() { Name = "Laser", Key = "item.part_laser", Source = "Accessories/Lasers/Laser_A_Right", Slot = GunSlot.Tactical, Families = Long,
            Mods = new[] { (GunStat.HipSpread, -25f) }, Weight = 0.12f, Value = 150, Rarity = ItemRarity.Uncommon, Laser = true,
            SwitchSound = "GunLight_Switch" },
        new() { Name = "Flashlight", Key = "item.part_flashlight", Source = "Accessories/Lights/Light_A_Right", Slot = GunSlot.Tactical,
            Families = Long, Weight = 0.15f, Value = 100, Rarity = ItemRarity.Common, Light = true, SwitchSound = "GunLight_Switch" },
        new() { Name = "PistolLight", Key = "item.part_pistol_light", Source = "Accessories/Accessories_Pistol/Light_Pistol_A",
            Slot = GunSlot.Tactical, Families = GunFamily.Pistol, Weight = 0.1f, Value = 80, Rarity = ItemRarity.Common, Light = true,
            SwitchSound = "GunLight_Switch" },
        new() { Name = "RifleMag", Key = "item.part_rifle_mag", Slot = GunSlot.Magazine, Families = GunFamily.Rifle | GunFamily.Marksman, Ammo = "RifleAmmo",
            Mods = new[] { (GunStat.MagazineSize, 33f), (GunStat.AimTime, 5f), (GunStat.ReloadTime, 5f) }, Weight = 0.3f, Value = 200,
            Rarity = ItemRarity.Uncommon, MagazineScale = new Vector3(1f, 1.3f, 1f), MagazineFrom = "AK47" },
        new() { Name = "PistolMag", Key = "item.part_pistol_mag", Slot = GunSlot.Magazine, Families = GunFamily.Pistol | GunFamily.Smg,
            Ammo = "PistolAmmo", Mods = new[] { (GunStat.MagazineSize, 33f), (GunStat.AimTime, 5f) }, Weight = 0.15f, Value = 150,
            Rarity = ItemRarity.Uncommon, MagazineScale = new Vector3(1f, 1.3f, 1f), MagazineFrom = "Pistol" },
    };

    public static string All() => Optics() + "\n" + Audio() + "\n" + Parts() + "\n" + Mounts();

    // ---- Audio: the parts' sound entities, and the inventory's fit / take-off sounds ----

    public static string Audio()
    {
        foreach ((string library, string name, float volume, float pitch, string[] clips) in Sounds)
            Sound(library, name, volume, pitch, clips);
        var style = AssetDatabase.LoadAssetAtPath<InventoryStyle>(InventoryStylePath) ?? throw new Exception("missing " + InventoryStylePath);
        style.AttachSound = SoundOf("Part_Attach");
        style.DetachSound = SoundOf("Part_Detach");
        EditorUtility.SetDirty(style);
        AssetDatabase.SaveAssetIfDirty(style);
        return "sounds: " + string.Join(", ", Sounds.Select(s => s.name));
    }

    // A 2D one-shot SFX entity in the library's folder with shuffled clips and a random pitch range (as InventorySetup.Sound).
    static AudioEntity Sound(string libraryPath, string name, float volume, float pitchRandom, string[] clips)
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(libraryPath) ?? throw new Exception("missing " + libraryPath);
        string path = $"{libraryPath.Substring(0, libraryPath.Length - ".asset".Length)}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioAsset>k__BackingField").objectReferenceValue = library;
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)BroAudioType.SFX;
        so.FindProperty("<Loop>k__BackingField").boolValue = false;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = 1f;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)(clips.Length > 1 ? MulticlipsPlayMode.Shuffle : MulticlipsPlayMode.Single);
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = pitchRandom > 0f ? 1 : 0;
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = null;
        SerializedProperty list = so.FindProperty("Clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]) ?? throw new Exception("missing clip " + clips[i]);
            element.FindPropertyRelative("Volume").floatValue = 1f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(entity);
        AssetDatabase.SaveAssetIfDirty(entity);
        return entity;
    }

    static AudioEntity EntityOf(string name)
    {
        (string library, string _, float _, float _, string[] _) = Sounds.First(s => s.name == name);
        string path = $"{library.Substring(0, library.Length - ".asset".Length)}/{name}.asset";
        return AssetDatabase.LoadAssetAtPath<AudioEntity>(path) ?? throw new Exception("run Audio first: missing " + path);
    }

    static SoundID SoundOf(string name) => new(EntityOf(name));

    // ---- Optics: procedural reticles (white on transparent; the material tints them) and one lens material per optic ----

    public static string Optics()
    {
        Directory.CreateDirectory(OpticsDir);
        // Bold shapes with a soft glow around them, so they read on bright sky and at a glance (user: better reticles).
        Texture2D dot = Reticle("Reticle_Dot", 256, (x, y) => Mathf.Max(Disc(x, y, 8f), Glow(Mathf.Sqrt(x * x + y * y) - 8f, 16f, 0.35f)));
        Texture2D holo = Reticle("Reticle_Holo", 256, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y);
            return Mathf.Max(Mathf.Max(Disc(x, y, 6f), Ring(x, y, 78f, 6f)),
                Mathf.Max(Glow(r - 6f, 10f, 0.3f), Glow(Mathf.Abs(r - 78f) - 3f, 10f, 0.25f)));
        });
        Texture2D chevron = Reticle("Reticle_Chevron", 256, (x, y) =>
        {
            float d = Mathf.Min(Mathf.Min(SegmentDistance(x, y, Vector2.zero, new Vector2(-34f, -40f)), SegmentDistance(x, y, Vector2.zero, new Vector2(34f, -40f))),
                SegmentDistance(x, y, new Vector2(0f, -55f), new Vector2(0f, -110f)) + 1.5f);
            return Mathf.Max(Mathf.Clamp01(3.5f - d + 0.5f), Glow(d - 3.5f, 10f, 0.3f));
        });
        // The 4x (also the HUD scope view, 1024 px = one screen height): thick posts in from the edge, a fine cross with
        // range ticks, a centre dot.
        Texture2D scope = Reticle("Reticle_Scope", 1024, (x, y) =>
        {
            float fine = Mathf.Max(Segment(x, y, new Vector2(-500f, 0f), new Vector2(500f, 0f), 2.4f), Segment(x, y, new Vector2(0f, -500f), new Vector2(0f, 500f), 2.4f));
            float posts = Mathf.Max(Mathf.Max(Segment(x, y, new Vector2(-500f, 0f), new Vector2(-130f, 0f), 14f), Segment(x, y, new Vector2(500f, 0f), new Vector2(130f, 0f), 14f)),
                Segment(x, y, new Vector2(0f, -500f), new Vector2(0f, -130f), 14f));
            float ticks = 0f;
            for (int i = 1; i <= 2; i++)
            {
                float at = i * 45f;
                ticks = Mathf.Max(ticks, Mathf.Max(Segment(x, y, new Vector2(at, -7f), new Vector2(at, 7f), 2.4f), Segment(x, y, new Vector2(-at, -7f), new Vector2(-at, 7f), 2.4f)));
                ticks = Mathf.Max(ticks, Segment(x, y, new Vector2(-7f, -at), new Vector2(7f, -at), 2.4f));
            }
            return Mathf.Max(Mathf.Max(fine, posts), Mathf.Max(ticks, Disc(x, y, 4.5f)));
        });
        // The scope view: clear inside, a narrow dark edge, then a thin grey tube ring (lit a little from above), black past
        // it. Grey values in the texture: the HUD's Mask image is white.
        Reticle("ScopeMask", 1024, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y) / 512f;
            return r >= RingFrom ? 1f : Mathf.SmoothStep(0f, 0.55f, Mathf.InverseLerp(0.92f, RingFrom, r));
        }, (x, y) =>
        {
            float r = Mathf.Sqrt(x * x + y * y) / 512f;
            if (r < RingFrom || r > 1f)
                return 0f;
            float lit = 0.5f + 0.5f * y / Mathf.Max(Mathf.Sqrt(x * x + y * y), 1f);
            return (r < RingFrom + 0.006f ? 0.26f : 0.1f) + 0.06f * lit;
        });
        // The scope's shadow: twice the circle's size, clear inside, dark from just inside its rim outwards. Slid off the
        // circle it darkens the edge the scope moves away from.
        Reticle("ScopeShadow", 1024, (x, y) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.56f, Mathf.Sqrt(x * x + y * y) / 512f)));
        // The laser's dot: a solid core fading out at its rim (tinted red by its material).
        Reticle("LaserDot", 64, (x, y) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1f, Mathf.Sqrt(x * x + y * y) / 32f)));

        Shader shader = Shader.Find(ReticleShader) ?? throw new Exception("missing shader " + ReticleShader);
        // Clear glass (user: a tinted lens hid the target): only the reticle draws.
        Color lensTint = Color.clear;
        Lens("Dot", shader, dot, 8f, new Color(3f, 0.15f, 0.1f, 1f), lensTint);
        Lens("PistolDot", shader, dot, 7f, new Color(3f, 0.15f, 0.1f, 1f), lensTint);
        Lens("Holo", shader, holo, 10f, new Color(3f, 0.35f, 0.12f, 1f), lensTint);
        Lens("Chevron", shader, chevron, 7f, new Color(3f, 0.2f, 0.1f, 1f), lensTint);
        Lens("Scope", shader, scope, 12f, new Color(0f, 0f, 0f, 1f), lensTint);
        LaserDot();
        SightWindowAssets();
        return "optics: reticles, ScopeMask, ScopeShadow, 5 lens materials, the laser dot, the sight window disc and material in " + OpticsDir;
    }

    // Where the scope view's tube ring starts (share of the circle's radius).
    const float RingFrom = 0.975f;
    const string SightWindowShader = "Aipocalypse/Weapons/SightWindow";
    const string SightWindowMaterial = OpticsDir + "/SightWindow.mat";
    const string SightWindowMesh = OpticsDir + "/SightWindowDisc.asset";

    // A soft halo `width` px wide past a shape's edge (`distance` = px outside it), `strength` at the edge.
    static float Glow(float distance, float width, float strength) =>
        distance <= 0f ? strength : strength * (1f - Mathf.SmoothStep(0f, 1f, distance / width));

    static float SegmentDistance(float x, float y, Vector2 a, Vector2 b)
    {
        var p = new Vector2(x, y);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    // The sight window: a unit disc facing the eye (-Z, scaled by its radius) and its depth-only material, queued before
    // the opaque models so the tube behind it never draws (Optics/SightWindow.shadergraph: transparent, alpha 0, depth write on).
    static void SightWindowAssets()
    {
        const int sides = 32;
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SightWindowMesh);
        if (mesh == null)
        {
            mesh = new Mesh { name = "SightWindowDisc" };
            AssetDatabase.CreateAsset(mesh, SightWindowMesh);
        }
        var vertices = new Vector3[sides + 1];
        var normals = new Vector3[sides + 1];
        var triangles = new int[sides * 3];
        normals[0] = Vector3.back;
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            normals[i + 1] = Vector3.back;
            // Clockwise as seen from -Z (the eye), Unity's front face.
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = (i + 1) % sides + 1;
            triangles[i * 3 + 2] = i + 1;
        }
        mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        AssetDatabase.SaveAssetIfDirty(mesh);

        Shader shader = Shader.Find(SightWindowShader) ?? throw new Exception("missing shader " + SightWindowShader);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(SightWindowMaterial);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SightWindowMaterial);
        }
        material.shader = shader;
        material.SetFloat("_QueueControl", 1f);
        material.renderQueue = 1990;
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }

    // Adds (or refits) the sight window on each tube optic's model: just inside the rear opening, on the sight line, with
    // the window material then the optic's lens material (so the reticle draws on it). Off in the prefab; HeldGun turns it on.
    public static string Windows()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SightWindowMesh) ?? throw new Exception("run Optics first: no " + SightWindowMesh);
        var window = AssetDatabase.LoadAssetAtPath<Material>(SightWindowMaterial) ?? throw new Exception("run Optics first: no " + SightWindowMaterial);
        var report = new StringBuilder();
        foreach (Part spec in Specs)
        {
            if (spec.WindowRadius <= 0f)
                continue;
            string path = $"{Dir}/{spec.Name}/{spec.Name}_Model.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var model = root.GetComponent<AttachmentModel>();
                Transform sight = model.Sight;
                var lens = new SerializedObject(model);
                Renderer lensRenderer = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.EndsWith("_Lens"));
                float rear = RearOf(root.transform, sight, spec.WindowRadius * 1.3f, lensRenderer);
                Renderer renderer = SightWindow(sight, mesh, window, lensRenderer.sharedMaterial, spec.WindowRadius, rear);
                lens.FindProperty("_sightWindow").objectReferenceValue = renderer;
                lens.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.Append($"{spec.Name}: window r {spec.WindowRadius} at z {rear + 0.002f:F3}; ");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return report.ToString();
    }

    // The rear face of a tube: the lowest z (along the sight line) of the model's vertices within `radius` of the line.
    static float RearOf(Transform root, Transform sight, float radius, Renderer skip)
    {
        float rear = 0f;
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || skip != null && filter.gameObject == skip.gameObject)
                continue;
            foreach (Vector3 vertex in filter.sharedMesh.vertices)
            {
                Vector3 p = sight.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (p.x * p.x + p.y * p.y < radius * radius)
                    rear = Mathf.Min(rear, p.z);
            }
        }
        return rear;
    }

    static Renderer SightWindow(Transform sight, Mesh mesh, Material window, Material lens, float radius, float rear)
    {
        Transform existing = sight.Find("SightWindow");
        GameObject go = existing != null ? existing.gameObject : new GameObject("SightWindow", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(sight, false);
        go.layer = sight.gameObject.layer;
        // Uniform scale: the reticle shader reads the view direction in object space.
        go.transform.SetLocalPositionAndRotation(new Vector3(0f, 0f, rear + 0.002f), Quaternion.identity);
        go.transform.localScale = Vector3.one * radius;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = new[] { window, lens };
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }

    const string LaserDotMaterial = OpticsDir + "/LaserDot.mat";
    const string TracerMaterial = "Assets/_Game/Content/Weapons/_Shared/Effects/Tracer_Line.mat";

    // An unlit HDR red disc (the tracer's URP Particles Unlit setup, alpha blended rather than additive so it reads red on
    // orange or white too); bloom gives it a glow.
    static void LaserDot()
    {
        var tracer = AssetDatabase.LoadAssetAtPath<Material>(TracerMaterial) ?? throw new Exception("missing " + TracerMaterial);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(LaserDotMaterial);
        if (material == null)
        {
            material = new Material(tracer);
            AssetDatabase.CreateAsset(material, LaserDotMaterial);
        }
        material.CopyPropertiesFromMaterial(tracer);
        material.shaderKeywords = tracer.shaderKeywords;
        material.renderQueue = tracer.renderQueue;
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{OpticsDir}/LaserDot.png"));
        material.SetColor("_BaseColor", new Color(4f, 0.06f, 0.03f, 1f));
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }

    static float Disc(float x, float y, float radius) => Mathf.Clamp01(radius - Mathf.Sqrt(x * x + y * y) + 0.5f);

    static float Ring(float x, float y, float radius, float width) =>
        Mathf.Clamp01(width * 0.5f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - radius) + 0.5f);

    static float Segment(float x, float y, Vector2 a, Vector2 b, float width)
    {
        var p = new Vector2(x, y);
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Mathf.Clamp01(width * 0.5f - Vector2.Distance(p, a + ab * t) + 0.5f);
    }

    // White (or `grey(x, y)`), alpha from `shape(x, y)` with (0, 0) at the centre, in pixels. Imported as a sprite (the HUD
    // uses the scope ones), clamped so a lens never repeats it.
    static Texture2D Reticle(string name, int size, Func<float, float, float> shape, Func<float, float, float> grey = null)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            // The edge pixels stay empty: a lens samples them past the texture.
            float a = x == 0 || y == 0 || x == size - 1 || y == size - 1 ? 0f : shape(x + 0.5f - half, y + 0.5f - half);
            byte value = grey != null ? (byte)Mathf.RoundToInt(Mathf.Clamp01(grey(x + 0.5f - half, y + 0.5f - half)) * 255f) : (byte)255;
            pixels[y * size + x] = new Color32(value, value, value, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
        }
        texture.SetPixels32(pixels);
        string path = $"{OpticsDir}/{name}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = size;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void Lens(string name, Shader shader, Texture2D reticle, float size, Color color, Color tint)
    {
        string path = $"{OpticsDir}/Lens_{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.SetTexture("_ReticleTex", reticle);
        material.SetFloat("_ReticleSize", size);
        material.SetColor("_ReticleColor", color);
        material.SetColor("_LensTint", tint);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssetIfDirty(material);
    }

    // ---- Parts: model prefab, item asset, pickup prefab and icon for each ----

    public static string Parts()
    {
        var report = new StringBuilder();
        foreach (Part spec in Specs)
        {
            string folder = $"{Dir}/{spec.Name}";
            Directory.CreateDirectory(folder);
            AttachmentModel model = spec.Source != null ? Model(spec, folder) : null;
            AttachmentDefinition item = Asset<AttachmentDefinition>($"{folder}/{spec.Name}.asset");
            WorldItem pickup = Pickup(spec, folder, item, model);
            Sprite icon = Icon(pickup.gameObject, $"{folder}/{spec.Name}_Icon.png");
            Define(item, spec, model, pickup, icon);
            report.AppendLine($"{spec.Name}: {spec.Slot}, model {(model != null ? model.name : "none")}, icon {icon != null}");
        }
        report.AppendLine(Windows());

        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        int count = catalog.Collect();
        AssetDatabase.SaveAssetIfDirty(catalog);
        report.Append($"catalog: {count} items");
        return report.ToString();
    }

    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null)
            return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    // An unpacked copy of the Castle Bravo attachment (meshes and materials referenced, no colliders), with its markers.
    static AttachmentModel Model(Part spec, string folder)
    {
        string sourcePath = $"{Parts1}{spec.Source}.prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath) ?? throw new Exception("missing " + sourcePath);
        var root = new GameObject($"{spec.Name}_Model");
        try
        {
            // Muzzle parts: the vendor pivots differ, so the meshes move to put the part's back on the pivot (the barrel tip).
            Transform meshes = root.transform;
            if (spec.Slot == GunSlot.Muzzle)
            {
                meshes = new GameObject("Mesh").transform;
                meshes.SetParent(root.transform, false);
            }
            CopyMeshes(source.transform, meshes);
            if (spec.Slot == GunSlot.Muzzle)
                meshes.localPosition = new Vector3(0f, 0f, -LocalBounds(meshes, root.transform).min.z);
            Bounds bounds = LocalBounds(root.transform, root.transform);
            var model = root.AddComponent<AttachmentModel>();
            var so = new SerializedObject(model);

            if (spec.Slot == GunSlot.Muzzle)
                so.FindProperty("_tip").objectReferenceValue = Marker(root.transform, "Tip",
                    new Vector3(bounds.center.x, bounds.center.y, bounds.max.z), Quaternion.identity);
            if (spec.Slot == GunSlot.Optic)
            {
                Transform lens = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("_Lens"));
                Bounds sight = lens != null ? LocalBounds(lens, root.transform) : bounds;
                so.FindProperty("_sight").objectReferenceValue = Marker(root.transform, "Sight", sight.center, Quaternion.identity);
                if (lens != null && spec.Lens != null)
                    lens.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{OpticsDir}/Lens_{spec.Lens}.mat")
                        ?? throw new Exception("run Optics first: no lens material " + spec.Lens);
            }
            if (spec.Slot == GunSlot.Grip)
                // A HandGrip's convention: blue axis from the little finger to the index, so up the grip.
                so.FindProperty("_handGrip").objectReferenceValue = Marker(root.transform, "HandGrip",
                    new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.45f, bounds.center.z), Quaternion.LookRotation(Vector3.up, Vector3.back));
            if (spec.Light)
            {
                Transform node = Marker(root.transform, "Light", new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + 0.01f), Quaternion.identity);
                var light = node.gameObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 30f;
                light.spotAngle = 50f;
                light.innerSpotAngle = 22f;
                light.intensity = 6f;
                light.color = new Color(1f, 0.95f, 0.86f);
                light.shadows = LightShadows.None;
                light.enabled = false;
                SerializedProperty lights = so.FindProperty("_lights");
                lights.arraySize = 1;
                lights.GetArrayElementAtIndex(0).objectReferenceValue = light;
            }
            if (spec.Laser)
            {
                // The beam leaves the front face; the dot is a camera-facing quad the model moves into the world while held.
                Transform emitter = Marker(root.transform, "Laser", new Vector3(bounds.center.x, bounds.center.y, bounds.max.z + 0.002f), Quaternion.identity);
                var dot = new GameObject("LaserDot");
                dot.transform.SetParent(root.transform, false);
                dot.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                var renderer = dot.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(LaserDotMaterial) ?? throw new Exception("run Optics first: no " + LaserDotMaterial);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.enabled = false;
                so.FindProperty("_laser").objectReferenceValue = emitter;
                so.FindProperty("_laserDot").objectReferenceValue = renderer;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{spec.Name}_Model.prefab").GetComponent<AttachmentModel>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Transform Marker(Transform parent, string name, Vector3 position, Quaternion rotation)
    {
        var marker = new GameObject(name).transform;
        marker.SetParent(parent, false);
        marker.SetLocalPositionAndRotation(position, rotation);
        return marker;
    }

    // Copies every mesh under `source` into `target`, keeping names and poses relative to `source`.
    static void CopyMeshes(Transform source, Transform target)
    {
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null)
                continue;
            Transform node = target;
            if (filter.transform != source)
            {
                node = new GameObject(filter.name).transform;
                node.SetParent(target, false);
                Matrix4x4 local = source.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                node.SetLocalPositionAndRotation(local.GetColumn(3), local.rotation);
                node.localScale = local.lossyScale;
            }
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
    }

    // Bounds of the meshes under `of`, in `space`.
    static Bounds LocalBounds(Transform of, Transform space)
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

    // Root WorldItem + trigger box; World holds the part model (or the gun's magazine for a magazine part), standing on
    // the ground, centred.
    static WorldItem Pickup(Part spec, string folder, AttachmentDefinition item, AttachmentModel model)
    {
        var root = new GameObject(spec.Name);
        try
        {
            var world = new GameObject("World").transform;
            world.SetParent(root.transform, false);
            Transform shown;
            if (model != null)
                shown = ((GameObject)PrefabUtility.InstantiatePrefab(model.gameObject, world)).transform;
            else
            {
                shown = new GameObject("Model").transform;
                shown.SetParent(world, false);
                Transform magazine = HeldWeapon(spec.MagazineFrom).Find("Magazine") ?? throw new Exception(spec.MagazineFrom + " has no Magazine");
                shown.gameObject.AddComponent<MeshFilter>().sharedMesh = magazine.GetComponent<MeshFilter>().sharedMesh;
                shown.gameObject.AddComponent<MeshRenderer>().sharedMaterials = magazine.GetComponent<MeshRenderer>().sharedMaterials;
                shown.localScale = spec.MagazineScale;
            }
            Bounds bounds = LocalBounds(shown, world);
            shown.localPosition = new Vector3(-bounds.center.x, 0.005f - bounds.min.y, -bounds.center.z);

            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            Bounds placed = LocalBounds(world, root.transform);
            box.center = placed.center;
            box.size = Vector3.Max(placed.size + Vector3.one * 0.05f, Vector3.one * 0.3f);

            var worldItem = root.AddComponent<WorldItem>();
            var so = new SerializedObject(worldItem);
            so.FindProperty("_definition").objectReferenceValue = item;
            so.FindProperty("_worldModel").objectReferenceValue = world.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/{spec.Name}.prefab").GetComponent<WorldItem>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Transform HeldWeapon(string gun)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Weapons}{gun}/{gun}.prefab") ?? throw new Exception("missing gun " + gun);
        return prefab.transform.Find(WeaponRoot) ?? throw new Exception(gun + " has no " + WeaponRoot);
    }

    static void Define(AttachmentDefinition item, Part spec, AttachmentModel model, WorldItem pickup, Sprite icon)
    {
        var so = new SerializedObject(item);
        LocalizationKit.Set(so.FindProperty("_displayName"), "Items", spec.Key);
        so.FindProperty("_icon").objectReferenceValue = icon;
        so.FindProperty("_prefab").objectReferenceValue = pickup;
        so.FindProperty("_maxStack").intValue = 1;
        so.FindProperty("_weight").floatValue = spec.Weight;
        so.FindProperty("_value").intValue = spec.Value;
        so.FindProperty("_rarity").enumValueIndex = (int)spec.Rarity;
        so.FindProperty("_slot").enumValueIndex = (int)spec.Slot;
        so.FindProperty("_families").enumValueFlag = (int)spec.Families;
        so.FindProperty("_ammo").objectReferenceValue = spec.Ammo != null
            ? AssetDatabase.LoadAssetAtPath<AmmoType>($"{AmmoDir}{spec.Ammo}.asset") ?? throw new Exception("missing ammo " + spec.Ammo)
            : null;
        SerializedProperty mods = so.FindProperty("_modifiers");
        mods.arraySize = spec.Mods.Length;
        for (int i = 0; i < spec.Mods.Length; i++)
        {
            mods.GetArrayElementAtIndex(i).FindPropertyRelative("Stat").enumValueIndex = (int)spec.Mods[i].stat;
            mods.GetArrayElementAtIndex(i).FindPropertyRelative("Percent").floatValue = spec.Mods[i].percent;
        }
        so.FindProperty("_model").objectReferenceValue = model;
        so.FindProperty("_optic.Magnification").floatValue = spec.Magnification;
        so.FindProperty("_optic.EyeDistance").floatValue = spec.EyeDistance;
        so.FindProperty("_optic.SwayScale").floatValue = spec.SwayScale;
        so.FindProperty("_optic.FullScope").boolValue = spec.FullScope;
        so.FindProperty("_optic.EyeBox").floatValue = spec.EyeBox;
        so.FindProperty("_optic.ScopeReticle").objectReferenceValue = spec.FullScope
            ? AssetDatabase.LoadAssetAtPath<Sprite>($"{OpticsDir}/Reticle_Scope.png")
            : null;
        so.FindProperty("_magazineScale").vector3Value = spec.MagazineScale;
        so.FindProperty("_hidesFlash").boolValue = spec.HidesFlash;
        so.FindProperty("_fireSound._entity").objectReferenceValue = spec.FireSound != null ? EntityOf(spec.FireSound) : null;
        so.FindProperty("_fireTailSound._entity").objectReferenceValue = null;
        so.FindProperty("_loudness").floatValue = spec.Loudness;
        so.FindProperty("_switchSound._entity").objectReferenceValue = spec.SwitchSound != null ? EntityOf(spec.SwitchSound) : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(item);
        AssetDatabase.SaveAssetIfDirty(item);
    }

    // A three-quarter render on transparent (rendered on black and on white; alpha from the difference), as a 256 sprite.
    // `gun`: only the pickup's world model, standing upright as the inventory's 3D view shows it.
    static Sprite Icon(GameObject prefab, string path, bool gun = false)
    {
        const int size = 256;
        Texture2D black = Render(prefab, size, Color.black, gun);
        Texture2D white = Render(prefab, size, Color.white, gun);
        Color[] b = black.GetPixels(), w = white.GetPixels();
        var pixels = new Color[b.Length];
        for (int i = 0; i < b.Length; i++)
        {
            float a = Mathf.Clamp01(1f - ((w[i].r - b[i].r) + (w[i].g - b[i].g) + (w[i].b - b[i].b)) / 3f);
            pixels[i] = a > 0.004f ? new Color(b[i].r / a, b[i].g / a, b[i].b / a, a) : Color.clear;
        }
        Outline(pixels, size);
        var icon = new Texture2D(size, size, TextureFormat.RGBA32, false);
        icon.SetPixels(pixels);
        icon.Apply();
        File.WriteAllBytes(path, icon.EncodeToPNG());
        Object.DestroyImmediate(black);
        Object.DestroyImmediate(white);
        Object.DestroyImmediate(icon);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = size;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Lifts the dark Castle Bravo colours and rings the silhouette with a soft light outline, so icons read on the dark
    // inventory slots.
    static void Outline(Color[] pixels, int size)
    {
        const int radius = 3;
        var outline = new Color(0.86f, 0.88f, 0.92f, 1f);
        var source = (Color[])pixels.Clone();
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int i = y * size + x;
            Color c = source[i];
            if (c.a > 0.004f)
                pixels[i] = new Color(Mathf.Min(1f, c.r * 1.25f + 0.08f), Mathf.Min(1f, c.g * 1.25f + 0.08f), Mathf.Min(1f, c.b * 1.25f + 0.08f), c.a);
            if (c.a > 0.5f)
                continue;
            float near = 0f;
            for (int dy = -radius; dy <= radius; dy++)
            for (int dx = -radius; dx <= radius; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size || dx * dx + dy * dy > radius * radius)
                    continue;
                near = Mathf.Max(near, source[ny * size + nx].a);
            }
            float alpha = near * 0.85f;
            if (alpha > c.a)
            {
                Color blended = Color.Lerp(outline, pixels[i], c.a);
                blended.a = alpha;
                pixels[i] = blended;
            }
        }
    }

    static Texture2D Render(GameObject prefab, int size, Color background, bool gun = false)
    {
        var preview = new PreviewRenderUtility();
        try
        {
            GameObject go = preview.InstantiatePrefabInScene(prefab);
            GameObject world = gun ? go.GetComponent<WorldItem>().WorldModel : null;
            foreach (Transform child in go.transform)
                child.gameObject.SetActive(!gun || child.gameObject == world);
            if (gun && world.GetComponentInChildren<IItemView>(true) is Component view)
                view.transform.localRotation = Quaternion.identity;
            var renderers = go.GetComponentsInChildren<MeshRenderer>(false);
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);

            // From the right side, a little in front and above: barrels along +Z point left of the picture.
            Vector3 dir = Quaternion.Euler(18f, -70f, 0f) * Vector3.forward;
            Camera camera = preview.camera;
            camera.fieldOfView = 18f;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 50f;
            var look = Quaternion.LookRotation(dir);
            float tan = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 0.88f;
            float distance = 0f;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1);
                Vector3 local = Quaternion.Inverse(look) * Vector3.Scale(corner, bounds.extents);
                distance = Mathf.Max(distance, Mathf.Abs(local.x) / tan - local.z, Mathf.Abs(local.y) / tan - local.z);
            }
            camera.transform.SetPositionAndRotation(bounds.center - dir * distance, look);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            preview.lights[0].intensity = 2.1f;
            preview.lights[0].transform.rotation = Quaternion.LookRotation(dir + Vector3.down * 0.7f + Vector3.left * 0.4f);
            // From behind and above: a rim on the top edges.
            preview.lights[1].intensity = 1.4f;
            preview.lights[1].transform.rotation = Quaternion.LookRotation(-dir + Vector3.down * 0.9f);
            preview.ambientColor = new Color(0.75f, 0.76f, 0.8f);
            preview.BeginStaticPreview(new Rect(0, 0, size, size));
            preview.Render(true);
            return preview.EndStaticPreview();
        }
        finally
        {
            preview.Cleanup();
        }
    }

    // ---- Mounts on the guns, placed from the Castle Bravo presets (positions in the model's space) ----

    sealed class MountSpec
    {
        public GunSlot Slot;
        // Muzzle mounts sit at the gun's own Muzzle marker, a little inside the barrel; At is ignored.
        public bool AtMuzzle;
        // Under this part of Weapon (it moves with it in the clips); "" = Weapon itself.
        public string Parent = "";
        public Vector3 At;
        public string[] Show = Array.Empty<string>(), Hide = Array.Empty<string>();
        // Rails and adapters to add, shown while a part is fitted: (source prefab, child path in it or null, position).
        public (string source, string child, Vector3 at)[] Adapters = Array.Empty<(string, string, Vector3)>();
    }

    sealed class GunSpec
    {
        public string Name;
        public GunFamily Family;
        // Degrees of hip-fire cone (aimed: 0). First guesses: long, heavy guns are harder to point from the hip.
        public float HipSpread;
        public GunSlot[] Slots;
        public MountSpec[] Mounts;
        public string[] Magazines = Array.Empty<string>();
        // A rendered icon from its world model (GunIcons); the first five keep their Synty icons.
        public bool Icon;
    }

    const string RailDown = "Attachments/Accessories/Rails/Rail_Down";
    // How far a muzzle part's back sits inside the barrel, so no seam shows.
    static readonly Vector3 MuzzleOverlap = new(0f, 0f, -0.005f);
    const string RailRight = "Attachments/Accessories/Rails/Rail_Right";
    const string Mp5Preset = "Weapons/SMG Weapons/SMG_PreSet/SMG_B_PreSet";

    static readonly GunSpec[] Every =
    {
        new()
        {
            Name = "AK47", HipSpread = 3f, Family = GunFamily.Rifle,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.114f, 0.075f), Show = new[] { "Rail" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, Parent = "Handguard", At = new Vector3(0f, 0.005f, 0.401f),
                    Adapters = new[] { (RailDown, (string)null, new Vector3(0f, 0.013f, 0.399f)) } },
                new() { Slot = GunSlot.Tactical, Parent = "Handguard", At = new Vector3(0.038f, 0.040f, 0.396f),
                    Adapters = new[] { (RailRight, (string)null, new Vector3(0.026f, 0.041f, 0.398f)) } },
            },
            Magazines = new[] { "Magazine", "ReplacementMagazine" },
        },
        new()
        {
            Name = "MP5", HipSpread = 2f, Family = GunFamily.Smg,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.147f, 0.057f), Show = new[] { "Rail" }, Hide = new[] { "Sight" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, Parent = "Handguard", At = new Vector3(0f, 0.031f, 0.289f),
                    Adapters = new[] { (Mp5Preset, "SMG_B_BarrelGuard/SMG_B_BarrelGuard_Rail_Down", new Vector3(0f, 0.032f, 0.281f)) } },
                new() { Slot = GunSlot.Tactical, Parent = "Handguard", At = new Vector3(0.041f, 0.088f, 0.314f),
                    Adapters = new[] { (Mp5Preset, "SMG_B_BarrelGuard/SMG_B_BarrelGuard_Rail", new Vector3(0f, 0.087f, 0.311f)) } },
            },
            Magazines = new[] { "Magazine", "ReplacementMagazine" },
        },
        new()
        {
            Name = "Pistol", HipSpread = 2.5f, Family = GunFamily.Pistol,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, Parent = "Slide", At = new Vector3(0f, 0.051f, -0.012f) },
                new() { Slot = GunSlot.Muzzle, Parent = "Barrel", AtMuzzle = true },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0f, 0.005f, 0.138f) },
            },
            Magazines = new[] { "Magazine", "ReplacementMagazine" },
        },
        new()
        {
            Name = "PumpShotgun", HipSpread = 2f, Family = GunFamily.Shotgun,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Tactical },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.077f, 0.149f), Show = new[] { "Rail" }, Hide = new[] { "Sight_Back" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, Parent = "Pump", At = new Vector3(0f, -0.023f, 0.354f),
                    Adapters = new[] { (RailDown, (string)null, new Vector3(0f, -0.016f, 0.351f)) } },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0.026f, 0.047f, 0.616f),
                    Adapters = new[] { (RailRight, (string)null, new Vector3(0.014f, 0.047f, 0.599f)) } },
            },
        },
        new()
        {
            Name = "DoubleBarrel", HipSpread = 2f, Family = GunFamily.Shotgun,
            Slots = new[] { GunSlot.Tactical },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Tactical, Parent = "Hinge", At = new Vector3(0.044f, 0.047f, 0.499f),
                    Adapters = new[] { (RailRight, (string)null, new Vector3(0.011f, 0.047f, 0.522f)) } },
            },
        },

        // Block-outs (GunBlockoutSetup): Castle Bravo part names, mounts from each model's *_PreSet. No clips yet, so every
        // mount sits on Weapon itself; the animation rework re-parents them under the parts that move. Rendered icons.
        new()
        {
            Name = "Glock17", HipSpread = 2.5f, Family = GunFamily.Pistol, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.057f, -0.041f) },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0f, 0.004f, 0.111f) },
            },
            Magazines = new[] { "Magazine", "ReplacementMagazine" },
        },
        new()
        {
            Name = "DesertEagle", HipSpread = 3f, Family = GunFamily.Pistol, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.056f, -0.003f) },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0f, 0.003f, 0.132f) },
            },
            Magazines = new[] { "Magazine", "ReplacementMagazine" },
        },
        new()
        {
            Name = "Uzi", HipSpread = 2f, Family = GunFamily.Smg, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.107f, -0.089f), Show = new[] { "SMG_M_Rail" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0.042f, 0.023f, 0.180f),
                    Adapters = new[] { (RailRight, (string)null, new Vector3(0.030f, 0.023f, 0.178f)) } },
            },
            Magazines = new[] { "SMG_M_Mag" },
        },
        new()
        {
            // The optic sits where the preset's scout scope does, ahead of the receiver.
            Name = "M1Carbine", HipSpread = 2.5f, Family = GunFamily.Rifle, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.088f, 0.280f) },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
            },
            Magazines = new[] { "WWII_Rifle_A_Mag" },
        },
        new()
        {
            Name = "M4", HipSpread = 2.5f, Family = GunFamily.Rifle, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.131f, 0.031f), Hide = new[] { "AR_A_Sight" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, At = new Vector3(0f, 0.026f, 0.348f) },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0.033f, 0.080f, 0.520f) },
            },
            Magazines = new[] { "Magazine", "ReplacementMagazine" },
        },
        new()
        {
            Name = "SCAR", HipSpread = 2.5f, Family = GunFamily.Rifle, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.142f, 0.050f), Hide = new[] { "AR_C_Sight_Back" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, At = new Vector3(0f, 0.018f, 0.330f) },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0.041f, 0.074f, 0.373f) },
            },
            Magazines = new[] { "Magazine", "ReplacementMagazine" },
        },
        new()
        {
            Name = "FAL", HipSpread = 3f, Family = GunFamily.Marksman, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.105f, 0.063f) },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, At = new Vector3(0f, 0.012f, 0.394f) },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0.039f, 0.058f, 0.332f), Show = new[] { "DMR_L_BarrelGuared/DMR_L_BarrelGuard_Rail_Right" } },
            },
            Magazines = new[] { "DMR_L_Mag" },
        },
        new()
        {
            // A fitted optic replaces the built-in scope.
            Name = "HuntingRifle", HipSpread = 4f, Family = GunFamily.Sniper, Icon = true,
            Slots = new[] { GunSlot.Optic },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.103f, 0.159f), Hide = new[] { "Recon_E_Optic" } },
            },
        },
        new()
        {
            Name = "AA12", HipSpread = 2.5f, Family = GunFamily.Shotgun, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Tactical, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.127f, 0.031f), Show = new[] { "ShotGun_A_Rail" },
                    Hide = new[] { "ShotGun_A_Sight_Back", "ShotGun_A_Sight_Forward" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, At = new Vector3(0f, 0.008f, 0.334f), Show = new[] { "ShotGun_A_Receiver_Rail" } },
                new() { Slot = GunSlot.Tactical, At = new Vector3(0.040f, 0.070f, 0.339f),
                    Adapters = new[] { (RailRight, (string)null, new Vector3(0.029f, 0.071f, 0.344f)) } },
            },
            Magazines = new[] { "ShotGun_A_Mag" },
        },
        new()
        {
            // Grip slot for a bipod later (no grip fits a machine gun yet); its drum isn't scaled by magazine parts.
            Name = "RPD", HipSpread = 4f, Family = GunFamily.MachineGun, Icon = true,
            Slots = new[] { GunSlot.Optic, GunSlot.Muzzle, GunSlot.Grip, GunSlot.Magazine },
            Mounts = new MountSpec[]
            {
                new() { Slot = GunSlot.Optic, At = new Vector3(0f, 0.101f, 0.168f), Show = new[] { "LMG_A_Rail" }, Hide = new[] { "LMG_A_Sight" } },
                new() { Slot = GunSlot.Muzzle, AtMuzzle = true },
                new() { Slot = GunSlot.Grip, At = new Vector3(0f, -0.015f, 0.507f),
                    Adapters = new[] { (RailDown, (string)null, new Vector3(0f, -0.008f, 0.528f)) } },
            },
        },
    };

    // Parked: not in the game for now (user, 2026-11-16). Their rows stay; take a name out to bring that gun back.
    static readonly string[] Parked = { "Uzi", "M1Carbine", "FAL", "HuntingRifle", "AA12" };
    static readonly GunSpec[] Guns = Every.Where(gun => !Parked.Contains(gun.Name)).ToArray();

    // The AK47 variant keeps the AK47's mounts and clips; it only needs its icon.
    static readonly string[] IconOnly = { "AK74M" };

    // Rendered icons for the block-out guns, set on their items.
    public static string GunIcons()
    {
        var done = new List<string>();
        foreach (string name in Guns.Where(g => g.Icon).Select(g => g.Name).Concat(IconOnly))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Weapons}{name}/{name}.prefab") ?? throw new Exception("missing gun " + name);
            Sprite icon = Icon(prefab, $"{Weapons}{name}/{name}_Icon.png", gun: true);
            var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>($"{Weapons}{name}/{name}.asset");
            var so = new SerializedObject(definition);
            so.FindProperty("_icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
            done.Add(name);
        }
        var catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        int count = catalog.Collect();
        AssetDatabase.SaveAssetIfDirty(catalog);
        return $"icons: {string.Join(", ", done)}; catalog: {count} items";
    }

    // Rebuilds the AK47's mount nodes: run GunBlockoutSetup.Ak74m after it (the AK-74M variant's overrides on them drop).
    public static string Mounts()
    {
        var report = new StringBuilder();
        Matrix4x4 handToEye = HandToEye();
        foreach (GunSpec gun in Guns)
        {
            string path = $"{Weapons}{gun.Name}/{gun.Name}.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform weapon = root.transform.Find(WeaponRoot) ?? throw new Exception(gun.Name + ": no " + WeaponRoot);
                Transform model = root.transform.Find("World/Model") ?? throw new Exception(gun.Name + ": no World/Model");
                GunMounts held = Build(gun, weapon, weapon, held: true);
                Build(gun, weapon, model, held: false);

                Transform iron = IronSight(root.transform, weapon, handToEye);
                var mounts = new SerializedObject(held);
                mounts.FindProperty("_ironSight").objectReferenceValue = iron;
                mounts.ApplyModifiedPropertiesWithoutUndo();

                var heldGun = new SerializedObject(root.GetComponent<HeldGun>());
                heldGun.FindProperty("_mounts").objectReferenceValue = held;
                heldGun.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine($"{gun.Name}: {gun.Mounts.Length} mounts, iron sight at {weapon.InverseTransformPoint(iron.position):F3}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>($"{Weapons}{gun.Name}/{gun.Name}.asset");
            var so = new SerializedObject(definition);
            so.FindProperty("_family").enumValueFlag = (int)gun.Family;
            so.FindProperty("_accuracy.HipSpread").floatValue = gun.HipSpread;
            so.FindProperty("_accuracy.AimSpread").floatValue = 0f;
            SerializedProperty slots = so.FindProperty("_slots");
            slots.arraySize = gun.Slots.Length;
            for (int i = 0; i < gun.Slots.Length; i++)
                slots.GetArrayElementAtIndex(i).enumValueIndex = (int)gun.Slots[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssetIfDirty(definition);
        }
        return report.ToString();
    }

    // GunMounts on the first-person Weapon (held) or the World/Model copy: the same model space, other node names, so
    // nodes are matched by mesh.
    static GunMounts Build(GunSpec gun, Transform weapon, Transform target, bool held)
    {
        foreach (Transform old in target.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Mount_") || t.name.StartsWith("Adapter_")).ToArray())
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
        if (!target.TryGetComponent(out GunMounts mounts))
            mounts = target.gameObject.AddComponent<GunMounts>();
        var so = new SerializedObject(mounts);
        SerializedProperty list = so.FindProperty("_mounts");
        list.arraySize = gun.Mounts.Length;
        for (int i = 0; i < gun.Mounts.Length; i++)
        {
            MountSpec spec = gun.Mounts[i];
            Transform parent = held ? (spec.Parent.Length > 0 ? weapon.Find(spec.Parent) ?? throw new Exception($"{gun.Name}: no {spec.Parent}") : weapon) : target;
            Vector3 at = spec.At;
            if (spec.AtMuzzle)
            {
                Transform muzzle = weapon.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Muzzle")
                    ?? throw new Exception($"{gun.Name}: no Muzzle marker");
                at = weapon.InverseTransformPoint(muzzle.position) + MuzzleOverlap;
            }
            Transform point = new GameObject("Mount_" + spec.Slot).transform;
            point.SetParent(parent, false);
            point.localPosition = parent.InverseTransformPoint(target.TransformPoint(at));
            point.gameObject.layer = parent.gameObject.layer;

            var show = spec.Show.Select(name => Match(weapon, target, name, held)).Where(go => go != null).ToList();
            foreach ((string source, string child, Vector3 adapterAt) in spec.Adapters)
                show.Add(Adapter(source, child, parent, target.TransformPoint(adapterAt)));
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("Slot").enumValueIndex = (int)spec.Slot;
            entry.FindPropertyRelative("Point").objectReferenceValue = point;
            Fill(entry.FindPropertyRelative("ShowWhenFitted"), show);
            Fill(entry.FindPropertyRelative("HideWhenFitted"), spec.Hide.Select(name => Match(weapon, target, name, held)).Where(go => go != null).ToList());
        }
        Fill(so.FindProperty("_magazines"), gun.Magazines.Select(name => Match(weapon, target, name, held)).Where(go => go != null).Distinct()
            .Select(go => (Object)go.transform).ToList());
        // A built-in tube scope's window (GunBlockoutSetup adds it under the scope, e.g. the hunting rifle's).
        so.FindProperty("_sightWindow").objectReferenceValue = held
            ? target.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "SightWindow" && r.GetComponentInParent<AttachmentModel>(true) == null)
            : null;
        so.ApplyModifiedPropertiesWithoutUndo();
        return mounts;
    }

    static void Fill<T>(SerializedProperty array, List<T> values) where T : Object
    {
        array.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // The node named `name` under the held Weapon, or the world model's node with the same mesh (null when the world
    // model has none: a mesh only the first-person model was given, like the MP5's levelled sight).
    static GameObject Match(Transform weapon, Transform target, string name, bool held)
    {
        Transform node = weapon.Find(name) ?? throw new Exception($"{weapon.root.name}: no Weapon/{name}");
        if (held)
            return node.gameObject;
        if (!node.TryGetComponent(out MeshFilter filter))
            return null;
        MeshFilter twin = target.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh == filter.sharedMesh);
        if (twin == null)
            Debug.LogWarning($"{weapon.root.name}: World/Model has no {filter.sharedMesh.name}; its mount there skips it.");
        return twin != null ? twin.gameObject : null;
    }

    // A rail or adapter mesh copied from Castle Bravo, hidden until a part is fitted.
    static GameObject Adapter(string source, string child, Transform parent, Vector3 worldPosition)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CB}{source}.prefab") ?? throw new Exception("missing " + source);
        Transform from = child != null ? prefab.transform.Find(child) ?? throw new Exception($"{source} has no {child}") : prefab.transform;
        var node = new GameObject("Adapter_" + from.name);
        node.transform.SetParent(parent, false);
        node.transform.position = worldPosition;
        node.layer = parent.gameObject.layer;
        node.AddComponent<MeshFilter>().sharedMesh = from.GetComponent<MeshFilter>().sharedMesh;
        var renderer = node.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = from.GetComponent<MeshRenderer>().sharedMaterials;
        MeshRenderer like = parent.GetComponentInChildren<MeshRenderer>(true);
        if (like != null)
            renderer.shadowCastingMode = like.shadowCastingMode;
        node.SetActive(false);
        return node;
    }

    // Player prefab: the camera pitch (the eye) in the space of the hand the held guns sit on.
    static Matrix4x4 HandToEye()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath) ?? throw new Exception("missing " + PlayerPath);
        Transform hand = player.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Hand" && t.parent != null && t.parent.name == "HeldItems")
            ?? throw new Exception("Player has no HeldItems/Hand");
        Transform eye = hand.parent.parent;
        return eye.worldToLocalMatrix * hand.localToWorldMatrix;
    }

    // The iron sights' line in the aim pose: the eye's forward axis, carried into the model's space with the gun at its Aim
    // pose, marked where it crosses the model's origin depth (forward along the line).
    static Transform IronSight(Transform root, Transform weapon, Matrix4x4 handToEye)
    {
        Transform aim = root.Find("Poses/Aim") ?? throw new Exception(root.name + ": no Poses/Aim");
        Transform posePivot = root.Find("PosePivot");
        Matrix4x4 eyeToHand = handToEye.inverse;
        Matrix4x4 handToPose = Matrix4x4.TRS(aim.localPosition, aim.localRotation, Vector3.one).inverse;
        // Model space relative to the pose pivot (identity for these guns at rest).
        Matrix4x4 poseToModel = weapon.worldToLocalMatrix * posePivot.localToWorldMatrix;
        Matrix4x4 eyeToModel = poseToModel * handToPose * eyeToHand;
        Vector3 origin = eyeToModel.MultiplyPoint(Vector3.zero);
        Vector3 along = eyeToModel.MultiplyVector(Vector3.forward).normalized;
        Vector3 point = origin + along * (-origin.z / along.z);

        Transform existing = weapon.Find("IronSight");
        Transform marker = existing != null ? existing : new GameObject("IronSight").transform;
        marker.SetParent(weapon, false);
        marker.SetLocalPositionAndRotation(point, Quaternion.LookRotation(along, Vector3.up));
        return marker;
    }
}
