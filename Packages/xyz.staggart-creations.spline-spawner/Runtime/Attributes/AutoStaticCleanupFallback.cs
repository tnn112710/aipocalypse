// Spline Spawner © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

#if UNITY_6000_5_OR_NEWER
#define STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
#endif

using System;
using System.Runtime.CompilerServices;

#if !STATIC_CLEANUP_ATTRIBUTE_AVAILABLE
[assembly: InternalsVisibleTo("sc.splines.spawning.editor")]
namespace sc.splines.spawner.runtime
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
    class DummyClassSplineSpawner { }
}
#endif