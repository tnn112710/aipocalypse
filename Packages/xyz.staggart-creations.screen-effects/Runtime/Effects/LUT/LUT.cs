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
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Image Enhancement/Color Grading LUT")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.LUT, typeof(LUTRenderer))]
    [EffectDocs("color-grading-lut")]
    public sealed class LUT : PostProcessingComponent
    {
        public enum Mode
        {
            Single = 0,
            DistanceBased = 1,
        }

        [Serializable]
        public sealed class ModeParam : VolumeParameter<Mode> { }

        [Tooltip("Distance-based mode blends two LUTs over a distance")]
        public ModeParam mode = new ModeParam { value = Mode.Single };

        public FloatParameter startFadeDistance = new FloatParameter(0f);
        public FloatParameter endFadeDistance = new FloatParameter(1000f);
        
        [Tooltip("Supply a LUT strip texture.")]
        [VolumeTextureDefault("11e912b736bdca742b4e9c7b031324d3")]
        public TextureParameter lutNear = new TextureParameter(null);
        [VolumeTextureDefault("5272fc63bd3264e46b6a5591e46f78c7")]
        public TextureParameter lutFar = new TextureParameter(null);
        
        [Range(0f, 1f)]
        public ClampedFloatParameter invert = new ClampedFloatParameter(0f, 0f, 1f);
        
        [Range(0f, 1f), Tooltip("Increases the saturation of muted colors")]
        public ClampedFloatParameter vibrance = new ClampedFloatParameter(0f, 0f, 1f);
        
        [ColorUsage(false, false), Tooltip("Controls the effect of vibrancy for each color channel (RGB)")]
        public ColorParameter vibranceRGBBalance = new ColorParameter(Color.white);

        public new bool IsActive() => active && (intensity.value > 0f || invert.value > 0f || vibrance.value > 0f);

        //Used to temporarily disable the effect while capturing a screenshot for LUT extraction
        [NoAutoStaticsCleanup]
        public static bool Bypass = false;
    }
}