// Vendor-neutral character helpers for `unity command script run`. Nothing here touches the open scene.
//
//   Describe(prefabPath)                          renderers, bones, blendshapes, animator facts (JSON)
//   Snapshot(prefabPath, pngPath, clipPath, time, yaw, size)
//                                                 offscreen render of a prefab, optionally posed by a humanoid clip
//                                                 at `time` seconds ("" = bind pose); view the PNG to check the look
//   ClipInfo(fbxOrAnimPath)                       clips in a model/anim file: length, loop, root-motion bake flags,
//                                                 average speed (m/s) and turn rate, for picking clips and thresholds
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CharacterKit
{
    public static string Describe(string prefabPath)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return Fail($"no prefab at {prefabPath}");
        var sb = new StringBuilder("{");
        sb.Append($"\"prefab\":{Json(prefabPath)},\"transforms\":{prefab.GetComponentsInChildren<Transform>(true).Length}");
        foreach (Animator animator in prefab.GetComponentsInChildren<Animator>(true))
        {
            Avatar avatar = animator.avatar;
            sb.Append($",\"animator\":{{\"path\":{Json(PathOf(animator.transform, prefab.transform))},\"avatar\":{Json(avatar ? AssetDatabase.GetAssetPath(avatar) + ":" + avatar.name : null)},\"human\":{B(avatar && avatar.isHuman)},\"controller\":{Json(animator.runtimeAnimatorController ? AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) : null)},\"culling\":{Json(animator.cullingMode.ToString())},\"rootMotion\":{B(animator.applyRootMotion)},\"hasTransformHierarchy\":{B(animator.hasTransformHierarchy)}}}");
        }
        sb.Append(",\"renderers\":[");
        bool first = true;
        foreach (Renderer r in prefab.GetComponentsInChildren<Renderer>(true))
        {
            sb.Append(first ? "" : ",");
            first = false;
            sb.Append($"{{\"path\":{Json(PathOf(r.transform, prefab.transform))},\"type\":{Json(r.GetType().Name)},\"materials\":{JsonList(r.sharedMaterials.Select(m => m ? m.name + " (" + m.shader.name + ")" : "null"))},\"shadows\":{Json(r.shadowCastingMode.ToString())}");
            if (r is SkinnedMeshRenderer smr)
            {
                Mesh mesh = smr.sharedMesh;
                int weighted = 0;
                if (mesh != null)
                    for (int i = 0; i < mesh.blendShapeCount; i++)
                        if (smr.GetBlendShapeWeight(i) != 0f)
                            weighted++;
                sb.Append($",\"mesh\":{Json(mesh ? AssetDatabase.GetAssetPath(mesh) : null)},\"vertices\":{(mesh ? mesh.vertexCount : 0)},\"bones\":{smr.bones.Length},\"uniqueBones\":{smr.bones.Distinct().Count()},\"blendShapes\":{(mesh ? mesh.blendShapeCount : 0)},\"nonZeroBlendShapeWeights\":{weighted},\"updateWhenOffscreen\":{B(smr.updateWhenOffscreen)},\"motionVectors\":{B(smr.skinnedMotionVectors)},\"readable\":{B(mesh && mesh.isReadable)},\"boundsSize\":{Json(smr.localBounds.size.ToString("F2"))}");
            }
            sb.Append('}');
        }
        return sb.Append("]}").ToString();
    }

    public static string Snapshot(string prefabPath, string pngPath, string clipPath, float time, float yaw, int size)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return Fail($"no prefab at {prefabPath}");
        AnimationClip clip = string.IsNullOrEmpty(clipPath) ? null : LoadClip(clipPath);
        if (!string.IsNullOrEmpty(clipPath) && clip == null)
            return Fail($"no clip at {clipPath}");

        var preview = new PreviewRenderUtility();
        try
        {
            // Straight into the preview scene (the active scene would get dirty), unpacked so its hierarchy can change.
            GameObject instance = preview.InstantiatePrefabInScene(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, yaw, 0f));
            var animator = instance.GetComponentInChildren<Animator>();
            // An optimized hierarchy only skins while an Animator updates, which never happens in a preview scene: restore
            // the bones on this throwaway instance (the prefab stays optimized).
            if (animator != null && !animator.hasTransformHierarchy)
                AnimatorUtility.DeoptimizeTransformHierarchy(animator.gameObject);
            if (clip != null && animator != null)
                clip.SampleAnimation(animator.gameObject, time);

            Bounds bounds = WorldBounds(instance);
            float radius = Mathf.Max(bounds.extents.magnitude, 0.5f);
            Camera camera = preview.camera;
            camera.fieldOfView = 30f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.32f, 0.36f, 0.4f, 1f);
            float distance = radius / Mathf.Sin(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.position = bounds.center + new Vector3(0f, radius * 0.2f, distance);
            camera.transform.LookAt(bounds.center);
            preview.lights[0].intensity = 1.2f;
            preview.lights[0].transform.rotation = Quaternion.Euler(40f, 160f, 0f);
            preview.lights[1].intensity = 0.6f;
            preview.ambientColor = new Color(0.35f, 0.35f, 0.38f);

            var rect = new Rect(0, 0, size, size);
            preview.BeginStaticPreview(rect);
            preview.Render(true);
            Texture2D image = preview.EndStaticPreview();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pngPath)));
            File.WriteAllBytes(pngPath, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            return $"{{\"ok\":true,\"png\":{Json(pngPath)},\"bounds\":{Json(bounds.size.ToString("F2"))},\"clip\":{Json(clip ? clip.name : null)}}}";
        }
        catch (Exception e)
        {
            return Fail(e.GetType().Name + ": " + e.Message);
        }
        finally
        {
            preview.Cleanup();
        }
    }

    public static string ClipInfo(string path)
    {
        var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToList();
        if (clips.Count == 0)
            return Fail($"no clips in {path}");
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        var sb = new StringBuilder("[");
        for (int i = 0; i < clips.Count; i++)
        {
            AnimationClip clip = clips[i];
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            float speed = clip.length > 0f ? clip.averageSpeed.magnitude : 0f;
            sb.Append(i == 0 ? "" : ",");
            sb.Append($"{{\"name\":{Json(clip.name)},\"length\":{clip.length:F3},\"loop\":{B(settings.loopTime)},\"human\":{B(clip.humanMotion)},\"averageSpeed\":{speed:F3},\"averageAngularSpeedDeg\":{clip.averageAngularSpeed * Mathf.Rad2Deg:F1},\"bakeRotation\":{B(settings.loopBlendOrientation)},\"bakeY\":{B(settings.loopBlendPositionY)},\"bakeXZ\":{B(settings.loopBlendPositionXZ)},\"additiveReference\":{Json(settings.additiveReferencePoseClip ? settings.additiveReferencePoseClip.name : null)}}}");
        }
        sb.Append(']');
        if (importer != null)
            sb.Insert(0, $"{{\"animationType\":{Json(importer.animationType.ToString())},\"avatarSource\":{Json(importer.sourceAvatar ? AssetDatabase.GetAssetPath(importer.sourceAvatar) : null)},\"clips\":").Append('}');
        return sb.ToString();
    }

    static AnimationClip LoadClip(string path)
    {
        // "file.fbx" (first clip) or "file.fbx#ClipName"
        string[] parts = path.Split('#');
        var clips = AssetDatabase.LoadAllAssetsAtPath(parts[0]).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"));
        return parts.Length > 1 ? clips.FirstOrDefault(c => c.name == parts[1]) : clips.FirstOrDefault();
    }

    static Bounds WorldBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return new Bounds(go.transform.position, Vector3.one);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);
        return bounds;
    }

    static string PathOf(Transform t, Transform root) => t == root ? "" : (t.parent == root ? t.name : PathOf(t.parent, root) + "/" + t.name);
    static string B(bool b) => b ? "true" : "false";
    static string Fail(string message) => $"{{\"ok\":false,\"error\":{Json(message)}}}";
    static string Json(string s) => s == null ? "null" : "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    static string JsonList(IEnumerable<string> items) => "[" + string.Join(",", items.Select(Json)) + "]";
}
