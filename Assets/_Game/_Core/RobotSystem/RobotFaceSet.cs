using System;
using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A robot model's face screen: which renderers show the eyes and mouth (a strip of Columns frames each, picked by the
    // texture's offset), which frames make each expression, and the speech line shown while it talks. One asset per model, beside it in Content/Robots/.
    [CreateAssetMenu(fileName = "RobotFaceSet", menuName = "Aipocalypse/Robots/Robot Face Set")]
    public sealed class RobotFaceSet : ScriptableObject
    {
        [Serializable]
        public struct Frame
        {
            [Min(0)] public int Eyes;
            [Min(0)] public int Mouth;
        }

        [Tooltip("Name of the renderer showing the eyes")] public string EyesRenderer;
        [Tooltip("Name of the renderer showing the mouth")] public string MouthRenderer;
        [Min(1), Tooltip("Frames side by side in each strip")] public int Columns = 10;
        [Tooltip("Name of the renderer showing the scrolling speech line (swapped in for the mouth while it talks); empty = none")] public string SpeechRenderer;
        [Tooltip("Speech texture scroll (texture widths per second)")] public float SpeechScroll = 0.2f;
        [Min(1), Tooltip("Lines stacked in the speech texture")] public int SpeechTracks = 3;
        [Min(0), Tooltip("Which line it shows")] public int SpeechTrack = 1;
        [SerializeField] public Dictionary<RobotExpression, Frame> Frames = new();
    }
}
