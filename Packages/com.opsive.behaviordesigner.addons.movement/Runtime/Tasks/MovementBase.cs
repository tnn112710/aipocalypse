/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.Shared.Utility;

    /// <summary>
    /// Compatibility alias for the renamed MovementAgent task base.
    /// </summary>
    [System.Obsolete("MovementBase has been renamed to MovementAgent. Derive from MovementAgent instead.")]
    [HideNameInTaskControl]
    [Category("Movement Pack")]
    public abstract class MovementBase : MovementAgent
    {
    }
}