//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    /// <summary>
    /// Determine how and when ambience should change.
    /// </summary>
    public enum AmbienceChangeMode
    {
        ChangeWhenAmbienceFinishes,
        ChangeViaScripting,
    }


    /// <summary>
    /// Interface for all atmosphere module types.
    /// </summary>
    public interface IAmbienceModule : IStateMachineModule<AmbienceProfile>
    {
        /// <summary>
        /// Holds a list of all profiles that can play as ambiences
        /// </summary>
        public AmbienceProfile[] PossibleProfiles { get; set; }
        /// <summary>
        /// When should ambience change?
        /// </summary>
        public AmbienceChangeMode AmbienceChangeMode { get; }
    }
}