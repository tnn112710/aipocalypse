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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Retro/Pixelize")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Pixelize, typeof(PixelizeRenderer))]
    [EffectDocs("pixelize-2")]
    public sealed class Pixelize : PostProcessingComponent
    {
        public IntParameter resolution = new IntParameter(240);

        public enum ResolutionPreset
        {
            Custom = 1,
            [InspectorName("600p")]
            Sixhundred = 600,
            [InspectorName("480p")]
            FourEighty = 480,
            [InspectorName("240p")]
            TwoFourty = 240,
            [InspectorName("200p")]
            TwoHundred = 200,
            [InspectorName("160p")]
            HundredSixty = 160
        }

        [Serializable]
        public sealed class ResolutionPresetParameter : VolumeParameter<ResolutionPreset> { }

        public ResolutionPresetParameter resolutionPreset = new ResolutionPresetParameter { value = ResolutionPreset.Custom };

        [Tooltip("When disabled, pixels will retain a square aspect ratio")]
        public BoolParameter preserveAspectRatio = new BoolParameter(false);
        [Tooltip("When enabled, pixels are shifted by half. Mostly has a visible effect on extremely low resolutions")]
        public BoolParameter centerPixel = new BoolParameter(true);
        
        public new bool IsActive() => active && intensity.value > 0f;
    }
}