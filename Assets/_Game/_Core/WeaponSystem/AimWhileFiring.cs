using System;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // How well a holder raises the sights while the trigger is held (one level of StatId.AimWhileFiringLevel).
    // The default (level 0) can't: RMB does nothing until the trigger is let go.
    [Serializable]
    public struct AimWhileFiring
    {
        [Range(0.05f, 1f), Tooltip("Aim-in speed while the trigger is held, × the gun's aim speed")]
        public float AimSpeedScale;
        [Min(1f), Tooltip("Recoil kick × while aiming in with the trigger held")]
        public float RecoilScale;
        [Min(1f), Tooltip("Wobble × while aiming in with the trigger held")]
        public float WobbleScale;

        public bool Allowed => AimSpeedScale > 0f;
    }
}
