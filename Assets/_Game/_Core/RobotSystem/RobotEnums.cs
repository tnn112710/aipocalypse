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
        Cry,
        // Boxing stance and a double punch: brave for a moment.
        Brave,
        LookAround,
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
        Dizzy,
        Love,
        Excited,
        Yawn,
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

    // How a hit landed: a flinch, a knockdown (enough hits in a row), or nothing (it's already down).
    public enum RobotHit
    {
        None,
        Flinch,
        Knockdown,
    }

    // How it takes the end of a raid.
    public enum RobotCheer
    {
        None,
        Win,
        Cry,
    }

    // Why it walks over to the player.
    public enum RobotCallReason
    {
        Whistle,
        Welcome,
    }
}
