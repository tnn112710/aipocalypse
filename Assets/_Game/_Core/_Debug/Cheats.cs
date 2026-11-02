namespace Aipocalypse.Core.Debugging
{
    // Whether this build has the dev console, its commands and the cheat hotkeys: the editor, and players built with
    // ENABLE_CHEAT (every development build, and distribution builds with Cheat on; BUILD.md).
    public static class Cheats
    {
#if UNITY_EDITOR || ENABLE_CHEAT
        public static bool Enabled => true;
#else
        public static bool Enabled => false;
#endif
    }
}
