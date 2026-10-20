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
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.Serialization;

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Stylized/Edge Detection")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.EdgeDetection, typeof(EdgeDetectionRenderer))]
    [EffectDocs("edge-detection-2")]
    public sealed partial class EdgeDetection : PostProcessingComponent
    {
        [Range(0f, 1f), Tooltip("Shows only the effect, to allow for fine-tuning")]
        [NonSerialized]
        [AutoStaticsCleanup]
        public static float debug = 0f;

        public enum Solver
        {
            CrossAnalysis = 0,
            Gradient = 1,
        }

        [Serializable]
        public sealed class EdgeDetectionMode : VolumeParameter<Solver> { }

        [Space]
        [Tooltip("Choose one of the two edge solvers")]
        public EdgeDetectionMode mode = new() { value = Solver.CrossAnalysis };
        
        [FormerlySerializedAs("sensitivityDepth")] [InspectorName("Depth"), Tooltip("Sets how much difference in depth between pixels contribute to drawing an edge")]
        public ClampedFloatParameter depthInfluence = new ClampedFloatParameter(1f, 0f, 1f, true);

        [FormerlySerializedAs("sensitivityNormals")] [InspectorName("Normals"), Tooltip("Sets how much difference in normals between pixels contribute to drawing an edge")]
        public ClampedFloatParameter normalInfluence = new ClampedFloatParameter(0f, 0f, 1f);

        [FormerlySerializedAs("lumThreshold")] [InspectorName("Color"), Tooltip("Color threshold, pixels below this threshold will not contribute to the effect")]
        public ClampedFloatParameter colorInfluence = new ClampedFloatParameter(0.0f, 0.0f, 1f);

        [InspectorName("Color"), Tooltip("")]
        public ColorParameter edgeColor = new ColorParameter(Color.black);

        [Range(0f, 5f), Tooltip("Edge Exponent")]
        public ClampedFloatParameter edgeExp = new ClampedFloatParameter(0f, 0f, 5f);

        [InspectorName("Size"), Range(1, 4), Tooltip("Size of an edge in pixels")]
        public ClampedIntParameter edgeSize = new ClampedIntParameter(1, 1, 4);
        
        [Tooltip("Enables the use of a 3D noise texture to add a subtle variation to the effect.")]
        public BoolParameter noise = new BoolParameter(false);
        [VolumeTextureDefault("1cb3feda20ecd5948b709bbe7e74ebaf")]
        public Texture3DParameter noiseTex3D = new Texture3DParameter(null);
        [Tooltip("The time in seconds between each noise animation frame. Set to 0 to disable.")]
        public FloatParameter noiseAnimationInterval = new FloatParameter(0f);
        public FloatParameter noiseOffsetScale = new FloatParameter(6f);
        public ClampedFloatParameter noiseOffsetStrength = new ClampedFloatParameter(0.25f, 0, 1);
        public ClampedFloatParameter noiseThicknessStrength = new ClampedFloatParameter(0.25f, 0, 1);
        public FloatParameter noiseClippingScale = new FloatParameter(1f);
        public ClampedFloatParameter noiseClippingStrength = new ClampedFloatParameter(0f, 0, 0.5f);
        
        [Tooltip("Fades out the effect between the cameras near and far clipping plane")]
        public BoolParameter distanceFade = new BoolParameter(false);
        public BoolParameter invertFadeDistance = new BoolParameter(false);
        public FloatParameter startFadeDistance = new FloatParameter (0f);
        public FloatParameter endFadeDistance = new FloatParameter(1000f);

        internal override void WarnWhenMissingResources()
        {
            if(noise.value) ThrowWarningExceptionIfNull(noiseTex3D);
        }

        public new bool IsActive() => active && intensity.value > 0f && (depthInfluence.value > 0f || (mode.value == Solver.CrossAnalysis && normalInfluence.value > 0f) || colorInfluence.value > 0f);
    }
}