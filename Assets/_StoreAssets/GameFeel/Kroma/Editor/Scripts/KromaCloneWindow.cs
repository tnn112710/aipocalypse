using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace FayLabs.Kroma.Editor
{
    public class KromaCloneWindow : EditorWindow
    {
        private const string UXML_PATH = "Assets/FayLabs/Kroma/Editor/UI/Views/KromaCloneModal.uxml";
        private const string THEME_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Theme/KromaTheme.uss";
        private const string COMMON_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Components/KromaCommon.uss";
        private const string MODAL_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Components/KromaModal.uss";

        private KromaProfile _sourceProfile;
        private string _newName;
        private TextField _nameField;
        private Button _btnConfirm;

        public static void ShowPrompt(KromaProfile source)
        {
            if (source == null) return;
            
            KromaCloneWindow window = CreateInstance<KromaCloneWindow>();
            window.titleContent = new GUIContent("Clone Profile");
            window._sourceProfile = source;
            window._newName = GenerateCloneName(source);
            
            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float initialWidth = 460;
            float initialHeight = 280;
            float x = main.x + (main.width - initialWidth) * 0.5f;
            float y = main.y + (main.height - initialHeight) * 0.5f;

            window.position = new Rect(x, y, initialWidth, initialHeight);
            window.minSize = new Vector2(initialWidth, 180);
            window.maxSize = new Vector2(initialWidth, 600);
            window.ShowUtility();
        }

        private static string GenerateCloneName(KromaProfile source)
        {
            // Priority 1: Use the internal Display Name
            string sourceName = source.profileName;
            
            // Priority 2: Use Asset Name
            if (string.IsNullOrEmpty(sourceName)) sourceName = source.name;
            
            // Clean it: Remove "Profile_00_" prefix if present
            string cleanName = KromaLightingUtils.GetCleanProfileName(sourceName);
            
            return $"{cleanName} (Clone)";
        }

        public void CreateGUI()
        {
            if (_sourceProfile == null)
            {
                Close();
                return;
            }

            LoadStyleSheet(THEME_USS_PATH);
            LoadStyleSheet(COMMON_USS_PATH);
            LoadStyleSheet(MODAL_USS_PATH);

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (uxml == null)
            {
                string[] guids = AssetDatabase.FindAssets("KromaCloneModal t:VisualTreeAsset");
                if (guids.Length > 0)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                    uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                }
            }

            if (uxml != null)
            {
                uxml.CloneTree(rootVisualElement);
            }

            // Body label
            var bodyLabel = rootVisualElement.Q<Label>("modal-body-label");
            if (bodyLabel != null)
            {
                bodyLabel.enableRichText = true;
                string cleanName = KromaLightingUtils.GetCleanProfileName(_sourceProfile.profileName);
                if (string.IsNullOrEmpty(cleanName)) cleanName = KromaLightingUtils.GetCleanProfileName(_sourceProfile.name);
                bodyLabel.text = $"Create an independent duplicate of \"<color=#ffa83a>{cleanName}</color>\". The new profile will be saved to your project library.";
            }

            // Thumbnail & source info
            var thumbImg = rootVisualElement.Q<Image>("clone-source-thumbnail");
            if (thumbImg != null)
            {
                if (_sourceProfile.thumbnail != null)
                {
                    thumbImg.image = _sourceProfile.thumbnail;
                }
                else
                {
                    thumbImg.style.display = DisplayStyle.None;
                }
            }

            var sourceNameLabel = rootVisualElement.Q<Label>("clone-source-name");
            if (sourceNameLabel != null)
            {
                string displayName = _sourceProfile.profileName;
                if (string.IsNullOrEmpty(displayName)) displayName = _sourceProfile.name;
                sourceNameLabel.text = displayName;
            }

            // Text Field
            _nameField = rootVisualElement.Q<TextField>("field-profile-name");
            _btnConfirm = rootVisualElement.Q<Button>("btn-confirm");

            if (_nameField != null)
            {
                _nameField.value = _newName;
                _nameField.RegisterValueChangedCallback(evt =>
                {
                    _newName = evt.newValue;
                    UpdateConfirmState();
                });

                // Auto-focus text field
                rootVisualElement.schedule.Execute(() => _nameField.Focus()).StartingIn(50);
            }

            UpdateConfirmState();

            // Actions
            var btnCancel = rootVisualElement.Q<Button>("btn-cancel");
            if (btnCancel != null)
            {
                btnCancel.clicked += Close;
            }

            if (_btnConfirm != null)
            {
                _btnConfirm.clicked += () =>
                {
                    if (!string.IsNullOrWhiteSpace(_newName))
                    {
                        KromaProfileCreator.CloneProfile(_sourceProfile, _newName.Trim());
                        Close();
                    }
                };
            }

            // Auto-fit window height dynamically
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void UpdateConfirmState()
        {
            if (_btnConfirm != null)
            {
                _btnConfirm.SetEnabled(!string.IsNullOrWhiteSpace(_newName));
            }
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var modalRoot = rootVisualElement.Q(className: "kroma-modal-root");
            if (modalRoot != null && modalRoot.layout.height > 50)
            {
                rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                float naturalHeight = Mathf.Ceil(modalRoot.layout.height);
                minSize = new Vector2(460, naturalHeight);
                maxSize = new Vector2(460, naturalHeight);

                Rect main = EditorGUIUtility.GetMainWindowPosition();
                float x = main.x + (main.width - 460) * 0.5f;
                float y = main.y + (main.height - naturalHeight) * 0.5f;
                position = new Rect(x, y, 460, naturalHeight);
            }
        }

        private void LoadStyleSheet(string path)
        {
            StyleSheet sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            if (sheet != null && !rootVisualElement.styleSheets.Contains(sheet))
            {
                rootVisualElement.styleSheets.Add(sheet);
            }
        }
    }
}
