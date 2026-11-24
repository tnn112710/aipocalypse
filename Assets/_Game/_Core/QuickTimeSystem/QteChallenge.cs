using MB13.Aipocalypse.Input;
using UnityEngine;
using UnityEngine.Serialization;

namespace MB13.Aipocalypse.Core.QuickTimeSystem
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
         Tooltip("Game slow-motion (Time.timeScale, through TimeService) while a standalone QTE runs; ignored when an owner drives it (gun repair). 1 = none. Not the world clock")]
        public float TimeScale = 1f;
        [Tooltip("IDs of compatible QteView widgets. One is chosen for each event.")]
        public string[] Presentations = { "Circle", "Bar" };
        [Tooltip("The press that attempts it; that frame's press goes to the QTE only (no reload, jump… with it)")]
        public GameButton Button = GameButton.Reload;

        // `random`: 0–1 rolls for the session's gameplay randomness (a RandomService stream); null = UnityEngine.Random.
        public abstract IQteSession CreateSession(bool ownerControlled = false, System.Func<float> random = null);
    }
}
