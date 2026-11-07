//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using UnityEngine.SceneManagement;

namespace DistantLands.Cozy
{
    public interface ICozyModule
    {
        /// <summary>
        /// Set whether the module is active and enabled
        /// </summary>
        public bool Active { get; set; }
        /// <summary>
        /// Resets the module to all default settings
        /// </summary>
        public void ResetModule();
        /// <summary>
        /// Runs when the module is added and when the game starts
        /// </summary>
        public void InitializeModule();
        /// <summary>
        /// Runs when the module is removed and when the game ends
        /// </summary>
        public void DeinitializeModule();
        /// <summary>
        /// Checks if this module has dependencies that prevent it from being removed
        /// </summary>
        /// <param name="warning">Returns a warning string to let the user know what went wrong</param>
        /// <returns>Returns true if module can be removed. Otherwise, return false</returns>
        public bool CheckIfModuleCanBeRemoved(out string warning);
        /// <summary>
        /// Checks if this module has any conflicts that prevent it from being added
        /// </summary>
        /// <param name="warning">Returns a warning string to let the user know what went wrong</param>
        /// <returns>Returns true if module can be added. Otherwise, return false</returns>
        public bool CheckIfModuleCanBeAdded(out string warning);
        /// <summary>
        /// List up to 5 required modules on the system in order for this module to be added. Use the DependsOn&lt;Types&gt; method to get the list of types.
        /// </summary>
        public Type[] Requirements { get; }
        /// <summary>
        /// Runs when the module is added to the weather manager but before the initialize module function.
        /// </summary>
        public void SetupModule();
        /// <summary>
        /// Runs when a scene is loaded
        /// </summary>
        public void OnSceneLoaded(LoadSceneMode mode);
        /// <summary>
        /// Runs when a scene is unloaded
        /// </summary>
        public void OnSceneUnloaded();
    }
}