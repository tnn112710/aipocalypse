using UnityEngine;

namespace Aipocalypse.Core.QuickTimeSystem
{
    public enum QteResult { Running, Success, TimedOut, Cancelled }

    public interface IQteSession
    {
        QteResult Result { get; }
        float Remaining { get; }
        void Tick(float unscaledDeltaTime);
        void Press();
        void Cancel();
    }

    public abstract class QteChallenge : ScriptableObject
    {
        [Range(0.01f, 1f)] public float WorldTimeScale = 1f;
        [Tooltip("IDs of compatible QteView widgets. One is chosen for each event.")]
        public string[] Presentations = { "Circle", "Bar" };

        public abstract IQteSession CreateSession(bool ownerControlled = false);
    }
}
