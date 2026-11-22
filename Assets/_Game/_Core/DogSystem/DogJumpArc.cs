using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The animator's JumpKind is this, then (ClimbUp only) the ledge height: 2–5 = 0.5 / 1.0 / 1.5 / 2.0 m; 6 = drop.
    public enum JumpKind
    {
        Hop,
        Scramble,
        ClimbUp,
        DropDown,
    }

    // How the dog crosses an off-mesh link: which move (by the rise and anything in the way) and the path its body takes.
    public static class DogJumpArc
    {
        // Rises, drops and obstacles up to this are hopped.
        const float HopLimit = 0.6f;
        const float ObstacleClearance = 0.1f;
        const float HopArc = 0.35f;
        const float LedgeArc = 0.15f;
        // The scramble's share of its time spent getting on top of the obstacle (the rest is the drop off the far side).
        const float ScrambleUp = 0.55f;
        const float ClimbShare = 0.65f;

        // `rise`: the end's height above the start; `obstacle`: the top of what's in between, above the start.
        public static JumpKind Pick(float rise, float obstacle)
        {
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
            _ => Mathf.Max(start.y, end.y, start.y + obstacle) + HopArc,
        };

        // Seconds the move takes over `distance` metres (flat).
        public static float Duration(JumpKind kind, float distance) => kind switch
        {
            JumpKind.Scramble => 2.4f,
            JumpKind.ClimbUp => 1.6f,
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
                // A parabola over the straight line, peaking at `peak` halfway.
                default:
                {
                    Vector3 flat = Vector3.Lerp(start, end, t);
                    float line = Mathf.Lerp(start.y, end.y, t);
                    float lift = 4f * (peak - Mathf.Lerp(start.y, end.y, 0.5f)) * t * (1f - t);
                    flat.y = line + lift;
                    return flat;
                }
            }
        }

        // The animator's JumpKind number for a move (ClimbUp picks the ledge clip nearest the rise).
        public static int AnimatorKind(JumpKind kind, float rise) => kind switch
        {
            JumpKind.Hop => 0,
            JumpKind.Scramble => 1,
            JumpKind.ClimbUp => 2 + Mathf.Clamp(Mathf.RoundToInt(rise / 0.5f) - 1, 0, 3),
            _ => 6,
        };
    }
}
