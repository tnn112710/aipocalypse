#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace SunfishCreations
{
    public class LabelVisualizer : MonoBehaviour
    {
        public bool show = true;

        public Color labelColor = Color.white;
        public int labelFontSize = 10;

        private void OnDrawGizmos()
        {
            if (!show) return;

            Vector3 labelPos = transform.position;

            Handles.Label(labelPos, name, new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = labelColor },
                fontSize = labelFontSize,
                fontStyle = FontStyle.Bold
            });
        }
    }
}
#endif