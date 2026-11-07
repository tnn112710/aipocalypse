using UnityEngine;
using UnityEditor;

namespace JBooth.MicroVerseCore
{
    // Adds a "GameObject > MicroVerse > Create Clear Mesh Stamp" menu item
    // alongside the standard "Create Clear Stamp" entry. Lives in its own
    // file so MicroVerse Core's MenuItems.cs doesn't have to be touched —
    // updates to the package won't overwrite this.
    static class ClearMeshStampMenuItems
    {
        // Matches the priority MicroVerse uses for the standard Clear Stamp
        // so the new entry lands in the same visual group in the menu.
        const int kMenuPriorityStamps = 100;

#if __MICROVERSE_VEGETATION__
        [MenuItem("GameObject/MicroVerse/Create Clear Mesh Stamp", false, kMenuPriorityStamps + 1)]
        static void CreateClearMeshStamp()
        {
            CreateGO("Clear Mesh Stamp").AddComponent<ClearMeshStamp>();

        }

        #region copied from MV
        public static GameObject CreateGO(string name)
        {
            GameObject go = new GameObject(name);
            if (Selection.activeObject != null)
            {
                if (Selection.activeObject as GameObject)
                {
                    go.transform.SetParent(((GameObject)Selection.activeObject).transform);
                }
            }

            if (Selection.activeObject is GameObject)
            {
                GameObject parent = Selection.activeObject as GameObject;
                go.transform.SetParent(parent.transform, false);
            }
            if (go.GetComponentInParent<MicroVerse>() == null && MicroVerse.instance != null)
            {
                go.transform.SetParent(MicroVerse.instance.gameObject.transform, true);
            }
            go.transform.localScale = new Vector3(100, 100, 100);
            Selection.activeObject = go;
            return go;
        }
        #endregion copied from MV
#endif
    }
}
