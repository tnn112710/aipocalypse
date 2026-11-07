//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    /// <summary>
    /// Interface for all atmosphere module types.
    /// </summary>
    public interface IAtmosphereModule : ICozyModule
    {
        /// <summary>
        /// Holds a list of atmosphere parameters that will be passed out to the shader
        /// </summary>
        public AtmosphereParameters Parameters { get; }
        /// <summary>
        /// Grab the current sun direction
        /// </summary>
        public Vector3 SunDirection { get; }
        /// <summary>
        /// Grab the current moon direction
        /// </summary>
        public Vector3 MoonDirection { get; }
        /// <summary>
        /// Grab the current sun light
        /// </summary>
        public Light SunLight { get; }
        /// <summary>
        /// Grab the current moon light
        /// </summary>
        public Light MoonLight { get; }
        /// <summary>
        /// Grab the current strongest light (either the sun or the moon)
        /// </summary>
        public Light StrongestLight { get; }

        /// <summary>
        /// How much is the lighting overridden by lightning at this time
        /// </summary>
        public float LightningLightingOverride { get; set; }
    }
}