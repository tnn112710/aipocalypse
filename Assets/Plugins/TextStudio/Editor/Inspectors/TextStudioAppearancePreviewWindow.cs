using System;
using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>Explicit, transient preview. It never changes an authored enabled or Solo flag.</summary>
    internal sealed class TextStudioAppearancePreviewWindow : EditorWindow
    {
        Scene _scene;
        Camera _camera;
        TextMeshPro _text;
        TextStudioText _hub;
        RenderTexture _texture;
        Image _image;
        Label _status;
        TextStudio2DStyleSettings _settings;
        string _id;

        internal static void Open(TextStudio2DStyleSettings settings, string id)
        {
            TextStudioAppearancePreviewWindow window = CreateInstance<TextStudioAppearancePreviewWindow>();
            window._settings = settings.Clone();
            window._id = id;
            window.titleContent = new GUIContent("Appearance Preview");
            window.minSize = new Vector2(360, 250);
            window.ShowUtility();
        }

        void CreateGUI()
        {
            CommonUITK.ApplyWindowLayout(rootVisualElement);
            TextField sample = new TextField("Sample Text") { value = "A little emphasis", tooltip = "Text used only by this preview. Your scene and appearance asset stay unchanged." };
            _image = new Image { scaleMode = ScaleMode.ScaleToFit };
            _image.style.flexGrow = 1;
            _image.style.minHeight = 180;
            _status = TextStudioInspectorElements.CreateMutedText("Previewing $" + _id + ". Changes here are temporary.");
            rootVisualElement.Add(sample); rootVisualElement.Add(_image); rootVisualElement.Add(_status);
            sample.RegisterValueChangedCallback(evt => { if (_hub != null) _hub.SetText(evt.newValue); });
            try { Initialize(sample.value); }
            catch (Exception error) { _status.text = "Preview unavailable: " + error.Message; Release(); }
            rootVisualElement.schedule.Execute(Render).Every(33);
            AssemblyReloadEvents.beforeAssemblyReload += Release;
        }

        void Initialize(string sample)
        {
            if (!TextStudioAppearanceDefinitions.Resolve(_settings).TryGet(_id, out TextStudioAppearanceDefinition definition) || !definition.IsValid) throw new InvalidOperationException("Repair this definition before previewing it.");
            _scene = EditorSceneManager.NewPreviewScene();
            GameObject cameraObject = EditorUtility.CreateGameObjectWithHideFlags("Appearance Preview Camera", HideFlags.HideAndDontSave, typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraObject, _scene);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.scene = _scene;
            _camera.orthographic = true;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = EditorGUIUtility.isProSkin ? new Color(.12f, .13f, .15f) : new Color(.84f, .85f, .87f);
            _camera.transform.position = new Vector3(0, 0, -20);
            _camera.nearClipPlane = .1f; _camera.farClipPlane = 100;
            _camera.enabled = false;
            _texture = new RenderTexture(1024, 512, 24) { hideFlags = HideFlags.HideAndDontSave };
            _texture.Create(); _camera.targetTexture = _texture;
            GameObject textObject = EditorUtility.CreateGameObjectWithHideFlags("Appearance Preview Text", HideFlags.HideAndDontSave, typeof(TextMeshPro));
            SceneManager.MoveGameObjectToScene(textObject, _scene);
            _text = textObject.GetComponent<TextMeshPro>();
            _text.font = TMP_Settings.defaultFontAsset;
            if (_text.font == null) throw new InvalidOperationException("Assign a default TextMeshPro font in project settings.");
            _text.fontSize = 6;
            _text.alignment = TextAlignmentOptions.Center;
            _text.rectTransform.sizeDelta = new Vector2(32, 16);
            _text.text = sample;
            _hub = textObject.AddComponent<TextStudioText>();
            _hub.SetAuthoringMode(TextStudioAuthoringMode.Preview);
            _hub.ApplyAppearanceSettings(definition.Settings);
            _hub.SetText(sample);
            _image.image = _texture;
            Render();
        }

        void Render()
        {
            if (_hub == null || _camera == null) return;
            _hub.ForceMeshRefresh((float)EditorApplication.timeSinceStartup);
            _text.ForceMeshUpdate();
            Bounds bounds = _text.textBounds;
            _camera.aspect = 2;
            _camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, -20);
            _camera.orthographicSize = Mathf.Max(1, Mathf.Max(bounds.extents.y, bounds.extents.x / 2) * 1.4f);
            _camera.Render();
        }

        void OnDisable() { AssemblyReloadEvents.beforeAssemblyReload -= Release; Release(); }
        void Release()
        {
            if (_image != null) _image.image = null;
            if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
            _scene = default;
            if (_texture != null) { _texture.Release(); Object.DestroyImmediate(_texture); }
            _texture = null; _hub = null; _camera = null; _text = null;
        }
    }
}
