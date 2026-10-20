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
using UnityEngine.Serialization;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Color/Colorize")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Colorize, typeof(ColorizeRenderer))]
    [EffectDocs("colorize-2")]
    public sealed class Colorize : PostProcessingComponent
    {
        public enum Source
        {
            Gradient,
            Texture
        }

        [Serializable]
        public sealed class GradientModeParameter : VolumeParameter<Source> { }
        public GradientModeParameter source = new GradientModeParameter { value = Source.Gradient };
        
        public TextureGradientParameter gradient = new TextureGradientParameter(
            new TextureGradient(
                new GradientColorKey[4]
                {
                    new (new Color(0f, 0.65f, 1f), 0), 
                    new (new Color(0.14f, 1f, 0f), 0.25f), 
                    new (new Color(1f, 0.39f, 0f), 0.75f), 
                    new (new Color(1f, 0f, 0.11f), 1f)
                }, 
                new GradientAlphaKey[2] { new (1f, 0f), new (1f, 1f) },
                GradientMode.Blend, ColorSpace.Linear, 16, true));
        
        [FormerlySerializedAs("colorRamp")]
        [Tooltip("Supply a gradient texture.\n\nLuminance values are colorized from left to right")]
        [VolumeTextureDefault("403f049634286f242b8d8bf8a5b8246e")]
        public TextureParameter gradientTexture = new TextureParameter(null);
        
        public Vector2Parameter range = new Vector2Parameter(new Vector2(0f, 1f));

        [FormerlySerializedAs("mode")] [Tooltip("Blends the gradient through various Photoshop-like blending modes")]
        public BlendModeFullParameter blendMode = new BlendModeFullParameter { value = BlendModeFull.Linear };
        
        /// <summary>
        /// Marks the gradient as changed, so it'll be internally rebuilt into a usable texture
        /// </summary>
        public void RebuildGradient()
        {
            gradient.value.SetDirty();
        }

        internal override void WarnWhenMissingResources()
        {
            if(source.value == Source.Texture) ThrowWarningExceptionIfNull(gradientTexture);
        }

        public new bool IsActive() => active && intensity.value > 0f;
    }
}