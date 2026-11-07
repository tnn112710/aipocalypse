//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    /// <summary>
    /// Interface for modules that control the amount of ground-cover precipitation for the COZY system.
    /// </summary>
    public interface IPrecipitationModule : ICozyModule
    {
        /// <summary>
        /// Controls how much snow is currently on the ground
        /// </summary>
        public Accumulation AccumulatedSnow { get; set; }
        /// <summary>
        /// Controls how much water is currently on the ground
        /// </summary>
        public Accumulation AccumulatedRain { get; set; }
        /// <summary>
        /// Controls how quickly snow is accumulating on the ground
        /// </summary>
        public float SnowAccumulationSpeed { get; set; }
        /// <summary>
        /// Controls how quickly water is accumulating on the ground
        /// </summary>
        public float RainAccumulationSpeed { get; set; }
    
    }
}