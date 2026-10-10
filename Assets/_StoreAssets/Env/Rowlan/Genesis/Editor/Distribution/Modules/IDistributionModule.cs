using UnityEngine;

namespace Rowlan.Genesis.Distribution
{
    public interface IDistributionModule
    {
        public void OnInspectorGUI();

        /// <summary>
        /// Whether this gameobject is supported by ReGenesis or not
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public bool IsSupported(GameObject gameObject);
    }
}