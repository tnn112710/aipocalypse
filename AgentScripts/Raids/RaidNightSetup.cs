using System;
using System.Collections.Generic;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.EnemySystem;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.RaidSystem;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Raid night content: the Enemies sound library (goblin voices, swing, impact, steps, war horn), the player's hurt grunt and
// the ammo crate sound; the goblin's sound set, spawn card, AttackDamage and the scene catalogue. Safe to re-run; saves only
// what it touches. Run: unity command script run --file AgentScripts/Raids/RaidNightSetup.cs --entry RaidNightSetup.Audio
//                      (then --entry RaidNightSetup.EnemyData, then with WorldMap open --entry RaidNightSetup.Raid)
public static class RaidNightSetup
{
    const string AudioDir = "Assets/_Game/Data/Audio";
    const string EnemiesLib = AudioDir + "/Enemies.asset";
    const string EnemiesDir = AudioDir + "/Enemies";
    const string PlayerLib = AudioDir + "/Player.asset";
    const string PlayerDir = AudioDir + "/Player";
    const string GM = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/";
    const string Malbers = "Assets/_StoreAssets/Characters/Malbers Animations/Common/Audio/";
    const string Survival = "Assets/_StoreAssets/Audio/PlaceHolder Inc/Survival Game Sounds - Lite Edition/Objects and Items/";
    const string GoblinDir = "Assets/_Game/Content/Enemies/Goblin";
    const string GoblinDefinition = GoblinDir + "/Goblin.asset";
    const string GoblinMelee = GoblinDir + "/Goblin_Melee.asset";
    const string GoblinSounds = GoblinDir + "/Goblin_Sounds.asset";
    const string GoblinCard = GoblinDir + "/Goblin_Card.asset";
    const string CapsuleDefinition = "Assets/_Game/Content/Enemies/WanderCapsule/WanderCapsule.asset";
    const string SystemSettings = "Assets/_Game/Data/Enemies/EnemySystemSettings.asset";
    const string RaidsDir = "Assets/_Game/Data/Raids";
    const string SettingsPath = RaidsDir + "/RaidSettings.asset";
    const string PlanPath = RaidsDir + "/Night_Default.asset";
    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    // The home gate's centre and the yard's outward facing (ENVIRONMENT.md: HomeGate, HomeYaw).
    const float GateX = 274.43f, GateZ = 294.15f, GateGroundY = 29.6f, HomeYaw = 50f;

    public static object Audio()
    {
        var log = new List<string>();
        Folder(EnemiesDir);
        AudioAsset enemies = Library(EnemiesLib, "Enemies", log);
        AudioAsset player = AssetDatabase.LoadAssetAtPath<AudioAsset>(PlayerLib) ?? throw new Exception("missing " + PlayerLib);

        SpatialSetting near = Spatial(EnemiesDir + "/Enemy_Spatial.asset", 2f, 40f, AudioRolloffMode.Logarithmic);
        SpatialSetting far = Spatial(EnemiesDir + "/Horn_Spatial.asset", 10f, 220f, AudioRolloffMode.Linear);

        AudioEntity e;
        e = Entity(enemies, EnemiesDir, "Goblin_Attack", 0.8f, 0.85f, 0.12f, Range(GM + "Voice/GoblinFairy/goblin_fairy_attack_", 24));
        Finish(e, near, Group(EnemiesDir + "/Goblin_Voice_Group.asset", 4), log);
        e = Entity(enemies, EnemiesDir, "Goblin_Hurt", 0.85f, 0.85f, 0.12f, Range(GM + "Voice/GoblinFairy/goblin_fairy_hurt_pain_", 22));
        Finish(e, near, Group(EnemiesDir + "/Goblin_Hurt_Group.asset", 3), log);
        e = Entity(enemies, EnemiesDir, "Goblin_Death", 0.9f, 0.85f, 0.1f, Range(GM + "Voice/GoblinFairy/goblin_fairy_death_", 11));
        Finish(e, near, Group(EnemiesDir + "/Goblin_Death_Group.asset", 3), log);
        e = Entity(enemies, EnemiesDir, "Goblin_Shout", 0.9f, 0.85f, 0.1f, Range(GM + "Voice/GoblinFairy/goblin_fairy_battle_shout_", 8));
        Finish(e, near, Group(EnemiesDir + "/Goblin_Shout_Group.asset", 2), log);
        var steps = new List<string>(Range(GM + "Footsteps/footstep_grass_walk_", 10));
        steps.AddRange(Range(GM + "Footsteps/footstep_grass_run_", 10));
        e = Entity(enemies, EnemiesDir, "Enemy_Step_Grass", 0.45f, 1f, 0.15f, steps.ToArray());
        Finish(e, near, Group(EnemiesDir + "/Enemy_Step_Group.asset", 6), log);
        e = Entity(enemies, EnemiesDir, "Enemy_Swing", 0.8f, 1f, 0.15f, Malbers + "Weapons/Melee/Melee Swing.wav");
        Finish(e, near, null, log);
        e = Entity(enemies, EnemiesDir, "Enemy_Impact", 1f, 1f, 0.1f, Malbers + "Hits/Flesh Hit.wav", Malbers + "Hits/Flesh Hit2.wav");
        Finish(e, near, null, log);
        e = Entity(enemies, EnemiesDir, "Raid_Horn_Goblin", 1f, 1f, 0.04f, Range(GM + "Comedy_Cartoon/battle_viking_horn_call_far_", 5));
        Finish(e, far, null, log);

        e = Entity(player, PlayerDir, "Player_Hurt", 0.9f, 1f, 0.08f, Range(GM + "Voice/Human Male A/voice_male_grunt_pain_", 10));
        Finish(e, null, null, log);
        e = Entity(player, PlayerDir, "AmmoCrate_Take", 0.9f, 1f, 0.08f, Survival + "Bag 1-3.wav", Survival + "Bag 5-5.wav");
        Finish(e, null, null, log);
        AssetDatabase.SaveAssetIfDirty(enemies);
        return string.Join("\n", log);
    }

    public static object EnemyData()
    {
        var log = new List<string>();
        var sounds = AssetDatabase.LoadAssetAtPath<EnemySoundSet>(GoblinSounds);
        if (sounds == null)
        {
            sounds = ScriptableObject.CreateInstance<EnemySoundSet>();
            AssetDatabase.CreateAsset(sounds, GoblinSounds);
            log.Add("created " + GoblinSounds);
        }
        sounds.Attack = Id("Goblin_Attack");
        sounds.Swing = Id("Enemy_Swing");
        sounds.Impact = Id("Enemy_Impact");
        sounds.Hurt = Id("Goblin_Hurt");
        sounds.Death = Id("Goblin_Death");
        sounds.Shout = Id("Goblin_Shout");
        sounds.Footstep = Id("Enemy_Step_Grass");
        Save(sounds);

        var goblin = Load<EnemyDefinition>(GoblinDefinition);
        goblin.SetBaseStat(EnemyStat.AttackDamage, 15f);
        goblin.Sounds = sounds;
        Save(goblin);
        var capsule = Load<EnemyDefinition>(CapsuleDefinition);
        capsule.SetBaseStat(EnemyStat.AttackDamage, 0f);
        Save(capsule);
        // Writes the new fields and drops the removed ones from the YAML.
        Save(Load<MeleeSettings>(GoblinMelee));

        var card = AssetDatabase.LoadAssetAtPath<EnemySpawnCard>(GoblinCard);
        if (card == null)
        {
            card = ScriptableObject.CreateInstance<EnemySpawnCard>();
            AssetDatabase.CreateAsset(card, GoblinCard);
            log.Add("created " + GoblinCard);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { GoblinDir }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab.TryGetComponent(out EnemyBrain brain))
            {
                card.SetVariant(brain, 1f);
                log.Add("variant " + prefab.name);
            }
        }
        card.GroupSize = new Vector2Int(2, 3);
        card.ArrivalCue = Id("Raid_Horn_Goblin");
        Save(card);

        var settings = Load<EnemySystemSettings>(SystemSettings);
        if (!settings.Cards.Contains(card))
            settings.Cards.Add(card);
        Save(settings);

        string[] tunings = AssetDatabase.FindAssets("t:" + nameof(PlayerSoundTuning));
        foreach (string guid in tunings)
        {
            var tuning = AssetDatabase.LoadAssetAtPath<PlayerSoundTuning>(AssetDatabase.GUIDToAssetPath(guid));
            var so = new SerializedObject(tuning);
            so.FindProperty("_hurtSound._entity").objectReferenceValue = Entity("Player", "Player_Hurt");
            so.ApplyModifiedPropertiesWithoutUndo();
            Save(tuning);
        }
        log.Add($"goblin AttackDamage 15, sounds, card ({card.Variants.Count} variants), {tunings.Length} player sound tuning(s)");
        return string.Join("\n", log);
    }

    // The raid data (Data/Raids: settings + the nightly plan, rewritten to three goblin waves) and the open WorldMap's
    // wiring: a RaidInstaller on its GameLifetimeScope and Raid/Spawn Zone (Front) at the gate, facing out. Saves only those.
    public static object Raid()
    {
        var log = new List<string>();
        Scene open = SceneManager.GetActiveScene();
        if (open.path == WorldMapPath && open.isDirty)
            return "WorldMap has unsaved changes: save or discard them, then run Raid again (nothing changed)";
        Folder(RaidsDir);
        var card = Load<EnemySpawnCard>(GoblinCard);
        var plan = AssetDatabase.LoadAssetAtPath<RaidPlan>(PlanPath);
        if (plan == null)
        {
            plan = ScriptableObject.CreateInstance<RaidPlan>();
            AssetDatabase.CreateAsset(plan, PlanPath);
            log.Add("created " + PlanPath);
        }
        plan.Waves.Clear();
        foreach (int count in new[] { 5, 6, 7 })
            plan.Waves.Add(new RaidWave { Entries = new List<RaidWaveEntry> { new(card, count) } });
        Save(plan);

        var settings = AssetDatabase.LoadAssetAtPath<RaidSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<RaidSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            log.Add("created " + SettingsPath);
        }
        settings.Plan = plan;
        Save(settings);

        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != WorldMapPath)
            throw new Exception("open WorldMap first (active: " + scene.path + ")");
        var scope = UnityEngine.Object.FindAnyObjectByType<GameLifetimeScope>() ?? throw new Exception("no GameLifetimeScope");
        if (!scope.TryGetComponent(out RaidInstaller installer))
        {
            installer = scope.gameObject.AddComponent<RaidInstaller>();
            log.Add("added RaidInstaller to " + scope.name);
        }
        var installerSo = new SerializedObject(installer);
        installerSo.FindProperty("_settings").objectReferenceValue = settings;
        installerSo.ApplyModifiedPropertiesWithoutUndo();

        GameObject root = GameObject.Find("/Raid") ?? new GameObject("Raid");
        Transform zoneTransform = root.transform.Find("Spawn Zone (Front)");
        if (zoneTransform == null)
        {
            zoneTransform = new GameObject("Spawn Zone (Front)", typeof(SpawnZone)).transform;
            zoneTransform.SetParent(root.transform, false);
        }
        var gate = new Vector3(GateX, 60f, GateZ);
        if (Physics.Raycast(gate, Vector3.down, out RaycastHit hit, 100f, 1))
            gate.y = hit.point.y;
        else
            gate.y = GateGroundY;
        zoneTransform.SetPositionAndRotation(gate, Quaternion.Euler(0f, HomeYaw, 0f));
        installer.CollectSpawnZones();
        EditorUtility.SetDirty(installer);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.Add($"zone at {gate}, yaw {HomeYaw}; plan {plan.TotalEnemies} goblins in {plan.Waves.Count} waves; scene saved");
        return string.Join("\n", log);
    }

    static T Load<T>(string path) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("missing " + path);

    static void Save(UnityEngine.Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }

    static AudioEntity Entity(string library, string name) => Load<AudioEntity>($"{AudioDir}/{library}/{name}.asset");

    static SoundID Id(string enemyEntity) => new(Entity("Enemies", enemyEntity));

    static string[] Range(string prefix, int count)
    {
        var paths = new string[count];
        for (int i = 0; i < count; i++)
            paths[i] = $"{prefix}{i + 1:00}.wav";
        return paths;
    }

    static AudioAsset Library(string path, string name, List<string> log)
    {
        var library = AssetDatabase.LoadAssetAtPath<AudioAsset>(path);
        if (library != null)
            return library;
        library = ScriptableObject.CreateInstance<AudioAsset>();
        AssetDatabase.CreateAsset(library, path);
        var so = new SerializedObject(library);
        so.FindProperty("<AssetName>k__BackingField").stringValue = name;
        so.FindProperty("_assetGUID").stringValue = AssetDatabase.AssetPathToGUID(path);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("created library " + path);
        return library;
    }

    // A one-shot SFX entity with shuffled clips (no immediate repeat), a base pitch and a random pitch range.
    static AudioEntity Entity(AudioAsset library, string dir, string name, float volume, float pitch, float pitchRandom, params string[] clips)
    {
        string path = $"{dir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)BroAudioType.SFX;
        so.FindProperty("<Loop>k__BackingField").boolValue = false;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = pitch;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)(clips.Length > 1 ? MulticlipsPlayMode.Shuffle : MulticlipsPlayMode.Single);
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = pitchRandom > 0f ? 1 : 0;
        var list = so.FindProperty("Clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]) ?? throw new Exception("missing clip " + clips[i]);
            var element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue = clip;
            element.FindPropertyRelative("Volume").floatValue = 1f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return entity;
    }

    static void Finish(AudioEntity entity, SpatialSetting spatial, PlaybackGroup group, List<string> log)
    {
        var so = new SerializedObject(entity);
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = spatial;
        so.FindProperty("_group").objectReferenceValue = group;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
        log.Add($"entity {AssetDatabase.GetAssetPath(entity)}");
    }

    // 3D range shared by several entities; doppler off (raiders are slow, the shift only sounds wrong).
    static SpatialSetting Spatial(string path, float min, float max, AudioRolloffMode rolloff)
    {
        var spatial = AssetDatabase.LoadAssetAtPath<SpatialSetting>(path);
        if (spatial == null)
        {
            spatial = ScriptableObject.CreateInstance<SpatialSetting>();
            AssetDatabase.CreateAsset(spatial, path);
        }
        spatial.MinDistance = min;
        spatial.MaxDistance = max;
        spatial.RolloffMode = rolloff;
        spatial.DopplerLevel = 0f;
        Save(spatial);
        return spatial;
    }

    // One group per entity: a group asset counts its own playing sounds, so sharing one would cap several entities together.
    static PlaybackGroup Group(string path, int maxPlaying)
    {
        var group = AssetDatabase.LoadAssetAtPath<DefaultPlaybackGroup>(path);
        if (group == null)
        {
            group = ScriptableObject.CreateInstance<DefaultPlaybackGroup>();
            AssetDatabase.CreateAsset(group, path);
        }
        var so = new SerializedObject(group);
        so.FindProperty("_maxPlayableCount.Value").intValue = maxPlaying;
        so.FindProperty("_maxPlayableCount._isOverride").boolValue = true;
        so.FindProperty("_logCombFilteringWarning").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(group);
        return group;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
