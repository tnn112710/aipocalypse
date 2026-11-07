//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(WindFX))]
    [CanEditMultipleObjects]
    public class WindFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField windAmount = new PropertyField();
            windAmount.BindProperty(serializedObject.FindProperty("windStrength"));
            container.Add(windAmount);

            PropertyField windHeading = new PropertyField();
            windHeading.BindProperty(serializedObject.FindProperty("heading"));
            container.Add(windHeading);

            PropertyField windSpeed = new PropertyField();
            windSpeed.BindProperty(serializedObject.FindProperty("windSpeed"));
            container.Add(windSpeed);

            PropertyField gustingPower = new PropertyField();
            gustingPower.BindProperty(serializedObject.FindProperty("gustingPower"));
            container.Add(gustingPower);

            PropertyField gustingFrequency = new PropertyField();
            gustingFrequency.BindProperty(serializedObject.FindProperty("gustingFrequency"));
            container.Add(gustingFrequency);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            return container;
        }
    }
}