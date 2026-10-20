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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Misc/Transition")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Transition, typeof(TransitionRenderer))]
    [EffectDocs("transition-2")]
    public sealed class Transition : PostProcessingComponent
    {
        [VolumeTextureDefault("90429d55138b87946858156650b3768c")]
        public TextureParameter gradientTex = new TextureParameter(null);
        public ColorParameter color = new ColorParameter(Color.black);

        public ClampedFloatParameter progress = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter softness = new ClampedFloatParameter(0f, 0f, 1f);
        
        public BoolParameter invert = new BoolParameter(false);

        internal override void WarnWhenMissingResources()
        {
            ThrowWarningExceptionIfNull(gradientTex);
        }

        public new bool IsActive() => active && intensity.value > 0 && progress.value > 0f && color.value.a > 0;
    }
}