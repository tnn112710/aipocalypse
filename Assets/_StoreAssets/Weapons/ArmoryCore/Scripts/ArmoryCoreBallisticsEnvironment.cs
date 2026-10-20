using UnityEngine;

namespace ArmoryCore
{
    [DisallowMultipleComponent]
    public sealed class ArmoryCoreBallisticsEnvironment : MonoBehaviour
    {
        [Tooltip("World-space wind velocity in metres per second.")]
        public Vector3 windVelocity;
        [Tooltip("1.225 kg/m3 is standard sea-level air at 15 C.")]
        [Min(0f)] public float airDensityKilogramsPerCubicMeter = 1.225f;

        public static Vector3 WindVelocity { get; private set; }
        public static float AirDensity { get; private set; } = 1.225f;

        private void OnEnable() => Apply();
        private void Update() => Apply();

        private void Apply()
        {
            WindVelocity = windVelocity;
            AirDensity = Mathf.Max(0f, airDensityKilogramsPerCubicMeter);
        }

        private void OnDisable()
        {
            WindVelocity = Vector3.zero;
            AirDensity = 1.225f;
        }
    }
}
