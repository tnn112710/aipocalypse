using UnityEngine;

namespace MB13.Aipocalypse.Core.Events
{
    // A barrier (fence segment, gate) was knocked down: the way through it is open.
    public readonly struct BarrierBroken
    {
        public readonly Vector3 Position;
        public readonly bool IsGate;

        public BarrierBroken(Vector3 position, bool isGate)
        {
            Position = position;
            IsGate = isGate;
        }
    }

    // A broken barrier stands again.
    public readonly struct BarrierRestored
    {
        public readonly Vector3 Position;
        public readonly bool IsGate;

        public BarrierRestored(Vector3 position, bool isGate)
        {
            Position = position;
            IsGate = isGate;
        }
    }
}
