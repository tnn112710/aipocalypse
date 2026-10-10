using JBooth.MicroVerseCore;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.ReGenesis
{
    public class BiomesMode1Controller
    {
        private enum TargetType
        {
            Sibling,
            Selection,
            TargetChildren,
        }

        private ReGenesisModule editor;

        private SerializedProperty includeChildren;
        private SerializedProperty noiseRandomization;

        private TargetType targetType = TargetType.Sibling;
        private GameObject targetObject = null;

        public BiomesMode1Controller(ReGenesisModule editor)
        {
            this.editor = editor;

            SerializedProperty settings = editor.GetSerializedObject().FindProperty("biomesSettings");

            includeChildren = settings.FindPropertyRelative("includeChildren");
            noiseRandomization = settings.FindPropertyRelative("noiseRandomization");
        }

        public void OnInspectorGUI()
        {
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.LabelField("Randomize", GUIStyles.BoxTitleStyle);

                if (ReGenesisEditorWindow.helpBoxVisible)
                {
                    EditorGUILayout.HelpBox("Perform action on the gameobject and its children in the hierarchy.", MessageType.None);
                }

                EditorGUILayout.PropertyField(includeChildren);
                EditorGUILayout.PropertyField(noiseRandomization);

                EditorGUILayout.Space();

                if (ReGenesisEditorWindow.helpBoxVisible)
                {
                    EditorGUILayout.HelpBox("Change seed and noise offset on stamps of selected gameobjects and optionally their children where applicable.", MessageType.None);
                }

                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button("Seed"))
                    {
                        Stamp[] stamps = GetStamps();

                        Undo.RegisterCompleteObjectUndo(stamps, "Change Seed");

                        RandomUtils.SetRandomSeed(stamps);

                        MicroVerse.instance.Invalidate();

                    }

                    if (GUILayout.Button("Noise"))
                    {
                        Stamp[] stamps = GetStamps();

                        Undo.RegisterCompleteObjectUndo(stamps, "Change Noise Offset");

                        RandomUtils.SetRandomNoiseOffset(stamps, editor.GetEditorTarget().biomesSettings.noiseRandomization);

                        MicroVerse.instance.Invalidate();

                    }
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Seed & Noise", GUILayout.Height(30)))
                {
                    Stamp[] stamps = GetStamps();

                    Undo.RegisterCompleteObjectUndo(stamps, "ReGenesis Seed & Noise");

                    RandomUtils.SetRandomSeed(stamps);
                    RandomUtils.SetRandomNoiseOffset(stamps, editor.GetEditorTarget().biomesSettings.noiseRandomization);

                    MicroVerse.instance.Invalidate();

                }
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space();

            EditorGUILayout.BeginVertical("box");
            {

                EditorGUILayout.LabelField("Transfer", GUIStyles.BoxTitleStyle);

                targetType = (TargetType) EditorGUILayout.EnumPopup("Target Type", targetType);

                bool valid = true;

                // target type specific settings and info
                switch (targetType)
                {
                    case TargetType.Sibling:
                        EditorGUILayout.HelpBox("Transfer settings of current selected stamp to all other stamps on the same hierarchy level", MessageType.None);
                        break;
                    case TargetType.Selection:
                        EditorGUILayout.HelpBox("Transfer settings of current selected stamp to all other selected stamps", MessageType.None);
                        break;
                    case TargetType.TargetChildren:
                        targetObject = EditorGUILayout.ObjectField("Target Object", targetObject, typeof(GameObject), true) as GameObject;
                        EditorGUILayout.HelpBox("Transfer settings of current selected stamp to all children of the target object", MessageType.None);
                        valid = targetObject != null;
                        break;
                    default:
                        break;
                }


                EditorGUI.BeginDisabledGroup(!valid);
                {
                    EditorGUILayout.Space();

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("Size"))
                        {
                            Stamp source = GetStamp();
                            List<Stamp> targets = GetTargetStamps(source);

                            StampUtils.RegisterUndo(targets, "Transfer size");

                            TransferUtils.TransferSize(source, targets);
                        }

                        if (GUILayout.Button("Distance"))
                        {
                            Stamp source = GetStamp();
                            List<Stamp> targets = GetTargetStamps(source);

                            StampUtils.RegisterUndo(targets, "Transfer distance");

                            TransferUtils.TransferDistance(source, targets);
                        }

                        if (GUILayout.Button("Density"))
                        {
                            Stamp source = GetStamp();
                            List<Stamp> targets = GetTargetStamps(source);

                            StampUtils.RegisterUndo(targets, "Transfer density");

                            TransferUtils.TransferDensity(source, targets);
                        }

                        if (GUILayout.Button("Noise"))
                        {
                            Stamp source = GetStamp();
                            List<Stamp> targets = GetTargetStamps(source);

                            StampUtils.RegisterUndo(targets, "Transfer noise");

                            TransferUtils.TransferNoise(source, targets);
                        }

                        if (GUILayout.Button("Filters"))
                        {
                            Stamp source = GetStamp();
                            List<Stamp> targets = GetTargetStamps(source);

                            StampUtils.RegisterUndo(targets, "Transfer filters");

                            TransferUtils.TransferFilters(source, targets);
                        }

                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    {
                        if (GUILayout.Button("All", GUILayout.Height(30)))
                        {
                            Stamp source = GetStamp();
                            List<Stamp> targets = GetTargetStamps(source);

                            StampUtils.RegisterUndo(targets, "Transfer all");

                            TransferUtils.TransferSize(source, targets);
                            TransferUtils.TransferDistance(source, targets);
                            TransferUtils.TransferDensity(source, targets);
                            TransferUtils.TransferNoise(source, targets);
                            TransferUtils.TransferFilters(source, targets);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.EndDisabledGroup();

            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Get a list of stamps from the gameobject, depending on the setting also includes children
        /// </summary>
        /// <returns></returns>
        private Stamp[] GetStamps()
        {
            if(includeChildren.boolValue)
            {
                return editor.GetEditorTarget().gameObject.GetComponentsInChildren<Stamp>();
            }
            else
            {
                return editor.GetEditorTarget().gameObject.GetComponents<Stamp>();
            }
        }

        /// <summary>
        /// Get the first stamp of the gameobject
        /// </summary>
        /// <returns></returns>
        private Stamp GetStamp()
        {
            Stamp[] stamps = editor.GetEditorTarget().gameObject.GetComponents<Stamp>();

            if (stamps.Length == 0)
                return null;

            return stamps[0];
        }

        private List<Stamp> GetTargetStamps(Stamp source)
        {
            if( source == null)
                return new List<Stamp>();

            switch (targetType)
            {
                case TargetType.Sibling:
                    return StampUtils.GetSiblings(source);
                case TargetType.Selection:
                    return StampUtils.GetSelectedStamps(source);
                case TargetType.TargetChildren:
                    return StampUtils.GetTargetChildren(source, targetObject?.transform);
                default:
                    return new List<Stamp>();
            }
        }


    }
}