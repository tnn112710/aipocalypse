using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace FayLabs.Kroma.Editor
{
    // [CreateAssetMenu(fileName = "Kroma Library", menuName = "Kroma/Kroma Library", order = 201)]
    public class KromaProfileLibrary : ScriptableObject
    {
        [Header("Kroma Profile Library")]
        [Tooltip("List of all registered Kroma Profiles available in the Browser.")]
        public List<KromaProfile> profiles = new List<KromaProfile>();
        
        /// <summary>
        /// Finds a profile by name (Current logic uses name matching)
        /// </summary>
        public KromaProfile GetProfileByName(string profileName)
        {
            if (profiles == null) return null;
            return profiles.Find(p => p != null && p.volumeProfile != null && p.volumeProfile.name == profileName);
        }
        
        /// <summary>
        /// Finds a profile by the VolumeProfile reference
        /// </summary>
        public KromaProfile GetProfileByAsset(VolumeProfile volProfile)
        {
            if (profiles == null) return null;
            return profiles.Find(p => p != null && p.volumeProfile == volProfile);
        }
    }
}
