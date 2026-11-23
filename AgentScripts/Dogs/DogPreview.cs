using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using MB13.Aipocalypse.Core.DogSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Renders review sheets of the dog in a preview scene (nothing is saved under Assets): its colour variants, and poses
// sampled from its clips. Args: the PNG path (outside Assets).
//   unity command script run --file AgentScripts/Dogs/DogPreview.cs --entry DogPreview.Colours --args '["C:/.../colours.png"]'
//   unity command script run --file AgentScripts/Dogs/DogPreview.cs --entry DogPreview.Poses --args '["C:/.../poses.png"]'
//   unity command script run --file AgentScripts/Dogs/DogPreview.cs --entry DogPreview.Jumps --args '["C:/.../jumps.png"]'
public static class DogPreview
{
    const string ModelPath = "Assets/_Game/Content/Dogs/BorderCollie/Model/BorderCollie_Model.prefab";
    const string Mats = "Assets/_StoreAssets/Characters/Malbers Animations/Animals Packs/04 Pets/Dog (Border Collie)/Materials Textures/PolyArt/";
    const string Clips = "Assets/_Game/Content/Dogs/BorderCollie/Animations/";
    const string VendorAnims = "Assets/_StoreAssets/Characters/Malbers Animations/Animals Packs/04 Pets/Dog (Border Collie)/Animations/";
    const int Cell = 384;

    static readonly string[] ColourMats =
    {
        "Black/Dog 4x4 Black 01", "Brown/Dog 4x4 Brown 01", "Gold/Dog 4x4 Gold 01", "Ash/Dog 4x4 Ash 01",
        "Blue/Dog 4x4 Blue 01", "Dog 4x4 White 01", "Dog 4x4 Black Yellow", "Dog 4x4 Lion",
    };

    public static string Colours(string png)
    {
        var mats = ColourMats.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Mats + m + ".mat")).ToArray();
        return Sheet(png, mats.Length, (dog, i) =>
        {
            foreach (Renderer r in dog.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = Enumerable.Repeat(mats[i], r.sharedMaterials.Length).ToArray();
            return ColourMats[i].Split('/').Last();
        });
    }

    // Each pose: a clip (our copy under Content/Dogs or a vendor take) and the share of it to sample.
    public static string Poses(string png)
    {
        (string Label, Func<AnimationClip> Clip, float At)[] poses =
        {
            ("idle", () => Vendor("Idle/Dog_Idle"), 0.2f),
            ("walk", () => Ours("Dog_Walk_Forward"), 0.3f),
            ("trot", () => Ours("Dog_Trot_Forward"), 0.3f),
            ("canter", () => Ours("Dog_Canter_Forward"), 0.3f),
            ("sprint", () => Ours("Dog_Sprint_Forward"), 0.3f),
            ("sniff walk", () => Ours("Dog_Smell_Walk_Forward") ?? Vendor("Stances/Smell/Dog_Smell_Walk_Forward"), 0.4f),
            ("sit", () => Vendor("Actions/Dog_Sit_01"), 0.2f),
            ("lie", () => Vendor("Actions/Dog_Lie_01"), 0.2f),
            ("sleep", () => Vendor("Actions/Dog_Sleep_Pose1", "Dog_Sleep_P1"), 0.2f),
            ("swim", () => Ours("Dog_Swim_Forward") ?? Vendor("Swim/Dog_Swim_Forward"), 0.3f),
            ("ledge 2.0", () => Ours("Dog_Ledge_2.0M") ?? Vendor("Ledge_Grab/Dog_Ledge_2.0M"), 0.7f),
            ("happy", () => Vendor("Actions/Dog_Happy"), 0.4f),
        };
        return Sheet(png, poses.Length, (dog, i) =>
        {
            AnimationClip clip = poses[i].Clip();
            if (clip == null)
                return poses[i].Label + " (missing)";
            AnimationMode.StartAnimationMode();
            AnimationMode.SampleAnimationClip(dog, clip, clip.length * poses[i].At);
            return poses[i].Label;
        }, () => AnimationMode.StopAnimationMode());
    }

    // A prop from its four sides (+Z, +X, −Z, −X): which way its front faces. Args: the prefab path, the PNG path.
    public static string Sides(string prefab, string png)
    {
        string[] sides = { "from +Z", "from +X", "from -Z", "from -X" };
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
        return Sheet(png, 4, (dog, i) =>
        {
            Object.DestroyImmediate(dog.GetComponentInChildren<Animator>());
            foreach (Renderer r in dog.GetComponentsInChildren<Renderer>())
                r.enabled = false;
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(go, dog.scene);
            prop.transform.SetParent(dog.transform, false);
            prop.transform.localScale = Vector3.one * 0.7f;
            prop.transform.rotation = Quaternion.Euler(0f, -i * 90f + 40f, 0f);
            return sides[i];
        });
    }

    // Each jump the dog makes (DogJumpArc), as the game plays it: the real controller stepped frame by frame while the body
    // follows the code's path past a box of the obstacle, side view, a row per jump and `Frames` frames across the move.
    public static string Jumps(string png)
    {
        const float Reach = 2.2f; // DogSetup.LinkReach at ModelScale 2: a fence crossing's ends from the fence.
        (string Label, JumpKind Kind, float Rise, float Obstacle, float Across, bool Scramble)[] rows =
        {
            ("leap 2 m fence", JumpKind.Leap, 0f, 2f, Reach * 2f, true),
            ("leap onto 1.5 m block", JumpKind.Leap, 1.5f, 0f, 3f, false),
            ("leap 3 m wall", JumpKind.Leap, 0f, 3f, Reach * 2f, true),
            ("scramble 3.5 m wall", JumpKind.Scramble, 0f, 3.5f, Reach * 2f, true),
            ("hop 0.4 m log", JumpKind.Hop, 0f, 0.4f, 2f, false),
            ("drop 1 m (house door)", JumpKind.DropDown, -1f, 0f, 1.8f, false),
        };
        const int Frames = 8, Size = 256;
        var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Game/Content/Dogs/BorderCollie/BorderCollie.controller");
        var sheet = new Texture2D(Size * Frames, Size * rows.Length, TextureFormat.RGB24, false);
        var target = new RenderTexture(Size, Size, 24) { antiAliasing = 4 };
        var report = new System.Text.StringBuilder();
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40f, -60f, 0f);
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            camera.fieldOfView = 40f;
            camera.targetTexture = target;
            for (int r = 0; r < rows.Length; r++)
            {
                var row = rows[r];
                var props = new GameObject("Props");
                SceneManager.MoveGameObjectToScene(props, scene);
                Vector3 start = Vector3.zero, end = new(0f, row.Rise, row.Across);
                float half = row.Across * 0.5f;
                // The ground before and after the edge (a step when the ends differ in height), and the obstacle at the middle.
                Box(props, new Vector3(0f, Mathf.Min(0f, row.Rise) - 0.1f, (half - 4f) * 0.5f), new Vector3(3f, Mathf.Abs(Mathf.Min(0f, row.Rise)) + 0.1f, half + 4f));
                Box(props, new Vector3(0f, Mathf.Min(0f, row.Rise) - 0.1f, half + 2f), new Vector3(3f, Mathf.Max(0f, row.Rise) + 0.1f, 4f));
                if (row.Obstacle > 0f)
                    Box(props, new Vector3(0f, 0f, half), new Vector3(3f, row.Obstacle, 0.1f));
                camera.transform.position = new Vector3(-9f, 1.6f + row.Rise * 0.5f, row.Across * 0.5f);
                camera.transform.LookAt(new Vector3(0f, 1.1f + row.Rise * 0.5f, row.Across * 0.5f));

                var dog = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
                dog.transform.localScale = Vector3.one * 2f;
                var animator = dog.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                const float Dt = 1f / 60f;
                for (int i = 0; i < 30; i++)
                    animator.Update(Dt);
                // As the game: links (DogJump) wait WindUp with the clip started; the scramble (DogScramble) moves at once.
                float windUp = row.Scramble || row.Kind == JumpKind.Leap ? 0f : 0.15f;
                float peak = DogJumpArc.Peak(row.Kind, start, end, row.Obstacle);
                float duration = DogJumpArc.Duration(row.Kind, row.Across);
                animator.SetInteger("JumpKind", DogJumpArc.AnimatorKind(row.Kind, row.Rise, 2f));
                animator.SetTrigger("Jump");
                float total = windUp + duration + 0.6f;
                int frame = 0;
                report.Append(row.Label).Append(':');
                for (float t = 0f; frame < Frames; t += Dt)
                {
                    float k = Mathf.Clamp01((t - windUp) / duration);
                    dog.transform.position = DogJumpArc.Position(row.Kind, start, end, peak, k);
                    animator.Update(Dt);
                    if (t + 1e-4f < total * frame / (Frames - 1))
                        continue;
                    camera.Render();
                    RenderTexture.active = target;
                    var cell = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                    cell.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    cell.Apply();
                    RenderTexture.active = null;
                    sheet.SetPixels(frame * Size, (rows.Length - 1 - r) * Size, Size, Size, cell.GetPixels());
                    Object.DestroyImmediate(cell);
                    AnimatorClipInfo[] clips = animator.GetCurrentAnimatorClipInfo(0);
                    report.Append($" {t:0.00}s {(clips.Length > 0 ? clips[0].clip.name.Replace("Dog_", "") : "?")}");
                    frame++;
                }
                report.AppendLine();
                Object.DestroyImmediate(dog);
                Object.DestroyImmediate(props);
            }
            File.WriteAllBytes(png, sheet.EncodeToPNG());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(sheet);
            target.Release();
        }
        return png + "\n" + report;
    }

    static void Box(GameObject parent, Vector3 baseCentre, Vector3 size)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(box.GetComponent<Collider>());
        box.transform.SetParent(parent.transform, false);
        box.transform.localScale = size;
        box.transform.position = baseCentre + Vector3.up * size.y * 0.5f;
    }

    static AnimationClip Ours(string name) => AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + name + ".anim");

    static AnimationClip Vendor(string file, string take = null) =>
        AssetDatabase.LoadAllAssetsAtPath(VendorAnims + file + ".fbx").OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__") && (take == null || c.name == take));

    // One dog per cell, three-quarter side view, in a grid four wide; `dress` sets cell i up and returns its label.
    static string Sheet(string png, int count, Func<GameObject, int, string> dress, Action after = null)
    {
        const int Columns = 4;
        int rows = (count + Columns - 1) / Columns;
        var sheet = new Texture2D(Cell * Columns, Cell * rows, TextureFormat.RGB24, false);
        Scene scene = EditorSceneManager.NewPreviewScene();
        var labels = new string[count];
        var target = new RenderTexture(Cell, Cell, 24) { antiAliasing = 4 };
        try
        {
            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            SceneManager.MoveGameObjectToScene(light.gameObject, scene);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.transform.localScale = Vector3.one * 0.5f;
            SceneManager.MoveGameObjectToScene(ground, scene);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            camera.fieldOfView = 30f;
            camera.targetTexture = target;
            camera.transform.position = new Vector3(1.9f, 0.9f, 1.6f);
            camera.transform.LookAt(new Vector3(0f, 0.35f, 0.05f));
            for (int i = 0; i < count; i++)
            {
                var dog = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), scene);
                labels[i] = dress(dog, i);
                camera.Render();
                RenderTexture.active = target;
                var cell = new Texture2D(Cell, Cell, TextureFormat.RGB24, false);
                cell.ReadPixels(new Rect(0, 0, Cell, Cell), 0, 0);
                cell.Apply();
                RenderTexture.active = null;
                int x = i % Columns, y = rows - 1 - i / Columns;
                sheet.SetPixels(x * Cell, y * Cell, Cell, Cell, cell.GetPixels());
                Object.DestroyImmediate(cell);
                Object.DestroyImmediate(dog);
                after?.Invoke();
            }
            File.WriteAllBytes(png, sheet.EncodeToPNG());
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            Object.DestroyImmediate(sheet);
            target.Release();
        }
        return png + ": " + string.Join(" | ", labels.Select((l, i) => $"{i + 1} {l}"));
    }
}
