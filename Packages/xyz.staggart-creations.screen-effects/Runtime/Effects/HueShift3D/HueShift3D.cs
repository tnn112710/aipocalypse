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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Color/3D Hue Shift")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.HueShift3D, typeof(HueShift3DRenderer))]
    [EffectDocs("hue-shift-3d-2")]
    public sealed class HueShift3D : PostProcessingComponent
    {
        public enum ColorSource
        {
            RGBSpectrum,
            GradientTexture
        }

        [Serializable]
        public sealed class ColorSourceParameter : VolumeParameter<ColorSource> { }
        
        [Tooltip("Box blurring uses fewer texture samples but has a limited blur range")]
        public ColorSourceParameter colorSource = new ColorSourceParameter { value = ColorSource.RGBSpectrum };
        
        public TextureParameter gradientTex = new TextureParameter(null);

        [Range(0f, 1f), Tooltip("Speed")]
        public ClampedFloatParameter speed = new ClampedFloatParameter(0.3f, 0f,1f);

        [Range(0f, 3f), Tooltip("Size")]
        public ClampedFloatParameter size = new ClampedFloatParameter(1f,0f,3f);

        [Range(0f, 10f), Tooltip("Bends the effect over the scene's geometry normals\n\nHigh values may induce banding artifacts")]
        public ClampedFloatParameter geoInfluence = new ClampedFloatParameter(5f,0f,10f);
        
        public new bool IsActive() => active && intensity.value > 0f;
        
        //Serialized on profile so its included in a build
        [SerializeField]
        public Shader DepthNormalsShader;

        protected override bool SerializeShader()
        {
            DepthNormalsShader = Shader.Find(ShaderNames.DepthNormals);

            return base.SerializeShader();
        }
    }
}