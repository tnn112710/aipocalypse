#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MB13.Aipocalypse.Core.HandSystem
{
    // Edit-mode ghost hands from the project's first HandSet, kept on their targets every editor frame so grips can be
    // moved with the hands visible. Never saved: the ghosts are DontSave and live outside the edited hierarchy.
    internal static class HandPreview
    {
        sealed class Entry
        {
            public Object Owner;
            public HandSide Side;
            public Func<HandGrip> Source;
            public HandRig Rig;
        }

        static readonly List<Entry> Entries = new();
        static HandSet _set;

        static HandPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += HideAll;
            EditorApplication.playModeStateChanged += _ => HideAll();
        }

        public static bool IsShowing(Object owner) => Entries.Exists(e => e.Owner == owner);

        // One ghost per owner and side; the source grip is read every editor frame (null = hidden).
        public static void Show(Object owner, HandSide side, Func<HandGrip> source)
        {
            Entry entry = Entries.Find(e => e.Owner == owner && e.Side == side);
            if (entry == null)
            {
                entry = new Entry { Owner = owner, Side = side };
                Entries.Add(entry);
                if (Entries.Count == 1)
                    EditorApplication.update += Update;
            }
            entry.Source = source;
            Refresh(entry);
            SceneView.RepaintAll();
        }

        public static void Hide(Object owner)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Owner != owner)
                    continue;
                Destroy(Entries[i]);
                Entries.RemoveAt(i);
            }
            if (Entries.Count == 0)
                EditorApplication.update -= Update;
            SceneView.RepaintAll();
        }

        static void HideAll()
        {
            foreach (Entry entry in Entries)
                Destroy(entry);
            Entries.Clear();
            EditorApplication.update -= Update;
        }

        static void Update()
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry entry = Entries[i];
                if (entry.Owner == null)
                {
                    Destroy(entry);
                    Entries.RemoveAt(i);
                    continue;
                }
                Refresh(entry);
            }
            if (Entries.Count == 0)
                EditorApplication.update -= Update;
        }

        static void Refresh(Entry entry)
        {
            HandGrip grip = entry.Source();
            if (grip == null)
            {
                if (entry.Rig != null)
                    entry.Rig.SetVisible(false);
                return;
            }

            if (entry.Rig == null)
            {
                entry.Rig = Spawn(entry.Side, grip.gameObject.scene);
                if (entry.Rig == null)
                    return;
            }
            entry.Rig.SetVisible(true);
            Pose(entry.Rig, grip);
        }

        // Places a rig on a grip as the player would see it, with the elbow from the HandSet.
        public static void Pose(HandRig rig, HandGrip grip)
        {
            ItemHands item = grip.GetComponentInParent<ItemHands>(true);
            HandRig prefab = Set != null ? Set.Get(rig.Side) : null;
            rig.transform.localScale = (prefab != null ? prefab.transform.localScale : Vector3.one) * (item != null ? item.HandScale : 1f);
            Vector3 elbow = Elbow(grip.transform, rig.Side);
            grip.Resolve(rig, elbow, out Vector3 position, out Quaternion rotation);
            rig.Place(position, rotation, Shoulder(grip.transform, rig.Side), elbow);
            rig.Apply(grip.Shape);
        }

        public static Vector3 Shoulder(Transform grip, HandSide side)
        {
            HandSet set = Set;
            if (set == null)
                return Elbow(grip, side);
            ItemHands item = grip.GetComponentInParent<ItemHands>(true);
            Transform root = item != null ? item.transform : grip.root;
            return root.TransformPoint(set.Shoulder(side) - set.PreviewItemPosition);
        }

        // The implied elbow in edit mode: the item's root is assumed to sit at the player's Hand point, unrotated.
        public static Vector3 Elbow(Transform grip, HandSide side)
        {
            HandSet set = Set;
            ItemHands item = grip.GetComponentInParent<ItemHands>(true);
            Transform root = item != null ? item.transform : grip.root;
            Vector3 local = set != null ? set.Elbow(side) - set.PreviewItemPosition : new Vector3(side == HandSide.Left ? -0.4f : 0.15f, -0.35f, -0.4f);
            return root.TransformPoint(local);
        }

        public static HandSet Set
        {
            get
            {
                if (_set == null)
                {
                    string[] guids = AssetDatabase.FindAssets($"t:{nameof(HandSet)}");
                    if (guids.Length > 0)
                        _set = AssetDatabase.LoadAssetAtPath<HandSet>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
                return _set;
            }
        }

        public static HandRig Spawn(HandSide side, Scene scene)
        {
            HandRig prefab = Set != null ? Set.Get(side) : null;
            if (prefab == null)
            {
                Debug.LogWarning($"Hand preview: no {nameof(HandSet)} asset with a {side} hand.");
                return null;
            }

            HandRig rig = Object.Instantiate(prefab);
            rig.name = $"(Preview) {prefab.name}";
            if (scene.IsValid() && scene != rig.gameObject.scene)
                SceneManager.MoveGameObjectToScene(rig.gameObject, scene);
            foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.HideAndDontSave;
            return rig;
        }

        static void Destroy(Entry entry)
        {
            if (entry.Rig != null)
                Object.DestroyImmediate(entry.Rig.gameObject);
        }
    }
}
#endif
