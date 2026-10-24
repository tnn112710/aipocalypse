using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio
{
    /// <summary>
    /// The cameras that may show retained text, captured once per player frame for the projected pixel demand of every text.
    /// Settled styled text revalidates that demand each frame; querying every camera once per text was the largest idle cost
    /// of static styled labels on mobile CPUs. Edit Mode captures on every call because editor ticks do not advance frames.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class TextStudio2DCameraSnapshot
    {
        internal struct Entry
        {
            internal Camera Camera;
            internal Matrix4x4 WorldToClip;
            internal Vector2 Pixels;
            internal int CullingMask;
            internal Scene Scene;
            internal ulong SceneCullingMask;
            internal bool IsSceneView;
        }

        static Entry[] s_entries = new Entry[4];
        static Camera[] s_cameras = new Camera[32];
        static int s_count;
        static int s_frame = -1;
        static ulong s_fingerprint;

        /// <summary>Changes whenever a captured camera, its output size or its visibility filters change.</summary>
        internal static ulong Fingerprint
        {
            get
            {
                Capture();
                return s_fingerprint;
            }
        }

        internal static int Count
        {
            get
            {
                Capture();
                return s_count;
            }
        }

        internal static ref readonly Entry At(int index) => ref s_entries[index];

        static void Capture()
        {
            int frame = Time.frameCount;
            if (frame == s_frame && Application.isPlaying)
                return;
            s_frame = frame;
            int active = Camera.allCamerasCount;
            if (s_cameras.Length < active + 16)
                Array.Resize(ref s_cameras, Mathf.NextPowerOfTwo(active + 16));
            int count = Camera.GetAllCameras(s_cameras);
            count = TextStudio2DCameraUniforms.AppendRecentCameras(s_cameras, count);
            if (s_entries.Length < count)
                Array.Resize(ref s_entries, Mathf.NextPowerOfTwo(count));
            TextStudioDeterministicHash64 hash = default;
            int captured = 0;
            for (int index = 0; index < count; index++)
            {
                Camera camera = s_cameras[index];
                s_cameras[index] = null;
                if (camera == null || camera.pixelWidth <= 0 || camera.pixelHeight <= 0)
                    continue;
                ref Entry entry = ref s_entries[captured++];
                entry.Camera = camera;
                entry.WorldToClip = camera.projectionMatrix * camera.worldToCameraMatrix;
                entry.Pixels = new Vector2(camera.pixelWidth, camera.pixelHeight);
                entry.CullingMask = camera.cullingMask;
                entry.Scene = camera.scene;
                entry.IsSceneView = camera.cameraType == CameraType.SceneView;
#if UNITY_EDITOR
                entry.SceneCullingMask = camera.overrideSceneCullingMask != 0
                    ? camera.overrideSceneCullingMask
                    : camera.gameObject.sceneCullingMask;
#endif
                hash.Add(camera.GetHashCode());
                for (int element = 0; element < 16; element++)
                    hash.Add(entry.WorldToClip[element]);
                hash.Add(entry.Pixels.x);
                hash.Add(entry.Pixels.y);
                hash.Add(entry.CullingMask);
                hash.Add(entry.Scene.GetHashCode());
                hash.Add(entry.SceneCullingMask);
                hash.Add(entry.IsSceneView);
            }

            // Released entries must not keep destroyed cameras alive.
            for (int index = captured; index < s_count; index++)
                s_entries[index] = default;
            s_count = captured;
            s_fingerprint = hash.Value;
        }
    }
}
