using MalbersAnimations.Scriptables;
using UnityEngine;

namespace SunfishCreations
{
    [DefaultExecutionOrder(2001)]
    public class FishingLine : MonoBehaviour
    {
        [Tooltip("The tip of the fishing rod where the line starts")]
        public TransformReference RodTip;

        [Tooltip("The tip of the bobber where the line ends")]
        public TransformReference BobberTip;

        [Tooltip("How much the fishing line droops (higher values = more sag)")]
        public float sagAmount = 0.15f;

        [Tooltip("Number of segments used to draw the line curve (more segments = smoother curve)")]
        public int lineSegments = 10;

        protected LineRenderer line;

        protected virtual void Start()
        {
            Initialization();
        }

        protected virtual void Initialization()
        {
            line = GetComponent<LineRenderer>();
            if (line == null)
            {
                Debug.LogWarning("FishingLine requires a LineRenderer component.", this);
                enabled = false;
                return;
            }
            line.positionCount = lineSegments;
        }

        protected virtual void LateUpdate()
        {
            DrawCurvedLine();
        }

        protected virtual void DrawCurvedLine()
        {
            if (RodTip?.Value == null || BobberTip?.Value == null || line == null)
                return;

            Vector3 startPos = RodTip.Value.position;
            Vector3 endPos = BobberTip.Value.position;
            float distance = Vector3.Distance(startPos, endPos);

            // Calculate sag based on distance (longer lines sag more)
            float currentSag = sagAmount * (distance / 10f);

            for (int i = 0; i < lineSegments; i++)
            {
                float t = (float)i / (lineSegments - 1);

                // Linear interpolation between start and end
                Vector3 point = Vector3.Lerp(startPos, endPos, t);

                // Add downward curve (parabolic sag)
                float sag = currentSag * 4 * t * (1 - t); // Parabola peaks at t=0.5
                point.y -= sag;

                line.SetPosition(i, point);
            }
        }
    }
}