using Aipocalypse.Core.EquipmentSystem;
using UnityEngine;

namespace Aipocalypse.Core.PlayerSystem
{
    // A tool held to repair things (the hammer): with it in hand, the reload key repairs what the player looks at
    // (PlayerRepair) and the fences offer Repair All. Its held prefab is a RepairTool.
    [CreateAssetMenu(fileName = "RepairTool", menuName = "Aipocalypse/Items/Repair Tool")]
    public sealed class RepairToolDefinition : EquippableDefinition
    {
        public override ItemCategory Category => ItemCategory.Tool;
    }
}
