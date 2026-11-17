// Builds a gun's two jam-clearing clips from its own reload clips, for `unity command script run` (edit mode only):
//   <Gun>_Repairing         "Reseat": the support hand goes to the magazine, tugs it down and slaps it home, twice
//   <Gun>_RepairingStubborn "Rack":   the hand goes to the bolt (or slide) and tugs it: two short stuck pulls, one full
// Where the hand holds the magazine and the bolt, its finger shapes, how the gun is presented and which way the parts
// move are read off <Gun>_Reloading and <Gun>_ReloadingEmpty (sampled through GunAnimation, so hand-keyed and
// retargeted reloads both work). Re-run after re-baking a gun's reloads. The third motion, the shake, is procedural
// (GunShake) and needs no clip.
//   unity command script run --file AgentScripts/Hands/RepairClips.cs --entry RepairClips.Bake --args '["AK47"]'
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RepairClips
{
    const float KeyRate = 60f;
    const float ReseatSeconds = 4f, RackSeconds = 5f;
    // How far the magazine is tugged out of its well (m) and how much of the bolt's travel each rack pulls.
    const float Tug = 0.022f;
    // Bolt travel (m) for a gun whose empty reload never racks it.
    const float DefaultRack = 0.07f;
    static readonly float[] Pulls = { 0.45f, 0.6f, 1f };
    // The hand swings out by this much (gun space, left hand) on its way between holds.
    static readonly Vector3 Bulge = new(-0.05f, -0.05f, 0f);
    static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

    struct Pose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public static Pose Lerp(Pose a, Pose b, float t) =>
            new() { Position = Vector3.Lerp(a.Position, b.Position, t), Rotation = Quaternion.Slerp(a.Rotation, b.Rotation, t) };
    }

    // What one reload shows: the gun presented, the hand on a part, the part's move.
    sealed class Hold
    {
        public Pose Motion, HandInPart;
        public FingerShape[] Shape;
        public Vector3 Travel;
        public bool Found;
    }

    public static string Bake(string gun)
    {
        if (Application.isPlaying)
            return "leave Play mode first";
        string folder = $"Assets/_Game/Content/Weapons/{gun}";
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{gun}.prefab");
        var reload = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folder}/Animations/{gun}_Reloading.anim");
        if (prefab == null || reload == null)
            return "no prefab or reload clip for " + gun;
        var report = new StringBuilder(gun + ": ");

        Scene scene = EditorSceneManager.NewPreviewScene();
        GunAnimation animation = null;
        try
        {
            GameObject item = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(item, scene);
            animation = item.GetComponentInChildren<GunAnimation>(true);
            Transform root = animation.transform, motion = root.Find("Motion"), weapon = motion.Find("Weapon"), controls = motion.Find("Controls");
            Transform target = motion.Find("HandTargets/Left"), magazine = weapon.Find("Magazine");
            var grip = target.GetComponent<HandGrip>();
            WeightedTransformArray contacts = root.Find("Rig/LeftHand").GetComponent<MultiParentConstraint>().data.sourceObjects;
            Transform bolt = contacts.Count > 3 && contacts[3].transform != null && contacts[3].transform.parent != weapon &&
                             contacts[3].transform.IsChildOf(weapon) ? contacts[3].transform.parent : weapon.Find("Bolt") ?? weapon.Find("Slide");
            animation.Show();

            Pose InControls(Transform t) => new() { Position = controls.InverseTransformPoint(t.position), Rotation = Quaternion.Inverse(controls.rotation) * t.rotation };
            Pose Local(Transform t) => new() { Position = t.localPosition, Rotation = t.localRotation };
            FingerShape[] Shape() => Enumerable.Range(0, 5).Select(f => grip.Shape[f]).ToArray();

            animation.StartAction(GunAction.Reloading, false);
            animation.Tick(1f / 60f, 0f);
            Pose restHand = InControls(target), restMotion = Local(motion);
            FingerShape[] restShape = Shape();

            // The first moment a part leaves its seat in a reload: the hand is on it, the gun is turned to show it.
            Hold Find(bool empty, Transform part)
            {
                var hold = new Hold();
                if (part == null)
                    return hold;
                animation.StartAction(GunAction.Reloading, empty);
                animation.Tick(1f / 60f, 0f);
                Vector3 seat = part.localPosition;
                const int steps = 240;
                for (int k = 1; k <= steps; k++)
                {
                    animation.Tick(1f / 60f, k / (float)steps);
                    if ((part.localPosition - seat).magnitude < 0.004f)
                        continue;
                    // Step back to just before it moved for the hold, then on for the farthest point.
                    animation.Tick(1f / 60f, (k - 1) / (float)steps);
                    Pose partPose = InControls(part), hand = InControls(target);
                    hold.HandInPart = new Pose
                    {
                        Position = Quaternion.Inverse(partPose.Rotation) * (hand.Position - partPose.Position),
                        Rotation = Quaternion.Inverse(partPose.Rotation) * hand.Rotation,
                    };
                    hold.Motion = Local(motion);
                    hold.Shape = Shape();
                    for (int j = k; j <= steps; j++)
                    {
                        animation.Tick(1f / 60f, j / (float)steps);
                        if (part.TryGetComponent(out Renderer renderer) && !renderer.enabled)
                            break;
                        Vector3 moved = part.localPosition - seat;
                        if (moved.magnitude > hold.Travel.magnitude)
                            hold.Travel = moved;
                    }
                    hold.Found = true;
                    return hold;
                }
                return hold;
            }

            Hold onMagazine = Find(false, magazine), onBolt = Find(true, bolt);

            // The magazine is best in view where the reload pushes the new one home: present the gun as it is then.
            Transform replacement = weapon.Find("ReplacementMagazine");
            if (onMagazine.Found && replacement != null && replacement.TryGetComponent(out Renderer spare))
            {
                animation.StartAction(GunAction.Reloading, false);
                const int steps = 240;
                animation.Tick(1f / 60f, 1f);
                Vector3 home = replacement.localPosition;
                bool away = false;
                for (int k = 0; k <= steps; k++)
                {
                    animation.Tick(1f / 60f, k / (float)steps);
                    float fromHome = (replacement.localPosition - home).magnitude;
                    away |= spare.enabled && fromHome > 0.05f;
                    if (!away || !spare.enabled || fromHome > 0.02f)
                        continue;
                    onMagazine.Motion = Local(motion);
                    // The hand is on the magazine for certain here (a magazine that drops free leaves its well alone).
                    Pose spare2 = InControls(replacement), hand = InControls(target);
                    onMagazine.HandInPart = new Pose
                    {
                        Position = Quaternion.Inverse(spare2.Rotation) * (hand.Position - spare2.Position),
                        Rotation = Quaternion.Inverse(spare2.Rotation) * hand.Rotation,
                    };
                    onMagazine.Shape = Shape();
                    report.Append($"presented and held as at {k / (float)steps:0.00} of the reload; ");
                    break;
                }
            }

            // The reload's hand may only pass near the bolt; the gun's own bolt contact (constraint source 3) is on it.
            Transform boltContact = contacts.Count > 3 ? contacts[3].transform : null;
            if (boltContact != null && bolt != null && boltContact != bolt && boltContact.IsChildOf(bolt))
            {
                if (!onBolt.Found)
                {
                    // A reload that never racks (a bolt release): pull the bolt straight back, the gun shown as for the magazine.
                    onBolt.Found = true;
                    onBolt.Travel = new Vector3(0f, 0f, -DefaultRack);
                    onBolt.Motion = onMagazine.Found ? onMagazine.Motion : restMotion;
                    report.Append("no rack in the empty reload: straight pull; ");
                }
                animation.StartAction(GunAction.Reloading, true);
                animation.Tick(1f / 60f, 0f);
                Pose partPose = InControls(bolt), contact = InControls(boltContact);
                onBolt.HandInPart = new Pose
                {
                    Position = Quaternion.Inverse(partPose.Rotation) * (contact.Position - partPose.Position),
                    Rotation = Quaternion.Inverse(partPose.Rotation) * contact.Rotation,
                };
                onBolt.Shape = Enumerable.Range(0, 5).Select(f => new FingerShape { Curl = f == 0 ? 0.6f : 0.95f }).ToArray();
                report.Append("bolt hold from its contact; ");
            }
            animation.Stop();
            animation = null;

            // Every binding the reload keys, at rest: the generated clips key the same set so nothing is left over.
            var rest = new Dictionary<EditorCurveBinding, float>();
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(reload))
                rest[binding] = AnimationUtility.GetEditorCurve(reload, binding).Evaluate(0f);
            string rootPath(Transform t) => AnimationUtility.CalculateTransformPath(t, root);

            if (onMagazine.Found)
            {
                // Down the well's own direction, never farther than the reload pulls it.
                Vector3 tug = onMagazine.Travel.normalized * Mathf.Min(Tug, onMagazine.Travel.magnitude);
                var beats = new[] { (0.34f, 0.47f, 0.5f, 1f, false), (0.6f, 0.72f, 0.75f, 1f, true) };
                report.Append(Write($"{folder}/Animations/{gun}_Repairing.anim", ReseatSeconds, rest, rootPath, controls, weapon, motion, magazine, target,
                    restHand, restMotion, restShape, onMagazine, tug, beats, 0.12f, 0.3f, 0.82f, 0.96f));
            }
            else
                report.Append("no magazine moves in the reload: Reseat skipped; ");
            if (onBolt.Found)
            {
                var beats = new[] { (0.3f, 0.38f, 0.41f, Pulls[0], false), (0.47f, 0.55f, 0.58f, Pulls[1], false), (0.64f, 0.74f, 0.79f, Pulls[2], true) };
                report.Append(Write($"{folder}/Animations/{gun}_RepairingStubborn.anim", RackSeconds, rest, rootPath, controls, weapon, motion, bolt, target,
                    restHand, restMotion, restShape, onBolt, onBolt.Travel, beats, 0.1f, 0.27f, 0.84f, 0.97f));
            }
            else
                report.Append("no bolt moves in the empty reload: Rack skipped; ");
        }
        finally
        {
            if (animation != null)
                animation.Stop();
            EditorSceneManager.ClosePreviewScene(scene);
        }
        return report.ToString();
    }

    // One clip: present the gun, bring the hand to the part, pull it per beat (start, pulled, home, share, hard) and
    // let it snap home (an impact), then take the hand back.
    static string Write(string path, float seconds, Dictionary<EditorCurveBinding, float> rest, Func<Transform, string> rootPath, Transform controls,
        Transform weapon, Transform motion, Transform part, Transform target, Pose restHand, Pose restMotion, FingerShape[] restShape, Hold hold,
        Vector3 travel, (float start, float pulled, float home, float share, bool hard)[] beats, float reachStart, float reachEnd, float backStart, float backEnd)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return "no clip at " + path + "; ";
        int count = Mathf.RoundToInt(seconds * KeyRate) + 1;
        Vector3 seat = part.localPosition;
        Quaternion partRotation = Quaternion.Inverse(controls.rotation) * part.rotation;
        Vector3 Seat(Vector3 local) => controls.InverseTransformPoint(part.parent.TransformPoint(local));

        var curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
        foreach (KeyValuePair<EditorCurveBinding, float> pair in rest)
            curves[pair.Key] = new AnimationCurve(new Keyframe(0f, pair.Value), new Keyframe(seconds, pair.Value));
        var keyed = new Dictionary<string, float[]>();
        float[] Track(string name) => keyed.TryGetValue(name, out float[] track) ? track : keyed[name] = new float[count];

        Quaternion lastHand = restHand.Rotation, lastMotion = restMotion.Rotation;
        for (int k = 0; k < count; k++)
        {
            float u = k / (float)(count - 1);
            // 0 at the forend, 1 on the part.
            float on = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reachStart, reachEnd, u)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(backStart, backEnd, u)));
            // The gun turns to show the part a little ahead of the hand, and settles after it.
            float shown = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, reachEnd - 0.04f, u)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(backStart + 0.04f, 1f, u)));
            float pull = 0f, impacts = 0f, punch = 0f, jolt = 0f;
            foreach ((float start, float pulled, float home, float share, bool hard) in beats)
            {
                if (u >= start && u < pulled)
                    pull = share * Mathf.SmoothStep(0f, 1f, (u - start) / (pulled - start));
                else if (u >= pulled && u < home)
                    pull = share * (1f - Mathf.Pow((u - pulled) / (home - pulled), 2f));
                if (u >= home)
                {
                    impacts += 1f;
                    punch = hard ? 1f : 0f;
                    // A knock that dies away over the next tenth of the clip.
                    jolt = Mathf.Max(jolt, (hard ? 1f : 0.6f) * Mathf.Clamp01(1f - (u - home) / 0.1f));
                }
            }

            Vector3 partLocal = seat + travel * pull;
            Pose partNow = new() { Position = Seat(partLocal), Rotation = partRotation };
            Pose onPart = new() { Position = partNow.Position + partNow.Rotation * hold.HandInPart.Position, Rotation = partNow.Rotation * hold.HandInPart.Rotation };
            Pose hand = Pose.Lerp(restHand, onPart, on);
            hand.Position += Bulge * Mathf.Sin(Mathf.PI * on);
            Pose gun = Pose.Lerp(restMotion, hold.Motion, shown);
            gun.Position += new Vector3(0f, 0.012f, -0.008f) * jolt;
            gun.Rotation *= Quaternion.Euler(-2.5f * jolt, 0f, 1.5f * jolt);

            lastHand = Same(lastHand, hand.Rotation);
            lastMotion = Same(lastMotion, gun.Rotation);
            Set("Controls/LeftFree", hand.Position, lastHand);
            Set("", gun.Position, lastMotion);
            Track("part.x")[k] = partLocal.x;
            Track("part.y")[k] = partLocal.y;
            Track("part.z")[k] = partLocal.z;
            for (int f = 0; f < 5; f++)
            {
                Track($"{Fingers[f]}.Curl")[k] = Mathf.Lerp(restShape[f].Curl, hold.Shape[f].Curl, on);
                Track($"{Fingers[f]}.Spread")[k] = Mathf.Lerp(restShape[f].Spread, hold.Shape[f].Spread, on);
                Track($"{Fingers[f]}.Tip")[k] = Mathf.Lerp(restShape[f].Tip, hold.Shape[f].Tip, on);
            }
            Track("impacts")[k] = impacts;
            Track("punch")[k] = punch;

            void Set(string under, Vector3 position, Quaternion rotation)
            {
                Track(under + "/p.x")[k] = position.x;
                Track(under + "/p.y")[k] = position.y;
                Track(under + "/p.z")[k] = position.z;
                Track(under + "/r.x")[k] = rotation.x;
                Track(under + "/r.y")[k] = rotation.y;
                Track(under + "/r.z")[k] = rotation.z;
                Track(under + "/r.w")[k] = rotation.w;
            }
        }

        AnimationCurve Curve(string name, bool stepped = false)
        {
            float[] values = keyed[name];
            var keys = new Keyframe[count];
            for (int k = 0; k < count; k++)
                keys[k] = new Keyframe(k / KeyRate, values[k]);
            var curve = new AnimationCurve(keys);
            for (int k = 0; k < count; k++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, k, stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, k, stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        string motionPath = rootPath(motion), freePath = motionPath + "/Controls/LeftFree", partPath = rootPath(part), targetPath = rootPath(target);
        void Transforms(string at, string under)
        {
            foreach ((string property, string track) in new[]
                     {
                         ("m_LocalPosition.x", "/p.x"), ("m_LocalPosition.y", "/p.y"), ("m_LocalPosition.z", "/p.z"),
                         ("m_LocalRotation.x", "/r.x"), ("m_LocalRotation.y", "/r.y"), ("m_LocalRotation.z", "/r.z"), ("m_LocalRotation.w", "/r.w"),
                     })
                curves[EditorCurveBinding.FloatCurve(at, typeof(Transform), property)] = Curve(under + track);
        }
        Transforms(freePath, "Controls/LeftFree");
        Transforms(motionPath, "");
        foreach (string axis in new[] { "x", "y", "z" })
            curves[EditorCurveBinding.FloatCurve(partPath, typeof(Transform), "m_LocalPosition." + axis)] = Curve("part." + axis);
        foreach (string finger in Fingers)
            foreach (string value in new[] { "Curl", "Spread", "Tip" })
                curves[EditorCurveBinding.FloatCurve(targetPath, typeof(HandGrip), $"_animatedShape.{finger}.{value}")] = Curve($"{finger}.{value}");
        // The hand rides LeftFree all the way, as in the retargeted reloads.
        for (int i = 0; i < 5; i++)
        {
            float weight = i == 4 ? 1f : 0f;
            curves[EditorCurveBinding.FloatCurve("Rig/LeftHand", typeof(MultiParentConstraint), $"m_Data.m_SourceObjects.m_Item{i}.weight")] =
                new AnimationCurve(new Keyframe(0f, weight), new Keyframe(seconds, weight));
        }
        curves[EditorCurveBinding.FloatCurve("", typeof(GunAnimation), "_repairImpactCount")] = Curve("impacts", true);
        curves[EditorCurveBinding.FloatCurve("", typeof(GunAnimation), "_repairPunch")] = Curve("punch", true);

        clip.ClearCurves();
        clip.frameRate = KeyRate;
        foreach (KeyValuePair<EditorCurveBinding, AnimationCurve> pair in curves)
            AnimationUtility.SetEditorCurve(clip, pair.Key, pair.Value);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);
        return $"{clip.name} {seconds:0.#} s, {beats.Length} impacts, part moves {travel.magnitude * 1000f:0} mm; ";
    }

    // The same rotation with the sign that keeps keyed components continuous.
    static Quaternion Same(Quaternion last, Quaternion next) =>
        Quaternion.Dot(last, next) < 0f ? new Quaternion(-next.x, -next.y, -next.z, -next.w) : next;
}
