using UnityEngine;
using System;

namespace Rowlan.Genesis.Distribution
{
    public class DistributionData : ScriptableObject
    {

        public enum FilterMode
        {
            Texture,
        }

        /// <summary>
        /// The stamp that's being processed. Don't serialize it.
        /// </summary>
        [System.NonSerialized]
        public GameObject gameObject;

        /// <summary>
        /// Automatically assigns the selected gameobject
        /// </summary>
        [SerializeField]
        public bool autoAssignment = true;

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        #region Filter

        public FilterMode filterMode = FilterMode.Texture;

        [Serializable]
        public class FilterSettings
        {
            [Tooltip("Apply the changes to the children as well")]
            public bool includeChildren;

            public void Reset()
            {
                includeChildren = true;
            }
        }
    
        public FilterSettings filterSettings = new FilterSettings();

        #endregion Filter

        public void Reset()
        {
            autoAssignment = true;

            filterSettings = new FilterSettings();
            filterSettings.Reset();
        }
    }
}