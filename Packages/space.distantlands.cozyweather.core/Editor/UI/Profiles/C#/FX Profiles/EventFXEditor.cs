//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(EventFX))]
    [CanEditMultipleObjects]
    public class EventFXEditor : FXProfileEditor
    {
        public override VisualElement RenderInWindow()
        {
            VisualElement container = new VisualElement();

            HelpBox helpBox = new HelpBox("Configure events in the Events module", HelpBoxMessageType.Info);
            container.Add(helpBox);            

            return container;
        }
    }
}