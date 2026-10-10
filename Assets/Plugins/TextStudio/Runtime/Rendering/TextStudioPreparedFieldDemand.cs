using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TextStudio
{
    internal readonly struct TextStudioAppearanceSourceStatistics
    {
        internal readonly int FontCount;
        internal readonly int PreparedFontCount;
        internal readonly int NativeFallbackFontCount;
        internal readonly int MinimumSelectedTier;
        internal readonly int MaximumSelectedTier;
        internal readonly float MaximumDensity;
        internal readonly float MaximumDistance;

        internal TextStudioAppearanceSourceStatistics(
            int fontCount,
            int preparedFontCount,
            int nativeFallbackFontCount,
            int minimumSelectedTier,
            int maximumSelectedTier,
            float maximumDensity,
            float maximumDistance)
        {
            FontCount = fontCount;
            PreparedFontCount = preparedFontCount;
            NativeFallbackFontCount = nativeFallbackFontCount;
            MinimumSelectedTier = minimumSelectedTier;
            MaximumSelectedTier = maximumSelectedTier;
            MaximumDensity = maximumDensity;
            MaximumDistance = maximumDistance;
        }
    }

    /// <summary>Aggregates source demand across actual font glyphs and active/recent output cameras. Owns no GPU resources.</summary>
    internal sealed class TextStudioPreparedFieldDemand
    {
        internal sealed class Font
        {
            internal readonly ulong Identity;
            internal TextStudioFontAppearanceData Data;
            internal readonly TextStudioPreparedFieldSelection Selection = new TextStudioPreparedFieldSelection();
            internal readonly TextStudioContourPrecision ContourPrecision = new TextStudioContourPrecision();
            internal float Density, MinimumProjected;
            internal bool Used;
            internal Font(ulong identity) { Identity = identity; MinimumProjected = float.PositiveInfinity; }
        }

        readonly Dictionary<ulong, Font> _fonts = new Dictionary<ulong, Font>();
        readonly List<ulong> _unused = new List<ulong>();
        readonly List<TextStudio2DSourceProjection> _projections = new List<TextStudio2DSourceProjection>();
        Camera[] _cameras = Array.Empty<Camera>();
        ulong _projectionFingerprint;
        // Inputs of the last Screen Space Overlay projection; a sleeping overlay text compares these instead of rebuilding it.
        Canvas _overlayRoot;
        Matrix4x4 _overlayModel;
        Matrix4x4 _overlayRootMatrix;
        float _overlayScale;
        TextStudioVisualVersion _measuredVersion;
        int _measuredGlyphCount = -1;
        TextStudio2DAppearanceBinding _measuredBinding;
        int _measuredProgram;
        CompiledTextStudio2DStyle _selectedStyle;
        TextStudioTMPMaterialEffectAdapter _nativeMaterials;
        internal ulong Fingerprint { get; private set; }
        internal ulong ReadinessFingerprint { get; private set; }
        internal ulong GeometryFingerprint { get; private set; }
        internal float MaximumDensity { get; private set; }
        internal float MaximumDistance { get; private set; }
        internal float OutputPixelsPerUnit { get; private set; }
        internal int SelectionUpdateCount { get; private set; }
        internal bool TryGetFont(ulong identity, out Font font) => _fonts.TryGetValue(identity, out font);

        internal void CollectUsedFontCoverage(Dictionary<ulong, byte> coverage)
        {
            foreach (Font font in _fonts.Values)
            {
                if (!font.Used) continue;
                coverage.TryGetValue(font.Identity, out byte value);
                coverage[font.Identity] = (byte)(value | (font.Data != null && font.Selection.Tier >= 0 ? 1 : 2));
            }
        }

        internal TextStudioAppearanceSourceStatistics Statistics
        {
            get
            {
                int prepared = 0;
                int nativeFallback = 0;
                int minimumTier = int.MaxValue;
                int maximumTier = -1;
                foreach (Font font in _fonts.Values)
                {
                    if (!font.Used) continue;
                    if (font.Data != null && font.Selection.Tier >= 0)
                    {
                        prepared++;
                        minimumTier = Mathf.Min(minimumTier, font.Selection.Tier);
                        maximumTier = Mathf.Max(maximumTier, font.Selection.Tier);
                    }
                    else
                    {
                        nativeFallback++;
                    }
                }
                return new TextStudioAppearanceSourceStatistics(
                    prepared + nativeFallback,
                    prepared,
                    nativeFallback,
                    minimumTier == int.MaxValue ? -1 : minimumTier,
                    maximumTier,
                    MaximumDensity,
                    MaximumDistance);
            }
        }

        internal Material ResolveNativeMaterial(TMP_Text text, int materialIndex)
        {
            if (_nativeMaterials != null) return _nativeMaterials.ResolveSourceMaterial(text, materialIndex);
            TMP_TextInfo info = text != null ? text.textInfo : null;
            return info?.meshInfo != null && materialIndex >= 0 && materialIndex < info.meshInfo.Length
                ? info.meshInfo[materialIndex].material : null;
        }

        internal bool TryGetPixelContourProjection(in TextStudio2DTMPGlyphPlane plane,
            out TextStudioPixelContourProjection result, out string warning)
        {
            result = default;
            warning = "Pixel contour strokes need an affine glyph quad and a visible output camera. Use Em units for corner-deformed glyphs.";
            bool haveProjection = false;
            foreach (TextStudio2DSourceProjection candidate in _projections)
            {
                if (!candidate.TryCreateContourProjection(in plane, out TextStudioPixelContourProjection projection)) continue;
                if (!haveProjection) { result = projection; haveProjection = true; }
                else if (!result.EquivalentTo(in projection, plane.EmMinimum, plane.EmSize))
                {
                    warning = "These output cameras need different pixel contour shapes. Use separate text instances for each view, or use Em units.";
                    return false;
                }
            }
            if (haveProjection) warning = string.Empty;
            return haveProjection;
        }

        internal void Update(in TextStudioFrame frame, TMP_Text text, CompiledTextStudio2DStyle style, bool projectionOnly = false, TextStudioTMPMaterialEffectAdapter nativeMaterials = null)
        {
            if (!projectionOnly) _nativeMaterials = nativeMaterials;
            // Same outcome as the settled early return below, reached without rebuilding and hashing an unchanged projection.
            if (projectionOnly && ReferenceEquals(style, _selectedStyle) && !style.RequiresContourGeometry &&
                IsOverlayProjectionCurrent(text) && SelectionsSettled())
                return;
            CollectProjections(text);
            TextStudioDeterministicHash64 projectionHash = default;
            foreach (TextStudio2DSourceProjection projection in _projections) projection.AddFingerprint(ref projectionHash);
            bool reuseMeasurements = _projectionFingerprint == projectionHash.Value && (projectionOnly || HasMeasured(in frame));
            _projectionFingerprint = projectionHash.Value;
            if (!reuseMeasurements)
            {
                Measure(in frame);
                RecordMeasured(in frame);
            }
            // Unchanged projection, glyphs, program and field sources keep their selections; only pending timed demotions need the clock.
            else if (ReferenceEquals(style, _selectedStyle) && !style.RequiresContourGeometry && SelectionsSettled()) return;
            UpdateSelections(style, text);
            _selectedStyle = style;
        }

        // Measurements depend only on the posed glyph quads, which the Hero geometry cache keys the same way.
        bool HasMeasured(in TextStudioFrame frame)
        {
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            return _measuredGlyphCount == snapshot.RenderGlyphCount && _measuredVersion.Equals(snapshot.Version) &&
                ReferenceEquals(_measuredBinding, frame.AppearanceBinding) && _measuredProgram == frame.AppearanceProgramIndex &&
                (frame.GlobalDirty & (DirtyFlags.Topology | DirtyFlags.Layout | DirtyFlags.Placement | DirtyFlags.AppearanceTopology)) == 0 &&
                frame.Dirty.IsCreated && !frame.Dirty.AnyDirtyInRange(0, frame.AnimatedCount);
        }

        void RecordMeasured(in TextStudioFrame frame)
        {
            _measuredVersion = frame.VisualSnapshot.Version;
            _measuredGlyphCount = frame.VisualSnapshot.RenderGlyphCount;
            _measuredBinding = frame.AppearanceBinding;
            _measuredProgram = frame.AppearanceProgramIndex;
        }

        // Both callers return early only for styles without contour geometry, which never read contour precision. Waiting
        // for its one-second demotion kept every new sleeping text re-collecting its projection each frame.
        bool SelectionsSettled()
        {
            foreach (Font font in _fonts.Values)
                if (font.Used && !font.Selection.IsSettledFor(font.Data)) return false;
            return true;
        }

        void Measure(in TextStudioFrame frame)
        {
            OutputPixelsPerUnit = 0;
            TextStudioVisualSnapshot snapshot = frame.VisualSnapshot;
            foreach (Font font in _fonts.Values)
            {
                font.Used = false; font.Density = 0; font.MinimumProjected = float.PositiveInfinity;
            }
            IReadOnlyList<TextStudio2DAppearanceBinding.GlyphBinding> appearanceGlyphs = frame.AppearanceBinding != null && frame.AppearanceProgramIndex >= 0
                ? frame.AppearanceBinding.GetProgramGlyphs(frame.AppearanceProgramIndex) : null;
            int glyphCount = appearanceGlyphs?.Count ?? snapshot.RenderGlyphCount;
            for (int row = 0; row < glyphCount; row++)
            {
                int index = appearanceGlyphs != null ? appearanceGlyphs[row].RenderGlyphIndex : row;
                ref readonly TextStudioVisualGlyph glyph = ref snapshot.RenderGlyphAt(index);
                if (appearanceGlyphs != null && !frame.AppearanceBinding.Contains(appearanceGlyphs[row].OccurrenceIndex, glyph.TextUnitIndex)) continue;
                if (!glyph.IsVisible || glyph.SourceKind != TextStudioVisualSourceKind.FontGlyph)
                {
                    // Spaces and inline sources can own visible surfaces without consuming any font fields.
                    Vector2 center = (glyph.BoundsMin + glyph.BoundsMax) * .5f;
                    for (int projection = 0; projection < _projections.Count; projection++)
                        OutputPixelsPerUnit = Mathf.Max(OutputPixelsPerUnit, _projections[projection].MeasureOutputScale(center));
                    continue;
                }
                if (!_fonts.TryGetValue(glyph.FontIdentity, out Font font))
                { font = new Font(glyph.FontIdentity); _fonts.Add(glyph.FontIdentity, font); }
                if (!font.Used)
                {
                    font.Data = snapshot.TryGetFontSource(glyph.FontIdentity, out TextStudioVisualFontSource source) ? source.AppearanceData : null;
                    font.Used = true;
                }
                TextStudio2DSourceProjection.PoseQuad(in frame, in glyph, out Vector3 a, out Vector3 b, out Vector3 c, out Vector3 d, out Vector2 spanEm);
                for (int projection = 0; projection < _projections.Count; projection++)
                {
                    if (!_projections[projection].Measure(a, b, c, d, spanEm, out float maximum, out float minimum, out float localPixelsPerUnit)) continue;
                    OutputPixelsPerUnit = Mathf.Max(OutputPixelsPerUnit, localPixelsPerUnit);
                    font.Density = Mathf.Max(font.Density, maximum);
                    font.MinimumProjected = Mathf.Min(font.MinimumProjected, minimum);
                }
            }
        }

        void UpdateSelections(CompiledTextStudio2DStyle style, TMP_Text text)
        {
            SelectionUpdateCount++;
            float pixels = PixelDistance(style);
            TextStudio2DSourceFieldRequest request = style.SourceFieldRequest;
            double now = Time.realtimeSinceStartupAsDouble;
            TextStudioDeterministicHash64 hash = default;
            TextStudioDeterministicHash64 readiness = default;
            TextStudioDeterministicHash64 geometry = default;
            TMP_TextInfo info = text != null ? text.textInfo : null;
            if (style.RequiresContourGeometry && info?.meshInfo != null)
                for (int material = 0; material < Mathf.Min(info.materialCount, info.meshInfo.Length); material++)
                    TextStudioTMPFontMetrics.AddNativeFaceFingerprint(ref geometry, ResolveNativeMaterial(text, material));
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (!TextStudio2DStyleDecorations.UsesContourStroke(in operation) || operation.Stroke.Units != TextStudio2DUnit.Pixels) continue;
                geometry.Add(_projectionFingerprint);
                break;
            }
            MaximumDensity = request.MinimumPixelsPerEm; MaximumDistance = request.MinimumSafeDistanceEm;
            _unused.Clear();
            foreach (Font font in _fonts.Values)
            {
                if (!font.Used) { _unused.Add(font.Identity); continue; }
                float extent = float.IsFinite(font.MinimumProjected) ? pixels / Mathf.Max(.001f, font.MinimumProjected) : 0;
                font.Selection.Update(font.Data, in request, font.Density, extent, now);
                font.ContourPrecision.Update(font.Density, now);
                geometry.Add(font.Identity); geometry.Add(font.ContourPrecision.Density);
                hash.Add(font.Identity); hash.Add(font.Data != null ? font.Data.Revision : 0); hash.Add(font.Selection.Tier);
                readiness.Add(font.Identity); readiness.Add(Mathf.CeilToInt(font.Selection.RequiredPixelsPerEm));
                readiness.Add(Mathf.CeilToInt(font.Selection.RequiredSafeDistanceEm * 1000));
                MaximumDensity = Mathf.Max(MaximumDensity, font.Selection.RequiredPixelsPerEm);
                MaximumDistance = Mathf.Max(MaximumDistance, font.Selection.RequiredSafeDistanceEm);
            }
            foreach (ulong identity in _unused) _fonts.Remove(identity);
            _unused.Clear();
            Fingerprint = hash.Value;
            ReadinessFingerprint = readiness.Value;
            GeometryFingerprint = geometry.Value;
        }

        bool IsOverlayProjectionCurrent(TMP_Text text)
        {
            if (_overlayRoot == null || !(text is TextMeshProUGUI ui)) return false;
            Canvas canvas = ui.canvas;
            if (canvas == null) return false;
            Canvas root = canvas.rootCanvas;
            return ReferenceEquals(root, _overlayRoot) &&
                   root.renderMode == RenderMode.ScreenSpaceOverlay &&
                   root.scaleFactor == _overlayScale &&
                   _overlayModel.Equals(text.transform.localToWorldMatrix) &&
                   _overlayRootMatrix.Equals(root.transform.localToWorldMatrix);
        }

        void CollectProjections(TMP_Text text)
        {
            _projections.Clear();
            _overlayRoot = null;
            if (text == null) return;
            Matrix4x4 model = text.transform.localToWorldMatrix;
            Canvas canvas = text is TextMeshProUGUI ui ? ui.canvas : null;
            if (canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                Canvas root = canvas.rootCanvas;
                _overlayRoot = root;
                _overlayModel = model;
                _overlayRootMatrix = root.transform.localToWorldMatrix;
                _overlayScale = root.scaleFactor;
                _projections.Add(new TextStudio2DSourceProjection(root.transform.worldToLocalMatrix * model,
                    Vector2.one * Mathf.Max(.001f, _overlayScale), true));
                return;
            }
            if (canvas != null && canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceCamera && canvas.rootCanvas.worldCamera != null)
            {
                AddProjection(canvas.rootCanvas.worldCamera, text, model);
                return;
            }
            int activeCount = Camera.allCamerasCount;
            if (_cameras.Length < activeCount + 18) Array.Resize(ref _cameras, Mathf.NextPowerOfTwo(activeCount + 18));
            int count = Camera.GetAllCameras(_cameras);
            count = TextStudio2DCameraUniforms.AppendRecentCameras(_cameras, count);
            Camera explicitCamera = canvas != null ? canvas.worldCamera : Camera.current;
            if (explicitCamera != null)
            {
                bool exists = false;
                for (int index = 0; index < count; index++) if (_cameras[index] == explicitCamera) { exists = true; break; }
                if (!exists) _cameras[count++] = explicitCamera;
            }
            for (int index = 0; index < count; index++)
                AddProjection(_cameras[index], text, model);
            Array.Clear(_cameras, 0, _cameras.Length);
            // Editor navigation must not redefine the pixel scale of an actual output camera.
            // Keep Scene View demand only when it is the text's sole available view.
            bool hasOutput = false;
            for (int index = 0; index < _projections.Count; index++)
                if (!_projections[index].IsSceneView) { hasOutput = true; break; }
            if (hasOutput)
                for (int index = _projections.Count - 1; index >= 0; index--)
                    if (_projections[index].IsSceneView) _projections.RemoveAt(index);
        }

        void AddProjection(Camera camera, TMP_Text text, Matrix4x4 model)
        {
            if (camera == null || (camera.cullingMask & (1 << text.gameObject.layer)) == 0 || camera.pixelWidth <= 0 || camera.pixelHeight <= 0) return;
            Scene renderScene = camera.scene;
            if (renderScene.IsValid() && renderScene != text.gameObject.scene) return;
#if UNITY_EDITOR
            // Preview and prefab stages can share layers while using disjoint scene culling masks.
            if (!renderScene.IsValid())
            {
                ulong cameraMask = camera.overrideSceneCullingMask;
                if (cameraMask == 0) cameraMask = camera.gameObject.sceneCullingMask;
                if ((cameraMask & text.gameObject.sceneCullingMask) == 0) return;
            }
#endif
            _projections.Add(new TextStudio2DSourceProjection(camera.projectionMatrix * camera.worldToCameraMatrix * model,
                new Vector2(camera.pixelWidth, camera.pixelHeight), sceneView: camera.cameraType == CameraType.SceneView));
        }

        static float PixelDistance(CompiledTextStudio2DStyle style)
        {
            float distance = 0;
            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.SurfaceOwnerOperationIndex >= 0 || operation.Kind == TextStudio2DLayerKind.Decoration ||
                    TextStudio2DStyleDecorations.UsesContourStroke(in operation) || !TextStudio2DFieldPadding.UsesPixelUnits(in operation)) continue;
                if (operation.Kind == TextStudio2DLayerKind.FauxDepth)
                { distance = Mathf.Max(distance, operation.FauxDepth.SilhouetteOutset + operation.FauxDepth.Softness); continue; }
                TextStudio2DBoundsExpansion bounds = operation.BoundsExpansion;
                distance = Mathf.Max(distance, Mathf.Max(Mathf.Max(bounds.Left, bounds.Right), Mathf.Max(bounds.Top, bounds.Bottom)));
            }
            return distance;
        }
    }
}
