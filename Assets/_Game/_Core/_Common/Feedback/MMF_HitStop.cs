using MoreMountains.Feedbacks;
using UnityEngine;

namespace Aipocalypse.Core.Feedback
{
    [AddComponentMenu("")]
    [System.Serializable]
    [FeedbackPath("Aipocalypse/Hit Stop")]
    [FeedbackHelp("Asks TimeService for a short hit stop. Never sets Time.timeScale itself.")]
    public class MMF_HitStop : MMF_Feedback
    {
        public static bool FeedbackTypeAuthorized = true;

        [MMFInspectorGroup("Hit Stop", true, 12)]
        [Tooltip("Real seconds")] public float Duration = 0.07f;
        [Range(0f, 1f)] public float TimeScale = 0.05f;

        FeedbackContext _context;

        public override float FeedbackDuration { get => ApplyTimeMultiplier(Duration); set => Duration = value; }

        protected override void CustomInitialization(MMF_Player owner)
        {
            base.CustomInitialization(owner);
            _context = owner.GetComponentInParent<FeedbackContext>();
        }

        protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1.0f)
        {
            if (!Active || !FeedbackTypeAuthorized || _context == null || _context.Time == null)
                return;
            _context.Time.RequestHitStop(FeedbackDuration * feedbacksIntensity, TimeScale);
        }
    }
}
