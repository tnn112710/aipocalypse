using UnityEngine;

namespace TextStudio
{
    /// <summary>Hidden play-mode host whose LateUpdate ticks every enabled Text Studio hub.</summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    internal sealed class TextStudioHubSchedulerDriver : MonoBehaviour
    {
        internal static TextStudioHubSchedulerDriver Create()
        {
            GameObject host = new GameObject("Text Studio Hub Scheduler");
            host.hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable;
            DontDestroyOnLoad(host);
            return host.AddComponent<TextStudioHubSchedulerDriver>();
        }

        void LateUpdate()
        {
            TextStudioHubScheduler.Tick();
        }

        void OnDestroy()
        {
            TextStudioHubScheduler.ReleaseDriver(this);
        }
    }
}
