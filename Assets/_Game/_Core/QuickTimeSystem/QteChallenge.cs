using UnityEngine;
using UnityEngine.Serialization;

namespace Aipocalypse.Core.QuickTimeSystem
{
    public enum QteResult { Running, Success, TimedOut, Cancelled }
    public enum QteAttemptResult { Ignored, Miss, Success }

    public interface IQteSession
    {
        QteResult Result { get; }
        float Remaining { get; }
        void Tick(float unscaledDeltaTime);
        QteAttemptResult Press();
        void Cancel();
    }

    public abstract class QteChallenge : ScriptableObject
    {
        [Range(0.01f, 1f), FormerlySerializedAs("WorldTimeScale"),
         Tooltip("Game slow-motion (Time.timeScale, through TimeService) while a standalone QTE runs; 1 = none. Not the world clock")]
        public float TimeScale = 1f;
        [Tooltip("IDs of compatible QteView widgets. One is chosen for each event.")]
        public string[] Presentations = { "Circle", "Bar" };

        public abstract IQteSession CreateSession(bool ownerControlled = false);
    }
}
