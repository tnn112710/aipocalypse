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
using UnityEngine.Serialization;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Misc/Kaleidoscope")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Kaleidoscope, typeof(KaleidoscopeRenderer))]
    [EffectDocs("kaleidoscope-2")]
    public sealed class Kaleidoscope : PostProcessingComponent
    {
        [FormerlySerializedAs("splits")]
        [Tooltip("The number of times the screen is split up")]
        public ClampedIntParameter radialSplits = new ClampedIntParameter(5, 0, 10);
        
        public ClampedFloatParameter horizontalSplits = new ClampedFloatParameter(0, 0, 6);
        
        public ClampedFloatParameter verticalSplits = new ClampedFloatParameter(0, 0 ,6);
        
        [Tooltip("Sets the pivot point (screen center is [0.5, 0.5]).")]
        public Vector2Parameter center = new Vector2Parameter( new Vector2(0.5f, 0.5f) );
        
        [Space]
        
        public BoolParameter maintainAspectRatio = new BoolParameter (false);
        [Tooltip("Mirror the screen at the borders")]
        public BoolParameter borderReflection = new BoolParameter (true);
        
        public new bool IsActive() => active && intensity.value > 0 && (radialSplits.value > 0 || horizontalSplits.value > 0 || verticalSplits.value > 0);
    }
}