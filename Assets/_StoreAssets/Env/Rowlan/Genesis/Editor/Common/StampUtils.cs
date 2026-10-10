using UnityEngine;
using System.Linq;
using UnityEditor;
using JBooth.MicroVerseCore;
using System.Collections.Generic;

namespace Rowlan.Genesis
{
    public class StampUtils
    {
        /// <summary>
        /// Get all stamps of the same hierarchy level
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static List<Stamp> GetSiblings(Stamp source)
        {
            if (source == null)
                return new List<Stamp>();

            Transform parent = source.transform.parent;

            if (parent == null)
                return new List<Stamp>();

            List<Stamp> list = new List<Stamp>();

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);

                // skip self
                if (child == source.transform)
                    continue;

                // get stamp
                Stamp stamp = child.GetComponent<Stamp>();

                if (stamp == null)
                    continue;

                list.Add(stamp);
            }

            return list;
        }

        /// <summary>
        /// Get all stamps of the selected gameobjects
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static List<Stamp> GetSelectedStamps(Stamp source)
        {
            if (source == null)
                return new List<Stamp>();

            Transform sourceTransform = source.transform;

            List<Stamp> list = new List<Stamp>();

            Object[] selectedObjects = Selection.objects;

            foreach (Object selectedObject in selectedObjects)
            {
                if (!(selectedObject is GameObject))
                    continue;

                Transform otherTransform = (selectedObject as GameObject).transform;

                // skip self
                if (sourceTransform == otherTransform)
                    continue;

                // get stamp
                Stamp stamp = otherTransform.GetComponent<Stamp>();

                if (stamp == null)
                    continue;

                list.Add(stamp);
            }

            return list;
        }

        /// <summary>
        /// Get all stamps of the transform and its children
        /// </summary>
        /// <param name="source"></param>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static List<Stamp> GetHierarchy( Transform transform)
        {
            if (transform == null)
                return new List<Stamp>();

            List<Stamp> stamps = transform.GetComponentsInChildren<Stamp>().ToList();
                        
            return stamps;

        }

        /// <summary>
        /// Get all stamps of the transform and its children
        /// </summary>
        /// <param name="source"></param>
        /// <param name="transform"></param>
        /// <returns></returns>
        public static List<Stamp> GetTargetChildren(Stamp source, Transform transform)
        {
            if (transform == null)
                return new List<Stamp>();

            List<Stamp> stamps = transform.GetComponentsInChildren<Stamp>().ToList();

            stamps.Remove(source);

            return stamps;

        }

        public static void RegisterUndo(List<Stamp> list, string undoText)
        {
            foreach (Stamp stamp in list)
            {
                if (stamp == null)
                    continue;

                Undo.RegisterCompleteObjectUndo(stamp, undoText);
            }
        }
    }
}