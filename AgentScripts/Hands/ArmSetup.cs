// Builds first-person arms (Synty Sidekick arm and hand parts) for `unity command script run`. Each look is a
// "variant" with its own folder, Assets/_Game/Content/Player/Arms/<Variant>/, so several can sit side by side.
// build_arms.sh beside this file runs the whole chain for both sides:
//   1. WriteSk(sourceSk, variant, side)   a .sk holding only one side's upper arm, lower arm and hand
//   2. SidekickKit.Build on that .sk (tools-unity-synty-characters) -> Arm_<Side>_Model.prefab + mesh, material, color map
//   3. MakePrefab(variant, side)          FPArm_<Variant>_<Side>.prefab: HandRig + ArmRig, finger calibration, grip point
//                                         (re-run after changing the calibration numbers below)
//   4. Clean(variant)                     drops the intermediate model prefabs and avatars
//   5. Use(variant)                       points Data/Hands/HandSet.asset at that variant ("" = the floating hands)
// Rebuilding a variant overwrites in place (same GUIDs). Every Sidekick arm shares one skeleton, so item grips, finger
// poses and gun clips don't change with the variant.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MB13.Aipocalypse.Core.HandSystem;
using Synty.SidekickCharacters.Serialization;
using Unity.VisualScripting.YamlDotNet.Serialization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ArmSetup
{
    const string ArmsFolder = "Assets/_Game/Content/Player/Arms";
    const string HandsFolder = "Assets/_Game/_Core/HandSystem/Prefabs";
    const string HandSetPath = "Assets/_Game/Data/Hands/HandSet.asset";
    static readonly string[] FingerNames = { "thumb", "index", "middle", "ring", "pinky" };
    // Sidekick arms are life size and the guns about 1.3x: grown so the hand is the size the floating hands were.
    const float ArmScale = 1.2f;

    // Degrees each joint bends (base to tip) at Closed (round a handle) and at Fist.
    static readonly float[] ClosedFinger = { 52f, 78f, 42f };
    static readonly float[] FistFinger = { 82f, 100f, 62f };
    static readonly float[] ClosedThumb = { 14f, 22f, 34f };
    static readonly float[] FistThumb = { 24f, 34f, 52f };
    // Sideways fan at Spread, per finger (thumb, index, middle, ring, little).
    static readonly float[] SpreadAngle = { 22f, 10f, 0f, -9f, -18f };

    static string Folder(string variant) => $"{ArmsFolder}/{variant}";
    static string PrefabPath(string variant, string side) => $"{Folder(variant)}/FPArm_{variant}_{side}.prefab";

    public static string WriteSk(string sourceSk, string variant, string side)
    {
        string tag = side == "Left" ? "L_" : "R_";
        var character = new Deserializer().Deserialize<SerializedCharacter>(Encoding.ASCII.GetString(File.ReadAllBytes(sourceSk)));
        character.Parts = character.Parts
            .Where(p => p.Name.Contains("AUP" + tag) || p.Name.Contains("ALW" + tag) || p.Name.Contains("HND" + tag)).ToList();
        character.Name = "Arm_" + side;
        Directory.CreateDirectory(Folder(variant));
        string path = $"{Folder(variant)}/Arm_{side}.sk";
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes(new SerializerBuilder().EmitDefaults().Build().Serialize(character)));
        AssetDatabase.ImportAsset(path);
        return path + ": " + string.Join(", ", character.Parts.Select(p => p.Name));
    }

    public static string MakePrefab(string variant, string side)
    {
        string suffix = side == "Left" ? "_l" : "_r";
        string modelPath = $"{Folder(variant)}/Arm_{side}_Model.prefab";
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (model == null)
            return "no model at " + modelPath + " (run SidekickKit.Build first)";

        var root = new GameObject($"FPArm_{variant}_{side}");
        try
        {
            GameObject body = Object.Instantiate(model, root.transform);
            body.name = "Model";
            Object.DestroyImmediate(body.GetComponent<Animator>());
            Transform Find(string name) => body.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name + suffix);
            Transform upper = Find("upperarm"), lower = Find("lowerarm"), hand = Find("hand"), twist = Find("lowerarm_twist_01");
            Transform clavicle = Find("clavicle");
            if (upper == null || lower == null || hand == null || clavicle == null)
                return "arm bones not found";

            var renderer = body.GetComponentInChildren<SkinnedMeshRenderer>();
            string pruned = Prune(body.transform, renderer);
            renderer.rootBone = lower;
            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 1.2f);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.updateWhenOffscreen = false;

            var arm = root.AddComponent<ArmRig>();
            // The character stands facing +Z with its arms out: elbows point backwards.
            arm.Setup(clavicle.parent, upper, lower, hand, twist, -body.transform.forward);

            var rig = root.AddComponent<HandRig>();
            Calibrate(rig, arm, hand, suffix, side == "Left", renderer);

            // The upper arm starts at the prefab root, so the root's scale grows the arm from the shoulder.
            body.transform.position -= upper.position;
            root.transform.localScale = Vector3.one * ArmScale;

            string prefabPath = PrefabPath(variant, side);
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
            if (!saved)
                return "could not save " + prefabPath;
            return prefabPath + " " + pruned;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // Drops every bone the arm's vertices don't use (the rest of the body), keeping the chain down from the root.
    static string Prune(Transform body, SkinnedMeshRenderer renderer)
    {
        Transform[] bones = renderer.bones;
        var used = new HashSet<Transform>();
        foreach (BoneWeight1 weight in renderer.sharedMesh.GetAllBoneWeights())
        {
            if (weight.weight > 0f && bones[weight.boneIndex] != null)
                used.Add(bones[weight.boneIndex]);
        }
        var keep = new HashSet<Transform>();
        foreach (Transform bone in used)
        {
            // Fingers and twist bones under a used bone stay too: the hand rig poses them.
            foreach (Transform child in bone.GetComponentsInChildren<Transform>(true))
                if (!child.name.Contains("Attach") && !child.name.StartsWith("prop_"))
                    keep.Add(child);
            for (Transform t = bone; t != null && t != body.parent; t = t.parent)
                keep.Add(t);
        }
        keep.Add(renderer.transform);
        int removed = 0;
        foreach (Transform t in body.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || keep.Contains(t) || t == body)
                continue;
            if (t.parent != null && !keep.Contains(t.parent) && t.parent != body)
                continue;
            removed += t.GetComponentsInChildren<Transform>(true).Length;
            Object.DestroyImmediate(t.gameObject);
        }
        return $"(weighted bones: {string.Join(",", used.Select(b => b.name))}; removed {removed} transforms)";
    }

    // Once the arms are final: the intermediate model prefabs and avatars from SidekickKit.Build aren't referenced.
    public static string Clean(string variant)
    {
        foreach (string side in new[] { "Left", "Right" })
        {
            AssetDatabase.DeleteAsset($"{Folder(variant)}/Arm_{side}_Model.prefab");
            AssetDatabase.DeleteAsset($"{Folder(variant)}/Arm_{side}_Avatar.asset");
        }
        return "removed the intermediate models of " + variant;
    }

    public static string Use(string variant)
    {
        var set = AssetDatabase.LoadAssetAtPath<HandSet>(HandSetPath);
        var so = new SerializedObject(set);
        foreach (string side in new[] { "Left", "Right" })
        {
            string path = string.IsNullOrEmpty(variant) ? $"{HandsFolder}/FPHand_{side}.prefab" : PrefabPath(variant, side);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return "missing " + path;
            so.FindProperty(side == "Left" ? "_left" : "_right").objectReferenceValue = prefab.GetComponent<HandRig>();
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssetIfDirty(set);
        return "HandSet uses " + (string.IsNullOrEmpty(variant) ? "the floating hands" : variant);
    }

    static void Calibrate(HandRig rig, ArmRig arm, Transform hand, string suffix, bool left, SkinnedMeshRenderer renderer)
    {
        Transform Bone(string finger, int joint) =>
            hand.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == $"{finger}_0{joint}{suffix}");

        // Palm frame from the rest pose: along = wrist to middle knuckle, across = little to index, back = out of the back of the hand.
        Vector3 along = (Bone("middle", 1).position - hand.position).normalized;
        Vector3 across = Vector3.ProjectOnPlane(Bone("index", 1).position - Bone("pinky", 1).position, along).normalized;
        Vector3 back = Vector3.Cross(across, along) * (left ? 1f : -1f);
        // Fingers are modelled slightly curled: that curl's direction is the palm side.
        Vector3 curlHint = Bone("middle", 3).position - Bone("middle", 2).position;
        if (Vector3.Dot(Vector3.ProjectOnPlane(curlHint, along), back) > 0f)
            back = -back;

        var so = new SerializedObject(rig);
        so.FindProperty("_side").enumValueIndex = left ? 0 : 1;
        so.FindProperty("_arm").objectReferenceValue = arm;
        so.FindProperty("_fingerRadius").floatValue = 0.011f;
        so.FindProperty("_palmThickness").floatValue = 0.028f;
        SerializedProperty renderers = so.FindProperty("_renderers");
        renderers.arraySize = 1;
        renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;

        // The grip point: in the palm under the knuckles, where a handle's centre sits. Z little -> index, Y out of the back.
        var grip = new GameObject("GripPoint").transform;
        grip.SetParent(hand, false);
        Vector3 knuckles = (Bone("index", 1).position + Bone("pinky", 1).position) * 0.5f;
        grip.position = Vector3.Lerp(hand.position, knuckles, 0.82f) - back * 0.03f;
        // A handle crosses the palm on a slant: the index side sits further up the hand than the little-finger side.
        Vector3 handle = (across + along * 0.25f).normalized;
        grip.rotation = Quaternion.LookRotation(handle, Vector3.ProjectOnPlane(back, handle).normalized);
        so.FindProperty("_gripPoint").objectReferenceValue = grip;

        SerializedProperty fingers = so.FindProperty("_fingers");
        fingers.arraySize = FingerNames.Length;
        for (int f = 0; f < FingerNames.Length; f++)
        {
            // Joint 0 is a still marker at the wrist standing in for the metacarpal the rig lacks: fitting and the
            // forearm direction read "wrist, knuckle, joints" from the chain.
            var meta = new GameObject(FingerNames[f] + "_meta" + suffix).transform;
            meta.SetParent(hand, false);
            var joints = new List<Transform> { meta };
            for (int j = 1; j <= 3; j++)
                joints.Add(Bone(FingerNames[f], j));

            bool thumb = f == 0;
            float[] closed = thumb ? ClosedThumb : ClosedFinger;
            float[] fist = thumb ? FistThumb : FistFinger;
            SerializedProperty chain = fingers.GetArrayElementAtIndex(f);
            Write(chain.FindPropertyRelative("Joints"), joints.Select(j => (Object)j).ToArray());
            var open = new Quaternion[4];
            var closedRotations = new Quaternion[4];
            var fistRotations = new Quaternion[4];
            var spread = new Quaternion[4];
            for (int j = 0; j < 4; j++)
            {
                Transform joint = joints[j];
                open[j] = closedRotations[j] = fistRotations[j] = spread[j] = joint.localRotation;
                if (j == 0 || thumb)
                    continue;
                Vector3 bendAxis = across * (left ? 1f : -1f);
                closedRotations[j] = Turned(joint, bendAxis, closed[j - 1]);
                fistRotations[j] = Turned(joint, bendAxis, fist[j - 1]);
                if (j == 1)
                    spread[j] = Turned(joint, back, SpreadAngle[f] * (left ? 1f : -1f));
            }
            if (thumb)
            {
                // The thumb is posed by where its three bones point in the palm's frame (along, across to the index side, back).
                Vector3 Palm(float a, float c, float b) => (along * a + across * c + back * b).normalized;
                Aim(joints, closedRotations, Palm(0.55f, 0.35f, -0.75f), Palm(0.85f, -0.1f, -0.5f), Palm(0.9f, -0.4f, -0.1f));
                Aim(joints, fistRotations, Palm(0.5f, 0.2f, -0.85f), Palm(0.55f, -0.7f, -0.45f), Palm(0.15f, -0.97f, -0.2f));
                Aim(joints, spread, Palm(0.35f, 0.9f, 0.15f), Palm(0.45f, 0.88f, 0.1f), Palm(0.5f, 0.85f, 0.1f));
            }
            Write(chain.FindPropertyRelative("Open"), open);
            Write(chain.FindPropertyRelative("Closed"), closedRotations);
            Write(chain.FindPropertyRelative("Fist"), fistRotations);
            Write(chain.FindPropertyRelative("Spread"), spread);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // Local rotations (joints 1..3) that point each bone down its direction, base first.
    static void Aim(List<Transform> joints, Quaternion[] result, params Vector3[] directions)
    {
        var rest = joints.Select(j => j.localRotation).ToArray();
        for (int j = 1; j <= 3; j++)
        {
            Transform joint = joints[j];
            Vector3 current = j < 3
                ? (joints[j + 1].position - joint.position).normalized
                : joint.TransformDirection(joint.localPosition.normalized);
            joint.rotation = Quaternion.FromToRotation(current, directions[j - 1]) * joint.rotation;
            result[j] = joint.localRotation;
        }
        for (int j = 0; j < joints.Count; j++)
            joints[j].localRotation = rest[j];
    }

    // The joint's local rotation after a world-axis turn from its rest (or `from`) pose.
    static Quaternion Turned(Transform joint, Vector3 worldAxis, float degrees, Quaternion? from = null)
    {
        Quaternion rest = joint.localRotation;
        if (from.HasValue)
            joint.localRotation = from.Value;
        Quaternion world = Quaternion.AngleAxis(degrees, worldAxis) * joint.rotation;
        Quaternion local = Quaternion.Inverse(joint.parent.rotation) * world;
        joint.localRotation = rest;
        return local;
    }

    static void Write(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static void Write(SerializedProperty array, Quaternion[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).quaternionValue = values[i];
    }
}
