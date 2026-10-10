using UnityEngine;

namespace Rowlan.Genesis
{
    public class ResourceUtils
    {
        /// <summary>
        /// Loads a resource as Texture2D. The resource path mustn't contain a file extension.
        /// <see cref="https://docs.unity3d.com/ScriptReference/Resources.Load.html"/>
        /// </summary>
        /// <param name="path">Path in the Resources folder</param>
        /// <param name="name">File name without extension</param>
        /// <returns></returns>
        public static Texture2D LoadIcon(string path, string name)
        {
            string resourcePath = $"{path}/{name}";

            Texture2D icon = Resources.Load<Texture2D>(resourcePath);

            if (icon == null)
                Debug.LogError("Resource not found: " + resourcePath);

            return icon;

        }
    }
}