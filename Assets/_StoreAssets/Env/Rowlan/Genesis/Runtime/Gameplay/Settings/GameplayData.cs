using UnityEngine;
#if UNITY_EDITOR
#endif

namespace Rowlan.Genesis.Gameplay
{
    public class GameplayData : ScriptableObject
    {
        public enum Module
        {
            Player,
            GameManager
        }

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        public Module module = Module.Player;
    }
}