using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Core.UI
{
    // Shared by the widgets and their style assets.
    public static class StyleUtils
    {
        // A sprite with 9-slice borders stretches by its borders, any other is drawn whole.
        public static Image.Type ImageTypeFor(Sprite sprite) =>
            sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;

#if UNITY_EDITOR
        // Edit-time preview, from a style's OnValidate: every open view using it re-applies it. Views also apply their style in
        // Awake, so Play mode and builds never depend on this.
        public static void RefreshViews<TView>(ScriptableObject style, Func<TView, ScriptableObject> styleOf, Action<TView> apply)
            where TView : MonoBehaviour
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (style == null)
                    return;
                foreach (TView view in Object.FindObjectsByType<TView>(FindObjectsInactive.Include))
                    if (styleOf(view) == style)
                        apply(view);
            };
        }
#endif
    }
}
