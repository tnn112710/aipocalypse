using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextStudio
{
    /// <summary>Publishes actual camera output dimensions for pixel-sized retained geometry.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DCameraUniforms
    {
        static readonly int s_pixelsProperty = Shader.PropertyToID("_TextStudio2DCameraPixels");
        static uint s_generation = 1;
        static int s_owners;
        static readonly Camera[] s_recentCameras = new Camera[16];
        static readonly double[] s_cameraTimes = new double[16];
        // Cached delegates keep subscription changes from allocating a delegate per call.
        static readonly Camera.CameraCallback s_beforeCamera = BeforeCamera;
        static readonly Action<ScriptableRenderContext, Camera> s_beforeSrpCamera = BeforeSrpCamera;
        // Starts set so the first release in a domain also clears a uniform left by an earlier domain.
        static bool s_needsRelease = true;

        internal static int OwnerCount => s_owners;

        internal static void Synchronize(ref uint lease, bool required)
        {
            if (lease == s_generation)
            {
                if (required) return;
                s_owners--;
            }
            lease = 0;
            if (required)
            {
                if (s_owners++ == 0)
                {
                    Camera.onPreCull += s_beforeCamera;
                    RenderPipelineManager.beginCameraRendering += s_beforeSrpCamera;
                    s_needsRelease = true;
                }
                lease = s_generation;
            }
            else if (s_owners == 0) Unsubscribe();
        }

        static void BeforeSrpCamera(ScriptableRenderContext context, Camera camera) => BeforeCamera(camera);

        static void BeforeCamera(Camera camera)
        {
            if (camera == null) return;
            int slot = 0;
            int free = -1;
            for (int index = 0; index < s_recentCameras.Length; index++)
            {
                if (s_recentCameras[index] == camera) { free = index; break; }
                if (s_recentCameras[index] == null && free < 0) free = index;
                if (s_cameraTimes[index] < s_cameraTimes[slot]) slot = index;
            }
            if (free >= 0) slot = free;
            s_recentCameras[slot] = camera;
            s_cameraTimes[slot] = Time.realtimeSinceStartupAsDouble;
            Shader.SetGlobalVector(s_pixelsProperty, new Vector4(camera.pixelWidth, camera.pixelHeight, 0f, 1f));
        }

        internal static int AppendRecentCameras(Camera[] destination, int count)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            for (int index = 0; index < s_recentCameras.Length && count < destination.Length; index++)
            {
                Camera camera = s_recentCameras[index];
                if (camera == null || now - s_cameraTimes[index] > 2)
                { s_recentCameras[index] = null; continue; }
                bool exists = false;
                for (int item = 0; item < count; item++) if (destination[item] == camera) { exists = true; break; }
                if (!exists) destination[count++] = camera;
            }
            return count;
        }

        // Owners release every frame they do not need the uniform, so an already released state returns early.
        static void Unsubscribe()
        {
            if (!s_needsRelease) return;
            s_needsRelease = false;
            Camera.onPreCull -= s_beforeCamera;
            RenderPipelineManager.beginCameraRendering -= s_beforeSrpCamera;
            Shader.SetGlobalVector(s_pixelsProperty, Vector4.zero);
            Array.Clear(s_recentCameras, 0, s_recentCameras.Length);
            Array.Clear(s_cameraTimes, 0, s_cameraTimes.Length);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ReleaseAll()
        {
            s_needsRelease = true;
            Unsubscribe();
            s_owners = 0;
            s_generation = s_generation == uint.MaxValue ? 1 : s_generation + 1;
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        static void OnExitingPlayMode() => ReleaseAll();

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void OnCodeUnloading() => ReleaseAll();
#endif
    }
}
