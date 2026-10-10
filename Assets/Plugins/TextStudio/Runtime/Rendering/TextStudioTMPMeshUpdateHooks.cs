using System;
using TMPro;

namespace TextStudio
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioTMPMeshUpdateHooks
    {
        internal static event Action<TMP_Text> MeshUpdated;

        internal static void NotifyMeshUpdated(TMP_Text text)
        {
            MeshUpdated?.Invoke(text);
        }
    }
}
