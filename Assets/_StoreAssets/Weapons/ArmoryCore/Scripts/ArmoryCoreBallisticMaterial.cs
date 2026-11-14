using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreBallisticMaterial : MonoBehaviour
    {
        [Tooltip("Approximate material thickness along the surface normal.")]
        [Min(0.01f)] public float thicknessMillimeters = 10f;
        [Tooltip("Energy removed per millimetre of projectile path. Wood ~10-30, mild steel ~150-300.")]
        [Min(0f)] public float penetrationEnergyPerMillimeter = 25f;
        [Range(0f, 1f)] public float penetrationRandomness = 0.08f;
        [Range(0f, 89.9f)] public float minimumRicochetAngleFromNormal = 70f;
        [Range(0f, 1f)] public float ricochetChanceAtGrazingAngle = 0.6f;
        [Range(0f, 2f)] public float receivedDamageMultiplier = 1f;
    }
}
