using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Aipocalypse.Core.HandSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// Builds the first-person hand prefabs, hand set and poses from the VR Low Poly Hand pack.
// unity command run_script --file AgentScripts/FirstPersonHandsSetup.cs --entry FirstPersonHandsSetup.Hands
public static class FirstPersonHandsSetup
{
    const string Pack = "Assets/_StoreAssets/Characters/VR Low Poly Hand (Left & Right)/Prefab/NO UV/";
    const string HandsFolder = "Assets/_Game/Prefabs/Hands";
    const string DataFolder = "Assets/_Game/Data/Hands";
    // The pack's hand is ~5.9 units from wrist to middle fingertip; 0.044 makes it ~26 cm: the low-poly hand reads small
    // next to the weapon models (~1.3x real size) at anything less.
    const float Scale = 0.044f;

    // The pack's hands, in their own space: fingers along +Y, palm facing +Z, thumb on the -X (right) / +X (left) side.
    // Closed = wrapped around a ~3.6 cm handle at the grip point (Curl 1 is a grip, not a tight fist).
    static readonly float[] CurlAngles = { 4f, 80f, 78f, 45f };
    static readonly float[] ThumbCurlAngles = { 35f, 30f, 35f };

    public static object Hands()
    {
        EnsureFolder(HandsFolder);
        EnsureFolder(DataFolder);
        var right = BuildHand(HandSide.Right, Pack + "Right Low Poly Hand NO UV.prefab", HandsFolder + "/FPHand_Right.prefab");
        var left = BuildHand(HandSide.Left, Pack + "Left Low Poly Hand NO UV.prefab", HandsFolder + "/FPHand_Left.prefab");

        string setPath = DataFolder + "/HandSet.asset";
        var set = AssetDatabase.LoadAssetAtPath<HandSet>(setPath);
        if (set == null)
        {
            set = ScriptableObject.CreateInstance<HandSet>();
            AssetDatabase.CreateAsset(set, setPath);
        }
        var so = new SerializedObject(set);
        so.FindProperty("_left").objectReferenceValue = left;
        so.FindProperty("_right").objectReferenceValue = right;
        so.FindProperty("_leftElbow").vector3Value = LeftElbow;
        so.FindProperty("_rightElbow").vector3Value = RightElbow;
        so.FindProperty("_previewItemPosition").vector3Value = PlayerHandPoint(out _);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        return new { left = AssetDatabase.GetAssetPath(left), right = AssetDatabase.GetAssetPath(right), set = setPath };
    }

    // Implied elbows in view space: low and out to the sides, the right one further back (it holds the grip). The left
    // one sits well out to the side, so a support hand cradles a forend palm-up instead of hanging under it.
    static readonly Vector3 LeftElbow = new(-0.38f, -0.45f, 0.08f);
    static readonly Vector3 RightElbow = new(0.24f, -0.45f, -0.18f);

    // The player's Hand point (where held items sit) in camera-pitch space.
    static Vector3 PlayerHandPoint(out Quaternion rotation)
    {
        var player = PrefabUtility.LoadPrefabContents("Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab");
        try
        {
            var pitch = player.transform.Find("CameraRoot/CameraPitch");
            var hand = pitch.Find("HeldItems/Hand");
            rotation = Quaternion.Inverse(pitch.rotation) * hand.rotation;
            return pitch.InverseTransformPoint(hand.position);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(player);
        }
    }

    static HandRig BuildHand(HandSide side, string sourcePath, string path)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, root.transform);
            model.name = "Model";
            model.transform.localScale = Vector3.one * Scale;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                // Bones are moved far from the prefab root every frame; the overlay camera must never cull the hand.
                renderer.updateWhenOffscreen = true;
            }

            var rig = root.AddComponent<HandRig>();
            var rigSo = new SerializedObject(rig);
            rigSo.FindProperty("_side").enumValueIndex = (int)side;
            // Finger half-thickness ~0.24 units, bone to palm skin ~0.4 units in the pack's model.
            rigSo.FindProperty("_fingerRadius").floatValue = 0.24f * Scale;
            rigSo.FindProperty("_palmThickness").floatValue = 0.4f * Scale;
            rigSo.ApplyModifiedPropertiesWithoutUndo();
            Invoke(rig, "AutoFind");

            Transform[] all = model.GetComponentsInChildren<Transform>(true);
            Transform[][] chains = Chains(rig);
            float sign = side == HandSide.Right ? 1f : -1f;

            // Spread: each finger swings away from the middle finger around the palm normal, the thumb away from the index.
            ApplyShape(model.transform, chains, spreadOnly: true, sign);
            Invoke(rig, "CaptureSpread");
            ResetOpen(rig);
            ApplyShape(model.transform, chains, spreadOnly: false, sign);
            Invoke(rig, "CaptureClosed");
            ResetOpen(rig);

            // Grip point: the centre of the handle the closed fingers wrap, just below the knuckles in front of the palm.
            var grip = new GameObject("GripPoint").transform;
            grip.SetParent(root.transform, false);
            Vector3 center = new Vector3(0.2f * sign, 3.3f, 0.95f) * Scale;
            grip.localPosition = center;
            // Z from the little finger towards the index (thumb side), Y out of the back of the hand. Z climbs towards the
            // fingers: a held handle crosses the palm diagonally, from the heel to the index knuckle, so the wrist trails
            // behind and below it instead of sticking straight out.
            grip.localRotation = Quaternion.LookRotation(new Vector3(-sign, 0.6f, 0f), new Vector3(0f, 0f, -1f));
            rigSo = new SerializedObject(rig);
            rigSo.FindProperty("_gripPoint").objectReferenceValue = grip;
            rigSo.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<HandRig>();
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static Transform[][] Chains(HandRig rig)
    {
        var fingers = new SerializedObject(rig).FindProperty("_fingers");
        var chains = new Transform[fingers.arraySize][];
        for (int f = 0; f < chains.Length; f++)
        {
            var joints = fingers.GetArrayElementAtIndex(f).FindPropertyRelative("Joints");
            chains[f] = Enumerable.Range(0, joints.arraySize).Select(j => (Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue).ToArray();
        }
        return chains;
    }

    static void ResetOpen(HandRig rig)
    {
        var fingers = new SerializedObject(rig).FindProperty("_fingers");
        for (int f = 0; f < fingers.arraySize; f++)
        {
            var finger = fingers.GetArrayElementAtIndex(f);
            var joints = finger.FindPropertyRelative("Joints");
            var open = finger.FindPropertyRelative("Open");
            for (int j = 0; j < joints.arraySize; j++)
                ((Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue).localRotation = open.GetArrayElementAtIndex(j).quaternionValue;
        }
    }

    // Rotations are about axes in the hand's own space, converted into each joint's local space at rest, so every
    // joint of a chain bends in the same plane.
    static void ApplyShape(Transform hand, Transform[][] chains, bool spreadOnly, float sign)
    {
        Vector3 middleBase = hand.InverseTransformPoint(chains[(int)Finger.Middle][0].position);
        for (int f = 0; f < chains.Length; f++)
        {
            Transform[] chain = chains[f];
            if (chain.Length == 0)
                continue;
            bool thumb = f == (int)Finger.Thumb;
            var rest = chain.Select(j => j.rotation).ToArray();
            for (int j = 0; j < chain.Length; j++)
            {
                Vector3 axis;
                float angle;
                if (spreadOnly)
                {
                    if (j != (thumb ? 0 : 1))
                        continue;
                    // Which way along X the tip moves: away from the middle finger (thumb side = -X on the right hand).
                    Vector3 fingerBase = hand.InverseTransformPoint(chain[0].position);
                    float towardX = thumb || f == (int)Finger.Middle ? -sign : Mathf.Sign(fingerBase.x - middleBase.x);
                    float amount = thumb ? 30f : f == (int)Finger.Index ? 12f : f == (int)Finger.Middle ? 2f : f == (int)Finger.Ring ? 8f : 16f;
                    // Around the palm normal; a positive angle turns +Y towards -X.
                    axis = Vector3.forward;
                    angle = -amount * towardX;
                }
                else if (thumb)
                {
                    // Curls across, towards the palm and the little finger.
                    Vector3 dir = hand.InverseTransformDirection(chain[Mathf.Min(1, chain.Length - 1)].up);
                    Vector3 move = new Vector3(0.9f * sign, 0.1f, 0.45f).normalized;
                    axis = Vector3.Cross(dir, move).normalized;
                    angle = ThumbCurlAngles[Mathf.Min(j, ThumbCurlAngles.Length - 1)];
                }
                else
                {
                    axis = Vector3.right;
                    angle = CurlAngles[Mathf.Min(j, CurlAngles.Length - 1)];
                }
                Quaternion turn = Quaternion.AngleAxis(angle, hand.TransformDirection(axis));
                Quaternion local = Quaternion.Inverse(rest[j]) * turn * rest[j];
                chain[j].localRotation *= local;
            }
        }
    }

    // ---- Poses ------------------------------------------------------------------------------------------------

    static readonly (string name, HandShape shape)[] PoseTable =
    {
        ("Relaxed", Shape(0.15f, 0.1f, 0.15f, 0.2f, 0.25f, thumbSpread: 0.2f)),
        ("Grip", Shape(0.9f, 1f, 1f, 1f, 1f)),
        ("TriggerGrip", Shape(0.85f, 0.35f, 1f, 1f, 1f)),
        ("Forend", Shape(0.55f, 0.7f, 0.75f, 0.8f, 0.85f, thumbSpread: 0.3f)),
        ("Support", Shape(0.7f, 0.8f, 0.85f, 0.9f, 0.95f, thumbSpread: -0.2f)),
        ("HoldMag", Shape(0.65f, 0.75f, 0.8f, 0.85f, 0.9f)),
        ("Pinch", Shape(0.6f, 0.65f, 0.85f, 0.95f, 1f, thumbSpread: -0.3f)),
    };

    static HandShape Shape(float thumb, float index, float middle, float ring, float little, float thumbSpread = 0f) => new()
    {
        Thumb = new FingerShape(thumb, thumbSpread),
        Index = new FingerShape(index),
        Middle = new FingerShape(middle),
        Ring = new FingerShape(ring),
        Little = new FingerShape(little),
    };

    public static object Poses()
    {
        EnsureFolder(DataFolder + "/Poses");
        foreach (var (poseName, shape) in PoseTable)
        {
            string path = $"{DataFolder}/Poses/{poseName}.asset";
            var pose = AssetDatabase.LoadAssetAtPath<HandPose>(path);
            if (pose == null)
            {
                pose = ScriptableObject.CreateInstance<HandPose>();
                AssetDatabase.CreateAsset(pose, path);
            }
            pose.SetShape(shape);
            EditorUtility.SetDirty(pose);
        }
        AssetDatabase.SaveAssets();
        return PoseTable.Length;
    }

    static HandPose Pose(string poseName) => AssetDatabase.LoadAssetAtPath<HandPose>($"{DataFolder}/Poses/{poseName}.asset");

    static void Invoke(object target, string method) =>
        target.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, null);

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // Adds CameraPitch/HeldItems/Hands (FirstPersonHands, HeldWeapon layer) to the player and wires it.
    public static object Player()
    {
        const string path = "Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var heldItems = root.transform.Find("CameraRoot/CameraPitch/HeldItems");
            var handsTransform = heldItems.Find("Hands");
            if (handsTransform == null)
            {
                handsTransform = new GameObject("Hands").transform;
                handsTransform.SetParent(heldItems, false);
            }
            handsTransform.gameObject.layer = heldItems.Find("Hand").gameObject.layer;
            var hands = handsTransform.GetComponent<FirstPersonHands>() ?? handsTransform.gameObject.AddComponent<FirstPersonHands>();
            var handsSo = new SerializedObject(hands);
            handsSo.FindProperty("_set").objectReferenceValue = AssetDatabase.LoadAssetAtPath<HandSet>(DataFolder + "/HandSet.asset");
            handsSo.FindProperty("_view").objectReferenceValue = root.transform.Find("CameraRoot/CameraPitch");
            handsSo.ApplyModifiedPropertiesWithoutUndo();

            var player = root.GetComponent<Aipocalypse.Core.PlayerSystem.Player>();
            var playerSo = new SerializedObject(player);
            playerSo.FindProperty("_hands").objectReferenceValue = hands;
            playerSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return LayerMask.LayerToName(handsTransform.gameObject.layer);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- Item grips and actions --------------------------------------------------------------------------------

    const string Guns = "Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/";
    const string Melee = "Assets/_Game/_Core/WeaponSystem/Prefabs/Melee/";
    const string ShellMaterialPath = "Assets/_Game/Materials/ShotgunShell.mat";

    static readonly Vector3 Up = Vector3.up, Fwd = Vector3.forward, Right = Vector3.right;
    // Pistol-grip axis: up the handle, leaning forward at the top.
    static readonly Vector3 GripAxis = new Vector3(0f, 0.93f, 0.37f).normalized;
    const HandSide L = HandSide.Left, R = HandSide.Right;
    const FingerMask All = FingerMask.Thumb | FingerMask.Index | FingerMask.Middle | FingerMask.Ring | FingerMask.Little;

    static readonly string[] ItemNames = { "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel", "FireAxe" };

    // Each item is its own call: fitting the grips can take a while and run_script calls time out at 30 s.
    public static object Items() => string.Join(", ", ItemNames.Select(n => Item(n)));

    public static object Item(string name)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        switch (name)
        {
            case "AK47":
                Rifle(Guns + "AK47.prefab", "AR_B", gripPart: "AR_B_Grip", grip: new Vector3(0f, -0.055f, -0.042f),
                    forend: new Vector3(0f, 0.05f, 0.36f), mag: new Vector3(0f, -0.1f, 0.2f), magAxis: new Vector3(0f, 1f, -0.35f),
                    bolt: new Vector3(0.05f, 0.07f, 0.225f), boltPull: new Vector3(0f, 0f, -0.085f),
                    reloadOffset: new Vector3(-0.06f, 0.04f, 0.08f), reloadEuler: new Vector3(-15f, 15f, -40f));
                break;
            case "MP5":
                Rifle(Guns + "MP5.prefab", "SMG_B", gripPart: "SMG_B_Receiver_Down", grip: new Vector3(0f, -0.06f, -0.065f),
                    forend: new Vector3(0f, 0.075f, 0.29f), mag: new Vector3(0f, -0.09f, 0.175f), magAxis: new Vector3(0f, 1f, -0.3f),
                    bolt: new Vector3(-0.055f, 0.125f, 0.325f), boltPull: new Vector3(0f, 0f, -0.1f),
                    reloadOffset: new Vector3(-0.06f, 0.04f, 0.08f), reloadEuler: new Vector3(-15f, 15f, -40f));
                break;
            case "Pistol": Pistol(Guns + "Pistol.prefab"); break;
            case "PumpShotgun": Pump(Guns + "PumpShotgun.prefab"); break;
            case "DoubleBarrel": Break(Guns + "DoubleBarrel.prefab"); break;
            case "FireAxe": Axe(Melee + "FireAxe.prefab"); break;
            default: throw new ArgumentException(name);
        }
        AssetDatabase.SaveAssets();
        return $"{name} {watch.ElapsedMilliseconds} ms";
    }

    sealed class Build
    {
        public GameObject Root;
        public Transform Model;
        public HandGrip Right;
        public HandGrip Left;
        public readonly List<ItemHands.HandAction> Actions = new();

        public Transform Part(string name) => Model.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        // pos / axis are in the model's space: pos on the held part's centre line, axis = little finger → index along it.
        // The roll round the axis comes from the implied elbow; Bake fit then moves the palm onto the part and curls fingers.
        public HandGrip Grip(Transform parent, string name, HandSide side, string pose, Vector3 pos, Vector3 axis, bool fitPalm = true,
            FingerMask keep = FingerMask.None, float roll = 0f, Vector3? backOfHand = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Vector3 a = Model.TransformDirection(axis).normalized;
            // With backOfHand the grip keeps this exact orientation (AsAuthored) instead of turning towards the elbow.
            Vector3 up = Model.TransformDirection(backOfHand ?? Vector3.up);
            go.transform.SetPositionAndRotation(Model.TransformPoint(pos), Quaternion.LookRotation(a, up));
            var grip = go.AddComponent<HandGrip>();
            var so = new SerializedObject(grip);
            so.FindProperty("_side").enumValueIndex = (int)side;
            so.FindProperty("_pose").objectReferenceValue = Pose(pose);
            so.FindProperty("_fitPalm").boolValue = fitPalm;
            so.FindProperty("_keepFingers").intValue = (int)keep;
            so.FindProperty("_roll").floatValue = roll;
            so.FindProperty("_aim").enumValueIndex = (int)(backOfHand.HasValue ? GripAim.AsAuthored : GripAim.TowardElbow);
            so.ApplyModifiedPropertiesWithoutUndo();
            return grip;
        }

        // Shotgun shells (hidden until an action shows them), lying along `axis` at `pos` in model space.
        public GameObject Shell(Transform parent, string name, Vector3 pos, Vector3 axis, int count = 1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Model.TransformPoint(pos), Model.rotation * Quaternion.LookRotation(axis, Vector3.up));
            var material = ShellMaterial();
            for (int i = 0; i < count; i++)
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(body.GetComponent<Collider>());
                body.name = "Shell";
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3((i - (count - 1) * 0.5f) * 0.024f, 0f, 0f);
                body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                // 12 gauge, scaled like the weapon models (~1.3x).
                body.transform.localScale = new Vector3(0.024f, 0.04f, 0.024f);
                var renderer = body.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            go.SetActive(false);
            return go;
        }

        public void Action(string name, ItemHands.HandKey[] hands, ItemHands.PartTrack[] parts = null, ItemHands.ShowSpan[] show = null) =>
            Actions.Add(new ItemHands.HandAction
            {
                Name = name,
                Hands = hands,
                Parts = parts ?? Array.Empty<ItemHands.PartTrack>(),
                Show = show ?? Array.Empty<ItemHands.ShowSpan>(),
            });

        // The gun's own reload / jam tilt (HeldGun → Action motion), chosen so the hands' work stays in view.
        public void GunMotion(Vector3 reloadOffset, Vector3 reloadEuler, Vector3? jamOffset = null, Vector3? jamEuler = null)
        {
            var gun = Root.GetComponent("HeldGun");
            var so = new SerializedObject(gun);
            so.FindProperty("_reloadOffset").vector3Value = reloadOffset;
            so.FindProperty("_reloadEuler").vector3Value = reloadEuler;
            if (jamOffset is Vector3 o) so.FindProperty("_clearJamOffset").vector3Value = o;
            if (jamEuler is Vector3 e) so.FindProperty("_clearJamEuler").vector3Value = e;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static ItemHands.HandKey H(float t, HandSide side, HandGrip grip, float blend = 0f) => new() { Time = t, Side = side, Grip = grip, Blend = blend };
    static ItemHands.PartKey K(float t, Vector3 pos, Vector3 euler = default) => new() { Time = t, Position = pos, Euler = euler };
    static ItemHands.PartTrack T(Transform part, params ItemHands.PartKey[] keys) => new() { Part = part, Keys = keys };
    static ItemHands.ShowSpan S(GameObject target, float from, float to) => new() { Target = target, From = from, To = to };
    static readonly Vector3 Zero = Vector3.zero;

    static void Edit(string path, Action<Build> build)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            // Idempotent: drop what an earlier run made.
            foreach (var old in root.GetComponentsInChildren<HandGrip>(true))
                if (old != null) Object.DestroyImmediate(old.gameObject);
            foreach (var old in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("HandProp_")).ToArray())
                if (old != null) Object.DestroyImmediate(old.gameObject);
            var hands = root.GetComponent<ItemHands>();
            if (hands == null)
                hands = root.AddComponent<ItemHands>();

            // Grips are placed and fitted in the hip / idle pose, as the player sees the item most of the time.
            var pivot = root.transform.Find("PosePivot");
            var rest = root.transform.Find("Poses/Hip") ?? root.transform.Find("Poses/Idle");
            if (pivot != null && rest != null)
                pivot.SetLocalPositionAndRotation(rest.localPosition, rest.localRotation);

            var b = new Build { Root = root, Model = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Model") };
            build(b);

            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(ItemHands).GetField("_right", flags).SetValue(hands, b.Right);
            typeof(ItemHands).GetField("_left", flags).SetValue(hands, b.Left);
            typeof(ItemHands).GetField("_actions", flags).SetValue(hands, b.Actions.ToArray());
            EditorUtility.SetDirty(hands);

            var bake = typeof(HandGrip).Assembly.GetType("Aipocalypse.Core.HandSystem.HandFit")
                .GetMethod("Bake", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            foreach (var grip in root.GetComponentsInChildren<HandGrip>(true))
                bake.Invoke(null, new object[] { grip });
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void Rifle(string path, string prefix, string gripPart, Vector3 grip, Vector3 forend, Vector3 mag, Vector3 magAxis, Vector3 bolt,
        Vector3 boltPull, Vector3 reloadOffset, Vector3 reloadEuler) => Edit(path, b =>
    {
        Transform forendPart = b.Part(prefix + "_BarrelGuard");
        Transform magPart = b.Part(prefix + "_Mag");
        Transform boltPart = b.Part(prefix + "_Bolt");

        b.Right = b.Grip(b.Part(gripPart), "Grip_R", R, "TriggerGrip", grip, GripAxis, keep: FingerMask.Index);
        b.Left = b.Grip(forendPart, "Grip_L_Forend", L, "Forend", forend, Fwd);
        var magGrip = b.Grip(magPart, "Grip_L_Mag", L, "HoldMag", mag, magAxis);
        // Bolt handle on the right (AK): reached over the top, palm down. On the left (MP5): from the side, as aimed.
        var boltGrip = bolt.x > 0f
            // Aimed at the elbow, then rolled -60 degrees: the hand comes over the receiver from the left and hooks the handle.
            ? b.Grip(boltPart, "Grip_L_Bolt", L, "Pinch", bolt + new Vector3(0f, 0.01f, 0f), Fwd, fitPalm: false, roll: -60f)
            : b.Grip(boltPart, "Grip_L_Bolt", L, "Pinch", bolt, Fwd, fitPalm: false);
        b.GunMotion(reloadOffset, reloadEuler);

        // Grab the mag, pull it down and out (front tipping forward), drop it out of view and come back with the "new"
        // one, rock its front into the well, then seat it with a push.
        var pulled = new Vector3(0f, -0.07f, 0.005f);
        var gone = new Vector3(-0.05f, -0.36f, -0.03f);
        var goneTurn = new Vector3(-15f, 10f, 40f);
        var hooked = new Vector3(0f, -0.05f, 0.012f);
        var hookedTurn = new Vector3(10f, 0f, 0f);
        var nearly = new Vector3(0f, -0.012f, 0.003f);
        ItemHands.PartKey[] MagKeys(float start, float end)
        {
            float T01(float t) => Mathf.Lerp(start, end, t);
            return new[]
            {
                K(T01(0.12f), Zero), K(T01(0.24f), pulled, new Vector3(6f, 0f, 0f)), K(T01(0.42f), gone, goneTurn), K(T01(0.56f), gone, goneTurn),
                K(T01(0.76f), hooked, hookedTurn), K(T01(0.88f), nearly, new Vector3(3f, 0f, 0f)), K(T01(0.94f), new Vector3(0f, 0.004f, 0f)), K(T01(1f), Zero),
            };
        }

        b.Action(ItemHands.Actions.Reload,
            new[] { H(0.02f, L, magGrip, 0.2f), H(0.9f, L, null, 0.22f) },
            new[] { T(magPart, MagKeys(0f, 0.86f)) });

        // Empty: the mag change, then the hand goes to the bolt, yanks it back and lets it fly home.
        b.Action(ItemHands.Actions.ReloadEmpty,
            new[] { H(0.02f, L, magGrip, 0.18f), H(0.62f, L, boltGrip, 0.14f), H(0.9f, L, null, 0.18f) },
            new[]
            {
                T(magPart, MagKeys(0f, 0.6f)),
                T(boltPart, K(0.72f, Zero), K(0.79f, boltPull), K(0.81f, boltPull), K(0.83f, Zero)),
            });

        // Jam: hand to the bolt, rack it twice.
        b.Action(ItemHands.Actions.ClearJam,
            new[] { H(0.03f, L, boltGrip, 0.16f), H(0.86f, L, null, 0.18f) },
            new[] { T(boltPart, K(0.22f, Zero), K(0.32f, boltPull), K(0.4f, Zero), K(0.56f, Zero), K(0.66f, boltPull), K(0.74f, Zero)) });
    });

    static void Pistol(string path) => Edit(path, b =>
    {
        Transform receiver = b.Part("Pistol_E_Receiver");
        Transform slide = b.Part("Pistol_E_BarrelGuard");
        Transform magPart = b.Part("Pistol_E_Mag");
        var pistolGrip = new Vector3(0f, 0.95f, 0.3f);

        b.Right = b.Grip(receiver, "Grip_R", R, "TriggerGrip", new Vector3(0f, -0.05f, -0.058f), pistolGrip, keep: FingerMask.Index);
        // Support hand fills the grip's left side, fingers over the shooting hand's (not fitted: they'd stop on the grip).
        b.Left = b.Grip(receiver, "Grip_L_Support", L, "Support", new Vector3(0f, -0.07f, -0.06f), pistolGrip, keep: All);
        var magGrip = b.Grip(magPart, "Grip_L_Mag", L, "HoldMag", new Vector3(0f, -0.11f, -0.062f), Fwd);
        // Overhand rack: palm down on the back of the slide, fingers on its right side.
        var slideGrip = b.Grip(slide, "Grip_L_Slide", L, "Pinch", new Vector3(0f, 0.04f, -0.055f), -Fwd, backOfHand: Up);
        b.GunMotion(new Vector3(-0.04f, 0.07f, 0.01f), new Vector3(-18f, 18f, -30f));

        // The empty mag drops free; the hand goes down for a new one, brings it up into the grip and slaps it home.
        var dropped = new Vector3(0f, -0.32f, -0.02f);
        var droppedTurn = new Vector3(0f, 0f, 20f);
        var started = new Vector3(0f, -0.05f, 0f);
        ItemHands.PartKey[] MagKeys(float start, float end)
        {
            float T01(float t) => Mathf.Lerp(start, end, t);
            return new[]
            {
                K(T01(0.05f), Zero), K(T01(0.28f), dropped, droppedTurn), K(T01(0.5f), dropped, droppedTurn),
                K(T01(0.8f), started), K(T01(0.92f), new Vector3(0f, -0.006f, 0f)), K(T01(1f), Zero),
            };
        }
        b.Action(ItemHands.Actions.Reload,
            new[] { H(0.16f, L, magGrip, 0.22f), H(0.9f, L, null, 0.2f) },
            new[] { T(magPart, MagKeys(0f, 0.86f)) });
        b.Action(ItemHands.Actions.ReloadEmpty,
            new[] { H(0.1f, L, magGrip, 0.18f), H(0.62f, L, slideGrip, 0.14f), H(0.9f, L, null, 0.16f) },
            new[]
            {
                T(magPart, MagKeys(0f, 0.6f)),
                T(slide, K(0.72f, Zero), K(0.79f, new Vector3(0f, 0f, -0.05f)), K(0.81f, new Vector3(0f, 0f, -0.05f)), K(0.83f, Zero)),
            });
        b.Action(ItemHands.Actions.ClearJam,
            new[] { H(0.03f, L, slideGrip, 0.16f), H(0.86f, L, null, 0.18f) },
            new[] { T(slide, K(0.22f, Zero), K(0.32f, new Vector3(0f, 0f, -0.05f)), K(0.4f, Zero), K(0.56f, Zero), K(0.66f, new Vector3(0f, 0f, -0.05f)), K(0.74f, Zero)) });
    });

    static void Pump(string path) => Edit(path, b =>
    {
        Transform pump = b.Part("ShotGun_C_BarrelGuard");

        b.Right = b.Grip(b.Part("ShotGun_C_Grip"), "Grip_R", R, "TriggerGrip", new Vector3(0f, -0.05f, -0.045f), GripAxis, keep: FingerMask.Index);
        // On the pump, which HeldGun already slides: the hand works it with no action of its own.
        b.Left = b.Grip(pump, "Grip_L_Pump", L, "Forend", new Vector3(0f, 0.016f, 0.34f), Fwd);
        b.GunMotion(new Vector3(-0.04f, 0.05f, 0.02f), new Vector3(-10f, 15f, -45f));

        // One shell per round: the hand goes down to the belt, brings a shell up to the loading port under the receiver
        // and thumbs it forward into the tube. The next round starts where this one ended, so there's no jump.
        var shell = b.Shell(b.Model, "HandProp_Shell", new Vector3(0f, 0.0f, 0.2f), Fwd);
        var shellGrip = b.Grip(shell.transform, "Grip_L_Shell", L, "Pinch", new Vector3(0f, -0.012f, 0.18f), Fwd, fitPalm: false, keep: All);
        var belt = new Vector3(-0.07f, -0.28f, -0.1f);
        b.Action(ItemHands.Actions.LoadRound,
            new[] { H(0f, L, shellGrip, 0.12f) },
            new[] { T(shell.transform, K(0f, Zero), K(0.28f, belt, new Vector3(40f, 0f, 0f)), K(0.36f, belt, new Vector3(40f, 0f, 0f)),
                K(0.66f, new Vector3(0f, -0.045f, -0.04f), new Vector3(12f, 0f, 0f)), K(0.86f, Zero), K(1f, Zero)) },
            new[] { S(shell, 0.3f, 0.88f) });
    });

    static void Break(string path) => Edit(path, b =>
    {
        Transform hinge = b.Model.Find("Hinge");

        b.Right = b.Grip(b.Part("ShotGun_E_Receiver"), "Grip_R", R, "TriggerGrip", new Vector3(0f, -0.05f, -0.06f), GripAxis, keep: FingerMask.Index);
        b.Left = b.Grip(b.Part("ShotGun_E_BarrelGuard"), "Grip_L_Forend", L, "Forend", new Vector3(0f, 0.027f, 0.28f), Fwd);
        b.GunMotion(new Vector3(-0.03f, -0.02f, 0.15f), new Vector3(15f, 5f, -15f));

        // Two shells ride the barrels (HeldGun swings them open while reloading): up from the belt, into the chambers.
        var shells = b.Shell(hinge, "HandProp_Shells", new Vector3(0f, 0.047f, 0.135f), Fwd, 2);
        // Palm up under the pair, fingertips pushing them in, so the hand never covers the breech.
        var shellGrip = b.Grip(shells.transform, "Grip_L_Shells", L, "HoldMag", new Vector3(0f, 0.03f, 0.1f), Fwd, fitPalm: false,
            keep: All, backOfHand: new Vector3(-0.45f, -1f, 0f));
        var belt = new Vector3(-0.07f, -0.28f, -0.14f);
        b.Action(ItemHands.Actions.Reload,
            new[] { H(0.12f, L, shellGrip, 0.2f), H(0.8f, L, null, 0.2f) },
            new[] { T(shells.transform, K(0f, belt, new Vector3(40f, 0f, 0f)), K(0.3f, belt, new Vector3(40f, 0f, 0f)),
                K(0.55f, new Vector3(-0.01f, -0.02f, -0.055f), new Vector3(-10f, 0f, 0f)), K(0.72f, Zero), K(1f, Zero)) },
            new[] { S(shells, 0.2f, 0.76f) });
    });

    static void Axe(string path) => Edit(path, b =>
    {
        // The handle runs up the model's Y (head at the top), bowing slightly towards -Z.
        var handle = new Vector3(0f, 1f, -0.08f);
        b.Right = b.Grip(b.Model, "Grip_R", R, "Grip", new Vector3(0f, -0.07f, -0.012f), handle);
        b.Left = b.Grip(b.Model, "Grip_L", L, "Grip", new Vector3(0f, 0.2f, -0.045f), handle);
    });

    static Material ShellMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(ShellMaterialPath);
        if (material != null)
            return material;
        EnsureFolder(Path.GetDirectoryName(ShellMaterialPath).Replace('\\', '/'));
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", new Color(0.62f, 0.09f, 0.07f));
        material.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(material, ShellMaterialPath);
        return material;
    }

    // Renders an item with the hands at points of an action ("" = default grips): first person (top row) and from the left.
    public static string RenderItem(string outDir, string prefabPath, string action, float[] times)
    {
        Vector3 handPos = PlayerHandPoint(out Quaternion handRot);
        var set = AssetDatabase.LoadAssetAtPath<HandSet>(DataFolder + "/HandSet.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var files = new List<string>();
        // Side view: from the left, framed on the item's meshes in the hip pose.
        var probe = (GameObject)Object.Instantiate(prefab);
        probe.transform.SetPositionAndRotation(handPos, handRot);
        var probePivot = probe.transform.Find("PosePivot");
        var probeHip = probe.transform.Find("Poses/Hip") ?? probe.transform.Find("Poses/Idle");
        if (probePivot != null && probeHip != null)
            probePivot.SetLocalPositionAndRotation(probeHip.localPosition, probeHip.localRotation);
        var renderers = probe.GetComponentsInChildren<MeshRenderer>();
        var bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        Object.DestroyImmediate(probe);
        float extent = Mathf.Max(bounds.extents.y, bounds.extents.z) * 0.75f + 0.05f;
        float distance = extent / Mathf.Tan(15f * Mathf.Deg2Rad);
        string label = Path.GetFileNameWithoutExtension(prefabPath) + "_" + (string.IsNullOrEmpty(action) ? "Default" : action);
        // Rows: the player's view, then close views from the front-left and the right, aimed at the item's middle.
        var views = new (string name, Vector3 eye, Vector3 target, float fov)[]
        {
            ("fp", Vector3.zero, new Vector3(0f, 0f, 1f), 60f),
            ("left", bounds.center + new Vector3(-0.8f, 0.15f, 0.55f).normalized * distance, bounds.center, 30f),
            ("right", bounds.center + new Vector3(0.85f, 0.1f, -0.2f).normalized * distance, bounds.center, 30f),
            // From the muzzle end, low: the cross-section of how each hand wraps.
            ("front", bounds.center + new Vector3(-0.15f, -0.1f, 1f).normalized * distance * 0.8f, bounds.center + new Vector3(0f, -0.04f, 0f), 30f),
        };
        foreach (var view in views)
        {
            foreach (float t in times)
            {
                files.Add(Snap(outDir, $"{label}_{t:0.00}_{view.name}", scene =>
                {
                    var item = (GameObject)Object.Instantiate(prefab);
                    SceneManager(scene, item);
                    item.transform.SetPositionAndRotation(handPos, handRot);
                    var pivot = item.transform.Find("PosePivot");
                    var hip = item.transform.Find("Poses/Hip") ?? item.transform.Find("Poses/Idle");
                    if (pivot != null && hip != null)
                        pivot.SetLocalPositionAndRotation(hip.localPosition, hip.localRotation);
                    ApplyGunMotion(item, action);
                    var hands = item.GetComponent<ItemHands>();
                    typeof(ItemHands).GetMethod("CaptureRest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hands, null);
                    if (!string.IsNullOrEmpty(action))
                    {
                        hands.Play(action);
                        hands.SetProgress(t);
                    }
                    foreach (HandSide s in new[] { HandSide.Left, HandSide.Right })
                    {
                        var grip = hands.GetGrip(s, out _);
                        if (grip == null)
                            continue;
                        var rig = Object.Instantiate(set.Get(s));
                        SceneManager(scene, rig.gameObject);
                        grip.Resolve(rig, set.Elbow(s), out Vector3 gripPosition, out Quaternion gripRotation);
                        rig.Place(gripPosition, gripRotation);
                        rig.Apply(grip.Shape);
                    }
                }, view.eye, view.target, 512, view.fov));
            }
        }
        return Sheet(outDir, "sheet_" + label, files, times.Length, 360);
    }

    // First-person frames of one action moment with different reload tilts (offset, euler pairs), for choosing one.
    public static string RenderTilts(string outDir, string prefabPath, string action, float t, float[] tilts)
    {
        Vector3 handPos = PlayerHandPoint(out Quaternion handRot);
        var set = AssetDatabase.LoadAssetAtPath<HandSet>(DataFolder + "/HandSet.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var files = new List<string>();
        for (int i = 0; i + 5 < tilts.Length; i += 6)
        {
            var offset = new Vector3(tilts[i], tilts[i + 1], tilts[i + 2]);
            var euler = new Vector3(tilts[i + 3], tilts[i + 4], tilts[i + 5]);
            files.Add(Snap(outDir, $"tilt_{i / 6}", scene =>
            {
                var item = (GameObject)Object.Instantiate(prefab);
                SceneManager(scene, item);
                item.transform.SetPositionAndRotation(handPos, handRot);
                var pivot = item.transform.Find("PosePivot");
                var hip = item.transform.Find("Poses/Hip");
                pivot.SetLocalPositionAndRotation(hip.localPosition + offset, hip.localRotation * Quaternion.Euler(euler));
                var gunSo = new SerializedObject(item.GetComponent("HeldGun"));
                if (gunSo.FindProperty("_breakPart").objectReferenceValue is Transform breakPart && action != ItemHands.Actions.ClearJam)
                    breakPart.localRotation *= Quaternion.Euler(gunSo.FindProperty("_breakOpenEuler").vector3Value);
                var hands = item.GetComponent<ItemHands>();
                typeof(ItemHands).GetMethod("CaptureRest", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(hands, null);
                hands.Play(action);
                hands.SetProgress(t);
                foreach (HandSide s in new[] { HandSide.Left, HandSide.Right })
                {
                    var grip = hands.GetGrip(s, out _);
                    if (grip == null) continue;
                    var rig = Object.Instantiate(set.Get(s));
                    SceneManager(scene, rig.gameObject);
                    grip.Resolve(rig, set.Elbow(s), out Vector3 p, out Quaternion r);
                    rig.Place(p, r);
                    rig.Apply(grip.Shape);
                }
            }, Vector3.zero, new Vector3(0f, 0f, 1f), 400, 60f));
        }
        return Sheet(outDir, "tilts", files, Mathf.Min(4, files.Count), 300);
    }

    // HeldGun's reload / jam tilt at full weight, as in game during that action.
    static void ApplyGunMotion(GameObject item, string action)
    {
        var gun = item.GetComponent("HeldGun");
        var pivot = item.transform.Find("PosePivot");
        if (gun == null || pivot == null || string.IsNullOrEmpty(action) || action == ItemHands.Actions.Cycle)
            return;
        var so = new SerializedObject(gun);
        bool jam = action == ItemHands.Actions.ClearJam;
        Vector3 offset = so.FindProperty(jam ? "_clearJamOffset" : "_reloadOffset").vector3Value;
        Vector3 euler = so.FindProperty(jam ? "_clearJamEuler" : "_reloadEuler").vector3Value;
        pivot.localPosition += offset;
        pivot.localRotation *= Quaternion.Euler(euler);
        var breakPart = so.FindProperty("_breakPart").objectReferenceValue as Transform;
        if (breakPart != null && !jam)
            breakPart.localRotation *= Quaternion.Euler(so.FindProperty("_breakOpenEuler").vector3Value);
    }

    // Renders the hand prefabs with a few shapes to PNGs, for checking calibration.
    public static object RenderShapes(string outDir)
    {
        var set = AssetDatabase.LoadAssetAtPath<HandSet>(DataFolder + "/HandSet.asset");
        var shapes = new (string, HandShape)[]
        {
            ("open", default),
            ("closed", Uniform(1f, 0f)),
            ("spread", Uniform(0f, 1f)),
            ("grip", Uniform(0.85f, 0f)),
        };
        var files = new List<string>();
        foreach (HandSide side in new[] { HandSide.Right, HandSide.Left })
        {
            foreach (var (label, shape) in shapes)
            {
                files.Add(Snap(outDir, $"{side}_{label}", scene =>
                {
                    var rig = Object.Instantiate(set.Get(side));
                    SceneManager(scene, rig.gameObject);
                    rig.transform.position = Vector3.zero;
                    rig.Apply(shape);
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    SceneManager(scene, marker);
                    marker.transform.localScale = new Vector3(0.035f, 0.08f, 0.035f);
                    marker.transform.SetPositionAndRotation(rig.GripPoint.position, rig.GripPoint.rotation * Quaternion.Euler(90f, 0f, 0f));
                }, new Vector3(0f, 0.09f, 0.35f), new Vector3(0f, 0.09f, 0f)));
                files.Add(Snap(outDir, $"{side}_{label}_side", scene =>
                {
                    var rig = Object.Instantiate(set.Get(side));
                    SceneManager(scene, rig.gameObject);
                    rig.Apply(shape);
                    var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    SceneManager(scene, marker);
                    marker.transform.localScale = new Vector3(0.035f, 0.08f, 0.035f);
                    marker.transform.SetPositionAndRotation(rig.GripPoint.position, rig.GripPoint.rotation * Quaternion.Euler(90f, 0f, 0f));
                }, new Vector3(-0.22f, 0.2f, 0.25f), new Vector3(0f, 0.09f, 0f)));
            }
        }
        foreach (string side in new[] { "Right", "Left" })
            Sheet(outDir, "sheet_" + side, files.Where(f => Path.GetFileName(f).StartsWith(side + "_")).ToList(), 4);
        return files.Count;
    }

    // Tiles PNGs into one image (columns wide), first file at the top left.
    public static string Sheet(string outDir, string name, List<string> files, int columns, int tile = 256)
    {
        int rows = (files.Count + columns - 1) / columns;
        var sheet = new Texture2D(columns * tile, rows * tile, TextureFormat.RGB24, false);
        for (int i = 0; i < files.Count; i++)
        {
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(files[i]));
            int x = (i % columns) * tile, y = (rows - 1 - i / columns) * tile;
            for (int py = 0; py < tile; py++)
                for (int px = 0; px < tile; px++)
                    sheet.SetPixel(x + px, y + py, src.GetPixelBilinear((px + 0.5f) / tile, (py + 0.5f) / tile));
            Object.DestroyImmediate(src);
        }
        sheet.Apply();
        string file = Path.Combine(outDir, name + ".png");
        File.WriteAllBytes(file, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        return file;
    }

    static HandShape Uniform(float curl, float spread) => new()
    {
        Thumb = new FingerShape(curl, spread),
        Index = new FingerShape(curl, spread),
        Middle = new FingerShape(curl, spread),
        Ring = new FingerShape(curl, spread),
        Little = new FingerShape(curl, spread),
    };

    static void SceneManager(UnityEngine.SceneManagement.Scene scene, GameObject go) =>
        EditorSceneManager.MoveGameObjectToScene(go, scene);

    public static string Snap(string outDir, string name, Action<UnityEngine.SceneManagement.Scene> build, Vector3 eye, Vector3 target, int size = 512, float fov = 40f)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            build(scene);
            var lightGo = new GameObject("Light");
            SceneManager(scene, lightGo);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            var camGo = new GameObject("Cam");
            SceneManager(scene, camGo);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.cameraType = CameraType.Preview;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.45f, 0.55f);
            cam.nearClipPlane = 0.01f;
            cam.fieldOfView = fov;
            cam.transform.position = eye;
            cam.transform.LookAt(target);
            var rt = RenderTexture.GetTemporary(size, size, 24);
            cam.targetTexture = rt;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(outDir);
            string file = Path.Combine(outDir, name + ".png");
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return file;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
