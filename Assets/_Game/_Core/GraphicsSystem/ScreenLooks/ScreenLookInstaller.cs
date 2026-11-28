using MB13.Aipocalypse.Core.Bootstrap;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace MB13.Aipocalypse.Core.GraphicsSystem.ScreenLooks
{
    // Scene system: ScreenLookService (entry point) with the settings' backend. Systems that want a look (sanity) resolve the
    // service optionally; remove this component and every look is off.
    public sealed class ScreenLookInstaller : SceneInstaller
    {
        [SerializeField, InlineEditor] ScreenLookSettings _settings;

        public override string Problem => _settings == null ? "no Screen Look Settings assigned" : null;

        public override void Install(IContainerBuilder builder)
        {
            builder.RegisterInstance(_settings);
            builder.RegisterEntryPoint<ScreenLookService>().AsSelf();
        }
    }
}
