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

using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Blurring/Blur")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Blur, typeof(BlurRenderer))]
    [EffectDocs("blur-2")]
    public sealed class Blur : PostProcessingComponent
    {
        public enum BlurMethod
        {
            Gaussian,
            Box
        }

        [Serializable]
        public sealed class BlurMethodParameter : VolumeParameter<BlurMethod> { }

        [Tooltip("Box blurring uses fewer texture samples but has a limited blur range")]
        public BlurMethodParameter mode = new BlurMethodParameter { value = BlurMethod.Gaussian };
        
        [Range(0f, 5f), Tooltip("The amount of blurring that must be performed")]
        public ClampedFloatParameter radius = new ClampedFloatParameter(1f, 0f, 5f);

        [Range(1, 12), Tooltip("The number of times the effect is blurred. More iterations provide a smoother effect but induce more drawcalls.")]
        public ClampedIntParameter iterations = new ClampedIntParameter(6, 1, 12);

        [Range(1, 4), Tooltip("Every step halves the resolution of the blur effect. Lower resolution provides a smoother blur but may induce flickering.")]
        public ClampedIntParameter downscaling = new ClampedIntParameter(2, 1, 8);
        
        public BoolParameter distanceFade = new BoolParameter(false);
        public FloatParameter startFadeDistance = new FloatParameter(0f);
        public FloatParameter endFadeDistance = new FloatParameter(500f);

        public new bool IsActive() => active && intensity.value > 0f && radius.value > 0f;
    }
}