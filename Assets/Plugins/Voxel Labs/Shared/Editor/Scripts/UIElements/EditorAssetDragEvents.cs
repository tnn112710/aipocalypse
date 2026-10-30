using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VoxelLabs.Shared.UIElements
{
    // Ship as source: UI Toolkit event types resolve against the installed Unity Editor.
    public static class EditorAssetDragEvents
    {
        private static bool initialized;
        private static bool dragging;
        public static bool IsDragging => dragging;
        private static ConditionalWeakTable<VisualElement, object> attached = new ConditionalWeakTable<VisualElement, object>();
        private static readonly List<WeakReference<VisualElement>> roots = new List<WeakReference<VisualElement>>();
        private static double nextScan;

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            nextScan = 0;
            ScanWindows();
            EditorApplication.update -= ScanWindows;
            EditorApplication.update += ScanWindows;
        }

        private static void ScanWindows()
        {
            // A drag released outside Unity must not leave its repaint limit active.
            if (dragging && !UnityEditorInternal.InternalEditorUtility.isApplicationActive) dragging = false;
            if (EditorApplication.timeSinceStartup < nextScan) return;
            nextScan = EditorApplication.timeSinceStartup + 0.5d;
            // Discover windows only twice a second. Each existing root is hooked once,
            // so the actual drag is observed immediately without polling its target controls.
            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                Attach(window.rootVisualElement);
            for (int i = roots.Count - 1; i >= 0; i--)
                if (!roots[i].TryGetTarget(out _)) roots.RemoveAt(i);
        }

        private static void Attach(VisualElement root)
        {
            if (root == null || attached.TryGetValue(root, out _)) return;
            attached.Add(root, new object());
            roots.Add(new WeakReference<VisualElement>(root));
            // Observe on the capture phase, before native object fields consume the event.
            root.RegisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
            root.RegisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
            root.RegisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        // DragExited is not an end signal: it also occurs while crossing Editor windows.
        private static void OnDragUpdated(DragUpdatedEvent evt) => dragging = true;
        private static void OnDragPerform(DragPerformEvent evt) => dragging = false;
        private static void OnMouseUp(MouseUpEvent evt) => dragging = false;
        private static void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape) dragging = false;
        }

        public static void Shutdown()
        {
            EditorApplication.update -= ScanWindows;
            foreach (var weak in roots)
            {
                if (!weak.TryGetTarget(out var root)) continue;
                root.UnregisterCallback<DragUpdatedEvent>(OnDragUpdated, TrickleDown.TrickleDown);
                root.UnregisterCallback<DragPerformEvent>(OnDragPerform, TrickleDown.TrickleDown);
                root.UnregisterCallback<MouseUpEvent>(OnMouseUp, TrickleDown.TrickleDown);
                root.UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            }
            roots.Clear();
            attached = new ConditionalWeakTable<VisualElement, object>();
            dragging = false;
            initialized = false;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
        }
    }
}
