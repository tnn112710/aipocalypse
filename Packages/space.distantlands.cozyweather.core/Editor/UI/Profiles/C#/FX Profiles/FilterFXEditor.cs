//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(FilterFX))]
    [CanEditMultipleObjects]
    public class FilterFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField filterSaturation = new PropertyField();
            filterSaturation.BindProperty(serializedObject.FindProperty("filterSaturation"));
            container.Add(filterSaturation);

            PropertyField filterValue = new PropertyField();
            filterValue.BindProperty(serializedObject.FindProperty("filterValue"));
            container.Add(filterValue);

            PropertyField filterColor = new PropertyField();
            filterColor.BindProperty(serializedObject.FindProperty("filterColor"));
            container.Add(filterColor);

            PropertyField sunFilter = new PropertyField();
            sunFilter.BindProperty(serializedObject.FindProperty("sunFilter"));
            container.Add(sunFilter);

            PropertyField cloudFilter = new PropertyField();
            cloudFilter.BindProperty(serializedObject.FindProperty("cloudFilter"));
            container.Add(cloudFilter);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            
            return container;
        }
    }
}