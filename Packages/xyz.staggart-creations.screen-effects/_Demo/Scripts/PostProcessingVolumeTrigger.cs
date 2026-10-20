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

using UnityEngine;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    [ExecuteInEditMode]
    public class PostProcessingVolumeTrigger : MonoBehaviour
    {
        [Header("Target volume")]
        public Volume volume;
        [Space]

        public float decreaseSpeed = 1f;

        private float currentWeight = 0f;

        private void OnEnable()
        {
            //No volume assigned, try to get from current object
            if (volume == null)
            {
                volume = this.GetComponent<Volume>();
                if (volume) volume.weight = 0f;
            }
            //Set start initial weight to 0
            else
            {
                volume.weight = 0f;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            Trigger();
        }

        /// <summary>
        /// Sets the weight of the current volume to 1, after which it will decrease by the set descreaseSpeed value
        /// </summary>
        public void Trigger()
        {
            //Bump up weight to 1
            currentWeight = 1f;
        }

        private void Update()
        {
            //Decreased weight
            currentWeight = Mathf.Clamp01(currentWeight - Time.deltaTime * decreaseSpeed);

            if (!volume) return;

            //Set current weight
            volume.weight = currentWeight;
        }
    }
}