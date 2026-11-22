using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // A robot's job, one asset per kind: what it's called, its E prompt's verb and its icon. A RobotService opens a menu
    // (shop, storage, upgrades); a RobotJob works on its own (collecting, guarding). Assign another one to change its job.
    public abstract class RobotRole : ScriptableObject
    {
        [Tooltip("The robot's name, the prompt's target")] public LocalizedString DisplayName;
        [Tooltip("The prompt's verb, e.g. \"Shop\"")] public LocalizedString Verb;
        [Tooltip("Shown over the robot's head when the player is near")] public Sprite Icon;
    }
}
