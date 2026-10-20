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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Stylized/Kuwahara")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Kuwahara, typeof(KuwaharaRenderer))]
    [EffectDocs("kuwahara-2")]
    public sealed class Kuwahara : PostProcessingComponent
    {
        [InspectorName("Resolution"), Tooltip("Low, quarter resolution\n\nNormal, half resolution\n\nHigh, full resolution\n\nLower resolutions may induce jittering")]
        public ResolutionParameter resolution = new ResolutionParameter { value = Resolution.Full };
        
        //[Range(0, 8), DisplayName("Radius")]
        public ClampedIntParameter radius = new ClampedIntParameter(3, 0, 8);

        public BoolParameter distanceFade = new BoolParameter(true);
        public FloatParameter startFadeDistance = new FloatParameter(100f);
        public FloatParameter endFadeDistance = new FloatParameter(500f);

        public new bool IsActive() => active && intensity.value > 0 && radius.value > 0;
    }
}