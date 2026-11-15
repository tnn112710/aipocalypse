// Edit-mode renders of the first-person hands / arms, for `unity command script run`. Nothing in the open scene changes;
// images go to Temp/ArmReview/.
//   Hand(side, curl, spread, tip)             the HandSet's hand alone, three views in one image
//   Gun(weapon, clip, progress, tag)          the player's view of a gun with both hands; clip = Idle, Reloading,
//                                             ReloadingEmpty, Repairing, RepairingStubborn or Cycling
//   Strip(weapon, clip, frames, tag)          the same as a grid of `frames` views through the clip
//   A tag holding "@left", "@right", "@top" or "@below" looks from outside instead of the player's eye.
using System;
using System.IO;
using MB13.Aipocalypse.Core.HandSystem;
using MB13.Aipocalypse.Core.WeaponSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class ArmReview
{
    const string OutFolder = "Temp/ArmReview";
    const string HandSetPath = "Assets/_Game/Data/Hands/HandSet.asset";
    const float FieldOfView = 70f;

    public static string Hand(string side, float curl, float spread, float tip)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var set = AssetDatabase.LoadAssetAtPath<HandSet>(HandSetPath);
            HandRig rig = Spawn(set.Get(side == "Left" ? HandSide.Left : HandSide.Right), scene);
            var finger = new FingerShape(curl, spread, tip);
            rig.Apply(new HandShape { Thumb = finger, Index = finger, Middle = finger, Ring = finger, Little = finger });
            Transform grip = rig.GripPoint;
            Light(scene);
            Camera camera = NewCamera(scene, 30f);
            var views = new[] { -grip.up, grip.up, grip.forward, -grip.forward };
            var image = new Texture2D(480 * views.Length, 480, TextureFormat.RGB24, false);
            for (int i = 0; i < views.Length; i++)
            {
                Vector3 up = Mathf.Abs(Vector3.Dot(views[i], grip.forward)) > 0.9f ? grip.up : grip.forward;
                camera.transform.SetPositionAndRotation(grip.position - views[i] * 0.55f, Quaternion.LookRotation(views[i], up));
                Blit(camera, image, i * 480, 0, 480, 480);
            }
            return Save(image, $"Hand_{side}_{curl:0.0}_{spread:0.0}_{tip:0.0}");
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static string View = "";

    public static string Gun(string weapon, string clip, float progress, string tag)
    {
        View = tag ?? "";
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        string error = Views(weapon, clip, new[] { progress }, image, 1, 1280, 720);
        return error ?? Save(image, $"{weapon}_{clip}_{progress:0.00}{tag}");
    }

    public static string Strip(string weapon, string clip, int frames, string tag)
    {
        View = tag ?? "";
        int columns = Mathf.Min(frames, 4), rows = Mathf.CeilToInt(frames / (float)columns);
        var image = new Texture2D(columns * 480, rows * 270, TextureFormat.RGB24, false);
        var times = new float[frames];
        for (int i = 0; i < frames; i++)
            times[i] = frames > 1 ? i / (float)(frames - 1) : 0f;
        string error = Views(weapon, clip, times, image, columns, 480, 270);
        return error ?? Save(image, $"{weapon}_{clip}_strip{tag}");
    }

    static string Views(string weapon, string clip, float[] times, Texture2D image, int columns, int width, int height)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        GunAnimation animation = null;
        try
        {
            var set = AssetDatabase.LoadAssetAtPath<HandSet>(HandSetPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Content/Weapons/{weapon}/{weapon}.prefab");
            if (prefab == null)
                return "no gun prefab " + weapon;
            GameObject gun = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(gun, scene);
            // The camera sits at the origin looking down +Z: view space is world space.
            gun.transform.position = set.PreviewItemPosition;
            var held = new SerializedObject(gun.GetComponent<HeldGun>());
            var pivot = (Transform)held.FindProperty("_posePivot").objectReferenceValue;
            var hip = (Transform)held.FindProperty("_hipPose").objectReferenceValue;
            pivot.SetLocalPositionAndRotation(hip.localPosition, hip.localRotation);
            Transform world = gun.transform.Find("World");
            if (world != null)
                world.gameObject.SetActive(false);

            var hands = gun.GetComponent<ItemHands>();
            var rigs = new HandRig[2];
            for (int i = 0; i < 2; i++)
            {
                rigs[i] = Spawn(set.Get((HandSide)i), scene);
                rigs[i].transform.localScale *= hands.HandScale;
            }
            Light(scene);
            Camera camera = NewCamera(scene, FieldOfView);
            // Debug views from outside the player's eye: tag "@left", "@right", "@top" or "@below".
            Vector3 focus = set.PreviewItemPosition + new Vector3(0f, 0f, -0.15f);
            if (View.Contains("@left")) camera.transform.SetPositionAndRotation(focus + Vector3.left * 1.1f, Quaternion.LookRotation(Vector3.right));
            if (View.Contains("@right")) camera.transform.SetPositionAndRotation(focus + Vector3.right * 1.1f, Quaternion.LookRotation(Vector3.left));
            if (View.Contains("@top")) camera.transform.SetPositionAndRotation(focus + Vector3.up * 1.1f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            if (View.Contains("@below")) camera.transform.SetPositionAndRotation(focus + Vector3.down * 1.1f, Quaternion.LookRotation(Vector3.up, Vector3.forward));

            animation = gun.GetComponentInChildren<GunAnimation>();
            if (animation != null)
            {
                animation.Show();
                Start(animation, clip);
            }
            int rows = Mathf.CeilToInt(times.Length / (float)columns);
            for (int i = 0; i < times.Length; i++)
            {
                if (animation != null)
                    animation.Tick(1f / 60f, times[i]);
                for (int side = 0; side < 2; side++)
                {
                    HandGrip grip = hands.GetGrip((HandSide)side);
                    if (grip == null)
                    {
                        rigs[side].SetVisible(false);
                        continue;
                    }
                    Vector3 elbow = hands.Carry(set.Elbow((HandSide)side));
                    grip.Resolve(rigs[side], elbow, out Vector3 position, out Quaternion rotation);
                    rigs[side].Place(position, rotation, hands.Carry(set.Shoulder((HandSide)side)), elbow);
                    rigs[side].Apply(grip.Shape);
                }
                Blit(camera, image, i % columns * width, (rows - 1 - i / columns) * height, width, height);
            }
            return null;
        }
        finally
        {
            if (animation != null)
                animation.Stop();
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void Start(GunAnimation animation, string clip)
    {
        switch (clip)
        {
            case "Reloading": animation.StartAction(GunAction.Reloading, false); break;
            case "ReloadingEmpty": animation.StartAction(GunAction.Reloading, true); break;
            case "Repairing": animation.StartAction(GunAction.ClearingJam, false); break;
            case "RepairingStubborn": animation.StartAction(GunAction.ClearingJam, false, true); break;
            case "Cycling": animation.StartAction(GunAction.Cycling, false); break;
        }
    }

    static HandRig Spawn(HandRig prefab, Scene scene)
    {
        HandRig rig = Object.Instantiate(prefab);
        SceneManager.MoveGameObjectToScene(rig.gameObject, scene);
        return rig;
    }

    static void Light(Scene scene)
    {
        var key = new GameObject("Key", typeof(Light));
        SceneManager.MoveGameObjectToScene(key, scene);
        var light = key.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.4f;
        key.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
        var fill = new GameObject("Fill", typeof(Light));
        SceneManager.MoveGameObjectToScene(fill, scene);
        light = fill.GetComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.5f;
        fill.transform.rotation = Quaternion.Euler(-20f, 150f, 0f);
    }

    static Camera NewCamera(Scene scene, float fieldOfView)
    {
        var go = new GameObject("Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.GetComponent<Camera>();
        camera.scene = scene;
        camera.fieldOfView = fieldOfView;
        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = 20f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.32f, 0.36f, 0.4f);
        return camera;
    }

    static void Blit(Camera camera, Texture2D image, int x, int y, int width, int height)
    {
        var target = new RenderTexture(width, height, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), x, y);
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(target);
        }
    }

    static string Save(Texture2D image, string name)
    {
        image.Apply();
        Directory.CreateDirectory(OutFolder);
        string path = $"{OutFolder}/{name}.png";
        File.WriteAllBytes(path, image.EncodeToPNG());
        Object.DestroyImmediate(image);
        return Path.GetFullPath(path);
    }
}
