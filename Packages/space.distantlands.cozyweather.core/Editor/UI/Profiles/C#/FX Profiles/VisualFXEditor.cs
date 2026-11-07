//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(PostProcessingFX))]
    [CanEditMultipleObjects]
    public class VisualFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField effectSettings = new PropertyField();
            effectSettings.BindProperty(serializedObject.FindProperty("effectSettings"));
            container.Add(effectSettings);

            LayerField layerField = new LayerField();
            layerField.AddToClassList("unity-base-field__aligned");
            layerField.label = "Layer";
            layerField.BindProperty(serializedObject.FindProperty("layer"));
            container.Add(layerField);

            PropertyField priority = new PropertyField();
            priority.BindProperty(serializedObject.FindProperty("priority"));
            container.Add(priority);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            return container;
        }
    }
}