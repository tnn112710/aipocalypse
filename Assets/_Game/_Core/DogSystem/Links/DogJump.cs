using System.Collections;
using Pathfinding;
using Pathfinding.ECS;
using Unity.Mathematics;
using UnityEngine;

namespace MB13.Aipocalypse.Core.DogSystem
{
    // The dog's own way over every off-mesh link it takes (it overrides the link's handler, e.g. the robots' door hop):
    // walk to the link's start (and its DogLinkInfo's Via point, a door sill), then hop, scramble over, climb up or drop
    // down to the end (DogJumpArc), playing the matching clip.
    [RequireComponent(typeof(FollowerEntity))]
    [DisallowMultipleComponent]
    public sealed class DogJump : MonoBehaviour, IOffMeshLinkHandler, IOffMeshLinkStateMachine
    {
        const float WalkSpeed = 1.6f;
        const float WindUp = 0.15f;
        const float TurnRate = 360f;

        FollowerEntity _agent;
        Dog _dog;

        void OnEnable()
        {
            _agent = GetComponent<FollowerEntity>();
            _dog = GetComponent<Dog>();
            _agent.onTraverseOffMeshLink = this;
        }

        void OnDisable()
        {
            if (_agent != null && ReferenceEquals(_agent.onTraverseOffMeshLink, this))
                _agent.onTraverseOffMeshLink = null;
        }

        IOffMeshLinkStateMachine IOffMeshLinkHandler.GetOffMeshLinkStateMachine(AgentOffMeshLinkTraversalContext context) => this;

        IEnumerable IOffMeshLinkStateMachine.OnTraverseOffMeshLink(AgentOffMeshLinkTraversalContext context)
        {
            float3 start = context.link.relativeStart;
            float3 end = context.link.relativeEnd;
            float3 up = context.movementPlane.ToWorld(float2.zero, 1);
            DogLinkInfo info = context.link.gameObject != null ? context.link.gameObject.GetComponent<DogLinkInfo>() : null;
            while (!context.MoveTowards(start, Facing(context, start, end, up), true, false).reached)
                yield return null;

            context.DisableLocalAvoidance();
            context.enableBuiltInMovement = false;
            float3 from = start;
            if (info != null && info.Via != null)
            {
                float3 via = info.Via.position;
                quaternion facing = Facing(context, start, via, up);
                float time = math.distance(start, via) / WalkSpeed;
                for (float t = 0f; t < time; t += context.deltaTime)
                {
                    context.transform.Position = math.lerp(start, via, t / time);
                    context.transform.Rotation = Turn(context, facing);
                    yield return null;
                }
                from = via;
            }

            float rise = end.y - from.y;
            float obstacle = info != null ? info.ObstacleHeight : 0f;
            JumpKind kind = DogJumpArc.Pick(rise, obstacle);
            Vector3 a = from, b = end;
            float peak = DogJumpArc.Peak(kind, a, b, obstacle);
            float duration = DogJumpArc.Duration(kind, Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)));
            quaternion toward = Facing(context, from, end, up);
            if (_dog != null)
                _dog.PlayJump(DogJumpArc.AnimatorKind(kind, rise));
            for (float t = 0f; t < WindUp; t += context.deltaTime)
            {
                context.transform.Position = from;
                context.transform.Rotation = Turn(context, toward);
                yield return null;
            }
            for (float t = 0f; t < duration; t += context.deltaTime)
            {
                context.transform.Position = (float3)DogJumpArc.Position(kind, a, b, peak, t / duration);
                context.transform.Rotation = Turn(context, toward);
                yield return null;
            }
            context.transform.Position = end;
        }

        // Turns toward `target` at TurnRate instead of snapping (a link's legs can point another way than the walk in).
        static quaternion Turn(AgentOffMeshLinkTraversalContext context, quaternion target) =>
            Quaternion.RotateTowards(context.transform.Rotation, target, TurnRate * context.deltaTime);

        static quaternion Facing(AgentOffMeshLinkTraversalContext context, float3 from, float3 to, float3 up) =>
            quaternion.LookRotationSafe(context.movementPlane.ToWorld(context.movementPlane.ToPlane(to - from), 0), up);
    }
}
