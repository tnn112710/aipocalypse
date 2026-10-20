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
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Overlay/Black Bars")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.BlackBars, typeof(BlackBarsRenderer))]
    [EffectDocs("black-bars-2")]
    public sealed class BlackBars : PostProcessingComponent
    {
        public enum Direction
        {
            Horizontal = 0,
            Vertical = 1,
        }

        [Serializable]
        public sealed class DirectionParam : VolumeParameter<Direction> { }

        //[DisplayName("Direction"), Tooltip("")]
        public DirectionParam mode = new DirectionParam { value = Direction.Horizontal };

        [Range(0f, 1f), Tooltip("Max Size")]
        public ClampedFloatParameter maxSize = new ClampedFloatParameter(0.33f, 0f, 1f);

        public new bool IsActive() => active && intensity.value > 0f;
    }
}