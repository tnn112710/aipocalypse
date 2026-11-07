//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public interface IStateMachineProfile
    {
        /// <summary>
        /// Set the minimum time that this profile can play
        /// </summary>
        public MeridiemTimeDelta MinTime { get; }
        /// <summary>
        /// Set the maximum time that this profile can play
        /// </summary>
        public MeridiemTimeDelta MaxTime { get; }
        /// <summary>
        /// Runs when the profile plays
        /// </summary>
        /// <param name="weight">0-1 float that is determined by the current weight of this profile</param>
        public void Play(float weight)
        {

        }
    }
}