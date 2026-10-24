using UnityEngine;

namespace SunfishCreations
{
    public static class FishingGlobalSettings
    {
        private const string KEY_SPAWN_FISH_ON_WATER = "Fishing_SpawnFishOnWater";
        private const bool DEFAULT_SPAWN_FISH_ON_WATER = true;

        public static bool SpawnFishOnWater
        {
            get => PlayerPrefs.GetInt(KEY_SPAWN_FISH_ON_WATER, DEFAULT_SPAWN_FISH_ON_WATER ? 1 : 0) == 1;
            set
            {
                int intValue = value ? 1 : 0;
                if (PlayerPrefs.GetInt(KEY_SPAWN_FISH_ON_WATER, -1) != intValue)
                {
                    PlayerPrefs.SetInt(KEY_SPAWN_FISH_ON_WATER, intValue);
                    PlayerPrefs.Save();
                }
            }
        }

        public static void ResetSpawnFishOnWater()
        {
            PlayerPrefs.DeleteKey(KEY_SPAWN_FISH_ON_WATER);
            PlayerPrefs.Save();
        }
    }
}
