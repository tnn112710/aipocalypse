using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class GameObjectUtils
    {
        private static readonly string tag_MainCamera = "MainCamera";

        /// <summary>
        /// Attempt to autoconfigure the camera, if it is not already set.
        /// Log a warning about needing to set the camera if successfull,
        /// log an error if not successful.
        /// </summary>
        public static Camera GetMainCamera()
        {

            // find all cameras
            Camera[] cameras = Object.FindObjectsOfType<Camera>();

            Camera mainCamera = null;
            foreach (Camera camera in cameras)
            {

                // skip all inaktive
                if (!camera.isActiveAndEnabled)
                    continue;

                // last one wins unless it's tagged as maincamera, then that one wins
                if (!mainCamera || camera.CompareTag(tag_MainCamera))
                {
                    mainCamera = camera;
                }

            }

            if (mainCamera)
            {
                return mainCamera;
            }
            else
            {
                Debug.LogError("No camera found in the scene.");
                return null;

            }
        }

        public static Light GetDirectionalLight()
        {
            Light directionalLight = RenderSettings.sun;

            return directionalLight;

        }

        /// <summary>
        /// Delete all the gameobjects of a parent (container)
        /// </summary>
        /// <param name="parent"></param>
        public static void ClearContainerChildren( GameObject parent, bool registerUndo)
        {
            if (parent == null)
                return;

            if( registerUndo)
            {
                Undo.RegisterFullObjectHierarchyUndo( parent, "Clear " + parent.name);
            }

            List<Transform> list = new List<Transform>();
            foreach (Transform child in parent.transform)
            {
                list.Add(child);
            }

            foreach (Transform child in list)
            {
                GameObject go = child.gameObject;

                Object.DestroyImmediate(go);

            }
        }

        /// <summary>
        /// SetParentAndAlign of Unity removes the layers. This method keeps the layers
        /// </summary>
        /// <param name="child"></param>
        /// <param name="parent"></param>
        public static void SetParentAndAlign_KeepHierarchyLayers(GameObject child, GameObject parent)
        {
            if (!child || !parent) return;

            // Snapshot all layers in the hierarchy
            var layers = new Dictionary<Transform, int>(128);
            foreach (var t in child.GetComponentsInChildren<Transform>(true))
                layers[t] = t.gameObject.layer;

            GameObjectUtility.SetParentAndAlign(child, parent);

            // Restore
            foreach (var kvp in layers)
                if (kvp.Key) kvp.Key.gameObject.layer = kvp.Value;
        }
    }
}