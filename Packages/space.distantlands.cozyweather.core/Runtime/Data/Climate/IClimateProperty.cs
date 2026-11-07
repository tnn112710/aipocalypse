//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{

    /// <summary>
    /// Holds an absolute value of a climate property. For example, a temperature of 70 degrees.
    /// </summary>
    /// <typeparam name="Absolute">The type used for the values</typeparam>
    /// <typeparam name="Delta">The type used for deltas of this climate property. Normally the same as the enclosing type unless localization offsets the 0 index.</typeparam>
    public interface IClimateProperty<Absolute, Delta> where Delta : IClimatePropertyDelta<Absolute>
    {
        private static float MinValue => 0f;
        private static float MaxValue => 100f;

        public float Normalized { get; }

        public Absolute Lerp(Absolute to, float t);
        public void Add(Delta delta);
        public void Subtract(Delta delta);
    }

    /// <summary>
    /// Holds a delta value of a climate property. For example, a drop in temperature of 5 degrees.
    /// </summary>
    /// <typeparam name="T">The type used for the values</typeparam>
    public interface IClimatePropertyDelta<T>
    {
    }
}