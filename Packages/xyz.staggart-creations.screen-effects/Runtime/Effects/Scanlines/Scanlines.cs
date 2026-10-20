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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Retro/Scanlines")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Scanlines, typeof(ScanlinesRenderer))]
    [EffectDocs("scanlines-2")]
    public sealed class Scanlines : PostProcessingComponent
    {
        public enum LineColor
        {
            Dark,
            Bright
        }
        
        [Serializable]
        public sealed class LineColorParam : VolumeParameter<LineColor> { }

        public LineColorParam lineColor = new LineColorParam { value = LineColor.Dark };
        
        public ClampedIntParameter linePixelSpacing = new ClampedIntParameter(1, 1, 10);
        [FormerlySerializedAs("amount")]
        public ClampedIntParameter linesManual = new ClampedIntParameter(700, 32, 2048);
        public BoolParameter resolutionDependent = new BoolParameter(true);

        [Range(-1f, 1f), Tooltip("Animation speed")]
        public ClampedFloatParameter speed = new ClampedFloatParameter(0f, -1f, 1f);

        public new bool IsActive() => active && intensity.value > 0f;
    }
}