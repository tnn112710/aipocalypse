using UnityEngine;

namespace TextStudio
{
    /// <summary>Closes shared Appearance resources before native player teardown and Editor code reload.</summary>
#if UNITY_6000_7_OR_NEWER
    // Shutdown state remains set while late component cleanup releases stale handles.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioAppearanceResources
    {
        internal static bool IsShuttingDown { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void BeginSession()
        {
            IsShuttingDown = false;
            Application.quitting -= Shutdown;
            Application.quitting += Shutdown;
        }

        internal static void Shutdown()
        {
            if (IsShuttingDown) return;
            IsShuttingDown = true;
            ReleaseShared();
        }

        internal static void ReleaseShared()
        {
            TextStudio2DHeroSubmission.Flush();
            TextStudioColorFieldCache.ResetRuntimeState();
            TextStudio2DMappingTable.ResetRuntimeState();
            TextStudio2DSurfaceTable.ReleaseShared();
            TextStudio2DStyleTable.ResetSharedTable();
            TextStudio2DTextureArrayTable.ResetRuntimeState();
            TextStudioWritingGpuTable.ResetShared();
            TextStudio2DHeroTargetPool.ResetRuntimeState();
            TextStudio2DCameraUniforms.ReleaseAll();
            TextStudio2DGpuFallbackResources.Release();
            TextStudioAppearanceMemory.ResetSession();
        }

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void OnCodeUnloading()
        {
            Application.quitting -= Shutdown;
            Shutdown();
        }
#endif
    }
}
