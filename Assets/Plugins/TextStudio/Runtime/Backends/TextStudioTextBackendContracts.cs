using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Stable identifiers used to select built-in text backends.</summary>
    public static class TextStudioTextBackendIds
    {
        public const string TextMeshPro = "textmeshpro";
        public const string UnityUIText = "unity-ui-text";
    }

    /// <summary>Flags the shaping, bidirectional text, color, rich-text, editing, geometry, and animation features exposed by a text backend.</summary>
    [Flags]
    public enum TextStudioTextBackendCapabilities
    {
        None = 0,
        ComplexShaping = 1 << 0,
        BidirectionalText = 1 << 1,
        ColorGlyphs = 1 << 2,
        RichText = 1 << 3,
        EditableText = 1 << 4,
        NativeEffects = 1 << 5,
        ParallelMeshGeneration = 1 << 6,
        SourceVertexColors = 1 << 7,
        TextStudioVertexAnimation = 1 << 8,
        ShapedGlyphOutlines = 1 << 9
    }

    /// <summary>Classifies a backend notification so the hub can refresh the affected data.</summary>
    public enum TextStudioTextBackendChange
    {
        Mesh,
        Layout,
        Text,
        /// <summary>Source colors changed while text, layout and glyph identity stayed valid.</summary>
        Presentation
    }

    /// <summary>Chooses how an active text backend handles direct text writes outside the hub: warn, import them as canonical source, or ignore them.</summary>
    public enum TextStudioBackendWriteHandling
    {
        Warn = 0,
        ImportAsSource = 1,
        Ignore = 2
    }

    /// <summary>
    /// Stable identity and lifecycle contract for an optional text backend.
    /// Source, layout, and mesh responsibilities are split into additive interfaces so
    /// future backend features do not require changing this base contract.
    /// </summary>
    public interface ITextStudioTextBackend
    {
        /// <summary>Gets the stable namespaced identifier persisted by backend selection.</summary>
        string BackendId { get; }
        /// <summary>Gets the user-facing backend name.</summary>
        string DisplayName { get; }
        /// <summary>Gets the selection priority used when compatible backends compete.</summary>
        int Priority { get; }
        /// <summary>Gets whether the backend can currently drive its target.</summary>
        bool IsAvailable { get; }
        /// <summary>Gets the Unity object whose output is controlled by this backend.</summary>
        UnityEngine.Object TargetObject { get; }
        /// <summary>Gets the shaping, rendering, and editing features implemented by the backend.</summary>
        TextStudioTextBackendCapabilities Capabilities { get; }

        /// <summary>Occurs when text, layout, or mesh state changes outside the host pipeline.</summary>
        event Action<TextStudioTextBackendChange> Changed;

        /// <summary>Attaches the backend to a host and begins observing its target.</summary>
        /// <param name="owner">Host that will issue backend requests.</param>
        void Attach(TextStudioText owner);
        /// <summary>Stops observation and releases backend-owned output state.</summary>
        void Detach();
    }

    /// <summary>
    /// Optional capability contract for text backends that do not render Text Studio's complete
    /// persistent 2D Appearance stack. Backends that omit this interface retain the established
    /// supported behavior.
    /// </summary>
    public interface ITextStudioTextBackendAppearanceSupport
    {
        /// <summary>
        /// Gets whether the backend renders every Text Studio 2D Appearance execution path.
        /// </summary>
        bool SupportsTextStudio2DAppearance { get; }
    }

    /// <summary>Optional capability contract that reports faithfully rendered shared effect channels.</summary>
    public interface ITextStudioTextBackendRenderChannelSupport
    {
        /// <summary>Gets the shared Text Studio channels implemented by the backend.</summary>
        TextStudioRenderChannels SupportedRenderChannels { get; }
    }

    /// <summary>Optional backend capability for synchronizing authored markup with an external text target.</summary>
    public interface ITextStudioTextSourceBackend
    {
        /// <summary>Gets whether the target changed since the host last pulled its text.</summary>
        bool HasUnobservedTextChange { get; }
        /// <summary>Gets the text currently stored by the backend target.</summary>
        string CurrentText { get; }

        /// <summary>Resolves an external text mutation according to the host's write-handling policy.</summary>
        /// <returns><see langword="true"/> when <c>newMarkup</c> should replace host markup.</returns>
        bool TryPullText(
            bool isDriving,
            TextStudioBackendWriteHandling handling,
            string currentMarkup,
            out string newMarkup,
            out bool ignoredExternalWrite);

        /// <summary>Pushes compiled text and its transient plan view to the backend.</summary>
        /// <param name="request">Text request valid only for the duration of the call.</param>
        void PushText(in TextStudioBackendTextRequest request);
        /// <summary>Writes raw authoring markup when the target supports editing.</summary>
        /// <param name="markup">Uncompiled markup to display or edit.</param>
        void PushRawAuthoringText(string markup);
    }

    /// <summary>Optional backend capability for publishing semantic glyph layout to Text Studio.</summary>
    public interface ITextStudioLayoutBackend
    {
        /// <summary>Gets whether layout data can be built for the backend's current target state.</summary>
        bool IsLayoutReady { get; }

        /// <summary>Builds and commits layout for the current compiled plan.</summary>
        /// <returns><see langword="true"/> only when a complete compatible layout was committed.</returns>
        bool TryBuildLayout(
            in TextStudioBackendLayoutRequest request,
            TextStudioBackendLayoutBuilder builder);
    }

    /// <summary>Optional backend capability for refreshing source colors without rebuilding layout.</summary>
    public interface ITextStudioSourceColorBackend
    {
        /// <summary>Copies current colors into caller-owned storage for the committed semantic units. Returns false when a new layout is required.</summary>
        bool TryRefreshSourceColors(TextStudioGlyphColorQuad[] colors, int count);
    }

    /// <summary>Optional backend capability that renders evaluated glyph state into its target.</summary>
    public interface ITextStudioMeshBackend
    {
        /// <summary>Gets whether the backend currently permits visible output.</summary>
        bool IsVisualOutputEnabled { get; }
        /// <summary>Gets whether the host must evaluate complete composed glyph state before rendering.</summary>
        bool NeedsComposedGlyphState { get; }

        /// <summary>Renders one transient evaluated frame without retaining its views.</summary>
        /// <param name="frame">Frame data valid only for the duration of the call.</param>
        /// <param name="suppressOutput">Whether to update internal state without presenting visible output.</param>
        void Render(in TextStudioFrame frame, bool suppressOutput);
        /// <summary>Restores target state that the backend replaced while driving output.</summary>
        void RestoreOutput();
    }

    /// <summary>
    /// Lets the settled host of a custom text backend idle-sleep and park in the hub scheduler like TMP-hosted text. The backend
    /// guarantees that every change of its target that can alter the host's output reaches the host through
    /// <see cref="ITextStudioTextBackend.Changed"/> or a host rebuild request in the frame it happens, before the scheduler's
    /// LateUpdate, and that its text projection revision does not change without one. Backends without it keep their host awake.
    /// </summary>
    internal interface ITextStudioIdleTextBackend
    {
        /// <summary>Names what keeps the host polling, or null while it may sleep. Every sleep check reads it, so keep it cheap.</summary>
        string IdleSleepBlocker { get; }
    }

    /// <summary>
    /// Optional topology contract for renderers that need the exact shaped glyph IDs and
    /// font outlines selected by a text backend. This keeps complex shaping and fallback
    /// font decisions authoritative when another renderer, such as Text Studio 3D,
    /// converts the backend output into different geometry.
    /// </summary>
    public interface ITextStudioShapedGlyphBackend
    {
        int ShapedGlyphRevision { get; }
        int ShapedGlyphCount { get; }

        /// <summary>Gets one glyph from the current shaped topology revision.</summary>
        /// <returns><see langword="false"/> when the index is invalid or topology is unavailable.</returns>
        bool TryGetShapedGlyph(
            int shapedGlyphIndex,
            out TextStudioBackendShapedGlyph glyph);

        /// <summary>Gets the font face referenced by a shaped glyph.</summary>
        /// <returns><see langword="false"/> when the font is unavailable in the current revision.</returns>
        bool TryGetShapedFontFace(
            int fontId,
            out TextStudioBackendShapedFontFace fontFace);
    }

    /// <summary>Optional configured font-source capability for authoring validation before a backend has committed layout.</summary>
    public interface ITextStudioRuntimeFontSourceBackend
    {
        /// <summary>Copies the configured primary, authored fallback/typeface and styled font closure into caller-owned descriptors retaining immutable owned source bytes. This must not shape text, change the target or require a committed glyph revision. Dynamically selected system faces remain authoritative through the shaped-glyph contract once selected.</summary>
        /// <returns><see langword="false"/> with an actionable diagnostic when the configured source cannot supply runtime outlines.</returns>
        bool TryGetRuntimeFontSources(
            out TextStudioBackendShapedFontFace[] fontFaces,
            out string error);
    }

    /// <summary>Optional shaped-glyph data that an external backend can publish.</summary>
    [Flags]
    public enum TextStudioShapedGlyphFeature
    {
        None = 0,
        GlyphTopology = 1 << 0,
        FontOutlines = 1 << 1,
        FinalVertexColors = 1 << 2,
        GeometryScale = 1 << 3,
        ItalicShear = 1 << 4,
        SyntheticWeight = 1 << 5,
        IntrinsicRotation = 1 << 6,
        Decorations = 1 << 7,
        VirtualGlyphTopology = 1 << 8,
        WorldSpaceTarget = 1 << 9,
        NativeRichText = 1 << 10,
        PlanarGeometry = 1 << 11,
        FilledPrimitives = 1 << 12
    }

    /// <summary>
    /// Describes which shaped-glyph features are present and which represent the backend's
    /// final rendered state rather than a compatible derivation.
    /// </summary>
    public readonly struct TextStudioShapedGlyphFeatureSet
    {
        public TextStudioShapedGlyphFeature Available { get; }
        public TextStudioShapedGlyphFeature Exact { get; }

        /// <summary>Creates a shaped-glyph capability set, restricting exact features to those that are available.</summary>
        public TextStudioShapedGlyphFeatureSet(
            TextStudioShapedGlyphFeature available,
            TextStudioShapedGlyphFeature exact)
        {
            Available = available;
            Exact = exact & available;
        }

        /// <summary>Reports whether all requested shaped-glyph features are available.</summary>
        public bool Supports(TextStudioShapedGlyphFeature feature)
        {
            return (Available & feature) == feature;
        }

        /// <summary>Reports whether all requested shaped-glyph features are provided with exact fidelity.</summary>
        public bool IsExact(TextStudioShapedGlyphFeature feature)
        {
            return (Exact & feature) == feature;
        }
    }

    /// <summary>
    /// Optional capability contract for consumers that need to distinguish exact shaped-glyph
    /// data from compatible fallback values.
    /// </summary>
    public interface ITextStudioShapedGlyphFeatureProvider
    {
        TextStudioShapedGlyphFeatureSet ShapedGlyphFeatures { get; }
    }

    /// <summary>
    /// Optional authoring capability for shaping representative text independently from the live
    /// backend target. Implementations must not replace target text or retain the returned snapshot.
    /// </summary>
    public interface ITextStudioShapedGlyphPreparationBackend
    {
        /// <summary>Creates a caller-owned shaped glyph snapshot for the supplied representative text.</summary>
        bool TryCreatePreparationSnapshot(
            string text,
            out TextStudioShapedGlyphPreparationSnapshot snapshot,
            out string error);
    }

    /// <summary>
    /// Caller-owned shaped glyph and font-face data for one representative preparation sample.
    /// </summary>
    public sealed class TextStudioShapedGlyphPreparationSnapshot : ITextStudioShapedGlyphBackend,
        ITextStudioShapedGeometryBackend, ITextStudioShapedGlyphImageBackend
    {
        readonly TextStudioGlyphImageSnapshot[] _images;
        readonly TextStudioBackendShapedGlyph[] _glyphs;
        readonly TextStudioBackendShapedFontFace[] _fontFaces;
        readonly TextStudioBackendShapedGlyphGeometry[] _geometry;
        readonly TextStudioBackendShapedPrimitive[] _primitives;
        readonly int[] _ownerCodepoints;

        /// <summary>Creates a snapshot by copying the supplied glyph and font-face data.</summary>
        public TextStudioShapedGlyphPreparationSnapshot(
            TextStudioBackendShapedGlyph[] glyphs,
            TextStudioBackendShapedFontFace[] fontFaces,
            TextStudioBackendShapedGlyphGeometry[] geometry = null,
            TextStudioBackendShapedPrimitive[] primitives = null,
            int[] ownerCodepoints = null,
            TextStudioGlyphImageSnapshot[] images = null)
        {
            _glyphs = glyphs != null
                ? (TextStudioBackendShapedGlyph[])glyphs.Clone()
                : Array.Empty<TextStudioBackendShapedGlyph>();
            if (geometry != null && geometry.Length != _glyphs.Length)
                throw new ArgumentException("Planar geometry must align with the same shaped glyph revision.", nameof(geometry));
            _geometry = geometry != null ? (TextStudioBackendShapedGlyphGeometry[])geometry.Clone()
                : Array.Empty<TextStudioBackendShapedGlyphGeometry>();
            _primitives = primitives != null ? (TextStudioBackendShapedPrimitive[])primitives.Clone()
                : Array.Empty<TextStudioBackendShapedPrimitive>();
            _ownerCodepoints = ownerCodepoints != null ? (int[])ownerCodepoints.Clone() : Array.Empty<int>();
            if (images != null && images.Length != _glyphs.Length)
                throw new ArgumentException("Prepared images must align with the exact shaped glyph array.", nameof(images));
            _images = new TextStudioGlyphImageSnapshot[_glyphs.Length];
            for (int i = 0; i < _images.Length; i++)
            {
                TextStudioGlyphImageSnapshot image = images?[i];
                if (image == null) continue;
                if (!image.Request.MatchesGlyph(_glyphs[i]))
                    throw new ArgumentException("A prepared image does not match its complete shaped occurrence.", nameof(images));
                TextStudioGlyphImageRequest original = image.Request;
                var request = new TextStudioGlyphImageRequest(original.Identity, _glyphs[i], 1, i, original.SourceStart, original.SourceLength);
                _images[i] = image.ForOccurrence(in request, 0);
            }
            int faceCount = fontFaces?.Length ?? 0;
            _fontFaces = new TextStudioBackendShapedFontFace[faceCount];
            for (int i = 0; i < faceCount; i++)
            {
                TextStudioBackendShapedFontFace face = fontFaces[i];
                _fontFaces[i] = new TextStudioBackendShapedFontFace(
                    face.FontId,
                    face.DisplayName,
                    face.GetFontBytesSnapshot(),
                    face.IsColorFont,
                    face.UnitsPerEm,
                    face.FaceIndex,
                    face.Ascent,
                    face.UnderlineOffset,
                    face.UnderlineThickness,
                    face.StrikethroughOffset,
                    face.StrikethroughThickness);
            }
        }

        /// <summary>Gets the prepared exact image request for a shaped glyph, when owned pixels are available.</summary>
        public TextStudioGlyphImageStatus GetGlyphImageRequest(int shapedGlyphIndex, out TextStudioGlyphImageRequest request, out string diagnostic)
        {
            request = default; diagnostic = string.Empty;
            if ((uint)shapedGlyphIndex >= _glyphs.Length)
            { diagnostic = "The shaped glyph index is outside the preparation snapshot."; return TextStudioGlyphImageStatus.InvalidData; }
            TextStudioGlyphImageSnapshot image = _images[shapedGlyphIndex];
            if (image != null) { request = image.Request; return TextStudioGlyphImageStatus.Ready; }
            if (TryGetFontFace(_glyphs[shapedGlyphIndex].FontId, out TextStudioBackendShapedFontFace face) && face.IsColorFont)
            { diagnostic = "The preparation snapshot has no owned RGBA pixels for this exact color glyph."; return TextStudioGlyphImageStatus.Unavailable; }
            return TextStudioGlyphImageStatus.NotAnImage;
        }

        /// <summary>Copies prepared image pixels into the requested shaped occurrence when its identity still matches.</summary>
        public TextStudioGlyphImageStatus CaptureGlyphImage(in TextStudioGlyphImageRequest request, out TextStudioGlyphImageSnapshot image, out string diagnostic)
        {
            image = null; diagnostic = string.Empty;
            if (!request.IsCurrentFor(this))
            { diagnostic = "The image request does not match the immutable preparation snapshot."; return TextStudioGlyphImageStatus.Stale; }
            TextStudioGlyphImageSnapshot stored = _images[request.ShapedGlyphIndex];
            if (stored == null || !stored.Request.Identity.Equals(request.Identity))
            { diagnostic = "No prepared pixels exist for this exact shaped image identity."; return TextStudioGlyphImageStatus.Unavailable; }
            image = stored.ForOccurrence(in request, 0); return TextStudioGlyphImageStatus.Ready;
        }

        public int GlyphCount => _glyphs.Length;
        public int FontFaceCount => _fontFaces.Length;
        public int ShapedGlyphRevision => 1;
        public int ShapedGlyphCount => GlyphCount;
        public int ShapedPrimitiveCount => _primitives.Length;
        public bool TryGetShapedGlyph(int index, out TextStudioBackendShapedGlyph glyph) => TryGetGlyph(index, out glyph);
        public bool TryGetShapedFontFace(int fontId, out TextStudioBackendShapedFontFace face) => TryGetFontFace(fontId, out face);

        /// <summary>Gets planar geometry aligned with a glyph in this preparation snapshot.</summary>
        public bool TryGetShapedGlyphGeometry(int index, out TextStudioBackendShapedGlyphGeometry geometry)
        {
            geometry = (uint)index < (uint)_geometry.Length ? _geometry[index] : default;
            return geometry.IsPresent;
        }

        /// <summary>Gets one filled vector primitive owned by this preparation snapshot.</summary>
        public bool TryGetShapedPrimitive(int index, out TextStudioBackendShapedPrimitive primitive)
        {
            if ((uint)index < (uint)_primitives.Length) { primitive = _primitives[index]; return true; }
            primitive = default;
            return false;
        }

        /// <summary>Gets the semantic owner codepoint used for preparation scopes, without changing the shaped font glyph.</summary>
        public bool TryGetOwnerCodepoint(int textUnitIndex, out int codepoint)
        {
            if ((uint)textUnitIndex < (uint)_ownerCodepoints.Length) { codepoint = _ownerCodepoints[textUnitIndex]; return true; }
            codepoint = 0;
            return false;
        }

        /// <summary>Gets the shaped glyph at the supplied snapshot index.</summary>
        public bool TryGetGlyph(
            int index,
            out TextStudioBackendShapedGlyph glyph)
        {
            if ((uint)index < (uint)_glyphs.Length)
            {
                glyph = _glyphs[index];
                return true;
            }
            glyph = default;
            return false;
        }

        /// <summary>Gets the font face identified by the supplied backend font ID.</summary>
        public bool TryGetFontFace(
            int fontId,
            out TextStudioBackendShapedFontFace fontFace)
        {
            for (int i = 0; i < _fontFaces.Length; i++)
            {
                if (_fontFaces[i].FontId != fontId)
                    continue;
                fontFace = _fontFaces[i];
                return true;
            }
            fontFace = default;
            return false;
        }
    }

    /// <summary>
    /// Transient description of a shaped font face, including metrics and a bounded view of backend-owned font bytes.
    /// </summary>
    public readonly struct TextStudioBackendShapedFontFace
    {
        readonly byte[] _fontBytes;

        public int FontId { get; }
        public string DisplayName { get; }

        /// <summary>
        /// Transient read-only view over backend-owned font data. The view remains valid until the
        /// backend's <see cref="ITextStudioShapedGlyphBackend.ShapedGlyphRevision"/> changes.
        /// </summary>
        public TextStudioReadOnlyBuffer<byte> FontData =>
            new TextStudioReadOnlyBuffer<byte>(_fontBytes, _fontBytes?.Length ?? 0);

        /// <summary>Number of bytes in <see cref="FontData"/>.</summary>
        public int FontByteCount => _fontBytes?.Length ?? 0;

        /// <summary>Returns a detached caller-owned copy of the font data.</summary>
        public byte[] GetFontBytesSnapshot()
        {
            return _fontBytes != null && _fontBytes.Length > 0
                ? (byte[])_fontBytes.Clone()
                : Array.Empty<byte>();
        }

        internal byte[] UnsafeFontBytes => _fontBytes;
        public bool IsColorFont { get; }
        public int UnitsPerEm { get; }

        /// <summary>
        /// Zero-based face index inside a font collection (<c>ttcf</c>). Zero for single-face files.
        /// Outline parsing selects this face before reading the sfnt table directory.
        /// </summary>
        public int FaceIndex { get; }
        public float Ascent { get; }
        public float UnderlineOffset { get; }
        public float UnderlineThickness { get; }
        public float StrikethroughOffset { get; }
        public float StrikethroughThickness { get; }

        public bool IsValid =>
            FontId != 0 &&
            _fontBytes != null &&
            _fontBytes.Length > 0;

        /// <summary>Creates shaped-font metadata for one backend font identifier.</summary>
        public TextStudioBackendShapedFontFace(
            int fontId,
            string displayName,
            byte[] fontBytes,
            bool isColorFont,
            int unitsPerEm,
            float ascent,
            float underlineOffset,
            float underlineThickness,
            float strikethroughOffset,
            float strikethroughThickness)
            : this(
                fontId,
                displayName,
                fontBytes,
                isColorFont,
                unitsPerEm,
                0,
                ascent,
                underlineOffset,
                underlineThickness,
                strikethroughOffset,
                strikethroughThickness)
        {
        }

        /// <summary>Creates shaped-font metadata for one backend font identifier and collection face.</summary>
        public TextStudioBackendShapedFontFace(
            int fontId,
            string displayName,
            byte[] fontBytes,
            bool isColorFont,
            int unitsPerEm,
            int faceIndex,
            float ascent,
            float underlineOffset,
            float underlineThickness,
            float strikethroughOffset,
            float strikethroughThickness)
        {
            FontId = fontId;
            DisplayName = displayName ?? string.Empty;
            _fontBytes = fontBytes;
            IsColorFont = isColorFont;
            UnitsPerEm = Mathf.Max(1, unitsPerEm);
            FaceIndex = Mathf.Max(0, faceIndex);
            Ascent = ascent;
            UnderlineOffset = underlineOffset;
            UnderlineThickness = underlineThickness;
            StrikethroughOffset = strikethroughOffset;
            StrikethroughThickness = strikethroughThickness;
        }
    }

    /// <summary>Immutable shaped-glyph topology and metrics published by a shaping-capable backend.</summary>
    public readonly struct TextStudioBackendShapedGlyph
    {
        public int TextUnitIndex { get; }
        public int ClusterIndex { get; }
        public int FontId { get; }
        public int GlyphIndex { get; }
        public uint Codepoint { get; }
        public Vector2 BaselineOrigin { get; }
        public Vector2 Advance { get; }
        public float GeometryScale { get; }
        public float ItalicShear { get; }
        public float SyntheticBoldScale { get; }
        public Quaternion IntrinsicRotation { get; }
        public bool IsVisible { get; }
        public bool IsWhitespace { get; }
        public DecorationBits Decorations { get; }

        /// <summary>Creates a shaped glyph mapped to a semantic text unit and font face.</summary>
        public TextStudioBackendShapedGlyph(
            int textUnitIndex,
            int clusterIndex,
            int fontId,
            int glyphIndex,
            uint codepoint,
            Vector2 baselineOrigin,
            Vector2 advance,
            float geometryScale,
            float italicShear,
            float syntheticBoldScale,
            Quaternion intrinsicRotation,
            bool isVisible,
            bool isWhitespace,
            DecorationBits decorations = DecorationBits.None)
        {
            TextUnitIndex = textUnitIndex;
            ClusterIndex = clusterIndex;
            FontId = fontId;
            GlyphIndex = glyphIndex;
            Codepoint = codepoint;
            BaselineOrigin = baselineOrigin;
            Advance = advance;
            GeometryScale = geometryScale;
            ItalicShear = italicShear;
            SyntheticBoldScale = syntheticBoldScale;
            IntrinsicRotation = intrinsicRotation;
            IsVisible = isVisible;
            IsWhitespace = isWhitespace;
            Decorations = decorations;
        }
    }

    /// <summary>Transient compiled-text request passed to an external text backend.</summary>
    public readonly struct TextStudioBackendTextRequest
    {
        public TextStudioPlanView Plan { get; }
        public string Markup { get; }
        public string ResolvedText { get; }
        public string DisplayText { get; }
        public bool IsDriving { get; }
        public bool IsLiteralMarkup { get; }

        internal TextStudioBackendTextRequest(
            TextStudioCompiledPlan plan,
            string markup,
            bool isDriving,
            bool isLiteralMarkup)
        {
            Plan = new TextStudioPlanView(plan);
            Markup = markup ?? string.Empty;
            ResolvedText = plan?.ResolvedText ?? string.Empty;
            DisplayText = plan?.DisplayText ?? ResolvedText;
            IsDriving = isDriving;
            IsLiteralMarkup = isLiteralMarkup;
        }
    }

    /// <summary>Transient layout request passed to a backend before it publishes glyph positions.</summary>
    public readonly struct TextStudioBackendLayoutRequest
    {
        readonly TextStudioCompiledPlan _plan;
        readonly TextStudioSourceContextRequirements _sourceContextRequirements;

        public TextStudioPlanView Plan => new TextStudioPlanView(_plan);
        public bool CacheSourceVertexData { get; }

        internal TextStudioCompiledPlan CompiledPlan => _plan;
        internal TextStudioSourceContextRequirements SourceContextRequirements =>
            _sourceContextRequirements;

        internal TextStudioBackendLayoutRequest(
            TextStudioCompiledPlan plan,
            bool cacheSourceVertexData,
            TextStudioSourceContextRequirements sourceContextRequirements)
        {
            _plan = plan;
            CacheSourceVertexData = cacheSourceVertexData;
            _sourceContextRequirements = sourceContextRequirements;
        }
    }

    /// <summary>
    /// Structured zero-allocation layout publication request. Array inputs remain caller-owned and
    /// are copied into Text Studio storage only when committed successfully.
    /// </summary>
    public struct TextStudioBackendLayoutData
    {
        internal Vector3[] Positions;
        internal int PositionCount;
        internal float AutoFitGlyphMargin;
        internal float[] SourceCenterBaselineOffsets;
        internal LineRange[] PlacementLines;
        internal int PlacementLineCount;
        internal TextStudioGlyphColorQuad[] SourceColors;
        internal bool[] SourceColorValidity;
        internal TextStudioGlyphColorBounds[] SourceColorBounds;
        internal TextStudioGlyphVisualBounds[] SourceVisualBounds;
        internal bool[] SourceLayoutValidity;
        internal float[] EffectUnitScales;
        internal int[] TextUnitToLineIndex;
        internal int[] TextUnitToWordIndex;
        internal Vector2? SourceAlignment;
        internal TextStudioVisualGlyph[] VisualGlyphs;
        internal TextStudioVisualGlyph[] RenderGlyphs;
        internal int RenderGlyphCount;
        internal TextStudioVisualFontSource[] FontSources;
        internal int FontSourceCount;
        internal TextStudioVisualCapabilities VisualCapabilities;
        internal TextStudioVisualVersion? VisualVersion;

        /// <summary>Number of positions to publish.</summary>
        public int Count => PositionCount;

        /// <summary>
        /// Starts a layout publication for the supplied positions. The array is read only during
        /// <see cref="TextStudioBackendLayoutBuilder.Commit(in TextStudioBackendLayoutData)"/>.
        /// </summary>
        public TextStudioBackendLayoutData(Vector3[] positions, int count)
        {
            Positions = positions;
            PositionCount = count;
            AutoFitGlyphMargin = 0f;
            SourceCenterBaselineOffsets = null;
            PlacementLines = null;
            PlacementLineCount = 0;
            SourceColors = null;
            SourceColorValidity = null;
            SourceColorBounds = null;
            SourceVisualBounds = null;
            SourceLayoutValidity = null;
            EffectUnitScales = null;
            TextUnitToLineIndex = null;
            TextUnitToWordIndex = null;
            SourceAlignment = null;
            VisualGlyphs = null;
            RenderGlyphs = null; RenderGlyphCount = 0; FontSources = null; FontSourceCount = 0;
            VisualCapabilities = TextStudioVisualCapabilities.None;
            VisualVersion = null;
        }

        /// <summary>Returns a copy with additional glyph margin used by auto-fit calculations.</summary>
        /// <param name="margin">Margin in backend layout units.</param>
        public TextStudioBackendLayoutData WithAutoFitGlyphMargin(float margin)
        {
            TextStudioBackendLayoutData result = this;
            result.AutoFitGlyphMargin = margin;
            return result;
        }

        /// <summary>Returns a copy with per-unit center-to-baseline offsets.</summary>
        /// <param name="offsets">Caller-owned offsets read during commit.</param>
        public TextStudioBackendLayoutData WithSourceBaselineOffsets(float[] offsets)
        {
            TextStudioBackendLayoutData result = this;
            result.SourceCenterBaselineOffsets = offsets;
            return result;
        }

        /// <summary>Returns a copy with line ranges used by placement strategies.</summary>
        public TextStudioBackendLayoutData WithPlacementLines(LineRange[] lines, int count)
        {
            TextStudioBackendLayoutData result = this;
            result.PlacementLines = lines;
            result.PlacementLineCount = count;
            return result;
        }

        /// <summary>Returns a copy with immutable source color samples and their optional validity metadata.</summary>
        public TextStudioBackendLayoutData WithSourceColors(
            TextStudioGlyphColorQuad[] colors,
            bool[] validity = null,
            TextStudioGlyphColorBounds[] bounds = null)
        {
            TextStudioBackendLayoutData result = this;
            result.SourceColors = colors;
            result.SourceColorValidity = validity;
            result.SourceColorBounds = bounds;
            return result;
        }

        /// <summary>Returns a copy with per-unit source visual bounds for sizing-aware placement and effects.</summary>
        /// <summary>Marks measured source layout by semantic unit. Null means complete coverage.</summary>
        public TextStudioBackendLayoutData WithSourceLayoutValidity(bool[] validity)
        {
            TextStudioBackendLayoutData result = this;
            result.SourceLayoutValidity = validity;
            return result;
        }

        /// <summary>Returns a copy of the layout data with the supplied per-unit native visual bounds.</summary>
        public TextStudioBackendLayoutData WithSourceVisualBounds(
            TextStudioGlyphVisualBounds[] bounds)
        {
            TextStudioBackendLayoutData result = this;
            result.SourceVisualBounds = bounds;
            return result;
        }

        /// <summary>Returns a copy with per-unit scale normalization for renderer-neutral effects.</summary>
        public TextStudioBackendLayoutData WithEffectUnitScales(float[] scales)
        {
            TextStudioBackendLayoutData result = this;
            result.EffectUnitScales = scales;
            return result;
        }

        /// <summary>Returns a copy with mappings from text units to semantic line and word indices.</summary>
        public TextStudioBackendLayoutData WithSemanticMappings(
            int[] textUnitToLineIndex,
            int[] textUnitToWordIndex)
        {
            TextStudioBackendLayoutData result = this;
            result.TextUnitToLineIndex = textUnitToLineIndex;
            result.TextUnitToWordIndex = textUnitToWordIndex;
            return result;
        }

        /// <summary>Returns a copy with normalized source alignment, from bottom-left (0, 0) to top-right (1, 1).</summary>
        public TextStudioBackendLayoutData WithSourceAlignment(Vector2 alignment)
        {
            TextStudioBackendLayoutData result = this;
            result.SourceAlignment = alignment;
            return result;
        }

        /// <summary>Publishes actual draw glyphs in backend order, linked to semantic units through TextUnitIndex.</summary>
        public TextStudioBackendLayoutData WithRenderGlyphs(TextStudioVisualGlyph[] glyphs, int count,
            TextStudioVisualFontSource[] fontSources = null, int fontSourceCount = 0)
        {
            TextStudioBackendLayoutData result = this;
            result.RenderGlyphs = glyphs; result.RenderGlyphCount = count;
            result.FontSources = fontSources; result.FontSourceCount = fontSourceCount;
            return result;
        }

        /// <summary>Returns a copy with renderer-neutral glyph, atlas, and field metadata.</summary>
        public TextStudioBackendLayoutData WithVisualGlyphs(
            TextStudioVisualGlyph[] glyphs,
            TextStudioVisualCapabilities capabilities,
            TextStudioVisualVersion version)
        {
            TextStudioBackendLayoutData result = this;
            result.VisualGlyphs = glyphs;
            result.VisualCapabilities = capabilities;
            result.VisualVersion = version;
            return result;
        }
    }

    /// <summary>
    /// Controlled writer that lets an optional backend publish layout without exposing
    /// Text Studio's mutable layout storage.
    /// </summary>
    public sealed class TextStudioBackendLayoutBuilder
    {
        TextStudioLayout _layout;
        TextStudioCompiledPlan _plan;
        TextStudioSourceContextRequirements _sourceContextRequirements;

        public bool HasCommittedLayout { get; private set; }
        public int ExpectedTextUnitCount => _plan?.TextUnitCount ?? 0;

        internal void Reset(
            TextStudioLayout layout,
            TextStudioCompiledPlan plan,
            TextStudioSourceContextRequirements sourceContextRequirements)
        {
            _layout = layout;
            _plan = plan;
            _sourceContextRequirements = sourceContextRequirements;
            HasCommittedLayout = false;
        }

        /// <summary>Publishes one validated structured layout into Text Studio-owned storage.</summary>
        public bool Commit(in TextStudioBackendLayoutData data)
        {
            return CommitCore(
                data.Positions,
                data.PositionCount,
                data.AutoFitGlyphMargin,
                data.SourceCenterBaselineOffsets,
                data.PlacementLines,
                data.PlacementLineCount,
                data.SourceColors,
                data.SourceColorValidity,
                data.SourceColorBounds,
                data.SourceVisualBounds,
                data.EffectUnitScales,
                data.TextUnitToLineIndex,
                data.TextUnitToWordIndex,
                data.SourceAlignment,
                data.VisualGlyphs,
                data.VisualCapabilities,
                data.VisualVersion, data.RenderGlyphs, data.RenderGlyphCount, data.FontSources, data.FontSourceCount, data.SourceLayoutValidity);
        }

        bool CommitCore(
            Vector3[] positions,
            int count,
            float autoFitGlyphMargin,
            float[] sourceCenterBaselineOffsets,
            LineRange[] placementLines,
            int placementLineCount,
            TextStudioGlyphColorQuad[] sourceColors,
            bool[] sourceColorValidity,
            TextStudioGlyphColorBounds[] sourceColorBounds,
            TextStudioGlyphVisualBounds[] sourceVisualBounds,
            float[] effectUnitScales,
            int[] textUnitToLineIndex,
            int[] textUnitToWordIndex,
            Vector2? sourceAlignment,
            TextStudioVisualGlyph[] visualGlyphs,
            TextStudioVisualCapabilities visualCapabilities,
            TextStudioVisualVersion? visualVersion,
            TextStudioVisualGlyph[] renderGlyphs, int renderGlyphCount, TextStudioVisualFontSource[] fontSources, int fontSourceCount, bool[] sourceLayoutValidity = null)
        {
            if (_layout == null || _plan == null || positions == null)
                return false;
            if (count != _plan.TextUnitCount || positions.Length < count)
                return false;
            if (sourceLayoutValidity != null && sourceLayoutValidity.Length < count) return false;
            if (visualGlyphs != null && visualGlyphs.Length < count)
                return false;
            if (renderGlyphCount < 0 || renderGlyphCount > (renderGlyphs?.Length ?? 0)) return false;
            for (int index = 0; index < renderGlyphCount; index++)
                if (renderGlyphs[index].TextUnitIndex < 0 || renderGlyphs[index].TextUnitIndex >= count) return false;

            _layout.SetFromPositions(
                positions,
                count,
                _plan.LineRanges,
                _plan.LineCount,
                _plan.WordRanges,
                _plan.WordCount,
                autoFitGlyphMargin,
                sourceCenterBaselineOffsets,
                placementLines,
                placementLineCount,
                sourceColors,
                sourceColorValidity,
                sourceColorBounds,
                sourceVisualBounds,
                effectUnitScales,
                textUnitToLineIndex,
                textUnitToWordIndex,
                _sourceContextRequirements,
                sourceAlignment,
                visualGlyphs,
                visualCapabilities,
                visualVersion, fontSources, fontSourceCount, renderGlyphs, renderGlyphCount, sourceLayoutValidity: sourceLayoutValidity);
            HasCommittedLayout = true;
            return true;
        }
    }
}
