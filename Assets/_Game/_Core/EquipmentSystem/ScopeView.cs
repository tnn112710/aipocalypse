using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // A held item's scope view for the HUD: how much of it shows (0 none .. 1 full), the reticle drawn in it, and how far the
    // scope sits off the eye's line (in shares of its eye box: sway, kick, not yet up), which darkens the view's edge.
    public readonly struct ScopeView
    {
        // From here the scope view covers the screen and the held model is no longer drawn.
        const float HideHeldFrom = 0.99f;

        public ScopeView(float weight, Sprite reticle, Vector2 eyeOffset = default)
        {
            Weight = weight;
            Reticle = reticle;
            EyeOffset = eyeOffset;
        }

        public float Weight { get; }
        public Sprite Reticle { get; }
        // Right / up of the eye's line; 1 = at the edge of the eye box.
        public Vector2 EyeOffset { get; }
        public bool HidesHeld => Weight >= HideHeldFrom;
    }
}
