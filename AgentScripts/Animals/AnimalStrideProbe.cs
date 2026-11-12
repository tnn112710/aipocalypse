// Measures how fast each farm animal's walk and run clips move their planted feet backwards (the ground speed the clip was
// made for, at model scale 1), so AnimalSetup's AuthoredWalk / AuthoredRun can be set from it instead of guessed:
//   unity command script run --file AgentScripts/Animals/AnimalStrideProbe.cs --entry AnimalStrideProbe.Run
// The feet are the bones that reach lowest during the cycle; a foot counts as planted while it is within 2 cm of its lowest.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class AnimalStrideProbe
{
    const string PP = "Assets/_StoreAssets/Characters/polyperfect/Low Poly Animated Animals/";
    const int Samples = 120;
    const float PlantedBand = 0.02f;

    static readonly (string Prefab, string Fbx, string Prefix)[] Looks =
    {
        ("Cow_White", "Cow/SKM_Cow_Animations", "Cow_"),
        ("Sheep_Wool", "Sheep/SKM_Sheep_Wool_Animations", "Sheep_Wool_"),
        ("Hen", "Hen/SKM_Hen_Animations", "Hen_"),
        ("Rooster", "Rooster/SKM_Rooster_Animations", "Rooster_"),
        ("Pig", "Pig/SKM_Pig_Animations", "Pig_"),
    };

    public static string Run()
    {
        var sb = new StringBuilder();
        foreach ((string prefab, string fbx, string prefix) in Looks)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(PP + "Meshes/Animals/" + fbx + ".fbx").OfType<AnimationClip>().ToDictionary(c => c.name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PP + "Prefabs/Animals/" + prefab + ".prefab"));
            try
            {
                model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.transform.localScale = Vector3.one;
                string walk = Measure(model, clips[prefix + "Walk"]);
                string run = Measure(model, clips[prefix + "Run"]);
                sb.AppendLine($"{prefab}: walk {walk}; run {run}");
            }
            finally
            {
                Object.DestroyImmediate(model);
            }
        }
        return sb.ToString();
    }

    static string Measure(GameObject model, AnimationClip clip)
    {
        Transform root = model.transform;
        Transform[] bones = model.GetComponentsInChildren<Transform>().Where(t => t != root && t.childCount <= 1).ToArray();
        var heights = new float[bones.Length, Samples];
        var forward = new float[bones.Length, Samples];
        float dt = clip.length / Samples;
        for (int s = 0; s < Samples; s++)
        {
            clip.SampleAnimation(model, s * dt);
            for (int b = 0; b < bones.Length; b++)
            {
                Vector3 local = root.InverseTransformPoint(bones[b].position);
                heights[b, s] = local.y;
                forward[b, s] = local.z;
            }
        }
        // Feet: the bones whose lowest point is near the lowest of all.
        var lows = Enumerable.Range(0, bones.Length).Select(b => Enumerable.Range(0, Samples).Min(s => heights[b, s])).ToArray();
        float floor = lows.Min();
        var feet = Enumerable.Range(0, bones.Length).Where(b => lows[b] < floor + 0.04f).ToList();
        var speeds = new List<float>();
        foreach (int b in feet)
        {
            float backwards = 0f, time = 0f;
            for (int s = 0; s < Samples; s++)
            {
                int next = (s + 1) % Samples;
                if (heights[b, s] > lows[b] + PlantedBand || heights[b, next] > lows[b] + PlantedBand)
                    continue;
                float step = forward[b, s] - forward[b, next];
                if (step <= 0f)
                    continue;
                backwards += step;
                time += dt;
            }
            if (time > clip.length * 0.15f)
                speeds.Add(backwards / time);
        }
        // Second reading: whichever foot is lowest each frame is the one on the ground; its backward speed is the ground
        // speed. The median over the frames where that foot is near the floor (a gallop has frames with none down).
        var legs = Enumerable.Range(0, bones.Length).Where(b => lows[b] < floor + 0.12f).ToList();
        var contact = new List<float>();
        for (int s = 0; s < Samples; s++)
        {
            int next = (s + 1) % Samples;
            int lowest = legs.OrderBy(b => heights[b, s]).First();
            if (heights[lowest, s] > floor + 0.05f)
                continue;
            contact.Add((forward[lowest, s] - forward[lowest, next]) / dt);
        }
        contact.Sort();
        string second = contact.Count > 0 ? $"lowest foot {contact[contact.Count / 2]:0.00} m/s over {contact.Count} frames" : "lowest foot: none";
        if (speeds.Count == 0)
            return $"planted: none; {second}";
        speeds.Sort();
        float median = speeds[speeds.Count / 2];
        return $"planted {median:0.00} m/s ({speeds.Count} feet); {second}";
    }
}
