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

namespace sc.screeneffects.components.runtime
{
    /// <summary>
    /// Controls the weight of a volume based on the velocity of the transform.
    /// </summary>
    [ExecuteInEditMode]
    public class VolumeWeightByVelocity : MonoBehaviour
    {
        [Tooltip("Minimum velocity threshold for volume weight to above 0.")]
        public float minVelocity = 1f;
        [Tooltip("Velocity at which volume is at full intensity (km/h).")]
        public float maxVelocity = 3f;

        [Space] public Volume volume;
        
        private Vector3 previousPosition;
        
        private float velocity;
        private float weight;
        
        public float CurrentVelocity => velocity;

        private void Reset()
        {
            volume = GetComponent<Volume>();
        }

        private void OnEnable()
        {
            previousPosition = transform.position;
        }

        private void Update()
        {
            velocity = Vector3.Distance(transform.position, previousPosition);
            previousPosition = transform.position;
            
            weight = Mathf.InverseLerp(minVelocity, maxVelocity, velocity);

            if (volume)
            {
                volume.weight = weight;
            }
        }
    }
}