using System;
using System.Collections.Generic;
using System.Reflection;
using Aipocalypse.Core.PlayerSystem;
using Ami.BroAudio;
using Ami.BroAudio.Data;
using DG.Tweening;
using UnityEditor;
using UnityEngine;

// Builds the death pose sequence: sound entities (kneel, groan, body fall, music), Data/Player/Sequences/PlayerDeath.asset,
// and links it as PlayerTuning.DeathSequence. Safe to re-run; `resetBeats` rewrites the beats to the defaults below.
public static class PoseSequenceSetup
{
    const string TuningPath = "Assets/_Game/Data/Player/PlayerTuning.asset";
    const string SequenceDir = "Assets/_Game/Data/Player/Sequences";
    const string DeathPath = SequenceDir + "/PlayerDeath.asset";
    const string PlayerLibPath = "Assets/_Game/Data/Audio/Player.asset";
    const string PlayerAudioDir = "Assets/_Game/Data/Audio/Player";
    const string MusicLibPath = "Assets/_Game/Data/Audio/Music.asset";
    const string MusicAudioDir = "Assets/_Game/Data/Audio/Music";
    const string Lakefell = "Assets/_StoreAssets/Env/Raygeas/Lakefell Village/Assets/Audio/Player/";
    const string Malbers = "Assets/_StoreAssets/Animals/Malbers Animations/Common/Audio/";
    const string Samples = "Assets/_Samples/Audio/";

    public static object Death(bool resetBeats)
    {
        var log = new List<string>();
        AssetDatabase.Refresh();
        Folder(SequenceDir);
        Folder(MusicAudioDir);

        var playerLib = AssetDatabase.LoadAssetAtPath<AudioAsset>(PlayerLibPath) ?? throw new Exception("missing " + PlayerLibPath);
        var musicLib = Library(MusicLibPath, "Music", log);

        var kneel = Entity(playerLib, PlayerAudioDir, "Kneel", BroAudioType.SFX, 0.8f, 0.85f, MulticlipsPlayMode.Random, 0.05f,
            Lakefell + "Player_Footstep_Grass_3.wav", Lakefell + "Player_Footstep_Grass_6.wav");
        var groan = Entity(playerLib, PlayerAudioDir, "DeathGroan", BroAudioType.SFX, 0.9f, 1f, MulticlipsPlayMode.Shuffle, 0.04f,
            Samples + "Voice/DeathGroan_1.wav", Samples + "Voice/DeathGroan_2.wav", Samples + "Voice/DeathGroan_3.wav");
        var fall = Entity(playerLib, PlayerAudioDir, "BodyFall", BroAudioType.SFX, 1f, 0.9f, MulticlipsPlayMode.Single, 0.04f,
            Malbers + "Steps/FallLand.wav");
        // The clip starts and ends at full volume: a seamless loop crossfades it into itself until the player revives.
        var music = Entity(musicLib, MusicAudioDir, "DeathMusic", BroAudioType.Music, 0.8f, 1f, MulticlipsPlayMode.Single, 0f,
            Samples + "Music/DeathMusic_ThreeArrows.ogg");
        var mso = new SerializedObject(music);
        mso.FindProperty("<SeamlessLoop>k__BackingField").boolValue = true;
        mso.FindProperty("<TransitionTime>k__BackingField").floatValue = 1.2f;
        mso.FindProperty("SeamlessTransitionType").intValue = (int)SeamlessType.Time;
        mso.ApplyModifiedPropertiesWithoutUndo();
        log.Add("entities: Kneel, DeathGroan, BodyFall, DeathMusic");

        bool created = AssetDatabase.LoadAssetAtPath<PoseSequence>(DeathPath) == null;
        var death = created ? ScriptableObject.CreateInstance<PoseSequence>() : AssetDatabase.LoadAssetAtPath<PoseSequence>(DeathPath);
        if (created)
            AssetDatabase.CreateAsset(death, DeathPath);
        if (created || resetBeats)
        {
            Set(death, "LowerHands", true);
            Set(death, "Beats", new[]
            {
                Beat("Hit", 0.35f, Ease.OutQuad, 1.45f, 0f, 0f, 18f, 3f, 5f, 1f, 0f),
                Beat("Kneel, look at the ground", 0.75f, Ease.InOutSine, 0.95f, 0.05f, 0.2f, 60f, -8f, 7f, 1f, 0f),
                Beat("Look across the grass", 1f, Ease.InOutSine, 0.85f, 0.08f, 0.28f, 52f, 12f, 3f, 1f, 0f),
                Beat("Topple onto the side", 0.75f, Ease.InQuad, 0.5f, 0.45f, -0.6f, 10f, 4f, -78f, 1f, 0f),
                Beat("Roll onto the back, look up", 2.6f, Ease.InOutSine, 0.6f, 0.6f, -0.9f, -28f, 14f, 0f, 1.12f, 0f),
            });
            Set(death, "Continuous", true);
            Set(death, "HandheldAngles", new Vector3(1.2f, 1.6f, 0.9f));
            Set(death, "HandheldOffset", 0.012f);
            Set(death, "HandheldSpeed", 0.45f);
            Set(death, "DriftAmplitude", new Vector2(3f, 6f));
            Set(death, "DriftPeriod", 10f);
            Set(death, "EndAfter", 6f);
            Set(death, "MusicBeat", 4);
            Set(death, "MusicDelay", 0.3f);
            Set(death, "MusicFadeIn", 1.5f);
            Set(death, "MusicFadeOut", 1f);
            log.Add(created ? "created " + DeathPath : "reset beats");
        }
        EditorUtility.SetDirty(death);

        var so = new SerializedObject(death);
        Sound(so, "<Beats>k__BackingField.Array.data[0].StartSound", groan);
        Sound(so, "<Beats>k__BackingField.Array.data[1].ArriveSound", kneel);
        Sound(so, "<Beats>k__BackingField.Array.data[3].ArriveSound", fall);
        Sound(so, "<Music>k__BackingField", music);
        so.ApplyModifiedPropertiesWithoutUndo();

        var tuning = AssetDatabase.LoadAssetAtPath<PlayerTuning>(TuningPath) ?? throw new Exception("missing " + TuningPath);
        var tso = new SerializedObject(tuning);
        tso.FindProperty("<DeathSequence>k__BackingField").objectReferenceValue = death;
        tso.ApplyModifiedPropertiesWithoutUndo();
        log.Add("linked PlayerTuning.DeathSequence");

        foreach (UnityEngine.Object asset in new UnityEngine.Object[] { death, tuning, musicLib, kneel, groan, fall, music })
            AssetDatabase.SaveAssetIfDirty(asset);
        return string.Join("\n", log);
    }

    static PoseBeat Beat(string name, float duration, Ease ease, float height, float x, float z, float pitch, float yaw, float roll,
        float fov, float hold) => new()
    {
        Name = name, Duration = duration, Ease = ease, Height = height, Offset = new Vector2(x, z),
        Pitch = pitch, Yaw = yaw, Roll = roll, FovScale = fov, Hold = hold,
    };

    static void Set(PoseSequence sequence, string property, object value)
    {
        FieldInfo field = typeof(PoseSequence).GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new Exception("no property " + property);
        field.SetValue(sequence, value);
    }

    static void Sound(SerializedObject so, string path, AudioEntity entity)
    {
        SerializedProperty property = so.FindProperty(path + "._entity") ?? throw new Exception("no SoundID at " + path);
        property.objectReferenceValue = entity;
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

    static AudioEntity Entity(AudioAsset library, string dir, string name, BroAudioType type, float volume, float pitch,
        MulticlipsPlayMode mode, float pitchRandom, params string[] clips)
    {
        string path = $"{dir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<AudioEntity>(path);
        if (entity == null)
        {
            entity = AudioEntity.CreateNewInstance(library, name, type);
            AssetDatabase.CreateAsset(entity, path);
        }

        var so = new SerializedObject(entity);
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)type;
        so.FindProperty("<Loop>k__BackingField").boolValue = false;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = pitch;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)mode;
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
        EditorUtility.SetDirty(entity);
        return entity;
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
