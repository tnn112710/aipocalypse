using CodeStage.AdvancedFPSCounter;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MB13.Aipocalypse.Core.Debugging
{
    // Advanced FPS Counter through the console (editor and development builds): off whenever a scene loads, afps.show / afps.hide.
    // Its own hotkey is cleared (it defaults to `, the console's key) and it draws under the console. The plugin's Instance is
    // used because the counter is a scene object no scope knows about.
    [CommandGroup("afps")]
    public sealed class FpsCounterCommands : ConsoleCommands
    {
        // Under the console overlay (1000), over the HUD (10) and the death screen (100).
        const int SortingOrder = 999;

        public FpsCounterCommands(CommandRegistry registry) : base(registry)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Tame(false);
        }

        public override void Dispose()
        {
            base.Dispose();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        [Command("Show the FPS counter (adds one to a scene without it)")]
        void Show()
        {
            if (AFPSCounter.Instance == null)
                AFPSCounter.AddToScene(false);
            Tame(true);
        }

        [Command("Hide the FPS counter")]
        void Hide() => Tame(false);

        void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Tame(false);

        static void Tame(bool shown)
        {
            AFPSCounter counter = AFPSCounter.Instance;
            if (counter == null)
                return;
            counter.hotKey = KeyCode.None;
            counter.SortingOrder = SortingOrder;
            counter.OperationMode = shown ? OperationMode.Normal : OperationMode.Disabled;
        }
    }
}
