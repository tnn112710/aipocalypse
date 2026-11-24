using System.Collections;
using MB13.Aipocalypse.Core.Links;
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
            while (!context.MoveTowards(start, LinkMoves.Facing(context, start, end, up), true, false).reached)
                yield return null;

            context.DisableLocalAvoidance();
            context.enableBuiltInMovement = false;
            float3 from = start;
            if (info != null && info.Via != null)
            {
                float3 via = info.Via.position;
                foreach (object step in LinkMoves.Walk(context, start, via, WalkSpeed, LinkMoves.Facing(context, start, via, up), TurnRate))
                    yield return step;
                from = via;
            }

            float rise = end.y - from.y;
            float obstacle = info != null ? info.ObstacleHeight : 0f;
            DogSettings settings = _dog != null ? _dog.Settings : null;
            JumpKind kind = DogJumpArc.Pick(rise, obstacle, settings != null ? settings.LeapHeight : 3f);
            Vector3 a = from, b = end;
            float peak = DogJumpArc.Peak(kind, a, b, obstacle);
            float duration = DogJumpArc.Duration(kind, Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)));
            quaternion toward = LinkMoves.Facing(context, from, end, up);
            // A leap: it stands squared up a moment first (the clip's own crouch is part of the move).
            float pause = kind == JumpKind.Leap && settings != null ? settings.LeapPause : 0f;
            foreach (object step in LinkMoves.Hold(context, from, toward, pause, TurnRate))
                yield return step;
            if (_dog != null)
                _dog.PlayJump(DogJumpArc.AnimatorKind(kind, rise, _dog.ModelScale));
            foreach (object step in LinkMoves.Hold(context, from, toward, kind == JumpKind.Leap ? 0f : WindUp, TurnRate))
                yield return step;
            for (float t = 0f; t < duration; t += context.deltaTime)
            {
                context.transform.Position = (float3)DogJumpArc.Position(kind, a, b, peak, t / duration);
                context.transform.Rotation = LinkMoves.Turn(context, toward, TurnRate);
                yield return null;
            }
            context.transform.Position = end;
        }
    }
}
