//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(ClimateFX))]
    [CanEditMultipleObjects]
    public class ClimateFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField temperatureOffset = new PropertyField();
            temperatureOffset.BindProperty(serializedObject.FindProperty("temperature"));
            container.Add(temperatureOffset);

            PropertyField humidity = new PropertyField();
            humidity.BindProperty(serializedObject.FindProperty("humidity"));
            container.Add(humidity);

            PropertyField pressure = new PropertyField();
            pressure.BindProperty(serializedObject.FindProperty("pressure"));
            container.Add(pressure);

            PropertyField uvIndex = new PropertyField();
            uvIndex.BindProperty(serializedObject.FindProperty("uvIndex"));
            container.Add(uvIndex);

            PropertyField airQuality = new PropertyField();
            airQuality.BindProperty(serializedObject.FindProperty("airQuality"));
            container.Add(airQuality);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            
            return container;
        }
    }
}