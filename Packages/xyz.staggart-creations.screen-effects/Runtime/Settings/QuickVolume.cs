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
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace sc.screeneffects.runtime
{
    /// <summary>
    /// Quickly create a volume with a set of settings
    /// </summary>
    public static class QuickVolume
    {
        private static Volume CreateGameObject(int layer, float priority)
        {
            var gameObject = new GameObject()
            {
                name = "Quick Volume",
                layer = Mathf.Max(0, layer),
                #if SCPE_DEV
                hideFlags = HideFlags.DontSave
                #else
                hideFlags = HideFlags.HideAndDontSave
                #endif
            };

            var volume = gameObject.AddComponent<Volume>();
            volume.priority = priority;
            volume.isGlobal = true;

            return volume;
        }
        
        /// <summary>
        /// Creates a new GameObject with a global volume, preconfigured with the assigned effects.
        /// </summary>
        /// <param name="overrideAll">Override all parameters</param>
        /// <param name="layer">Layer the volume gets put on, should correspond to the camera's volume layer (not culling layer!)</param>
        /// <param name="priority">Priority the settings have over other volumes.</param>
        /// <param name="profile">Profile to assign</param>
        public static Volume Create(int layer, float priority, VolumeProfile profile)
        {
            var volume = CreateGameObject(layer, priority);
            volume.profile = profile;
            
            return volume;
        }

        /// <summary>
        /// Creates a new GameObject with a global volume, preconfigured with the assigned effects.
        /// </summary>
        /// <param name="overrideAll">Override all parameters</param>
        /// <param name="layer">Layer the volume gets put on, should correspond to the camera's volume layer (not culling layer!)</param>
        /// <param name="priority">Priority the settings have over other volumes.</param>
        /// <param name="settings">Array of volume component settings, for any kind of effect</param>
        /// <returns></returns>
        public static Volume Create(int layer, float priority, bool overrideAll, params VolumeComponent[] settings)
        {
            var volume = CreateGameObject(layer, priority);
            
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            
            //Debug.Log($"Created Quick Volume with {settings.Length} effects");
            
            foreach (VolumeComponent s in settings)
            {
                Assert.IsNotNull(s, "Trying to create a volume with null effects");
                
                s.SetAllOverridesTo(overrideAll);
                profile.components.Add(s);
                
                //Debug.Log($"Added {s.GetType().Name} to Quick Volume");
            }
            
            volume.profile = profile;

            return volume;
        }

        /// <summary>
        /// Destroys the GameObject of the given volume.
        /// </summary>
        /// <param name="volume"></param>
        public static void Destroy(Volume volume)
        {
            //if(volume.profile) CoreUtils.Destroy(volume.profile);
            CoreUtils.Destroy(volume.gameObject);
        }
    }
}