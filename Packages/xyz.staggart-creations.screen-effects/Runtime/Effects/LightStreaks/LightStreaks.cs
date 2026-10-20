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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Rendering/Light Streaks")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.LightStreaks, typeof(LightStreaksRenderer))]
    [EffectDocs("light-streaks-2")]
    public sealed class LightStreaks : PostProcessingComponent
    {
        [Range(0f, 1f), Tooltip("Shows only the effect, to allow for finetuning")]
        public BoolParameter debug = new BoolParameter(false);

        [Range(0.01f, 5f), Tooltip("Luminance threshold, pixels above this threshold (material's emission value) will contribute to the effect")]
        public ClampedFloatParameter luminanceThreshold = new ClampedFloatParameter(1f, 0.01f, 5f);

        public ClampedFloatParameter brightness = new ClampedFloatParameter(1f, 1f, 4);
        public ClampedFloatParameter length = new ClampedFloatParameter(5f, 1f, 10f);
        public ClampedFloatParameter angle = new ClampedFloatParameter(0f, 0f, 90f);
        
        public ClampedFloatParameter sampleDistance = new ClampedFloatParameter(0.1f, 0.1f,3f);
        [Range(1, 4), Tooltip("Every step halves the resolution of the blur effect. Lower resolution provides a smoother blur but may induce flickering")]
        public ClampedIntParameter downscaling = new ClampedIntParameter(2, 1, 4);

        public new bool IsActive() => active && intensity.value > 0f;
    }
}