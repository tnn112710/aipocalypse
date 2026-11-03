using UnityEngine;

namespace Aipocalypse.Core.Ghosts
{
    // Something that shows a see-through ghost of how it would be once rebuilt, built or placed (a broken fence segment).
    // Whoever looks at it (the player) asks each frame and draws it with GhostDrawer.Draw.
    public interface IGhostSource
    {
        // False when there's nothing to show right now.
        bool TryGetGhost(GameObject viewer, out GhostView ghost);
    }

    public readonly struct GhostView
    {
        public readonly GhostShape Shape;
        // Root of the shape in the world.
        public readonly Matrix4x4 Pose;
        public readonly GhostState State;

        public GhostView(GhostShape shape, Matrix4x4 pose, GhostState state)
        {
            Shape = shape;
            Pose = pose;
            State = state;
        }
    }
}
