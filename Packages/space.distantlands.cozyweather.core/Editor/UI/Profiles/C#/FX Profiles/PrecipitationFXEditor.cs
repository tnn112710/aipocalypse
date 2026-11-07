//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(PrecipitationFX))]
    [CanEditMultipleObjects]
    public class PrecipitationFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField rainAccumulationSpeed = new PropertyField();
            rainAccumulationSpeed.BindProperty(serializedObject.FindProperty("rainAccumulationSpeed"));
            container.Add(rainAccumulationSpeed);

            PropertyField maximumRainAmount = new PropertyField();
            maximumRainAmount.BindProperty(serializedObject.FindProperty("maximumRainAmount"));
            container.Add(maximumRainAmount);

            PropertyField snowAccumulationSpeed = new PropertyField();
            snowAccumulationSpeed.BindProperty(serializedObject.FindProperty("snowAccumulationSpeed"));
            container.Add(snowAccumulationSpeed);

            PropertyField maximumSnowAmount = new PropertyField();
            maximumSnowAmount.BindProperty(serializedObject.FindProperty("maximumSnowAmount"));
            container.Add(maximumSnowAmount);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            return container;
        }
    }
}