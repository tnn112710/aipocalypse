using UnityEngine;

namespace MB13.Aipocalypse.Core.EquipmentSystem
{
    // What the holder wants from the item in hand this frame, plus the holder state items react to.
    public struct HeldItemInput
    {
        public bool UseHeld;
        public bool UsePressed;
        public bool AimHeld;
        public bool ReloadPressed;
        // Switch the item's light (a gun's flashlight part).
        public bool LightPressed;

        // Fully raised; false while switching. Set by the rig.
        public bool IsReady;
        public bool IsSprinting;
        public bool IsMoving;
        public bool IsAirborne;
    }

    // Camera offset an item adds on top of the look (gun recoil).
    public readonly struct ViewKick
    {
        public readonly Vector3 Euler;
        public readonly Vector3 Offset;

        public ViewKick(Vector3 euler, Vector3 offset)
        {
            Euler = euler;
            Offset = offset;
        }
    }
}
