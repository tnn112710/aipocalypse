using UnityEngine;

namespace TextStudio.BuiltIns
{
    /// <summary>
    /// Code-defined curve library. No shipped .asset files. Registered at domain load.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Built-in curve definitions are immutable after construction.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class BuiltInCurves
    {
        const float TwoPi = Mathf.PI * 2f;

        public static readonly CodeCurve Linear = new CodeCurve(
            "curve.linear", "Linear", "Straight line, t->t.",
            t => t);

        public static readonly CodeCurve ReverseLinear = new CodeCurve(
            "curve.reverseLinear", "Reverse Linear", "Straight reverse line, t->1-t.",
            t => 1f - t);

        public static readonly CodeCurve Sine = new CodeCurve(
            "curve.sine", "Sine", "One full sine cycle in [0,1]. Returns values in [-1,1].",
            t => Mathf.Sin(t * TwoPi));

        public static readonly CodeCurve HalfSine = new CodeCurve(
            "curve.halfSine", "Half Sine", "Half sine lobe. Returns values in [0,1].",
            t => Mathf.Sin(t * Mathf.PI));

        public static readonly CodeCurve Square = new CodeCurve(
            "curve.square", "Square", "Hard flip at 0.5; returns +1 for first half, -1 for second.",
            t => t < 0.5f ? 1f : -1f);

        public static readonly CodeCurve Step = new CodeCurve(
            "curve.step", "Step", "Step at 0.5 returning 0 then 1.",
            t => t < 0.5f ? 0f : 1f);

        public static readonly CodeCurve Hold = new CodeCurve(
            "curve.hold", "Hold", "Constant 1.",
            _ => 1f);

        public static readonly CodeCurve Bounce = new CodeCurve(
            "curve.bounce", "Bounce", "Absolute sine bounce; |sin(pi * t)|.",
            t => Mathf.Abs(Mathf.Sin(t * Mathf.PI)));

        public static readonly CodeCurve EaseIn = new CodeCurve(
            "curve.easeIn", "Ease In", "Quadratic ease-in.",
            t => t * t);

        public static readonly CodeCurve EaseOut = new CodeCurve(
            "curve.easeOut", "Ease Out", "Quadratic ease-out.",
            t => 1f - (1f - t) * (1f - t));

        public static readonly CodeCurve EaseInOut = new CodeCurve(
            "curve.easeInOut", "Ease In-Out", "Smoothstep (cubic).",
            t => t * t * (3f - 2f * t));

        public static readonly CodeCurve BackOut = new CodeCurve(
            "curve.backOut", "Back Out", "Overshoot then settle.",
            t =>
            {
                const float c1 = 1.70158f;
                const float c3 = c1 + 1f;
                float u = t - 1f;
                return 1f + c3 * u * u * u + c1 * u * u;
            });

        public static readonly CodeCurve ElasticOut = new CodeCurve(
            "curve.elasticOut", "Elastic Out", "Damped elastic settle.",
            t =>
            {
                if (t <= 0f) return 0f;
                if (t >= 1f) return 1f;
                const float c4 = TwoPi / 3f;
                return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * c4) + 1f;
            });

        /// <summary>
        /// Enumerate all built-in curves for registration.
        /// </summary>
        public static System.Collections.Generic.IEnumerable<CodeCurve> All()
        {
            yield return Linear;
            yield return ReverseLinear;
            yield return Sine;
            yield return HalfSine;
            yield return Square;
            yield return Step;
            yield return Hold;
            yield return Bounce;
            yield return EaseIn;
            yield return EaseOut;
            yield return EaseInOut;
            yield return BackOut;
            yield return ElasticOut;
        }
    }
}
