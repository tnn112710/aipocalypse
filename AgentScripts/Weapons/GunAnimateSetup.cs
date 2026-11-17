// Turns a block-out gun (GunBlockoutSetup: a Gun_Base variant that only poses and kicks) into an animated one, by
// grafting an already animated gun's layout onto it, for `unity command script run` (edit mode only):
//   unity command script run --file AgentScripts/Weapons/GunAnimateSetup.cs --entry GunAnimateSetup.Animate --args '["M4","AK47"]'
// `gun` gets, from `like`: the part names the clips key (Magazine, Bolt or Slide, Handguard, Grip, ReplacementMagazine),
// the hand contacts, Controls/LeftFree, the Rig with its constraints, Animator + RigBuilder + GunAnimation, copies of
// its clips (moved to this gun's part positions) under an override controller, a dropped-magazine prefab and a repair
// profile. Then bake its own motion: ReloadRetarget.Bake (needs a Table entry) and RepairClips.Bake.
// Safe to re-run: it rebuilds the grafted parts and re-copies nothing that exists. GunBlockoutSetup leaves an animated
// gun alone afterwards.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

public static class GunAnimateSetup
{
    const string Weapons = "Assets/_Game/Content/Weapons/";
    const string Root = "PosePivot/RecoilPivot/AnimationRoot";

    // The names the clips key, and the Castle Bravo part suffixes that can stand in for each.
    static readonly Dictionary<string, string[]> Parts = new()
    {
        ["Magazine"] = new[] { "_Mag" },
        ["Bolt"] = new[] { "_Bolt", "_Slide" },
        ["Slide"] = new[] { "_Slide", "_Bolt" },
        ["Handguard"] = new[] { "_BarrelGuard", "_Handguard", "_Barrel" },
        ["Receiver"] = new[] { "_Receiver", "_Receiver_Down", "_Frame", "_Body" },
        ["Grip"] = new[] { "_Grip", "_Receiver", "_Receiver_Down", "_Frame", "_Body" },
    };

    public static string Animate(string gun, string like)
    {
        if (Application.isPlaying)
            return "leave Play mode first";
        string folder = Weapons + gun, likeFolder = Weapons + like;
        string path = $"{folder}/{gun}.prefab", likePath = $"{likeFolder}/{like}.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null || AssetDatabase.LoadAssetAtPath<GameObject>(likePath) == null)
            return "missing prefab";
        var report = new StringBuilder(gun + " like " + like + ": ");

        // Clips, controller, dropped magazine and repair profile: copies with this gun's name.
        Directory.CreateDirectory($"{folder}/Animations");
        AssetDatabase.Refresh();
        int copied = 0;
        foreach (string clipPath in Directory.GetFiles($"{likeFolder}/Animations", $"{like}_*.anim").Select(p => p.Replace('\\', '/')))
            copied += Copy(clipPath, $"{folder}/Animations/{gun}_{Path.GetFileName(clipPath).Substring(like.Length + 1)}") ? 1 : 0;
        string controllerPath = $"{folder}/Animations/{gun}.overrideController";
        Copy($"{likeFolder}/Animations/{like}.overrideController", controllerPath);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip from = overrides[i].Value;
            if (from == null)
                continue;
            string name = from.name.StartsWith(like + "_") ? gun + from.name.Substring(like.Length) : from.name;
            var own = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{folder}/Animations/{name}.anim");
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, own != null ? own : from);
        }
        controller.ApplyOverrides(overrides);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssetIfDirty(controller);
        string magazinePath = $"{folder}/{gun}_Magazine.prefab", repairPath = $"{folder}/{gun}_Repair.asset";
        bool hasMagazine = AssetDatabase.LoadAssetAtPath<GameObject>($"{likeFolder}/{like}_Magazine.prefab") != null;
        if (hasMagazine)
            Copy($"{likeFolder}/{like}_Magazine.prefab", magazinePath);
        Copy($"{likeFolder}/{like}_Repair.asset", repairPath);
        report.Append($"{copied} clips copied; ");

        var moved = new Dictionary<string, Vector3>();
        GameObject source = PrefabUtility.LoadPrefabContents(likePath);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Transform fromRoot = source.transform.Find(Root), toRoot = root.transform.Find(Root);
            Transform fromWeapon = fromRoot.Find("Motion/Weapon"), weapon = toRoot.Find("Motion/Weapon"), motion = toRoot.Find("Motion");
            Transform leftTarget = motion.Find("HandTargets/Left"), rightTarget = motion.Find("HandTargets/Right");

            // What the clips and contacts of `like` hang on: every part a constraint source sits under, plus the magazine.
            var contacts = new List<Transform>();
            foreach (MultiParentConstraint constraint in fromRoot.GetComponentsInChildren<MultiParentConstraint>(true))
            {
                WeightedTransformArray sources = constraint.data.sourceObjects;
                for (int i = 0; i < sources.Count; i++)
                    if (sources[i].transform != null && sources[i].transform.IsChildOf(fromWeapon))
                        contacts.Add(sources[i].transform);
            }
            foreach (string part in contacts.Select(c => c.parent.name).Append("Magazine").Distinct().ToList())
            {
                if (part == "ReplacementMagazine" || weapon.Find(part) != null || !Parts.TryGetValue(part, out string[] suffixes))
                    continue;
                Transform found = suffixes.Select(s => weapon.Cast<Transform>().FirstOrDefault(c => c.name.EndsWith(s))).FirstOrDefault(c => c != null);
                if (found == null)
                {
                    found = new GameObject(part).transform;
                    found.SetParent(weapon, false);
                    found.localPosition = fromWeapon.Find(part).localPosition;
                    report.Append($"no part for {part}: empty node; ");
                }
                found.name = part;
            }

            Transform magazine = weapon.Find("Magazine");
            Transform replacement = weapon.Find("ReplacementMagazine");
            if (replacement != null)
                Object.DestroyImmediate(replacement.gameObject);
            if (fromWeapon.Find("ReplacementMagazine") != null)
            {
                replacement = Object.Instantiate(magazine.gameObject, weapon).transform;
                replacement.name = "ReplacementMagazine";
                replacement.SetSiblingIndex(magazine.GetSiblingIndex() + 1);
                foreach (Transform child in replacement.Cast<Transform>().ToList())
                    Object.DestroyImmediate(child.gameObject);
                if (replacement.TryGetComponent(out Renderer spare))
                    spare.enabled = false;
            }

            // Contacts: the forend and trigger ones where the block-out's hands already are, the rest placed on this
            // gun's parts as they sit on `like`'s (offset from each part's middle).
            foreach (Transform contact in contacts)
            {
                Transform part = weapon.Find(contact.parent.name);
                Transform old = part.Find(contact.name);
                if (old != null)
                    Object.DestroyImmediate(old.gameObject);
                Transform made = new GameObject(contact.name).transform;
                made.SetParent(part, false);
                made.gameObject.layer = part.gameObject.layer;
                bool forend = contact == contacts[0], trigger = contact.name.StartsWith("Right");
                if (forend || trigger)
                    made.SetPositionAndRotation((forend ? leftTarget : rightTarget).position, (forend ? leftTarget : rightTarget).rotation);
                else
                {
                    Vector3 offset = fromWeapon.InverseTransformPoint(contact.position) - Middle(contact.parent, fromWeapon);
                    made.SetPositionAndRotation(weapon.TransformPoint(Middle(part, weapon) + offset),
                        weapon.rotation * (Quaternion.Inverse(fromWeapon.rotation) * contact.rotation));
                }
            }

            Transform controls = motion.Find("Controls");
            if (controls != null)
                Object.DestroyImmediate(controls.gameObject);
            controls = new GameObject("Controls").transform;
            controls.SetParent(motion, false);
            controls.SetSiblingIndex(weapon.GetSiblingIndex() + 1);
            Transform free = new GameObject("LeftFree").transform;
            free.SetParent(controls, false);
            free.SetPositionAndRotation(leftTarget.position, leftTarget.rotation);

            Transform rig = toRoot.Find("Rig");
            if (rig != null)
                Object.DestroyImmediate(rig.gameObject);
            rig = Object.Instantiate(fromRoot.Find("Rig").gameObject, toRoot).transform;
            rig.name = "Rig";

            // A re-run: the old ones go dependants first (GunAnimation and RigBuilder require the Animator).
            foreach (Type type in new[] { typeof(GunAnimation), typeof(RigBuilder), typeof(Animator) })
            {
                Component old = toRoot.GetComponent(type);
                if (old != null)
                    Object.DestroyImmediate(old);
            }
            foreach (Type type in new[] { typeof(Animator), typeof(RigBuilder), typeof(GunAnimation) })
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(fromRoot.GetComponent(type));
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(toRoot.gameObject);
            }
            int lost = 0;
            foreach (Component component in toRoot.GetComponents<Component>().Concat(rig.GetComponentsInChildren<Component>(true)))
                lost += Remap(component, source.transform, root.transform);
            if (lost > 0)
                report.Append($"{lost} references had no twin; ");
            // Set outright: on a re-run the copied layer can come out pointing at nothing, and the hands then never move.
            var builder = toRoot.GetComponent<RigBuilder>();
            builder.layers.Clear();
            builder.layers.Add(new RigLayer(rig.GetComponent<Rig>(), true));

            toRoot.GetComponent<Animator>().runtimeAnimatorController = controller;
            var animation = new SerializedObject(toRoot.GetComponent<GunAnimation>());
            animation.FindProperty("_droppedMagazine").objectReferenceValue =
                hasMagazine ? AssetDatabase.LoadAssetAtPath<GameObject>(magazinePath).GetComponent<MB13.Aipocalypse.Core.Effects.PooledEffect>() : null;
            animation.ApplyModifiedPropertiesWithoutUndo();
            var held = new SerializedObject(root.GetComponent<HeldGun>());
            held.FindProperty("_animation").objectReferenceValue = toRoot.GetComponent<GunAnimation>();
            held.ApplyModifiedPropertiesWithoutUndo();

            // The clips key the finger shapes.
            foreach (Transform target in new[] { leftTarget, rightTarget })
            {
                var grip = target.GetComponent<HandGrip>();
                grip.SetAnimatedShape(grip.Shape);
                var so = new SerializedObject(grip);
                so.FindProperty("_animateShape").boolValue = true;
                so.FindProperty("_aim").enumValueIndex = (int)GripAim.AsAuthored;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // How far each keyed part sits from where `like` has it: the copied clips move by that.
            foreach (Transform part in fromWeapon)
            {
                Transform twin = weapon.Find(part.name);
                if (twin != null)
                    moved["Motion/Weapon/" + part.name] = twin.localPosition - part.localPosition;
            }

            if (hasMagazine)
            {
                // The dropped magazine shows this gun's magazine mesh.
                GameObject dropped = PrefabUtility.LoadPrefabContents(magazinePath);
                MeshFilter shown = dropped.GetComponentInChildren<MeshFilter>(true);
                if (shown != null && magazine.TryGetComponent(out MeshFilter mesh))
                {
                    shown.sharedMesh = mesh.sharedMesh;
                    shown.GetComponent<MeshRenderer>().sharedMaterials = magazine.GetComponent<MeshRenderer>().sharedMaterials;
                    foreach (MeshCollider collider in dropped.GetComponentsInChildren<MeshCollider>(true))
                        collider.sharedMesh = mesh.sharedMesh;
                }
                PrefabUtility.SaveAsPrefabAsset(dropped, magazinePath);
                PrefabUtility.UnloadPrefabContents(dropped);
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
            PrefabUtility.UnloadPrefabContents(source);
        }

        int shifted = 0;
        if (copied > 0)
            foreach (string clipPath in Directory.GetFiles($"{folder}/Animations", $"{gun}_*.anim").Select(p => p.Replace('\\', '/')))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if (binding.type != typeof(Transform) || !binding.propertyName.StartsWith("m_LocalPosition.") || !moved.TryGetValue(binding.path, out Vector3 by))
                        continue;
                    float delta = binding.propertyName.EndsWith("x") ? by.x : binding.propertyName.EndsWith("y") ? by.y : by.z;
                    if (Mathf.Abs(delta) < 1e-6f)
                        continue;
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                    Keyframe[] keys = curve.keys;
                    for (int k = 0; k < keys.Length; k++)
                        keys[k].value += delta;
                    curve.keys = keys;
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                    shifted++;
                }
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
            }
        report.Append($"{shifted} part curves moved; ");

        var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>($"{folder}/{gun}.asset");
        var item = new SerializedObject(definition);
        SerializedProperty repair = item.FindProperty("_jam.Repair");
        if (repair != null)
        {
            repair.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GunRepairProfile>(repairPath);
            item.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(definition);
        }
        else
            report.Append("no _jam.Repair on the definition; ");
        return report.ToString();
    }

    static bool Copy(string from, string to)
    {
        if (File.Exists(to))
            return false;
        if (!AssetDatabase.CopyAsset(from, to))
            throw new Exception($"could not copy {from} to {to}");
        return true;
    }

    // A part's middle in the weapon's space (its mesh bounds, or its origin without a mesh).
    static Vector3 Middle(Transform part, Transform weapon)
    {
        if (!part.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
            return weapon.InverseTransformPoint(part.position);
        return weapon.InverseTransformPoint(part.TransformPoint(filter.sharedMesh.bounds.center));
    }

    // Points every reference into `from`'s hierarchy at the object on the same path under `to`. Returns how many had none.
    static int Remap(Component component, Transform from, Transform to)
    {
        int lost = 0;
        var so = new SerializedObject(component);
        SerializedProperty property = so.GetIterator();
        while (property.Next(true))
        {
            if (property.propertyType != SerializedPropertyType.ObjectReference || property.objectReferenceValue == null)
                continue;
            Object value = property.objectReferenceValue;
            Transform at = value is Component c ? c.transform : value is GameObject g ? g.transform : null;
            if (at == null || at.root != from)
                continue;
            Transform twin = at == from ? to : to.Find(AnimationUtility.CalculateTransformPath(at, from));
            Object mapped = twin == null ? null : value is GameObject ? twin.gameObject : value is Transform ? twin : twin.GetComponent(value.GetType());
            if (mapped == null)
                lost++;
            property.objectReferenceValue = mapped;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return lost;
    }
}
