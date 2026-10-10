using UnityEngine;

namespace Rowlan.Genesis.Integrations.KWS2
{
    public class GUIUtils
    {

        /// <summary>
        /// Wrap GUI.enabled for Unity 2021.
        /// For Unity 2022+ use built-in <code>using (new EditorGUI.DisabledScope(true))</code>
        /// </summary>
        public struct DisabledScope : System.IDisposable
        {
            private readonly bool previousState;

            public DisabledScope(bool disabled)
            {
                previousState = GUI.enabled;
                GUI.enabled = !disabled;
            }

            public void Dispose() => GUI.enabled = previousState;
        }

    }
}