using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public interface IReGenesisModule
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