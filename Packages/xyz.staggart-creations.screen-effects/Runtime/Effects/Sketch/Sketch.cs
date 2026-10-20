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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Stylized/Sketch")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Sketch, typeof(SketchRenderer))]
    [EffectDocs("sketch-2")]
    public sealed class Sketch : PostProcessingComponent
    {
        [Tooltip("The Red channel is used for darker shades, whereas the Green channel is for lighter.")]
        [VolumeTextureDefault("21481de04ce9b6b49ab193b969e8d92d")]
        public TextureParameter strokeTex = new TextureParameter(null);

        public enum SketchProjectionMode
        {
            WorldSpace,
            ScreenSpace
        }
        [Serializable]
        public sealed class SketchProjectionParameter : VolumeParameter<SketchProjectionMode> { }

        [Space]
        [Tooltip("Choose the type of UV space being used")]
        public SketchProjectionParameter projectionMode = new SketchProjectionParameter { value = SketchProjectionMode.WorldSpace };

        public enum SketchMode
        {
            EffectOnly,
            Multiply,
            Add
        }
        [Serializable]
        public sealed class SketchModeParameter : VolumeParameter<SketchMode> { }

        [Tooltip("Choose one of the different modes")]
        public SketchModeParameter blendMode = new SketchModeParameter { value = SketchMode.EffectOnly };    

        public ClampedFloatParameter darkThreshold = new ClampedFloatParameter(0.1f, 0f, 1f);
        public ClampedFloatParameter lightThreshold = new ClampedFloatParameter(1f, 0f, 3f);
        public ClampedFloatParameter contrast = new ClampedFloatParameter(0f, 0f, 1f);
        
        public ClampedFloatParameter tiling = new ClampedFloatParameter(16f, 1f, 128f);
        public FloatParameter animationInterval = new FloatParameter(0f);

        public new bool IsActive() => active && intensity.value > 0f && strokeTex.value;
    }
}