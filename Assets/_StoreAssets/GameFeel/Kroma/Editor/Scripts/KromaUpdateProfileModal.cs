using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace FayLabs.Kroma.Editor
{
    /// <summary>
    /// UI Toolkit modal dialog warning users before updating a source KromaProfile asset from the scene.
    /// </summary>
    public class KromaUpdateProfileModal : EditorWindow
    {
        private const string PREF_SUPPRESS_KEY = "Kroma_SuppressUpdateProfileWarning";
        private const string UXML_PATH = "Assets/FayLabs/Kroma/Editor/UI/Views/KromaUpdateProfileModal.uxml";
        private const string THEME_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Theme/KromaTheme.uss";
        private const string COMMON_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Components/KromaCommon.uss";
        private const string MODAL_USS_PATH = "Assets/FayLabs/Kroma/Editor/UI/Components/KromaModal.uss";

        private KromaProfile _targetProfile;
        private Action _onConfirm;
        private bool _doNotShowAgain = false;

        /// <summary>
        /// Prompts user confirmation before executing onConfirm, unless suppressed by user preference.
        /// </summary>
        public static void PromptConfirmation(KromaProfile profile, Action onConfirm)
        {
            if (profile == null) return;

            if (EditorPrefs.GetBool(PREF_SUPPRESS_KEY, false))
            {
                onConfirm?.Invoke();
                return;
            }

            KromaUpdateProfileModal window = CreateInstance<KromaUpdateProfileModal>();
            window.titleContent = new GUIContent("Update Profile Warning");
            window._targetProfile = profile;
            window._onConfirm = onConfirm;

            Rect main = EditorGUIUtility.GetMainWindowPosition();
            float initialWidth = 480;
            float initialHeight = 230;
            float x = main.x + (main.width - initialWidth) * 0.5f;
            float y = main.y + (main.height - initialHeight) * 0.5f;

            window.position = new Rect(x, y, initialWidth, initialHeight);
            window.minSize = new Vector2(initialWidth, 180);
            window.maxSize = new Vector2(initialWidth, 600);
            window.ShowUtility();
        }

        /// <summary>
        /// Resets the preference so the warning modal will display again.
        /// </summary>
        public static void ResetWarningPreference()
        {
            EditorPrefs.DeleteKey(PREF_SUPPRESS_KEY);
        }

        public void CreateGUI()
        {
            // Explicitly attach stylesheets to guarantee loading in EditorWindow
            LoadStyleSheet(THEME_USS_PATH);
            LoadStyleSheet(COMMON_USS_PATH);
            LoadStyleSheet(MODAL_USS_PATH);

            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UXML_PATH);
            if (uxml == null)
            {
                string[] guids = AssetDatabase.FindAssets("KromaUpdateProfileModal t:VisualTreeAsset");
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

            // Set Body Text
            var bodyLabel = rootVisualElement.Q<Label>("modal-body-label");
            if (bodyLabel != null)
            {
                bodyLabel.enableRichText = true;
                string profileName = _targetProfile != null ? _targetProfile.name : "Active Profile";
                bodyLabel.text = $"Updating \"<color=#ffa83a>{profileName}</color>\" will overwrite the source profile.\n\nAny other scenes in your project referencing this profile will automatically inherit these changes. If you want these changes without altering the original profile, consider cloning the profile first.";
            }

            // Checkbox
            var toggle = rootVisualElement.Q<Toggle>("modal-toggle-suppress");
            if (toggle != null)
            {
                toggle.EnableInClassList("kroma-toggle--checked", toggle.value);
                toggle.RegisterValueChangedCallback(evt =>
                {
                    _doNotShowAgain = evt.newValue;
                    toggle.EnableInClassList("kroma-toggle--checked", evt.newValue);
                });
            }

            // CTAs
            var btnCancel = rootVisualElement.Q<Button>("btn-cancel");
            if (btnCancel != null)
            {
                btnCancel.clicked += Close;
            }

            var btnClone = rootVisualElement.Q<Button>("btn-clone");
            if (btnClone != null)
            {
                btnClone.clicked += () =>
                {
                    KromaProfile prof = _targetProfile;
                    Close();
                    if (prof != null)
                    {
                        KromaCloneWindow.ShowPrompt(prof);
                    }
                };
            }

            var btnConfirm = rootVisualElement.Q<Button>("btn-confirm");
            if (btnConfirm != null)
            {
                btnConfirm.clicked += () =>
                {
                    if (_doNotShowAgain)
                    {
                        EditorPrefs.SetBool(PREF_SUPPRESS_KEY, true);
                    }
                    Action callback = _onConfirm;
                    Close();
                    callback?.Invoke();
                };
            }

            // Auto-fit window height dynamically to rendered content
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            var modalRoot = rootVisualElement.Q(className: "kroma-modal-root");
            if (modalRoot != null && modalRoot.layout.height > 50)
            {
                rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                float naturalHeight = Mathf.Ceil(modalRoot.layout.height);
                minSize = new Vector2(480, naturalHeight);
                maxSize = new Vector2(480, naturalHeight);

                Rect main = EditorGUIUtility.GetMainWindowPosition();
                float x = main.x + (main.width - 480) * 0.5f;
                float y = main.y + (main.height - naturalHeight) * 0.5f;
                position = new Rect(x, y, 480, naturalHeight);
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
