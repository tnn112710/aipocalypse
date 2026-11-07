//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{

    [CustomPropertyDrawer(typeof(FXAttribute))]
    public class FXDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();

            Foldout foldout = new Foldout();
            foldout.text = "";
            container.Add(foldout);
            PropertyField propertyField = new PropertyField();
            propertyField.style.flexGrow = 1;
            propertyField.BindProperty(property);
            propertyField.label = "";
            propertyField.style.marginRight = 6;
            propertyField.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                propertyField.Q<VisualElement>(className: "unity-image").style.marginRight = 6;
            });
            foldout.Q<VisualElement>(className: "unity-base-field__input").Add(propertyField);
            foldout.value = false;

            propertyField.RegisterValueChangeCallback(evt => 
            {
                foldout.Clear();

                if (property.objectReferenceValue != null)
                {
                    CozyFXProfile profile = (CozyFXProfile)property.objectReferenceValue;
                    FXProfileEditor editor = Editor.CreateEditor(profile as ScriptableObject) as FXProfileEditor;
                    VisualElement profileEditor = editor.RenderInWindow();
                    foldout.Add(profileEditor);
                }
            });

            return container;
        }
    }


    public class FXProfileSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        public Action<CozyFXProfile> onSelect;

        public void Init(Action<CozyFXProfile> action)
        {
            this.onSelect = action;
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            var entries = new List<SearchTreeEntry>
        {
            new SearchTreeGroupEntry(new GUIContent("Add FX Profile"), 0),
        };

            string[] guids = AssetDatabase.FindAssets("t:CozyFXProfile");
            List<CozyFXProfile> foundProfiles = new List<CozyFXProfile>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) is CozyFXProfile asset) foundProfiles.Add(asset);
            }

            var sortedProfileGroups = foundProfiles.OrderBy(p => (p as ScriptableObject).name).GroupBy(x => x.GetType().Name);

            foreach (var group in sortedProfileGroups)
            {
                string groupName = Regex.Replace(group.Key, @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ").Trim();
                Texture2D icon = (Texture2D)Resources.Load($"COZY Icons/{groupName}");
                entries.Add(new SearchTreeGroupEntry(new GUIContent(groupName, icon)) { level = 1 });

                foreach (var fx in group)
                    entries.Add(new SearchTreeEntry(new GUIContent((fx as ScriptableObject).name, icon))
                    {
                        level = 2,
                        userData = fx
                    });
            }

            return entries;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            if (SearchTreeEntry.userData is CozyFXProfile selectedProfile)
            {
                onSelect?.Invoke(selectedProfile);
                return true;
            }
            return false;
        }
    }

}