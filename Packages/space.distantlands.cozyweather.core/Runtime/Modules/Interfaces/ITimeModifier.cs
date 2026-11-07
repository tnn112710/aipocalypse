//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    /// <summary>
    /// Interface for modules that modify the time for modified time percentage (Transit, etc.)
    /// </summary>
    public interface ITimeModifier
    {
        public float ModifyDayPercentage(float input);
    }
}