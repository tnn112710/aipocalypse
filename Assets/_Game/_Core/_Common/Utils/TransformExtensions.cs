using UnityEngine;

namespace MB13.Aipocalypse.Core
{
    public static class TransformExtensions
    {
        public static void SetLayerRecursively(this Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                root.GetChild(i).SetLayerRecursively(layer);
        }
    }
}
