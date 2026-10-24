using MalbersAnimations;
using MalbersAnimations.Controller;
using MalbersAnimations.Items;
using UnityEngine;

namespace SunfishCreations
{
    /// <summary>
    /// Routes a Super Item action to the fishing rod adapter.
    /// </summary>
    [System.Serializable, AddTypeMenu("Action/Fishing")]
    public class FishingActionProcessor : ItemProcessor
    {
        [Tooltip("Mode ID containing the fishing rod Cast ability.")]
        public ModeID castMode;

        [Tooltip("Ability index for the fishing rod Cast ability.")]
        public int castAbilityIndex = 101;

        public override string DynamicName =>
            castMode != null
                ? $"Fishing Rod Cast [{castMode.name}:{castMode.ID}] - Ability [{castAbilityIndex}]"
                : $"Fishing Rod Cast [<Missing Mode>] - Ability [{castAbilityIndex}]";

        public override ExecuteProcessor InitialExecute => ExecuteProcessor.OnActionStart;

        public override void Execute(SuperItem item, ItemActionSet set, ItemAction action)
        {
            if (item == null)
                return;

            FishingRod fishingRod = item.GetComponent<FishingRod>();
            if (fishingRod == null)
            {
                Debug.LogWarning($"{item.name} has a Fishing Super Item processor but no FishingRod adapter.", item);
                return;
            }

            fishingRod.ExecuteItemAction(item, castMode, castAbilityIndex);
        }

        public override string Validation(SuperItem item, ItemActionSet set, ItemAction action)
        {
            if (castMode == null)
                return "Fishing cast Mode ID reference is missing.";

            if (item == null || item.GetComponent<FishingRod>() == null)
                return "FishingRod adapter is missing from this Super Item.";

            return string.Empty;
        }
    }
}
