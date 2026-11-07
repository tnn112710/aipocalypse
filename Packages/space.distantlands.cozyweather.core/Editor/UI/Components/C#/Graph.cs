//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using DistantLands.Cozy;
using UnityEngine.UIElements;

namespace DistantLands.Cozy.EditorScripts
{
    [UxmlElement]
    partial class Graph : VisualElement
    {
        public Graph()
        {
            Init();
        }

        public void Init()
        {
            style.display = CozyPreferences.Graphs ? DisplayStyle.Flex : DisplayStyle.None;
        }

    }
}