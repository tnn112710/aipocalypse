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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Rendering/Motion Trails")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.MotionTrails, typeof(MotionTrailsRenderer))]
    [EffectDocs("motion-trails")]
    public sealed class MotionTrails : PostProcessingComponent
    {
        public enum BufferBlendMode
        {
            Additive,
            Max,
        }

        [Serializable]
        public sealed class BufferBlendModeParameter : VolumeParameter<BufferBlendMode>
        {
            public BufferBlendModeParameter(BufferBlendMode value, bool overrideState = false)
                : base(value, overrideState)
            {
                this.value = value;
                this.overrideState = overrideState;
            }
        }
        
        [Tooltip("Blends the gradient through various Photoshop-like blending modes")]
        public BufferBlendModeParameter blendMode = new BufferBlendModeParameter(BufferBlendMode.Max);
        
        public enum Interval
        {
            EveryFrame,
            Seconds,
        }

        [Serializable]
        public sealed class IntervalModeParameter : VolumeParameter<Interval> { };
        public IntervalModeParameter intervalMode = new IntervalModeParameter();
        [Tooltip("The interval at which the effect will be captured")]
        public ClampedFloatParameter captureInterval = new ClampedFloatParameter(0.1f, 0.05f, 1f);
        
        [Range(0.01f, 5f), Tooltip("Luminance threshold, pixels above this threshold (material's emission value) will contribute to the effect")]
        public ClampedFloatParameter luminanceThreshold = new ClampedFloatParameter(0.5f, 0.01f, 5f);
        [Tooltip("Enable to clamp luminance to the range [0, 1], this avoids bright pixels creating trails that take longer to fade out")]
        public BoolParameter clamped = new BoolParameter(false);
        public ClampedFloatParameter length = new ClampedFloatParameter(1f, 0f, 1f);
        
        public Vector3Parameter constantDirection = new Vector3Parameter(new Vector3(0f, 0f, 0f));
        [Tooltip("If enabled, motion from objects that appear static on the screen will still contribute to the effect." +
                 "\n\n" +
                 "Only enable this if it desired for objects such a FPS weapons or third-person cars (tail light trails).")]
        public BoolParameter useMotionVectors = new BoolParameter(false);
        public FloatParameter motionVectorsIntensity = new FloatParameter(1f);
        
        public new bool IsActive() => active && intensity.value > 0f;
    }
}