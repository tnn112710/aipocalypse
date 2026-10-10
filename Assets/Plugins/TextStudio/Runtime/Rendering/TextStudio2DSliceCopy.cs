using UnityEngine;
using UnityEngine.Rendering;

namespace TextStudio
{
    /// <summary>
    /// Owns how shared texture-array slices are filled. Copying a 2D texture or target into an array slice needs
    /// <see cref="CopyTextureSupport.DifferentTypes"/>; WebGL 2 has no such copy, so those devices draw the slice with a
    /// blit that writes the same texels. WebGL also ignores the destination slice of a blit without a material, so the
    /// draw goes through the Hero composite shader's slice copy pass.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DSliceCopy
    {
        static readonly int s_sourceScale = Shader.PropertyToID("_TextStudioSliceSourceScale");
        static bool s_drawnForTests;
        // Immediate draws set their scale on one material; recorded draws execute later, so they keep their own unit scale.
        static Material s_material;
        static Material s_recordedMaterial;
        static int s_pass = -1;

        internal static bool CopiesAcrossTypes =>
            !s_drawnForTests && (SystemInfo.copyTextureSupport & CopyTextureSupport.DifferentTypes) != 0;

        /// <summary>Makes a device that can copy slices draw them instead, so tests compare both paths.</summary>
        internal static void SetDrawnSlicesForTests(bool drawn) => s_drawnForTests = drawn;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void ResetRuntimeState()
        {
            s_drawnForTests = false;
            Destroy(ref s_material);
            Destroy(ref s_recordedMaterial);
            s_pass = -1;
        }

        /// <summary>Writes <paramref name="source"/> at its own size into the origin of <paramref name="slice"/>.</summary>
        internal static void CopyToSliceOrigin(Texture source, RenderTexture target, int slice)
        {
            if (CopiesAcrossTypes)
            {
                Graphics.CopyTexture(source, 0, 0, 0, 0, source.width, source.height, target, slice, 0, 0, 0);
                return;
            }
            // Scaling the source coordinates puts every target texel inside the source extent on its source texel centre;
            // the texels beyond it are outside the region readers sample.
            Draw(source, target, slice, new Vector2((float)target.width / source.width, (float)target.height / source.height));
        }

        /// <summary>Resamples <paramref name="source"/> over all of <paramref name="slice"/>'s first mip.</summary>
        internal static void DrawSlice(Texture source, RenderTexture target, int slice) => Draw(source, target, slice, Vector2.one);

        /// <summary>Records a draw of <paramref name="source"/> over all of <paramref name="slice"/>.</summary>
        internal static void RecordDrawSlice(CommandBuffer commands, RenderTargetIdentifier source, RenderTexture target, int slice)
        {
            Material material = ResolveMaterial(ref s_recordedMaterial);
            if (material == null) commands.Blit(source, target, 0, slice);
            else commands.Blit(source, target, material, s_pass, slice);
        }

        static void Draw(Texture source, RenderTexture target, int slice, Vector2 scale)
        {
            RenderTexture previous = RenderTexture.active;
            Material material = ResolveMaterial(ref s_material);
            if (material == null)
                Graphics.Blit(source, target, scale, Vector2.zero, 0, slice);
            else
            {
                material.SetVector(s_sourceScale, scale);
                Graphics.Blit(source, target, material, s_pass, slice);
            }
            RenderTexture.active = previous;
        }

        static Material ResolveMaterial(ref Material material)
        {
            if (material != null) return material;
            Shader shader = Resources.Load<Shader>(TextStudio2DResourceKeys.HeroCompositeShader);
            if (shader == null || !shader.isSupported) return null;
            material = new Material(shader) { name = "Text Studio Slice Copy", hideFlags = HideFlags.HideAndDontSave };
            material.SetVector(s_sourceScale, Vector2.one);
            s_pass = material.FindPass("SLICE_COPY");
            if (s_pass >= 0) return material;
            Destroy(ref material);
            return null;
        }

        static void Destroy(ref Material material)
        {
            if (material != null)
            {
                if (Application.isPlaying) Object.Destroy(material);
                else Object.DestroyImmediate(material);
            }
            material = null;
        }
    }
}
