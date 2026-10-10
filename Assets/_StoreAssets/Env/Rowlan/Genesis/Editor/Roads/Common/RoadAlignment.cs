#if __MICROVERSE_ROADS__

using JBooth.MicroVerseCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Roads
{
    public class RoadAlignment
    {
        private RoadsModule editor;

        /// <summary>
        /// The supported types
        /// </summary>
        private Type[] types = new Type[] { typeof(Intersection), typeof(Road) };
    
        public RoadAlignment(RoadsModule editor)
        {
            this.editor = editor;
        }

        /// <summary>
        /// Align the selected components (Intersection and Road objects) to the terrain height
        /// </summary>
        /// <param name="components"></param>
        public void AlignSelectionToTerrain()
        {
            // get items to modify
            List<Component> components = SelectionUtils.GetSelectedComponents(types);

            AlignToTerrain(components);
        }

        /// <summary>
        /// Align the specified components (Intersection and Road objects) to the terrain height
        /// </summary>
        /// <param name="components"></param>
        public void AlignToTerrain(List<Component> components)
        {
            // get only those items that are active
            components = components.Where(x => x is MonoBehaviour behaviour && behaviour.isActiveAndEnabled == true && x.transform.gameObject.activeInHierarchy == true).ToList();

            // disable items
            components.ForEach(x => x.transform.gameObject.SetActive(false));

            //  update microverse without the objects
            editor.GetRoadSystem()?.ClearCacheAndUpdateSystem();

            // recreate terrain without splines, no async
            MicroVerse.instance.Modify(true, true);

            // alignment
            #region Alignment

            // roads
            foreach (Component component in components)
            {
                if (component is Road)
                {
                    (component as Road).AlignToTerrain();
                }
                else if (component is Intersection)
                {
                    (component as Intersection).AlignToTerrain();
                }
            }
            #endregion Alignment

            // re-enable items; we can use all the data because the list consists only of the previously active ones
            components.ForEach(x => x.transform.gameObject.SetActive(true));

            // recreate terrain with splines, no async
            MicroVerse.instance.Modify(true, true);
            MicroVerse.instance.Invalidate();

            //  update microverse with the objects
            editor.GetRoadSystem()?.ClearCacheAndUpdateSystem();

        }
    }
}
#endif