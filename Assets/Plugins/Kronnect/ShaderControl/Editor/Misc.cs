/*
Shader Control - (C) Copyright Kronnect Technologies SL
*/
using System;
using UnityEngine;

namespace ShaderControl {

    /// <summary>Static proxy that abstracts Unity API differences across versions.</summary>
    internal static class Misc {

        public static T[] FindObjectsByType<T>() where T : UnityEngine.Object {
#if UNITY_6000_4_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Exclude);
#else
            return UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
#endif
        }

        public static int GetInstanceID(UnityEngine.Object obj) {
#if UNITY_6000_4_OR_NEWER
            return obj.GetEntityId().GetHashCode();
#else
            return obj.GetInstanceID();
#endif
        }
    }
}
