//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(MultiFX))]
    [CanEditMultipleObjects]
    public class MultiFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            PropertyField filterSaturation = new PropertyField();
            filterSaturation.BindProperty(serializedObject.FindProperty("multiFX"));
            container.Add(filterSaturation);

            PropertyField transitionTimeModifier = new PropertyField();
            transitionTimeModifier.BindProperty(serializedObject.FindProperty("transitionTimeModifier"));
            container.Add(transitionTimeModifier);
            
            
            return container;
        }
    }
}