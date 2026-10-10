using System;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    /// <summary>
    /// Editor mode box visualizing the thumbnail orthographic camera
    /// </summary>
    [Serializable]
    public class ThumbnailGuide : MonoBehaviour
    {
        [Tooltip("Total number of potential label lines in the thumbnail")]
        [Range(1,8)]
        public int totalLines = 6;

        [Tooltip("Number of visible label lines in the thumbnail")]
        [Range(1, 8)]
        public int visibleLines = 2;

        [Tooltip("Block background color, the area that won't be visible in the numbnail")]
        public Color backgroundColor = Color.black;

        [Tooltip("Color to visualize block bounds")]
        public Color lineColor = Color.black;

        [Tooltip("If enabled, then the line label blocks will be dimmed from top to bottom")]
        public bool dim = true;

        [Tooltip("The factor that's applied to the alpha value of the dim color")]
        [Range(0f, 1f)]
        public float dimFactor = 0.6f;
    }
}