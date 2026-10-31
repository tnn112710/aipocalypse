namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    /// <summary>Finds reachable cover and publishes its position and stance without acquiring or changing movement.</summary>
    [Opsive.Shared.Utility.Category("Movement Pack")]
    [Opsive.Shared.Utility.Description("Finds cover without changing navigation. Outputs the position, normal, geometry, and required stance.")]
    public class FindCover : MoveToCover
    {
        /// <summary>Queries navigation without changing locomotion or stopping another action.</summary>
        protected override bool ControlsMovement => false;
    }
}