//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System.Collections.Generic;

namespace DistantLands.Cozy
{
    public struct SavedData
    {
        public string key;
        public List<string> properties;
        public SavedData(string key, List<string> properties)
        {
            this.key = key;
            this.properties = properties;
        }
    }
    public interface ISaveable : ICozyModule
    {
        /// <summary>
        /// Saves the data of this module into a custom SavedData struct
        /// </summary>
        public SavedData Save();
        /// <summary>
        /// Loads the current state of the module based on a SavedData struct
        /// </summary>
        public void Load(SavedData saveString);
    }
}