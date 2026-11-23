using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Turning: a big turn to face something is walked as a short arc (a dog never pivots on a point); a small one, or one
    // with nowhere to step, turns on the spot, where a quick turn plays a slow walk so its legs step round instead of the
    // body sliding (the collie has no turn clips).
    public static class DogTurn
    {
        // Past this it's no longer worth another arc: the turn in place finishes it.
        public const float ArcDone = 10f;

        public static bool WantsArc(Vector3 forward, Vector3 direction, float arcAngle) => Vector3.Angle(Flat(forward), Flat(direction)) > arcAngle;

        // The arc's landing point: `step` ahead in the wanted direction.
        public static Vector3 Landing(Vector3 from, Vector3 direction, float step) => from + Flat(direction).normalized * step;

        // An arc that keeps it near `anchor` (its stay spot): no farther from it than it already is, once it's more than
        // `near` away.
        public static bool KeepsNear(Vector3 from, Vector3 landing, Vector3 anchor, float near = 1.5f, float slack = 0.3f) =>
            Flat(landing - anchor).magnitude <= Mathf.Max(Flat(from - anchor).magnitude, near) + slack;

        static Vector3 Flat(Vector3 v) => new(v.x, 0f, v.z);

        // `yawRate` in degrees per second; `onItsFeet`: standing (not sitting, lying, swimming or in its house).
        public static float AnimatorSpeed(float speed, float yawRate, bool onItsFeet, DogSettings settings) =>
            onItsFeet && speed < settings.TurnStillBelow && Mathf.Abs(yawRate) >= settings.TurnMinRate
                ? Mathf.Max(speed, settings.TurnStepSpeed)
                : speed;
    }
}
