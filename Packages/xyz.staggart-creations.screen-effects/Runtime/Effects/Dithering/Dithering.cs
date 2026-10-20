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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Retro/Dithering")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Dithering, typeof(DitheringRenderer))]
    [EffectDocs("dithering-2")]
    public sealed class Dithering : PostProcessingComponent
    {
        [Tooltip("Note that the texture's filter mode (Point or Bilinear) greatly affects the behavior of the pattern")]
        [VolumeTextureDefault("68b293236ca89714684f8f610f370657")]
        public TextureParameter lut = new TextureParameter(null);
        
        public TextureProjectionParameter textureProjection = new TextureProjectionParameter();
        
        [Range(0f, 1f), Tooltip("The screen's luminance values control the density of the dithering matrix")]
        public ClampedFloatParameter luminanceThreshold = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, -1f, 1f);

        //[Range(0f,2f), DisplayName("Tiling")]
        public ClampedIntParameter tiling = new ClampedIntParameter(1, 1, 6);

        internal override void WarnWhenMissingResources()
        {
            ThrowWarningExceptionIfNull(lut);
        }

        public new bool IsActive() => active && intensity.value > 0f && lut.value;
    }
}