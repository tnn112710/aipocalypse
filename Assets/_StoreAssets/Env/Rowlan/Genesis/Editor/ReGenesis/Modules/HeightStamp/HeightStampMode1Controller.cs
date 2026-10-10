using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class HeightStampMode1Controller
    {
        private ReGenesisModule editor;

        public HeightStampMode1Controller(ReGenesisModule editor)
        {
            this.editor = editor;
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Settings", GUIStyles.BoxTitleStyle);

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Position"))
                    {
                        Transform[] transforms = editor.GetSelectedHeightStampTransforms();

                        Undo.RegisterCompleteObjectUndo(transforms, "ReGenesis Position");

                        HeightStampUtils.SetRandomPosition(transforms, HeightStampUtils.ConstrainWorldBounds.Half);

                    }

                    if (GUILayout.Button("Rotation"))
                    {
                        Transform[] transforms = editor.GetSelectedHeightStampTransforms();

                        Undo.RegisterCompleteObjectUndo(transforms, "ReGenesis Rotation");

                        HeightStampUtils.SetRandomRotation(transforms);

                    }
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Position & Rotation", GUILayout.Height(30)))
                {
                    Transform[] transforms = editor.GetSelectedHeightStampTransforms();

                    Undo.RegisterCompleteObjectUndo(transforms, "ReGenesis Position & Rotation");

                    HeightStampUtils.SetRandomPosition(transforms, HeightStampUtils.ConstrainWorldBounds.Half);
                    HeightStampUtils.SetRandomRotation(transforms);

                }
            }
            EditorGUILayout.EndVertical();
        }

    }
}