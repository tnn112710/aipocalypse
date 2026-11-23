using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // Turning on the spot: the collie has no turn clips, so a quick turn while standing plays a slow walk and its legs
    // step round instead of the body sliding.
    public static class DogTurn
    {
        // `yawRate` in degrees per second; `onItsFeet`: standing (not sitting, lying, swimming or in its house).
        public static float AnimatorSpeed(float speed, float yawRate, bool onItsFeet, DogSettings settings) =>
            onItsFeet && speed < settings.TurnStillBelow && Mathf.Abs(yawRate) >= settings.TurnMinRate
                ? Mathf.Max(speed, settings.TurnStepSpeed)
                : speed;
    }
}
