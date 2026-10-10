using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextStudio
{
    internal sealed partial class TextStudio2DHeroCompositor
    {
        Matrix4x4 _captureProjection = Matrix4x4.identity;
        TMP_Text _viewText;
        /// <summary>Engine-published glyphs the output draws in place of a TMP text, with their host transform.</summary>
        TextStudio2DExternalGlyphSource _viewExternal;
        TextStudio2DHeroProgram _viewProgram;
        TextStudioLensMagnifierDescriptor _viewLens;
        TextStudio2DQualityTier _viewQuality;
        float _viewDensity;
        bool _projectionCallbacks;
        bool _renderingProjection;

        Transform ViewTransform => _viewText != null ? _viewText.transform : _viewExternal?.Host;
        bool ViewIsCanvas => _viewText is TextMeshProUGUI || _viewExternal != null && _viewExternal.IsCanvas;
        Canvas ViewCanvas => _viewText is TextMeshProUGUI ui ? ui.canvas
            : ViewIsCanvas && _viewExternal.Host != null ? _viewExternal.Host.GetComponentInParent<Canvas>() : null;

        void SynchronizeProjectionCallbacks()
        {
            bool spatial = false;
            if (_appearancePlan != null)
            {
                foreach (OccurrenceState state in _activeAppearanceStates)
                    for (int i = 0; i < state.Batches.Count && !spatial; i++) spatial = state.Batches[i].HasSpatialVertices();
            }
            else
                for (int i = 0; i < _occurrenceState.Batches.Count && !spatial; i++) spatial = _occurrenceState.Batches[i].HasSpatialVertices();
            if (spatial == _projectionCallbacks) return;
            if (spatial)
            {
                Camera.onPreCull += BeforeProjectionCamera;
                RenderPipelineManager.beginCameraRendering += BeforeProjectionSrpCamera;
                _projectionCallbacks = true;
            }
            else
            {
                ReleaseProjectionCallbacks();
                _captureProjection = Matrix4x4.identity;
            }
        }

        void ReleaseProjectionCallbacks()
        {
            if (!_projectionCallbacks) return;
            Camera.onPreCull -= BeforeProjectionCamera;
            RenderPipelineManager.beginCameraRendering -= BeforeProjectionSrpCamera;
            _projectionCallbacks = false;
        }

        void BeforeProjectionSrpCamera(ScriptableRenderContext context, Camera camera) => BeforeProjectionCamera(camera);

        void BeforeProjectionCamera(Camera camera)
        {
            Transform view = ViewTransform;
            if (_disposed || _renderingProjection || !HasOutput || camera == null || view == null ||
                !_hub.isActiveAndEnabled) return;
            Canvas canvas = ViewCanvas;
            int layer = canvas != null ? canvas.rootCanvas.gameObject.layer : _hub.gameObject.layer;
            if ((camera.cullingMask & (1 << layer)) == 0) return;
            Matrix4x4 projection = ResolveCaptureProjection(camera, view, canvas);
            if (_captureProjection == projection) return;
            _captureProjection = projection;
            _renderingProjection = true;
            try
            {
                // Retained meshes and immutable composition settings are owned here. Never
                // retain a borrowed TextStudioFrame across pipeline ticks for camera rendering.
                if (_appearancePlan != null) RenderCachedAppearance(true);
                else RenderCachedGeometry(true);
                // The shared stream may already have been submitted for this camera; this camera-time render must reach it.
                TextStudio2DHeroSubmission.Flush();
            }
            finally { _renderingProjection = false; }
        }

        internal static Matrix4x4 ResolveCaptureProjection(Camera camera, TMP_Text text) =>
            ResolveCaptureProjection(camera, text.transform, text is TextMeshProUGUI ui ? ui.canvas : null);

        static Matrix4x4 ResolveCaptureProjection(Camera camera, Transform source, Canvas canvas)
        {
            Matrix4x4 projection = Matrix4x4.identity;
            if (canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay &&
                camera.cameraType != CameraType.SceneView) return projection;
            if (camera.orthographic)
            {
                Vector3 direction = source.InverseTransformVector(camera.transform.forward);
                if (Mathf.Abs(direction.z) < .00001f) return projection;
                projection.m02 = -direction.x / direction.z;
                projection.m12 = -direction.y / direction.z;
            }
            else
            {
                Vector3 position = source.InverseTransformPoint(camera.transform.position);
                if (Mathf.Abs(position.z) < .00001f) return projection;
                projection.m02 = -position.x / position.z;
                projection.m12 = -position.y / position.z;
                projection.m32 = -1f / position.z;
            }
            // Intersect the viewing ray with the output plane. Homogeneous W retains
            // perspective interpolation; signed glyph depth therefore changes the silhouette.
            projection.m22 = 0f;
            return projection;
        }

        Rect ResolveProjectedGeometryBounds()
        {
            if (_captureProjection == Matrix4x4.identity) return _occurrenceState.CachedGeometryBounds;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < _occurrenceState.Batches.Count; i++)
                _occurrenceState.Batches[i].IncludeProjectedBounds(_captureProjection, ref minimum, ref maximum);
            _occurrenceState.SurfaceGeometry.IncludeHeroBounds(ref minimum, ref maximum, _occurrenceState.OperationHasSelection, _captureProjection);
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }
    }
}
