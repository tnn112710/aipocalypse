//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(VisualEffectGraphFX))]
    [CanEditMultipleObjects]
    public class VFXGraphFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField prefab = new PropertyField();
            prefab.BindProperty(serializedObject.FindProperty("prefab"));
            container.Add(prefab);

            LayerField layerField = new LayerField();
            layerField.label = "Layer";
            layerField.AddToClassList("unity-base-field__aligned");
            layerField.BindProperty(serializedObject.FindProperty("layer"));
            container.Add(layerField);

            PropertyField targetWeight = new PropertyField();
            targetWeight.BindProperty(serializedObject.FindProperty("targetWeight"));
            container.Add(targetWeight);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            return container;
        }
    }
}