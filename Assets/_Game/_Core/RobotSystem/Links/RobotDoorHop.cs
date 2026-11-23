using System.Collections;
using Pathfinding;
using Pathfinding.ECS;
using Unity.Mathematics;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // On a robot-only NodeLink2 through a doorway over a step with no stairs (the house door): a robot walks from the link's
    // start to the threshold, then hops down to the end in a small arc playing its jump (and the other way round coming in).
    // Tag the link NavTags.RobotsOnly so no other walker plans through it.
    [RequireComponent(typeof(NodeLink2))]
    public sealed class RobotDoorHop : MonoBehaviour, IOffMeshLinkHandler, IOffMeshLinkStateMachine
    {
        // A segment that climbs or drops more than this is hopped, not walked.
        const float StepHeight = 0.3f;
        const float TurnRate = 270f;

        [SerializeField, Tooltip("The door's sill: walked to on the floor's level, hopped from")] Transform _threshold;
        [SerializeField, Min(0.1f), Tooltip("Walking pace on the link (m/s)")] float _walkSpeed = 0.8f;
        [SerializeField, Min(0.1f), Tooltip("Seconds the hop takes")] float _hopTime = 0.6f;
        [SerializeField, Min(0f), Tooltip("How far above the higher end the arc peaks (m)")] float _arc = 0.3f;
        [SerializeField, Min(0f), Tooltip("Seconds into the jump clip before it leaves the ground")] float _windUp = 0.25f;

        NodeLink2 _link;

        void OnEnable()
        {
            _link = GetComponent<NodeLink2>();
            _link.onTraverseOffMeshLink = this;
        }

        void OnDisable()
        {
            if (_link != null && ReferenceEquals(_link.onTraverseOffMeshLink, this))
                _link.onTraverseOffMeshLink = null;
        }

        public void Setup(Transform threshold) => _threshold = threshold;

        IOffMeshLinkStateMachine IOffMeshLinkHandler.GetOffMeshLinkStateMachine(AgentOffMeshLinkTraversalContext context) => this;

        IEnumerable IOffMeshLinkStateMachine.OnTraverseOffMeshLink(AgentOffMeshLinkTraversalContext context)
        {
            float3 start = context.link.relativeStart;
            float3 end = context.link.relativeEnd;
            float3 up = context.movementPlane.ToWorld(float2.zero, 1);
            quaternion facing = Facing(context, start, end, up);
            while (!context.MoveTowards(start, facing, true, false).reached)
                yield return null;

            context.DisableLocalAvoidance();
            context.enableBuiltInMovement = false;
            float3 middle = _threshold != null ? (float3)_threshold.position : start;
            foreach (object step in Segment(context, start, middle, up))
                yield return step;
            foreach (object step in Segment(context, middle, end, up))
                yield return step;
            context.transform.Position = end;
        }

        IEnumerable Segment(AgentOffMeshLinkTraversalContext context, float3 from, float3 to, float3 up)
        {
            if (math.distancesq(from, to) < 0.0001f)
                yield break;
            quaternion facing = Facing(context, from, to, up);
            if (math.abs(to.y - from.y) <= StepHeight)
            {
                float time = math.distance(from, to) / _walkSpeed;
                for (float t = 0f; t < time; t += context.deltaTime)
                {
                    context.transform.Position = math.lerp(from, to, t / time);
                    context.transform.Rotation = Turn(context, facing);
                    yield return null;
                }
                yield break;
            }

            Robot robot = context.gameObject != null ? context.gameObject.GetComponent<Robot>() : null;
            if (robot != null)
                robot.Emote(RobotEmote.Jump);
            for (float t = 0f; t < _windUp; t += context.deltaTime)
            {
                context.transform.Position = from;
                context.transform.Rotation = Turn(context, facing);
                yield return null;
            }
            float peak = math.max(from.y, to.y) + _arc;
            for (float t = 0f; t < _hopTime; t += context.deltaTime)
            {
                float k = t / _hopTime;
                float3 flat = math.lerp(from, to, k);
                // Up from `from` to the peak and down to `to`: a parabola over the straight line between them.
                float line = math.lerp(from.y, to.y, k);
                float lift = 4f * (peak - math.lerp(from.y, to.y, 0.5f)) * k * (1f - k);
                context.transform.Position = new float3(flat.x, line + lift, flat.z);
                context.transform.Rotation = Turn(context, facing);
                yield return null;
            }
            context.transform.Position = to;
        }

        // Turns toward `target` at TurnRate instead of snapping (a link's legs can point another way than the walk in).
        static quaternion Turn(AgentOffMeshLinkTraversalContext context, quaternion target) =>
            Quaternion.RotateTowards(context.transform.Rotation, target, TurnRate * context.deltaTime);

        static quaternion Facing(AgentOffMeshLinkTraversalContext context, float3 from, float3 to, float3 up) =>
            quaternion.LookRotationSafe(context.movementPlane.ToWorld(context.movementPlane.ToPlane(to - from), 0), up);
    }
}
