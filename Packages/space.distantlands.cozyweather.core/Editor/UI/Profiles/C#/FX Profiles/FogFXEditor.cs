//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(FogFX))]
    [CanEditMultipleObjects]
    public class FogFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField fogDensity = new PropertyField();
            fogDensity.BindProperty(serializedObject.FindProperty("fogCoverage"));
            container.Add(fogDensity);

            PropertyField fogHeightCoverage = new PropertyField();
            fogHeightCoverage.BindProperty(serializedObject.FindProperty("fogHeightCoverage"));
            container.Add(fogHeightCoverage);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            return container;
        }
    }
}