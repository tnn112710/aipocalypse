using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Animations.Rigging;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.WeaponSystem;
using Object = UnityEngine.Object;

public static class Ak47Animations
{
    const string GunPath = "Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/AK47.prefab";
    const string Animations = "Assets/_Game/_Core/WeaponSystem/Animations";
    const string Clips = Animations + "/AK47";
    const string Shared = Animations + "/Shared";
    const string ControllerPath = Shared + "/Gun.controller";
    const string OverridePath = Clips + "/AK47.overrideController";
    const string DropPath = "Assets/_Game/_Core/WeaponSystem/Prefabs/Effects/AK47_Magazine.prefab";
    static readonly string[] States = { "Equipped", "Idle", "Firing", "Reloading", "ReloadingEmpty", "Repairing" };

    struct Pose
    {
        public Vector3 P;
        public Quaternion R;
        public HandShape Shape;
    }

    sealed class Context
    {
        public Transform Root, Motion, Model, Magazine, Replacement, Bolt, Free;
        public HandGrip Left, Right;
        public MultiParentConstraint LeftConstraint;
        public Pose Forend, Mag, BoltGrip, RightGrip;
        public Vector3 MagRest, BoltRest;
    }

    public static object Build()
    {
        Folder(Clips);
        Folder(Shared);
        var root = PrefabUtility.LoadPrefabContents(GunPath);
        try
        {
            if (root.GetComponentInChildren<GunAnimation>(true) != null)
                throw new InvalidOperationException("AK47 already converted. Edit its clips directly; Build is a one-time migration.");
            var recoil = root.transform.Find("PosePivot/RecoilPivot");
            var model = recoil.Find("Model");
            var muzzle = recoil.Find("Muzzle");
            var oldGrips = root.GetComponentsInChildren<HandGrip>(true);
            var handSet = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
            var leftRig = Object.Instantiate(handSet.Get(HandSide.Left));
            var rightRig = Object.Instantiate(handSet.Get(HandSide.Right));
            Pose Resolve(string name, HandRig rig)
            {
                var grip = oldGrips.Single(g => g.name == name);
                grip.Resolve(rig, root.transform.TransformPoint(handSet.Elbow(rig.Side) - handSet.PreviewItemPosition), out var p, out var r);
                return new Pose { P = model.InverseTransformPoint(p), R = Quaternion.Inverse(model.rotation) * r, Shape = grip.Shape };
            }
            var forend = Resolve("Grip_L_Forend", leftRig);
            var mag = Resolve("Grip_L_Mag", leftRig);
            var bolt = Resolve("Grip_L_Bolt", leftRig);
            var right = Resolve("Grip_R", rightRig);
            right.Shape = AssetDatabase.LoadAssetAtPath<HandPose>("Assets/_Game/Data/Hands/Poses/TriggerGrip.asset").Shape;
            mag.Shape = AssetDatabase.LoadAssetAtPath<HandPose>("Assets/_Game/Data/Hands/Poses/HoldMag.asset").Shape;
            bolt.Shape = AssetDatabase.LoadAssetAtPath<HandPose>("Assets/_Game/Data/Hands/Poses/Pinch.asset").Shape;
            Object.DestroyImmediate(leftRig.gameObject);
            Object.DestroyImmediate(rightRig.gameObject);

            if (PrefabUtility.IsAnyPrefabInstanceRoot(model.gameObject))
                PrefabUtility.UnpackPrefabInstance(model.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var grip in oldGrips)
                Object.DestroyImmediate(grip.gameObject);

            var animationRoot = Child(recoil, "AnimationRoot");
            var motion = Child(animationRoot, "Motion");
            model.SetParent(motion, false);
            model.name = "Weapon";
            foreach (Transform part in model)
                part.name = part.name.Replace("AR_B_", "").Replace("BarrelGuard", "Handguard").Replace("Mag", "Magazine");
            var animatedMuzzle = Child(model, "Muzzle");
            animatedMuzzle.SetLocalPositionAndRotation(muzzle.localPosition, muzzle.localRotation);
            Set(root.GetComponent<HeldGun>(), "_muzzle", animatedMuzzle);
            muzzle.name = "UnusedMuzzle";
            var ctx = new Context {
                Root = animationRoot, Motion = motion, Model = model,
                Magazine = model.Find("Magazine"), Bolt = model.Find("Bolt"),
                Forend = forend, Mag = mag, BoltGrip = bolt, RightGrip = right
            };
            ctx.MagRest = ctx.Magazine.localPosition;
            ctx.BoltRest = ctx.Bolt.localPosition;
            ctx.Replacement = Object.Instantiate(ctx.Magazine.gameObject, model).transform;
            ctx.Replacement.name = "ReplacementMagazine";
            ctx.Replacement.GetComponent<MeshRenderer>().enabled = false;
            Contact(model.Find("Handguard"), "LeftGrip", model, forend);
            Contact(model.Find("Grip"), "RightGrip", model, right);
            var magContact = Contact(ctx.Magazine, "LeftGrip", model, mag);
            var newContact = Contact(ctx.Replacement, "LeftGrip", model, mag);
            var boltContact = Contact(ctx.Bolt, "LeftGrip", model, bolt);

            var controls = Child(motion, "Controls");
            ctx.Free = Child(controls, "LeftFree");
            ctx.Free.SetLocalPositionAndRotation(forend.P, forend.R);
            var targets = Child(motion, "HandTargets");
            ctx.Left = Target(targets, "Left", HandSide.Left, forend);
            ctx.Right = Target(targets, "Right", HandSide.Right, right);
            var rigObject = Child(animationRoot, "Rig");
            var rig = rigObject.gameObject.AddComponent<Rig>();
            ctx.LeftConstraint = Constraint(rigObject, "LeftHand", ctx.Left.transform,
                model.Find("Handguard/LeftGrip"), magContact, newContact, boltContact, ctx.Free);
            Constraint(rigObject, "RightHand", ctx.Right.transform, model.Find("Grip/RightGrip"));

            var animator = animationRoot.gameObject.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var rigBuilder = animationRoot.gameObject.AddComponent<RigBuilder>();
            rigBuilder.layers.Add(new RigLayer(rig));
            var dropped = DroppedMagazine(ctx.Magazine);
            var controller = CreateController();
            var overrides = new AnimatorOverrideController(controller) { name = "AK47" };
            AssetDatabase.CreateAsset(overrides, OverridePath);
            var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/AK47.asset");
            foreach (string state in States)
            {
                float duration = state == "Equipped" ? definition.EquipTime : state == "Idle" ? 3f :
                    state == "Firing" ? definition.Fire.SecondsPerShot : state == "Repairing" ? definition.Jam.ClearTime : definition.Magazine.ReloadTime;
                var clip = CreateClip(ctx, state, duration);
                overrides[state] = clip;
            }
            animator.runtimeAnimatorController = overrides;
            var animation = animationRoot.gameObject.AddComponent<GunAnimation>();
            Set(animation, "_animator", animator);
            Set(animation, "_rig", rigBuilder);
            Set(animation, "_magazine", ctx.Magazine);
            Set(animation, "_droppedMagazine", dropped);
            Set(root.GetComponent<HeldGun>(), "_animation", animation);
            var hands = root.GetComponent<ItemHands>();
            Set(hands, "_left", ctx.Left);
            Set(hands, "_right", ctx.Right);
            var so = new SerializedObject(hands);
            so.FindProperty("_handScale").floatValue = 1.12f;
            so.FindProperty("_actions").arraySize = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GunPath);
            AssetDatabase.SaveAssets();
            return new { prefab = GunPath, controller = ControllerPath, overrides = OverridePath, clips = States };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    public static object UpdateRepairing()
    {
        var root = PrefabUtility.LoadPrefabContents(GunPath);
        try
        {
            var animationRoot = root.transform.Find("PosePivot/RecoilPivot/AnimationRoot");
            var model = animationRoot.Find("Motion/Weapon");
            var hands = root.GetComponent<ItemHands>();
            var forend = model.Find("Handguard/LeftGrip");
            var c = new Context {
                Root = animationRoot, Motion = animationRoot.Find("Motion"), Model = model,
                Magazine = model.Find("Magazine"), Replacement = model.Find("ReplacementMagazine"), Bolt = model.Find("Bolt"),
                Free = animationRoot.Find("Motion/Controls/LeftFree"), Left = hands.GetDefault(HandSide.Left),
                LeftConstraint = animationRoot.Find("Rig/LeftHand").GetComponent<MultiParentConstraint>(),
                Forend = new Pose { P = model.InverseTransformPoint(forend.position), R = Quaternion.Inverse(model.rotation) * forend.rotation,
                    Shape = hands.GetDefault(HandSide.Left).Shape }
            };
            c.BoltRest = c.Bolt.localPosition;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/AK47_Repairing.anim");
            Undo.RegisterCompleteObjectUndo(clip, "AK47 receiver slap");
            Repair(clip, c, clip.length);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssetIfDirty(clip);
            return "Repairing now slaps the receiver; clip duration and gameplay unchanged.";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static object RenderRepairing() => Render(true);

    public static object Render(bool repairingOnly = false)
    {
        const string output = "Temp/AK47Animations";
        Directory.CreateDirectory(output);
        var set = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GunPath);
        var files = new List<string>();
        var poses = new[] { ("Idle", 0f), ("Equipped", 0.35f), ("Firing", 0.3f), ("Reloading", 0.1f),
            ("Reloading", 0.22f), ("Reloading", 0.36f), ("Reloading", 0.5f), ("Reloading", 0.7f),
            ("ReloadingEmpty", 0.75f), ("ReloadingEmpty", 0.82f), ("ReloadingEmpty", 0.94f), ("Repairing", 0.33f) };
        if (repairingOnly)
            poses = new[] { 0f, 0.1f, 0.2f, 0.3f, 0.35f, 0.4f, 0.43f, 0.46f, 0.5f, 0.65f, 0.85f, 1f }
                .Select(t => ("Repairing", t)).ToArray();
        var sheet = new Texture2D(4 * 480, 3 * 270, TextureFormat.RGB24, false);
        for (int i = 0; i < poses.Length; i++)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            GunAnimation animation = null;
            try
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                item.transform.position = set.PreviewItemPosition;
                animation = item.GetComponentInChildren<GunAnimation>();
                string state = poses[i].Item1;
                float t = poses[i].Item2;
                if (state == "Equipped") { animation.Show(); animation.Tick(t * 0.45f, 0f); }
                else if (state == "Firing") { animation.Fired(); animation.Tick(t * 0.1f, 0f); }
                else if (state == "Idle") { animation.EndAction(); animation.Tick(0.01f, 0f); }
                else { animation.StartAction(state == "Repairing" ? GunAction.ClearingJam : GunAction.Reloading, state == "ReloadingEmpty"); animation.Tick(0.01f, t); }
                var hands = item.GetComponent<ItemHands>();
                foreach (HandSide side in new[] { HandSide.Left, HandSide.Right })
                {
                    var hand = Object.Instantiate(set.Get(side));
                    SceneManager.MoveGameObjectToScene(hand.gameObject, scene);
                    var grip = hands.GetGrip(side, out _);
                    hand.transform.localScale *= hands.HandScale;
                    grip.Resolve(hand, set.Elbow(side), out var p, out var r);
                    hand.Place(p, r);
                    hand.Apply(grip.Shape);
                }
                var lightGo = new GameObject("Light");
                SceneManager.MoveGameObjectToScene(lightGo, scene);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.5f;
                lightGo.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
                var cameraGo = new GameObject("Camera");
                SceneManager.MoveGameObjectToScene(cameraGo, scene);
                var camera = cameraGo.AddComponent<Camera>();
                camera.scene = scene;
                camera.cameraType = CameraType.Preview;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.25f, 0.31f, 0.38f);
                camera.nearClipPlane = 0.01f;
                camera.fieldOfView = 70f;
                var rt = RenderTexture.GetTemporary(960, 540, 24);
                var previous = RenderTexture.active;
                var frame = new Texture2D(960, 540, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = rt;
                    camera.Render();
                    RenderTexture.active = rt;
                    frame.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                    frame.Apply();
                    string path = output + "/" + i.ToString("00") + "_" + state + "_" + t.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + ".png";
                    File.WriteAllBytes(path, frame.EncodeToPNG());
                    files.Add(path);
                    for (int y = 0; y < 270; y++)
                        for (int x = 0; x < 480; x++)
                            sheet.SetPixel((i % 4) * 480 + x, (2 - i / 4) * 270 + y, frame.GetPixel(x * 2, y * 2));
                }
                finally
                {
                    RenderTexture.active = previous;
                    camera.targetTexture = null;
                    RenderTexture.ReleaseTemporary(rt);
                    Object.DestroyImmediate(frame);
                }
            }
            finally
            {
                animation?.Stop();
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        sheet.Apply();
        string sheetPath = output + "/contact-sheet.png";
        File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        return new { sheet = sheetPath, frames = files };
    }

    static Transform Contact(Transform parent, string name, Transform space, Pose pose)
    {
        var contact = Child(parent, name);
        contact.SetPositionAndRotation(space.TransformPoint(pose.P), space.rotation * pose.R);
        return contact;
    }

    static HandGrip Target(Transform parent, string name, HandSide side, Pose pose)
    {
        var target = Child(parent, name);
        target.SetLocalPositionAndRotation(pose.P, pose.R);
        var grip = target.gameObject.AddComponent<HandGrip>();
        var so = new SerializedObject(grip);
        so.FindProperty("_aim").enumValueIndex = (int)GripAim.AsAuthored;
        so.FindProperty("_side").enumValueIndex = (int)side;
        so.FindProperty("_animateShape").boolValue = true;
        SetShape(so.FindProperty("_animatedShape"), pose.Shape);
        so.ApplyModifiedPropertiesWithoutUndo();
        return grip;
    }

    static void SetShape(SerializedProperty property, HandShape shape)
    {
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        for (int i = 0; i < fingers.Length; i++)
        {
            var finger = property.FindPropertyRelative(fingers[i]);
            finger.FindPropertyRelative("Curl").floatValue = shape[i].Curl;
            finger.FindPropertyRelative("Spread").floatValue = shape[i].Spread;
            finger.FindPropertyRelative("Tip").floatValue = shape[i].Tip;
        }
    }

    static MultiParentConstraint Constraint(Transform rig, string name, Transform target, params Transform[] sources)
    {
        var constraint = Child(rig, name).gameObject.AddComponent<MultiParentConstraint>();
        var data = constraint.data;
        data.constrainedObject = target;
        var weights = new WeightedTransformArray();
        for (int i = 0; i < sources.Length; i++)
            weights.Add(new WeightedTransform(sources[i], i == 0 ? 1f : 0f));
        data.sourceObjects = weights;
        data.maintainPositionOffset = false;
        data.maintainRotationOffset = false;
        constraint.data = data;
        return constraint;
    }

    static PooledEffect DroppedMagazine(Transform magazine)
    {
        var go = new GameObject("AK47_Magazine");
        try
        {
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = magazine.GetComponent<MeshFilter>().sharedMesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = magazine.GetComponent<MeshRenderer>().sharedMaterials;
            var collider = go.AddComponent<BoxCollider>();
            collider.center = filter.sharedMesh.bounds.center;
            collider.size = filter.sharedMesh.bounds.size;
            var body = go.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            var effect = go.AddComponent<PooledEffect>();
            var so = new SerializedObject(effect);
            so.FindProperty("_lifetime").floatValue = 4f;
            so.FindProperty("_maxActive").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(go, DropPath).GetComponent<PooledEffect>();
        }
        finally { Object.DestroyImmediate(go); }
    }

    static AnimatorController CreateController()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
            return controller;
        controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        foreach (string name in States)
        {
            var slot = new AnimationClip { name = name, frameRate = 60f };
            AnimationUtility.SetEditorCurve(slot, EditorCurveBinding.FloatCurve("Motion", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Constant(0f, 1f, 0f));
            AssetDatabase.CreateAsset(slot, Shared + "/" + name + ".anim");
            var state = machine.AddState(name, new Vector3(260f, 60f + Array.IndexOf(States, name) * 65f, 0f));
            state.motion = slot;
            state.writeDefaultValues = false;
            if (name == "Idle") machine.defaultState = state;
        }
        return controller;
    }

    static AnimationClip CreateClip(Context c, string state, float duration)
    {
        string path = Clips + "/AK47_" + state + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip { name = "AK47_" + state, frameRate = 60f };
            AssetDatabase.CreateAsset(clip, path);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = state == "Idle";
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        Position(clip, c, c.Motion, duration, (0f, Vector3.zero), (1f, Vector3.zero));
        Rotation(clip, c, c.Motion, duration, (0f, Quaternion.identity), (1f, Quaternion.identity));
        Position(clip, c, c.Magazine, duration, (0f, c.MagRest), (1f, c.MagRest));
        Rotation(clip, c, c.Magazine, duration, (0f, Quaternion.identity), (1f, Quaternion.identity));
        Position(clip, c, c.Replacement, duration, (0f, c.MagRest), (1f, c.MagRest));
        Rotation(clip, c, c.Replacement, duration, (0f, Quaternion.identity), (1f, Quaternion.identity));
        Position(clip, c, c.Bolt, duration, (0f, c.BoltRest), (1f, c.BoltRest));
        Position(clip, c, c.Free, duration, (0f, c.Forend.P), (1f, c.Forend.P));
        Rotation(clip, c, c.Free, duration, (0f, c.Forend.R), (1f, c.Forend.R));
        Weights(clip, c, duration, (0f, 0), (1f, 0));
        Visible(clip, c, c.Magazine, duration, (0f, 1f), (1f, 1f));
        Visible(clip, c, c.Replacement, duration, (0f, 0f), (1f, 0f));
        Shape(clip, c, c.Left, duration, (0f, c.Forend.Shape), (1f, c.Forend.Shape));
        Shape(clip, c, c.Right, duration, (0f, c.RightGrip.Shape), (1f, c.RightGrip.Shape));
        if (state == "Equipped")
        {
            Position(clip, c, c.Motion, duration, (0f, new Vector3(0.02f, -0.07f, -0.035f)), (0.72f, new Vector3(0f, 0.004f, 0.004f)), (1f, Vector3.zero));
            Euler(clip, c, c.Motion, duration, (0f, new Vector3(8f, -5f, 10f)), (0.7f, new Vector3(-0.7f, 0f, -1f)), (1f, Vector3.zero));
        }
        else if (state == "Idle")
        {
            Position(clip, c, c.Motion, duration, (0f, Vector3.zero), (0.25f, new Vector3(0.0007f, 0.0012f, 0f)), (0.5f, new Vector3(0f, 0.002f, 0f)), (0.75f, new Vector3(-0.0007f, 0.0012f, 0f)), (1f, Vector3.zero));
        }
        else if (state == "Firing")
        {
            Position(clip, c, c.Bolt, duration, (0f, c.BoltRest), (0.3f, c.BoltRest + Vector3.back * 0.055f), (0.72f, c.BoltRest), (1f, c.BoltRest));
            var pulled = c.RightGrip.Shape;
            pulled.Index.Curl = Mathf.Clamp01(pulled.Index.Curl + 0.12f);
            Shape(clip, c, c.Right, duration, (0f, c.RightGrip.Shape), (0.2f, pulled), (0.8f, c.RightGrip.Shape), (1f, c.RightGrip.Shape));
        }
        else if (state == "Reloading" || state == "ReloadingEmpty")
            Reload(clip, c, duration, state == "ReloadingEmpty");
        else if (state == "Repairing")
            Repair(clip, c, duration);
        return clip;
    }

    static void Reload(AnimationClip clip, Context c, float duration, bool empty)
    {
        float seated = empty ? 0.64f : 0.76f;
        float back = empty ? 0.93f : 0.9f;
        var tilt = new Vector3(-15f, 15f, -32f);
        Position(clip, c, c.Motion, duration, (0f, Vector3.zero), (0.12f, new Vector3(-0.045f, 0.09f, 0.055f)), (seated - 0.02f, new Vector3(-0.045f, 0.09f, 0.055f)), (seated, new Vector3(-0.043f, 0.099f, 0.05f)), (seated + 0.035f, new Vector3(-0.045f, 0.09f, 0.055f)), (0.91f, new Vector3(-0.02f, 0.035f, 0.025f)), (1f, Vector3.zero));
        Euler(clip, c, c.Motion, duration, (0f, Vector3.zero), (0.14f, tilt), (seated, tilt + new Vector3(1f, 0f, 1.2f)), (seated + 0.035f, tilt), (0.9f, tilt * 0.55f), (1f, Vector3.zero));
        var extracted = new Vector3(-0.025f, -0.12f, 0.018f);
        Position(clip, c, c.Magazine, duration, (0f, c.MagRest), (0.16f, c.MagRest), (0.24f, c.MagRest + new Vector3(0f, -0.065f, 0.015f)), (0.3f, c.MagRest + extracted), (1f, c.MagRest + extracted));
        Euler(clip, c, c.Magazine, duration, (0f, Vector3.zero), (0.16f, Vector3.zero), (0.24f, new Vector3(14f, 0f, 0f)), (0.3f, new Vector3(18f, -5f, 12f)), (1f, new Vector3(18f, -5f, 12f)));
        Visible(clip, c, c.Magazine, duration, (0f, 1f), (0.3f, 0f), (1f, 0f));
        var fromBelt = new Vector3(-0.09f, -0.32f, -0.045f);
        Position(clip, c, c.Replacement, duration, (0f, c.MagRest + fromBelt), (0.42f, c.MagRest + fromBelt), (seated - 0.16f, c.MagRest + new Vector3(-0.015f, -0.07f, 0.025f)), (seated - 0.055f, c.MagRest + new Vector3(0f, -0.025f, 0.015f)), (seated, c.MagRest), (seated + 0.025f, c.MagRest + new Vector3(0f, 0.003f, 0f)), (seated + 0.05f, c.MagRest), (1f, c.MagRest));
        Euler(clip, c, c.Replacement, duration, (0f, new Vector3(18f, -8f, -12f)), (0.42f, new Vector3(18f, -8f, -12f)), (seated - 0.16f, new Vector3(14f, 0f, 0f)), (seated - 0.055f, new Vector3(8f, 0f, 0f)), (seated, Vector3.zero), (1f, Vector3.zero));
        Visible(clip, c, c.Replacement, duration, (0f, 0f), (0.42f, 1f), (1f, 1f));

        var releasePosition = c.MagRest + extracted + Quaternion.Euler(18f, -5f, 12f) * (c.Mag.P - c.MagRest);
        var beltGrip = c.MagRest + fromBelt + Quaternion.Euler(18f, -8f, -12f) * (c.Mag.P - c.MagRest);
        var seatedPosition = c.Mag.P;
        var boltStart = c.BoltGrip.P;
        var boltEnd = boltStart + Vector3.back * 0.085f;
        Position(clip, c, c.Free, duration,
            (0f, c.Forend.P), (0.02f, c.Forend.P), (0.08f, Vector3.Lerp(c.Forend.P, c.Mag.P, 0.5f) + new Vector3(-0.04f, 0.015f, -0.025f)), (0.145f, c.Mag.P),
            (0.3f, releasePosition), (0.36f, releasePosition + new Vector3(-0.045f, -0.08f, -0.015f)), (0.42f, beltGrip),
            (seated + 0.025f, seatedPosition),
            (empty ? 0.73f : 0.83f, empty ? boltStart + new Vector3(-0.035f, 0.07f, -0.02f) : Vector3.Lerp(seatedPosition, c.Forend.P, 0.5f) + new Vector3(-0.045f, 0f, -0.03f)),
            (empty ? 0.77f : back, empty ? boltStart : c.Forend.P),
            (empty ? 0.85f : 0.95f, empty ? boltEnd : c.Forend.P),
            (empty ? 0.89f : 0.98f, empty ? boltEnd + new Vector3(-0.035f, 0.04f, -0.025f) : c.Forend.P),
            (back, c.Forend.P), (1f, c.Forend.P));
        Rotation(clip, c, c.Free, duration, (0f, c.Forend.R), (0.145f, c.Mag.R), (0.3f, Quaternion.Euler(18f, -5f, 12f) * c.Mag.R), (0.42f, Quaternion.Euler(18f, -8f, -12f) * c.Mag.R), (seated + 0.025f, c.Mag.R), (empty ? 0.77f : back, empty ? c.BoltGrip.R : c.Forend.R), (empty ? 0.85f : 0.96f, empty ? c.BoltGrip.R : c.Forend.R), (back, c.Forend.R), (1f, c.Forend.R));
        if (empty)
        {
            Weights(clip, c, duration, (0f, 0), (0.02f, 4), (0.145f, 1), (0.3f, 4), (0.42f, 2), (seated + 0.025f, 4), (0.77f, 3), (0.85f, 4), (back, 0), (1f, 0));
            Position(clip, c, c.Bolt, duration, (0f, c.BoltRest), (0.78f, c.BoltRest), (0.83f, c.BoltRest + Vector3.back * 0.085f), (0.85f, c.BoltRest + Vector3.back * 0.085f), (0.865f, c.BoltRest), (1f, c.BoltRest));
        }
        else
            Weights(clip, c, duration, (0f, 0), (0.02f, 4), (0.145f, 1), (0.3f, 4), (0.42f, 2), (seated + 0.025f, 4), (back, 0), (1f, 0));
        var open = HandShape.Lerp(c.Mag.Shape, default, 0.8f);
        Shape(clip, c, c.Left, duration, (0f, c.Forend.Shape), (0.065f, open), (0.125f, open), (0.16f, c.Mag.Shape), (0.28f, c.Mag.Shape), (0.315f, open), (0.38f, open), (0.435f, c.Mag.Shape), (seated, c.Mag.Shape), (seated + 0.04f, open), (empty ? 0.78f : back, empty ? c.BoltGrip.Shape : c.Forend.Shape), (empty ? 0.845f : 0.95f, empty ? c.BoltGrip.Shape : c.Forend.Shape), (empty ? 0.87f : 0.98f, empty ? open : c.Forend.Shape), (back + 0.025f, c.Forend.Shape), (1f, c.Forend.Shape));
    }

    static void Repair(AnimationClip clip, Context c, float duration)
    {
        var tilt = new Vector3(-10f, 25f, -20f);
        var held = new Vector3(-0.035f, 0.07f, 0.035f);
        Position(clip, c, c.Motion, duration, (0f, Vector3.zero), (0.18f, held), (0.43f, held),
            (0.455f, held + new Vector3(0.014f, -0.004f, 0.008f)), (0.50f, held + new Vector3(-0.003f, 0.002f, -0.001f)),
            (0.58f, held), (0.78f, held * 0.65f), (1f, Vector3.zero));
        Euler(clip, c, c.Motion, duration, (0f, Vector3.zero), (0.18f, tilt), (0.43f, tilt),
            (0.455f, tilt + new Vector3(-1.5f, 4f, -3f)), (0.50f, tilt + new Vector3(0.5f, -1f, 0.8f)),
            (0.58f, tilt), (0.78f, tilt * 0.65f), (1f, Vector3.zero));
        Position(clip, c, c.Bolt, duration, (0f, c.BoltRest), (1f, c.BoltRest));
        var hit = new Vector3(-0.05f, 0.07f, 0.15f);
        var windup = hit + new Vector3(-0.18f, 0.055f, -0.035f);
        var slapRotation = Quaternion.LookRotation(Vector3.up, Vector3.left);
        Position(clip, c, c.Free, duration, (0f, c.Forend.P), (0.04f, c.Forend.P),
            (0.16f, Vector3.Lerp(c.Forend.P, windup, 0.6f) + Vector3.up * 0.045f),
            (0.27f, windup), (0.34f, windup), (0.43f, hit), (0.445f, hit),
            (0.49f, hit + new Vector3(-0.08f, 0.02f, -0.01f)), (0.60f, hit + new Vector3(-0.13f, 0.015f, 0.045f)),
            (0.74f, Vector3.Lerp(hit, c.Forend.P, 0.65f) + new Vector3(-0.055f, 0.025f, 0f)),
            (0.88f, c.Forend.P), (1f, c.Forend.P));
        Rotation(clip, c, c.Free, duration, (0f, c.Forend.R), (0.04f, c.Forend.R),
            (0.25f, Quaternion.Euler(0f, 0f, -12f) * slapRotation), (0.34f, Quaternion.Euler(0f, 0f, -12f) * slapRotation),
            (0.43f, slapRotation), (0.49f, slapRotation), (0.64f, slapRotation), (0.88f, c.Forend.R), (1f, c.Forend.R));
        Weights(clip, c, duration, (0f, 0), (0.04f, 4), (0.88f, 0), (1f, 0));
        var open = new HandShape {
            Thumb = new FingerShape(0.18f, 0.25f), Index = new FingerShape(0.05f),
            Middle = new FingerShape(0.08f), Ring = new FingerShape(0.1f), Little = new FingerShape(0.12f)
        };
        Shape(clip, c, c.Left, duration, (0f, c.Forend.Shape), (0.14f, open), (0.43f, open),
            (0.60f, open), (0.75f, HandShape.Lerp(open, c.Forend.Shape, 0.35f)), (0.90f, c.Forend.Shape), (1f, c.Forend.Shape));
    }

    static void Weights(AnimationClip clip, Context c, float duration, params (float t, int source)[] keys)
    {
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            Curve(clip, c, c.LeftConstraint.transform, typeof(MultiParentConstraint), "m_Data.m_SourceObjects.m_Item" + i + ".weight", duration, keys.Select(k => (k.t, k.source == index ? 1f : 0f)).ToArray(), true);
        }
    }

    static void Shape(AnimationClip clip, Context c, HandGrip grip, float duration, params (float t, HandShape shape)[] keys)
    {
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        for (int i = 0; i < fingers.Length; i++)
        {
            int f = i;
            Curve(clip, c, grip.transform, typeof(HandGrip), "_animatedShape." + fingers[i] + ".Curl", duration, keys.Select(k => (k.t, k.shape[f].Curl)).ToArray());
            Curve(clip, c, grip.transform, typeof(HandGrip), "_animatedShape." + fingers[i] + ".Spread", duration, keys.Select(k => (k.t, k.shape[f].Spread)).ToArray());
            Curve(clip, c, grip.transform, typeof(HandGrip), "_animatedShape." + fingers[i] + ".Tip", duration, keys.Select(k => (k.t, k.shape[f].Tip)).ToArray());
        }
    }

    static void Visible(AnimationClip clip, Context c, Transform part, float duration, params (float t, float v)[] keys) =>
        Curve(clip, c, part, typeof(MeshRenderer), "m_Enabled", duration, keys, true);

    static void Position(AnimationClip clip, Context c, Transform part, float duration, params (float t, Vector3 v)[] keys)
    {
        for (int i = 0; i < 3; i++)
        {
            int axis = i;
            Curve(clip, c, part, typeof(Transform), "m_LocalPosition." + "xyz"[i], duration, keys.Select(k => (k.t, k.v[axis])).ToArray());
        }
    }

    static void Euler(AnimationClip clip, Context c, Transform part, float duration, params (float t, Vector3 v)[] keys) =>
        Rotation(clip, c, part, duration, keys.Select(k => (k.t, Quaternion.Euler(k.v))).ToArray());

    static void Rotation(AnimationClip clip, Context c, Transform part, float duration, params (float t, Quaternion v)[] keys)
    {
        for (int i = 0; i < 4; i++)
        {
            int axis = i;
            Curve(clip, c, part, typeof(Transform), "m_LocalRotation." + "xyzw"[i], duration, keys.Select(k => (k.t, k.v[axis])).ToArray());
        }
        clip.EnsureQuaternionContinuity();
    }

    static void Curve(AnimationClip clip, Context c, Transform part, Type type, string property, float duration, (float t, float v)[] keys, bool stepped = false)
    {
        var curve = new AnimationCurve(keys.OrderBy(k => k.t).GroupBy(k => k.t).Select(g => g.Last()).Select(k => new Keyframe(k.t * duration, k.v)).ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            var mode = stepped ? AnimationUtility.TangentMode.Constant : AnimationUtility.TangentMode.ClampedAuto;
            AnimationUtility.SetKeyLeftTangentMode(curve, i, mode);
            AnimationUtility.SetKeyRightTangentMode(curve, i, mode);
        }
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(part, c.Root), type, property), curve);
    }

    static void Set(Object target, string property, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(property).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    public static object Inspect()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(GunPath);
        return root.GetComponentsInChildren<Transform>(true).Select(t => new {
            path = AnimationUtility.CalculateTransformPath(t, root.transform),
            p = t.localPosition.ToString("F3"), r = t.localEulerAngles.ToString("F1"), s = t.localScale.ToString("F3"),
            components = t.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()
        }).ToArray();
    }
}
