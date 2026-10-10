using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
    public class TemplateProvider<TC> where TC : UnityEngine.Object, ITemplateCollection
    {

        /// Number of prefab template drop targets per row in the template grid of the inspector
        /// </summary>
        public const int PrefabTemplateGridColumnCount = 4;

        public TC templateCollection;

        private ITemplateProcessor templateProcessor;

        public TemplateProvider(ITemplateProcessor templateProcessor)
        {
            this.templateProcessor = templateProcessor;

            LoadTemplateCollection();
        }

        private void LoadTemplateCollection()
        {
            // load the available prefab settings templates
            string[] templateCollectionGuids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(TC)));

            // check if we have a template collection at all
            if (templateCollectionGuids.Length == 0)
            {
                Debug.LogError(string.Format("No asset of type {0} found", typeof(TC)));
                return;
            }

            // use the first one found
            string templateCollectionGuidsFilePath = AssetDatabase.GUIDToAssetPath(templateCollectionGuids[0]);

            // if we have more than 1 colleciton, inform the user that e use the first one
            if (templateCollectionGuids.Length > 1)
            {
                Debug.LogError(string.Format("Multiple template collections found, this isn't supported yet. Using the first one found: {0}", templateCollectionGuidsFilePath));
            }

            templateCollection = (TC) AssetDatabase.LoadAssetAtPath(templateCollectionGuidsFilePath, typeof(TC));

            if (!templateCollection)
            {
                Debug.LogError(string.Format("Template collection not found: {0}", templateCollectionGuidsFilePath));
            }
             
        }

        public void DrawDropTargets()
        {
            if (!templateCollection)
                return;

            EditorGUILayout.HelpBox(templateCollection.GetPresetHelpText(), MessageType.None);

            GUILayout.BeginHorizontal();
            {
                GUILayout.BeginVertical();
                {
                    int gridRows = Mathf.CeilToInt((float)templateCollection.GetTemplates().Length / PrefabTemplateGridColumnCount);

                    for (int row = 0; row < gridRows; row++)
                    {
                        GUILayout.BeginHorizontal();
                        {
                            for (int column = 0; column < PrefabTemplateGridColumnCount; column++)
                            {
                                int index = column + row * PrefabTemplateGridColumnCount;

                                // drop area
                                Rect prefabDropArea = GUILayoutUtility.GetRect(0.0f, 34.0f, GUIStyles.DropAreaStyle, GUILayout.ExpandWidth(true));

                                bool hasDropArea = index < templateCollection.GetTemplates().Length;
                                if (hasDropArea)
                                {
                                    ITemplateSettings template = templateCollection.GetTemplates()[index];

                                    // drop area box with background color and info text
                                    GUI.color = GUIStyles.DropAreaBackgroundColor;
                                    GUI.Box(prefabDropArea, template.GetName(), GUIStyles.DropAreaStyle);
                                    GUI.color = GUIStyles.DefaultBackgroundColor;

                                    Event evt = Event.current;
                                    switch (evt.type)
                                    {
                                        case EventType.DragUpdated:
                                        case EventType.DragPerform:

                                            if (prefabDropArea.Contains(evt.mousePosition))
                                            {

                                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                                                if (evt.type == EventType.DragPerform)
                                                {
                                                    DragAndDrop.AcceptDrag();

                                                    //foreach (Object droppedObject in DragAndDrop.objectReferences)
                                                    //{
                                                    //    Debug.Log($"Dropped {droppedObject}");
                                                    //}

                                                    templateProcessor.Process( template, DragAndDrop.objectReferences);
                                                }
                                            }
                                            break;
                                    }
                                }

                            }
                        }
                        GUILayout.EndHorizontal();
                    }
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();


        }
    }
}