#if __MICROVERSE_ROADS__
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    /// <summary>
    /// Common methods to find demo content
    /// </summary>
    public class AutoSetupUtils
    {
        /// <summary>
        /// Find the intersection prefab with the given name
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static GameObject FindIntersection(string name)
        {
            GameObject intersection = null;

            // Intersection 2 Lanes T.prefab"
            string[] folders = new string[] { "Packages/com.jbooth.microverse.roads/Content/Roads/Collections/Prefabs" };
            string[] prefabs = AssetDatabase.FindAssets($"\"{name}\" t:prefab", folders);
            foreach (string guid in prefabs)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                if (path.EndsWith(name + ".prefab"))
                {
                    intersection = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    break;
                }
            }

            return intersection;
        }
    }
}
#endif