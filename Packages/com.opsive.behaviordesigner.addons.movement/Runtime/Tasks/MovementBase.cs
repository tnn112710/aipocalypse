/// ---------------------------------------------
/// Movement Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.MovementPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Utility;
#if OPSIVE_SHARED_V3
    using Opsive.Shared.Runtime.Utility;
    using Opsive.Shared.Runtime.Serialization;
    using Serialization = Opsive.Shared.Runtime.Serialization.Serialization;
    using MemberVisibility = Opsive.Shared.Runtime.Serialization.MemberVisibility;
#else
    using Opsive.Shared.Utility;
#endif

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