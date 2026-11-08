using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Input
{
    public sealed class InputInstaller : IInstaller
    {
        readonly ControlSettings _controls;

        public InputInstaller(ControlSettings controls) => _controls = controls;

        public void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_controls);
            builder.RegisterEntryPoint<InputService>().AsSelf();
        }
    }
}
