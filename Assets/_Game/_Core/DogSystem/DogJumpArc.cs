using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The animator's JumpKind: 0 hop, 1 scramble, 2–5 climb (by ledge height: 0.5 / 1.0 / 1.5 / 2.0 m at 1×), 6 drop, 7 leap.
    public enum JumpKind
    {
        Hop,
        Scramble,
        ClimbUp,
        DropDown,
        Leap,
    }

    // How the dog crosses an off-mesh link: which move (by the rise and anything in the way) and the path its body takes.
    public static class DogJumpArc
    {
        // Rises, drops and obstacles up to this are hopped.
        const float HopLimit = 0.6f;
        const float ObstacleClearance = 0.1f;
        const float HopArc = 0.35f;
        const float LedgeArc = 0.15f;
        // The scramble's clips (BorderCollie.controller: ScrambleGrab, ScrambleClimb at ScrambleClipSpeed, then FromEdge) take
        // ScrambleUpTime to get on top and ScrambleDownTime to drop off the far side; the body follows the same timing.
        public const float ScrambleClipSpeed = 1.2f;
        public const float ScrambleUpTime = 1.7f;
        public const float ScrambleDownTime = 0.67f;
        const float ScrambleUp = ScrambleUpTime / (ScrambleUpTime + ScrambleDownTime);
        const float ClimbShare = 0.65f;
        // The leap (BorderCollie.controller's Leap, Dog_Jump_Walk): it crouches LeapTakeOff s, is in the air LeapFlight s,
        // then takes LeapRecover s to absorb the landing before moving on; the body follows the same timing (DogTests).
        public const float LeapTakeOff = 0.38f;
        public const float LeapFlight = 0.89f;
        const float LeapRecover = 0.3f;
        const float LeapClearance = 0.25f;

        // `rise`: the end's height above the start; `obstacle`: the top of what's in between, above the start. Anything
        // from HopLimit up to `maxLeap` high (a fence, a wall, a block to land on) is leapt; taller is climbed.
        public static JumpKind Pick(float rise, float obstacle, float maxLeap)
        {
            float height = Mathf.Max(rise, obstacle);
            if (height > HopLimit && height <= maxLeap)
                return JumpKind.Leap;
            if (obstacle > HopLimit && Mathf.Abs(rise) <= HopLimit)
                return JumpKind.Scramble;
            if (rise > HopLimit)
                return JumpKind.ClimbUp;
            return rise < -HopLimit ? JumpKind.DropDown : JumpKind.Hop;
        }

        // The highest the body goes (world height).
        public static float Peak(JumpKind kind, Vector3 start, Vector3 end, float obstacle) => kind switch
        {
            JumpKind.Scramble => start.y + obstacle + ObstacleClearance,
            JumpKind.ClimbUp => end.y + LedgeArc,
            JumpKind.Leap => Mathf.Max(start.y, end.y, start.y + obstacle) + LeapClearance,
            _ => Mathf.Max(start.y, end.y, start.y + obstacle) + HopArc,
        };

        // Seconds the move takes over `distance` metres (flat).
        public static float Duration(JumpKind kind, float distance) => kind switch
        {
            JumpKind.Scramble => ScrambleUpTime + ScrambleDownTime,
            JumpKind.ClimbUp => 1.6f,
            JumpKind.Leap => LeapTakeOff + LeapFlight + LeapRecover,
            JumpKind.DropDown => 0.75f + distance * 0.05f,
            _ => 0.55f + distance * 0.05f,
        };

        // Where the body is at `t` (0..1) of the move; exactly `start` at 0 and `end` at 1.
        public static Vector3 Position(JumpKind kind, Vector3 start, Vector3 end, float peak, float t)
        {
            t = Mathf.Clamp01(t);
            if (t <= 0f)
                return start;
            if (t >= 1f)
                return end;
            switch (kind)
            {
                // Up onto the top of the obstacle at its middle, then off the far side.
                case JumpKind.Scramble:
                {
                    Vector3 top = Vector3.Lerp(start, end, 0.5f);
                    top.y = peak;
                    if (t < ScrambleUp)
                    {
                        float k = t / ScrambleUp;
                        Vector3 flat = Vector3.Lerp(start, top, Mathf.SmoothStep(0f, 1f, k));
                        flat.y = Mathf.Lerp(start.y, peak, Mathf.Sin(k * Mathf.PI * 0.5f));
                        return flat;
                    }
                    float d = (t - ScrambleUp) / (1f - ScrambleUp);
                    Vector3 down = Vector3.Lerp(top, end, d);
                    down.y = Mathf.Lerp(peak, end.y, d * d);
                    return down;
                }
                // Paws up the ledge (mostly straight up), then onto it.
                case JumpKind.ClimbUp:
                {
                    if (t < ClimbShare)
                    {
                        float k = t / ClimbShare;
                        Vector3 up = Vector3.Lerp(start, end, 0.25f * k);
                        up.y = Mathf.Lerp(start.y, peak, Mathf.Sin(k * Mathf.PI * 0.5f));
                        return up;
                    }
                    float k2 = (t - ClimbShare) / (1f - ClimbShare);
                    Vector3 on = Vector3.Lerp(Vector3.Lerp(start, end, 0.25f), end, k2);
                    on.y = Mathf.Lerp(peak, end.y, k2);
                    return on;
                }
                // Crouched at the start until takeoff, the parabola through the air, then the landing absorbed at the end.
                case JumpKind.Leap:
                {
                    float k = (t * Duration(JumpKind.Leap, 0f) - LeapTakeOff) / LeapFlight;
                    return k <= 0f ? start : k >= 1f ? end : Arc(start, end, peak, k);
                }
                default:
                    return Arc(start, end, peak, t);
            }
        }

        // A parabola over the straight line, peaking at `peak` halfway.
        static Vector3 Arc(Vector3 start, Vector3 end, float peak, float t)
        {
            Vector3 flat = Vector3.Lerp(start, end, t);
            float line = Mathf.Lerp(start.y, end.y, t);
            flat.y = line + 4f * (peak - Mathf.Lerp(start.y, end.y, 0.5f)) * t * (1f - t);
            return flat;
        }

        // The animator's JumpKind number for a move (ClimbUp picks the ledge clip nearest the rise; the clips are named for
        // the model at 1×, so a dog scaled `scale` divides the rise by it first).
        public static int AnimatorKind(JumpKind kind, float rise, float scale = 1f) => kind switch
        {
            JumpKind.Hop => 0,
            JumpKind.Scramble => 1,
            JumpKind.ClimbUp => 2 + Mathf.Clamp(Mathf.RoundToInt(rise / Mathf.Max(scale, 0.01f) / 0.5f) - 1, 0, 3),
            JumpKind.Leap => 7,
            _ => 6,
        };
    }
}
