using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TextStudio.InputSystem
{
    // One owner per action, independent of the number of text targets awaiting it.
#if UNITY_6000_7_OR_NEWER
    // Explicit lifecycle reset releases owned resources and restores leases before clearing state.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioInputActionLease : IDisposable
    {
        sealed class Entry
        {
            public InputAction Action;
            public int Users;
            public bool EnabledHere;
            public bool KeepEnabled;
            public bool ExternallyChanged;
        }

        static readonly Dictionary<InputAction, Entry> Entries = new Dictionary<InputAction, Entry>();
        static InputAction _changing;
        readonly Entry _entry;
        bool _released;

        TextStudioInputActionLease(Entry entry) { _entry = entry; }

        public static IDisposable Acquire(InputAction action, bool autoEnable, bool restore)
        {
            if (action == null) return null;
            if (!Entries.TryGetValue(action, out Entry entry))
            {
                if (Entries.Count == 0)
                {
                    UnityEngine.InputSystem.InputSystem.onActionChange += OnActionChange;
                    Application.quitting += Reset;
                }
                entry = new Entry { Action = action };
                Entries.Add(action, entry);
            }
            entry.Users++;
            entry.KeepEnabled |= !restore;
            if (autoEnable && !action.enabled)
            {
                ChangeEnabled(action, true);
                entry.EnabledHere = true;
            }
            return new TextStudioInputActionLease(entry);
        }

        public void Dispose()
        {
            if (_released) return;
            _released = true;
            if (!Entries.TryGetValue(_entry.Action, out Entry current) || !ReferenceEquals(current, _entry)) return;
            if (--current.Users != 0) return;
            Entries.Remove(current.Action);
            Restore(current);
            if (Entries.Count == 0) Unsubscribe();
        }

        static void Restore(Entry entry)
        {
            if (entry.EnabledHere && !entry.KeepEnabled && !entry.ExternallyChanged && entry.Action.enabled)
                ChangeEnabled(entry.Action, false);
        }

        static void ChangeEnabled(InputAction action, bool enabled)
        {
            InputAction previous = _changing;
            _changing = action;
            try { if (enabled) action.Enable(); else action.Disable(); }
            finally { _changing = previous; }
        }

        static void OnActionChange(object changed, InputActionChange change)
        {
            if (change != InputActionChange.ActionEnabled && change != InputActionChange.ActionDisabled &&
                change != InputActionChange.ActionMapEnabled && change != InputActionChange.ActionMapDisabled) return;
            if (changed is InputAction action)
            {
                if (!ReferenceEquals(action, _changing) && Entries.TryGetValue(action, out Entry entry)) entry.ExternallyChanged = true;
            }
            else if (changed is InputActionMap map)
            {
                foreach (Entry entry in Entries.Values)
                    if (ReferenceEquals(entry.Action.actionMap, map) && !ReferenceEquals(entry.Action, _changing)) entry.ExternallyChanged = true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void Reset()
        {
            // Release previous-session ownership even when domain reload is disabled.
            Unsubscribe();
            foreach (Entry entry in Entries.Values) Restore(entry);
            Entries.Clear();
            _changing = null;
        }

        static void Unsubscribe()
        {
            UnityEngine.InputSystem.InputSystem.onActionChange -= OnActionChange;
            Application.quitting -= Reset;
        }
    }
}
