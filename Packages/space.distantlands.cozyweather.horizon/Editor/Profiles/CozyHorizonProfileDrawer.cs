using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;
using System.Collections.Generic;
using DistantLands.Cozy.Horizon;

namespace DistantLands.Cozy.EditorScripts
{
    [CustomEditor(typeof(CozyHorizonProfile))]
    [CanEditMultipleObjects]
    public class HorizonProfileEditor : Editor
    {

        VisualElement root;

        public override VisualElement CreateInspectorGUI()
        {
            root = new VisualElement();

            ListView listView = new ListView();
            listView.BindProperty(serializedObject.FindProperty("layers"));
            listView.showAlternatingRowBackgrounds = AlternatingRowBackground.All;
            listView.showAddRemoveFooter = true;
            listView.virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight;
            listView.showBorder = true;
            listView.reorderable = true;
            listView.reorderMode = ListViewReorderMode.Animated;
            listView.headerTitle = "Layers";
            listView.showBoundCollectionSize = true;
            listView.showFoldoutHeader = true;

            root.Add(listView);

            return root;
        }

    }
}