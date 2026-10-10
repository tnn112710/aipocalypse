using System;
using System.Collections.Generic;
using System.Globalization;
using ImpossibleRobert.Common;
using UnityEngine;
using UnityEngine.UI;

namespace TextStudio
{
    internal sealed class TextStudioUnityUITextLayout
    {
        Vector3[] _positions = Array.Empty<Vector3>();
        float[] _baselineOffsets = Array.Empty<float>();
        TextStudioGlyphColorQuad[] _sourceColors =
            Array.Empty<TextStudioGlyphColorQuad>();
        bool[] _sourceColorValidity = Array.Empty<bool>();
        TextStudioGlyphColorBounds[] _sourceColorBounds =
            Array.Empty<TextStudioGlyphColorBounds>();
        TextStudioGlyphVisualBounds[] _sourceVisualBounds =
            Array.Empty<TextStudioGlyphVisualBounds>();
        float[] _effectUnitScales = Array.Empty<float>();
        int[] _textUnitToLine = Array.Empty<int>();
        int[] _textUnitToWord = Array.Empty<int>();
        TextStudioVisualGlyph[] _visualGlyphs =
            Array.Empty<TextStudioVisualGlyph>();

        bool[] _unitHasPosition = Array.Empty<bool>();
        bool[] _unitHasBounds = Array.Empty<bool>();
        Vector2[] _unitMinimum = Array.Empty<Vector2>();
        Vector2[] _unitMaximum = Array.Empty<Vector2>();
        float[] _unitBaseline = Array.Empty<float>();
        float[] _unitAdvance = Array.Empty<float>();
        Vector4[] _unitAtlasUvRect = Array.Empty<Vector4>();
        int[] _unitFirstVisualIndex = Array.Empty<int>();

        int[] _quadToTextUnit = Array.Empty<int>();
        LineRange[] _placementLines = Array.Empty<LineRange>();
        int _quadCount;
        uint _layoutRevision;
        uint _geometryRevision;
        uint _atlasRevision;
        uint _materialRevision;
        Material _lastMaterial;

        public int QuadCount => _quadCount;

        public int GetTextUnitForQuad(int quadIndex)
        {
            return (uint)quadIndex < (uint)_quadCount
                ? _quadToTextUnit[quadIndex]
                : -1;
        }

        public bool TryBuild(
            Text target,
            in TextStudioBackendLayoutRequest request,
            TextStudioBackendLayoutBuilder builder,
            string pushedText,
            int[] pushedCharacterToTextUnit,
            int pushedCharacterCount)
        {
            TextStudioPlanView plan = request.Plan;
            if (target == null || target.font == null || !plan.IsCreated)
                return false;

            string sourceText = target.text ?? string.Empty;
            TextGenerationSettings settings =
                target.GetGenerationSettings(target.rectTransform.rect.size);
            TextGenerator generator = target.cachedTextGenerator;
            if (!generator.Populate(sourceText, settings) && sourceText.Length > 0)
                return false;

            IList<UIVertex> generatedVertices = generator.verts;
            if (generatedVertices == null || (generatedVertices.Count & 3) != 0)
                return false;

            int textUnitCount = plan.TextUnitCount;
            EnsureUnitCapacity(textUnitCount);
            ResetUnits(plan, textUnitCount);

            CharacterMap map = ResolveCharacterMap(
                plan,
                sourceText,
                pushedText,
                pushedCharacterToTextUnit,
                pushedCharacterCount);
            int candidateQuadCount = CountQuadCandidates(sourceText, in map);
            int generatedQuadCount = generatedVertices.Count / 4;
            generatedQuadCount = RemoveCompatibilitySentinel(
                generatedVertices,
                generatedQuadCount,
                candidateQuadCount);
            if (generatedQuadCount > candidateQuadCount)
                return false;

            EnsureQuadCapacity(generatedQuadCount);
            BuildQuadMap(
                sourceText,
                in map,
                generatedQuadCount);

            float unitsPerPixel = target.pixelsPerUnit > 0f
                ? 1f / target.pixelsPerUnit
                : 1f;
            Vector2 roundingOffset = ResolveRoundingOffset(
                target,
                generatedVertices,
                generatedQuadCount,
                unitsPerPixel);
            float defaultEffectUnitScale = Mathf.Max(
                0.0001f,
                generator.fontSizeUsedForBestFit > 0
                    ? generator.fontSizeUsedForBestFit * unitsPerPixel
                    : target.fontSize * unitsPerPixel);

            BuildCharacterLayout(
                generator,
                plan,
                sourceText,
                in map,
                textUnitCount,
                unitsPerPixel,
                roundingOffset,
                defaultEffectUnitScale);
            BuildVisibleQuadLayout(
                generatedVertices,
                generatedQuadCount,
                unitsPerPixel,
                roundingOffset,
                defaultEffectUnitScale);
            FinalizeUnitLayout(textUnitCount, defaultEffectUnitScale);
            int placementLineCount = BuildPlacementLines(textUnitCount);
            BuildVisualGlyphs(target, plan, textUnitCount);

            Increment(ref _layoutRevision);
            Increment(ref _geometryRevision);
            if (!ReferenceEquals(_lastMaterial, target.material))
            {
                _lastMaterial = target.material;
                Increment(ref _materialRevision);
            }

            TextStudioVisualCapabilities visualCapabilities =
                TextStudioVisualCapabilities.LogicalVisualMapping |
                TextStudioVisualCapabilities.ClusterMapping |
                TextStudioVisualCapabilities.LineAndWordMapping |
                TextStudioVisualCapabilities.GlyphMetrics |
                TextStudioVisualCapabilities.VisualBounds |
                TextStudioVisualCapabilities.Orientation |
                TextStudioVisualCapabilities.AtlasCoordinates |
                TextStudioVisualCapabilities.AtlasPages |
                TextStudioVisualCapabilities.SourceVertexColors |
                TextStudioVisualCapabilities.SourceColorBounds;
            TextStudioVisualVersion version = new TextStudioVisualVersion(
                _layoutRevision,
                _geometryRevision,
                _atlasRevision,
                _materialRevision);
            TextStudioBackendLayoutData layoutData =
                new TextStudioBackendLayoutData(_positions, textUnitCount)
                    .WithSourceBaselineOffsets(_baselineOffsets)
                    .WithPlacementLines(_placementLines, placementLineCount)
                    .WithSourceColors(
                        _sourceColors,
                        _sourceColorValidity,
                        _sourceColorBounds)
                    .WithSourceVisualBounds(_sourceVisualBounds)
                    .WithEffectUnitScales(_effectUnitScales)
                    .WithSemanticMappings(_textUnitToLine, _textUnitToWord)
                    .WithSourceAlignment(ResolveAlignment(target.alignment))
                    .WithVisualGlyphs(
                        _visualGlyphs,
                        visualCapabilities,
                        version);
            _quadCount = generatedQuadCount;
            return builder.Commit(in layoutData);
        }

        public void NotifyFontAtlasChanged()
        {
            Increment(ref _atlasRevision);
        }

        void ResetUnits(TextStudioPlanView plan, int count)
        {
            if (count > 0)
            {
                Array.Clear(_unitHasPosition, 0, count);
                Array.Clear(_unitHasBounds, 0, count);
                Array.Clear(_sourceColorValidity, 0, count);
            }

            for (int unit = 0; unit < count; unit++)
            {
                _positions[unit] = Vector3.zero;
                _baselineOffsets[unit] = 0f;
                _sourceColors[unit] = TextStudioGlyphColorQuad.White;
                _sourceColorBounds[unit] = default;
                _sourceVisualBounds[unit] = default;
                _effectUnitScales[unit] = 1f;
                _textUnitToLine[unit] = unit < plan.GlyphToLineIndex.Count
                    ? plan.GlyphToLineIndex[unit]
                    : 0;
                _textUnitToWord[unit] = unit < plan.GlyphToWordIndex.Count
                    ? plan.GlyphToWordIndex[unit]
                    : -1;
                _unitMinimum[unit] = Vector2.zero;
                _unitMaximum[unit] = Vector2.zero;
                _unitBaseline[unit] = 0f;
                _unitAdvance[unit] = 0f;
                _unitAtlasUvRect[unit] = default;
                _unitFirstVisualIndex[unit] = -1;
                _visualGlyphs[unit] = default;
            }
        }

        void BuildCharacterLayout(
            TextGenerator generator,
            TextStudioPlanView plan,
            string sourceText,
            in CharacterMap map,
            int textUnitCount,
            float unitsPerPixel,
            Vector2 roundingOffset,
            float effectUnitScale)
        {
            IList<UICharInfo> characters = generator.characters;
            IList<UILineInfo> lines = generator.lines;
            int count = Mathf.Min(
                sourceText.Length,
                characters != null ? characters.Count : 0);
            int lineIndex = 0;
            for (int characterIndex = 0; characterIndex < count; characterIndex++)
            {
                int textUnitIndex = map.GetTextUnit(characterIndex);
                if ((uint)textUnitIndex >= (uint)textUnitCount)
                    continue;

                while (lines != null &&
                       lineIndex + 1 < lines.Count &&
                       lines[lineIndex + 1].startCharIdx <= characterIndex)
                {
                    lineIndex++;
                }

                UICharInfo character = characters[characterIndex];
                Vector2 cursor = character.cursorPos * unitsPerPixel + roundingOffset;
                float advance = Mathf.Max(0f, character.charWidth * unitsPerPixel);
                float baseline = cursor.y - effectUnitScale;
                if (!_unitHasPosition[textUnitIndex])
                {
                    _positions[textUnitIndex] = new Vector3(
                        cursor.x + advance * 0.5f,
                        baseline + effectUnitScale * 0.5f,
                        0f);
                    _unitBaseline[textUnitIndex] = baseline;
                    _unitHasPosition[textUnitIndex] = true;
                }
                _unitAdvance[textUnitIndex] += advance;
                _textUnitToLine[textUnitIndex] = lineIndex;
                _effectUnitScales[textUnitIndex] = effectUnitScale;
            }
        }

        void BuildVisibleQuadLayout(
            IList<UIVertex> vertices,
            int quadCount,
            float unitsPerPixel,
            Vector2 roundingOffset,
            float defaultEffectUnitScale)
        {
            for (int quadIndex = 0; quadIndex < quadCount; quadIndex++)
            {
                int textUnitIndex = _quadToTextUnit[quadIndex];
                if ((uint)textUnitIndex >= (uint)_positions.Length)
                    continue;

                int vertexIndex = quadIndex * 4;
                UIVertex topLeft = vertices[vertexIndex];
                UIVertex topRight = vertices[vertexIndex + 1];
                UIVertex bottomRight = vertices[vertexIndex + 2];
                UIVertex bottomLeft = vertices[vertexIndex + 3];
                Vector2 tl = Convert(topLeft.position, unitsPerPixel, roundingOffset);
                Vector2 tr = Convert(topRight.position, unitsPerPixel, roundingOffset);
                Vector2 br = Convert(bottomRight.position, unitsPerPixel, roundingOffset);
                Vector2 bl = Convert(bottomLeft.position, unitsPerPixel, roundingOffset);
                Vector2 minimum = Vector2.Min(Vector2.Min(tl, tr), Vector2.Min(br, bl));
                Vector2 maximum = Vector2.Max(Vector2.Max(tl, tr), Vector2.Max(br, bl));

                if (_unitHasBounds[textUnitIndex])
                {
                    _unitMinimum[textUnitIndex] =
                        Vector2.Min(_unitMinimum[textUnitIndex], minimum);
                    _unitMaximum[textUnitIndex] =
                        Vector2.Max(_unitMaximum[textUnitIndex], maximum);
                }
                else
                {
                    _unitMinimum[textUnitIndex] = minimum;
                    _unitMaximum[textUnitIndex] = maximum;
                    _unitHasBounds[textUnitIndex] = true;
                    _unitFirstVisualIndex[textUnitIndex] = quadIndex;
                    _sourceColors[textUnitIndex] =
                        new TextStudioGlyphColorQuad(
                            bottomLeft.color,
                            topLeft.color,
                            topRight.color,
                            bottomRight.color);
                    _sourceColorValidity[textUnitIndex] = true;
                    _unitAtlasUvRect[textUnitIndex] = ResolveUvRect(
                        topLeft.uv0,
                        topRight.uv0,
                        bottomRight.uv0,
                        bottomLeft.uv0);
                }

                float height = maximum.y - minimum.y;
                _effectUnitScales[textUnitIndex] = Mathf.Max(
                    defaultEffectUnitScale,
                    height);
            }
        }

        void FinalizeUnitLayout(int count, float defaultEffectUnitScale)
        {
            Vector3 previousPosition = Vector3.zero;
            float previousBaseline = 0f;
            for (int unit = 0; unit < count; unit++)
            {
                if (_unitHasBounds[unit])
                {
                    Vector2 minimum = _unitMinimum[unit];
                    Vector2 maximum = _unitMaximum[unit];
                    Vector2 center = (minimum + maximum) * 0.5f;
                    _positions[unit] = center;
                    if (!_unitHasPosition[unit])
                        _unitBaseline[unit] = minimum.y;
                    _baselineOffsets[unit] = center.y - _unitBaseline[unit];
                    _sourceVisualBounds[unit] =
                        new TextStudioGlyphVisualBounds(
                            minimum,
                            maximum,
                            true);
                    _sourceColorBounds[unit] =
                        new TextStudioGlyphColorBounds(
                            minimum - center,
                            maximum - center);
                    previousPosition = center;
                    previousBaseline = _unitBaseline[unit];
                }
                else if (_unitHasPosition[unit])
                {
                    _baselineOffsets[unit] =
                        _positions[unit].y - _unitBaseline[unit];
                    _sourceVisualBounds[unit] =
                        new TextStudioGlyphVisualBounds(
                            _positions[unit],
                            _positions[unit],
                            false);
                    previousPosition = _positions[unit];
                    previousBaseline = _unitBaseline[unit];
                }
                else
                {
                    _positions[unit] = previousPosition;
                    _unitBaseline[unit] = previousBaseline;
                    _baselineOffsets[unit] = 0f;
                    _sourceVisualBounds[unit] =
                        new TextStudioGlyphVisualBounds(
                            previousPosition,
                            previousPosition,
                            false);
                }

                if (_effectUnitScales[unit] <= 0f)
                    _effectUnitScales[unit] = defaultEffectUnitScale;
            }
        }

        int BuildPlacementLines(int textUnitCount)
        {
            int maximumLine = -1;
            for (int unit = 0; unit < textUnitCount; unit++)
                maximumLine = Mathf.Max(maximumLine, _textUnitToLine[unit]);
            if (maximumLine < 0)
                return 0;

            EnsurePlacementLineCapacity(maximumLine + 1);
            int written = 0;
            for (int line = 0; line <= maximumLine; line++)
            {
                int first = -1;
                int last = -1;
                float minimumX = float.PositiveInfinity;
                float maximumX = float.NegativeInfinity;
                float baseline = 0f;
                for (int unit = 0; unit < textUnitCount; unit++)
                {
                    if (_textUnitToLine[unit] != line)
                        continue;
                    if (first < 0)
                    {
                        first = unit;
                        baseline = _unitBaseline[unit];
                    }
                    last = unit;
                    if (_unitHasBounds[unit])
                    {
                        minimumX = Mathf.Min(minimumX, _unitMinimum[unit].x);
                        maximumX = Mathf.Max(maximumX, _unitMaximum[unit].x);
                    }
                    else
                    {
                        float halfAdvance = _unitAdvance[unit] * 0.5f;
                        minimumX = Mathf.Min(
                            minimumX,
                            _positions[unit].x - halfAdvance);
                        maximumX = Mathf.Max(
                            maximumX,
                            _positions[unit].x + halfAdvance);
                    }
                }

                if (first < 0)
                    continue;
                float width = float.IsInfinity(minimumX) || float.IsInfinity(maximumX)
                    ? 0f
                    : Mathf.Max(0f, maximumX - minimumX);
                _placementLines[written++] = new LineRange
                {
                    glyphStart = first,
                    glyphCount = last - first + 1,
                    lineIndex = line,
                    width = width,
                    baseline = baseline
                };
            }
            return written;
        }

        void BuildVisualGlyphs(
            Text target,
            TextStudioPlanView plan,
            int textUnitCount)
        {
            ulong fontIdentity = target.font != null
                ? unchecked((uint)target.font.GetStableId())
                : 0UL;
            for (int unit = 0; unit < textUnitCount; unit++)
            {
                TextStudioTextUnit textUnit = plan.GetTextUnit(unit);
                Vector2 minimum = _unitHasBounds[unit]
                    ? _unitMinimum[unit]
                    : (Vector2)_positions[unit];
                Vector2 maximum = _unitHasBounds[unit]
                    ? _unitMaximum[unit]
                    : (Vector2)_positions[unit];
                float advance = _unitAdvance[unit] > 0f
                    ? _unitAdvance[unit]
                    : Mathf.Max(0f, maximum.x - minimum.x);
                uint glyphIndex = plan.GetPrimaryChar(unit);
                _visualGlyphs[unit] = new TextStudioVisualGlyph(
                    fontIdentity,
                    glyphIndex,
                    unit,
                    unit,
                    _unitFirstVisualIndex[unit] >= 0
                        ? _unitFirstVisualIndex[unit]
                        : unit,
                    textUnit.resolvedStart,
                    textUnit.resolvedLength,
                    _textUnitToLine[unit],
                    _textUnitToWord[unit],
                    new Vector2(
                        _positions[unit].x - advance * 0.5f,
                        _unitBaseline[unit]),
                    advance,
                    _unitBaseline[unit],
                    minimum,
                    maximum,
                    Vector2.right,
                    _unitAtlasUvRect[unit],
                    0,
                    0f,
                    0f,
                    _unitHasBounds[unit]);
            }
        }

        static CharacterMap ResolveCharacterMap(
            TextStudioPlanView plan,
            string sourceText,
            string pushedText,
            int[] pushedCharacterToTextUnit,
            int pushedCharacterCount)
        {
            if (pushedCharacterToTextUnit != null &&
                pushedCharacterCount == sourceText.Length &&
                pushedCharacterToTextUnit.Length >= pushedCharacterCount &&
                string.Equals(sourceText, pushedText, StringComparison.Ordinal))
            {
                return new CharacterMap(
                    plan,
                    pushedCharacterToTextUnit,
                    pushedCharacterCount,
                    CharacterMapKind.Captured);
            }
            if (string.Equals(sourceText, plan.DisplayText, StringComparison.Ordinal))
                return new CharacterMap(plan, null, sourceText.Length, CharacterMapKind.Display);
            if (string.Equals(sourceText, plan.ResolvedText, StringComparison.Ordinal))
                return new CharacterMap(plan, null, sourceText.Length, CharacterMapKind.Resolved);
            return new CharacterMap(plan, null, sourceText.Length, CharacterMapKind.Sequential);
        }

        static int CountQuadCandidates(string sourceText, in CharacterMap map)
        {
            int count = 0;
            for (int i = 0; i < sourceText.Length; i++)
            {
                if (map.GetTextUnit(i) >= 0 && ProducesGlyphQuad(sourceText[i]))
                    count++;
            }
            return count;
        }

        void BuildQuadMap(
            string sourceText,
            in CharacterMap map,
            int generatedQuadCount)
        {
            int written = 0;
            for (int characterIndex = 0;
                 characterIndex < sourceText.Length && written < generatedQuadCount;
                 characterIndex++)
            {
                int textUnitIndex = map.GetTextUnit(characterIndex);
                if (textUnitIndex < 0 || !ProducesGlyphQuad(sourceText[characterIndex]))
                    continue;
                _quadToTextUnit[written++] = textUnitIndex;
            }
            _quadCount = written;
        }

        static int RemoveCompatibilitySentinel(
            IList<UIVertex> vertices,
            int generatedQuadCount,
            int candidateQuadCount)
        {
            if (generatedQuadCount <= 0)
                return 0;
            if (generatedQuadCount == candidateQuadCount + 1 &&
                IsEmptySentinel(vertices, generatedQuadCount - 1))
            {
                return candidateQuadCount;
            }
            if (generatedQuadCount <= candidateQuadCount &&
                IsEmptySentinel(vertices, generatedQuadCount - 1))
            {
                return generatedQuadCount - 1;
            }
            return generatedQuadCount;
        }

        static bool IsEmptySentinel(IList<UIVertex> vertices, int quadIndex)
        {
            int vertexIndex = quadIndex * 4;
            UIVertex first = vertices[vertexIndex];
            for (int i = 0; i < 4; i++)
            {
                UIVertex vertex = vertices[vertexIndex + i];
                if ((vertex.position - first.position).sqrMagnitude > 0.0000001f ||
                    vertex.uv0.sqrMagnitude > 0.0000001f)
                {
                    return false;
                }
            }
            return first.position.sqrMagnitude <= 0.0000001f;
        }

        static bool ProducesGlyphQuad(char value)
        {
            if (char.IsWhiteSpace(value) || char.IsControl(value))
                return false;
            UnicodeCategory category = char.GetUnicodeCategory(value);
            return category != UnicodeCategory.Format &&
                   category != UnicodeCategory.LineSeparator &&
                   category != UnicodeCategory.ParagraphSeparator &&
                   category != UnicodeCategory.SpaceSeparator;
        }

        static Vector2 ResolveRoundingOffset(
            Text target,
            IList<UIVertex> vertices,
            int quadCount,
            float unitsPerPixel)
        {
            if (target == null || vertices == null || quadCount <= 0)
                return Vector2.zero;
            Vector2 first = vertices[0].position * unitsPerPixel;
            return target.PixelAdjustPoint(first) - first;
        }

        static Vector2 Convert(
            Vector3 value,
            float unitsPerPixel,
            Vector2 roundingOffset)
        {
            return new Vector2(
                value.x * unitsPerPixel + roundingOffset.x,
                value.y * unitsPerPixel + roundingOffset.y);
        }

        static Vector4 ResolveUvRect(
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth)
        {
            float minimumX = Mathf.Min(Mathf.Min(first.x, second.x), Mathf.Min(third.x, fourth.x));
            float minimumY = Mathf.Min(Mathf.Min(first.y, second.y), Mathf.Min(third.y, fourth.y));
            float maximumX = Mathf.Max(Mathf.Max(first.x, second.x), Mathf.Max(third.x, fourth.x));
            float maximumY = Mathf.Max(Mathf.Max(first.y, second.y), Mathf.Max(third.y, fourth.y));
            return new Vector4(minimumX, minimumY, maximumX, maximumY);
        }

        static Vector2 ResolveAlignment(TextAnchor anchor)
        {
            float horizontal = anchor switch
            {
                TextAnchor.UpperCenter or
                TextAnchor.MiddleCenter or
                TextAnchor.LowerCenter => 0.5f,
                TextAnchor.UpperRight or
                TextAnchor.MiddleRight or
                TextAnchor.LowerRight => 1f,
                _ => 0f
            };
            float vertical = anchor switch
            {
                TextAnchor.MiddleLeft or
                TextAnchor.MiddleCenter or
                TextAnchor.MiddleRight => 0.5f,
                TextAnchor.UpperLeft or
                TextAnchor.UpperCenter or
                TextAnchor.UpperRight => 1f,
                _ => 0f
            };
            return new Vector2(horizontal, vertical);
        }

        void EnsureUnitCapacity(int count)
        {
            if (_positions.Length >= count)
                return;
            int capacity = CapacityFor(count);
            _positions = new Vector3[capacity];
            _baselineOffsets = new float[capacity];
            _sourceColors = new TextStudioGlyphColorQuad[capacity];
            _sourceColorValidity = new bool[capacity];
            _sourceColorBounds = new TextStudioGlyphColorBounds[capacity];
            _sourceVisualBounds = new TextStudioGlyphVisualBounds[capacity];
            _effectUnitScales = new float[capacity];
            _textUnitToLine = new int[capacity];
            _textUnitToWord = new int[capacity];
            _visualGlyphs = new TextStudioVisualGlyph[capacity];
            _unitHasPosition = new bool[capacity];
            _unitHasBounds = new bool[capacity];
            _unitMinimum = new Vector2[capacity];
            _unitMaximum = new Vector2[capacity];
            _unitBaseline = new float[capacity];
            _unitAdvance = new float[capacity];
            _unitAtlasUvRect = new Vector4[capacity];
            _unitFirstVisualIndex = new int[capacity];
        }

        void EnsureQuadCapacity(int count)
        {
            if (_quadToTextUnit.Length < count)
                _quadToTextUnit = new int[CapacityFor(count)];
        }

        void EnsurePlacementLineCapacity(int count)
        {
            if (_placementLines.Length < count)
                _placementLines = new LineRange[CapacityFor(count)];
        }

        static int CapacityFor(int count)
        {
            int capacity = 4;
            while (capacity < count)
                capacity <<= 1;
            return capacity;
        }

        static void Increment(ref uint revision)
        {
            unchecked
            {
                revision++;
                if (revision == 0)
                    revision = 1;
            }
        }

        enum CharacterMapKind
        {
            Captured,
            Display,
            Resolved,
            Sequential
        }

        readonly struct CharacterMap
        {
            readonly TextStudioPlanView _plan;
            readonly int[] _captured;
            readonly int _count;
            readonly CharacterMapKind _kind;

            public CharacterMap(
                TextStudioPlanView plan,
                int[] captured,
                int count,
                CharacterMapKind kind)
            {
                _plan = plan;
                _captured = captured;
                _count = count;
                _kind = kind;
            }

            public int GetTextUnit(int characterIndex)
            {
                if ((uint)characterIndex >= (uint)_count)
                    return -1;
                return _kind switch
                {
                    CharacterMapKind.Captured => _captured[characterIndex],
                    CharacterMapKind.Display =>
                        _plan.GetUnitIndexForDisplayChar(characterIndex),
                    CharacterMapKind.Resolved =>
                        _plan.GetUnitIndexForResolvedChar(characterIndex),
                    _ => characterIndex < _plan.TextUnitCount
                        ? characterIndex
                        : -1
                };
            }
        }
    }

    internal readonly struct TextStudioUnityUITextLayoutSignature :
        IEquatable<TextStudioUnityUITextLayoutSignature>
    {
        readonly Font _font;
        readonly Material _material;
        readonly FontStyle _fontStyle;
        readonly TextAnchor _alignment;
        readonly HorizontalWrapMode _horizontalOverflow;
        readonly VerticalWrapMode _verticalOverflow;
        readonly Vector2 _rectSize;
        readonly Vector2 _pivot;
        readonly Vector2 _pixelAdjustedOrigin;
        readonly Color32 _color;
        readonly float _pixelsPerUnit;
        readonly int _fontSize;
        readonly int _resizeTextMinSize;
        readonly int _resizeTextMaxSize;
        readonly float _lineSpacing;
        readonly bool _supportRichText;
        readonly bool _resizeTextForBestFit;
        readonly bool _alignByGeometry;

        TextStudioUnityUITextLayoutSignature(
            Font font,
            Material material,
            FontStyle fontStyle,
            TextAnchor alignment,
            HorizontalWrapMode horizontalOverflow,
            VerticalWrapMode verticalOverflow,
            Vector2 rectSize,
            Vector2 pivot,
            Vector2 pixelAdjustedOrigin,
            Color32 color,
            float pixelsPerUnit,
            int fontSize,
            int resizeTextMinSize,
            int resizeTextMaxSize,
            float lineSpacing,
            bool supportRichText,
            bool resizeTextForBestFit,
            bool alignByGeometry)
        {
            _font = font;
            _material = material;
            _fontStyle = fontStyle;
            _alignment = alignment;
            _horizontalOverflow = horizontalOverflow;
            _verticalOverflow = verticalOverflow;
            _rectSize = rectSize;
            _pivot = pivot;
            _pixelAdjustedOrigin = pixelAdjustedOrigin;
            _color = color;
            _pixelsPerUnit = pixelsPerUnit;
            _fontSize = fontSize;
            _resizeTextMinSize = resizeTextMinSize;
            _resizeTextMaxSize = resizeTextMaxSize;
            _lineSpacing = lineSpacing;
            _supportRichText = supportRichText;
            _resizeTextForBestFit = resizeTextForBestFit;
            _alignByGeometry = alignByGeometry;
        }

        public static TextStudioUnityUITextLayoutSignature Capture(Text text)
        {
            if (text == null)
                return default;
            return new TextStudioUnityUITextLayoutSignature(
                text.font,
                text.material,
                text.fontStyle,
                text.alignment,
                text.horizontalOverflow,
                text.verticalOverflow,
                text.rectTransform.rect.size,
                text.rectTransform.pivot,
                text.PixelAdjustPoint(Vector2.zero),
                text.color,
                text.pixelsPerUnit,
                text.fontSize,
                text.resizeTextMinSize,
                text.resizeTextMaxSize,
                text.lineSpacing,
                text.supportRichText,
                text.resizeTextForBestFit,
                text.alignByGeometry);
        }

        public bool Equals(TextStudioUnityUITextLayoutSignature other)
        {
            return ReferenceEquals(_font, other._font) &&
                   ReferenceEquals(_material, other._material) &&
                   _fontStyle == other._fontStyle &&
                   _alignment == other._alignment &&
                   _horizontalOverflow == other._horizontalOverflow &&
                   _verticalOverflow == other._verticalOverflow &&
                   _rectSize == other._rectSize &&
                   _pivot == other._pivot &&
                   _pixelAdjustedOrigin == other._pixelAdjustedOrigin &&
                   _color.Equals(other._color) &&
                   _pixelsPerUnit.Equals(other._pixelsPerUnit) &&
                   _fontSize == other._fontSize &&
                   _resizeTextMinSize == other._resizeTextMinSize &&
                   _resizeTextMaxSize == other._resizeTextMaxSize &&
                   _lineSpacing.Equals(other._lineSpacing) &&
                   _supportRichText == other._supportRichText &&
                   _resizeTextForBestFit == other._resizeTextForBestFit &&
                   _alignByGeometry == other._alignByGeometry;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioUnityUITextLayoutSignature other &&
                   Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = _font != null ? _font.GetStableId() : 0;
                hash = hash * 397 ^ (_material != null ? _material.GetStableId() : 0);
                hash = hash * 397 ^ (int)_fontStyle;
                hash = hash * 397 ^ (int)_alignment;
                hash = hash * 397 ^ _rectSize.GetHashCode();
                hash = hash * 397 ^ _pivot.GetHashCode();
                hash = hash * 397 ^ _pixelAdjustedOrigin.GetHashCode();
                hash = hash * 397 ^ _pixelsPerUnit.GetHashCode();
                hash = hash * 397 ^ _fontSize;
                hash = hash * 397 ^ _lineSpacing.GetHashCode();
                hash = hash * 397 ^ _supportRichText.GetHashCode();
                return hash;
            }
        }
    }
}
