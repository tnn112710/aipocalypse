//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using UnityEditor;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    public class FXProfileEditor : Editor
    {

        public override VisualElement CreateInspectorGUI()
        {
            return RenderInWindow();
        }

        public virtual VisualElement RenderInWindow()
        {
            return new VisualElement();
        }

    }
}