using DistantLands.Cozy.Blocks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomPropertyDrawer(typeof(BlocksConditions))]
    public class BlocksConditionsDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement container = new VisualElement();
            container.Add(new PropertyField(property.FindPropertyRelative("blocksProfile")));
            container.Add(new PropertyField(property.FindPropertyRelative("chance")));
            return container;
        }
    }
}
