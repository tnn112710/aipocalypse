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
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Blurring/Tilt Shift")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.TiltShift, typeof(TiltShiftRenderer))]
    [EffectDocs("tilt-shift-2")]
    public sealed class TiltShift : PostProcessingComponent
    {
        public enum TiltShiftMethod
        {
            Horizontal,
            Radial,
        }

        [Serializable]
        public sealed class TiltShiftMethodParameter : VolumeParameter<TiltShiftMethod> { }

        public TiltShiftMethodParameter mode = new TiltShiftMethodParameter();
        
        public ClampedFloatParameter areaSize = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter areaFalloff = new ClampedFloatParameter(1f, 0.01f, 1f);
        public ClampedFloatParameter offset = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter angle = new ClampedFloatParameter(0f, 0f, 360f);
        
        public new bool IsActive() => active && intensity.value > 0f;
        
        [NoAutoStaticsCleanup]
        public static bool debug;
    }
}