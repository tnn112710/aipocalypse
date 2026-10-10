#if __MICROVERSE_ROADS__

using JBooth.MicroVerseCore;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public interface IRoadsModule
    {
        public void OnInspectorGUI();

        /// <summary>
        /// Whether this gameobject is supported or not
        /// </summary>
        /// <param name="gameObject"></param>
        /// <returns></returns>
        public bool IsSupported(GameObject gameObject);
    }
}
#endif