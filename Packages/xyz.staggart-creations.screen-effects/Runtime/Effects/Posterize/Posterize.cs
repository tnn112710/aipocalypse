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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Retro/Posterize")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Posterize, typeof(PosterizeRenderer))]
    [EffectDocs("posterize-2")]
    public sealed class Posterize : PostProcessingComponent
    {
        public BoolParameter hsvMode = new BoolParameter(true);

        [Range(0, 256)]
        public ClampedIntParameter levels = new ClampedIntParameter(256, 2, 256);

        [Header("Levels")]
        [Range(2, 256)]
        public ClampedIntParameter hue = new ClampedIntParameter(256, 2, 256);
        [Range(2, 256)]
        public ClampedIntParameter value = new ClampedIntParameter(256, 2, 256);
        [Range(2, 256)]
        public ClampedIntParameter saturation = new ClampedIntParameter(256, 2, 256);

        public new bool IsActive()
        {
            if(!active || intensity.value <= 0) return false;

            if (hsvMode.value)
            {
                return (hue.value < 256 || saturation.value < 256 || value.value < 256);
            }
            else
            {
                return levels.value < 256;
            }
        } 
    }
}