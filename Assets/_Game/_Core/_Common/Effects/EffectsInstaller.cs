using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.Effects
{
    // Root scope: the one EffectPool every scene's guns, melee and enemies spawn their effects from, ticked centrally.
    public sealed class EffectsInstaller : IInstaller
    {
        public void Install(IContainerBuilder builder) => builder.RegisterEntryPoint<EffectPool>().AsSelf();
    }
}
