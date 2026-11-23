// Checks the dog's clip copies against the vendor clips (editor, no Play). Rerun after a Malbers pack update or a
// DogSetup.Controller change: DOGS.md → "Vendor update".
//   unity command script run --file AgentScripts/Dogs/DogClipCheck.cs --entry DogClipCheck.Roots
//   unity command script run --file AgentScripts/Dogs/DogClipCheck.cs --entry DogClipCheck.Accuracy
//   unity command script run --file AgentScripts/Dogs/DogClipCheck.cs --entry DogClipCheck.Paws
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DogClipCheck
{
    const string Controller = "Assets/_Game/Content/Dogs/BorderCollie/BorderCollie.controller";
    const string Copies = "Assets/_Game/Content/Dogs/BorderCollie/Animations";
    const string Vendor = "Assets/_StoreAssets/Characters/Malbers Animations/Animals Packs/04 Pets/Dog (Border Collie)/Animations";
    const string Model = "Assets/_Game/Content/Dogs/BorderCollie/Model/BorderCollie_Model.prefab";
    const string RootBone = "Root";
    // DogSetup.Standing.z: what every non-pose clip should average.
    const float Standing = 0.237f;
    static readonly string[] PawBones = { "L Finger0", "R Finger0", "L Toe0", "R Toe0" };
    static readonly string[] PoseClips = { "Dog_Sit_From_Idle", "Dog_Sit_to_Idle", "Dog_Sit_to_Lie", "Dog_Lie_to_Sit" };

    // Every state's clip: where it puts the body (the Root bone's start and mean x, z). "vendor" means a clip played as is
    // (its Root is dropped as root motion: the body lands on the Animator's default); "OFF" a non-pose clip off Standing.
    // The sit / lie / sleep chain and the knockdown leave Standing on purpose, each starting where the last ended.
    public static string Roots()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Controller);
        var sb = new StringBuilder();
        foreach (ChildAnimatorState s in controller.layers[0].stateMachine.states)
        {
            if (s.state.motion is BlendTree tree)
                foreach (ChildMotion c in tree.children)
                    RootLine(sb, $"{s.state.name}[{c.threshold:0.00}]", c.motion as AnimationClip, false);
            else
                RootLine(sb, s.state.name, s.state.motion as AnimationClip, Posed(s.state.name));
        }
        return sb.ToString();
    }

    static bool Posed(string state) => state is "SitDown" or "Sit" or "SitUp" or "SitToLie" or "Lie" or "LieToSit" or "LieToSleep"
        or "Sleep" or "SleepToLie" || state.StartsWith("Down ") || state.StartsWith("Rise ");

    static void RootLine(StringBuilder sb, string label, AnimationClip clip, bool posed)
    {
        if (clip == null)
            return;
        bool vendor = AssetDatabase.GetAssetPath(clip).EndsWith(".fbx");
        AnimationCurve x = RootCurve(clip, "x"), z = RootCurve(clip, "z");
        if (z == null)
        {
            sb.AppendLine($"{label,-22} {clip.name,-28} {(vendor ? "vendor" : "copy")} no Root curve{(vendor ? "" : "  OFF")}");
            return;
        }
        float mx = 0f, mz = 0f;
        const int Samples = 60;
        for (int i = 0; i < Samples; i++)
        {
            float t = clip.length * i / Samples;
            mx += x?.Evaluate(t) ?? 0f;
            mz += z.Evaluate(t);
        }
        mx /= Samples;
        mz /= Samples;
        bool off = vendor || (!posed && (Mathf.Abs(mz - Standing) > 0.005f || Mathf.Abs(mx) > 0.005f));
        sb.AppendLine($"{label,-22} {clip.name,-28} {(vendor ? "vendor" : "copy  ")} start ({x?.Evaluate(0f) ?? 0f:0.000},{z.Evaluate(0f):0.000}) " +
                      $"mean ({mx:0.000},{mz:0.000}){(off ? "  OFF" : "")}");
    }

    static AnimationCurve RootCurve(AnimationClip clip, string axis) =>
        AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(RootBone, typeof(Transform), "m_LocalPosition." + axis));

    // Each copy sampled against its vendor clip on the model, every bone but Root (placed on purpose): the worst rotation
    // and position difference. After DogSetup.Reduce: within about 0.35° and 1.5 mm.
    public static string Accuracy()
    {
        var vendor = new Dictionary<string, AnimationClip>();
        foreach (string fbx in Directory.GetFiles(Vendor, "*.fbx", SearchOption.AllDirectories))
        {
            string file = Path.GetFileNameWithoutExtension(fbx).Replace(' ', '_');
            foreach (AnimationClip c in AssetDatabase.LoadAllAssetsAtPath(fbx.Replace(Path.DirectorySeparatorChar, '/')).OfType<AnimationClip>()
                         .Where(c => !c.name.StartsWith("__")))
            {
                vendor.TryAdd(c.name.Replace(' ', '_'), c);
                vendor.TryAdd(file, c);
            }
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        var sb = new StringBuilder();
        float worstRot = 0f, worstPos = 0f;
        string worstRotClip = "", worstPosClip = "";
        int compared = 0;
        try
        {
            Transform[] bones = go.GetComponentsInChildren<Transform>().Where(t => t != go.transform && t.name != RootBone).ToArray();
            foreach (string path in Directory.GetFiles(Copies, "*.anim"))
            {
                var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(path.Replace(Path.DirectorySeparatorChar, '/'));
                // Copies named for their use (DogSetup's copyName) map back to their vendor clip here.
                string vendorName = copy.name == "Dog_Attack_Bite_Pin" ? "Dog_Attack_Bite_Forward_v2" : copy.name;
                if (!vendor.TryGetValue(vendorName, out AnimationClip source))
                {
                    sb.AppendLine("  no vendor clip named like " + copy.name);
                    continue;
                }
                compared++;
                float rot = 0f, pos = 0f;
                for (int i = 0; i < 40; i++)
                {
                    float t = copy.length * i / 40f;
                    source.SampleAnimation(go, t);
                    Quaternion[] r = bones.Select(b => b.localRotation).ToArray();
                    Vector3[] p = bones.Select(b => b.localPosition).ToArray();
                    copy.SampleAnimation(go, t);
                    for (int b = 0; b < bones.Length; b++)
                    {
                        rot = Mathf.Max(rot, Quaternion.Angle(r[b], bones[b].localRotation));
                        pos = Mathf.Max(pos, (p[b] - bones[b].localPosition).magnitude);
                    }
                }
                if (rot > worstRot) { worstRot = rot; worstRotClip = copy.name; }
                if (pos > worstPos) { worstPos = pos; worstPosClip = copy.name; }
                if (rot > 0.5f || pos > 0.003f)
                    sb.AppendLine($"  {copy.name}: {rot:0.00}°, {pos * 1000f:0.0} mm");
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
        return $"compared {compared}: worst rotation {worstRot:0.000}° ({worstRotClip}), worst position {worstPos * 1000f:0.00} mm ({worstPosClip})\n" + sb;
    }

    // How far each paw slides while on the ground (within 3 cm of its lowest) through the pose transitions, at model scale
    // 1, with the Root as copied and with it held at the first frame (how the copies were before keepRoot). Kept should be
    // the smaller: sit down was 6 / 8 / 27 / 18 cm (front L, R, hind L, R) against 22 / 13 / 45 / 31 held.
    public static string Paws()
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        var sb = new StringBuilder();
        try
        {
            go.transform.localScale = Vector3.one;
            Transform[] all = go.GetComponentsInChildren<Transform>();
            Transform[] feet = PawBones.Select(n => all.First(t => t.name == n)).ToArray();
            foreach (string name in PoseClips)
            {
                var copy = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{Copies}/{name}.anim");
                AnimationClip held = Object.Instantiate(copy);
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(held).Where(b => b.path == RootBone && b.propertyName.StartsWith("m_LocalPosition.")))
                {
                    AnimationCurve c = AnimationUtility.GetEditorCurve(held, b);
                    AnimationUtility.SetEditorCurve(held, b, AnimationCurve.Constant(0f, held.length, c.Evaluate(0f)));
                }
                sb.AppendLine($"{name,-18} root kept: {Slide(go, copy, feet)}   root held: {Slide(go, held, feet)}");
                Object.DestroyImmediate(held);
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
        return sb.ToString();
    }

    static string Slide(GameObject go, AnimationClip clip, Transform[] feet)
    {
        const int Steps = 60;
        var at = new Vector3[feet.Length, Steps + 1];
        for (int s = 0; s <= Steps; s++)
        {
            clip.SampleAnimation(go, clip.length * s / Steps);
            for (int f = 0; f < feet.Length; f++)
                at[f, s] = feet[f].position;
        }
        var parts = new string[feet.Length];
        for (int f = 0; f < feet.Length; f++)
        {
            float low = float.MaxValue;
            for (int s = 0; s <= Steps; s++)
                low = Mathf.Min(low, at[f, s].y);
            float slide = 0f;
            for (int s = 1; s <= Steps; s++)
            {
                if (at[f, s].y > low + 0.03f || at[f, s - 1].y > low + 0.03f)
                    continue;
                Vector3 d = at[f, s] - at[f, s - 1];
                d.y = 0f;
                slide += d.magnitude;
            }
            parts[f] = $"{slide * 100f:0}";
        }
        return string.Join(" / ", parts) + " cm";
    }
}
