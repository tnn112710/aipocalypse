#if __MICROVERSE_ROADS__
using JBooth.MicroVerseCore;
using UnityEditor;
using UnityEngine;
using UnityEngine.Splines;

namespace Rowlan.Genesis.Roads
{
    public class RoadDesignController
    {

        private RoadsModule editor;

        private enum DesignTab
        {
            General,
            Looping,
            Knot,
            Twist,
            Hills
        }

        DesignTab designTab = DesignTab.General;
        GUIContent[] designTabs;

        IRoadDesigner generalDesigner;
        IRoadDesigner loopingDesigner;
        IRoadDesigner knotDesigner;
        IRoadDesigner twistDesigner;
        IRoadDesigner hillsDesigner;

        public RoadDesignController(RoadsModule editor)
        {
            this.editor = editor;

            designTabs = new GUIContent[]
{
                new GUIContent( "General", "General Operations"),
                new GUIContent( "Looping", "Looping Settings"),
                new GUIContent( "Knot", "Knot Settings"),
                new GUIContent( "Twist", "Twist Settings"),
                new GUIContent( "Hills", "Hills Settings"),

            };

            generalDesigner = new GeneralDesigner(this);
            loopingDesigner = new LoopingDesigner(this);
            knotDesigner = new KnotDesigner(this);
            twistDesigner = new TwistDesigner(this);
            hillsDesigner = new HillsDesigner(this);
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("Design the spline or parts of the spline. The changes will be applied to the selected spline knots in edit mode. Please note that closed loops aren't supported", MessageType.None);

            EditorGUILayout.BeginVertical("box");
            {
                // EditorGUILayout.LabelField("Design", GUIStyles.BoxTitleStyle);

                designTab = (DesignTab) GUILayout.Toolbar((int) designTab, designTabs, EditorStyles.miniButton);

                switch (designTab)
                {
                    case DesignTab.General:
                        generalDesigner.OnInspectorGUI();
                        break;

                    case DesignTab.Looping:
                        loopingDesigner.OnInspectorGUI();
                        break;

                    case DesignTab.Knot:
                        knotDesigner.OnInspectorGUI();
                        break;

                    case DesignTab.Twist:
                        twistDesigner.OnInspectorGUI();
                        break;

                    case DesignTab.Hills:
                        hillsDesigner.OnInspectorGUI();
                        break;
                }
            }
            EditorGUILayout.EndVertical();
           
        }

        public bool IsRollEnabled()
        {
            Road road = editor.GetRoad();

            if (!road)
                return false;

            return road.allowRoll;
        }

        public RoadsModule GetEditor()
        {
            return this.editor;
        }

    }
}
#endif