#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace MB13.Aipocalypse.Core
{
    // Sidekick's RandomStateBehaviourAutoRefresh loads every AnimatorController and saves assets after each domain reload
    // and Play press (~12 s with 1000+ controllers). No controller here uses RandomStateBehaviour, so unhook it from outside
    // the vendor folder. Its inspector "Refresh Destinations" button still works.
    static class SidekickAutoRefreshBlocker
    {
        [InitializeOnLoadMethod]
        static void Unhook()
        {
            var type = Type.GetType("RandomStateBehaviourAutoRefresh, SidekickCharacters");
            if (type == null)
                return;

            // Make sure its static constructor has subscribed before removing (no-op if it already ran).
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo refresh = type.GetMethod("RefreshAllBehaviours", flags);
            MethodInfo playMode = type.GetMethod("OnPlayModeStateChanged", flags);
            if (refresh == null || playMode == null)
            {
                Debug.LogWarning("SidekickAutoRefreshBlocker: Sidekick changed RandomStateBehaviourAutoRefresh; update or remove this blocker.");
                return;
            }

            EditorApplication.delayCall -= (EditorApplication.CallbackFunction)Delegate.CreateDelegate(typeof(EditorApplication.CallbackFunction), refresh);
            EditorApplication.playModeStateChanged -= (Action<PlayModeStateChange>)Delegate.CreateDelegate(typeof(Action<PlayModeStateChange>), playMode);
        }
    }
}
#endif
