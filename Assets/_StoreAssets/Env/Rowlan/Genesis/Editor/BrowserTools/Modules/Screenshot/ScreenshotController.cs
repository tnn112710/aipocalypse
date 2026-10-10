using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserTools
{
    public class ScreenshotController
    {
        private static GUIContent thumbnailGuideContent = new GUIContent("Thumbnail Guide", "Find existing thumbnail guide and select it. If none could be found, a thumbnail guide gameobject will be created");

        private BrowserToolsModule editor;

        public ScreenshotController(BrowserToolsModule editor)
        {
            this.editor = editor;

        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox("Where is the thumbnail creator?" +
                    "\n\nJason allowed me to integrate it directly into MicroVerse which is more convenient for everyone." +
                    "\n\nThe thumbnail creator can be accessed directly from within a preset item's context menu within the Content Browser." +
                    "\n\nI also added a menu option to access (ping) the content collection directly, so you can adjust the order of the items there."
                    , MessageType.Info);

                EditorGUILayout.LabelField("Guides", EditorStyles.miniBoldLabel);

                if (GUILayout.Button(thumbnailGuideContent))
                {
                    FindOrAddThumbnailGuide();
                }

                EditorGUILayout.LabelField("Object Frame", EditorStyles.miniBoldLabel);

                if (GUILayout.Button("Frame Selected Object"))
                {
                    FrameSelectedObject();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void FindOrAddThumbnailGuide()
        {
            // try finding the thumbnail guide
            ThumbnailGuide existingThumbnailGuide = GameObject.FindFirstObjectByType<ThumbnailGuide>();
            if (existingThumbnailGuide != null)
            {
                Selection.activeObject = existingThumbnailGuide.gameObject;
                return;
            }

            // create gameobject
            GameObject go = new GameObject("Thumbnail Guide");
            GameObjectUtility.SetParentAndAlign(go, null);

            // ensure unique name
            GameObjectUtility.EnsureUniqueNameForSibling(go);

            // add script
            go.AddComponent<ThumbnailGuide>();

            // Register the creation in the undo system
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);

            Selection.activeObject = go;

        }

        private void FrameSelectedObject()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            Camera camera = sceneView.camera;
            Object obj = Selection.activeObject;

            if (obj == null || !(obj is GameObject))
                return;

            GameObject go = obj as GameObject;
            
            if( BoundsUtils.CalculateBounds( go, out Bounds bounds))
            {
                sceneView.Frame( bounds, false);
            }
        }
    }
}