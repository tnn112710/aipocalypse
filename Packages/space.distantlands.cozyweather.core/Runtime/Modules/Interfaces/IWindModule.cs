//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEngine;

namespace DistantLands.Cozy
{
    public interface IWindModule : ICozyModule
    {
        public WindParameters WindParameters { get; set; }
        /// <summary>
        /// Determines the direction (in degrees) of the wind heading. A value of 0 is a wind moving towards North and values move clockwise (90 -> East, 180 -> South, 270 -> West, etc.)
        /// </summary>
        public float Heading { get; set; }
        /// <summary>
        /// Adds an offset (+/-) to the Heading to allow for subtle wind direction fluttering.
        /// </summary>
        public float HeadingVariance { get; set; }
        /// <summary>
        /// The base strength for the wind at this heading. Determines how much force is exerted by the wind.
        /// </summary>
        public float WindStrength { get; set; }
        /// <summary>
        /// The base speed for the wind at this heading. Measured in m/sec
        /// </summary>
        public float WindSpeed { get; set; }
        /// <summary>
        /// 0 = no variance, 1 = wind speed can be anywhere from the base wind speed to 0
        /// </summary>
        public float WindSpeedVariance { get; set; }
        /// <summary>
        /// How quickly will the wind change speeds from the base wind speed to the base wind speed multiplied by the wind speed variance
        /// </summary>
        public float WindSpeedChangeRate { get; set; }
        /// <summary>
        /// How quickly will the wind change headings from the base wind heading to the base wind heading plus the wind heading variance
        /// </summary>
        public float HeadingChangeRate { get; set; }
        /// <summary>
        /// Sets the gusting power of the wind
        /// </summary>
        public float GustingPower { get; set; }
        /// <summary>
        /// How frequently does the wind gust
        /// </summary>
        public float GustingFrequency { get; set; }
        /// <summary>
        /// Get the direction of the wind as a vector. Points in the direction of the current wind heading.
        /// </summary>
        public Vector3 WindDirection { get; set; }
    }
}