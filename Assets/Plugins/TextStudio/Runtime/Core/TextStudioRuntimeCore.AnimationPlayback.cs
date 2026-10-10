using UnityEngine;

namespace TextStudio
{
#if UNITY_6000_7_OR_NEWER
    // Each Play session uses a fresh animation identity, matching BeginAnimationSession.
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioRuntimeCore
    {
        static object s_animationSession = new object();
        object _animationSession = s_animationSession;
        bool _animationPaused;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BeginAnimationSession() => s_animationSession = new object();

        public bool IsAnimationPaused
        {
            get
            {
                if (!ReferenceEquals(_animationSession, s_animationSession))
                {
                    _animationSession = s_animationSession;
                    _animationPaused = false;
                }
                return _animationPaused;
            }
        }

        public void PauseAnimation() { _ = IsAnimationPaused; _animationPaused = true; }
        public void ResumeAnimation() { _ = IsAnimationPaused; _animationPaused = false; }
        internal float ResolvePresentationDeltaTime(float deltaTime) => IsAnimationPaused ? 0f : Mathf.Max(0f, deltaTime);
    }
}
