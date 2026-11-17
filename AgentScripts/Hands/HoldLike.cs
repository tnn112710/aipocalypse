// Gives a gun the support-hand hold of another gun, for `unity command script run` (edit mode only). For guns with no
// pack reload to retarget (hand-keyed shotguns, block-outs): the forend hold keeps its place on this gun, takes the
// other gun's hand rotation and finger shape, then is pressed onto this gun's own forend and closed until the fingers
// touch (HandFit). In its clips, every key that sat on the old hold moves to the new one.
//   unity command script run --file AgentScripts/Hands/HoldLike.cs --entry HoldLike.Apply --args '["PumpShotgun","AK47"]'
using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class HoldLike
{
    const string HandSetPath = "Assets/_Game/Data/Hands/HandSet.asset";
    const string Weapons = "Assets/_Game/Content/Weapons/";
    const string Free = "Motion/Controls/LeftFree";

    public static string Apply(string gun, string like)
    {
        if (Application.isPlaying)
            return "leave Play mode first";
        string prefabPath = $"{Weapons}{gun}/{gun}.prefab";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var other = AssetDatabase.LoadAssetAtPath<GameObject>($"{Weapons}{like}/{like}.prefab");
        if (prefab == null || other == null)
            return "no prefab for " + gun + " or " + like;

        HandGrip otherGrip = Left(other.transform);
        Quaternion hold = Quaternion.Inverse(Weapon(otherGrip).rotation) * otherGrip.transform.rotation;
        var fingers = Enumerable.Range(0, 5).Select(f => otherGrip.Shape[f]).ToArray();

        Scene scene = EditorSceneManager.NewPreviewScene();
        Vector3 position;
        float palm;
        try
        {
            GameObject item = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(item, scene);
            HandGrip grip = Left(item.transform);
            Transform weapon = Weapon(grip);
            HandRig rig = Object.Instantiate(AssetDatabase.LoadAssetAtPath<HandSet>(HandSetPath).Get(HandSide.Left));
            SceneManager.MoveGameObjectToScene(rig.gameObject, scene);
            rig.transform.localScale *= item.GetComponent<ItemHands>().HandScale;
            grip.transform.rotation = weapon.rotation * hold;
            palm = Fit(grip, rig, fingers);
            position = weapon.InverseTransformPoint(grip.transform.position + grip.transform.rotation * Vector3.up * palm);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        var shape = new HandShape { Thumb = fingers[0], Index = fingers[1], Middle = fingers[2], Ring = fingers[3], Little = fingers[4] };
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        int rebased;
        try
        {
            HandGrip grip = Left(contents.transform);
            Transform weapon = Weapon(grip), motion = weapon.parent;
            var moved = new List<Transform> { grip.transform };
            Transform free = motion.Find("Controls/LeftFree");
            if (free != null)
                moved.Add(free);
            // The forend contact the hand rests on (constraint source 0), where the gun has a rig.
            Transform hand = motion.parent.Find("Rig/LeftHand");
            if (hand != null && hand.TryGetComponent(out MultiParentConstraint constraint) && constraint.data.sourceObjects.Count > 0)
                moved.Add(constraint.data.sourceObjects[0].transform);
            foreach (Transform t in moved)
                t.SetPositionAndRotation(weapon.TransformPoint(position), weapon.rotation * hold);
            grip.SetAnimatedShape(shape);
            EditorUtility.SetDirty(grip);
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            rebased = Rebase(gun, contents.GetComponentInChildren<Animator>(true));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
        return $"{gun}: hold like {like}, palm moved {palm * 1000f:0} mm, curls {string.Join(" ", fingers.Select(f => f.Curl.ToString("0.00")))}; rebased {rebased} clips";
    }

    static HandGrip Left(Transform root) => root.GetComponentsInChildren<HandGrip>(true).First(grip => grip.Side == HandSide.Left);

    // HandTargets and Weapon are siblings under Motion.
    static Transform Weapon(HandGrip grip) => grip.transform.parent.parent.Find("Weapon");

    // Keys that hold the left hand's control or finger shape at the old rest go to the prefab's new rest values. A clip
    // that moves the hand starts on the old hold, so every key still on that pose moves with it and the hand-offs
    // between the forend contact and the free control stay seamless.
    static int Rebase(string gun, Animator animator)
    {
        if (animator == null || !AssetDatabase.IsValidFolder($"{Weapons}{gun}/Animations"))
            return 0;
        Transform rest = animator.transform.Find(Free);
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { $"{Weapons}{gun}/Animations" }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
            var edited = new Dictionary<EditorCurveBinding, AnimationCurve>();

            EditorCurveBinding[] Group(params string[] properties) =>
                properties.Select(property => bindings.FirstOrDefault(b => b.path == Free && b.propertyName == property)).ToArray();
            bool Whole(EditorCurveBinding[] group) => group.All(b => b.propertyName != null);
            EditorCurveBinding[] place = Group("m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z");
            EditorCurveBinding[] turn = Group("m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w");
            EditorCurveBinding[] angles = Group("localEulerAnglesRaw.x", "localEulerAnglesRaw.y", "localEulerAnglesRaw.z");
            if (rest != null && Whole(place) && (Whole(turn) || Whole(angles)))
            {
                bool euler = !Whole(turn);
                EditorCurveBinding[] pose = place.Concat(euler ? angles : turn).ToArray();
                AnimationCurve[] curves = pose.Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
                Vector3 Position(float t) => new(curves[0].Evaluate(t), curves[1].Evaluate(t), curves[2].Evaluate(t));
                Quaternion Rotation(float t) => euler
                    ? Quaternion.Euler(curves[3].Evaluate(t), curves[4].Evaluate(t), curves[5].Evaluate(t))
                    : new Quaternion(curves[3].Evaluate(t), curves[4].Evaluate(t), curves[5].Evaluate(t), curves[6].Evaluate(t)).normalized;
                Vector3 oldPosition = Position(0f);
                Quaternion oldRotation = Rotation(0f);
                float[] times = curves.SelectMany(c => c.keys.Select(key => key.time)).Distinct()
                    .Where(t => Vector3.Distance(Position(t), oldPosition) < 0.0005f && Quaternion.Angle(Rotation(t), oldRotation) < 0.5f).ToArray();

                var values = new List<float> { rest.localPosition.x, rest.localPosition.y, rest.localPosition.z };
                if (euler)
                {
                    // The same turn written nearest to the angles keyed before, so the eases to the next keys don't spin.
                    Vector3 now = rest.localEulerAngles;
                    for (int i = 0; i < 3; i++)
                        values.Add(curves[3 + i].Evaluate(0f) + Mathf.DeltaAngle(curves[3 + i].Evaluate(0f), now[i]));
                }
                else
                {
                    Quaternion now = rest.localRotation;
                    if (Quaternion.Dot(now, oldRotation) < 0f)
                        now = new Quaternion(-now.x, -now.y, -now.z, -now.w);
                    values.AddRange(new[] { now.x, now.y, now.z, now.w });
                }
                for (int i = 0; i < pose.Length; i++)
                    if (Move(curves[i], times, values[i]))
                        edited[pose[i]] = curves[i];
            }

            foreach (EditorCurveBinding binding in bindings)
            {
                if (binding.path != "Motion/HandTargets/Left" || !AnimationUtility.GetFloatValue(animator.gameObject, binding, out float value))
                    continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                float first = curve.keys[0].value;
                if (Move(curve, curve.keys.Where(key => Mathf.Abs(key.value - first) < 1e-4f).Select(key => key.time).ToArray(), value))
                    edited[binding] = curve;
            }
            if (edited.Count == 0)
                continue;
            // In one call: set one at a time, a rotation's components are re-matched against the half-edited rest.
            AnimationUtility.SetEditorCurves(clip, edited.Keys.ToArray(), edited.Values.ToArray());
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            changed++;
        }
        return changed;
    }

    static bool Move(AnimationCurve curve, float[] times, float value)
    {
        Keyframe[] keys = curve.keys;
        bool moved = false;
        for (int k = 0; k < keys.Length; k++)
        {
            float time = keys[k].time;
            if (Mathf.Approximately(keys[k].value, value) || !times.Any(t => Mathf.Abs(t - time) < 1e-4f))
                continue;
            keys[k].value = value;
            moved = true;
        }
        if (moved)
            curve.keys = keys;
        return moved;
    }

    // The project's own grip fit (HandFit, editor-only and internal): returns the palm's move along the back of the hand
    // and writes the fitted fingers into `fingers`.
    static float Fit(HandGrip grip, HandRig rig, FingerShape[] fingers)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        Type fit = typeof(HandGrip).Assembly.GetType("MB13.Aipocalypse.Core.HandSystem.HandFit", true);
        object meshes = fit.GetMethod("Collect", flags).Invoke(null, new object[] { grip });
        var samples = new List<Vector3>();
        var pose = new HandShape { Thumb = fingers[0], Index = fingers[1], Middle = fingers[2], Ring = fingers[3], Little = fingers[4] };
        Vector3 elbow = grip.transform.position + Vector3.down;
        float palm = (float)fit.GetMethod("FitPalm", flags).Invoke(null, new object[] { grip, rig, elbow, pose, meshes, samples });
        grip.Resolve(rig, elbow, palm, out Vector3 position, out Quaternion rotation);
        rig.Place(position, rotation);
        rig.Apply(pose);
        for (int f = 0; f < fingers.Length; f++)
            fingers[f] = (FingerShape)fit.GetMethod("FitFinger", flags).Invoke(null, new object[] { rig, f, fingers[f], meshes, samples });
        return palm;
    }
}
