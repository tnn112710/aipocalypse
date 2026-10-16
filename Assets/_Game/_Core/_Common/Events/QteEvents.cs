using Aipocalypse.Core.QuickTimeSystem;

namespace Aipocalypse.Core.Events
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
        public QteStarted(IQteSession session, string presentation) { Session = session; Presentation = presentation; }
    }

    public readonly struct QteEnded
    {
        public readonly IQteSession Session;
        public readonly QteResult Result;
        public QteEnded(IQteSession session, QteResult result) { Session = session; Result = result; }
    }
}
