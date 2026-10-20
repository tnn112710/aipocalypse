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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Retro/Color Split")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.ColorSplit, typeof(ColorSplitRenderer))]
    [EffectDocs("color-split-2")]
    public sealed class ColorSplit : PostProcessingComponent
    {
        public enum SplitMode
        {
            [InspectorName("Horizontal")]
            Single,
            [InspectorName("Horizontal + Vertical")]
            Double
        }

        [Serializable]
        public sealed class SplitModeParam : VolumeParameter<SplitMode> { }

        [Tooltip("Box filtered methods provide a subtle blur effect and are less efficient")]
        public SplitModeParam mode = new SplitModeParam { value = SplitMode.Single };

        [Range(0f, 1f), Tooltip("The amount by which the color channels offset")]
        public FloatParameter offset = new FloatParameter(0f);
        
        [Tooltip("0=Full screen. 1=Limit to screen edges")]
        public ClampedFloatParameter edgeMasking = new ClampedFloatParameter(0f, 0f, 1f);

        [Range(0f, 3f), Tooltip("Luminance threshold, pixels above this threshold will contribute to the effect")]
        public ClampedFloatParameter luminanceThreshold = new ClampedFloatParameter(0f, 0f, 3f);
        
        public new bool IsActive() => active && offset.value > 0f && intensity.value > 0;
    }
}