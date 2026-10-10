using System;
using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
    internal sealed class TextStudioPreviewFrameRenderer :
        ITextStudioPreviewExportFrameRenderer,
        ITextStudioInteractiveSpatialPreviewRenderer
    {
        const float FramingPadding = 1.12f;
        const float MinimumOrthographicSize = 0.5f;
        const int MinimumFramingSamples = 60;
        const int MaximumFramingSamples = 360;

        readonly TextStudioPreviewExportSnapshot _snapshot;
        readonly TextStudioTMPFrameApplier _frameApplier = new TextStudioTMPFrameApplier();
        readonly TextStudioTMPMaterialEffectAdapter _materialEffectAdapter =
            new TextStudioTMPMaterialEffectAdapter();
        readonly Scene _previewScene;
        readonly GameObject _cameraObject;
        readonly GameObject _textObject;
        readonly Camera _camera;
        readonly TextMeshPro _text;
        readonly RenderTexture _target;
        readonly Texture2D _readback;
        readonly TextStudioPreviewSpatialEvaluationContext _spatialContext;
        bool _previewTargetEnabled;
        TextStudioPreviewSpatialTarget _previewTarget;

        internal TextStudioPreviewFrameRenderer(
            TextStudioPreviewExportSnapshot snapshot,
            int width,
            int height,
            bool transparentBackground = false)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            Width = Mathf.Max(2, width);
            Height = Mathf.Max(2, height);
            try
            {
                _previewScene = EditorSceneManager.NewPreviewScene();
                if (EditorSceneManager.GetSceneCullingMask(_previewScene) == 0)
                {
                    throw new InvalidOperationException(
                        "Unity could not allocate an isolated preview scene. Close unused preview windows or restart the Editor, then try again.");
                }
                _cameraObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "Text Studio Export Camera",
                    HideFlags.HideAndDontSave,
                    typeof(Camera));
                _textObject = EditorUtility.CreateGameObjectWithHideFlags(
                    "Text Studio Export Text",
                    HideFlags.HideAndDontSave,
                    typeof(TextMeshPro));
                SceneManager.MoveGameObjectToScene(_cameraObject, _previewScene);
                SceneManager.MoveGameObjectToScene(_textObject, _previewScene);

                _camera = _cameraObject.GetComponent<Camera>();
                _camera.scene = _previewScene;
                _camera.clearFlags = CameraClearFlags.SolidColor;
                _camera.backgroundColor = transparentBackground
                    ? new Color(0f, 0f, 0f, 0f)
                    : _snapshot.BackgroundColor;
                _camera.orthographic = true;
                _camera.nearClipPlane = 0.1f;
                _camera.farClipPlane = 100f;
                _camera.transform.position = new Vector3(0f, 0f, -20f);
                _camera.transform.rotation = Quaternion.identity;
                _camera.allowHDR = false;
                _camera.allowMSAA = false;

                _target = new RenderTexture(
                    Width,
                    Height,
                    24,
                    RenderTextureFormat.ARGB32,
                    RenderTextureReadWrite.sRGB)
                {
                    name = "Text Studio Export Frame",
                    antiAliasing = 1,
                    hideFlags = HideFlags.HideAndDontSave,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                _target.Create();
                _camera.targetTexture = _target;

                _readback = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false)
                {
                    name = "Text Studio Export Readback",
                    hideFlags = HideFlags.HideAndDontSave
                };

                _text = _textObject.GetComponent<TextMeshPro>();
                TMP_FontAsset font = TMP_Settings.defaultFontAsset;
                if (font == null)
                    font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
                if (font == null)
                {
                    throw new InvalidOperationException(
                        "No TextMesh Pro default font is configured. Assign a default font asset in TMP Settings before exporting.");
                }

                _text.font = font;
                _text.text = !string.IsNullOrEmpty(snapshot.Plan.DisplayText)
                    ? snapshot.Plan.DisplayText
                    : snapshot.Plan.ResolvedText;
                _text.color = snapshot.BaseColor;
                _text.fontStyle = FontStyles.Bold;
                _text.alignment = TextAlignmentOptions.Center;
                _text.richText = true;
                _text.enableAutoSizing = true;
                _text.fontSizeMin = 2f;
                _text.fontSizeMax = 5f;
                _text.rectTransform.sizeDelta = new Vector2(24f, 9f);
                UnityUtils.SetTextMeshProWordWrapping(_text, false);
                _materialEffectAdapter.Prepare(_text, snapshot.Plan);
                _text.ForceMeshUpdate(true, true);

                Bounds bounds = _text.textBounds;
                _text.transform.position = new Vector3(-bounds.center.x, -bounds.center.y, 0f);
                if ((snapshot.Plan.SourceContextRequirements &
                     TextStudioSourceContextRequirements.SourceTextNormalizedPosition) != 0 &&
                    TryResolveSourceTextBounds(_text.textInfo, bounds, out Vector4 sourceTextBounds))
                {
                    _frameApplier.SetSourceTextBounds(sourceTextBounds);
                }
                _frameApplier.CacheSourceData(_text.textInfo, snapshot.Plan);
                bool materialPayloadReady = _materialEffectAdapter.HasPreparedState &&
                    _frameApplier.InitializeMaterialPayloadStreams(_text);
                _materialEffectAdapter.SetPayloadEnabled(materialPayloadReady);
                _spatialContext = TextStudioPreviewTMPSpatialContextBuilder.Build(
                    _text,
                    snapshot.Plan);
                Bounds animationBounds = MeasureAnimationBounds(bounds);
                float aspect = Width / (float)Height;
                _camera.transform.position = new Vector3(
                    animationBounds.center.x,
                    animationBounds.center.y,
                    -20f);
                _camera.orthographicSize = Mathf.Max(
                    MinimumOrthographicSize,
                    animationBounds.extents.y * FramingPadding,
                    animationBounds.extents.x / Mathf.Max(0.01f, aspect) * FramingPadding);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        static bool TryResolveSourceTextBounds(
            TMP_TextInfo textInfo,
            Bounds fallback,
            out Vector4 bounds)
        {
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool hasVisibleGeometry = false;
            if (textInfo?.characterInfo != null)
            {
                int characterCount = Mathf.Min(
                    textInfo.characterCount,
                    textInfo.characterInfo.Length);
                for (int i = 0; i < characterCount; i++)
                {
                    TMP_CharacterInfo character = textInfo.characterInfo[i];
                    if (!character.isVisible)
                        continue;
                    minimum = Vector2.Min(minimum, character.bottomLeft);
                    maximum = Vector2.Max(maximum, character.topRight);
                    hasVisibleGeometry = true;
                }
            }

            if (!hasVisibleGeometry)
            {
                minimum = fallback.min;
                maximum = fallback.max;
            }

            if (!float.IsFinite(minimum.x) ||
                !float.IsFinite(minimum.y) ||
                !float.IsFinite(maximum.x) ||
                !float.IsFinite(maximum.y))
            {
                bounds = Vector4.zero;
                return false;
            }

            bounds = new Vector4(minimum.x, minimum.y, maximum.x, maximum.y);
            return true;
        }

        internal int Width { get; }

        internal int Height { get; }

        public Texture TargetTexture => _target;

        internal TextStudioPreviewSpatialEvaluationContext SpatialContext =>
            _spatialContext;

        public void RenderToTarget(float lifecycleTime)
        {
            RenderToTarget(lifecycleTime, lifecycleTime);
        }

        public void RenderToTarget(float continuousTime, float lifecycleTime)
        {
            ApplySnapshotFrame(continuousTime, lifecycleTime);
            _camera.Render();
        }

        public Texture2D Render(float lifecycleTime)
        {
            RenderToTarget(lifecycleTime);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = _target;
                _readback.ReadPixels(new Rect(0f, 0f, Width, Height), 0, 0, false);
                _readback.Apply(false, false);
            }
            finally
            {
                RenderTexture.active = previous;
            }

            return _readback;
        }

        public void ResetEvaluation()
        {
            _snapshot.ResetEvaluation();
        }

        public bool TryResolveTarget(
            Vector2 normalizedViewportPosition,
            out TextStudioPreviewSpatialTarget target)
        {
            target = default;
            if (_camera == null || _text == null || !_spatialContext.IsCreated)
                return false;

            Ray ray = _camera.ViewportPointToRay(new Vector3(
                normalizedViewportPosition.x,
                normalizedViewportPosition.y,
                0f));
            Plane textPlane = new Plane(
                _text.transform.forward,
                _text.transform.position);
            if (!textPlane.Raycast(ray, out float distance) ||
                !float.IsFinite(distance))
            {
                return false;
            }

            Vector3 worldPosition = ray.GetPoint(distance);
            Vector3 localPosition = _text.transform.InverseTransformPoint(worldPosition);
            target = new TextStudioPreviewSpatialTarget(
                localPosition * _spatialContext.InteractiveLocalSpatialScale,
                _spatialContext.NormalizeLocalPosition(localPosition),
                worldPosition);
            return true;
        }

        public bool TryProjectPoint(
            TextStudioInfluenceFieldSpace space,
            Vector3 position,
            out Vector2 normalizedViewportPosition)
        {
            normalizedViewportPosition = default;
            if (_camera == null || !_spatialContext.IsCreated)
                return false;

            Vector3 worldPosition = _spatialContext.ResolveWorldPosition(
                space,
                position);
            Vector3 viewport = _camera.WorldToViewportPoint(worldPosition);
            if (!float.IsFinite(viewport.x) ||
                !float.IsFinite(viewport.y) ||
                !float.IsFinite(viewport.z) ||
                viewport.z <= 0f)
            {
                return false;
            }

            normalizedViewportPosition = new Vector2(viewport.x, viewport.y);
            return true;
        }

        public void SetPreviewTarget(
            bool enabled,
            in TextStudioPreviewSpatialTarget target)
        {
            _previewTargetEnabled = enabled;
            _previewTarget = target;
        }

        Bounds MeasureAnimationBounds(Bounds fallbackTextBounds)
        {
            int sampleCount = Mathf.Clamp(
                Mathf.CeilToInt(_snapshot.SourceDuration * 60f),
                MinimumFramingSamples,
                MaximumFramingSamples);
            Bounds animationBounds = default;
            bool hasBounds = false;
            for (int frameIndex = 0; frameIndex < sampleCount; frameIndex++)
            {
                float sourceTime = TextStudioPreviewExportPreset.GetSourceTime(
                    frameIndex,
                    sampleCount,
                    _snapshot.SourceDuration);
                ApplySnapshotFrame(sourceTime, sourceTime);
                if (_text.mesh == null)
                    continue;

                Bounds frameBounds = _text.mesh.bounds;
                frameBounds.center += _text.transform.position;
                if (!hasBounds)
                {
                    animationBounds = frameBounds;
                    hasBounds = true;
                }
                else
                {
                    animationBounds.Encapsulate(frameBounds);
                }
            }

            _snapshot.ResetEvaluation();
            if (hasBounds && animationBounds.size.sqrMagnitude > 0.0001f)
                return animationBounds;

            fallbackTextBounds.center += _text.transform.position;
            return fallbackTextBounds;
        }

        void ApplySnapshotFrame(float continuousTime, float lifecycleTime)
        {
            TextStudioPreviewSpatialEvaluationContext spatialContext =
                _previewTargetEnabled
                    ? _spatialContext.WithTarget(in _previewTarget)
                    : _spatialContext;
            EvaluatedGlyphState[] states = _snapshot.Evaluate(
                continuousTime,
                lifecycleTime,
                in spatialContext);
            TextStudioFrame frame = new TextStudioFrame(
                _snapshot.Plan,
                null,
                0,
                null,
                PlacementMode.Linear,
                states,
                _snapshot.Plan.VisibleGlyphCount,
                _snapshot.Dirty,
                1f,
                DirtyFlags.Animation,
                deltaTime: 0f,
                time: lifecycleTime,
                renderProperties: _snapshot.RenderProperties);
            _frameApplier.ApplyFrame(
                _text,
                in frame,
                null,
                null,
                true,
                1f,
                false);
            if (_text.mesh != null)
                _text.mesh.RecalculateBounds();
        }

        public void Dispose()
        {
            _frameApplier.Clear();
            _materialEffectAdapter.Restore();
            if (_camera != null)
                _camera.targetTexture = null;
            if (_target != null)
            {
                _target.Release();
                UnityEngine.Object.DestroyImmediate(_target);
            }
            if (_readback != null)
                UnityEngine.Object.DestroyImmediate(_readback);
            if (_textObject != null)
                UnityEngine.Object.DestroyImmediate(_textObject);
            if (_cameraObject != null)
                UnityEngine.Object.DestroyImmediate(_cameraObject);
            if (_previewScene.IsValid())
                EditorSceneManager.ClosePreviewScene(_previewScene);
        }
    }
}
