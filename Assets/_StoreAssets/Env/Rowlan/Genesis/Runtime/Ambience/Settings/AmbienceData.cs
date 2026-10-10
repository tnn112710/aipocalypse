using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Rowlan.Genesis.Ambience
{
    public class AmbienceData : ScriptableObject
    {
        public enum Module
        {
            PostProcessing,
            TimeOfDay
        }

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        public Module module = Module.PostProcessing;
    }
}