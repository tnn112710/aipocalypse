// Animation Rigging helpers for `unity command script run`. Nothing here touches the open scene:
// prefabs are edited in an isolated copy (PrefabUtility.LoadPrefabContents).
//
//   Describe(prefabPath)                          Animators, RigBuilder layers, and every Rig with its weight and
//                                                 constraints in evaluation order (type, path, weight, data fields) as JSON
//   Bake(prefabPath, clipPath, outPath, fps, overwrite = false)
//                                                 plays `clipPath` on the prefab's Animator with its rigs applied and
//                                                 records every bone (all transforms under the Animator that are not
//                                                 inside a Rig) into a new .anim at `outPath`; loop settings and events
//                                                 are copied. The result needs no rig at runtime. Generic avatars only.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.Playables;

public static class RigKit
{
    public static string Describe(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return Fail($"no prefab at {prefabPath}");
        var sb = new StringBuilder("{");
        sb.Append($"\"prefab\":{Json(prefabPath)},\"animators\":[");
        var first = true;
        foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
        {
            sb.Append(first ? "" : ",");
            first = false;
            var avatar = animator.avatar;
            sb.Append($"{{\"path\":{Json(PathOf(animator.transform, prefab.transform))},\"avatar\":{Json(avatar ? avatar.name : null)},\"human\":{B(avatar && avatar.isHuman)},\"controller\":{Json(animator.runtimeAnimatorController ? animator.runtimeAnimatorController.name : null)},\"culling\":{Json(animator.cullingMode.ToString())}");
            var builder = animator.GetComponent<RigBuilder>();
            sb.Append($",\"rigBuilder\":{(builder ? $"{{\"enabled\":{B(builder.enabled)},\"layers\":{JsonList(builder.layers.Select(l => (l.rig ? PathOf(l.rig.transform, prefab.transform) : "null") + (l.active ? "" : " (inactive)")))}}}" : "null")}");
            sb.Append(",\"rigs\":[");
            var firstRig = true;
            foreach (Rig rig in animator.GetComponentsInChildren<Rig>(true))
            {
                sb.Append(firstRig ? "" : ",");
                firstRig = false;
                sb.Append($"{{\"path\":{Json(PathOf(rig.transform, prefab.transform))},\"weight\":{F(rig.weight)},\"constraints\":[");
                var firstConstraint = true;
                // Same order the package evaluates: depth-first under the Rig.
                foreach (var constraint in rig.GetComponentsInChildren<IRigConstraint>(true))
                {
                    var component = (MonoBehaviour)constraint;
                    sb.Append(firstConstraint ? "" : ",");
                    firstConstraint = false;
                    sb.Append($"{{\"type\":{Json(component.GetType().Name)},\"path\":{Json(PathOf(component.transform, prefab.transform))},\"weight\":{F(constraint.weight)},\"valid\":{B(constraint.IsValid())},\"data\":{DataJson(component, prefab.transform)}}}");
                }
                sb.Append("]}");
            }
            sb.Append("]}");
        }
        return sb.Append("]}").ToString();
    }

    public static string Bake(string prefabPath, string clipPath, string outPath, int fps, bool overwrite = false)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
            return Fail($"no prefab at {prefabPath}");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null)
            return Fail($"no clip at {clipPath}");
        if (!outPath.EndsWith(".anim") || !outPath.StartsWith("Assets/"))
            return Fail("outPath must be an Assets/... .anim path");
        if (!overwrite && AssetDatabase.LoadAssetAtPath<Object>(outPath) != null)
            return Fail($"{outPath} exists (pass overwrite = true)");
        if (fps <= 0)
            fps = Mathf.RoundToInt(clip.frameRate);

        var root = PrefabUtility.LoadPrefabContents(prefabPath);
        PlayableGraph graph = default;
        RigBuilder builder = null;
        try
        {
            builder = root.GetComponentInChildren<RigBuilder>(true);
            if (builder == null)
                return Fail("no RigBuilder in the prefab");
            var animator = builder.GetComponent<Animator>();
            if (animator.avatar && animator.avatar.isHuman)
                return Fail("humanoid avatar: the recorded transform curves would not play on it (bake on a Generic copy, or write muscles with HumanPoseHandler)");

            var rigRoots = builder.layers.Where(l => l.rig).Select(l => l.rig.transform).ToArray();
            var bones = animator.GetComponentsInChildren<Transform>(true)
                .Where(t => t != animator.transform && !rigRoots.Any(r => t == r || t.IsChildOf(r)))
                .ToArray();

            graph = PlayableGraph.Create("RigKit Bake");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var clipPlayable = AnimationClipPlayable.Create(graph, clip);
            clipPlayable.SetApplyFootIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Bake", animator);
            output.SetSourcePlayable(clipPlayable);
            // Build(graph) adds one extra output per rig chain on top of ours, so evaluate the whole graph;
            // AnimationMode.SamplePlayableGraph samples a single output and would skip the rig.
            if (!builder.Build(graph))
                return Fail("RigBuilder.Build failed (no Animator or no layers)");

            var recorder = new GameObjectRecorder(animator.gameObject);
            foreach (var bone in bones)
                recorder.BindComponent(bone);

            var frames = Mathf.Max(1, Mathf.CeilToInt(clip.length * fps));
            for (var f = 0; f <= frames; f++)
            {
                // A looping clip wraps to frame 0 at exactly its length.
                clipPlayable.SetTime(Mathf.Min(f / (float)fps, clip.length - 0.0001f));
                builder.SyncLayers();
                graph.Evaluate(0f);
                recorder.TakeSnapshot(f == 0 ? 0f : 1f / fps);
            }

            var baked = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(outPath) };
            recorder.SaveToClip(baked, fps, new CurveFilterOptions
            {
                keyframeReduction = true,
                unrollRotation = true,
                positionError = 0.5f,
                rotationError = 0.5f,
                scaleError = 0.5f,
                floatError = 0.5f,
            });
            AnimationUtility.SetAnimationClipSettings(baked, AnimationUtility.GetAnimationClipSettings(clip));
            AnimationUtility.SetAnimationEvents(baked, AnimationUtility.GetAnimationEvents(clip));

            var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
            if (existing != null)
            {
                // Keep the GUID so controllers and override controllers stay wired.
                EditorUtility.CopySerialized(baked, existing);
                existing.name = baked.name;
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(baked, outPath);
            }
            return $"{{\"ok\":true,\"out\":{Json(outPath)},\"bones\":{bones.Length},\"frames\":{frames + 1},\"curves\":{AnimationUtility.GetCurveBindings(baked).Length}}}";
        }
        finally
        {
            if (builder != null)
                builder.Clear();
            if (graph.IsValid())
                graph.Destroy();
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static string DataJson(Object component, Transform root)
    {
        var so = new SerializedObject(component);
        var data = so.FindProperty("m_Data");
        if (data == null)
            return "null";
        var sb = new StringBuilder("{");
        var end = data.GetEndProperty();
        var it = data.Copy();
        var first = true;
        var enter = true;
        while (it.NextVisible(enter) && !SerializedProperty.EqualContents(it, end))
        {
            var value = Value(it, root);
            enter = value == null;
            if (value == null)
                continue;
            sb.Append(first ? "" : ",");
            first = false;
            sb.Append($"{Json(it.propertyPath.Replace("m_Data.", ""))}:{value}");
        }
        return sb.Append('}').ToString();
    }

    // Leaf values only; null means "descend into children".
    static string Value(SerializedProperty p, Transform root)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.ObjectReference:
                var o = p.objectReferenceValue;
                return Json(o is Component c ? PathOf(c.transform, root) : o ? o.name : null);
            case SerializedPropertyType.Float:
                return F(p.floatValue);
            case SerializedPropertyType.Integer:
                return p.intValue.ToString();
            case SerializedPropertyType.Boolean:
                return B(p.boolValue);
            case SerializedPropertyType.Enum:
                return Json(p.enumDisplayNames.ElementAtOrDefault(p.enumValueIndex));
            case SerializedPropertyType.Vector2:
            case SerializedPropertyType.Vector3:
            case SerializedPropertyType.Vector4:
                return Json(p.boxedValue?.ToString());
            default:
                return null;
        }
    }

    static string PathOf(Transform t, Transform root)
    {
        if (t == root)
            return "";
        var names = new List<string>();
        for (var x = t; x != null && x != root; x = x.parent)
            names.Add(x.name);
        names.Reverse();
        return string.Join("/", names);
    }

    static string Fail(string message) => $"{{\"ok\":false,\"error\":{Json(message)}}}";
    static string B(bool b) => b ? "true" : "false";
    static string F(float f) => f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    static string Json(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string JsonList(IEnumerable<string> items) => "[" + string.Join(",", items.Select(Json)) + "]";
}
