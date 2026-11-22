using UnityEngine;
using UnityEngine.Localization;

namespace MB13.Aipocalypse.Core.RobotSystem
{
    // What a robot offers at home (shop, upgrades, storage…): its name, the E prompt's verb, its icon, and later its menu.
    // Assign another one on a robot to change its job. Assets in Data/Robots/Services/.
    [CreateAssetMenu(fileName = "RobotService", menuName = "Aipocalypse/Robots/Robot Service")]
    public sealed class RobotService : ScriptableObject
    {
        [Tooltip("The robot's name, the prompt's target")] public LocalizedString DisplayName;
        [Tooltip("The prompt's verb, e.g. \"Shop\"")] public LocalizedString Verb;
        [Tooltip("Shown over the robot's head when the player is near")] public Sprite Icon;
    }
}
