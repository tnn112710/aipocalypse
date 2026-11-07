//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public interface IMaterialProvider
    {
        /// <summary>
        /// Updates shader keywords based on which features are enabled or disabled
        /// </summary>
        public void UpdateKeywords();
    }
}