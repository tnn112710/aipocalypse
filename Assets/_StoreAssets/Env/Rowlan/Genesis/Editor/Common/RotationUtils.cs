using UnityEngine;

namespace Rowlan.Genesis
{
    public static class RotationUtils
    {
        private static readonly Vector3 Vector3_XZ = new Vector3(1f, 0f, 1f);
        private static readonly Vector3 Vector3_XY = new Vector3(1f, 1f, 0f);
        private static readonly Vector3 Vector3_YZ = new Vector3(0f, 1f, 1f);

        public enum RotationAxis
        {
            XYZ,
            X,
            Y,
            Z,
            XZ,
            XY,
            YZ,
        }

        /// <summary>
        /// Get the rotation for a given enum
        /// </summary>
        /// <param name="axis"></param>
        /// <returns></returns>
        public static Vector3 GetVector( this RotationAxis axis)
        {
            return axis switch
            {
                RotationAxis.XYZ => Vector3.one,
                RotationAxis.X => Vector3.right,
                RotationAxis.Y => Vector3.up,
                RotationAxis.Z => Vector3.forward,
                RotationAxis.XZ => Vector3_XZ,
                RotationAxis.XY => Vector3_XY,
                RotationAxis.YZ => Vector3_YZ,
                _ => Vector3.one,
            };
        }
    }
}