using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.WeaponSystem;
using Object = UnityEngine.Object;

public static class GunFraming
{
    const string Guns = "Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/";
    static readonly string[] Names = { "AK47", "MP5", "Pistol", "PumpShotgun", "DoubleBarrel" };

    public static object Inspect()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/PlayerSystem/Prefabs/Player.prefab");
        var anchor = player.transform.Find("CameraRoot/CameraPitch/HeldItems/Hand");
        var set = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
        return new { anchor = anchor.localPosition.ToString("F3"), preview = set.PreviewItemPosition.ToString("F3"),
            sceneDirty = EditorSceneManager.GetActiveScene().isDirty,
            guns = Names.Select(name => {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(Guns + name + ".prefab");
                return new { name, rootPosition = root.transform.localPosition.ToString("F3"), poses = root.transform.Find("Poses").Cast<Transform>().Select(t => new {
                    t.name, position = t.localPosition.ToString("F3"), rotation = t.localEulerAngles.ToString("F2") }).ToArray() };
            }).ToArray() };
    }

    public static object InspectMP5()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Guns + "MP5.prefab");
        var model = root.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon");
        return new[] { "Sight", "Barrel" }.Select(name => {
            var part = model.Find(name);
            return new { name, position = part.localPosition.ToString("F6"), rotation = part.localEulerAngles.ToString("F4"),
                vertices = part.GetComponent<MeshFilter>().sharedMesh.vertices.Select(v => model.InverseTransformPoint(part.TransformPoint(v)))
                    .Where(v => name == "Sight" || (Mathf.Abs(v.x) < 0.004f && v.y > 0.12f)).Select(v => new { x = (float)Math.Round(v.x, 5), y = (float)Math.Round(v.y, 5), z = (float)Math.Round(v.z, 5) }).Distinct().ToArray() };
        }).ToArray();
    }

    public static object SightIslands()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Guns + "MP5.prefab");
        var sight = root.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon/Sight");
        var mesh = sight.GetComponent<MeshFilter>().sharedMesh;
        var vertices = mesh.vertices;
        var indices = mesh.triangles;
        var parent = Enumerable.Range(0, vertices.Length).ToArray();
        int Find(int i) { while (parent[i] != i) i = parent[i]; return i; }
        void Join(int a, int b) { parent[Find(a)] = Find(b); }
        var welded = vertices.Select((v, i) => new { v, i }).GroupBy(p => new Vector3Int(Mathf.RoundToInt(p.v.x * 100000f), Mathf.RoundToInt(p.v.y * 100000f), Mathf.RoundToInt(p.v.z * 100000f)));
        foreach (var group in welded) foreach (var entry in group) Join(group.First().i, entry.i);
        for (int i = 0; i < indices.Length; i += 3) { Join(indices[i], indices[i + 1]); Join(indices[i], indices[i + 2]); }
        return Enumerable.Range(0, vertices.Length).GroupBy(Find).Select(g => {
            var points = g.Select(i => vertices[i] + sight.localPosition).Distinct().ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            return new { island = g.Key, count = g.Count(), center = bounds.center.ToString("F6"), size = bounds.size.ToString("F6"), points = points.Where(p => Mathf.Abs(p.x) < 0.004f).Select(p => p.ToString("F6")).ToArray() };
        }).ToArray();
    }

    public static object FixMP5()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        const string folder = "Assets/_Game/_Core/WeaponSystem/Meshes";
        const string meshPath = folder + "/MP5_Sight.asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) != null)
            throw new InvalidOperationException("MP5 sight already authored; adjust the game mesh or poses directly.");
        var root = PrefabUtility.LoadPrefabContents(Guns + "MP5.prefab");
        try
        {
            var model = root.transform.Find("PosePivot/RecoilPivot/AnimationRoot/Motion/Weapon");
            var sight = model.Find("Sight");
            var filter = sight.GetComponent<MeshFilter>();
            var source = filter.sharedMesh;
            var vertices = source.vertices;
            var parent = Enumerable.Range(0, vertices.Length).ToArray();
            int Find(int i) { while (parent[i] != i) i = parent[i]; return i; }
            void Join(int a, int b) { parent[Find(a)] = Find(b); }
            foreach (var group in vertices.Select((v, i) => new { v, i }).GroupBy(p => new Vector3Int(Mathf.RoundToInt(p.v.x * 100000f), Mathf.RoundToInt(p.v.y * 100000f), Mathf.RoundToInt(p.v.z * 100000f))))
                foreach (var entry in group) Join(group.First().i, entry.i);
            var triangles = source.triangles;
            for (int i = 0; i < triangles.Length; i += 3) { Join(triangles[i], triangles[i + 1]); Join(triangles[i], triangles[i + 2]); }
            var drum = Enumerable.Range(0, vertices.Length).GroupBy(Find).OrderByDescending(g => g.Max(i => vertices[i].y)).First().ToArray();
            Vector3 Closest(Vector3 point) => vertices.Select(v => v + sight.localPosition).OrderBy(v => (v - point).sqrMagnitude).First();
            var rear = (Closest(new Vector3(0.003665f, 0.157590f, -0.049822f)) + Closest(new Vector3(0.003665f, 0.161426f, -0.048033f))) * 0.5f;
            var front = (Closest(new Vector3(0.003665f, 0.149337f, -0.032125f)) + Closest(new Vector3(0.003665f, 0.153173f, -0.030336f))) * 0.5f;
            rear.x = front.x = 0f;
            var turn = Quaternion.FromToRotation((front - rear).normalized, Vector3.forward);
            var center = (front + rear) * 0.5f - sight.localPosition;
            const float sightHeight = 0.15924f;
            var lift = Vector3.up * (sightHeight - (front.y + rear.y) * 0.5f);
            var mesh = Object.Instantiate(source);
            mesh.name = "MP5_Sight";
            var normals = mesh.normals;
            var tangents = mesh.tangents;
            // Level the disconnected aperture drum while keeping its fixed mounting base seated on the receiver.
            foreach (int i in drum)
            {
                vertices[i] = center + turn * (vertices[i] - center) + lift;
                if (normals.Length == vertices.Length) normals[i] = turn * normals[i];
                if (tangents.Length == vertices.Length)
                {
                    var direction = turn * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    tangents[i] = new Vector4(direction.x, direction.y, direction.z, tangents[i].w);
                }
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            if (tangents.Length == vertices.Length) mesh.tangents = tangents;
            mesh.RecalculateBounds();
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_Game/_Core/WeaponSystem", "Meshes");
            AssetDatabase.CreateAsset(mesh, meshPath);
            filter.sharedMesh = mesh;
            var aim = root.transform.Find("Poses/Aim");
            aim.localPosition = new Vector3(-0.17f, 0.21f - sightHeight, -0.49f);
            aim.localRotation = Quaternion.identity;
            var hip = root.transform.Find("Poses/Hip");
            hip.localPosition = new Vector3(hip.localPosition.x, hip.localPosition.y, -0.40f);
            var sprint = root.transform.Find("Poses/Sprint");
            sprint.localPosition = new Vector3(sprint.localPosition.x, sprint.localPosition.y, -0.18f);
            root.transform.Find("PosePivot").SetLocalPositionAndRotation(hip.localPosition, hip.localRotation);
            PrefabUtility.SaveAsPrefabAsset(root, Guns + "MP5.prefab");
            AssetDatabase.SaveAssets();
            var newRear = center + turn * (rear - sight.localPosition - center) + lift + sight.localPosition;
            var newFront = center + turn * (front - sight.localPosition - center) + lift + sight.localPosition;
            return new { source = AssetDatabase.GetAssetPath(source), mesh = meshPath, drumVertices = drum.Length,
                correctionDegrees = Quaternion.Angle(Quaternion.identity, turn), apertureSlope = (newFront - newRear).normalized.ToString("F6"),
                apertureHeight = newRear.y, frontPostHeight = sightHeight, aim = aim.localPosition.ToString("F5") };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static object CheckLiveMP5Aim()
    {
        var held = Object.FindAnyObjectByType<HeldGun>();
        if (held == null || held.Gun.Item.Gun.name != "MP5") throw new InvalidOperationException("Equip MP5 first.");
        bool paused = EditorApplication.isPaused;
        EditorApplication.isPaused = true;
        try
        {
            held.Gun.Interrupt();
            held.Tick(new Aipocalypse.Core.EquipmentSystem.HeldItemInput { IsReady = true, AimHeld = true }, 5f);
            var animation = held.GetComponentInChildren<GunAnimation>();
            animation.EndAction();
            animation.Tick(0.001f, 0f);
            var player = Object.FindAnyObjectByType<Aipocalypse.Core.PlayerSystem.Player>();
            var eye = ((IWeaponUser)player).Eye;
            var model = animation.transform.Find("Motion/Weapon");
            var rear = eye.InverseTransformPoint(model.TransformPoint(new Vector3(0f, 0.15924f, -0.04f)));
            var front = eye.InverseTransformPoint(model.TransformPoint(new Vector3(0f, 0.15924f, 0.43244f)));
            float angle = Vector3.Angle(eye.forward, model.forward);
            var sight = model.Find("Sight");
            float nearest = sight.GetComponent<MeshFilter>().sharedMesh.vertices.Min(v => eye.InverseTransformPoint(sight.TransformPoint(v)).z);
            if (Mathf.Abs(rear.x) > 0.00002f || Mathf.Abs(rear.y) > 0.00002f || Mathf.Abs(front.x) > 0.00002f || Mathf.Abs(front.y) > 0.00002f || angle > 0.01f || nearest < 0.02f)
                throw new Exception($"ADS alignment failed: rear={rear:F6}, front={front:F6}, angle={angle}, nearest={nearest}.");
            return new { rear = rear.ToString("F6"), frontPost = front.ToString("F6"), axisErrorDegrees = angle, nearestSightSurfaceMetres = nearest };
        }
        finally { EditorApplication.isPaused = paused; }
    }

    public static object LowerMP5Hip()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var root = PrefabUtility.LoadPrefabContents(Guns + "MP5.prefab");
        try
        {
            var hip = root.transform.Find("Poses/Hip");
            var position = hip.localPosition;
            hip.localPosition = new Vector3(position.x, 0.04f, position.z);
            root.transform.Find("PosePivot").SetLocalPositionAndRotation(hip.localPosition, hip.localRotation);
            PrefabUtility.SaveAsPrefabAsset(root, Guns + "MP5.prefab");
            return new { hip = hip.localPosition.ToString("F5"), aim = root.transform.Find("Poses/Aim").localPosition.ToString("F5") };
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static object Apply()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before saving poses.");
        var changed = new System.Collections.Generic.List<object>();
        foreach (string name in Names)
        {
            var path = Guns + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var pivot = root.transform.Find("PosePivot");
                var markers = root.transform.Find("Poses").Cast<Transform>().ToArray();
                var preview = markers.OrderBy(p => Vector3.Distance(p.localPosition, pivot.localPosition) + Quaternion.Angle(p.localRotation, pivot.localRotation)).First();
                foreach (var marker in markers)
                {
                    var previous = marker.localPosition;
                    float z = marker.name switch {
                        "Hip" => name == "Pistol" ? -0.114f : -0.40f,
                        "Aim" => name == "Pistol" ? -0.34f : name == "AK47" ? -0.46f : name == "MP5" ? -0.49f : -0.44f,
                        "Sprint" => name == "MP5" ? -0.18f : -0.10f,
                        _ => previous.z
                    };
                    marker.localPosition = new Vector3(previous.x, previous.y, z);
                    changed.Add(new { weapon = name, pose = marker.name, before = previous.ToString("F4"), after = marker.localPosition.ToString("F4") });
                }
                pivot.SetLocalPositionAndRotation(preview.localPosition, preview.localRotation);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        return changed;
    }

    public static object Review()
    {
        var sheet = new Texture2D(5 * 480, 3 * 270, TextureFormat.RGB24, false);
        try
        {
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < Names.Length; col++)
            {
                string pose = row == 1 ? "Aim" : "Hip";
                string action = row == 2 ? "ReloadingEmpty" : "Idle";
                float progress = row == 2 ? 0.5f : 0f;
                Render(Names[col], 0f, pose, action, progress);
                string path = "Temp/GunFraming/" + Names[col] + "_" + pose + "_" + action + "_" + progress.ToString("F2") + "_0.00.png";
                var frame = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try
                {
                    frame.LoadImage(File.ReadAllBytes(path));
                    for (int y = 0; y < 270; y++)
                    for (int x = 0; x < 480; x++)
                        sheet.SetPixel(col * 480 + x, (2 - row) * 270 + y, frame.GetPixel(x * 2, y * 2));
                }
                finally { Object.DestroyImmediate(frame); }
            }
            sheet.Apply();
            var output = "Temp/GunFraming/closer-guns.png";
            File.WriteAllBytes(output, sheet.EncodeToPNG());
            return new { output, columns = Names, rows = new[] { "Hip", "Aim", "ReloadingEmpty at 0.5" } };
        }
        finally { Object.DestroyImmediate(sheet); }
    }

    public static object Render(string name, float pullback = 0f, string pose = "Hip", string action = "Idle", float progress = 0f)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        GunAnimation animation = null;
        var output = "Temp/GunFraming";
        Directory.CreateDirectory(output);
        try
        {
            var set = AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
            var item = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Guns + name + ".prefab"), scene);
            item.transform.position = set.PreviewItemPosition;
            var marker = item.transform.Find("Poses/" + pose);
            var pivot = item.transform.Find("PosePivot");
            pivot.SetLocalPositionAndRotation(marker.localPosition + Vector3.back * pullback, marker.localRotation);
            animation = item.GetComponentInChildren<GunAnimation>();
            if (action == "Idle") animation.EndAction();
            else animation.StartAction(action == "Repairing" ? GunAction.ClearingJam : GunAction.Reloading, action == "ReloadingEmpty");
            animation.Tick(0.001f, progress);
            var hands = item.GetComponent<ItemHands>();
            foreach (var side in new[] { HandSide.Left, HandSide.Right })
            {
                var hand = Object.Instantiate(set.Get(side));
                SceneManager.MoveGameObjectToScene(hand.gameObject, scene);
                hand.transform.localScale *= hands.HandScale;
                var grip = hands.GetDefault(side);
                grip.Resolve(hand, set.Elbow(side), out var p, out var r);
                hand.Place(p, r);
                hand.Apply(grip.Shape);
            }
            var lightGo = new GameObject("Light");
            SceneManager.MoveGameObjectToScene(lightGo, scene);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.transform.rotation = Quaternion.Euler(35f, -30f, 0f);
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
                var path = output + "/" + name + "_" + pose + "_" + action + "_" + progress.ToString("F2") + "_" + pullback.ToString("F2") + ".png";
                File.WriteAllBytes(path, frame.EncodeToPNG());
                return new { path };
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(frame);
            }
        }
        finally
        {
            if (animation != null) animation.Stop();
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
