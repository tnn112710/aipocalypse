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
    [Serializable, VolumeComponentMenu("SC Screen Effects/Feedback/Radial Blur")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    [PostProcessingVolumeComponent(ShaderNames.RadialBlur, typeof(RadialBlurRenderer))]
    [EffectDocs("radial-blur-2")]
    public sealed class RadialBlur : PostProcessingComponent
    {
        public FloatParameter blurLength = new FloatParameter(0.5f);
        
        [Tooltip("Sets the blur center point (screen center is [0.5, 0.5]).")]
        public Vector2Parameter center = new Vector2Parameter( new Vector2(0.5f, 0.5f) );
        
        [Range(-180f, 180f)]
        public ClampedFloatParameter angle = new ClampedFloatParameter(0f, -180f, 180f);

        [Tooltip("Distance between screen samples along the blur direction. This is a crucial performance parameter, increase it as high as possible without causing visual artifacts.")]
        public ClampedFloatParameter sampleDistance = new ClampedFloatParameter(0.33f, 0.1f,1);

        private Vector2 m_center;
        
        public void CenterOnScreenPosition(Vector2 screenPosition)
        {
            m_center.x = screenPosition.x / Screen.width;
            m_center.y = screenPosition.y / Screen.height;
            
            center.Override(m_center);
        }
        
        public void CenterOnWorldPosition(Camera camera, Vector3 worldPosition)
        {
            if (!camera) return;
            
            Vector3 position = camera.WorldToViewportPoint(worldPosition);

            m_center.x = position.x;
            m_center.y = position.y;
            
            center.Override(m_center);
        }

        public void ResetCenter()
        {
            center.Override(Vector2.one * 0.5f);
        }
        
        public new bool IsActive() => active && intensity.value > 0f;
    }
}