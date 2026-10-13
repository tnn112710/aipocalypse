using Aipocalypse.Core.Entities;
using VContainer;

namespace Aipocalypse.Core.UI
{
    // On the HUD prefab root: injects every view in the HUD from the scene scope (or the root), so the HUD works dropped into any scene.
    public sealed class HudScope : EntityScope
    {
        protected override void RegisterParts(IContainerBuilder builder)
        {
        }
    }
}
