using MB13.Aipocalypse.Core.SpawnSystem;
using MB13.Aipocalypse.Core.SurfaceSystem;
using UnityEngine;

namespace MB13.Aipocalypse.Core.PlayerSystem
{
    // What the feet are in, as a speed multiplier: water depth (read every frame) or, on dry land, the ground kind (read on
    // each footstep and landing, as the footstep sound is). Water decides when wading, so a muddy bed doesn't slow twice.
    public sealed class PlayerFooting
    {
        readonly PlayerMovementTuning _tuning;
        readonly IWaterLevel _water;
        SurfaceKind? _surface;

        public PlayerFooting(PlayerMovementTuning tuning, IWaterLevel water)
        {
            _tuning = tuning;
            _water = water;
        }

        // Metres of water over the feet; 0 on dry land.
        public float WaterDepth { get; private set; }
        public float SpeedScale { get; private set; } = 1f;
        public bool BlocksSprint => _tuning.GroundSlowdown && WaterDepth > _tuning.WadingSprintDepth;

        public void Tick(Vector3 feet)
        {
            WaterDepth = _water != null && _water.TryGetLevel(feet, out float level) ? Mathf.Max(0f, level - feet.y) : 0f;
            if (!_tuning.GroundSlowdown)
                SpeedScale = 1f;
            else if (WaterDepth > 0f)
                SpeedScale = _tuning.WadingSpeed(WaterDepth);
            else
                SpeedScale = _surface is { } kind ? _tuning.SurfaceSpeed(kind) : 1f;
        }

        // The ground under the feet changed (a footstep or a landing found it); null = nothing found.
        public void OnSurface(SurfaceKind? kind) => _surface = kind;
    }
}
