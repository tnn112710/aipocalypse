//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public delegate void ChangeSeasonEvent(SeasonName season);
    public interface ISeasonModule : ICozyModule
    {
        /// <summary>
        /// Get the index of the current season (0 = Spring, 1 = Summer, 2 = Fall, 3 = Winter).
        /// </summary>
        public SeasonName CurrentSeason { get; }
        /// <summary>
        /// Event that runs when the current season changes
        /// </summary>
        public event ChangeSeasonEvent OnChangeSeason;

    }
}