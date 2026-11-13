using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // A held item's scope view for the HUD: how much of it shows (0 none .. 1 full) and the reticle drawn in it.
    public readonly struct ScopeView
    {
        // From here the scope view covers the screen and the held model is no longer drawn.
        const float HideHeldFrom = 0.99f;

        public ScopeView(float weight, Sprite reticle)
        {
            Weight = weight;
            Reticle = reticle;
        }

        public float Weight { get; }
        public Sprite Reticle { get; }
        public bool HidesHeld => Weight >= HideHeldFrom;
    }
}
