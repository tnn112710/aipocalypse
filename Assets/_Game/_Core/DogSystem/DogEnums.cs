namespace MB13.Aipocalypse.Core.DogSystem
{
    // What the player told the dog to do (the command wheel, the whistle). Saved by number: append only.
    public enum DogOrder
    {
        Follow,
        Stay,
        Herding,
    }

    // Whether it joins fights on its own (Active) or only when sent (Passive). Saved by number: append only.
    public enum DogStance
    {
        Active,
        Passive,
    }

    public enum DogGait
    {
        Still,
        Walk,
        Trot,
        Canter,
        Sprint,
    }

    // The animator's Pose parameter (by number).
    public enum DogPose
    {
        Stand,
        Sit,
        Lie,
        Sleep,
    }

    // One-off acts (the animator's ActIndex, by number).
    public enum DogAct
    {
        Bark,
        Happy,
        HeadTilt,
        ShakeWater,
        Dig,
        Pee,
        Howl,
        Yes,
        No,
        Roll,
        Alert,
        Yawn,
    }

    // Settings keys for its sounds. Append only.
    public enum DogSound
    {
        Bark,
        Whine,
        Yelp,
        Pant,
        Sniff,
        Howl,
    }

    public enum DogWhistle
    {
        Come,
        // This whistle made it annoyed: it comes, slowly and grumpily.
        Annoyed,
        // Still annoyed: it stays put.
        Ignored,
    }
}
