using MB13.Aipocalypse.Core.QuickTimeSystem;
using MB13.Aipocalypse.Input;

namespace MB13.Aipocalypse.Core.Events
{
    public readonly struct QteAttempted
    {
        public readonly IQteSession Session;
        public readonly QteAttemptResult Result;
        public QteAttempted(IQteSession session, QteAttemptResult result) { Session = session; Result = result; }
    }

    public readonly struct QteStarted
    {
        public readonly IQteSession Session;
        public readonly string Presentation;
        // What to press (QteChallenge.Button).
        public readonly GameButton Button;

        public QteStarted(IQteSession session, string presentation, GameButton button)
        {
            Session = session;
            Presentation = presentation;
            Button = button;
        }
    }

    public readonly struct QteEnded
    {
        public readonly IQteSession Session;
        public readonly QteResult Result;
        public QteEnded(IQteSession session, QteResult result) { Session = session; Result = result; }
    }
}
