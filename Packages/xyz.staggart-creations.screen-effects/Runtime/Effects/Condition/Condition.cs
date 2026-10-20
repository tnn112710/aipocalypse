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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Feedback/Condition")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Condition, typeof(ConditionRenderer))]
    [EffectDocs("condition")]
    public sealed class Condition : PostProcessingComponent
    {
        [VolumeTextureDefault("43a1c3c1c5c02454a8f9d9dc208f4337")]
        public TextureParameter overlayTex = new TextureParameter(null);
        
        public ColorParameter color = new ColorParameter(new Color(0.66f, 0f, 0f), true, true, false);
        public ClampedFloatParameter vignetting = new ClampedFloatParameter(0.1f, 0f, 1f);
        
        public AnimationParameter textureAnimation1 = new AnimationParameter(new TextureAnimation(Vector2.up, 0f, 1f));
        public BoolParameter dualLayer = new BoolParameter(true);
        public AnimationParameter textureAnimation2 = new AnimationParameter(new TextureAnimation(Vector2.up, 0f, 0.5f));
        
        public FloatParameter animationInterval = new FloatParameter(0f);
        public ClampedFloatParameter falloff = new ClampedFloatParameter(0.5f, 0f, 1f);
        public BoolParameter hardFalloff = new BoolParameter(false);
        public BoolParameter circular = new BoolParameter(false);

        internal override void WarnWhenMissingResources()
        {
            ThrowWarningExceptionIfNull(overlayTex);
        }

        public new bool IsActive() => active && intensity.value > 0;
    }
}