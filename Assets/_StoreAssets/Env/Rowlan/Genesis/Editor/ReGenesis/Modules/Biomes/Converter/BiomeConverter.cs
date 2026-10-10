using JBooth.MicroVerseCore;
using System.Collections;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class BiomeConverter
    {
        private ReGenesisModule editor;

        public BiomeConverter(ReGenesisModule editor)
        {
            this.editor = editor;

        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginHorizontal();
            {

                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.HelpBox("Selected gameobject\n* set transform (300/120/300)\n* add falloff override (Range)\n* add clear stamp as first child\n\nChildren:\n* reset transforms\n* remove falloff overrides", MessageType.None);

                    if (GUILayout.Button("Convert to Biome", GUILayout.Height(30)))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(editor.GetEditorTarget().gameObject, "ReGenesis Convert to Biome");

                        ConvertToBiome();

                        MicroVerse.instance.Invalidate();

                    }
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                {

                    EditorGUILayout.HelpBox("Selected gameobject\n* set transform (300/120/300)\n* add falloff override (Range)\n* no clear stamp\n\nChildren:\n* reset transforms\n* remove falloff overrides", MessageType.None);

                    if (GUILayout.Button("Restructure", GUILayout.Height(30)))
                    {
                        Undo.RegisterFullObjectHierarchyUndo(editor.GetEditorTarget().gameObject, "ReGenesis Restructure");

                        Restructure();

                        MicroVerse.instance.Invalidate();

                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void ConvertToBiome()
        {
            if (Restructure(out GameObject go))
            {
#if __MICROVERSE_VEGETATION__

                // add clear stamp as first child
                ClearStamp cs = go.GetComponentInChildren<ClearStamp>();

                if (cs == null)
                {
                    GameObject csgo = new GameObject("Clear Stamp");

                    GameObjectUtility.SetParentAndAlign(csgo, go);
                    csgo.transform.SetAsFirstSibling();

                    cs = csgo.AddComponent<ClearStamp>();

                    cs.clearDetails = true;
                    cs.clearObjects = true;
                    cs.clearTrees = true;

                }
#endif
                // delayed invalidate
                // otherwise trees would remain painted while the gameobject's transform itself is moved to origin
                // MicroVerse.instance.Invalidate(null);
                EditorCoroutineUtility.StartCoroutine(DelayedInvalidate( go), this);

            }
        }

        /// <summary>
        /// Disable the gameobject and reenable it in the next frame and perform MV invalidation.
        /// For some yet unknown reason the trees remain painted when a biome is created.
        /// This is a temporary working solution. Could also be a new Unity bug, this worked before without delayed invalidate.
        /// </summary>
        /// <param name="go"></param>
        /// <returns></returns>
        public IEnumerator DelayedInvalidate(GameObject go)
        {
            // disable go
            go.SetActive(false);
            
            // wait 1 frame
            yield return null;

            // enable go
            go.SetActive(true);

            // invalidate MV
            MicroVerse.instance.Invalidate(null);
            SceneView.lastActiveSceneView.Repaint();

            yield break;
        }

        /// <summary>
        /// Restructure the current gameobject. Reset child transforms, remove child falloff overrides, add falloff override to gameobject.
        /// </summary>
        private void Restructure()
        {
            bool success = Restructure(out GameObject go);
        }

        /// <summary>
        /// Restructure the current gameobject.Reset child transforms, remove child falloff overrides, add falloff override to gameobject.
        /// Return restructured gameobject for further processing.
        /// </summary>
        /// <param name="go"></param>
        /// <returns></returns>
        private bool Restructure( out GameObject go)
        {
            go = editor.GetEditorTarget().gameObject;

            if (go == null)
                return false;

            // remove all falloff overrides including the one of the current gameobject
            FalloffOverride[] fos = go.GetComponentsInChildren<FalloffOverride>();
            foreach (FalloffOverride f in fos)
            {
                Object.DestroyImmediate(f);
            }

            // reset all transform in all children including current gameobject
            foreach (Transform t in go.GetComponentsInChildren<Transform>())
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                t.localScale = Vector3.one;
            }

            // reset transform of current gameobject
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(300, 120, 300);


            // add new falloff override
            FalloffOverride fo = go.AddComponent<FalloffOverride>();

            fo.filter = new FalloffFilter();
            fo.filter.filterType = FalloffFilter.FilterType.Range;

            return go;
        }
    }
}