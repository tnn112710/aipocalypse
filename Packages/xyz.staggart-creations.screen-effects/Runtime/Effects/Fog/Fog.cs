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
using UnityEngine.Rendering;
using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Environment/Fog")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Fog, typeof(FogRenderer))]
    [EffectDocs("fog-2")]
    public sealed class Fog : PostProcessingComponent
    {
        [Serializable]
        public sealed class FogModeParameter : VolumeParameter<FogMode> { }
        [Tooltip("Sets how the fog distance is calculated")]
        public FogModeParameter fogMode = new FogModeParameter { value = FogMode.Exponential };

        //[DisplayName("Start")]
        [FormerlySerializedAs("fogStartDistance")] public FloatParameter startDistance = new FloatParameter(0f);
        //[DisplayName("End")]
        [FormerlySerializedAs("fogEndDistance")] public FloatParameter endDistance = new FloatParameter(100f);

        public enum FogColorSource
        {
            UniformColor,
            Gradient,
            SkyboxColor
        }

        [Serializable]
        public sealed class FogColorSourceParameter : VolumeParameter<FogColorSource> { }

        [Space]

        [Tooltip("Color: use a uniform color for the fog\n\nGradient: sample a gradient texture to control the fog color over distance, the alpha channel controls the density\n\nSkybox: Sample the skybox's color for the fog, only works well with low detail skies")]
        public FogColorSourceParameter colorSource = new FogColorSourceParameter { value = FogColorSource.UniformColor };
        
        [ColorUsage(true, true)]
        public ColorParameter fogColor = new ColorParameter(new Color(0.76f, 0.94f, 1f, 1f), true, false, true, false);

        //[DisplayName("Texture")]
        //public TextureParameter fogColorGradient = new TextureParameter(null);
        public TextureGradientParameter fogColorGradient = new TextureGradientParameter(new TextureGradient(new UnityEngine.Gradient()), false);
        
        [Tooltip("Automatic mode uses the current camera's far clipping plane to set the max distance\n\nOtherwise, a fixed value may be used instead")]
        public FloatParameter gradientDistance = new FloatParameter(1000f);
        public BoolParameter gradientUseFarClipPlane = new BoolParameter(true);

        [Header("Distance")]
        //[DisplayName("Enable")]
        public BoolParameter distanceFog = new BoolParameter(true);
        [Min(0f)]
        public FloatParameter distanceDensity = new FloatParameter(1f);
        [Tooltip("Distance based on radial distance from viewer, rather than parrallel")]
        public BoolParameter useRadialDistance = new BoolParameter(true);
        
        public Vector2Parameter horizonMinMax = new Vector2Parameter(new Vector2(0,1));

        [Range(0f, 1f), Tooltip("Determines how much the fog influences the skybox")]
        public ClampedFloatParameter skyboxInfluence = new ClampedFloatParameter(1f,0f,1f);
        [Tooltip("Apply a Box blur to the skybox color used in the fog. Often needed if the skybox has high frequency details.")]
        public FloatParameter skyboxBlurring = new FloatParameter(0f);

        #region Directional Light
        [Tooltip("Translates the a Directional Light's direction and color into the fog. Creates a faux-atmospheric scattering effect.")]
        public BoolParameter enableDirectionalLight = new BoolParameter(false);
        
        [Tooltip("How much of the sun light's intensity is multiplied")]
        public ClampedFloatParameter sunIntensityInfluence = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter sunColorInfluence = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Adds an extra color temperature value (in Kelvin)")]
        public ClampedFloatParameter sunTemperatureShift = new ClampedFloatParameter(0f, -2000f, 2000f);

        
        [Tooltip("Use the intensity of the Directional Light that's set as the caster")]
        public BoolParameter useLightDirection = new BoolParameter(true);

        [ColorUsage(true, true)]
        public ColorParameter sunLightColor = new ColorParameter(new Color(1f, 0.89f, 0.55f, 1f));
        //[Max(1f)]
        public Vector3Parameter lightDirection = new Vector3Parameter(new Vector3(0, 0.5f, -1f));
        public FloatParameter sunLightIntensity = new FloatParameter(1f);
        public ClampedFloatParameter lightExponent = new ClampedFloatParameter(8f, 1f, 100f);
        #endregion
        
        #region Height Fog
        [Tooltip("Enable vertical height fog")]
        public BoolParameter heightFog = new BoolParameter(false);

        [Tooltip("Height relative to 0 world height position")]
        public FloatParameter height = new FloatParameter(10f);
        [Tooltip("Invert the height fog so it accumulates above the height level instead of below it")]
        public BoolParameter invertHeightFog = new BoolParameter(false);
        
        public ClampedFloatParameter heightDensity = new ClampedFloatParameter(0.75f, 0.001f, 1f);
        
        [Tooltip("Enables height fog density variation through the use of a texture")]
        public BoolParameter heightFogNoise = new BoolParameter(true);
        [VolumeTextureDefault("708d947190c537a4e8ad0c99eeca62f5")]
        public Texture3DParameter heightNoiseTex3D = new Texture3DParameter(null);
        
        public ClampedFloatParameter heightNoiseSize = new ClampedFloatParameter (0.25f,0.1f,5f);
        public ClampedFloatParameter heightNoiseStrength = new ClampedFloatParameter(1f,0f,1f);
        public ClampedFloatParameter heightNoiseHorizontalSpeed = new ClampedFloatParameter(0.25f,-1f,1f);
        public ClampedFloatParameter heightNoiseVerticalSpeed = new ClampedFloatParameter(0.25f,-1f,1f);
        #endregion
        
        public new bool IsActive() => intensity.value > 0f && this.active;

        /// <summary>
        /// Marks the gradient as changed, so it'll be internally rebuilt into a usable texture
        /// </summary>
        public void RebuildGradient()
        {
            fogColorGradient.value.SetDirty();
        }
        
        private void OnValidate()
        {
            if (colorSource.value == FogColorSource.Gradient && fogColorGradient.value.textureSize <= 0)
            {
                TextureGradient textureGradient = new TextureGradient(
                    new GradientColorKey[2] { new (new Color(0f, 1f, 0.89f), 0), new (new Color(0f, 0.53f, 1f), 1f) }, 
                    new GradientAlphaKey[2] { new (0f, 0f), new (1f, 1f) }, 
                    GradientMode.Blend, ColorSpace.Linear, 16, false);
                
                fogColorGradient = new TextureGradientParameter(textureGradient);
                RebuildGradient();
            }
        }

        protected override void OnDestroy()
        {
            fogColorGradient.Release();
        }
    }
}