using MB13.Aipocalypse.Core.TimeSystem;
using UnityEngine;
using VContainer;

namespace MB13.Aipocalypse.Core.Feedback
{
    // Gives Feel feedbacks (plain serialized objects VContainer can't inject) access to our services. Put it on or above the MMF_Players.
    public sealed class FeedbackContext : MonoBehaviour
    {
        public TimeService Time { get; private set; }

        [Inject]
        void Construct(TimeService time) => Time = time;
    }
}
