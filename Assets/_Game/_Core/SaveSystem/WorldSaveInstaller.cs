using MB13.Aipocalypse.Core.Bootstrap;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.SaveSystem
{
    // Scene system: makes this scene a world the game saves and loads (restore on the first frame, the morning autosave).
    // Without it the scene still plays, but nothing is restored or autosaved there.
    public sealed class WorldSaveInstaller : SceneInstaller
    {
        public override string Problem => null;

        public override void Install(IContainerBuilder builder) => builder.RegisterEntryPoint<WorldSaver>();
    }
}
