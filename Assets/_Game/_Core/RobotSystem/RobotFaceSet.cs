using System;
using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A robot model's face screen: which renderers show the eyes and mouth (a strip of Columns frames each, picked by the
    // texture's offset), and which frames make each expression. One asset per model, beside it in Content/Robots/.
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
        [SerializeField] public Dictionary<RobotExpression, Frame> Frames = new();
    }
}
