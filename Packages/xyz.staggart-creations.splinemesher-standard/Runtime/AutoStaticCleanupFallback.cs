#if UNITY_6000_5_OR_NEWER
#define STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
#endif

using System;
using System.Runtime.CompilerServices;

#if !STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
[assembly: InternalsVisibleTo("sc.modeling.splines.editor")]
namespace sc.modeling.splines.runtime
{
    //Dummy attributes so they can be added error-free in older Unity versions
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
    internal class NoAutoStaticsCleanupAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Event, Inherited = false, AllowMultiple = false)]
    internal class AutoStaticsCleanupAttribute : Attribute { }
}

//Dummy attributes in this namespace, so attributes can be added error free in older Unity versions
namespace Unity.Scripting.LifecycleManagement
{
    class DummyClassSplineMesherStandard { }
}
#endif