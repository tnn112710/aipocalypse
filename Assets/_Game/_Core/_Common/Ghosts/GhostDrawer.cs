using UnityEngine;
using UnityEngine.Rendering;

namespace Aipocalypse.Core.Ghosts
{
    // Draws a ghost straight to the cameras for this frame (Graphics.RenderMesh: no GameObjects to pool or clean up).
    // Call it from Update on every frame the ghost should show.
    public static class GhostDrawer
    {
        public static void Draw(in GhostView ghost, GhostStyle style)
        {
            if (ghost.Shape == null || style == null)
                return;
            Material look = style.LookFor(ghost.State);
            if (look == null)
                return;
            RenderParams lookParams = Params(look);
            bool depth = style.DepthOnly != null;
            RenderParams depthParams = depth ? Params(style.DepthOnly) : default;
            for (int i = 0; i < ghost.Shape.Count; i++)
            {
                GhostShape.Part part = ghost.Shape[i];
                Matrix4x4 matrix = ghost.Pose * part.Local;
                for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
                {
                    if (depth)
                        Graphics.RenderMesh(depthParams, part.Mesh, sub, matrix);
                    Graphics.RenderMesh(lookParams, part.Mesh, sub, matrix);
                }
            }
        }

        // Default layer: the world camera draws it, the first-person pass doesn't.
        static RenderParams Params(Material material) => new(material)
        {
            layer = 0,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
        };
    }
}
