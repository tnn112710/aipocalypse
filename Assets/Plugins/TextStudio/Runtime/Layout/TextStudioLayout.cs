using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Alignment modes for text layout.
    /// </summary>
    public enum TextAlignment
    {
        Left,
        Center,
        Right,
        Justified
    }

    /// <summary>
    /// Overflow handling policy.
    /// </summary>
    public enum OverflowPolicy
    {
        Overflow,
        Truncate,
        Ellipsis,
        Wrap
    }

    /// <summary>
    /// Backend-neutral layout configuration.
    /// </summary>
    [Serializable]
    public struct LayoutSettings
    {
        public TextAlignment alignment;
        public OverflowPolicy overflow;
        public float lineSpacing;
        public float maxWidth;
        public float characterSpacing;

        public static LayoutSettings Default => new LayoutSettings
        {
            alignment = TextAlignment.Left,
            overflow = OverflowPolicy.Overflow,
            lineSpacing = 1.2f,
            maxWidth = 0f,
            characterSpacing = 0f
        };
    }

    /// <summary>
    /// Per-glyph layout data produced by the layout engine.
    /// Backend-neutral: TMP and UITK adapters translate their layout to this model.
    /// </summary>
    public struct GlyphLayoutInfo
    {
        public Vector2 position;
        public float advance;
        public float ascender;
        public float descender;
        public float baseline;
        public int lineIndex;
        public int wordIndex;
        public bool isVisible;
    }

    /// <summary>
    /// Backend-neutral layout engine.
    /// Normalizes glyph positions from any text backend into a unified coordinate space
    /// for placement and animation processing.
    /// </summary>
    internal sealed class TextStudioLayout
    {
        GlyphLayoutInfo[] _glyphLayouts;
        Vector3[] _basePositions;
        float[] _sourceCenterBaselineOffsets;
        TextStudioGlyphColorQuad[] _sourceColors;
        bool[] _sourceColorValidity;
        TextStudioGlyphColorBounds[] _sourceColorBounds;
        TextStudioGlyphVisualBounds[] _sourceVisualBounds;
        Vector3[] _sourceTextNormalizedPositions;
        float[] _effectUnitScales;
        TextStudioVisualGlyph[] _visualGlyphs;
        TextStudioVisualGlyph[] _renderGlyphs;
        int _renderGlyphCount;
        int[] _unitToRenderGlyph;
        bool _hasRenderGlyphs;
        TextStudioVisualFontSource[] _fontSources;
        int _fontSourceCount;
        TextStudioColorSource[] _colorSources;
        int _colorSourceCount;
        LineRange[] _placementLineRanges;
        int _placementLineCount;
        int _count;
        float _autoFitGlyphMargin;
        bool _hasSourceCenterBaselineOffsets;
        Vector2 _sourceAlignment = new Vector2(0.5f, 0.5f);
        Vector4 _sourceTextBounds;
        TextStudioVisualCapabilities _visualCapabilities;
        TextStudioVisualVersion _visualVersion;
        int _revision;

        public ReadOnlySpan<GlyphLayoutInfo> GlyphLayouts => new ReadOnlySpan<GlyphLayoutInfo>(_glyphLayouts, 0, _count);
        public Vector3[] BasePositions => _basePositions;
        public float[] SourceCenterBaselineOffsets => _sourceCenterBaselineOffsets;
        public TextStudioGlyphColorQuad[] SourceColors => _sourceColors;
        public bool[] SourceColorValidity => _sourceColorValidity;
        public TextStudioGlyphColorBounds[] SourceColorBounds => _sourceColorBounds;
        public TextStudioGlyphVisualBounds[] SourceVisualBounds => _sourceVisualBounds;
        bool[] _sourceLayoutValidity;
        bool _hasPartialSourceLayout;
        public bool[] SourceLayoutValidity => _hasPartialSourceLayout ? _sourceLayoutValidity : null;
        public bool HasSourceLayout(int unit) => (uint)unit < (uint)_count && (!_hasPartialSourceLayout || _sourceLayoutValidity[unit]);
        public Vector3[] SourceTextNormalizedPositions => _sourceTextNormalizedPositions;
        public float[] EffectUnitScales => _effectUnitScales;
        public TextStudioVisualGlyph[] VisualGlyphs => _visualGlyphs;
        public TextStudioVisualGlyph[] RenderGlyphs => _hasRenderGlyphs ? _renderGlyphs : _visualGlyphs;
        public int RenderGlyphCount => _hasRenderGlyphs ? _renderGlyphCount : _count;
        public int[] UnitToRenderGlyph => _hasRenderGlyphs ? _unitToRenderGlyph : null;

        public TextStudioVisualGlyph[] EnsureRenderGlyphCapacity(int count)
        {
            if (_renderGlyphs == null || _renderGlyphs.Length < count)
                _renderGlyphs = new TextStudioVisualGlyph[TextStudioBufferCapacity.ForCount(count)];
            return _renderGlyphs;
        }

        void SetRenderGlyphs(TextStudioVisualGlyph[] glyphs, int count)
        {
            _hasRenderGlyphs = glyphs != null;
            _renderGlyphCount = _hasRenderGlyphs ? count : 0;
            if (!_hasRenderGlyphs) return;
            if (count < 0 || count > glyphs.Length) throw new ArgumentOutOfRangeException(nameof(count));
            EnsureRenderGlyphCapacity(count);
            if (!ReferenceEquals(glyphs, _renderGlyphs)) Array.Copy(glyphs, _renderGlyphs, count);
            if (_unitToRenderGlyph == null || _unitToRenderGlyph.Length < _count)
                _unitToRenderGlyph = new int[TextStudioBufferCapacity.ForCount(_count)];
            Array.Fill(_unitToRenderGlyph, -1);
            for (int index = 0; index < count; index++)
            {
                int unit = glyphs[index].TextUnitIndex;
                if (unit < 0 || unit >= _count) throw new ArgumentException("A rendered glyph must belong to a published text unit.", nameof(glyphs));
                if (_unitToRenderGlyph[unit] < 0) _unitToRenderGlyph[unit] = index;
            }
        }
        public TextStudioVisualFontSource[] FontSources => _fontSources;
        public int FontSourceCount => _fontSourceCount;
        public TextStudioColorSource[] ColorSources => _colorSources;
        public int ColorSourceCount => _colorSourceCount;
        public TextStudioVisualCapabilities VisualCapabilities => _visualCapabilities;
        public TextStudioVisualVersion VisualVersion => _visualVersion;
        public LineRange[] PlacementLineRanges => _placementLineRanges;
        public int PlacementLineCount => _placementLineCount;
        public int GlyphCount => _count;
        public float AutoFitGlyphMargin => _autoFitGlyphMargin;
        public bool HasSourceCenterBaselineOffsets => _hasSourceCenterBaselineOffsets;
        public Vector2 SourceAlignment => _sourceAlignment;
        public Vector4 SourceTextBounds => _sourceTextBounds;
        public int Revision => _revision;

        public void EnsureCapacity(int count)
        {
            if (_glyphLayouts == null || _glyphLayouts.Length < count)
            {
                _glyphLayouts = new GlyphLayoutInfo[TextStudioBufferCapacity.ForCount(count)];
                _basePositions = new Vector3[_glyphLayouts.Length];
                _sourceCenterBaselineOffsets = new float[_glyphLayouts.Length];
                _sourceColors = new TextStudioGlyphColorQuad[_glyphLayouts.Length];
                _sourceColorValidity = new bool[_glyphLayouts.Length];
                _sourceColorBounds = new TextStudioGlyphColorBounds[_glyphLayouts.Length];
                _sourceVisualBounds = new TextStudioGlyphVisualBounds[_glyphLayouts.Length];
                _sourceTextNormalizedPositions = new Vector3[_glyphLayouts.Length];
                _effectUnitScales = new float[_glyphLayouts.Length];
                _visualGlyphs = new TextStudioVisualGlyph[_glyphLayouts.Length];
            }
            else if (_sourceCenterBaselineOffsets == null || _sourceCenterBaselineOffsets.Length < _glyphLayouts.Length)
            {
                _sourceCenterBaselineOffsets = new float[_glyphLayouts.Length];
            }
            if (_sourceColors == null || _sourceColors.Length < _glyphLayouts.Length)
                _sourceColors = new TextStudioGlyphColorQuad[_glyphLayouts.Length];
            if (_sourceColorValidity == null || _sourceColorValidity.Length < _glyphLayouts.Length)
                _sourceColorValidity = new bool[_glyphLayouts.Length];
            if (_sourceColorBounds == null || _sourceColorBounds.Length < _glyphLayouts.Length)
                _sourceColorBounds = new TextStudioGlyphColorBounds[_glyphLayouts.Length];
            if (_sourceVisualBounds == null || _sourceVisualBounds.Length < _glyphLayouts.Length)
                _sourceVisualBounds = new TextStudioGlyphVisualBounds[_glyphLayouts.Length];
            if (_sourceTextNormalizedPositions == null ||
                _sourceTextNormalizedPositions.Length < _glyphLayouts.Length)
            {
                _sourceTextNormalizedPositions = new Vector3[_glyphLayouts.Length];
            }
            if (_effectUnitScales == null || _effectUnitScales.Length < _glyphLayouts.Length)
                _effectUnitScales = new float[_glyphLayouts.Length];
            if (_visualGlyphs == null || _visualGlyphs.Length < _glyphLayouts.Length)
                _visualGlyphs = new TextStudioVisualGlyph[_glyphLayouts.Length];
            _count = count;
        }

        /// <summary>
        /// Populate layout from explicit positions (used by TMP adapter after TMP layout).
        /// </summary>
        public void SetFromPositions(
            Vector3[] positions,
            int count,
            LineRange[] lines,
            int lineCount,
            WordRange[] words,
            int wordCount,
            float autoFitGlyphMargin = 0f,
            float[] sourceCenterBaselineOffsets = null,
            LineRange[] placementLines = null,
            int placementLineCount = 0,
            TextStudioGlyphColorQuad[] sourceColors = null,
            bool[] sourceColorValidity = null,
            TextStudioGlyphColorBounds[] sourceColorBounds = null,
            TextStudioGlyphVisualBounds[] sourceVisualBounds = null,
            float[] effectUnitScales = null,
            int[] glyphToLineIndex = null,
            int[] glyphToWordIndex = null,
            TextStudioSourceContextRequirements sourceContextRequirements =
                TextStudioSourceContextRequirements.None,
            Vector2? sourceAlignment = null,
            TextStudioVisualGlyph[] visualGlyphs = null,
            TextStudioVisualCapabilities visualCapabilities = TextStudioVisualCapabilities.None,
            TextStudioVisualVersion? visualVersion = null,
            TextStudioVisualFontSource[] fontSources = null,
            int fontSourceCount = 0,
            TextStudioVisualGlyph[] renderGlyphs = null, int renderGlyphCount = 0,
            TextStudioColorSource[] colorSources = null, int colorSourceCount = 0, bool[] sourceLayoutValidity = null)
        {
            EnsureCapacity(count);
            _hasPartialSourceLayout = sourceLayoutValidity != null;
            if (_hasPartialSourceLayout)
            {
                if (_sourceLayoutValidity == null || _sourceLayoutValidity.Length < count)
                    _sourceLayoutValidity = new bool[TextStudioBufferCapacity.ForCount(count)];
                Array.Copy(sourceLayoutValidity, _sourceLayoutValidity, count);
            }
            SetRenderGlyphs(renderGlyphs, renderGlyphCount);
            _colorSourceCount = colorSources == null ? 0 : Math.Min(Math.Max(0, colorSourceCount), colorSources.Length);
            if (_colorSourceCount > 0 && (_colorSources == null || _colorSources.Length < _colorSourceCount))
                _colorSources = new TextStudioColorSource[TextStudioBufferCapacity.ForCount(_colorSourceCount)];
            if (_colorSources != null && !ReferenceEquals(colorSources, _colorSources))
            {
                Array.Clear(_colorSources, 0, _colorSources.Length);
                if (_colorSourceCount > 0) Array.Copy(colorSources, _colorSources, _colorSourceCount);
            }
            _fontSourceCount = fontSources == null ? 0 : Math.Min(Math.Max(0, fontSourceCount), fontSources.Length);
            if (_fontSourceCount > 0 && (_fontSources == null || _fontSources.Length < _fontSourceCount))
                _fontSources = new TextStudioVisualFontSource[TextStudioBufferCapacity.ForCount(_fontSourceCount)];
            if (_fontSources != null && !ReferenceEquals(fontSources, _fontSources))
            {
                Array.Clear(_fontSources, 0, _fontSources.Length);
                if (_fontSourceCount > 0) Array.Copy(fontSources, _fontSources, _fontSourceCount);
            }
            _autoFitGlyphMargin = Mathf.Max(0f, autoFitGlyphMargin);
            _hasSourceCenterBaselineOffsets = sourceCenterBaselineOffsets != null;
            _sourceAlignment = sourceAlignment ?? new Vector2(0.5f, 0.5f);
            bool positionsOwnedByLayout = ReferenceEquals(positions, _basePositions);
            bool baselinesOwnedByLayout = ReferenceEquals(
                sourceCenterBaselineOffsets,
                _sourceCenterBaselineOffsets);
            bool colorsOwnedByLayout = ReferenceEquals(sourceColors, _sourceColors);
            bool colorValidityOwnedByLayout = ReferenceEquals(sourceColorValidity, _sourceColorValidity);
            bool colorBoundsOwnedByLayout = ReferenceEquals(sourceColorBounds, _sourceColorBounds);
            bool visualBoundsOwnedByLayout = ReferenceEquals(sourceVisualBounds, _sourceVisualBounds);
            bool scalesOwnedByLayout = ReferenceEquals(effectUnitScales, _effectUnitScales);
            bool visualGlyphsOwnedByLayout = ReferenceEquals(visualGlyphs, _visualGlyphs);
            for (int i = 0; i < count; i++)
            {
                if (!positionsOwnedByLayout)
                    _basePositions[i] = positions[i];
                if (!baselinesOwnedByLayout)
                {
                    _sourceCenterBaselineOffsets[i] =
                        sourceCenterBaselineOffsets != null && i < sourceCenterBaselineOffsets.Length
                            ? sourceCenterBaselineOffsets[i]
                            : 0f;
                }
                if (!colorsOwnedByLayout)
                {
                    _sourceColors[i] = sourceColors != null && i < sourceColors.Length
                        ? sourceColors[i]
                        : TextStudioGlyphColorQuad.White;
                }
                if (!colorValidityOwnedByLayout)
                {
                    _sourceColorValidity[i] = sourceColorValidity != null &&
                                              i < sourceColorValidity.Length &&
                                              sourceColorValidity[i];
                }
                if (!colorBoundsOwnedByLayout)
                {
                    _sourceColorBounds[i] = sourceColorBounds != null && i < sourceColorBounds.Length
                        ? sourceColorBounds[i]
                        : default;
                }
                if (!visualBoundsOwnedByLayout)
                {
                    _sourceVisualBounds[i] = sourceVisualBounds != null &&
                                             i < sourceVisualBounds.Length
                        ? sourceVisualBounds[i]
                        : new TextStudioGlyphVisualBounds(
                            new Vector2(positions[i].x, positions[i].y),
                            new Vector2(positions[i].x, positions[i].y),
                            true);
                }
                if (!scalesOwnedByLayout)
                {
                    _effectUnitScales[i] = effectUnitScales != null && i < effectUnitScales.Length
                        ? Mathf.Max(0.0001f, effectUnitScales[i])
                        : 1f;
                }
                _glyphLayouts[i] = new GlyphLayoutInfo
                {
                    position = new Vector2(positions[i].x, positions[i].y),
                    advance = i + 1 < count ? positions[i + 1].x - positions[i].x : 0f,
                    lineIndex = glyphToLineIndex != null && i < glyphToLineIndex.Length
                        ? glyphToLineIndex[i]
                        : TextSegmenter.GetLineIndex(lines, lineCount, i),
                    wordIndex = glyphToWordIndex != null && i < glyphToWordIndex.Length
                        ? glyphToWordIndex[i]
                        : TextSegmenter.GetWordIndex(words, wordCount, i),
                    isVisible = HasSourceLayout(i)
                };
                if (!HasSourceLayout(i)) _sourceVisualBounds[i] = default;
                if (visualGlyphs != null && !visualGlyphsOwnedByLayout && i < visualGlyphs.Length)
                    _visualGlyphs[i] = visualGlyphs[i];
            }

            if (placementLines != null && placementLineCount > 0)
                CopyPlacementLines(placementLines, placementLineCount);
            else
                CopyPlacementLines(lines, lineCount);

            RebuildSourceContext(sourceContextRequirements);
            if (visualGlyphs == null)
                BuildFallbackVisualGlyphs();
            _visualCapabilities = visualGlyphs != null
                ? visualCapabilities
                : FallbackVisualCapabilities;
            _visualVersion = visualVersion ?? new TextStudioVisualVersion(
                unchecked((uint)_revision),
                unchecked((uint)_revision),
                0u,
                0u);
        }

        /// <summary>
        /// Generate simple linear layout for standalone use (no TMP/UITK backend), using
        /// font metrics from the supplied <see cref="IGlyphMetricsProvider"/> for per-glyph
        /// advance widths and pair kerning. Falls back to <paramref name="fallbackCharWidth"/>
        /// when the provider returns 0 (missing glyph) or is <c>null</c>.
        /// </summary>
        public void GenerateLinearLayout(TextStudioCompiledPlan plan, LayoutSettings settings,
            IGlyphMetricsProvider metrics, float fallbackCharWidth = 0.5f)
        {
            int count = plan.VisibleGlyphCount;
            EnsureCapacity(count);
            _hasPartialSourceLayout = false;
            _autoFitGlyphMargin = 0f;
            _sourceAlignment = new Vector2(ResolveHorizontalAlignment(settings.alignment), 1f);

            float x = 0f;
            float y = 0f;
            int lineIdx = 0;
            float spacingBonus = settings.characterSpacing;

            for (int i = 0; i < count; i++)
            {
                _sourceColors[i] = TextStudioGlyphColorQuad.White;
                _sourceColorValidity[i] = false;
                _sourceColorBounds[i] = default;
                _effectUnitScales[i] = 1f;
                char c = plan.GetPrimaryChar(i);
                if (c == '\n')
                {
                    x = 0f;
                    y -= settings.lineSpacing;
                    lineIdx++;
                    _glyphLayouts[i] = new GlyphLayoutInfo
                    {
                        position = new Vector2(x, y),
                        advance = 0f,
                        lineIndex = lineIdx,
                        wordIndex = TextSegmenter.GetWordIndex(plan.WordRanges, plan.WordCount, i),
                        isVisible = false
                    };
                    _basePositions[i] = new Vector3(x, y, 0f);
                    _sourceCenterBaselineOffsets[i] = 0f;
                    _sourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                        new Vector2(x, y),
                        new Vector2(x, y),
                        false);
                    continue;
                }

                float glyphAdvance = metrics != null ? metrics.GetAdvance(c) : 0f;
                if (glyphAdvance <= 0f) glyphAdvance = fallbackCharWidth;

                // Kerning applies to the boundary between i-1 and i (adjusts this glyph's
                // left edge, not its advance). Skip at line starts where no previous glyph exists.
                if (metrics != null && i > 0 && !plan.IsLineBreakUnit(i - 1))
                {
                    x += metrics.GetKerning(plan.GetPrimaryChar(i - 1), c);
                }

                float cellAdvance = glyphAdvance + spacingBonus;

                // Wrap if maxWidth is set. Only break on whitespace to keep words intact.
                if (settings.maxWidth > 0f && x + cellAdvance > settings.maxWidth && c == ' ')
                {
                    x = 0f;
                    y -= settings.lineSpacing;
                    lineIdx++;
                }

                _glyphLayouts[i] = new GlyphLayoutInfo
                {
                    position = new Vector2(x, y),
                    advance = cellAdvance,
                    baseline = y,
                    lineIndex = lineIdx,
                    wordIndex = TextSegmenter.GetWordIndex(plan.WordRanges, plan.WordCount, i),
                    isVisible = !plan.IsWhiteSpaceUnit(i)
                };
                _basePositions[i] = new Vector3(x, y, 0f);
                _sourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                    new Vector2(x, y + _glyphLayouts[i].descender),
                    new Vector2(x + cellAdvance, y + _glyphLayouts[i].ascender),
                    _glyphLayouts[i].isVisible);
                _sourceCenterBaselineOffsets[i] = 0f;
                x += cellAdvance;
            }

            if (settings.alignment != TextAlignment.Left && plan.LineCount > 0)
            {
                ApplyAlignment(plan.LineRanges, plan.LineCount, settings);
            }

            BuildPlacementLinesFromLayoutInfo(plan);
            RebuildSourceContext(plan.SourceContextRequirements);
            BuildFallbackVisualGlyphs();
            SetFallbackVisualMetadata();
        }

        /// <summary>
        /// Generate simple linear layout for standalone use (no TMP/UITK backend).
        /// Characters are placed sequentially with uniform spacing.
        /// </summary>
        public void GenerateLinearLayout(TextStudioCompiledPlan plan, LayoutSettings settings, float charWidth = 0.5f)
        {
            int count = plan.VisibleGlyphCount;
            EnsureCapacity(count);
            _hasPartialSourceLayout = false;
            _autoFitGlyphMargin = 0f;
            _sourceAlignment = new Vector2(ResolveHorizontalAlignment(settings.alignment), 1f);

            float x = 0f;
            float y = 0f;
            int lineIdx = 0;
            float spacing = charWidth + settings.characterSpacing;

            for (int i = 0; i < count; i++)
            {
                _sourceColors[i] = TextStudioGlyphColorQuad.White;
                _sourceColorValidity[i] = false;
                _sourceColorBounds[i] = default;
                _effectUnitScales[i] = 1f;
                if (plan.IsLineBreakUnit(i))
                {
                    x = 0f;
                    y -= settings.lineSpacing;
                    lineIdx++;
                    _glyphLayouts[i] = new GlyphLayoutInfo
                    {
                        position = new Vector2(x, y),
                        advance = 0f,
                        lineIndex = lineIdx,
                        wordIndex = TextSegmenter.GetWordIndex(plan.WordRanges, plan.WordCount, i),
                        isVisible = false
                    };
                    _basePositions[i] = new Vector3(x, y, 0f);
                    _sourceCenterBaselineOffsets[i] = 0f;
                    _sourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                        new Vector2(x, y),
                        new Vector2(x, y),
                        false);
                    continue;
                }

                // Wrap if maxWidth is set
                if (settings.maxWidth > 0f && x + spacing > settings.maxWidth && plan.GetPrimaryChar(i) == ' ')
                {
                    x = 0f;
                    y -= settings.lineSpacing;
                    lineIdx++;
                }

                _glyphLayouts[i] = new GlyphLayoutInfo
                {
                    position = new Vector2(x, y),
                    advance = spacing,
                    baseline = y,
                    lineIndex = lineIdx,
                    wordIndex = TextSegmenter.GetWordIndex(plan.WordRanges, plan.WordCount, i),
                    isVisible = !plan.IsWhiteSpaceUnit(i)
                };
                _basePositions[i] = new Vector3(x, y, 0f);
                _sourceVisualBounds[i] = new TextStudioGlyphVisualBounds(
                    new Vector2(x, y - settings.lineSpacing * 0.5f),
                    new Vector2(x + spacing, y + settings.lineSpacing * 0.5f),
                    _glyphLayouts[i].isVisible);
                _sourceCenterBaselineOffsets[i] = 0f;
                x += spacing;
            }

            // Apply alignment
            if (settings.alignment != TextAlignment.Left && plan.LineCount > 0)
            {
                ApplyAlignment(plan.LineRanges, plan.LineCount, settings);
            }

            BuildPlacementLinesFromLayoutInfo(plan);
            RebuildSourceContext(plan.SourceContextRequirements);
            BuildFallbackVisualGlyphs();
            SetFallbackVisualMetadata();
        }

        void RebuildSourceContext(TextStudioSourceContextRequirements requirements)
        {
            _revision++;
            if (_count <= 0)
            {
                _sourceTextBounds = Vector4.zero;
                return;
            }

            bool needsNormalized =
                (requirements & TextStudioSourceContextRequirements.SourceTextNormalizedPosition) != 0;

            bool hasVisibleBounds = false;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < _count; i++)
            {
                if (!HasSourceLayout(i)) continue;
                TextStudioGlyphVisualBounds bounds = _sourceVisualBounds[i];
                if (!bounds.IsValid || !bounds.IsVisible)
                    continue;
                minimum = Vector2.Min(minimum, bounds.Minimum);
                maximum = Vector2.Max(maximum, bounds.Maximum);
                hasVisibleBounds = true;
            }

            if (!hasVisibleBounds)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!HasSourceLayout(i)) continue;
                    TextStudioGlyphVisualBounds bounds = _sourceVisualBounds[i];
                    if (!bounds.IsValid)
                        continue;
                    minimum = Vector2.Min(minimum, bounds.Minimum);
                    maximum = Vector2.Max(maximum, bounds.Maximum);
                    hasVisibleBounds = true;
                }
            }

            if (!hasVisibleBounds)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!HasSourceLayout(i)) continue;
                    Vector2 position = _basePositions[i];
                    minimum = Vector2.Min(minimum, position);
                    maximum = Vector2.Max(maximum, position);
                    hasVisibleBounds = true;
                }
            }
            if (!hasVisibleBounds) minimum = maximum = Vector2.zero;

            Vector2 size = maximum - minimum;
            if (needsNormalized)
            {
                bool hasWidth = Mathf.Abs(size.x) > 0.0001f;
                bool hasHeight = Mathf.Abs(size.y) > 0.0001f;
                for (int i = 0; i < _count; i++)
                {
                    if (!HasSourceLayout(i)) { _sourceTextNormalizedPositions[i] = Vector3.zero; continue; }
                    Vector2 center = _sourceVisualBounds[i].IsValid
                        ? _sourceVisualBounds[i].Center
                        : (Vector2)_basePositions[i];
                    _sourceTextNormalizedPositions[i] = new Vector3(
                        hasWidth ? Mathf.Clamp01((center.x - minimum.x) / size.x) : 0.5f,
                        hasHeight ? Mathf.Clamp01((center.y - minimum.y) / size.y) : 0.5f,
                        0f);
                }
            }
            _sourceTextBounds = new Vector4(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        static TextStudioVisualCapabilities FallbackVisualCapabilities =>
            TextStudioVisualCapabilities.LogicalVisualMapping |
            TextStudioVisualCapabilities.ClusterMapping |
            TextStudioVisualCapabilities.LineAndWordMapping |
            TextStudioVisualCapabilities.GlyphMetrics |
            TextStudioVisualCapabilities.VisualBounds |
            TextStudioVisualCapabilities.Orientation;

        void BuildFallbackVisualGlyphs()
        {
            _colorSourceCount = 0;
            if (_colorSources != null) Array.Clear(_colorSources, 0, _colorSources.Length);
            _hasRenderGlyphs = false;
            _renderGlyphCount = 0;
            _fontSourceCount = 0;
            if (_fontSources != null) Array.Clear(_fontSources, 0, _fontSources.Length);
            for (int i = 0; i < _count; i++)
            {
                GlyphLayoutInfo layout = _glyphLayouts[i];
                TextStudioGlyphVisualBounds bounds = _sourceVisualBounds[i];
                Vector2 minimum = bounds.IsValid ? bounds.Minimum : layout.position;
                Vector2 maximum = bounds.IsValid ? bounds.Maximum : layout.position;
                _visualGlyphs[i] = new TextStudioVisualGlyph(
                    0UL,
                    0u,
                    i,
                    i,
                    i,
                    i,
                    1,
                    layout.lineIndex,
                    layout.wordIndex,
                    layout.position,
                    layout.advance,
                    layout.baseline,
                    minimum,
                    maximum,
                    Vector2.right,
                    Vector4.zero,
                    0,
                    0f,
                    0f,
                    layout.isVisible);
            }
        }

        void SetFallbackVisualMetadata()
        {
            _hasPartialSourceLayout = false;
            _colorSources = null;
            _colorSourceCount = 0;
            _visualCapabilities = FallbackVisualCapabilities;
            _fontSources = null;
            _fontSourceCount = 0;
            _visualVersion = new TextStudioVisualVersion(
                unchecked((uint)_revision),
                unchecked((uint)_revision),
                0u,
                0u);
        }

        void CopyPlacementLines(LineRange[] lines, int lineCount)
        {
            if (lines == null || lineCount <= 0)
            {
                _placementLineCount = 0;
                return;
            }

            if (_placementLineRanges == null || _placementLineRanges.Length < lineCount)
                _placementLineRanges = new LineRange[Mathf.Max(lineCount, 8)];

            _placementLineCount = 0;
            for (int i = 0; i < lineCount; i++)
            {
                LineRange line = lines[i];
                int start = Mathf.Clamp(line.glyphStart, 0, _count);
                int count = Mathf.Clamp(line.glyphCount, 0, _count - start);
                if (count <= 0)
                    continue;

                line.glyphStart = start;
                line.glyphCount = count;
                line.lineIndex = _placementLineCount;
                _placementLineRanges[_placementLineCount++] = line;
            }
        }

        void BuildPlacementLinesFromLayoutInfo(TextStudioCompiledPlan plan)
        {
            if (_count <= 0)
            {
                _placementLineCount = 0;
                return;
            }

            if (_placementLineRanges == null || _placementLineRanges.Length < _count)
                _placementLineRanges = new LineRange[Mathf.Max(_count, 8)];

            _placementLineCount = 0;
            int activeStart = -1;
            int activeLayoutLine = 0;
            for (int i = 0; i < _count; i++)
            {
                if (plan != null && plan.IsLineBreakUnit(i))
                {
                    AddActivePlacementLine(activeStart, i);
                    activeStart = -1;
                    continue;
                }

                int layoutLine = _glyphLayouts[i].lineIndex;
                if (activeStart < 0)
                {
                    activeStart = i;
                    activeLayoutLine = layoutLine;
                }
                else if (layoutLine != activeLayoutLine)
                {
                    AddActivePlacementLine(activeStart, i);
                    activeStart = i;
                    activeLayoutLine = layoutLine;
                }
            }

            AddActivePlacementLine(activeStart, _count);
        }

        void AddActivePlacementLine(int start, int end)
        {
            if (start < 0 || end <= start)
                return;

            if (_placementLineRanges == null || _placementLineRanges.Length <= _placementLineCount)
            {
                int newSize = _placementLineRanges == null
                    ? 8
                    : Mathf.Max(_placementLineRanges.Length * 2, _placementLineCount + 1);
                Array.Resize(ref _placementLineRanges, newSize);
            }

            _placementLineRanges[_placementLineCount] = new LineRange
            {
                glyphStart = start,
                glyphCount = end - start,
                lineIndex = _placementLineCount,
                width = 0f,
                baseline = _basePositions[start].y
            };
            _placementLineCount++;
        }

        void ApplyAlignment(LineRange[] lines, int lineCount, LayoutSettings settings)
        {
            // When the user hasn't set an explicit maxWidth we fall back to the widest
            // line in the paragraph as the reference frame. If we used each line's own
            // width instead (the obvious-but-wrong choice) every line's offset would
            // evaluate to zero and Center/Right would silently behave like Left - the
            // longest line's width is what the shorter lines need to slide inside of.
            float referenceWidth = settings.maxWidth;
            if (referenceWidth <= 0f)
            {
                for (int li = 0; li < lineCount; li++)
                {
                    ref var line = ref lines[li];
                    if (line.glyphCount == 0) continue;
                    int lastGlyph = line.glyphStart + line.glyphCount - 1;
                    float w = _glyphLayouts[lastGlyph].position.x + _glyphLayouts[lastGlyph].advance;
                    if (w > referenceWidth) referenceWidth = w;
                }
            }

            if (referenceWidth <= 0f) return;

            for (int li = 0; li < lineCount; li++)
            {
                ref var line = ref lines[li];
                if (line.glyphCount == 0) continue;

                int lastGlyph = line.glyphStart + line.glyphCount - 1;
                float lineWidth = _glyphLayouts[lastGlyph].position.x + _glyphLayouts[lastGlyph].advance;

                float offset = settings.alignment switch
                {
                    TextAlignment.Center => (referenceWidth - lineWidth) * 0.5f,
                    TextAlignment.Right => referenceWidth - lineWidth,
                    _ => 0f
                };

                if (Mathf.Abs(offset) >= 0.001f)
                {
                    for (int i = line.glyphStart; i < line.glyphStart + line.glyphCount; i++)
                    {
                        var layout = _glyphLayouts[i];
                        layout.position.x += offset;
                        _glyphLayouts[i] = layout;
                        _basePositions[i].x += offset;
                    }
                }

                line.width = lineWidth;
            }
        }

        static float ResolveHorizontalAlignment(TextAlignment alignment)
        {
            return alignment switch
            {
                TextAlignment.Center => 0.5f,
                TextAlignment.Right => 1f,
                _ => 0f
            };
        }
    }
}
