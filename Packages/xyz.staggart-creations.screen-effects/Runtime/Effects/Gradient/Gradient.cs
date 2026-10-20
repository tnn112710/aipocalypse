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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Color/Gradient")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Gradient, typeof(GradientRenderer))]
    [EffectDocs("gradient-2")]
    public sealed class Gradient : PostProcessingComponent
    {
        public enum Mode
        {
            ColorFields,
            Texture
        }

        [Serializable]
        public sealed class GradientModeParameter : VolumeParameter<Mode> { }

        //[DisplayName("Direction"), Tooltip("")]
        public GradientModeParameter input = new GradientModeParameter();

        [Tooltip("The color's alpha channel controls its opacity")]
        public ColorParameter color1 = new ColorParameter(new Color(0f, 0.8f, 0.56f, 0.5f));
        [Tooltip("The color's alpha channel controls its opacity")]
        public ColorParameter color2 = new ColorParameter(new Color(0.81f, 0.37f, 1f, 0.5f));

        [Range(0f, 1f), Tooltip("Size")]
        public ClampedFloatParameter rotation = new ClampedFloatParameter(0f, 0f, 1f);

        //[DisplayName("Gradient"), Tooltip("")]
        [VolumeTextureDefault("15ac07d634de8aa4bb63da4698fc503b")]
        public TextureParameter gradientTex = new TextureParameter(null);
        
        public BlendModeFullParameter mode = new BlendModeFullParameter();

        public new bool IsActive() => active && intensity.value > 0f;
    }
}