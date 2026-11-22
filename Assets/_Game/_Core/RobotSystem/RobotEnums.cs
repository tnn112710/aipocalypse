namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A one-shot body animation; the value is the animator's EmoteIndex.
    public enum RobotEmote
    {
        None,
        Hello,
        Talk,
        ThumbsUp,
        Laugh,
        DontKnow,
        No,
        Win,
        Dance,
        Jump,
    }

    // What its face screen shows (RobotFaceSet maps each to eye and mouth frames).
    public enum RobotExpression
    {
        Neutral,
        Happy,
        Blink,
        Sleepy,
        Surprised,
        Sad,
        Angry,
        Talk,
        Worried,
    }

    // A robot's beeps (RobotSettings.Sounds).
    public enum RobotSound
    {
        Greet,
        Called,
        Happy,
        Shrug,
        Hit,
        Nervous,
        Wake,
        Chirp,
    }
}
