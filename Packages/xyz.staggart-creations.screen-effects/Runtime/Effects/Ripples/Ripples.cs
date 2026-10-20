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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Distortion/Ripples")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Ripples, typeof(RipplesRenderer))]
    [EffectDocs("ripples-2")]
    public sealed class Ripples : PostProcessingComponent
    {
        public enum RipplesMode
        {
            Radial = 0,
            DistortionTexture = 1,
        }

        [Serializable]
        public sealed class RipplesModeParam : VolumeParameter<RipplesMode> { }

        //[DisplayName("Method")]
        public RipplesModeParam mode = new RipplesModeParam { value = RipplesMode.Radial };

        [VolumeTextureDefault("8af8b9cb204211d42a11edc94c9eced3")]
        public TextureParameter distortionMap = new TextureParameter(null);
        
        public TextureProjectionParameter textureProjection = new TextureProjectionParameter();
        
        //[Range(0f, 10), DisplayName("Intensity")]
        public ClampedFloatParameter strength = new ClampedFloatParameter (1f, 0f, 1f);

        [Range(1f, 10), Tooltip("The frequency of the waves")]
        public ClampedFloatParameter distance = new ClampedFloatParameter(5f, 1f, 10f);

        [Range(0f, 10), Tooltip("Speed")]
        public ClampedFloatParameter speed = new ClampedFloatParameter(1f, 0f, 10f);

        [Range(0f, 5), Tooltip("Width")]
        public ClampedFloatParameter width = new ClampedFloatParameter(0.75f, 0f, 5f);

        [Range(0f, 5), Tooltip("Height")]
        public ClampedFloatParameter height = new ClampedFloatParameter (1f, 0f, 5f);

        internal override void WarnWhenMissingResources()
        {
            if(mode.value == RipplesMode.DistortionTexture) ThrowWarningExceptionIfNull(distortionMap);
        }
        
        public new bool IsActive() => active && strength.value > 0f && intensity.value > 0;
    }
}