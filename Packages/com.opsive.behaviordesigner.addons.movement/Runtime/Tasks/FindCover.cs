/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
#if OPSIVE_SHARED_V3
    using OpsiveSharedUtility = Opsive.Shared.Runtime.Utility;
#else
    using OpsiveSharedUtility = Opsive.Shared.Utility;
#endif

    /// <summary>
    /// Finds reachable cover and publishes its position and stance without acquiring or changing movement.
    /// </summary>
    [OpsiveSharedUtility.Category("Movement Pack")]
    [OpsiveSharedUtility.Description("Finds cover without changing navigation. Outputs the position, normal, geometry, and required stance.")]
    public class FindCover : MoveToCover
    {
        /// <summary>
        /// Queries navigation without changing locomotion or stopping another action.
        /// </summary>
        protected override bool ControlsMovement => false;
    }
}