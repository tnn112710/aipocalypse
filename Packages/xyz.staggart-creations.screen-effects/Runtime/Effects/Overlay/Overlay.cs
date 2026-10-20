// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using UnityEngine.Rendering.Universal;
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Overlay/Overlay")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Overlay, typeof(OverlayRenderer))]
    [EffectDocs("overlay-2")]
    public sealed class Overlay : PostProcessingComponent
    {
        [VolumeTextureDefault("12cd4a6dc2eebac4f817e5db9684a2f9")]
        public TextureParameter overlayTex = new TextureParameter(null);
        
        public TextureProjectionParameter textureProjection = new TextureProjectionParameter();

        [Tooltip("The screen's luminance values control the opacity of the image")]
        public ClampedFloatParameter luminanceThreshold = new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Maintains the image aspect ratio, regardless of the screen width")]
        public BoolParameter autoAspect = new BoolParameter(false);
        
        [Tooltip("Blends the gradient through various Photoshop-like blending modes")]
        public BlendModeFullParameter blendMode = new BlendModeFullParameter();

        [Range(0f, 1f)]
        public ClampedFloatParameter tiling = new ClampedFloatParameter(0f, 0f, 1f);

        internal override void WarnWhenMissingResources()
        {
            ThrowWarningExceptionIfNull(overlayTex);
        }

        public new bool IsActive() => active && intensity.value > 0f && overlayTex.value;
    }
}