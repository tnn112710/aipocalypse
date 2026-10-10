#if __MICROVERSE_SPLINES__
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.SplinePainter
{
    public class SplinePainter : MonoBehaviour
    {
        [Tooltip("The minimum amount of cursor movement to be considered a new sample.")]
        [Min(0.1f)]
        public float strokeDeltaThreshold = .1f;

        [Tooltip("Point reduction epsilon determines how aggressive the point reduction algorithm is when removing redundant points. Lower values result in more accurate spline representations of the original line, at the cost of greater number of knots.")]
        [Range(0f, 10f), SerializeField]
        public float pointReductionEpsilon = 3f;

        [Tooltip("Tension affects how curvy splines are at knots. 0 is a sharp corner, 1 is maximum curvitude.")]
        [Range(0f, 1f), SerializeField]
        public float splineTension = 1 / 4f;

        [Tooltip("Offset in y direction that's applied to the spline")]
        public float heightOffset = 0f;

        [Tooltip("The container of the spline which will be affected by the painting")]
        public SplineContainer container;
    }
}
#endif
