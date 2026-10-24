using UnityEngine;
using System;

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Unity", "UAL0010", Scope = "namespaceanddescendants", Target = "~N:DynamicFogAndMist2")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Unity", "UAL0013", Scope = "namespaceanddescendants", Target = "~N:DynamicFogAndMist2")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Unity", "UAL0010", Scope = "namespaceanddescendants", Target = "~N:UnityStandardAssets.CrossPlatformInput")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Unity", "UAL0013", Scope = "namespaceanddescendants", Target = "~N:UnityStandardAssets.CrossPlatformInput")]

namespace DynamicFogAndMist2 {
    public class Misc {
        public static T FindObjectOfType<T>(bool includeInactive = false) where T : UnityEngine.Object {
#if UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindAnyObjectByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
        return UnityEngine.Object.FindObjectOfType<T>(includeInactive);
#endif
        }

        public static UnityEngine.Object[] FindObjectsOfType(Type type, bool includeInactive = false) {
#if UNITY_6000_4_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(type, includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(type, includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType(type, includeInactive);
#endif
        }


        public static T[] FindObjectsOfType<T>(bool includeInactive = false) where T : UnityEngine.Object {
#if UNITY_6000_4_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_2023_1_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#endif
        }
    }

}