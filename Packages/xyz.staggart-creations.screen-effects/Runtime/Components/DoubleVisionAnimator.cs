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
using sc.screeneffects.runtime;
using UnityEngine;

namespace sc.screeneffects.examples.runtime
{
    public class DoubleVisionAnimator : MonoBehaviour
    {
        [Range(0, 1)]
        public float intensity = 0.5f;
        [Range(0, 1)]
        public float edgeMask = 0.66f;
        
        [Header("Animation")]
        public float rotationSpeed = 0.5f;
        public float phaseSpeed = 1f;

        private DoubleVision effect;
        private float offset;
        
        private void Start()
        {
            effect = PostProcessingComponent.Create<DoubleVision>();
        }

        private void Update()
        {
            offset = Mathf.Sin(Time.time * phaseSpeed) * 0.5f + 0.75f;
            float m_angle = (Mathf.PingPong((Time.time + 0.5f) * rotationSpeed, 1) * 0.5f + 0.5f) * 360f;
            
            effect.edgeMask.Override(edgeMask);
            effect.intensity.Override(offset * intensity);
            effect.angle.Override(m_angle);
        }

        private void OnDestroy()
        {
            effect.Dispose();
        }
    }
}