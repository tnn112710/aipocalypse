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

namespace sc.screeneffects.runtime
{
    /// <summary>
    /// Helper for creating a time-based animation. Allows for smooth speed changes in realtime. Shaders won't use _TimeParameters
    /// </summary>
    public struct TimeAnimation
    {
        private const float MAX_TIME_VALUE = 10000f;
        
#if UNITY_EDITOR
        private double lastEditorTime;
#endif
        private float timeOffset;
        private float deltaTime;
        private float timeSinceLastInterval;

        /// <summary>
        /// Returns Time.time, or the equivalent in the editor.
        /// </summary>
        /// <returns></returns>
        public static float GetCurrentTime()
        {
            float time = Time.time;
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                time = (float)UnityEditor.EditorApplication.timeSinceStartup;
            }
            #endif
            
            return time;
        }
        
        private void UpdateDeltaTime()
        {
            deltaTime = Time.deltaTime;
            
            #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                double editorTime = UnityEditor.EditorApplication.timeSinceStartup;
                deltaTime = lastEditorTime > 0.0 ? (float)(editorTime - lastEditorTime) : 0f;
                lastEditorTime = editorTime;
            }
            #endif
        }
        
        
        public void Update(float speed, float interval = 0f)
        {
            if (speed == 0) return;
            
            UpdateDeltaTime();

            if (interval > 0f)
            {
                timeSinceLastInterval += deltaTime;

                if (timeSinceLastInterval >= interval)
                {
                    timeSinceLastInterval = 0;
                
                    timeOffset += speed * GetCurrentTime();
                }
            }
            else
            {
                timeOffset += speed * deltaTime * Time.timeScale;
            }
            
            if(timeOffset >= MAX_TIME_VALUE)
            {
                #if SCPE_DEV
                //Debug.LogWarning($"Time offset exceeded {MAX_TIME_VALUE}, resetting to 0 ({timeOffset % MAX_TIME_VALUE})");
                #endif
            }
            
            //Prevent floating point precision issues, reset to 0 when exceeding large values
            timeOffset %= MAX_TIME_VALUE;
        }
        
        public void Reset()
        {
            timeOffset = 0f;

#if UNITY_EDITOR
            lastEditorTime = UnityEditor.EditorApplication.timeSinceStartup;
#endif
        }

        public float value => timeOffset;
    }
}