#if __MICROVERSE_SPLINES__

using JBooth.MicroVerseCore;
using JBooth.MicroVerseCore.Browser;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.BrowserWizard
{
    public class BiomesModule : IBrowserWizardModule
    {
        private BrowserWizardModule editor;
        private BrowserWizardData editorTarget;
        private SerializedObject serializedObject;

        private SerializedProperty mode;

        private GUIContent[] modeButtons;
        private Vector2 scrollPosition = Vector2.zero;


        private ShapeController shapeController;
        private HeightController heightController;
        private BiomesController biomesController;

        private SerializedProperty rootContainer;
        private SerializedProperty shapeContainer;
        private SerializedProperty heightContainer;
        private SerializedProperty biomesContainer;

        public BiomesModule(BrowserWizardModule editor, SerializedObject serializedObject)
        {
            this.editor = editor;
            this.editorTarget = editor.GetEditorTarget();
            this.serializedObject = serializedObject;

            Init();
        }

        private void Init()
        {
            modeButtons = new GUIContent[]
            {
                new GUIContent( "Shape", "Voronoi Shapes"),
                new GUIContent( "Height", "Height Stamps"),
                new GUIContent( "Biomes", "Biome Stamps"),
            };

            mode = serializedObject.FindProperty("biomesMode");

            // controllers
            shapeController = new ShapeController(editor, this);
            heightController = new HeightController(editor, this);
            biomesController = new BiomesController(editor, this);

            rootContainer = editor.GetSerializedObject().FindProperty("rootContainer");
            shapeContainer = editor.GetSerializedObject().FindProperty("shapeContainer");
            heightContainer = editor.GetSerializedObject().FindProperty("heightContainer");
            biomesContainer = editor.GetSerializedObject().FindProperty("biomesContainer");
        }


        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox("Automatically place presets of the Content Browser in the Scene" +
                    "\n* create a shape, ie splines of a voronoi diagram" +
                    "\n* create height stamps depending on the splines of the shape" +
                    "\n* create biome stamps depending on the splines of the shape" +
                    "\nNote: you can have different biome shapes on height stamps by changing the voronoi diagram", MessageType.None);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("Containers", EditorStyles.miniBoldLabel);

            EditorGUILayout.BeginHorizontal();
            {
                // indicate error: root gameobject required
                GUI.backgroundColor = editor.GetEditorTarget().rootContainer != null ? GUIStyles.DefaultBackgroundColor : GUIStyles.ErrorBackgroundColor;
                {
                    // using objectfield, we need scene objects
                    editor.GetEditorTarget().rootContainer = EditorGUILayout.ObjectField("Root", editor.GetEditorTarget().rootContainer, typeof(GameObject), true) as GameObject;
                }
                GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;

                if (GUILayout.Button("New", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    CreateContainer();

                    // create initial voronoi diagram
                    shapeController.Init();
                }

                if (GUILayout.Button(new GUIContent("Clear", "Remove all objects from this container"), EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    ClearAll();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;
            {
                bool prevEnabled = GUI.enabled;
                GUI.enabled = false;
                {
                    // error if root is set, but no spline; spline is required for biomes and height
                    GUI.backgroundColor = editor.GetEditorTarget().rootContainer != null && editor.GetEditorTarget().splineContainer == null ? GUIStyles.ErrorBackgroundColor: GUIStyles.DefaultBackgroundColor;
                    {
                        editor.GetEditorTarget().splineContainer = EditorGUILayout.ObjectField("Splines", editor.GetEditorTarget().splineContainer, typeof(GameObject), true) as GameObject;
                    }
                    GUI.backgroundColor = GUIStyles.DefaultBackgroundColor;
                    
                    editor.GetEditorTarget().biomesContainer = EditorGUILayout.ObjectField("Biomes", editor.GetEditorTarget().biomesContainer, typeof(GameObject), true) as GameObject;
                    editor.GetEditorTarget().heightContainer = EditorGUILayout.ObjectField("Height", editor.GetEditorTarget().heightContainer, typeof(GameObject), true) as GameObject;
                }
                GUI.enabled = prevEnabled;
            }
            EditorGUI.indentLevel--;

            EditorGUILayout.LabelField("Browser", EditorStyles.miniBoldLabel);

            ContentBrowser browser = ContentBrowserManager.GetContentBrowser();

            // browser not found, show message
            if (browser == null)
            {
                EditorGUILayout.HelpBox("Please open the Content Browser", MessageType.Error);
            }
            // browser found
            else
            {
                EditorGUI.BeginDisabledGroup(true);
                {
                    EditorGUILayout.TextField("Package", $"{browser.GetSelectedPackage()?.packName}");
                    EditorGUILayout.TextField("Presets", $"{browser.GetPresets()?.Count}");
                }
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.LabelField("Procedural", EditorStyles.miniBoldLabel);

            // toolbar
            EditorGUI.BeginChangeCheck();
            {
                mode.intValue = GUILayout.Toolbar(mode.intValue, modeButtons, EditorStyles.miniButton);
            }
            if (EditorGUI.EndChangeCheck())
            {
                ModeChanged((BrowserWizardData.BiomesMode)mode.intValue);
            }

            // control
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                GUI.enabled = GetRootContainer() != null;
                {
                    switch (editorTarget.biomesMode)
                    {
                        case BrowserWizardData.BiomesMode.Shapes:
                            shapeController.OnInspectorGUI(browser);
                            break;

                        case BrowserWizardData.BiomesMode.Heights:
                            heightController.OnInspectorGUI(browser);
                            break;

                        case BrowserWizardData.BiomesMode.Biomes:
                            biomesController.OnInspectorGUI(browser);
                            break;

                        default:
                            editorTarget.biomesMode = BrowserWizardData.BiomesMode.Shapes;
                            break;
                    }
                    GUI.enabled = true;
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void ModeChanged(BrowserWizardData.BiomesMode mode)
        {

            // link tabs with content browser
            ContentBrowser browser = ContentBrowserManager.GetContentBrowser();
            switch (mode)
            {
                case BrowserWizardData.BiomesMode.Shapes:
                    // nothing to do
                    break;

                case BrowserWizardData.BiomesMode.Heights:
                    browser.SetSelectedTab(ContentBrowser.Tab.Height);
                    break;

                case BrowserWizardData.BiomesMode.Biomes:
                    browser.SetSelectedTab(ContentBrowser.Tab.Biomes);
                    break;

                default:
                    // nothing to do
                    break;
            }

        }

        private void CreateContainer()
        {
            // create a parent GameObject for organization
            GameObject container = new GameObject("Browser Wizard");

            // parent to microverse
            GameObjectUtility.SetParentAndAlign(container, MicroVerse.instance.transform.gameObject);

            // ensure unique name
            GameObjectUtility.EnsureUniqueNameForSibling(container);

            Undo.RegisterCreatedObjectUndo(container, "Create biomes container");

            Selection.activeObject = container;

            editor.GetEditorTarget().rootContainer = container;

        }

        public void ClearAll()
        {
            // root of splines and biomes
            GameObject container = editor.GetEditorTarget().rootContainer;

            Clear(container);
        }

        public void Clear(GameObject container)
        {

            if (container == null)
                return;

            // ensure no object stamps remain when the container children get deleted
            ObjectStamp[] stamps = container.GetComponentsInChildren<ObjectStamp>();
            foreach (ObjectStamp stamp in stamps)
            {
                stamp.ClearSpawnedInstances();
            }

            // can't use this one; with that MV wouldn't detect delection and object containers would remain inactive in the hierarchy
            GameObjectUtils.ClearContainerChildren(container, true);

            MicroVerse.instance?.Invalidate(null);

        }

        public GameObject GetRootContainer()
        {
            return editor.GetEditorTarget().rootContainer;
        }

        public GameObject GetOrCreateSubContainer(string name, ref GameObject slot)
        {
            if (slot != null)
                return slot;

            Transform parentTransform = GetRootContainer()?.transform;

            if (parentTransform == null)
            {
                slot = null;
                return null;
            }

            GameObject parentGO = new GameObject(name);

            Undo.RegisterCreatedObjectUndo(parentGO, $"Create {name}");
            Undo.SetTransformParent(parentGO.transform, parentTransform, $"Create {name}");

            slot = parentGO;

            return parentGO;
        }


    }
}
#endif