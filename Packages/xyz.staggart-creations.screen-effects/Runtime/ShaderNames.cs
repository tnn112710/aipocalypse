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

using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace sc.screeneffects.runtime
{
    [NoAutoStaticsCleanup]
    public static class ShaderNames
    {
        public const string PREFIX = "Hidden/SC Screen Effects/";
        
        public const string TEST = PREFIX + "Test";
        public const string Anaglyph3D = PREFIX + "Anaglyph 3D";
        public const string AmbientOcclusion = PREFIX + "Ambient Occlusion";
        public const string BlackBars = (PREFIX + "Black Bars");
        public const string Blur = PREFIX + "Blur";
        public const string CurvatureHighlight = PREFIX + "Curvature Highlight";
        public const string ColorSplit = PREFIX + "Color Split";
        public const string Colorize = PREFIX + "Colorize";
        public const string Condition = PREFIX + "Condition";
        public const string Dithering = PREFIX + "Dithering";
        public const string DoubleVision = PREFIX + "Double Vision";
        public const string EdgeDetection = PREFIX + "Edge Detection";
        public const string Fog = PREFIX + "Fog";
        public const string Gradient = PREFIX + "Gradient";
        public const string HueShift3D = PREFIX + "3D Hue Shift";
        public const string Kaleidoscope = PREFIX + "Kaleidoscope";
        public const string Kuwahara = PREFIX + "Kuwahara";
        public const string LightStreaks = PREFIX + "Light Streaks";
        public const string LUT = PREFIX + "LUT";
        public const string Mosaic = PREFIX + "Mosaic";
        public const string Overlay = PREFIX + "Overlay";
        public const string Pixelize = PREFIX + "Pixelize";
        public const string Posterize = PREFIX + "Posterize";
        public const string RadialBlur = PREFIX + "Radial Blur";
        public const string Refraction = PREFIX + "Refraction";
        public const string Ripples = PREFIX + "Ripples";
        public const string Scanlines = PREFIX + "Scanlines";
        public const string Sharpen = PREFIX + "Sharpen";
        public const string Sketch = PREFIX + "Sketch";
        public const string SpeedLines = PREFIX + "SpeedLines";
        public const string Sunshafts = PREFIX + "Sun Shafts";
        public const string TiltShift = PREFIX + "Tilt Shift";
        public const string MotionTrails = PREFIX + "MotionTrails";
        public const string Transition = PREFIX + "Transition";
        public const string TubeDistortion = PREFIX + "Tube Distortion";

        public const string BlurPass = PREFIX + "Blur Pass";
        public const string DepthNormals = PREFIX + "DepthNormals";
        public const string SphereMapper = PREFIX + "SphereMapper";
        public const string PostEffectMask = "SC Screen Effects/Write to Rendering Layer";
    }

    [NoAutoStaticsCleanup]
    internal static class ShaderKeywords
    {
        public const string ReconstructedDepthNormals = "_RECONSTRUCT_NORMAL";
    }

    [NoAutoStaticsCleanup]
    public static class ShaderParameters
    {
        public static readonly int RenderingLayer = Shader.PropertyToID("effect_RenderingLayer");
        
        public static int _DeferredRendering = Shader.PropertyToID("_DeferredRendering");
        
        public static readonly int unity_WorldToLight = Shader.PropertyToID("unity_WorldToLight");

        public static readonly int Params = Shader.PropertyToID("_Params");
        public static readonly int AnimationParams = Shader.PropertyToID("_AnimationParams");
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int FadeParams = Shader.PropertyToID("_FadeParams");
        public static readonly int BlurOffsets = Shader.PropertyToID("_BlurOffsets");
        public static readonly int _PostProcessingDebugSlider = Shader.PropertyToID("_PostProcessingDebugSlider");
    }

    [NoAutoStaticsCleanup]
    internal static class TextureNames
    {
        public const string Source = "_BlitTexture";
        public const string Blurred = "_BlurredTex";
        public const string _CameraDepthTexture = "_CameraDepthTexture";
        public const string _CameraNormalsTexture = "_CameraNormalsTexture";
        public const string _CameraOpaqueTexture = "_CameraOpaqueTexture";
        public const string _ScreenSpaceOcclusionTexture = "_ScreenSpaceOcclusionTexture";
        public const string _MotionVectorTexture = "_MotionVectorTexture";
        public const string FogSkyboxTex = "_SkyboxTex";
    }
}