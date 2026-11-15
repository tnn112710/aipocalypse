// Retargets a KINEMATION FPS Animation Pack reload onto one of our guns, for `unity command script run`.
// The pack's arms and gun are sampled in a preview scene; nothing in the pack or the open scene changes.
//   Describe(gun)                 what the source rig and clips hold (bones, lengths, part travel)
//   Bake(gun)                     writes <Gun>_Reloading / _ReloadingEmpty from the pack's reloads: left-hand path and
//                                 fingers, magazine, bolt and gun tilt, fitted to our gun's proportions; sets the
//                                 forend contact and idle finger shape to the pack's hold; rebases the other clips
//   SourceStrip(gun, clip, n, view)   the pack's own animation as a grid of n views (reference), in Temp/ArmReview;
//                                 view = "eye", "left" or "below"
// Guns are listed in Table below: our gun -> the pack weapon and clips it borrows.
using System;
using System.Collections.Generic;
using System.IO;
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

public static class ReloadRetarget
{
    const string Pack = "Assets/_StoreAssets/Animation/KINEMATION/FPSAnimationPack";
    const string OutFolder = "Temp/ArmReview";

    sealed class Entry
    {
        public string PackWeapon;      // Prefabs/<PackWeapon>.prefab
        public string Folder;          // Animations/<Folder>
        public string Idle, WeaponIdle;
        public string Reload, WeaponReload;
        public string ReloadEmpty, WeaponReloadEmpty;
        // Hand-tuned nudges in our gun's space (m), on top of the fitted mapping.
        public Vector3 ForendOffset, MagazineOffset, BoltOffset;
        // Extra lift / turn of the whole gun while reloading, so the hands' work is in view (eased in and out).
        public Vector3 PresentPosition, PresentEuler;
        // 0 = fitted from the two guns' proportions.
        public float Scale;
    }

    static readonly Dictionary<string, Entry> Table = new()
    {
        ["AK47"] = new Entry
        {
            PackWeapon = "AK", Folder = "AK",
            Idle = "Character/A_FP_AKX_Idle.anim", WeaponIdle = "Weapon/A_W_AKX_Idle.FBX",
            Reload = "Character/A_FP_AKX_Reload_Tac.anim", WeaponReload = "Weapon/A_W_AKX_Reload_Tac.FBX",
            ReloadEmpty = "Character/A_FP_AKX_Reload_Empty.anim", WeaponReloadEmpty = "Weapon/A_W_AKX200_Reload_Empty.FBX",
            PresentPosition = new Vector3(-0.04f, 0.07f, 0f), PresentEuler = new Vector3(-6f, 0f, 0f),
        },
    };

    // The pack's player rig with one of its guns on the weapon bone, posed by a character clip and a weapon clip.
    sealed class Source : IDisposable
    {
        public Scene Scene;
        public Transform Arms, WeaponBone, Weapon, Camera;
        public GameObject WeaponAnimated;
        public Quaternion RotationOffset;
        public AnimationClip Character, WeaponClip;
        readonly Dictionary<string, Transform> _bones = new();

        public Source(Entry entry, string characterClip, string weaponClip)
        {
            Scene = EditorSceneManager.NewPreviewScene();
            GameObject player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/Prefabs/FPSPlayer.prefab"));
            SceneManager.MoveGameObjectToScene(player, Scene);
            MonoBehaviour script = player.GetComponentsInChildren<MonoBehaviour>(true).First(m => m != null && m.GetType().Name == "FPSPlayer");
            Arms = script.transform;
            WeaponBone = (Transform)new SerializedObject(script).FindProperty("weaponBone").objectReferenceValue;
            Camera = player.GetComponentInChildren<Camera>(true).transform;
            foreach (Transform t in Arms.GetComponentsInChildren<Transform>(true))
                _bones[t.name] = t;

            GameObject weapon = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{Pack}/Prefabs/{entry.PackWeapon}.prefab"), WeaponBone, false);
            Weapon = weapon.transform;
            WeaponAnimated = weapon.GetComponentInChildren<Animator>(true).gameObject;
            MonoBehaviour weaponScript = weapon.GetComponents<MonoBehaviour>().First(m => m != null && m.GetType().Name == "FPSWeapon");
            Object settings = new SerializedObject(weaponScript).FindProperty("weaponSettings").objectReferenceValue;
            RotationOffset = new SerializedObject(settings).FindProperty("rotationOffset").quaternionValue;

            Character = LoadClip($"{Pack}/Animations/{entry.Folder}/{characterClip}");
            WeaponClip = LoadClip($"{Pack}/Animations/{entry.Folder}/{weaponClip}");
        }

        public Transform Bone(string name) => _bones.TryGetValue(name, out Transform bone) ? bone : null;
        public Transform Part(string name) => WeaponAnimated.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        public void Sample(float time)
        {
            Character.SampleAnimation(Arms.gameObject, time);
            // The pack turns the weapon bone by its settings' offset at runtime (FPSProceduralJob).
            WeaponBone.rotation *= RotationOffset;
            WeaponClip.SampleAnimation(WeaponAnimated, Mathf.Min(time, WeaponClip.length));
        }

        public void Dispose() => EditorSceneManager.ClosePreviewScene(Scene);
    }

    static AnimationClip LoadClip(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));

    public static string Describe(string gun)
    {
        Entry entry = Table[gun];
        using var source = new Source(entry, entry.Reload, entry.WeaponReload);
        var sb = new StringBuilder();
        sb.AppendLine($"weaponBone={source.WeaponBone.name} parent={source.WeaponBone.parent.name} offset={source.RotationOffset.eulerAngles}");
        sb.AppendLine($"character={source.Character.name} {source.Character.length:0.###}s  weapon={source.WeaponClip.name} {source.WeaponClip.length:0.###}s");
        sb.AppendLine("weapon parts: " + string.Join(", ", source.WeaponAnimated.GetComponentsInChildren<Transform>(true).Select(t => t.name)));
        Transform hand = source.Bone("hand_l"), mag = source.Part("Mag"), charge = source.Part("Charge");
        for (int i = 0; i <= 16; i++)
        {
            float time = source.Character.length * i / 16f;
            source.Sample(time);
            Vector3 inWeapon = source.Weapon.InverseTransformPoint(hand.position);
            Vector3 magInWeapon = source.Weapon.InverseTransformPoint(mag.position);
            Vector3 chargeInWeapon = source.Weapon.InverseTransformPoint(charge.position);
            Vector3 weaponInCamera = source.Camera.InverseTransformPoint(source.Weapon.position);
            sb.AppendLine($"t={time:0.00} handL={inWeapon:F3} mag={magInWeapon:F3} handToMag={Vector3.Distance(hand.position, mag.position):0.000} charge={chargeInWeapon:F3} weaponInCam={weaponInCamera:F3} rot={(Quaternion.Inverse(source.Camera.rotation) * source.Weapon.rotation).eulerAngles:F0}");
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------------------------------------------------------
    // Bake

    const string HandSetPath = "Assets/_Game/Data/Hands/HandSet.asset";
    const float KeyRate = 30f;
    static readonly string[] FingerBones = { "thumb", "index", "middle", "ring", "pinky" };
    static readonly string[] ShapeFingers = { "Thumb", "Index", "Middle", "Ring", "Little" };

    // A hand's grip frame and palm axes, read off its bones the same way on the pack's hand and on ours
    // (ArmSetup places our GripPoint by this rule, so a grip frame here is a HandGrip pose there).
    sealed class Palm
    {
        public Transform Hand, Index, Middle, Pinky;
        public float Sign = 1f;
        public Vector3 Position, Along, Across, Back;
        public Quaternion Rotation;

        public Palm(Transform hand, Func<string, Transform> bone, string suffix)
        {
            Hand = hand;
            Index = bone("index_01" + suffix);
            Middle = bone("middle_01" + suffix);
            Pinky = bone("pinky_01" + suffix);
            Update();
            // Fingers rest slightly curled: the curl points to the palm side.
            Vector3 curl = bone("middle_03" + suffix).position - bone("middle_02" + suffix).position;
            if (Vector3.Dot(Vector3.ProjectOnPlane(curl, Along), Back) > 0f)
                Sign = -1f;
            Update();
        }

        public void Update()
        {
            Along = (Middle.position - Hand.position).normalized;
            Across = Vector3.ProjectOnPlane(Index.position - Pinky.position, Along).normalized;
            Back = Vector3.Cross(Across, Along) * Sign;
            Vector3 knuckles = (Index.position + Pinky.position) * 0.5f;
            Position = Vector3.Lerp(Hand.position, knuckles, 0.82f) - Back * (0.275f * Vector3.Distance(Middle.position, Hand.position));
            Vector3 handle = (Across + Along * 0.25f).normalized;
            Rotation = Quaternion.LookRotation(handle, Vector3.ProjectOnPlane(Back, handle).normalized);
        }

        // A world direction as (along, across, back) parts.
        public Vector3 Local(Vector3 direction) => new(Vector3.Dot(direction, Along), Vector3.Dot(direction, Across), Vector3.Dot(direction, Back));
    }

    static Vector3[] FingerDirections(Palm palm, Transform j1, Transform j2, Transform j3)
    {
        return new[]
        {
            palm.Local((j2.position - j1.position).normalized),
            palm.Local((j3.position - j2.position).normalized),
            palm.Local(j3.TransformDirection(j3.localPosition.normalized)),
        };
    }

    public static string Bake(string gun)
    {
        var report = new StringBuilder();
        report.AppendLine(ResetHandScale(gun));
        report.AppendLine(BakeClip(gun, false, true));
        report.AppendLine(BakeClip(gun, true, false));
        return report.ToString();
    }

    // The arms carry their own size (ArmSetup.ArmScale): a per-gun hand scale left over from the smaller floating
    // hands would enlarge them again, so a baked gun goes back to 1.
    static string ResetHandScale(string gun)
    {
        string prefabPath = $"Assets/_Game/Content/Weapons/{gun}/{gun}.prefab";
        GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var hands = new SerializedObject(contents.GetComponent<ItemHands>());
            SerializedProperty scale = hands.FindProperty("_handScale");
            float was = scale.floatValue;
            if (Mathf.Approximately(was, 1f))
                return "hand scale 1";
            scale.floatValue = 1f;
            hands.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            return $"hand scale {was:0.##} -> 1";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(contents);
        }
    }

    static string BakeClip(string gun, bool empty, bool writeIdle)
    {
        Entry entry = Table[gun];
        string folder = $"Assets/_Game/Content/Weapons/{gun}";
        string prefabPath = $"{folder}/{gun}.prefab";
        string clipPath = $"{folder}/Animations/{gun}_{(empty ? "ReloadingEmpty" : "Reloading")}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
            return "no clip at " + clipPath;
        var report = new StringBuilder(clip.name + ": ");

        using var source = new Source(entry, empty ? entry.ReloadEmpty : entry.Reload, empty ? entry.WeaponReloadEmpty : entry.WeaponReload);
        GameObject item = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
        SceneManager.MoveGameObjectToScene(item, source.Scene);
        Transform root = item.GetComponentInChildren<GunAnimation>(true).transform;
        Transform motion = root.Find("Motion"), weapon = motion.Find("Weapon");
        Transform magazine = weapon.Find("Magazine"), bolt = weapon.Find("Bolt");
        Transform leftFree = motion.Find("Controls/LeftFree"), leftTarget = motion.Find("HandTargets/Left"), rightTarget = motion.Find("HandTargets/Right");
        var constraint = root.Find("Rig/LeftHand").GetComponent<MultiParentConstraint>();
        WeightedTransformArray contacts = constraint.data.sourceObjects;
        Transform forend = contacts[0].transform, boltContact = contacts.Count > 3 ? contacts[3].transform : null;

        var set = AssetDatabase.LoadAssetAtPath<HandSet>(HandSetPath);
        HandRig rig = Object.Instantiate(set.Get(HandSide.Left));
        SceneManager.MoveGameObjectToScene(rig.gameObject, source.Scene);
        rig.transform.localScale *= item.GetComponent<ItemHands>().HandScale;
        var ourBones = new Dictionary<string, Transform>();
        foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
            ourBones[t.name] = t;
        var ourPalm = new Palm(ourBones["hand_l"], n => ourBones[n], "_l");

        // Source at rest (the clip starts on the pack's idle hold).
        var leftPalm = new Palm(source.Bone("hand_l"), source.Bone, "_l");
        var rightPalm = new Palm(source.Bone("hand_r"), source.Bone, "_r");
        source.Sample(0f);
        leftPalm.Update();
        rightPalm.Update();
        Vector3 SourceIn(Vector3 world) => Quaternion.Inverse(source.Weapon.rotation) * (world - source.Weapon.position);
        Vector3 OursIn(Vector3 world) => Quaternion.Inverse(weapon.rotation) * (world - weapon.position);

        var weaponSkin = source.WeaponAnimated.GetComponentInChildren<SkinnedMeshRenderer>(true);
        Bounds sourceMag = PartBounds(weaponSkin, "Mag", source.Weapon);
        Bounds sourceGun = PartBounds(weaponSkin, null, source.Weapon);
        Bounds ourMag = magazine.GetComponent<Renderer>().bounds;
        Vector3 ourMagCentre = OursIn(ourMag.center);
        Vector3 gripS = SourceIn(rightPalm.Position), gripO = OursIn(rightTarget.position);
        float scaleMag = (ourMagCentre - gripO).magnitude / (sourceMag.center - gripS).magnitude;
        float scaleMuzzle = (OursIn(weapon.Find("Muzzle").position).z - gripO.z) / (sourceGun.max.z - gripS.z);
        float scaleSize = ourMag.size.y / sourceMag.size.y;
        // The right hand holds the gun: fit our gun to the pack's at the pistol grip, sized by the middle of three measures
        // (grip to magazine, grip to muzzle, magazine height); the contacts' own offsets take up what's left.
        float handScale = Vector3.Distance(ourPalm.Middle.position, ourPalm.Hand.position) / Vector3.Distance(leftPalm.Middle.position, leftPalm.Hand.position);
        var measures = new[] { scaleMag, scaleMuzzle, scaleSize };
        Array.Sort(measures);
        float scale = entry.Scale > 0f ? entry.Scale : measures[1];
        Vector3 offset = gripO - scale * gripS;
        Vector3 Map(Vector3 sourcePoint, Vector3 extra) => scale * sourcePoint + offset + extra;
        report.Append($"scale {scale:0.00} (mag {scaleMag:0.00} muzzle {scaleMuzzle:0.00} magSize {scaleSize:0.00} hand {handScale:0.00}); ");

        // Sample every key.
        int count = Mathf.CeilToInt(source.Character.length * KeyRate) + 1;
        var times = new float[count];
        var gripPosition = new Vector3[count];
        var gripRotation = new Quaternion[count];
        var magPosition = new Vector3[count];
        var magRotation = new Quaternion[count];
        var magCentre = new Vector3[count];
        var chargePosition = new Vector3[count];
        var gunPosition = new Vector3[count];
        var gunRotation = new Quaternion[count];
        var fingers = new Vector3[count][][];
        Transform magBone = source.Part("Mag"), chargeBone = source.Part("Charge");
        Vector3 magCentreInBone = Vector3.zero;
        for (int k = 0; k < count; k++)
        {
            times[k] = Mathf.Min(k / KeyRate, source.Character.length);
            source.Sample(times[k]);
            leftPalm.Update();
            gripPosition[k] = SourceIn(leftPalm.Position);
            gripRotation[k] = Quaternion.Inverse(source.Weapon.rotation) * leftPalm.Rotation;
            magPosition[k] = SourceIn(magBone.position);
            magRotation[k] = Quaternion.Inverse(source.Weapon.rotation) * magBone.rotation;
            if (k == 0)
                magCentreInBone = Quaternion.Inverse(magRotation[0]) * (sourceMag.center - magPosition[0]);
            magCentre[k] = magPosition[k] + magRotation[k] * magCentreInBone;
            chargePosition[k] = chargeBone != null ? SourceIn(chargeBone.position) : Vector3.zero;
            gunPosition[k] = source.Camera.InverseTransformPoint(source.Weapon.position);
            gunRotation[k] = Quaternion.Inverse(source.Camera.rotation) * source.Weapon.rotation;
            fingers[k] = new Vector3[5][];
            for (int f = 0; f < 5; f++)
                fingers[k][f] = FingerDirections(leftPalm, source.Bone(FingerBones[f] + "_01_l"), source.Bone(FingerBones[f] + "_02_l"), source.Bone(FingerBones[f] + "_03_l"));
        }

        // How much the hand is on each contact, per key.
        var onForend = new float[count];
        var onMag = new float[count];
        var onBolt = new float[count];
        int boltKey = 0;
        float boltNearest = float.MaxValue;
        for (int k = 0; k < count; k++)
        {
            onForend[k] = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.02f, 0.1f, (gripPosition[k] - gripPosition[0]).magnitude));
            int a = Mathf.Max(k - 1, 0), b = Mathf.Min(k + 1, count - 1);
            Vector3 relA = Quaternion.Inverse(magRotation[a]) * (gripPosition[a] - magPosition[a]);
            Vector3 relB = Quaternion.Inverse(magRotation[b]) * (gripPosition[b] - magPosition[b]);
            float slip = (relB - relA).magnitude * KeyRate / Mathf.Max(b - a, 1);
            bool moved = (magPosition[k] - magPosition[0]).magnitude > 0.004f;
            onMag[k] = (moved || slip < 0.12f) && slip < 0.35f && (gripPosition[k] - magCentre[k]).magnitude < 0.16f ? 1f : 0f;
            float toCharge = (gripPosition[k] - chargePosition[k]).magnitude;
            onBolt[k] = empty && chargeBone != null ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.08f, 0.16f, toCharge)) : 0f;
            if (toCharge < boltNearest)
            {
                boltNearest = toCharge;
                boltKey = k;
            }
        }
        onMag = Blur(onMag, 3);

        Vector3 magResidual = ourMagCentre - Map(sourceMag.center, Vector3.zero) + entry.MagazineOffset;
        Vector3 boltResidual = empty && boltContact != null && boltNearest < 0.12f
            ? OursIn(boltContact.position) - Map(gripPosition[boltKey], Vector3.zero) + entry.BoltOffset
            : Vector3.zero;
        report.Append($"mag residual {magResidual:F3} bolt residual {boltResidual:F3} (nearest {boltNearest:0.000} at {times[boltKey]:0.00}s); ");

        // Our tracks.
        var handPosition = new Vector3[count];
        var motionPosition = new Vector3[count];
        var motionRotation = new Quaternion[count];
        var ourMagPosition = new Vector3[count];
        var ourMagRotation = new Quaternion[count];
        var boltTravel = new float[count];
        float chargeMax = 0f;
        for (int k = 0; k < count; k++)
            chargeMax = Mathf.Max(chargeMax, (chargePosition[k] - chargePosition[0]).magnitude);
        Vector3 magRestPosition = magazine.localPosition;
        Quaternion magRestRotation = magazine.localRotation;
        Vector3 magOffset = offset + magResidual;
        int swapKey = 0;
        float farthest = 0f;
        bool released = false;
        for (int k = 0; k < count; k++)
        {
            float total = onForend[k] + onMag[k] + onBolt[k];
            float norm = total > 1f ? 1f / total : 1f;
            Vector3 extra = (entry.ForendOffset * onForend[k] + magResidual * onMag[k] + boltResidual * onBolt[k]) * norm;
            handPosition[k] = Map(gripPosition[k], extra);

            float progress = k / (count - 1f);
            float present = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.14f, progress)) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 0.86f, progress));
            Quaternion turn = Quaternion.Inverse(gunRotation[0]) * gunRotation[k];
            Vector3 move = Quaternion.Inverse(gunRotation[0]) * (gunPosition[k] - gunPosition[0]);
            motionRotation[k] = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(entry.PresentEuler), present) * turn;
            motionPosition[k] = scale * move + offset - turn * offset + entry.PresentPosition * present;

            Quaternion magTurn = magRotation[k] * Quaternion.Inverse(magRotation[0]);
            Vector3 magMove = magPosition[k] - magTurn * magPosition[0];
            ourMagRotation[k] = magTurn * magRestRotation;
            ourMagPosition[k] = magTurn * magRestPosition + scale * magMove + magOffset - magTurn * magOffset;
            // The old magazine is swapped for the new one at its far point, or as soon as the hand lets it fly.
            float away = (ourMagPosition[k] - magRestPosition).magnitude;
            bool thrown = (magPosition[k] - magPosition[0]).magnitude > 0.05f && (gripPosition[k] - magCentre[k]).magnitude > 0.22f;
            if (!released && (away > farthest || thrown))
            {
                farthest = away;
                swapKey = k;
                released = thrown;
            }
            boltTravel[k] = chargeMax > 0.005f ? (chargePosition[k] - chargePosition[0]).magnitude / chargeMax : 0f;
        }
        report.Append($"magazine swap at {swapKey / (count - 1f):0.00}, travel {farthest:0.000} m; ");

        // Fingers: the shape whose bones point most like the pack's, finger by finger.
        var shapes = new FingerShape[count][];
        var joints = new Transform[5][];
        for (int f = 0; f < 5; f++)
            joints[f] = new[] { ourBones[FingerBones[f] + "_01_l"], ourBones[FingerBones[f] + "_02_l"], ourBones[FingerBones[f] + "_03_l"] };
        float fingerError = 0f;
        for (int k = 0; k < count; k++)
        {
            shapes[k] = new FingerShape[5];
            for (int f = 0; f < 5; f++)
            {
                shapes[k][f] = FitFinger(rig, ourPalm, f, joints[f], fingers[k][f], out float error);
                fingerError += error;
            }
        }
        for (int f = 0; f < 5; f++)
            SmoothShapes(shapes, f);
        report.Append($"finger fit error {fingerError / (count * 5):0.000}; ");

        // The bolt's travel on our gun, read off the clip being replaced.
        float boltBack = 0.085f;
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path == "Motion/Weapon/Bolt" && binding.propertyName == "m_LocalPosition.z")
            {
                Keyframe[] keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
                float travel = bolt.localPosition.z - keys.Min(key => key.value);
                if (travel > 0.01f)
                    boltBack = travel;
            }
        }

        // The idle hold: the forend contact and its finger shape become the pack's first frame. A clip that doesn't set
        // it (the empty reload starts a hair off the tactical one) is eased onto the hold already in the prefab.
        Vector3 idlePosition = writeIdle ? handPosition[0] : OursIn(forend.position);
        Quaternion idleRotation = writeIdle ? gripRotation[0] : Quaternion.Inverse(weapon.rotation) * forend.rotation;
        HandShape idleHold = leftTarget.GetComponent<HandGrip>().Shape;
        FingerShape[] idleShape = writeIdle ? (FingerShape[])shapes[0].Clone() : new[] { idleHold[0], idleHold[1], idleHold[2], idleHold[3], idleHold[4] };
        if (writeIdle)
        {
            // Our handguard isn't the pack's: press the palm onto it and close the fingers until they touch (HandFit).
            leftTarget.SetPositionAndRotation(weapon.TransformPoint(idlePosition), weapon.rotation * idleRotation);
            float palm = FitToGun(leftTarget.GetComponent<HandGrip>(), rig, idleShape);
            idlePosition += idleRotation * Vector3.up * palm;
            report.Append($"idle hold fitted (palm {palm * 1000f:0} mm); ");
        }
        const int pinKeys = 6;
        Vector3 startMove = idlePosition - handPosition[0], endMove = idlePosition - handPosition[count - 1];
        Quaternion startTurn = idleRotation * Quaternion.Inverse(gripRotation[0]), endTurn = idleRotation * Quaternion.Inverse(gripRotation[count - 1]);
        FingerShape[] startShape = shapes[0], endShape = shapes[count - 1];
        for (int k = 0; k < count; k++)
        {
            float start = 1f - Mathf.SmoothStep(0f, 1f, k / (float)pinKeys);
            float end = Mathf.SmoothStep(0f, 1f, (k - (count - 1 - pinKeys)) / (float)pinKeys);
            handPosition[k] += startMove * start + endMove * end;
            gripRotation[k] = Quaternion.Slerp(Quaternion.identity, startTurn, start) * Quaternion.Slerp(Quaternion.identity, endTurn, end) * gripRotation[k];
            var pinned = new FingerShape[5];
            for (int f = 0; f < 5; f++)
            {
                FingerShape shape = shapes[k][f];
                if (start > 0f)
                    shape = Blend(shape, startShape[f], idleShape[f], start);
                if (end > 0f)
                    shape = Blend(shape, endShape[f], idleShape[f], end);
                pinned[f] = shape;
            }
            shapes[k] = pinned;
        }
        if (writeIdle)
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Transform cRoot = contents.GetComponentInChildren<GunAnimation>(true).transform;
                Transform cWeapon = cRoot.Find("Motion/Weapon");
                Transform cForend = cRoot.Find("Rig/LeftHand").GetComponent<MultiParentConstraint>().data.sourceObjects[0].transform;
                foreach (Transform t in new[] { cForend, cRoot.Find("Motion/Controls/LeftFree"), cRoot.Find("Motion/HandTargets/Left") })
                    t.SetPositionAndRotation(cWeapon.TransformPoint(idlePosition), cWeapon.rotation * idleRotation);
                var grip = cRoot.Find("Motion/HandTargets/Left").GetComponent<HandGrip>();
                grip.SetAnimatedShape(ToShape(idleShape));
                EditorUtility.SetDirty(grip);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
        // The instance here follows, so the clip baselines read the rest values now in the prefab.
        foreach (Transform t in new[] { forend, leftFree, leftTarget })
            t.SetPositionAndRotation(weapon.TransformPoint(idlePosition), weapon.rotation * idleRotation);
        leftTarget.GetComponent<HandGrip>().SetAnimatedShape(ToShape(idleShape));

        // Curves: every binding the old clip had gets a baseline at the rest value, then the tracks go on top.
        var curves = new Dictionary<EditorCurveBinding, AnimationCurve>();
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (AnimationUtility.GetFloatValue(root.gameObject, binding, out float rest))
                curves[binding] = Constant(rest, times[count - 1]);
        }
        void Track(string path, Type type, string property, Func<int, float> value, bool stepped = false)
        {
            var curve = new AnimationCurve();
            for (int k = 0; k < count; k++)
                curve.AddKey(new Keyframe(times[k], value(k)));
            for (int k = 0; k < count; k++)
            {
                if (stepped)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
                    AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
                }
                else
                    curve.SmoothTangents(k, 0f);
            }
            curves[EditorCurveBinding.FloatCurve(path, type, property)] = curve;
        }
        void Pose(string path, Vector3[] positions, Quaternion[] rotations)
        {
            for (int k = 1; k < count; k++)
            {
                if (Quaternion.Dot(rotations[k - 1], rotations[k]) < 0f)
                    rotations[k] = new Quaternion(-rotations[k].x, -rotations[k].y, -rotations[k].z, -rotations[k].w);
            }
            Track(path, typeof(Transform), "m_LocalPosition.x", k => positions[k].x);
            Track(path, typeof(Transform), "m_LocalPosition.y", k => positions[k].y);
            Track(path, typeof(Transform), "m_LocalPosition.z", k => positions[k].z);
            Track(path, typeof(Transform), "m_LocalRotation.x", k => rotations[k].x);
            Track(path, typeof(Transform), "m_LocalRotation.y", k => rotations[k].y);
            Track(path, typeof(Transform), "m_LocalRotation.z", k => rotations[k].z);
            Track(path, typeof(Transform), "m_LocalRotation.w", k => rotations[k].w);
        }

        Pose("Motion", motionPosition, motionRotation);
        Pose("Motion/Controls/LeftFree", handPosition, gripRotation);

        // The magazine in the gun rides out with the hand and hides at its far point (GunAnimation drops the pooled
        // copy there); the replacement takes over from that point and seats.
        var oldPosition = new Vector3[count];
        var oldRotation = new Quaternion[count];
        var newPosition = new Vector3[count];
        var newRotation = new Quaternion[count];
        for (int k = 0; k < count; k++)
        {
            int held = Mathf.Min(k, swapKey);
            oldPosition[k] = k == count - 1 ? magRestPosition : ourMagPosition[held];
            oldRotation[k] = k == count - 1 ? magRestRotation : ourMagRotation[held];
            newPosition[k] = ourMagPosition[Mathf.Max(k, swapKey)];
            newRotation[k] = ourMagRotation[Mathf.Max(k, swapKey)];
        }
        newPosition[count - 1] = magRestPosition;
        newRotation[count - 1] = magRestRotation;
        Pose("Motion/Weapon/Magazine", oldPosition, oldRotation);
        Pose("Motion/Weapon/ReplacementMagazine", newPosition, newRotation);
        Track("Motion/Weapon/Magazine", typeof(MeshRenderer), "m_Enabled", k => k < swapKey ? 1f : 0f, true);
        Track("Motion/Weapon/ReplacementMagazine", typeof(MeshRenderer), "m_Enabled", k => k < swapKey ? 0f : 1f, true);
        if (empty)
            Track("Motion/Weapon/Bolt", typeof(Transform), "m_LocalPosition.z", k => bolt.localPosition.z - boltTravel[k] * boltBack);

        for (int i = 0; i < 5; i++)
        {
            int index = i;
            Track("Rig/LeftHand", typeof(MultiParentConstraint), $"m_Data.m_SourceObjects.m_Item{index}.weight", k => index == 4 ? 1f : 0f, true);
        }
        for (int f = 0; f < 5; f++)
        {
            int finger = f;
            Track("Motion/HandTargets/Left", typeof(HandGrip), $"_animatedShape.{ShapeFingers[f]}.Curl", k => shapes[k][finger].Curl);
            Track("Motion/HandTargets/Left", typeof(HandGrip), $"_animatedShape.{ShapeFingers[f]}.Spread", k => shapes[k][finger].Spread);
            Track("Motion/HandTargets/Left", typeof(HandGrip), $"_animatedShape.{ShapeFingers[f]}.Tip", k => shapes[k][finger].Tip);
        }

        clip.ClearCurves();
        foreach (KeyValuePair<EditorCurveBinding, AnimationCurve> pair in curves)
            AnimationUtility.SetEditorCurve(clip, pair.Key, pair.Value);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssetIfDirty(clip);

        if (writeIdle)
            report.Append(Rebase(gun, root.gameObject));
        report.Append($"{count} keys, {curves.Count} curves, {clip.length:0.00}s");
        return report.ToString();
    }

    // The other clips hold the left hand's free control and finger shape at the old rest values: move the still ones
    // to the new rest (clips that animate them are left alone).
    static string Rebase(string gun, GameObject root)
    {
        int changed = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { $"Assets/_Game/Content/Weapons/{gun}/Animations" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.Contains("Reloading"))
                continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            bool dirty = false;
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.path != "Motion/Controls/LeftFree" && binding.path != "Motion/HandTargets/Left")
                    continue;
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
                float first = curve.keys[0].value;
                if (curve.keys.Any(key => Mathf.Abs(key.value - first) > 1e-4f) || !AnimationUtility.GetFloatValue(root, binding, out float rest))
                    continue;
                AnimationUtility.SetEditorCurve(clip, binding, Constant(rest, clip.length));
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssetIfDirty(clip);
                changed++;
            }
        }
        return $"rebased {changed} other clips; ";
    }

    static FingerShape FitFinger(HandRig rig, Palm palm, int finger, Transform[] joints, Vector3[] target, out float best)
    {
        best = float.MaxValue;
        var result = new FingerShape();
        float Error(FingerShape shape)
        {
            rig.ApplyFinger(finger, shape);
            Vector3[] ours = FingerDirections(palm, joints[0], joints[1], joints[2]);
            return (1f - Vector3.Dot(ours[0], target[0])) + 0.8f * (1f - Vector3.Dot(ours[1], target[1])) + 0.5f * (1f - Vector3.Dot(ours[2], target[2]));
        }
        for (float curl = 0f; curl <= FingerShape.MaxCurl + 0.01f; curl += 0.15f)
        for (float spread = -1f; spread <= 1.01f; spread += 0.5f)
        for (float tip = -1f; tip <= 1.01f; tip += 0.5f)
        {
            var shape = new FingerShape(curl, spread, tip);
            float error = Error(shape);
            if (error < best)
            {
                best = error;
                result = shape;
            }
        }
        float step = 0.075f, spreadStep = 0.25f;
        for (int pass = 0; pass < 4; pass++, step *= 0.5f, spreadStep *= 0.5f)
        {
            FingerShape centre = result;
            for (int c = -1; c <= 1; c++)
            for (int s = -1; s <= 1; s++)
            for (int t = -1; t <= 1; t++)
            {
                var shape = new FingerShape(Mathf.Clamp(centre.Curl + c * step, 0f, FingerShape.MaxCurl),
                    Mathf.Clamp(centre.Spread + s * spreadStep, -1f, 1f), Mathf.Clamp(centre.Tip + t * spreadStep, -1f, 1f));
                float error = Error(shape);
                if (error < best)
                {
                    best = error;
                    result = shape;
                }
            }
        }
        return result;
    }

    static void SmoothShapes(FingerShape[][] shapes, int finger)
    {
        int count = shapes.Length;
        var smooth = new FingerShape[count];
        for (int k = 0; k < count; k++)
        {
            FingerShape a = shapes[Mathf.Max(k - 1, 0)][finger], b = shapes[k][finger], c = shapes[Mathf.Min(k + 1, count - 1)][finger];
            smooth[k] = new FingerShape((a.Curl + 2f * b.Curl + c.Curl) / 4f, (a.Spread + 2f * b.Spread + c.Spread) / 4f, (a.Tip + 2f * b.Tip + c.Tip) / 4f);
        }
        for (int k = 0; k < count; k++)
            shapes[k][finger] = smooth[k];
    }

    // The project's own grip fit (HandFit, editor-only and internal) on one grip, from a given finger shape: returns
    // the palm's move along the back of the hand and writes the fitted fingers into `fingers`.
    static float FitToGun(HandGrip grip, HandRig rig, FingerShape[] fingers)
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        Type fit = typeof(HandGrip).Assembly.GetType("MB13.Aipocalypse.Core.HandSystem.HandFit", true);
        object meshes = fit.GetMethod("Collect", flags).Invoke(null, new object[] { grip });
        var samples = new List<Vector3>();
        HandShape pose = ToShape(fingers);
        Vector3 elbow = grip.transform.position + Vector3.down;
        float palm = (float)fit.GetMethod("FitPalm", flags).Invoke(null, new object[] { grip, rig, elbow, pose, meshes, samples });
        grip.Resolve(rig, elbow, palm, out Vector3 position, out Quaternion rotation);
        rig.Place(position, rotation);
        rig.Apply(pose);
        for (int f = 0; f < fingers.Length; f++)
            fingers[f] = (FingerShape)fit.GetMethod("FitFinger", flags).Invoke(null, new object[] { rig, f, fingers[f], meshes, samples });
        return palm;
    }

    // `shape` moved by the step from `from` to `to`, weighted: pins a track's end onto a given value without a pop.
    static FingerShape Blend(FingerShape shape, FingerShape from, FingerShape to, float weight) =>
        new(shape.Curl + (to.Curl - from.Curl) * weight, shape.Spread + (to.Spread - from.Spread) * weight, shape.Tip + (to.Tip - from.Tip) * weight);

    static HandShape ToShape(FingerShape[] fingers) =>
        new() { Thumb = fingers[0], Index = fingers[1], Middle = fingers[2], Ring = fingers[3], Little = fingers[4] };

    static AnimationCurve Constant(float value, float length)
    {
        var curve = new AnimationCurve(new Keyframe(0f, value), new Keyframe(length, value));
        for (int k = 0; k < 2; k++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
            AnimationUtility.SetKeyRightTangentMode(curve, k, AnimationUtility.TangentMode.Constant);
        }
        return curve;
    }

    static float[] Blur(float[] values, int radius)
    {
        var result = new float[values.Length];
        for (int k = 0; k < values.Length; k++)
        {
            float sum = 0f;
            int n = 0;
            for (int i = Mathf.Max(k - radius, 0); i <= Mathf.Min(k + radius, values.Length - 1); i++, n++)
                sum += values[i];
            result[k] = sum / n;
        }
        return result;
    }

    // Bounds, in `space` (no scale), of the skinned vertices that follow one bone (null = all of them).
    static Bounds PartBounds(SkinnedMeshRenderer skin, string boneName, Transform space)
    {
        var baked = new Mesh();
        skin.BakeMesh(baked, false);
        Vector3[] vertices = baked.vertices;
        BoneWeight[] weights = skin.sharedMesh.boneWeights;
        int bone = boneName == null ? -1 : Array.FindIndex(skin.bones, b => b != null && b.name == boneName);
        Matrix4x4 toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
        var bounds = new Bounds();
        bool any = false;
        for (int i = 0; i < vertices.Length; i++)
        {
            if (bone >= 0 && weights[i].boneIndex0 != bone)
                continue;
            Vector3 point = Quaternion.Inverse(space.rotation) * (toWorld.MultiplyPoint3x4(vertices[i]) - space.position);
            if (!any)
                bounds = new Bounds(point, Vector3.zero);
            else
                bounds.Encapsulate(point);
            any = true;
        }
        Object.DestroyImmediate(baked);
        return bounds;
    }

    public static string SourceStrip(string gun, string clip, int frames, string view)
    {
        Entry entry = Table[gun];
        bool empty = clip == "ReloadingEmpty";
        using var source = new Source(entry, clip == "Idle" ? entry.Idle : empty ? entry.ReloadEmpty : entry.Reload,
            clip == "Idle" ? entry.WeaponIdle : empty ? entry.WeaponReloadEmpty : entry.WeaponReload);
        foreach (Camera other in source.Arms.root.GetComponentsInChildren<Camera>(true))
            other.enabled = false;
        var lamp = new GameObject("Light", typeof(Light));
        SceneManager.MoveGameObjectToScene(lamp, source.Scene);
        lamp.GetComponent<Light>().type = LightType.Directional;
        lamp.GetComponent<Light>().intensity = 1.4f;
        lamp.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        var go = new GameObject("ReviewCamera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(go, source.Scene);
        var camera = go.GetComponent<Camera>();
        camera.scene = source.Scene;
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.01f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.32f, 0.36f, 0.4f);
        camera.transform.SetPositionAndRotation(source.Camera.position, source.Camera.rotation);
        // "left" / "below": from outside the eye, to see the hands' work the pack keeps low in the frame.
        Vector3 focus = source.Camera.TransformPoint(new Vector3(0.05f, -0.2f, 0.3f));
        if (view == "left")
            camera.transform.SetPositionAndRotation(focus - source.Camera.right * 0.9f + source.Camera.up * 0.1f, Quaternion.LookRotation(source.Camera.right - source.Camera.up * 0.1f, source.Camera.up));
        if (view == "below")
            camera.transform.SetPositionAndRotation(focus - source.Camera.forward * 0.5f - source.Camera.up * 0.25f, Quaternion.LookRotation(source.Camera.forward + source.Camera.up * 0.35f, source.Camera.up));

        int columns = Mathf.Min(frames, 4), rows = Mathf.CeilToInt(frames / (float)columns);
        const int width = 480, height = 270;
        var image = new Texture2D(columns * width, rows * height, TextureFormat.RGB24, false);
        for (int i = 0; i < frames; i++)
        {
            source.Sample(source.Character.length * (frames > 1 ? i / (float)(frames - 1) : 0f));
            var target = new RenderTexture(width, height, 24);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), i % columns * width, (rows - 1 - i / columns) * height);
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
        }
        image.Apply();
        Directory.CreateDirectory(OutFolder);
        string path = $"{OutFolder}/Source_{gun}_{clip}_{view}.png";
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        return Path.GetFullPath(path);
    }
}
