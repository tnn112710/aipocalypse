using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    [CustomEditor(typeof(TextStudio2DStyleAsset))]
    [CanEditMultipleObjects]
    internal sealed class TextStudio2DStyleAssetEditor : UnityEditor.Editor
    {
        TextStudio2DStyleEditor _styleEditor;
        Func<string, string, string, string, bool> _deleteConfirmationHandler;

        internal Func<string, string, string, string, bool> DeleteConfirmationHandler
        {
            get => _deleteConfirmationHandler;
            set
            {
                _deleteConfirmationHandler = value;
                if (_styleEditor != null)
                    _styleEditor.DeleteConfirmationHandler = value;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            _styleEditor?.Dispose();
            _styleEditor = new TextStudio2DStyleEditor(
                serializedObject, string.Empty,
                () => TextStudio2DStyleCompiler.Compile(target as TextStudio2DStyleAsset))
            {
                DeleteConfirmationHandler = _deleteConfirmationHandler
            };
            VisualElement root = _styleEditor.Build();
            if (!serializedObject.isEditingMultipleObjects)
                root.Insert(0, TextStudioAppearanceAssetIdentity.Create((TextStudio2DStyleAsset)target));
            return root;
        }

        void OnDisable()
        {
            _styleEditor?.Dispose();
        }
    }
}
