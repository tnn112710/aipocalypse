using System.Collections;
using Pathfinding.ECS;
using Unity.Mathematics;
using UnityEngine;

namespace MB13.Aipocalypse.Core.Links
{
    // Moves shared by the off-mesh link handlers (DogJump, RobotDoorHop) while built-in movement is off.
    public static class LinkMoves
    {
        // Turns toward `target` at `turnRate` °/s instead of snapping (a link's legs can point another way than the walk in).
        public static quaternion Turn(AgentOffMeshLinkTraversalContext context, quaternion target, float turnRate) =>
            Quaternion.RotateTowards(context.transform.Rotation, target, turnRate * context.deltaTime);

        public static quaternion Facing(AgentOffMeshLinkTraversalContext context, float3 from, float3 to, float3 up) =>
            quaternion.LookRotationSafe(context.movementPlane.ToWorld(context.movementPlane.ToPlane(to - from), 0), up);

        // Walks from `from` to `to` at `speed` m/s, turning toward `facing`; one step per frame.
        public static IEnumerable Walk(AgentOffMeshLinkTraversalContext context, float3 from, float3 to, float speed, quaternion facing, float turnRate)
        {
            float time = math.distance(from, to) / speed;
            for (float t = 0f; t < time; t += context.deltaTime)
            {
                context.transform.Position = math.lerp(from, to, t / time);
                context.transform.Rotation = Turn(context, facing, turnRate);
                yield return null;
            }
        }

        // Stands at `at` for `seconds`, turning toward `facing`.
        public static IEnumerable Hold(AgentOffMeshLinkTraversalContext context, float3 at, quaternion facing, float seconds, float turnRate)
        {
            for (float t = 0f; t < seconds; t += context.deltaTime)
            {
                context.transform.Position = at;
                context.transform.Rotation = Turn(context, facing, turnRate);
                yield return null;
            }
        }
    }
}
