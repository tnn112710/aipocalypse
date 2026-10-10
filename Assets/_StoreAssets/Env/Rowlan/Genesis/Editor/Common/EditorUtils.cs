using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class EditorUtils
    {
        /// <summary>
        /// Repaint all custom editor windows
        /// </summary>
        public static void RepaintAllEditorWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var window in windows)
            {
                window.Repaint();
            }

        }
    }
}