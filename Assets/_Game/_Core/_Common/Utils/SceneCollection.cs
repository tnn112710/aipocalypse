#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Core
{
    // Editor: keeps a serialized list of scene objects current on save and when Play starts, so nobody keeps it by hand.
    public static class SceneCollection
    {
        // Lists the T in the owner's scene, sorted. Compared as a set first: re-collecting the same objects must not dirty
        // the scene on every Play.
        public static void Collect<T>(Component owner, List<T> list) where T : Component
        {
            var found = new List<T>();
            foreach (T item in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
                if (item.gameObject.scene == owner.gameObject.scene)
                    found.Add(item);
            if (found.Count == list.Count && new HashSet<T>(found).SetEquals(list))
                return;

            found.Sort((a, b) =>
            {
                int byName = string.CompareOrdinal(a.name, b.name);
                return byName != 0 ? byName : string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
            });
            list.Clear();
            list.AddRange(found);
            EditorUtility.SetDirty(owner);
        }

        // Call from an [InitializeOnLoadMethod]: runs `collect` on every TOwner of a scene being saved, and of every open
        // scene when Play starts.
        public static void Hook<TOwner>(Action<TOwner> collect) where TOwner : Component
        {
            EditorSceneManager.sceneSaving += (scene, _) => CollectIn(scene, collect);
            EditorApplication.playModeStateChanged += state =>
            {
                if (state != PlayModeStateChange.ExitingEditMode)
                    return;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    CollectIn(SceneManager.GetSceneAt(i), collect);
            };
        }

        static void CollectIn<TOwner>(Scene scene, Action<TOwner> collect) where TOwner : Component
        {
            foreach (TOwner owner in Object.FindObjectsByType<TOwner>(FindObjectsInactive.Include))
                if (owner.gameObject.scene == scene)
                    collect(owner);
        }
    }
}
#endif
