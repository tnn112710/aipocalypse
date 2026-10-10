using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    /// <summary>A renderer-owned lighting environment with the same lifetime as its preview specimen.</summary>
    internal interface ITextStudioStylePreviewEnvironment : IDisposable
    {
        void Render(Camera camera, Bounds subjectBounds);
    }

    /// <summary>One isolated rendered specimen, shared by live selection and generated thumbnails.</summary>
    internal sealed class TextStudioStylePreviewSession : IDisposable
    {
        Scene _scene;
        Camera _camera;
        TextMeshPro _text;
        TextStudioText _hub;
        RenderTexture _texture;
        readonly bool _threeD;
        readonly Action _refresh;
        readonly ITextStudioStylePreviewEnvironment _environment;
        readonly System.Collections.Generic.List<MeshRenderer> _renderers = new System.Collections.Generic.List<MeshRenderer>();
        internal Texture Texture => _texture;

        internal TextStudioStylePreviewSession(string sample, TMP_FontAsset font, int width, int height, bool threeD, Action<TextMeshPro, TextStudioText> configure, Action refresh = null, ITextStudioStylePreviewEnvironment environment = null)
        {
            _threeD = threeD;
            _refresh = refresh;
            _environment = environment;
            try
            {
                _scene = EditorSceneManager.NewPreviewScene();
                if (!_scene.IsValid() || EditorSceneManager.GetSceneCullingMask(_scene) == 0)
                    throw new InvalidOperationException("Close unused preview windows, then try again.");
                GameObject cameraObject = NewObject("Style Preview Camera", typeof(Camera));
                _camera = cameraObject.GetComponent<Camera>();
                _camera.scene = _scene;
                _camera.enabled = false;
                _camera.orthographic = true;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = new Color(.12f, .13f, .15f);
                _camera.nearClipPlane = .01f;
                _camera.farClipPlane = 1000;
                _camera.transform.rotation = threeD ? Quaternion.Euler(10f, -18f, 0f) : Quaternion.identity;
                _texture = new RenderTexture(width, height, 24) { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 4 };
                _texture.Create();
                _camera.targetTexture = _texture;
                GameObject textObject = NewObject("Style Preview Text");
                textObject.SetActive(false);
                _text = textObject.AddComponent<TextMeshPro>();
                _text.font = font != null ? font : TMP_Settings.defaultFontAsset;
                if (_text.font == null) throw new InvalidOperationException("Assign a default TextMeshPro font to preview this style.");
                _text.fontSize = 6;
                _text.alignment = TextAlignmentOptions.Center;
                _text.rectTransform.sizeDelta = new Vector2(36, 18);
                _text.text = sample;
                _hub = textObject.AddComponent<TextStudioText>();
                _hub.SetAuthoringMode(TextStudioAuthoringMode.Preview);
                _hub.SetRevealMode(RevealMode.Instant);
                configure(_text, _hub);
                textObject.SetActive(true);
                _hub.SetText(sample);
                _hub.SkipReveal();
                _hub.DiscoverRenderersAndProviders();
                Render(0f);
            }
            catch { Dispose(); throw; }
        }

        GameObject NewObject(string name, params Type[] types)
        {
            GameObject result = EditorUtility.CreateGameObjectWithHideFlags(name, HideFlags.HideAndDontSave, types);
            SceneManager.MoveGameObjectToScene(result, _scene);
            return result;
        }

        internal void SetText(string text) { if (_hub != null) _hub.SetText(text); }

        internal void Render(float time)
        {
            if (_hub == null || _camera == null) return;
            _hub.ForceMeshRefresh(time);
            _refresh?.Invoke();
            Bounds bounds = _text.bounds;
            if (_threeD)
            {
                _renderers.Clear();
                _hub.GetComponentsInChildren(false, _renderers);
                bool found = false;
                foreach (MeshRenderer renderer in _renderers)
                {
                    if (!renderer.enabled || renderer.gameObject == _text.gameObject) continue;
                    if (!found) { bounds = renderer.bounds; found = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
            }
            _camera.aspect = (float)_texture.width / _texture.height;
            float radius = Mathf.Max(.5f, bounds.extents.magnitude);
            _camera.transform.position = bounds.center - _camera.transform.forward * Mathf.Max(20f, radius * 4f);
            Quaternion inverseRotation = Quaternion.Inverse(_camera.transform.rotation);
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            float halfWidth = 0f;
            float halfHeight = 0f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? minimum.x : maximum.x,
                    (corner & 2) == 0 ? minimum.y : maximum.y,
                    (corner & 4) == 0 ? minimum.z : maximum.z);
                Vector3 projected = inverseRotation * (point - bounds.center);
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(projected.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(projected.y));
            }
            _camera.orthographicSize = Mathf.Max(.05f,
                Mathf.Max(halfHeight, halfWidth / _camera.aspect) * (_threeD ? 1.18f : 1.4f));
            RenderTexture previous = RenderTexture.active;
            try
            {
                if (_environment != null) _environment.Render(_camera, bounds);
                else _camera.Render();
            }
            finally { RenderTexture.active = previous; }
        }

        internal Texture2D Capture()
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _texture;
                Texture2D image = new Texture2D(_texture.width, _texture.height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                image.Apply(false, false);
                return image;
            }
            finally { RenderTexture.active = previous; }
        }

        public void Dispose()
        {
            if (_camera != null) _camera.targetTexture = null;
            try { _environment?.Dispose(); }
            finally
            {
                if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
                _scene = default;
                if (_texture != null) { _texture.Release(); Object.DestroyImmediate(_texture); }
                _texture = null; _hub = null; _text = null; _camera = null;
                _renderers.Clear();
            }
        }
    }
}
