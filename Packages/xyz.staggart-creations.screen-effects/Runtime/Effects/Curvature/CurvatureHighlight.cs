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

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    [VolumeComponentMenu("SC Screen Effects/Stylized/Curvature Highlight")]
    [VolumeRequiresRendererFeatures(typeof(CurvatureHighlightRenderer))]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.CurvatureHighlight, typeof(CurvatureHighlightRenderer))]
    [DisplayInfo(name = "Curvature Highlight")]
    [EffectDocs("curvature-highlight")]
    public sealed class CurvatureHighlight : PostProcessingComponent
    {
        public ClampedFloatParameter threshold = new ClampedFloatParameter(2f, 1f, 8f);
        public ClampedIntParameter radius = new ClampedIntParameter(1, 1, 4);
        public ClampedFloatParameter softness = new ClampedFloatParameter(0f, 0f, 1f);
        
        public ClampedFloatParameter convexity = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter concavity = new ClampedFloatParameter(0.5f, 0f, 1f);
        
        public new bool IsActive()
        {
            return this.active && intensity.value > 0f && (convexity.value > 0f || concavity.value > 0f);
        }
    }
}
