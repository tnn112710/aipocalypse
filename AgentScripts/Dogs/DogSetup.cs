using System;
using System.Collections.Generic;
using System.Linq;
using MB13.Aipocalypse.Core.DogSystem;
using MB13.Aipocalypse.Core.SaveSystem;
using MB13.Aipocalypse.Editor.Localization;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Object = UnityEngine.Object;

// Builds the companion dog: Data/Dogs/DogSettings, a first-party copy of the Malbers Border Collie model (no Malbers
// scripts), in-place copies of its moving clips, its animator controller, and the Dog base prefab with the Loki variant.
// The tree is built apart: TreeKit.Build AgentScripts/Dogs/Dog.tree.txt.
// Run: unity command script run --file AgentScripts/Dogs/DogSetup.cs --entry DogSetup.All
public static class DogSetup
{
    const string Vendor = "Assets/_StoreAssets/Characters/Malbers Animations/Animals Packs/04 Pets/Dog (Border Collie)/";
    const string VendorModel = Vendor + "Model/PolyArt/Border_Collie_Skye_PA.prefab";
    const string VendorAnims = Vendor + "Animations/";
    const string Material = Vendor + "Materials Textures/PolyArt/Black/Dog 4x4 Black 01.mat";
    const string Template = "Assets/_Game/Content/Enemies/WanderCapsule/WanderCapsule.prefab";
    const string Content = "Assets/_Game/Content/Dogs";
    const string ModelDir = Content + "/BorderCollie";
    const string ModelPath = ModelDir + "/Model/BorderCollie_Model.prefab";
    const string ClipDir = ModelDir + "/Animations";
    const string ControllerPath = ModelDir + "/BorderCollie.controller";
    const string BasePath = Content + "/Dog.prefab";
    const string LokiPath = Content + "/Loki/Loki.prefab";
    const string TreePath = Content + "/_Shared/Dog.asset";
    const string DataDir = "Assets/_Game/Data/Dogs";
    const string SettingsPath = DataDir + "/DogSettings.asset";
    const string StatsPath = DataDir + "/DogStatBlock.asset";
    const string StaggerDir = "Assets/_Game/Data/Enemies/Stagger/";

    // The spec's start values (docs/superpowers/specs/2026-11-23-dog-companion-design.md, "Stats").
    static readonly (DogStat Stat, float Value)[] StartStats =
    {
        (DogStat.BiteDamage, 12f), (DogStat.BiteCooldown, 8f), (DogStat.MaxBiteClass, 0f), (DogStat.StaggerTime, 0.9f),
        (DogStat.RetaliationWindow, 2.5f), (DogStat.CircleRadius, 5f), (DogStat.CircleSpeed, 3.5f), (DogStat.FeintChance, 0.25f),
        (DogStat.EscapeSpeed, 7f), (DogStat.DodgeChance, 0.35f), (DogStat.Toughness, 100f), (DogStat.RegenPerSecond, 2f),
        (DogStat.HelpUpToughness, 0.3f), (DogStat.LimpTime, 30f), (DogStat.RetreatAt, 0.25f), (DogStat.EngageRange, 15f),
        (DogStat.SentChaseRange, 30f), (DogStat.SenseRange, 25f), (DogStat.TakedownChanceBase, 0f), (DogStat.TakedownChanceBonus, 0f),
        (DogStat.MaxTakedownClass, 0f), (DogStat.TakedownCooldown, 20f), (DogStat.PinTime, 1.5f),
    };
    const string AnimalLayer = "Animal";
    const string RootBone = "Root";

    // The collie at 2× stands 1.3 m tall (ear tips, idle; 0.66 m at 1×): the user's size, 2026-11-23. Clip ground speeds,
    // the agent, the body collider and the dog house scale with it.
    public const float ModelScale = 2f;
    // A bigger animal steps slower: every moving clip plays at this rate, its blend position (its feet's ground speed)
    // scaled to match, so the feet never slide.
    const float Cadence = 0.85f;
    const float AgentRadius = 0.3f * ModelScale;
    const float AgentHeight = 0.7f * ModelScale;
    // A big dog turns in arcs, not on a pin: Face turns at the max rate, so it stays close to the walking one.
    const float TurnSpeed = 200f;
    const float MaxTurnSpeed = 260f;
    const float OnSpotTurnSpeed = 160f;
    const float TurnSmoothing = 0.2f;
    const float SlowdownTime = 0.9f;
    // Ears and tail lag the body a little (DampedTransform down each chain): 0 none, 1 all damping.
    const float EarDamp = 0.3f;
    const float TailDamp = 0.35f;

    public static string All()
    {
        var log = new List<string> { Data(), Model(), Controller(), Prefabs(), Hud() };
        return string.Join("\n", log);
    }

    // ---- Data ----

    public static string Data()
    {
        Folder(DataDir);
        string imported = LocalizationCsv.ImportAll();
        var settings = AssetDatabase.LoadAssetAtPath<DogSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<DogSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        bool ok = LocalizationKit.Set(settings, "Name", "Creatures", "dog.loki")
            & LocalizationKit.Set(settings, "OrdersVerb", "Interaction", "verb.dog_orders")
            & LocalizationKit.Set(settings, "PetVerb", "Interaction", "verb.dog_pet")
            & LocalizationKit.Set(settings, "CallVerb", "Interaction", "verb.dog_call")
            & LocalizationKit.Set(settings, "FollowLabel", "UI", "wheel.dog_follow")
            & LocalizationKit.Set(settings, "StayLabel", "UI", "wheel.dog_stay")
            & LocalizationKit.Set(settings, "PetLabel", "UI", "wheel.dog_pet")
            & LocalizationKit.Set(settings, "ActiveLabel", "UI", "wheel.dog_active")
            & LocalizationKit.Set(settings, "PassiveLabel", "UI", "wheel.dog_passive")
            & LocalizationKit.Set(settings, "HerdingLabel", "UI", "wheel.dog_herding")
            & LocalizationKit.Set(settings, "HelpUpVerb", "Interaction", "verb.dog_help_up");
        var stats = AssetDatabase.LoadAssetAtPath<DogStatBlock>(StatsPath);
        if (stats == null)
        {
            stats = ScriptableObject.CreateInstance<DogStatBlock>();
            AssetDatabase.CreateAsset(stats, StatsPath);
        }
        foreach ((DogStat stat, float value) in StartStats)
            if (!stats.Has(stat))
                stats.SetBase(stat, value);
        Save(stats);
        settings.Stats = stats;
        settings.Classes = new[] { "Light", "Medium", "Heavy", "Boss" }
            .Select(n => Load<MB13.Aipocalypse.Core.EnemySystem.StaggerProfile>(StaggerDir + n + ".asset")).ToArray();
        Save(settings);
        return $"data: {SettingsPath} (text {(ok ? "ok" : "MISSING ENTRIES")}; csv {imported}), stats {StatsPath}";
    }

    // ---- Model ----

    // A first-party copy of the vendor model with only the look left: no Malbers scripts, colliders or rigidbody, one
    // Animator with no controller and no root motion, on the Animal layer.
    public static string Model()
    {
        Folder(ModelDir + "/Model");
        GameObject root = PrefabUtility.LoadPrefabContents(VendorModel);
        try
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
                if (component is Collider or Rigidbody or AudioSource or MonoBehaviour)
                    Object.DestroyImmediate(component);
            Animator animator = root.GetComponent<Animator>() ?? root.AddComponent<Animator>();
            foreach (Animator other in root.GetComponentsInChildren<Animator>(true))
                if (other != animator)
                    Object.DestroyImmediate(other);
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            var material = Load<Material>(Material);
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            foreach (SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = false;
            int layer = LayerMask.NameToLayer(AnimalLayer);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
            root.name = "BorderCollie_Model";
            Body(root);
            PrefabUtility.SaveAsPrefabAsset(root, ModelPath, out bool saved);
            return saved ? "model: " + ModelPath : "model FAILED";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---- Clips ----

    // The vendor clips move the Root bone forward (root motion the Animator would apply). We move the dog ourselves, so
    // every clip gets a copy with its Root placed by PlaceRoot (a vendor clip played as is drops its Root as root motion
    // and the body lands wherever the Animator's default puts it). `keepRoot`: the Root as authored (the pose chain and
    // the knockdown, where the body shifts over planted paws; they start and end on Standing).
    // Returns the clip to use and the ground speed it was made for.
    // `copyName`: when the same clip is also used the other way (looping or not), so the two copies don't overwrite each other.
    static (AnimationClip Clip, float Speed) Source(string file, string take = null, bool loop = true, bool keepRoot = false,
        string copyName = null)
    {
        string path = VendorAnims + file + ".fbx";
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__") && (take == null || c.name == take))
            ?? throw new Exception($"no clip {take ?? "(first)"} in {path}");
        Vector3 drift = RootDrift(clip);
        Folder(ClipDir);
        string name = copyName ?? (take ?? System.IO.Path.GetFileName(file)).Replace(' ', '_');
        string copyPath = $"{ClipDir}/{name}.anim";
        AnimationClip copy = Object.Instantiate(clip);
        copy.name = name;
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        if (!keepRoot)
            PlaceRoot(copy, loop);
        Reduce(copy);
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(copyPath);
        if (existing == null)
            AssetDatabase.CreateAsset(copy, copyPath);
        else
        {
            EditorUtility.CopySerialized(copy, existing);
            Object.DestroyImmediate(copy);
            copy = existing;
            Save(copy);
        }
        float speed = loop ? new Vector2(drift.x, drift.z).magnitude / clip.length * ModelScale * Cadence : 0f;
        return (copy, speed);
    }

    // The model's body: DogBody (paws planted by its own two-bone solve, panting, and where to look) and a RigBuilder with
    // Look (neck and head aimed at DogBody's target) then Jiggle (ear and tail lag). A unique character, so a RigBuilder is
    // fine (animation-rigging skill).
    static void Body(GameObject model)
    {
        foreach (string name in new[] { "Look", "Jiggle" })
        {
            Transform old = model.transform.Find(name);
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
        }
        foreach (RigBuilder stale in model.GetComponents<RigBuilder>())
            Object.DestroyImmediate(stale);
        Transform[] bones = model.GetComponentsInChildren<Transform>(true);
        Transform Bone(string name) => bones.FirstOrDefault(b => b.name == name) ?? throw new Exception("no bone " + name);

        // Each paw's rest height (its contact bone above the ground, standing): sampled from the idle, the pose put back.
        var saved = bones.Select(b => (b, b.localPosition, b.localRotation, b.localScale)).ToArray();
        Load<AnimationClip>(ClipDir + "/Dog_Idle.anim").SampleAnimation(model, 0.3f);
        float Rest(string contact) => Bone(contact).position.y - model.transform.position.y;
        (string Name, string Root, string Mid, string Tip, string Contact)[] legs =
        {
            ("Front L", "L UpperArm", "L Forearm", "L Hand", "L Finger0"), ("Front R", "R UpperArm", "R Forearm", "R Hand", "R Finger0"),
            ("Hind L", "L Calf", "L HorseLink", "L Foot", "L Toe0"), ("Hind R", "R Calf", "R HorseLink", "R Foot", "R Toe0"),
        };
        float[] rests = legs.Select(l => Rest(l.Contact)).ToArray();
        foreach ((Transform b, Vector3 position, Quaternion rotation, Vector3 scale) in saved)
        {
            b.localPosition = position;
            b.localRotation = rotation;
            b.localScale = scale;
        }

        var legData = legs.Select((l, i) => new DogBody.Leg
        {
            Root = Bone(l.Root), Mid = Bone(l.Mid), Tip = Bone(l.Tip), Contact = Bone(l.Contact), Rest = rests[i],
        }).ToArray();

        // Neck then head, each aimed along its own forward (local +Y on this rig), kept upright to the scene's up.
        Rig lookRig = NewRig(model, "Look");
        Transform head = Bone("Head");
        var lookTarget = new GameObject("Target").transform;
        lookTarget.SetParent(lookRig.transform, false);
        lookTarget.position = head.position + model.transform.forward * 3f;
        (string Bone, float Share)[] aims = { ("Neck2", 0.4f), ("Head", 0.75f) };
        var look = new MultiAimConstraint[aims.Length];
        for (int i = 0; i < aims.Length; i++)
        {
            Transform bone = Bone(aims[i].Bone);
            var go = new GameObject(aims[i].Bone);
            go.transform.SetParent(lookRig.transform, false);
            var aim = go.AddComponent<MultiAimConstraint>();
            aim.weight = 0f;
            aim.data.constrainedObject = bone;
            var sources = new WeightedTransformArray { new WeightedTransform(lookTarget, 1f) };
            aim.data.sourceObjects = sources;
            aim.data.aimAxis = Axis(bone.InverseTransformDirection(model.transform.forward));
            aim.data.upAxis = Axis(bone.InverseTransformDirection(model.transform.up));
            aim.data.worldUpType = MultiAimConstraintData.WorldUpType.SceneUp;
            aim.data.maintainOffset = false;
            aim.data.limits = new Vector2(-60f, 60f);
            aim.data.constrainedXAxis = aim.data.constrainedYAxis = aim.data.constrainedZAxis = true;
            look[i] = aim;
        }

        Rig jiggle = Jiggle(model, Bone);
        var builder = model.AddComponent<RigBuilder>();
        builder.layers.Add(new RigLayer(lookRig, true));
        builder.layers.Add(new RigLayer(jiggle, true));
        var body = model.GetComponent<DogBody>() ?? model.AddComponent<DogBody>();
        body.Setup(legData, look, aims.Select(a => a.Share).ToArray(), lookTarget, head, Bone("Jaw"),
            new[] { Bone("Tongue"), Bone("Tongue02"), Bone("Tongue03") });
    }

    static Rig NewRig(GameObject model, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(model.transform, false);
        return go.AddComponent<Rig>();
    }

    // The bone axis nearest a direction given in the bone's space.
    static MultiAimConstraintData.Axis Axis(Vector3 local)
    {
        Vector3 a = new(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        if (a.x >= a.y && a.x >= a.z)
            return local.x >= 0f ? MultiAimConstraintData.Axis.X : MultiAimConstraintData.Axis.X_NEG;
        if (a.y >= a.z)
            return local.y >= 0f ? MultiAimConstraintData.Axis.Y : MultiAimConstraintData.Axis.Y_NEG;
        return local.z >= 0f ? MultiAimConstraintData.Axis.Z : MultiAimConstraintData.Axis.Z_NEG;
    }

    // Secondary motion: each ear tip and tail bone follows the bone above it with a lag (the constraints run in hierarchy
    // order, root to tip).
    static Rig Jiggle(GameObject model, Func<string, Transform> Bone)
    {
        var rigObject = new GameObject("Jiggle");
        rigObject.transform.SetParent(model.transform, false);
        Rig rig = rigObject.AddComponent<Rig>();
        void Damp(string constrained, string source, float damp)
        {
            var go = new GameObject(constrained);
            go.transform.SetParent(rigObject.transform, false);
            var damped = go.AddComponent<DampedTransform>();
            damped.data.constrainedObject = Bone(constrained);
            damped.data.sourceObject = Bone(source);
            damped.data.dampPosition = damp;
            damped.data.dampRotation = damp;
            damped.data.maintainAim = true;
        }
        Damp("Ear02 L", "Ear01 L", EarDamp);
        Damp("Ear02 R", "Ear01 R", EarDamp);
        for (int i = 2; i <= 6; i++)
            Damp($"Tail{i:00}", $"Tail{i - 1:00}", TailDamp);
        return rig;
    }

    // Where the vendor's standing pose has the Root (sitting down starts here, standing up ends here; the acts stand here):
    // every clip puts the body on this spot over the agent, so nothing slides or jumps between clips.
    static readonly Vector3 Standing = new(0f, 0f, 0.237f);

    // Loops lose their drift and are centred on Standing, their bob kept; one-shots are held there (jumps and ledges: the
    // code moves the body); a clip that doesn't animate the Root gets it pinned there. Height: loops keep theirs, one-shots
    // hold their first frame's.
    static void PlaceRoot(AnimationClip clip, bool loop)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            EditorCurveBinding binding = EditorCurveBinding.FloatCurve(RootBone, typeof(Transform), "m_LocalPosition." + "xyz"[axis]);
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
            float target = Standing[axis];
            if (curve == null || curve.length == 0)
                curve = AnimationCurve.Constant(0f, clip.length, target);
            else if (axis == 1)
                curve = loop ? curve : Held(curve);
            else if (!loop)
                curve = AnimationCurve.Constant(curve.keys[0].time, curve.keys[^1].time, target);
            else
            {
                curve = WithoutDrift(curve);
                curve = Shifted(curve, target - Mean(curve, clip.length));
            }
            AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
    }

    static float Mean(AnimationCurve curve, float length)
    {
        const int Samples = 64;
        float sum = 0f;
        for (int i = 0; i < Samples; i++)
            sum += curve.Evaluate(length * i / Samples);
        return sum / Samples;
    }

    static AnimationCurve Shifted(AnimationCurve curve, float by)
    {
        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
            keys[i].value += by;
        return new AnimationCurve(keys);
    }

    static Vector3 RootDrift(AnimationClip clip)
    {
        float Delta(string axis)
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(RootBone, typeof(Transform), "m_LocalPosition." + axis));
            return curve == null || curve.length < 2 ? 0f : curve.Evaluate(clip.length) - curve.Evaluate(0f);
        }
        return new Vector3(Delta("x"), Delta("y"), Delta("z"));
    }

    static AnimationCurve WithoutDrift(AnimationCurve curve)
    {
        Keyframe[] keys = curve.keys;
        float t0 = keys[0].time, t1 = keys[^1].time;
        float slope = t1 > t0 ? (keys[^1].value - keys[0].value) / (t1 - t0) : 0f;
        for (int i = 0; i < keys.Length; i++)
        {
            keys[i].value -= slope * (keys[i].time - t0);
            keys[i].inTangent -= slope;
            keys[i].outTangent -= slope;
        }
        return new AnimationCurve(keys);
    }

    // The vendor imports these clips uncompressed: a key per bone, channel and frame (a 10 s copy was 194 MB of YAML).
    // A key goes when the curve between its neighbours, drawn from their own values and slopes, stays within these
    // tolerances of the original (checked at every key and halfway between). A bone's x/y/z (and a rotation's w) are
    // thinned together so its keys stay on the same frames.
    const float RotationTolerance = 0.0015f;
    const float PositionTolerance = 0.001f;
    const float ScaleTolerance = 0.001f;

    static void Reduce(AnimationClip clip)
    {
        var bindings = new List<EditorCurveBinding>();
        var curves = new List<AnimationCurve>();
        foreach (IGrouping<(string, Type, string), EditorCurveBinding> group in AnimationUtility.GetCurveBindings(clip)
                     .GroupBy(b => (b.path, b.type, Channel(b.propertyName))))
        {
            EditorCurveBinding[] members = group.ToArray();
            AnimationCurve[] source = members.Select(b => AnimationUtility.GetEditorCurve(clip, b)).ToArray();
            int count = source[0].length;
            float tolerance = group.Key.Item3.Contains("Rotation") ? RotationTolerance
                : group.Key.Item3.Contains("Scale") ? ScaleTolerance : PositionTolerance;
            // Joint only when every member has its keys at the same times.
            bool joint = source.All(c => c.length == count && c.keys.Select(k => k.time).SequenceEqual(source[0].keys.Select(k => k.time)));
            AnimationCurve[][] sets = joint ? new[] { source } : source.Select(c => new[] { c }).ToArray();
            foreach (AnimationCurve[] set in sets)
            {
                List<int> keep = Keep(set, tolerance);
                foreach (AnimationCurve curve in set)
                {
                    bindings.Add(members[Array.IndexOf(source, curve)]);
                    curves.Add(Thinned(curve, keep));
                }
            }
        }
        AnimationUtility.SetEditorCurves(clip, bindings.ToArray(), curves.ToArray());
    }

    // "m_LocalRotation.x" → "m_LocalRotation": the channel a bone's components share.
    static string Channel(string property)
    {
        int dot = property.LastIndexOf('.');
        return dot < 0 ? property : property.Substring(0, dot);
    }

    // Greedy: extend each segment while it still fits every curve of the set.
    static List<int> Keep(AnimationCurve[] curves, float tolerance)
    {
        int count = curves[0].length;
        var keep = new List<int> { 0 };
        if (count <= 2)
        {
            for (int i = 1; i < count; i++)
                keep.Add(i);
            return keep;
        }
        Keyframe[][] keys = curves.Select(c => c.keys).ToArray();
        if (Still(keys, tolerance))
        {
            keep.Add(count - 1);
            return keep;
        }
        int from = 0;
        for (int to = 2; to < count; to++)
        {
            if (Fits(curves, keys, from, to, tolerance))
                continue;
            keep.Add(to - 1);
            from = to - 1;
        }
        keep.Add(count - 1);
        return keep;
    }

    // A channel that never moves: every value within tolerance of the first, every slope flat.
    static bool Still(Keyframe[][] keys, float tolerance)
    {
        foreach (Keyframe[] k in keys)
        {
            float span = Mathf.Max(k[^1].time - k[0].time, 1e-4f);
            foreach (Keyframe key in k)
                if (Mathf.Abs(key.value - k[0].value) > tolerance || Mathf.Abs(key.inTangent) * span > tolerance
                    || Mathf.Abs(key.outTangent) * span > tolerance)
                    return false;
        }
        return true;
    }

    // The segment from key `from` to key `to`, a Hermite on their own values and slopes, against the original curve at
    // each skipped key and halfway between keys.
    static bool Fits(AnimationCurve[] curves, Keyframe[][] keys, int from, int to, float tolerance)
    {
        for (int c = 0; c < curves.Length; c++)
        {
            Keyframe[] k = keys[c];
            Keyframe a = k[from], b = k[to];
            if (float.IsInfinity(a.outTangent) || float.IsInfinity(b.inTangent))
                return false;
            float span = b.time - a.time;
            for (int i = from; i < to; i++)
            {
                if (i > from && !Near(curves[c], k[i].time, a, b, span, tolerance))
                    return false;
                if (!Near(curves[c], (k[i].time + k[i + 1].time) * 0.5f, a, b, span, tolerance))
                    return false;
            }
        }
        return true;
    }

    static bool Near(AnimationCurve original, float time, Keyframe a, Keyframe b, float span, float tolerance)
    {
        float s = span > 0f ? (time - a.time) / span : 0f;
        float s2 = s * s, s3 = s2 * s;
        float value = (2f * s3 - 3f * s2 + 1f) * a.value + (s3 - 2f * s2 + s) * span * a.outTangent
                      + (-2f * s3 + 3f * s2) * b.value + (s3 - s2) * span * b.inTangent;
        return Mathf.Abs(value - original.Evaluate(time)) <= tolerance;
    }

    // The kept keys with their own values and slopes (what Fits measured against); weights off.
    static AnimationCurve Thinned(AnimationCurve curve, List<int> keep)
    {
        Keyframe[] source = curve.keys;
        var keys = new Keyframe[keep.Count];
        for (int i = 0; i < keep.Count; i++)
        {
            Keyframe key = source[keep[i]];
            keys[i] = new Keyframe(key.time, key.value, key.inTangent, key.outTangent);
        }
        return new AnimationCurve(keys);
    }

    static AnimationCurve Held(AnimationCurve curve) => AnimationCurve.Constant(curve.keys[0].time, curve.keys[^1].time, curve.keys[0].value);

    // ---- Controller ----

    // Move (idle → walk → trot → canter → run → sprint on Speed, m/s), Sniff (Stance 2) and Swim (Swim) blends; the pose
    // chain stand ↔ sit ↔ lie ↔ sleep through the pack's transition clips (Pose); one-off acts from standing (Act +
    // ActIndex); flinches by side (Hit + Side) and jumps (Jump + JumpKind) from any state. Rebuilt in place.
    public static string Controller()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        else
            Clear(controller);
        foreach ((string name, AnimatorControllerParameterType type) in new[]
                 {
                     ("Speed", AnimatorControllerParameterType.Float), ("Turn", AnimatorControllerParameterType.Float),
                     ("Stance", AnimatorControllerParameterType.Int), ("Pose", AnimatorControllerParameterType.Int),
                     ("Act", AnimatorControllerParameterType.Trigger), ("ActIndex", AnimatorControllerParameterType.Int),
                     ("Hit", AnimatorControllerParameterType.Trigger), ("Side", AnimatorControllerParameterType.Int),
                     ("Jump", AnimatorControllerParameterType.Trigger), ("JumpKind", AnimatorControllerParameterType.Int),
                     ("Swim", AnimatorControllerParameterType.Bool),
                     ("Attack", AnimatorControllerParameterType.Trigger), ("AttackIndex", AnimatorControllerParameterType.Int),
                     ("Dodge", AnimatorControllerParameterType.Trigger), ("Down", AnimatorControllerParameterType.Trigger),
                     ("Rise", AnimatorControllerParameterType.Trigger), ("Pin", AnimatorControllerParameterType.Bool),
                 })
            controller.AddParameter(name, type);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        var speeds = new List<string>();

        AnimationClip idle = Source("Idle/Dog_Idle").Clip;
        BlendTree moveTree = Tree(controller, "Move", "Speed");
        moveTree.AddChild(idle, 0f);
        // The walk again at a crawl, so the long idle only mixes in near a standstill (a blend syncs its clips' cycles: the
        // idle would drag the walking legs slower than the body moves).
        (AnimationClip walk, float walkSpeed) = Source("Locomotion/Dog_Walk_Forward");
        Gait(moveTree, walk, walkSpeed * SlowStep, Cadence * SlowStep);
        float top = 0f;
        AnimationClip fastest = null;
        foreach (string gait in new[] { "Walk", "Trot", "Canter", "Run", "Sprint" })
        {
            (AnimationClip clip, float speed) = Source($"Locomotion/Dog_{gait}_Forward");
            Gait(moveTree, clip, speed);
            speeds.Add($"{gait} {speed:0.00}");
            top = speed;
            fastest = clip;
        }
        // Past the sprint clip's own pace: the same clip, played faster.
        float topSpeed = top * 1.25f;
        Gait(moveTree, fastest, topSpeed, Cadence * topSpeed / top);
        AnimatorState move = machine.AddState("Move");
        move.motion = moveTree;
        machine.defaultState = move;

        BlendTree sniffTree = Tree(controller, "Sniff", "Speed");
        sniffTree.AddChild(Source("Stances/Smell/Dog_Smell_Idle").Clip, 0f);
        foreach (string gait in new[] { "Walk", "Trot" })
        {
            (AnimationClip clip, float speed) = Source($"Stances/Smell/Dog_Smell_{gait}_Forward");
            Gait(sniffTree, clip, speed);
        }
        AnimatorState sniff = machine.AddState("Sniff");
        sniff.motion = sniffTree;
        Quick(move.AddTransition(sniff), 0.25f).AddCondition(AnimatorConditionMode.Equals, 2, "Stance");
        Quick(sniff.AddTransition(move), 0.25f).AddCondition(AnimatorConditionMode.NotEqual, 2, "Stance");

        BlendTree swimTree = Tree(controller, "Swim", "Speed");
        swimTree.AddChild(Source("Swim/Dog_Swim_Idle").Clip, 0f);
        (AnimationClip swimClip, float swimSpeed) = Source("Swim/Dog_Swim_Forward");
        Gait(swimTree, swimClip, swimSpeed);
        AnimatorState swim = machine.AddState("Swim");
        swim.motion = swimTree;
        foreach (AnimatorState from in new[] { move, sniff })
            Quick(from.AddTransition(swim), 0.3f).AddCondition(AnimatorConditionMode.If, 0f, "Swim");
        Quick(swim.AddTransition(move), 0.3f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Swim");

        // Poses: stand ↔ sit ↔ lie ↔ sleep, each step through its own clip. The Root keeps its authored offsets (sitting puts
        // the body back with the paws planted): held, the paws slid forward on every sit and the body sat in the wrong spot.
        AnimatorState sitDown = Once(machine, "SitDown", "Actions/Dog_Sit_From_Idle", keepRoot: true);
        AnimatorState sit = Loop(machine, "Sit", "Actions/Dog_Sit_01", keepRoot: true);
        AnimatorState sitUp = Once(machine, "SitUp", "Actions/Dog_Sit_to_Idle", keepRoot: true);
        AnimatorState sitToLie = Once(machine, "SitToLie", "Actions/Dog_Sit_to_Lie", keepRoot: true);
        AnimatorState lie = Loop(machine, "Lie", "Actions/Dog_Lie_01", keepRoot: true);
        AnimatorState lieToSit = Once(machine, "LieToSit", "Actions/Dog_Lie_to_Sit", keepRoot: true);
        AnimatorState lieToSleep = Once(machine, "LieToSleep", "Actions/Dog_Lie_to_Sleep", keepRoot: true);
        AnimatorState sleep = Loop(machine, "Sleep", "Actions/Dog_Sleep_Pose1", "Dog_Sleep_P1", keepRoot: true);
        AnimatorState sleepToLie = Once(machine, "SleepToLie", "Actions/Dog_Sleep_to_Lie", keepRoot: true);
        AnimatorStateTransition down = Quick(move.AddTransition(sitDown), 0.2f);
        down.AddCondition(AnimatorConditionMode.Greater, 0, "Pose");
        down.AddCondition(AnimatorConditionMode.Less, 0.15f, "Speed");
        Then(sitDown, sit);
        Quick(sit.AddTransition(sitToLie), 0.2f).AddCondition(AnimatorConditionMode.Greater, 1, "Pose");
        Then(sitToLie, lie);
        Quick(lie.AddTransition(lieToSleep), 0.2f).AddCondition(AnimatorConditionMode.Equals, 3, "Pose");
        Then(lieToSleep, sleep);
        Quick(sleep.AddTransition(sleepToLie), 0.2f).AddCondition(AnimatorConditionMode.Less, 3, "Pose");
        Then(sleepToLie, lie);
        Quick(lie.AddTransition(lieToSit), 0.2f).AddCondition(AnimatorConditionMode.Less, 2, "Pose");
        Then(lieToSit, sit);
        Quick(sit.AddTransition(sitUp), 0.2f).AddCondition(AnimatorConditionMode.Equals, 0, "Pose");
        Then(sitUp, move);

        // Fighting stance (Stance 1) and the injured limp (Stance 3), each its own blend.
        BlendTree combatTree = Tree(controller, "Combat", "Speed");
        combatTree.AddChild(Source("Stances/Combat/Dog_Combat_Idle").Clip, 0f);
        foreach (string gait in new[] { "Walk", "Trot" })
        {
            (AnimationClip clip, float speed) = Source($"Stances/Combat/Dog_Combat_{gait}");
            Gait(combatTree, clip, speed);
        }
        // Faster than its fighting trot (darting in, getting out): the canter and the sprint.
        foreach (string gait in new[] { "Canter", "Sprint" })
        {
            (AnimationClip clip, float speed) = Source($"Locomotion/Dog_{gait}_Forward");
            Gait(combatTree, clip, speed);
        }
        AnimatorState combat = machine.AddState("Combat");
        combat.motion = combatTree;
        Quick(move.AddTransition(combat), 0.2f).AddCondition(AnimatorConditionMode.Equals, 1, "Stance");
        Quick(combat.AddTransition(move), 0.25f).AddCondition(AnimatorConditionMode.NotEqual, 1, "Stance");
        BlendTree injuredTree = Tree(controller, "Injured", "Speed");
        injuredTree.AddChild(Source("Stances/Injured/Dog_Injured_Idle").Clip, 0f);
        (AnimationClip limp, float limpSpeed) = Source("Stances/Injured/Dog_Injured_Walk_Forward");
        Gait(injuredTree, limp, limpSpeed);
        AnimatorState injured = machine.AddState("Injured");
        injured.motion = injuredTree;
        Quick(move.AddTransition(injured), 0.3f).AddCondition(AnimatorConditionMode.Equals, 3, "Stance");
        Quick(injured.AddTransition(move), 0.3f).AddCondition(AnimatorConditionMode.NotEqual, 3, "Stance");
        speeds.Add($"Limp {limpSpeed:0.00}");

        // Attacks (AttackIndex 0 bite forward, 1 front left, 2 front right, 3 feint): from moving, fighting or limping.
        string[] attacks = { "Attacks/Dog_Attack_Bite_Forward v2", "Attacks/Dog_Attack_Bite_Front_L", "Attacks/Dog_Attack_Bite_Front_R", "Attacks/Dog_Charge_Low" };
        for (int i = 0; i < attacks.Length; i++)
        {
            AnimatorState state = Once(machine, "Attack " + i, attacks[i]);
            foreach (AnimatorState from in new[] { move, combat, injured, sniff })
            {
                AnimatorStateTransition into = Quick(from.AddTransition(state), 0.08f);
                into.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                into.AddCondition(AnimatorConditionMode.Equals, i, "AttackIndex");
            }
            Exit(state.AddTransition(move), 0.85f, 0.15f);
        }
        // The takedown pin: biting and shaking over the body while Pin is on.
        AnimatorState pin = Loop(machine, "Pin", "Attacks/Dog_Attack_Bite_Forward v2", copyName: "Dog_Attack_Bite_Pin");
        foreach (AnimatorState from in new[] { move, combat })
            Quick(from.AddTransition(pin), 0.1f).AddCondition(AnimatorConditionMode.If, 0f, "Pin");
        Quick(pin.AddTransition(move), 0.2f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Pin");
        // Dodges (Side 0 left, 1 right).
        string[] dodgeSides = { "Left", "Right" };
        for (int i = 0; i < 2; i++)
        {
            AnimatorState state = Once(machine, "Dodge " + dodgeSides[i], "Dodge/Dog_Dodge_" + dodgeSides[i]);
            AnimatorStateTransition into = Quick(machine.AddAnyStateTransition(state), 0.06f);
            into.canTransitionToSelf = false;
            into.AddCondition(AnimatorConditionMode.If, 0f, "Dodge");
            into.AddCondition(AnimatorConditionMode.Equals, i, "Side");
            Exit(state.AddTransition(move), 0.85f, 0.15f);
        }
        // Knocked down (Side 0 left, 1 right): falls and stays down until Rise, then the recovery clip.
        for (int i = 0; i < 2; i++)
        {
            AnimatorState fall = Once(machine, "Down " + dodgeSides[i], "Deaths/Dog_Death1_" + dodgeSides[i], keepRoot: true);
            AnimatorStateTransition into = Quick(machine.AddAnyStateTransition(fall), 0.1f);
            into.canTransitionToSelf = false;
            into.AddCondition(AnimatorConditionMode.If, 0f, "Down");
            into.AddCondition(AnimatorConditionMode.Equals, i, "Side");
            AnimatorState rise = Once(machine, "Rise " + dodgeSides[i], "Deaths/Dog_Death_Recovery_" + dodgeSides[i], keepRoot: true);
            Quick(fall.AddTransition(rise), 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Rise");
            Exit(rise.AddTransition(move), 0.9f, 0.2f);
        }

        // Acts (DogAct order) from standing or sniffing; back to Move at the end (exit time: share of the clip played).
        (DogAct Act, string File, string Take, float Exit)[] acts =
        {
            (DogAct.Bark, "Actions/Dog_Bark", "Dog_Bark", 0.95f),
            (DogAct.Happy, "Actions/Dog_Happy", null, 0.95f),
            (DogAct.HeadTilt, "Actions/Dog_Head_Tilt", null, 0.55f),
            (DogAct.ShakeWater, "Actions/Dog_Shake_Water", null, 0.95f),
            (DogAct.Dig, "Actions/Dog_Dig", null, 0.95f),
            (DogAct.Pee, "Actions/Dog_Pee_Male", "Dog_Pee_Male", 0.95f),
            (DogAct.Howl, "Actions/Dog_Howl", null, 0.95f),
            (DogAct.Yes, "Actions/Dog_Yes", null, 0.95f),
            (DogAct.No, "Actions/Dog_No", null, 0.95f),
            (DogAct.Roll, "Actions/Dog_Rolling", null, 1.95f),
            (DogAct.Alert, "Actions/Dog_Alert", null, 0.4f),
            (DogAct.Yawn, "Idle/Dog_Idle_Yawn", null, 0.95f),
            (DogAct.Growl, "Stances/Combat/Dog_Combat_Idle_Growl", null, 0.9f),
            (DogAct.LookAround, "Idle/Dog_Idle_Look_Around", null, 0.95f),
            (DogAct.LookLeft, "Idle/Dog_Idle Look Left", null, 0.95f),
            (DogAct.LookRight, "Idle/Dog_Idle Look Right", null, 0.95f),
            (DogAct.ScratchEarL, "Idle/Dog_Idle_Scratch_Ear_L", null, 0.95f),
            (DogAct.ScratchEarR, "Idle/Dog_Idle_Scratch_Ear_R", null, 0.95f),
        };
        foreach ((DogAct act, string file, string take, float exit) in acts)
        {
            AnimatorState state = machine.AddState("Act " + act);
            state.motion = Source(file, take, loop: false).Clip;
            foreach (AnimatorState from in new[] { move, sniff, combat })
            {
                AnimatorStateTransition into = Quick(from.AddTransition(state), 0.15f);
                into.AddCondition(AnimatorConditionMode.If, 0f, "Act");
                into.AddCondition(AnimatorConditionMode.Equals, (int)act, "ActIndex");
            }
            Exit(state.AddTransition(move), exit, 0.25f);
        }

        // Flinches: Side 0 front-left, 1 front-right, 2 left, 3 right, 4 back-left, 5 back-right.
        string[] sides = { "Front_Left", "Front_Right", "Left", "Right", "Back_Left", "Back_Right" };
        for (int i = 0; i < sides.Length; i++)
        {
            AnimatorState state = machine.AddState("Hit " + sides[i]);
            state.motion = Source("Damage/Dog_Damage_" + sides[i], loop: false).Clip;
            AnimatorStateTransition into = Quick(machine.AddAnyStateTransition(state), 0.08f);
            into.canTransitionToSelf = false;
            into.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            into.AddCondition(AnimatorConditionMode.Equals, i, "Side");
            Exit(state.AddTransition(move), 0.9f, 0.2f);
        }

        // Jumps (DogJumpArc.JumpKind): the code moves the body along the link; these only pose it.
        AnimatorState hop = Once(machine, "Hop", "Jump/Dog_Jump_Trot", "Dog_Jump_Trot");
        AnimatorState hopLand = Once(machine, "HopLand", "Jump/Dog_Jump_Trot", "Dog_Land_Trot");
        AnimatorState ledge2 = Once(machine, "Ledge 2.0", "Ledge_Grab/Dog_Ledge_2.0M");
        AnimatorState climb = Once(machine, "ClimbUp", "Ledge_Grab/Dog_Ledge_Climb Up");
        AnimatorState edge = Once(machine, "FromEdge", "Fall/Dog_Fall_from_Edge");
        AnimatorState land = Once(machine, "Land", "Fall/Dog_Fall_Land");
        Then(hop, hopLand);
        Then(hopLand, move);
        Then(climb, move);
        Then(edge, land);
        Then(land, move);
        AnimatorState[] ledges =
        {
            Once(machine, "Ledge 0.5", "Ledge_Grab/Dog_Ledge_0.50M"), Once(machine, "Ledge 1.0", "Ledge_Grab/Dog_Ledge_1.00M"),
            Once(machine, "Ledge 1.5", "Ledge_Grab/Dog_Ledge_1.50M"), ledge2,
        };
        foreach (AnimatorState ledge in ledges)
            Then(ledge, climb);
        // The scramble over the 2 m fence: paws up (the 1 m ledge clip: the model is at 2×), climb, drop off the far side,
        // sped up to DogJumpArc's timing (the body's path over the fence follows the same; DogTests checks they agree).
        AnimatorState scrambleGrab = Once(machine, "ScrambleGrab", "Ledge_Grab/Dog_Ledge_1.00M");
        AnimatorState scrambleClimb = Once(machine, "ScrambleClimb", "Ledge_Grab/Dog_Ledge_Climb Up");
        scrambleGrab.speed = scrambleClimb.speed = DogJumpArc.ScrambleClipSpeed;
        Then(scrambleGrab, scrambleClimb);
        Then(scrambleClimb, edge);
        // The leap over a fence or onto a block: a standing jump (crouch, leap, land; DogJumpArc times the body to it).
        AnimatorState leap = Once(machine, "Leap", "Jump/Dog_Jump_Walk", "Dog_Jump_Walk");
        Exit(leap.AddTransition(move), 0.94f, 0.2f);
        // JumpKind 0 hop, 1 scramble, 2–5 climb onto a 0.5 / 1.0 / 1.5 / 2.0 m ledge (at 1×), 6 drop down, 7 leap.
        var starts = new (int Kind, AnimatorState State)[]
            { (0, hop), (1, scrambleGrab), (2, ledges[0]), (3, ledges[1]), (4, ledges[2]), (5, ledge2), (6, edge), (7, leap) };
        foreach ((int kind, AnimatorState state) in starts)
        {
            AnimatorStateTransition into = Quick(machine.AddAnyStateTransition(state), 0.1f);
            into.canTransitionToSelf = false;
            into.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
            into.AddCondition(AnimatorConditionMode.Equals, kind, "JumpKind");
        }
        Save(controller);
        return "controller: " + ControllerPath + " (" + string.Join(", ", speeds) + " m/s)";
    }

    const float SlowStep = 0.3f;

    // A moving clip at its blend position, played at `timeScale` (Cadence: its feet then match `speed`).
    static void Gait(BlendTree tree, AnimationClip clip, float speed, float timeScale = Cadence)
    {
        tree.AddChild(clip, Mathf.Max(speed, 0.3f));
        ChildMotion[] children = tree.children;
        children[^1].timeScale = timeScale;
        tree.children = children;
    }

    static BlendTree Tree(AnimatorController controller, string name, string parameter)
    {
        var tree = new BlendTree { name = name, blendParameter = parameter, useAutomaticThresholds = false, hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(tree, controller);
        return tree;
    }

    static AnimatorState Once(AnimatorStateMachine machine, string name, string file, string take = null, bool keepRoot = false)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = Source(file, take, loop: false, keepRoot).Clip;
        return state;
    }

    static AnimatorState Loop(AnimatorStateMachine machine, string name, string file, string take = null, bool keepRoot = false,
        string copyName = null)
    {
        AnimatorState state = machine.AddState(name);
        state.motion = Source(file, take, keepRoot: keepRoot, copyName: copyName).Clip;
        return state;
    }

    static void Then(AnimatorState from, AnimatorState to) => Exit(from.AddTransition(to), 0.92f, 0.15f);

    static void Clear(AnimatorController controller)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
            controller.RemoveParameter(parameter);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            machine.RemoveAnyStateTransition(transition);
        foreach (ChildAnimatorState state in machine.states)
            machine.RemoveState(state.state);
        foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath))
            if (sub is BlendTree)
                Object.DestroyImmediate(sub, true);
    }

    static void Exit(AnimatorStateTransition transition, float exitTime, float duration)
    {
        transition.hasExitTime = true;
        transition.exitTime = exitTime;
        transition.duration = duration;
        transition.hasFixedDuration = true;
    }

    static AnimatorStateTransition Quick(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = duration;
        return transition;
    }

    // ---- Prefabs ----

    // The base entity: the WanderCapsule's legs and tree (FollowerEntity, EnemyMover, Manual BehaviorTree) with the Dog on
    // top, the model nested as Model, a body capsule to look at and whistle at, a SaveId; then the Loki variant.
    public static string Prefabs()
    {
        Folder(Content + "/Loki");
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(BasePath) != null;
        GameObject root = PrefabUtility.LoadPrefabContents(exists ? BasePath : Template);
        try
        {
            if (!exists)
            {
                Object.DestroyImmediate(root.GetComponent<MB13.Aipocalypse.Core.EnemySystem.EnemyBody>());
                Object.DestroyImmediate(root.GetComponent<MB13.Aipocalypse.Core.EnemySystem.EnemyBrain>());
                Object.DestroyImmediate(root.GetComponent<MB13.Aipocalypse.Core.SpawnSystem.Spawnable>());
                Object.DestroyImmediate(root.transform.Find("Capsule").gameObject);
            }
            root.name = "Dog";
            root.layer = LayerMask.NameToLayer(AnimalLayer);

            var body = root.GetComponent<CapsuleCollider>();
            body.direction = 2;
            body.radius = 0.2f * ModelScale;
            body.height = 1f * ModelScale;
            body.center = new Vector3(0f, 0.45f, 0.05f) * ModelScale;

            var agent = new SerializedObject(root.GetComponent<Pathfinding.FollowerEntity>());
            agent.FindProperty("shape.radius").floatValue = AgentRadius;
            agent.FindProperty("shape.height").floatValue = AgentHeight;
            agent.FindProperty("movement.debugFlags").intValue = 0;
            agent.FindProperty("movement.follower.rotationSpeed").floatValue = TurnSpeed;
            agent.FindProperty("movement.follower.maxRotationSpeed").floatValue = MaxTurnSpeed;
            agent.FindProperty("movement.follower.maxOnSpotRotationSpeed").floatValue = OnSpotTurnSpeed;
            // A dog eases into a stop over a couple of steps (and sets off the same way).
            agent.FindProperty("movement.follower.slowdownTime").floatValue = SlowdownTime;
            agent.FindProperty("movement.follower.allowRotatingOnSpotBacking").boolValue = false;
            agent.FindProperty("movement.rotationSmoothing").floatValue = TurnSmoothing;
            agent.ApplyModifiedPropertiesWithoutUndo();

            var tree = new SerializedObject(root.GetComponent<Opsive.BehaviorDesigner.Runtime.BehaviorTree>());
            var subtree = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TreePath);
            if (subtree != null)
                tree.FindProperty("m_Subtree").objectReferenceValue = subtree;
            SetIfFound(tree, "m_UpdateMode", 1);
            SerializedProperty startWhenEnabled = tree.FindProperty("m_StartWhenEnabled");
            if (startWhenEnabled != null)
                startWhenEnabled.boolValue = false;
            tree.ApplyModifiedPropertiesWithoutUndo();

            var model = Load<GameObject>(ModelPath);
            Transform modelRoot = root.transform.Find("Model");
            if (modelRoot != null && PrefabUtility.GetCorrespondingObjectFromSource(modelRoot.gameObject) != model)
            {
                Object.DestroyImmediate(modelRoot.gameObject);
                modelRoot = null;
            }
            if (modelRoot == null)
            {
                modelRoot = ((GameObject)PrefabUtility.InstantiatePrefab(model, root.transform)).transform;
                modelRoot.name = "Model";
            }
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localScale = Vector3.one * ModelScale;
            var animator = modelRoot.GetComponent<Animator>();
            animator.runtimeAnimatorController = Load<AnimatorController>(ControllerPath);

            if (!root.TryGetComponent(out Dog dog))
                dog = root.AddComponent<Dog>();
            dog.Setup(animator, modelRoot);
            if (!root.TryGetComponent(out DogLegs _))
                root.AddComponent<DogLegs>();
            if (!root.TryGetComponent(out DogJump _))
                root.AddComponent<DogJump>();
            if (!root.TryGetComponent(out SaveId _))
                root.AddComponent<SaveId>();
            // Its paw on the minimap (registered when the dog is injected: DogInstaller injects the whole object).
            Transform markerNode = root.transform.Find("Map Marker");
            if (markerNode == null)
            {
                markerNode = new GameObject("Map Marker").transform;
                markerNode.SetParent(root.transform, false);
            }
            var marker = markerNode.GetComponent<MB13.Aipocalypse.Core.MapSystem.MapMarker>() ?? markerNode.gameObject.AddComponent<MB13.Aipocalypse.Core.MapSystem.MapMarker>();
            var markerSo = new SerializedObject(marker);
            markerSo.FindProperty("_kind").objectReferenceValue = MapKind();
            markerSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BasePath, out bool saved);
            if (!saved)
                return "prefabs FAILED " + BasePath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return "prefabs: " + BasePath + ", " + Variant();
    }

    const string MapKindPath = "Assets/_Game/Data/Map/Dog.asset";
    const string MapIconBackground = "Assets/Synty/InterfaceApocalypseHUD/Sprites/HUD/SPR_HUD_Apocalypse_Map_Icon_Background_01.png";

    // The dog's minimap icon: a small paw on the map icon backing, teal (home is yellow, enemies red). Shown only within
    // the minimap's range: pinned to the rim it would sit on the home icon whenever it stays at home.
    static MB13.Aipocalypse.Core.MapSystem.MapMarkerKind MapKind()
    {
        var kind = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.MapSystem.MapMarkerKind>(MapKindPath);
        if (kind == null)
        {
            kind = ScriptableObject.CreateInstance<MB13.Aipocalypse.Core.MapSystem.MapMarkerKind>();
            AssetDatabase.CreateAsset(kind, MapKindPath);
        }
        kind.Background = Load<Sprite>(MapIconBackground);
        kind.BackgroundColor = new Color(0.4f, 0.8f, 0.85f, 1f);
        kind.Icon = AssetDatabase.LoadAllAssetsAtPath(PawIconPath).OfType<Sprite>().FirstOrDefault();
        kind.IconColor = new Color(0.055f, 0.055f, 0.055f, 1f);
        kind.Size = 22f;
        kind.PinToEdge = false;
        Save(kind);
        return kind;
    }

    static string Variant()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(LokiPath) != null)
            return LokiPath + " (kept)";
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(BasePath));
        try
        {
            instance.name = "Loki";
            PrefabUtility.SaveAsPrefabAsset(instance, LokiPath, out bool saved);
            return saved ? LokiPath : "FAILED " + LokiPath;
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    // ---- WorldMap ----

    const string WorldMapPath = "Assets/_Game/Scenes/Prototypes/WorldMap.unity";
    const string DogHousePrefab = "Assets/_StoreAssets/Env/SICS/Toon Series/Toon Farm Pack/Prefabs/Buildings/Farm Structures/TFP_Dog_House_01B.prefab";
    const float FenceHeight = 2f;
    const float LinkReach = 1.1f * ModelScale;
    const float DoorDistance = 1.45f * ModelScale;

    // In WorldMap: the "Home Dog" root with the dog house (its door and bed, a navmesh cut), Loki at its door, the spots it
    // visits, a DogOnly NodeLink2 across every fence segment and the gate (scrambled over, DogLinkInfo), the sill as the
    // dog's way through the robots' house door; the DogInstaller on the scene scope; AstarPath names tag 2. Re-runnable.
    public static string World()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != WorldMapPath)
            return "open WorldMap first (active: " + scene.path + ")";
        var log = new List<string>();
        var yard = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.RobotSystem.RobotYard>() ?? throw new Exception("no RobotYard");
        Transform root = SceneRoot(scene, "Home Dog");

        Transform garden = FindNamed("Spot_Garden");
        Transform doorLink = FindNamed("DoorLink_House");
        Transform outside = doorLink != null ? doorLink.Find("Outside") : null;
        Transform gate = Object.FindObjectsByType<MB13.Aipocalypse.Core.DefenseSystem.BarrierSegment>(FindObjectsSortMode.None).FirstOrDefault(b => b.name == "Gate")?.transform;
        var pen = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.AnimalSystem.AnimalPen>();
        Vector3 yardCenter = yard.transform.position;

        // The dog house: the best free spot near the house door, its opening facing the middle of the yard.
        Transform house = root.Find("DogHouse");
        if (house == null)
        {
            Vector3 spot = BestHouseSpot(yard, outside != null ? outside.position : yardCenter, garden, gate, pen, out float score);
            house = ((GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(DogHousePrefab), root)).transform;
            house.name = "DogHouse";
            Vector3 away = spot - yardCenter;
            away.y = 0f;
            // The opening is on the model's -Z side: forward points away from the middle of the yard.
            house.SetPositionAndRotation(spot, Quaternion.LookRotation(away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward));
            log.Add($"dog house at {spot} (score {score:0.0})");
        }
        // Sized to the dog (its navmesh cut scales with it: useRotationAndScale).
        house.localScale = Vector3.one * ModelScale;
        Vector3 opening = -house.forward;
        Transform door = Child(house, "Door");
        door.position = Ground(house.position + opening * DoorDistance);
        door.rotation = Quaternion.LookRotation(-opening);
        Transform bed = Child(house, "Bed");
        bed.position = house.position + opening * 0.12f * ModelScale;
        bed.rotation = Quaternion.LookRotation(opening);
        var dogHouse = house.GetComponent<DogHouse>() ?? house.gameObject.AddComponent<DogHouse>();
        dogHouse.Setup(door, bed);
        EditorUtility.SetDirty(dogHouse);
        var cut = house.GetComponent<Pathfinding.NavmeshCut>() ?? house.gameObject.AddComponent<Pathfinding.NavmeshCut>();
        cut.type = Pathfinding.NavmeshCut.MeshType.Box;
        cut.rectangleSize = new Vector2(1f, 1.1f);
        cut.height = 2f;
        cut.center = new Vector3(0f, 0.8f, 0f);
        cut.useRotationAndScale = true;
        cut.radiusExpansionMode = Pathfinding.NavmeshCut.RadiusExpansionMode.ExpandByAgentRadius;
        EditorUtility.SetDirty(cut);

        // Loki, at its door.
        Dog loki = Object.FindObjectsByType<Dog>(FindObjectsSortMode.None).FirstOrDefault();
        if (loki == null)
        {
            loki = ((GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(LokiPath), root)).GetComponent<Dog>();
            loki.transform.SetPositionAndRotation(door.position, Quaternion.LookRotation(opening));
            log.Add("Loki placed");
        }

        // Spots it trots over to at home.
        Transform visitsRoot = Child(root, "Visits");
        var visits = new List<Transform>();
        void Visit(string name, Vector3? at)
        {
            if (at == null)
                return;
            Transform visit = Child(visitsRoot, name);
            visit.position = Ground(at.Value);
            visits.Add(visit);
        }
        Visit("Visit_Garden", garden != null ? garden.position + (yardCenter - garden.position).normalized * 1.8f : null);
        Visit("Visit_Door", outside != null ? outside.position + (yardCenter - outside.position).normalized * 1.2f : null);
        Visit("Visit_Gate", gate != null ? gate.position + (yardCenter - gate.position).normalized * 2.5f : null);
        Visit("Visit_Pen", pen != null ? pen.transform.position + (yardCenter - pen.transform.position).normalized * 3f : null);
        Visit("Visit_Middle", yardCenter);

        // Fence crossings: no NodeLink2 (it would join the shut yard's navmesh island to the outside for every agent: raiders
        // would stop breaking in); DogScramble crosses them by hand.
        Transform links = Child(root, "Fence Links");
        var crossings = new List<DogLinkInfo>();
        int count = 0;
        foreach (var segment in Object.FindObjectsByType<MB13.Aipocalypse.Core.DefenseSystem.BarrierSegment>(FindObjectsSortMode.None))
        {
            Vector3 at = segment.transform.position;
            Vector3 normal = Vector3.Cross(Vector3.up, segment.Span).normalized;
            if (Vector3.Dot(normal, yardCenter - at) < 0f)
                normal = -normal;
            Transform link = Child(links, "DogLink " + segment.name);
            link.position = Ground(at + normal * LinkReach);
            link.rotation = Quaternion.LookRotation(-normal);
            Transform end = Child(link, "Outside");
            end.position = Ground(at - normal * LinkReach);
            var nodeLink = link.GetComponent<Pathfinding.NodeLink2>();
            if (nodeLink != null)
                Object.DestroyImmediate(nodeLink);
            var info = link.GetComponent<DogLinkInfo>() ?? link.gameObject.AddComponent<DogLinkInfo>();
            info.Setup(FenceHeight, null, end, segment);
            EditorUtility.SetDirty(info);
            crossings.Add(info);
            count++;
        }
        log.Add($"{count} fence crossings");

        // The robots' house door: the dog walks to the sill, then hops.
        if (doorLink != null)
        {
            var info = doorLink.GetComponent<DogLinkInfo>() ?? doorLink.gameObject.AddComponent<DogLinkInfo>();
            info.Setup(0f, doorLink.Find("Sill"));
            EditorUtility.SetDirty(info);
        }

        var astar = Object.FindAnyObjectByType<AstarPath>() ?? throw new Exception("no AstarPath");
        var astarSo = new SerializedObject(astar);
        SerializedProperty names = astarSo.FindProperty("tagNames");
        if (names.arraySize > MB13.Aipocalypse.Core.EnemySystem.NavTags.DogOnly)
            names.GetArrayElementAtIndex(MB13.Aipocalypse.Core.EnemySystem.NavTags.DogOnly).stringValue = "Dog only";
        astarSo.ApplyModifiedPropertiesWithoutUndo();

        var scope = Object.FindAnyObjectByType<MB13.Aipocalypse.Core.Bootstrap.GameLifetimeScope>() ?? throw new Exception("no GameLifetimeScope");
        var installer = scope.GetComponent<DogInstaller>() ?? scope.gameObject.AddComponent<DogInstaller>();
        installer.Setup(Load<DogSettings>(SettingsPath), dogHouse, yard, visits, crossings);
        installer.CollectSceneDogs();
        EditorUtility.SetDirty(installer);
        log.Add(installer.Problem == null ? "installer ready" : "installer problem: " + installer.Problem);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        return "world: " + string.Join(", ", log) + $"; house {house.position}, door {door.position}";
    }

    // The yard spot (1 m grid) for the dog house: inside the yard with room round it, nothing in the way, 3.5 to 12 m from
    // the house door (nearer 6 m is better), clear of Bolt's spot, the herd's corner and the walk from the door to the gate.
    static Vector3 BestHouseSpot(MB13.Aipocalypse.Core.RobotSystem.RobotYard yard, Vector3 houseDoor, Transform garden, Transform gate,
        MB13.Aipocalypse.Core.AnimalSystem.AnimalPen pen, out float best)
    {
        best = float.MaxValue;
        Vector3 chosen = yard.transform.position;
        Matrix4x4 toWorld = yard.WorldToLocal.inverse;
        Vector3 half = yard.HalfSize;
        for (float x = -half.x + 3f; x <= half.x - 3f; x += 1f)
        for (float z = -half.z + 3f; z <= half.z - 3f; z += 1f)
        {
            Vector3 p = Ground(toWorld.MultiplyPoint3x4(new Vector3(x, 0f, z)));
            float door = FlatLength(p - houseDoor);
            if (door < 3.5f || door > 12f)
                continue;
            if (garden != null && FlatLength(p - garden.position) < 4.5f)
                continue;
            if (pen != null && FlatLength(p - pen.transform.position) < 9f)
                continue;
            if (gate != null && SegmentDistance(p, houseDoor, gate.position) < 3f)
                continue;
            if (Blocked(p))
                continue;
            float score = Mathf.Abs(door - 6f);
            if (score < best)
            {
                best = score;
                chosen = p;
            }
        }
        return chosen;
    }

    static bool Blocked(Vector3 ground)
    {
        foreach (Collider c in Physics.OverlapBox(ground + Vector3.up * 0.9f, new Vector3(1.1f, 0.7f, 1.1f), Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            if (c is not TerrainCollider)
                return true;
        return false;
    }

    static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        ab.y = 0f;
        Vector3 ap = p - a;
        ap.y = 0f;
        float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / Mathf.Max(ab.sqrMagnitude, 1e-4f));
        return (ap - ab * t).magnitude;
    }

    static float FlatLength(Vector3 v) => new Vector2(v.x, v.z).magnitude;

    static Vector3 Ground(Vector3 p)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(p + Vector3.up * 20f, Vector3.down, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (hit.collider is TerrainCollider)
                return hit.point;
        return p;
    }

    static Transform FindNamed(string name) => GameObject.Find(name)?.transform;

    static Transform SceneRoot(UnityEngine.SceneManagement.Scene scene, string name)
    {
        GameObject found = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (found != null)
            return found.transform;
        var made = new GameObject(name);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(made, scene);
        return made.transform;
    }

    static Transform Child(Transform parent, string name)
    {
        Transform found = parent.Find(name);
        if (found != null)
            return found;
        var made = new GameObject(name).transform;
        made.SetParent(parent, false);
        return made;
    }

    // ---- Audio ----

    const string AudioDir = "Assets/_Game/Data/Audio";
    const string DogAudioDir = AudioDir + "/Dogs";
    const string CollieAudio = Vendor + "Audio/";
    const string Impersonations = "Assets/_StoreAssets/Audio/Gamemaster Audio - Pro Sound Collection/Animal_Impersonations/";

    // The Dogs BroAudio library (placeholder sounds approved by the user, 2026-11-23): the collie's own barks, howls and
    // yelps; Gamemaster whimpers, panting and sniffs. Fills DogSettings.Sounds.
    public static string Audio()
    {
        var log = new List<string>();
        Folder(DogAudioDir);
        Ami.BroAudio.Data.AudioAsset dogs = Library(AudioDir + "/Dogs.asset", "Dogs", log);
        Ami.BroAudio.Data.SpatialSetting near = Spatial(DogAudioDir + "/Dog_Spatial.asset", 1.5f, 35f);
        var settings = Load<DogSettings>(SettingsPath);
        string[] Numbered(string prefix, params int[] numbers) => numbers.Select(n => $"{Impersonations}{prefix}{n:00}.wav").ToArray();
        var sounds = new (DogSound Sound, string Name, float Volume, float Pitch, string[] Clips)[]
        {
            (DogSound.Bark, "Dog_Bark", 0.7f, 1f, new[] { 1, 2, 3, 4 }.Select(n => $"{CollieAudio}Dog Barking {n}.ogg").ToArray()),
            (DogSound.Whine, "Dog_Whine", 0.55f, 1.1f, Numbered("dog_hurt_whimper_howl_", 1, 2, 3)),
            (DogSound.Yelp, "Dog_Yelp", 0.75f, 1.05f, new[] { CollieAudio + "Dog_Hit_1.ogg", CollieAudio + "Dog_Hit_2.ogg" }),
            (DogSound.Pant, "Dog_Pant", 0.45f, 1f, Numbered("dog_2_breathig_", 1, 2)),
            (DogSound.Sniff, "Dog_Sniff", 0.45f, 1f, Numbered("dog_sniff_breathe_", 1, 2, 3, 4, 5).Append(CollieAudio + "Dog_Smell.ogg").ToArray()),
            (DogSound.Howl, "Dog_Howl", 0.7f, 1f, new[] { CollieAudio + "Dog_Howl_1.ogg", CollieAudio + "Dog_Howl_2.ogg" }),
            (DogSound.Growl, "Dog_Growl", 0.6f, 1f, Numbered("dog_2_growl_", 1, 2, 3, 4, 5, 6, 7)),
            // Placeholder: no dog bite in the packs (only cartoon chewing); a short snappy bark stands in.
            (DogSound.Bite, "Dog_Bite", 0.7f, 1.1f, Numbered("dog_2_small_bark_short_", 1, 2, 3, 4)),
        };
        settings.Sounds = new Dictionary<DogSound, Ami.BroAudio.SoundID>();
        foreach ((DogSound sound, string name, float volume, float pitch, string[] clips) in sounds)
        {
            Ami.BroAudio.Data.AudioEntity entity = Entity(dogs, DogAudioDir, name, volume, pitch, 0.06f, clips);
            Finish(entity, near, log);
            settings.Sounds[sound] = new Ami.BroAudio.SoundID(entity);
        }
        Save(settings);
        Save(dogs);
        return "audio: " + string.Join(", ", log);
    }

    static Ami.BroAudio.Data.AudioAsset Library(string path, string name, List<string> log)
    {
        var library = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioAsset>(path);
        if (library != null)
            return library;
        library = ScriptableObject.CreateInstance<Ami.BroAudio.Data.AudioAsset>();
        AssetDatabase.CreateAsset(library, path);
        var so = new SerializedObject(library);
        so.FindProperty("<AssetName>k__BackingField").stringValue = name;
        so.FindProperty("_assetGUID").stringValue = AssetDatabase.AssetPathToGUID(path);
        so.ApplyModifiedPropertiesWithoutUndo();
        log.Add("library " + path);
        return library;
    }

    // A one-shot SFX entity with shuffled clips, a base pitch and a random pitch range (as RobotSetup builds them).
    static Ami.BroAudio.Data.AudioEntity Entity(Ami.BroAudio.Data.AudioAsset library, string dir, string name, float volume, float pitch, float pitchRandom, params string[] clips)
    {
        string path = $"{dir}/{name}.asset";
        var entity = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.AudioEntity>(path);
        if (entity == null)
        {
            entity = Ami.BroAudio.Data.AudioEntity.CreateNewInstance(library, name, Ami.BroAudio.BroAudioType.SFX);
            AssetDatabase.CreateAsset(entity, path);
        }
        var so = new SerializedObject(entity);
        so.FindProperty("<AudioAsset>k__BackingField").objectReferenceValue = library;
        so.FindProperty("<AudioType>k__BackingField").intValue = (int)Ami.BroAudio.BroAudioType.SFX;
        so.FindProperty("<Loop>k__BackingField").boolValue = false;
        so.FindProperty("<MasterVolume>k__BackingField").floatValue = volume;
        so.FindProperty("<Pitch>k__BackingField").floatValue = pitch;
        so.FindProperty("MulticlipsPlayMode").intValue = (int)(clips.Length > 1 ? Ami.BroAudio.Data.MulticlipsPlayMode.Shuffle : Ami.BroAudio.Data.MulticlipsPlayMode.Single);
        so.FindProperty("<PitchRandomRange>k__BackingField").floatValue = pitchRandom;
        so.FindProperty("<RandomFlags>k__BackingField").intValue = pitchRandom > 0f ? 1 : 0;
        SerializedProperty list = so.FindProperty("Clips");
        list.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clips[i]) ?? throw new Exception("missing clip " + clips[i]);
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("AudioClip").objectReferenceValue = clip;
            element.FindPropertyRelative("Volume").floatValue = 1f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        return entity;
    }

    static void Finish(Ami.BroAudio.Data.AudioEntity entity, Ami.BroAudio.Data.SpatialSetting spatial, List<string> log)
    {
        var so = new SerializedObject(entity);
        so.FindProperty("<SpatialSetting>k__BackingField").objectReferenceValue = spatial;
        so.ApplyModifiedPropertiesWithoutUndo();
        Save(entity);
        log.Add(entity.name);
    }

    static Ami.BroAudio.Data.SpatialSetting Spatial(string path, float min, float max)
    {
        var spatial = AssetDatabase.LoadAssetAtPath<Ami.BroAudio.Data.SpatialSetting>(path);
        if (spatial == null)
        {
            spatial = ScriptableObject.CreateInstance<Ami.BroAudio.Data.SpatialSetting>();
            AssetDatabase.CreateAsset(spatial, path);
        }
        spatial.MinDistance = min;
        spatial.MaxDistance = max;
        spatial.RolloffMode = AudioRolloffMode.Logarithmic;
        spatial.DopplerLevel = 0f;
        Save(spatial);
        return spatial;
    }

    // ---- HUD: the command wheel ----

    const string HudPath = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string LayerLab = "Assets/_StoreAssets/UI/Layer Lab/GUI Pro-SurvivalClean/ResourcesData/Sprites/Components/";
    const string WheelStylePath = "Assets/_Game/Data/UI/CommandWheelStyle.asset";
    const string LabelFontPath = "Assets/_Game/Data/UI/Fonts/SairaBold.asset";
    const string PawIconPath = LayerLab + "Icon_PictoIcons(x2)/128/Icon_Paw.Png";
    const int WheelSlots = 8;

    // The wheel in HUD.prefab's SafeFrame/Center (its own canvas, hidden until opened), its style asset, and the dog's
    // paw icon in DogSettings.
    public static string Hud()
    {
        var style = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.UI.CommandWheelStyle>(WheelStylePath);
        if (style == null)
        {
            style = ScriptableObject.CreateInstance<MB13.Aipocalypse.Core.UI.CommandWheelStyle>();
            AssetDatabase.CreateAsset(style, WheelStylePath);
        }
        style.Slice = Load<Sprite>(LayerLab + "Frame_Custom/Frame_ItemFrame04_n_White3.png");
        style.Backdrop = Load<Sprite>(LayerLab + "Button_Custom/Btn_IconButton_Circle01_White1.png");
        Save(style);

        var settings = Load<DogSettings>(SettingsPath);
        settings.Icon = AssetDatabase.LoadAllAssetsAtPath(PawIconPath).OfType<Sprite>().FirstOrDefault();
        Save(settings);

        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform center = root.transform.Find("SafeFrame/Center") ?? throw new Exception("no SafeFrame/Center");
            Transform old = center.Find("CommandWheel");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var node = new GameObject("CommandWheel", typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(center, false);
            node.anchorMin = node.anchorMax = node.pivot = new Vector2(0.5f, 0.5f);
            node.sizeDelta = new Vector2(560f, 560f);
            node.gameObject.AddComponent<Canvas>();
            var group = node.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var backdrop = Image("Backdrop", node, new Vector2(470f, 470f));
            backdrop.sprite = style.Backdrop;
            backdrop.color = new Color(0f, 0f, 0f, 0.45f);

            var slots = new UnityEngine.UI.Image[WheelSlots];
            var labels = new TMPro.TMP_Text[WheelSlots];
            var font = Load<TMPro.TMP_FontAsset>(LabelFontPath);
            for (int i = 0; i < WheelSlots; i++)
            {
                slots[i] = Image("Slot" + i, node, new Vector2(156f, 62f));
                slots[i].sprite = style.Slice;
                slots[i].type = UnityEngine.UI.Image.Type.Sliced;
                var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                label.rectTransform.SetParent(slots[i].rectTransform, false);
                label.rectTransform.anchorMin = Vector2.zero;
                label.rectTransform.anchorMax = Vector2.one;
                label.rectTransform.sizeDelta = Vector2.zero;
                label.font = font;
                label.fontSize = 24f;
                label.alignment = TMPro.TextAlignmentOptions.Center;
                label.raycastTarget = false;
                label.text = "";
                labels[i] = label;
            }
            var wheel = node.gameObject.AddComponent<MB13.Aipocalypse.Core.UI.CommandWheel>();
            wheel.Setup(style, group, backdrop, slots, labels);
            EditorUtility.SetDirty(wheel);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? "HUD.prefab: SafeFrame/Center/CommandWheel (" + WheelSlots + " slots); paw icon " + (settings.Icon != null) : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    const string StatBarPrefab = "Assets/_Game/_Core/UI/Prefabs/Widgets/StatBar.prefab";
    const string DogBarStylePath = "Assets/_Game/Data/UI/DogToughness.asset";

    // The dog's toughness bar (a StatBar with the paw icon) in HUD.prefab's SafeFrame/BottomLeft, above the vitals.
    public static string DogBar()
    {
        var stamina = Load<MB13.Aipocalypse.Core.UI.StatBarStyle>("Assets/_Game/Data/UI/StaminaBarStyle.asset");
        var style = AssetDatabase.LoadAssetAtPath<MB13.Aipocalypse.Core.UI.StatBarStyle>(DogBarStylePath);
        if (style == null)
        {
            style = Object.Instantiate(stamina);
            AssetDatabase.CreateAsset(style, DogBarStylePath);
        }
        // Its own paw icon in the bar's icon box (the stamina style it starts from has a bolt).
        style.IconSprite = AssetDatabase.LoadAllAssetsAtPath(PawIconPath).OfType<Sprite>().FirstOrDefault();
        Save(style);
        GameObject root = PrefabUtility.LoadPrefabContents(HudPath);
        try
        {
            Transform corner = root.transform.Find("SafeFrame/BottomLeft") ?? throw new Exception("no SafeFrame/BottomLeft");
            Transform old = corner.Find("DogBar");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            var node = new GameObject("DogBar", typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(corner, false);
            node.anchorMin = node.anchorMax = node.pivot = Vector2.zero;
            node.anchoredPosition = new Vector2(0f, 190f);
            node.sizeDelta = new Vector2(260f, 34f);
            node.gameObject.AddComponent<Canvas>();
            var group = node.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var barObject = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(StatBarPrefab), node);
            barObject.name = "Bar";
            var barRect = (RectTransform)barObject.transform;
            barRect.anchorMin = barRect.anchorMax = barRect.pivot = new Vector2(0f, 0.5f);
            barRect.anchoredPosition = Vector2.zero;
            var bar = barObject.GetComponent<MB13.Aipocalypse.Core.UI.StatBar>();
            var so = new SerializedObject(bar);
            so.FindProperty("_style").objectReferenceValue = style;
            so.ApplyModifiedPropertiesWithoutUndo();
            bar.ApplyStyle();
            var icon = barObject.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(i => i.name == "Icon");
            var hud = node.gameObject.AddComponent<MB13.Aipocalypse.Core.DogSystem.DogHud>();
            hud.Setup(group, bar, icon);
            EditorUtility.SetDirty(hud);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath, out bool saved);
            return saved ? "HUD.prefab: SafeFrame/BottomLeft/DogBar (" + DogBarStylePath + ")" : "failed to save " + HudPath;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static UnityEngine.UI.Image Image(string name, RectTransform parent, Vector2 size)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<UnityEngine.UI.Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        image.rectTransform.sizeDelta = size;
        image.raycastTarget = false;
        return image;
    }

    // ---- Helpers ----

    static void SetIfFound(SerializedObject so, string property, int value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p != null)
            p.intValue = value;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        Folder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new Exception("missing " + path);

    static void Save(Object asset)
    {
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssetIfDirty(asset);
    }
}
