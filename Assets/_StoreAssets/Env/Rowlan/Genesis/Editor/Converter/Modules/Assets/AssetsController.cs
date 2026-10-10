using UnityEditor;
using UnityEngine;

namespace Rowlan.Genesis.Converter
{
    public class AssetsController
    {
        public enum SelectedAsset
        {
            [InspectorName("Seta DnD Volumes")]
            SetaDndVolumes
        }

        private ConverterModule editor;

        private IAssetConverter setaDndVolumes;

        private SelectedAsset selectedAsset = SelectedAsset.SetaDndVolumes;

        public AssetsController(ConverterModule editor)
        {
            this.editor = editor;
            this.setaDndVolumes = new SetaDndVolumes(editor);
        }


        public void OnInspectorGUI()
        {

            EditorGUILayout.HelpBox("Select the asset you wish to convert", MessageType.None);

            selectedAsset = (SelectedAsset) EditorGUILayout.EnumPopup( "Asset", selectedAsset);

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);

            switch ( selectedAsset)
            {
                case SelectedAsset.SetaDndVolumes:
                    setaDndVolumes.OnInspectorGUI();
                    break;

                default:
                    throw new System.Exception("Unsupported asset: " + selectedAsset);
            }
        }

    }
}