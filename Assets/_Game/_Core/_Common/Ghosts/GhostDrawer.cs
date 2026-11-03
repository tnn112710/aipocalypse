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
            if (style != null)
                Draw(ghost, style.LookFor(ghost.State), style.DepthOnly);
        }

        // Any ghost material; `depth` (optional) lays the nearest surfaces first, `props` tints this one (its values are
        // copied at the call, so one block can be refilled for the next ghost).
        public static void Draw(in GhostView ghost, Material look, Material depth = null, MaterialPropertyBlock props = null)
        {
            if (ghost.Shape == null || look == null)
                return;
            RenderParams lookParams = Params(look, props);
            RenderParams depthParams = depth != null ? Params(depth, null) : default;
            for (int i = 0; i < ghost.Shape.Count; i++)
            {
                GhostShape.Part part = ghost.Shape[i];
                if (part.Mesh == null)
                    continue;
                Matrix4x4 matrix = part.WorldMatrix(ghost.Pose);
                for (int sub = 0; sub < part.Mesh.subMeshCount; sub++)
                {
                    if (depth != null)
                        Graphics.RenderMesh(depthParams, part.Mesh, sub, matrix);
                    Graphics.RenderMesh(lookParams, part.Mesh, sub, matrix);
                }
            }
        }

        // Default layer: the world camera draws it, the first-person pass doesn't.
        static RenderParams Params(Material material, MaterialPropertyBlock props) => new(material)
        {
            layer = 0,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.ForceNoMotion,
            matProps = props,
        };
    }
}
