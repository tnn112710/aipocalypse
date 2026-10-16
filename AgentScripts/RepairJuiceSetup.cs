using System.Linq;
using Aipocalypse.Core.Feedback;
using Aipocalypse.Core.WeaponSystem;
using MoreMountains.Feedbacks;
using MoreMountains.FeedbacksForThirdParty;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

// Camera impulses, hit stop, jam / repair sounds and feel tuning for the gun jam and repair sequence. Safe to re-run.
public static class RepairJuiceSetup
{
    const string PlayerPath = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
    const string NoisePath = "Assets/_Game/Data/Camera/ImpulseReaction.asset";
    const string FeedbackPath = "Assets/_Game/Data/Weapons/RepairFeedback.asset";
    const string Malbers = "Assets/_StoreAssets/Animals/Malbers Animations/Common/Audio/";
    const string Highlands = "Assets/_StoreAssets/Env/Raygeas/HIGHLANDS - Stylized Environment/Assets/Audio/Environment/Items/";
    static readonly string[] Guns = { "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel" };

    public static object All()
    {
        var noise = ReactionNoise();
        var player = Player(noise);
        var sounds = Sounds();
        var feedback = Feedback();
        var profiles = Profiles();
        AssetDatabase.SaveAssets();
        // Repair clips: run Ak47Animations.UpdateRepairing for each gun (separate file).
        return new { player, sounds, feedback, profiles };
    }

    // Rotation-only shake; one unit of impulse strength gives about 1.5 degrees.
    static NoiseSettings ReactionNoise()
    {
        var noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoisePath);
        if (noise == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data/Camera"))
                AssetDatabase.CreateFolder("Assets/_Game/Data", "Camera");
            noise = ScriptableObject.CreateInstance<NoiseSettings>();
            AssetDatabase.CreateAsset(noise, NoisePath);
        }
        NoiseSettings.NoiseParams P(float frequency, float amplitude) => new() { Frequency = frequency, Amplitude = amplitude };
        noise.PositionNoise = new NoiseSettings.TransformNoiseParams[0];
        noise.OrientationNoise = new[]
        {
            new NoiseSettings.TransformNoiseParams { X = P(19f, 0.9f), Y = P(15f, 0.55f), Z = P(17f, 0.7f) },
            new NoiseSettings.TransformNoiseParams { X = P(6.5f, 0.6f), Y = P(5f, 0.35f), Z = P(7f, 0.4f) },
        };
        EditorUtility.SetDirty(noise);
        return noise;
    }

    static object Player(NoiseSettings noise)
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPath);
        try
        {
            var camera = root.GetComponentInChildren<CinemachineCamera>(true);
            var listener = camera.GetComponent<CinemachineImpulseListener>() ?? camera.gameObject.AddComponent<CinemachineImpulseListener>();
            listener.ChannelMask = 1;
            // The impulse itself only nudges the camera a little; its strength mostly drives the rotation reaction.
            listener.Gain = 0.015f;
            listener.UseCameraSpace = true;
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.Additive;
            listener.ReactionSettings = new CinemachineImpulseListener.ImpulseReaction
            {
                m_SecondaryNoise = noise, AmplitudeGain = 5f, FrequencyGain = 1f, Duration = 0.35f,
            };

            var old = root.transform.Find("GunJamFeedback");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var host = new GameObject("GunJamFeedback");
            host.transform.SetParent(root.transform, false);
            host.AddComponent<FeedbackContext>();
            var view = host.AddComponent<GunJamFeedback>();

            var so = new SerializedObject(view);
            so.FindProperty("_jammed").objectReferenceValue = Impulse(host, "Jammed", new Vector3(0.2f, -1f, 0.3f), 0.6f, 0.25f);
            so.FindProperty("_dryFire").objectReferenceValue = Impulse(host, "DryFire", new Vector3(0f, -1f, 0f), 0.2f, 0.12f);
            so.FindProperty("_slap").objectReferenceValue = Impulse(host, "Slap", new Vector3(0.6f, -0.8f, 0f), 0.45f, 0.18f);
            so.FindProperty("_punch").objectReferenceValue = Impulse(host, "Punch", new Vector3(0.5f, -0.8f, 0.3f), 0.9f, 0.22f, 0.035f, 0.1f);
            so.FindProperty("_miss").objectReferenceValue = Impulse(host, "Miss", new Vector3(-0.5f, -1f, 0f), 0.85f, 0.3f, shape: CinemachineImpulseDefinition.ImpulseShapes.Recoil);
            so.FindProperty("_success").objectReferenceValue = Impulse(host, "Success", new Vector3(0f, -0.6f, -1f), 1.6f, 0.3f, 0.08f, 0.05f);
            so.FindProperty("_natural").objectReferenceValue = Impulse(host, "Natural", new Vector3(0f, -1f, 0f), 0.3f, 0.2f);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
            return host.GetComponentsInChildren<MMF_Player>().Select(p => p.name + ": " + string.Join(", ", p.FeedbacksList.Select(f => f.Label))).ToArray();
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static MMF_Player Impulse(GameObject host, string name, Vector3 direction, float strength, float duration,
        float hitStop = 0f, float hitStopScale = 0.05f, CinemachineImpulseDefinition.ImpulseShapes shape = CinemachineImpulseDefinition.ImpulseShapes.Bump)
    {
        var go = new GameObject(name);
        go.transform.SetParent(host.transform, false);
        var player = go.AddComponent<MMF_Player>();
        player.CanPlayWhileAlreadyPlaying = true;
        player.PerformanceMode = true;
        var impulse = (MMF_CinemachineImpulse)player.AddFeedback(typeof(MMF_CinemachineImpulse));
        impulse.Label = name + " camera";
        impulse.m_ImpulseDefinition = new CinemachineImpulseDefinition
        {
            ImpulseChannel = 1,
            ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
            ImpulseShape = shape,
            ImpulseDuration = duration,
            DissipationDistance = 100f,
        };
        impulse.Velocity = direction.normalized * strength;
        if (hitStop > 0f)
        {
            var stop = (MMF_HitStop)player.AddFeedback(typeof(MMF_HitStop));
            stop.Duration = hitStop;
            stop.TimeScale = hitStopScale;
        }
        return player;
    }

    static AudioClip Clip(string path)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null)
            throw new System.InvalidOperationException("Missing clip " + path);
        return clip;
    }

    static object Sounds()
    {
        var jam = Clip(Malbers + "Weapons/Pistol/pistol Empty.wav");
        var dry = Clip(Malbers + "Weapons/Default Empty.wav");
        var punches = new[] { Clip(Malbers + "Hits/Hit1.wav"), Clip(Malbers + "Hits/Hit3.wav"), Clip(Highlands + "Item_Metal_Shield_1.wav") };
        foreach (string gun in Guns)
        {
            var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/" + gun + ".asset");
            Undo.RecordObject(definition, "Jam sounds");
            definition.Sounds.Jam = jam;
            definition.Sounds.DryFire = dry;
            definition.Sounds.RepairPunches = punches;
            definition.Sounds.RepairPunchVolume = 0.85f;
            EditorUtility.SetDirty(definition);
        }
        return new { jam = jam.name, dry = dry.name, punches = punches.Select(p => p.name).ToArray() };
    }

    // Writes the jam pause and miss penalty into each gun's repair profile.
    static object Profiles() => Guns.Select(gun =>
    {
        var profile = AssetDatabase.LoadAssetAtPath<GunRepairProfile>("Assets/_Game/Data/Weapons/" + gun + "_Repair.asset");
        Undo.RecordObject(profile, "Jam pause and miss penalty");
        profile.JamPause = 0.6f;
        profile.MissPenalty = 0.5f;
        profile.MaxMissPenalty = 1.5f;
        EditorUtility.SetDirty(profile);
        return profile.name;
    }).ToArray();

    static object Feedback()
    {
        var settings = AssetDatabase.LoadAssetAtPath<RepairFeedbackSettings>(FeedbackPath);
        var defaults = ScriptableObject.CreateInstance<RepairFeedbackSettings>();
        try
        {
            Undo.RecordObject(settings, "Repair juice");
            settings.JamJolt = defaults.JamJolt;
            settings.DryFireJolt = defaults.DryFireJolt;
            settings.SlapJolt = defaults.SlapJolt;
            settings.PunchJolt = defaults.PunchJolt;
            settings.SuccessJolt = defaults.SuccessJolt;
            settings.MissJolt = defaults.MissJolt;
            settings.SuccessScale = defaults.SuccessScale;
            settings.BurstScale = defaults.BurstScale;
            settings.MissColor = defaults.MissColor;
            settings.MissDuration = defaults.MissDuration;
            settings.MissShakePixels = defaults.MissShakePixels;
            settings.MissSquash = defaults.MissSquash;
            settings.MissVolume = defaults.MissVolume;
            settings.MissAccent = Clip(Malbers + "Weapons/Rock Hit.wav");
            settings.MissAccentVolume = 0.9f;
            settings.SuccessSound = Clip(Malbers + "Weapons/Melee/Clash On Metal/Sword_Clash_Metal_3.wav");
            settings.SuccessVolume = 0.75f;
            settings.SuccessMechanism = Clip("Assets/_Samples/Audio/Weapons/Sniper_1/Sniper_1 Bolt Push.wav");
            settings.MechanismVolume = 1f;
            settings.MechanismDelay = defaults.MechanismDelay;
            EditorUtility.SetDirty(settings);
            return new { success = settings.SuccessSound.name, mechanism = settings.SuccessMechanism.name, accent = settings.SuccessAccent?.name,
                miss = settings.MissSound?.name, missAccent = settings.MissAccent.name, natural = settings.NaturalSound?.name };
        }
        finally { Object.DestroyImmediate(defaults); }
    }
}
