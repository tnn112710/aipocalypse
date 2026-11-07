//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    /// <summary>
    /// Allows a module to change data when time is skipped
    /// </summary>
    public interface ISkippable : ICozyModule
    {
        /// <summary>
        /// Skip time by a certain factor
        /// </summary>
        public void SkipTime(MeridiemTimeDelta timeToSkip);
    }
}