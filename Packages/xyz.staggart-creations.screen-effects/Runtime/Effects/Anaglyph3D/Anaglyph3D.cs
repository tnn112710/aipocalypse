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

namespace sc.screeneffects.runtime
{
    [Serializable, VolumeComponentMenu("SC Screen Effects/Rendering/Anaglyph 3D")]
    [PostProcessingVolumeComponent(ShaderNames.Anaglyph3D, typeof(Anaglyph3DRenderer))]
    [EffectDocs("anaglyph-3d")]
    public class Anaglyph3D : PostProcessingComponent
    {
        public enum GlassesType
        {
            RedCyan,
            GreenMagenta,
            GreenRed,
        }

        [Serializable]
        public sealed class GlassesParam : VolumeParameter<GlassesType> { }
        
        public GlassesParam glasses = new GlassesParam { value = GlassesType.RedCyan };
        
        [Tooltip("Maximum distance, at which point the eye separation becomes 0. Tune this to the context. For indoors scenes this should be lower, for landscapes much higher (eg 500)")]
        public FloatParameter focalPoint = new FloatParameter(8f);
        [Tooltip("Offset between eyes, values too large can break the depth perception.")]
        public ClampedFloatParameter eyeSeparation = new ClampedFloatParameter(0.1f, 0f, 1f);
        [Tooltip("Convert the image to grayscale. Use if colors aren't important as certain glasses types causes a perceivable flickering on certain color ranges.")]
        public BoolParameter monochrome = new BoolParameter(false);

        private void OnValidate()
        {
            focalPoint.value = Mathf.Max(focalPoint.value, 1f);
        }

        public new bool IsActive() => active && intensity.value > 0f && eyeSeparation.value > 0f;
    }
}