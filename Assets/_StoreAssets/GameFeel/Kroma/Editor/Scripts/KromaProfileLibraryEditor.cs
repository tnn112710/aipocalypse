using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace FayLabs.Kroma.Editor
{
    [CustomEditor(typeof(KromaProfileLibrary))]
    public class KromaProfileLibraryEditor : UnityEditor.Editor
    {
        private ReorderableList _list;

        private void OnEnable()
        {
            _list = new ReorderableList(serializedObject, serializedObject.FindProperty("profiles"), true, true, true, true);
            
            _list.drawHeaderCallback = (Rect rect) => {
                EditorGUI.LabelField(rect, "Registered Profiles");
            };
            
            _list.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) => {
                var element = _list.serializedProperty.GetArrayElementAtIndex(index);
                rect.y += 2;
                
                // Draw just the object field
                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight),
                    element, 
                    GUIContent.none
                );
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            EditorGUILayout.HelpBox("This library contains all profiles visible in the Kroma Browser.", MessageType.Info);
            EditorGUILayout.Space(5);
            
            _list.DoLayoutList();
            
            serializedObject.ApplyModifiedProperties();
            
            // Drop area to add generic assets? 
            // Standard object field handles dragging one by one.
            // If we want bulk drag-drop, we need a custom drop area, but standard list includes + button.
        }
    }
}
