using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Editor.Build
{
    // One kind of player build, listed and built from Autobuild > Build Window (BUILD.md).
    [CreateAssetMenu(menuName = "Aipocalypse/Build Config", fileName = "BuildConfig")]
    public sealed class BuildConfig : ScriptableObject
    {
        public BuildTarget Target = BuildTarget.StandaloneWindows64;

        [Tooltip("Development Build: debugger, profiler hooks and cheats. Slower than distribution: don't judge frame times on it")]
        public bool Development;

        [Tooltip("Auto-connects the Profiler at start. Makes a development build")]
        public bool Profiler;

        [Tooltip("ENABLE_CHEAT: the dev console, its commands and cheat hotkeys. Always on in development builds")]
        public bool Cheat;

        [Tooltip("The Unity Pipeline server in the player, so the unity CLI can drive it (runtime commands). A loopback HTTP " +
            "server that runs code: QA builds only, never one you hand out")]
        public bool Pipeline;

        [Tooltip("Deletes the folders Unity marks as not to ship (*_BackUpThisFolder_ButDontShipItWithYourGame, " +
            "*_BurstDebugInformation_DoNotShip) after the build. Smaller, but this build's crash call stacks can't be symbolicated")]
        public bool DeleteDoNotShipFolders = true;

        [Tooltip("Also zips the build folder next to it, without the folders Unity marks as not to ship")]
        public bool Zip;

        public bool IsDevelopment => Development || Profiler;
        public bool HasCheat => Cheat || IsDevelopment;

        [ShowInInspector, ReadOnly, PropertyOrder(-1)]
        string BuildName => AutoBuild.GetBuildName(this);

        [Button(ButtonSizes.Large), PropertySpace]
        void Build() => AutoBuild.BuildLater(this);
    }
}
