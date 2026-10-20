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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Distortion/Refraction")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.Refraction, typeof(RefractionRenderer))]
    [EffectDocs("refraction-2")]
    public sealed class Refraction : PostProcessingComponent
    {
        [FormerlySerializedAs("refractionTex")]
        [Tooltip("Takes a normal map to perturb the image")]
        [VolumeTextureDefault("7de8a6d345816614fa16c116494588d6")]
        public TextureParameter normalMap = new TextureParameter(null);
        
        public ColorParameter tint = new ColorParameter(new Color(1,1,1, 0.1f));

        public AnimationParameter textureAnimation = new AnimationParameter(new TextureAnimation(-Vector2.up, 0f, 1f));
        
        internal override void WarnWhenMissingResources()
        {
            ThrowWarningExceptionIfNull(normalMap);
        }

        public new bool IsActive() => active && intensity.value > 0f && normalMap.value;
    }
}