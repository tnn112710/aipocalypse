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
using UnityEngine.Rendering;
using System;
using UnityEngine;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Environment/Sun Shafts")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Sunshafts, typeof(SunshaftsRenderer))]
    [EffectDocs("sunshafts-2")]
    public sealed class Sunshafts : PostProcessingComponent
    {
        public enum SourceMode
        {
            Point,
            SkyLuminance
        }
        
        public FloatParameter maxStrength = new FloatParameter(2f, false);
        
        [Serializable]
        public sealed class SourceParameter : VolumeParameter<SourceMode> { }
        public SourceParameter sourceMode = new SourceParameter { value = SourceMode.Point };
        public ClampedFloatParameter skyLuminanceThreshold = new ClampedFloatParameter(1f, 0f, 5f);
        
        [Tooltip("How much of the sun light's intensity is multiplied")]
        public ClampedFloatParameter casterIntensityInfluence = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter casterColorInfluence = new ClampedFloatParameter(1f, 0f, 1f);

         [Tooltip("Adds an extra color temperature value (in Kelvin)")]
        public ClampedFloatParameter temperatureShift = new ClampedFloatParameter(0f, -2000f, 2000f);
         
        [InspectorName("Resolution"), Tooltip("Low, quarter resolution\n\nNormal, half resolution\n\nHigh, full resolution\n\nLower resolutions may induce jittering")]
        public ResolutionParameter resolution = new ResolutionParameter { value = Resolution.Full };

        [Tooltip("Enabling this option to use 16-bit precision and reduce banding artefacts. Only enable this if that is noticeable.")]
        public BoolParameter highQuality = new BoolParameter(true, false);

        [Tooltip("Any color values over this threshold will contribute to the sunshafts effect")]
        [InspectorName("Sky color threshold")]
        public ColorParameter sunThreshold = new ColorParameter(Color.black, false);

        [InspectorName("Color")]
        public ColorParameter sunColor = new ColorParameter(Color.white, true, false, false, false);

        [Tooltip("The length of the sunrays from the caster's position to the camera")]
        [Min(0f)]
        public FloatParameter length = new FloatParameter(7.5f, false);
        
        [Range(0.1f, 1f)]
        [Tooltip("The degree to which the shafts’ brightness diminishes with distance from the caster")]
        public ClampedFloatParameter falloff = new ClampedFloatParameter(0.5f, 0.1f, 1f);
        
        public new bool IsActive() => active && intensity.value > 0 && maxStrength.value > 0f;
    }
}