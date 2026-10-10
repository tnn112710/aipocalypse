using JBooth.MicroVerseCore;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis
{
#if __MICROVERSE_VEGETATION__
    public class TreeModule : IGenesisModule, ITemplateProcessor
    {
        private GenesisDataEditor editor;

        private SerializedProperty treePlacement;

        private TemplateProvider<TreeTemplateCollection> templateProvider;

        private Vector2 scrollPosition = Vector2.zero;
        public TreeModule(GenesisDataEditor editor)
        {
            this.editor = editor;

            SerializedProperty data = editor.serializedObject.FindProperty("treeSettings");

            treePlacement = data.FindPropertyRelative("treePlacement");

            templateProvider = new TemplateProvider<TreeTemplateCollection>( this);
        }

        public void OnInspectorGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            {
                // toolbar (clear etc)
                GUILayout.BeginVertical("box");
                {
                    GUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();

                        if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(50)))
                        {
                            Undo.RegisterCompleteObjectUndo(editor.editorTarget, "Clear");

                            editor.editorTarget.treeSettings.Reset();


                        }
                    }
                    GUILayout.EndHorizontal();
                }
                GUILayout.EndVertical();

                // templates
                GUILayout.BeginVertical("box");
                {
                    EditorGUILayout.LabelField("Presets", GUIStyles.BoxTitleStyle);

                    templateProvider.DrawDropTargets();
                }
                GUILayout.EndVertical();

                // settings
                EditorGUILayout.PropertyField(treePlacement, true);
            }
            EditorGUILayout.EndScrollView();

        }

        public void Create(GameObject parent)
        {
            GameObject trees = new GameObject("Trees");

            // parent
            trees.transform.parent = parent.transform;

            // set group
            trees.AddComponent<TreeGroup>();

            foreach (TreeStamp treePlacement in editor.editorTarget.treeSettings.treePlacement)
            {
                // create instance
                GameObject treePrefab = PrefabUtility.InstantiatePrefab(treePlacement.gameObject) as GameObject;
                treePrefab.transform.parent = trees.transform;
            }
        }

        public void Destroy()
        {
            JBooth.MicroVerseCore.MicroVerse microVerse = JBooth.MicroVerseCore.MicroVerse.instance;

            if (!microVerse)
                return;

            TreeGroup group = microVerse.GetTreeGroup();

            if (!group)
                return;

            Object.DestroyImmediate(group.gameObject);

        }

        public void Process(ITemplateSettings template, Object[] objects)
        {
            foreach (Object droppedObject in objects)
            {
                // consistency
                TreeStamp treePlacement = (droppedObject as GameObject).GetComponent<TreeStamp>();
                bool accept = treePlacement != null;

                if (!accept)
                {
                    Debug.LogError($"Invalid object {droppedObject} for template {template}");
                    continue;
                }

                // get current settings
                TreeStamp[] currentObjects = editor.editorTarget.treeSettings.treePlacement;

                // create new settings
                TreeStamp[] newObjects = new TreeStamp[] { treePlacement };

                // merge current and new settings
                editor.editorTarget.treeSettings.treePlacement = currentObjects.Concat(newObjects).ToArray();
            }
        }
    }
#else
    public class TreeModule : IGenesisModule, ITemplateProcessor
    {
        public TreeModule(GenesisDataEditor editor)
        {
        }

        public void Create(GameObject parent)
        {
        }

        public void Destroy()
        {
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("MicroVerse module not available", MessageType.Warning);
        }

        public void Process(ITemplateSettings template, Object[] objects)
        {
        }
    }
#endif
}