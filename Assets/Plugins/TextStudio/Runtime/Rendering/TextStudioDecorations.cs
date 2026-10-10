using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

#if UNITY_2023_2_OR_NEWER
using TmpUv0 = UnityEngine.Vector4;
#else
using TmpUv0 = UnityEngine.Vector2;
#endif

namespace TextStudio
{
    /// <summary>
    /// Which decoration tiles this glyph participates in. Combinable as flags so a
    /// single character can sit under an underline and a strikethrough at the same
    /// time without us needing multiple mesh passes.
    /// </summary>
    [Flags]
    public enum DecorationBits
    {
        None = 0,
        Underline = 1 << 0,
        Strikethrough = 1 << 1,
        DoubleUnderline = 1 << 2,
        Overline = 1 << 3,
    }

    /// <summary>
    /// Builds and owns a child mesh that renders underline / strikethrough /
    /// double underline / overline tiles per-character, following the hub's
    /// placement curve and reveal / animation state.
    ///
    /// Strategy: one small quad per (character × decoration-type) positioned
    /// relative to the character's baseline using font metrics
    /// (<see cref="FaceInfo.underlineOffset"/>, <see cref="FaceInfo.underlineThickness"/>,
    /// <see cref="FaceInfo.strikethroughOffset"/>, <see cref="FaceInfo.strikethroughThickness"/>),
    /// then transformed by the same placement rotation / scale the glyph receives
    /// and blended with the per-glyph animation state (alpha, color override,
    /// position offset, rotation, scale). TMP's own decoration verts are alpha-
    /// zeroed in the meshInfo so the curved tiles don't double up with the flat
    /// line TMP would otherwise draw across the text bounds.
    ///
    /// Supports all four decorations:
    ///   - Underline / Strikethrough come from TMP's native
    ///     <see cref="TMP_CharacterInfo.style"/> flag (both the inline
    ///     <c>&lt;u&gt;</c>/<c>&lt;s&gt;</c> tags and the TMP inspector's
    ///     global <c>fontStyle</c> toggle land there).
    ///   - DoubleUnderline / Overline come from Text Studio's custom
    ///     <c>&lt;uu&gt;</c>/<c>&lt;o&gt;</c> tags, collected by the pipeline
    ///     onto <see cref="TextStudioCompiledPlan.CharDecorations"/>.
    ///
    /// Both backends are supported:
    ///   - World-space (<see cref="TextMeshPro"/>) uses a child GameObject
    ///     with <see cref="MeshFilter"/> + <see cref="MeshRenderer"/>.
    ///   - UGUI (<see cref="TextMeshProUGUI"/>) uses a child GameObject with
    ///     a <see cref="CanvasRenderer"/> driven by
    ///     <see cref="TextStudioDecorationGraphic"/>, so decorations live
    ///     inside the Canvas batching and masking pipeline.
    /// </summary>
    internal sealed class TextStudioDecorations
    {
        const string k_ChildName = "_TSDecor";
        const HideFlags k_ChildHideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;

        readonly TextStudioText _hub;

        // World-space child (TextMeshPro).
        GameObject _childGO;
        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        Mesh _mesh;

        // UGUI child (TextMeshProUGUI).
        GameObject _uguiGO;
        TextStudioDecorationGraphic _uguiGraphic;

        // CPU-side buffers reused across rebuilds. The mesh is uploaded with a
        // known exact length each time, so the buffer grows monotonically.
        readonly List<Vector3> _verts = new List<Vector3>(64);
        readonly List<Color32> _colors = new List<Color32>(64);
        readonly List<Vector4> _uvs = new List<Vector4>(64);
        readonly List<int> _tris = new List<int>(96);

        // Per-char decoration bits, resized to match textInfo.characterCount.
        DecorationBits[] _charBits = Array.Empty<DecorationBits>();

        /// <summary>
        /// True when no decorations were produced in the last rebuild. The hub
        /// can use this to skip child-GO activation in the common no-underline
        /// case, keeping the component hierarchy clean.
        /// </summary>
        public bool IsEmpty { get; private set; } = true;

        public TextStudioDecorations(TextStudioText hub)
        {
            _hub = hub;
        }

        /// <summary>
        /// Rebuild the decoration mesh for the current frame. Safe to call every
        /// tick: early-exits when TMP isn't a world-space TextMeshPro, when the
        /// font has no usable underline metrics, or when no character carries
        /// any decoration bit.
        /// </summary>
        public void Rebuild(
            in TextStudioFrame frame,
            TMP_Text tmpText,
            Material sourceMaterial = null)
        {
            IsEmpty = true;

            if (tmpText == null)
            {
                DestroyChildren();
                return;
            }

            bool isUGUI = tmpText is TextMeshProUGUI;
            // Tear down the backend-mismatched child so we don't accumulate
            // stale GOs if the user swaps the text component type.
            if (isUGUI) DestroyWorldChild();
            else DestroyUGUIChild();

            var textInfo = tmpText.textInfo;
            if (textInfo == null || textInfo.characterCount == 0)
            {
                HideChild();
                return;
            }

            var fontAsset = tmpText.font;
            if (fontAsset == null)
            {
                HideChild();
                return;
            }

            // Resize per-char bits and populate from two sources:
            //   1. TMP style flags (underline / strikethrough) - covers inline
            //      <u>/<s> tags AND the TMP inspector's global fontStyle toggle.
            //   2. plan.CharDecorations (double-underline / overline) - covers
            //      Text Studio's custom <uu>/<o> tags that TMP can't render.
            int charCount = textInfo.characterCount;
            int displayedPage = TextStudioPageUtility.GetDisplayedPageIndex(tmpText);
            if (_charBits.Length < charCount) _charBits = new DecorationBits[charCount];
            TextStudioReadOnlyBuffer<DecorationBits> planDecor = frame.Plan.IsCreated
                ? frame.Plan.CharDecorations
                : default;
            int planDecorLen = planDecor.Length;
            bool anyDecor = false;
            FontStyles globalStyle = tmpText.fontStyle;
            for (int i = 0; i < charCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!TextStudioPageUtility.IsDisplayedPage(in character, displayedPage))
                {
                    _charBits[i] = DecorationBits.None;
                    continue;
                }

                DecorationBits bits = DecorationBits.None;
                FontStyles style = character.style | globalStyle;
                if ((style & FontStyles.Underline) != 0) bits |= DecorationBits.Underline;
                if ((style & FontStyles.Strikethrough) != 0) bits |= DecorationBits.Strikethrough;
                int unitIndex = frame.Plan.IsCreated ? frame.Plan.GetUnitIndexForResolvedChar(i) : i;
                if (unitIndex >= 0 && unitIndex < planDecorLen) bits |= planDecor[unitIndex];
                _charBits[i] = bits;
                if (bits != DecorationBits.None) anyDecor = true;
            }

            if (!anyDecor)
            {
                // No decorations this frame - tear the mesh down so unused
                // verts don't hang around. Do not touch TMP's generated
                // geometry here; tags such as <mark> also append vertices
                // after characters and must keep their alpha.
                HideChild();
                return;
            }

            if (isUGUI) EnsureUGUIChild();
            else EnsureWorldChild();

            // Build the tiles. Each decorated char contributes one tile per
            // decoration type (currently underline + strikethrough). Tiles are
            // sized from font metrics so they scale correctly across font sizes.
            _verts.Clear();
            _colors.Clear();
            _uvs.Clear();
            _tris.Clear();

            FaceInfo faceInfo = fontAsset.faceInfo;

            // Atlas UV for the '_' glyph - reused for all tiles so the
            // material's font-atlas sampling produces the same pixel color and
            // SDF thickness TMP would use for its own underline.
            if (!TryGetUnderscoreUV(fontAsset, out Vector4 fallbackUv))
            {
                // Fall back to the atlas center; visual difference is negligible
                // because the TMP material reads a near-solid band at that UV.
                fallbackUv = new Vector4(0.5f, 0.5f, 0f, 1f);
            }

            TextStudioReadOnlyBuffer<GlyphPlacement> placements = frame.Placements;
            int placementCount = frame.PlacementCount;
            bool usePlacement = placements.IsCreated && placementCount > 0;
            TextStudioReadOnlyBuffer<EvaluatedGlyphState> states = frame.Animated;
            int stateCount = frame.AnimatedCount;
            float fallbackEffectUnitScale = TextStudioTMPBridge.ResolveEffectUnitScale(tmpText);

            for (int i = 0; i < charCount; i++)
            {
                DecorationBits bits = _charBits[i];
                if (bits == DecorationBits.None) continue;
                int unitIndex = frame.Plan.IsCreated ? frame.Plan.GetUnitIndexForResolvedChar(i) : i;
                if (unitIndex < 0)
                    continue;

                TMP_CharacterInfo ci = textInfo.characterInfo[i];
                Vector3 sourceCenter = GetGlyphSourceCenter(in ci);

                EvaluatedGlyphState state = states.IsCreated && unitIndex < stateCount
                    ? states[unitIndex]
                    : EvaluatedGlyphState.Identity;
                float effectUnitScale = frame.GetEffectUnitScale(
                    unitIndex,
                    fallbackEffectUnitScale);
                GlyphPlacement placement = default;
                bool hasPlacement = usePlacement && unitIndex < placementCount;
                if (hasPlacement)
                    placement = placements[unitIndex];

                TextStudioGlyphQuadTransform transform = TextStudioGlyphQuadTransformer.Create(
                    sourceCenter,
                    in state,
                    in placement,
                    hasPlacement,
                    effectUnitScale,
                    1f);
                TextStudioGlyphColorQuad sourceColors = frame.GetSourceColors(unitIndex);
                TextStudioGlyphColorBounds sourceColorBounds = frame.GetSourceColorBounds(unitIndex);

                DecorationUvData fallbackUvData = DecorationUvData.Solid(fallbackUv);
                float charScale = Mathf.Max(0.0001f, ci.scale);
                float underlineCenter = ci.baseLine + faceInfo.underlineOffset * charScale;
                float underlineThickness = Mathf.Max(0.01f, faceInfo.underlineThickness * charScale);

                if ((bits & DecorationBits.Underline) != 0)
                {
                    NativeDecorationData data = TryGetNativeDecorationData(
                        textInfo,
                        fontAsset,
                        in ci,
                        i,
                        DecorationBits.Underline,
                        fallbackUvData,
                        out NativeDecorationData nativeData)
                        ? nativeData
                        : NativeDecorationData.FromBand(underlineCenter, underlineThickness, fallbackUvData);
                    Color32 fallbackColor = GetDecorationColor(tmpText, in ci, DecorationBits.Underline);
                    EmitDecorationTile(
                        textInfo,
                        i,
                        in ci,
                        DecorationBits.Underline,
                        in data,
                        in transform,
                        in sourceColors,
                        in sourceColorBounds,
                        fallbackColor);
                }
                if ((bits & DecorationBits.Strikethrough) != 0)
                {
                    float strikeCenter = ci.baseLine + faceInfo.strikethroughOffset * charScale;
                    float strikeThickness = Mathf.Max(0.01f, faceInfo.strikethroughThickness * charScale);
                    NativeDecorationData data = TryGetNativeDecorationData(
                        textInfo,
                        fontAsset,
                        in ci,
                        i,
                        DecorationBits.Strikethrough,
                        fallbackUvData,
                        out NativeDecorationData nativeData)
                        ? nativeData
                        : NativeDecorationData.FromBand(strikeCenter, strikeThickness, fallbackUvData);
                    Color32 fallbackColor = GetDecorationColor(tmpText, in ci, DecorationBits.Strikethrough);
                    EmitDecorationTile(
                        textInfo,
                        i,
                        in ci,
                        DecorationBits.Strikethrough,
                        in data,
                        in transform,
                        in sourceColors,
                        in sourceColorBounds,
                        fallbackColor);
                }
                if ((bits & DecorationBits.DoubleUnderline) != 0)
                {
                    // Top line (coincides with single-underline offset) plus a
                    // parallel line beneath. Authored with <uu>..</uu>.
                    Color32 fallbackColor = GetDecorationColor(tmpText, in ci, DecorationBits.Underline);
                    NativeDecorationData topLine = NativeDecorationData.FromBand(underlineCenter, underlineThickness, fallbackUvData);
                    float doubleUnderlineGap = underlineThickness * 1.25f;
                    float secondCenter = underlineCenter - (underlineThickness + doubleUnderlineGap);
                    NativeDecorationData secondLine = NativeDecorationData.FromBand(secondCenter, underlineThickness, fallbackUvData);
                    EmitDecorationTile(
                        textInfo,
                        i,
                        in ci,
                        DecorationBits.DoubleUnderline,
                        in topLine,
                        in transform,
                        in sourceColors,
                        in sourceColorBounds,
                        fallbackColor);
                    EmitDecorationTile(
                        textInfo,
                        i,
                        in ci,
                        DecorationBits.DoubleUnderline,
                        in secondLine,
                        in transform,
                        in sourceColors,
                        in sourceColorBounds,
                        fallbackColor);
                }
                if ((bits & DecorationBits.Overline) != 0)
                {
                    // Just above cap height - authored with <o>..</o>.
                    float overlineCenter = ci.baseLine + faceInfo.ascentLine * charScale + underlineThickness * 0.5f;
                    NativeDecorationData overline = NativeDecorationData.FromBand(overlineCenter, underlineThickness, fallbackUvData);
                    Color32 fallbackColor = GetDecorationColor(tmpText, in ci, DecorationBits.Underline);
                    EmitDecorationTile(
                        textInfo,
                        i,
                        in ci,
                        DecorationBits.Overline,
                        in overline,
                        in transform,
                        in sourceColors,
                        in sourceColorBounds,
                        fallbackColor);
                }
            }

            IsEmpty = _tris.Count == 0;

            if (IsEmpty)
            {
                HideChild();
                SuppressTMPNativeDecorations(textInfo);
                return;
            }

            // Inherit TMP's font material in both backends so the decoration
            // tiles pick up the same atlas, SDF softness, outline color, and
            // face color users have tuned on the text itself. UGUI uses a
            // canvas-compatible TMP material (mobile/UI preset); world-space
            // uses the 3D TMP material. Both route through fontSharedMaterial.
            if (isUGUI)
            {
                _uguiGraphic.ApplyMaterial(sourceMaterial ?? tmpText.fontSharedMaterial);
                _uguiGraphic.SetGeometry(_verts, _colors, _uvs, _tris);
            }
            else
            {
                UploadMesh();
                _meshRenderer.enabled = true;
                _meshRenderer.sharedMaterial = sourceMaterial ?? tmpText.fontSharedMaterial;
            }

            // Hide TMP's own (flat) decoration quads now that we're drawing
            // the curved version. The TMP text mesh still holds the decoration
            // verts beyond the character range; we alpha-zero them in place.
            SuppressTMPNativeDecorations(textInfo);
            tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        /// <summary>
        /// Emit four verts + six triangle indices for a single decoration tile.
        /// The tile is authored in TMP's original layout space, then transformed
        /// by the same glyph quad transformer used for visible character quads.
        /// </summary>
        void EmitDecorationTile(
            TMP_TextInfo textInfo,
            int characterIndex,
            in TMP_CharacterInfo character,
            DecorationBits bit,
            in NativeDecorationData data,
            in TextStudioGlyphQuadTransform transform,
            in TextStudioGlyphColorQuad sourceColors,
            in TextStudioGlyphColorBounds sourceColorBounds,
            Color32 fallbackColor)
        {
            GetDecorationHorizontalSpan(textInfo, characterIndex, in character, bit, in data, out float left, out float right);

            Vector3 bl = new Vector3(left, data.yMin, 0f);
            Vector3 br = new Vector3(right, data.yMin, 0f);
            Vector3 tl = new Vector3(left, data.yMax, 0f);
            Vector3 tr = new Vector3(right, data.yMax, 0f);

            int baseIdx = _verts.Count;
            _verts.Add(transform.ApplyPosition(bl));
            _verts.Add(transform.ApplyPosition(br));
            _verts.Add(transform.ApplyPosition(tl));
            _verts.Add(transform.ApplyPosition(tr));

            _colors.Add(SampleDecorationColor(in bl, in character, in sourceColors, in sourceColorBounds, in transform, fallbackColor));
            _colors.Add(SampleDecorationColor(in br, in character, in sourceColors, in sourceColorBounds, in transform, fallbackColor));
            _colors.Add(SampleDecorationColor(in tl, in character, in sourceColors, in sourceColorBounds, in transform, fallbackColor));
            _colors.Add(SampleDecorationColor(in tr, in character, in sourceColors, in sourceColorBounds, in transform, fallbackColor));
            _uvs.Add(data.uvData.bottomLeft);
            _uvs.Add(data.uvData.bottomRight);
            _uvs.Add(data.uvData.topLeft);
            _uvs.Add(data.uvData.topRight);

            _tris.Add(baseIdx + 0);
            _tris.Add(baseIdx + 2);
            _tris.Add(baseIdx + 1);
            _tris.Add(baseIdx + 1);
            _tris.Add(baseIdx + 2);
            _tris.Add(baseIdx + 3);
        }

        static Color32 SampleDecorationColor(
            in Vector3 vertex,
            in TMP_CharacterInfo character,
            in TextStudioGlyphColorQuad sourceColors,
            in TextStudioGlyphColorBounds sourceColorBounds,
            in TextStudioGlyphQuadTransform transform,
            Color32 fallbackColor)
        {
            if (!sourceColorBounds.IsValid)
                return transform.ApplyColor(fallbackColor);

            Vector2 minimum = sourceColorBounds.Minimum;
            Vector2 size = sourceColorBounds.Maximum - minimum;
            float normalizedX = (vertex.x - character.origin - minimum.x) / size.x;
            float normalizedY = (vertex.y - character.baseLine - minimum.y) / size.y;
            return transform.ApplyColor(sourceColors.Sample(normalizedX, normalizedY));
        }

        static Vector3 GetGlyphSourceCenter(in TMP_CharacterInfo character)
        {
            if (character.isVisible)
                return (character.bottomLeft + character.topRight) * 0.5f;

            float cx = (character.origin + character.xAdvance) * 0.5f;
            return new Vector3(cx, character.baseLine, 0f);
        }

        void GetDecorationHorizontalSpan(
            TMP_TextInfo textInfo,
            int characterIndex,
            in TMP_CharacterInfo character,
            DecorationBits bit,
            in NativeDecorationData data,
            out float left,
            out float right)
        {
            left = character.origin;
            right = character.xAdvance;

            if (data.hasRunSpan)
            {
                if (!HasAdjacentDecoratedCharacterOnLine(textInfo, characterIndex, bit, -1))
                    left = data.runStartX;
                if (!HasAdjacentDecoratedCharacterOnLine(textInfo, characterIndex, bit, 1))
                    right = data.runEndX;
            }

            if (right < left)
            {
                float tmp = left;
                left = right;
                right = tmp;
            }

            if (right - left < 0.001f)
                right = left + 0.001f;
        }

        bool HasAdjacentDecoratedCharacterOnLine(
            TMP_TextInfo textInfo,
            int characterIndex,
            DecorationBits bit,
            int direction)
        {
            int neighborIndex = characterIndex + direction;
            if (neighborIndex < 0 || neighborIndex >= textInfo.characterCount)
                return false;
            if (neighborIndex >= _charBits.Length || (_charBits[neighborIndex] & bit) == 0)
                return false;

            TMP_CharacterInfo current = textInfo.characterInfo[characterIndex];
            TMP_CharacterInfo neighbor = textInfo.characterInfo[neighborIndex];
            return neighbor.pageNumber == current.pageNumber &&
                   neighbor.lineNumber == current.lineNumber;
        }

        bool TryGetNativeDecorationData(
            TMP_TextInfo textInfo,
            TMP_FontAsset fontAsset,
            in TMP_CharacterInfo character,
            int characterIndex,
            DecorationBits bit,
            DecorationUvData fallbackUvData,
            out NativeDecorationData data)
        {
            float expectedCenter = character.baseLine + GetNativeDecorationOffset(fontAsset, bit) * character.scale;
            float tolerance = Mathf.Max(0.5f, Mathf.Abs(character.pointSize) * 0.1f);

            if (TryReadNativeDecorationData(
                    textInfo,
                    in character,
                    GetNativeDecorationVertexIndex(in character, bit),
                    expectedCenter,
                    tolerance,
                    fallbackUvData,
                    out data))
            {
                return true;
            }

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                if (i == characterIndex)
                    continue;
                if (i >= _charBits.Length || (_charBits[i] & bit) == 0)
                    continue;

                TMP_CharacterInfo other = textInfo.characterInfo[i];
                if (other.pageNumber != character.pageNumber ||
                    other.lineNumber != character.lineNumber)
                    continue;

                if (TryReadNativeDecorationData(
                        textInfo,
                        in other,
                        GetNativeDecorationVertexIndex(in other, bit),
                        expectedCenter,
                        tolerance,
                        fallbackUvData,
                        out data))
                {
                    return true;
                }
            }

            data = default;
            return false;
        }

        static bool TryReadNativeDecorationData(
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character,
            int vertexIndex,
            float expectedCenter,
            float tolerance,
            DecorationUvData fallbackUvData,
            out NativeDecorationData data)
        {
            data = default;
            if (!IsNativeDecorationVertexIndexValid(textInfo, in character, vertexIndex))
                return false;

            ref readonly TMP_MeshInfo meshInfo = ref textInfo.meshInfo[character.materialReferenceIndex];
            Vector3[] vertices = meshInfo.vertices;
            if (vertices == null || vertexIndex + 11 >= vertices.Length)
                return false;
            if (meshInfo.vertexCount > 0 && vertexIndex + 11 >= meshInfo.vertexCount)
                return false;

            float y0 = vertices[vertexIndex].y;
            float y1 = vertices[vertexIndex + 1].y;
            float minY = Mathf.Min(y0, y1);
            float maxY = Mathf.Max(y0, y1);
            float centerY = (minY + maxY) * 0.5f;
            if (Mathf.Abs(centerY - expectedCenter) > tolerance)
                return false;

            float minX = vertices[vertexIndex].x;
            float maxX = minX;
            for (int i = 1; i < 12; i++)
            {
                float x = vertices[vertexIndex + i].x;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
            }

            DecorationUvData uvData = fallbackUvData;
            TmpUv0[] uvs0 = meshInfo.uvs0;
            if (uvs0 != null)
            {
                if (!TryReadDecorationUVs(uvs0, vertexIndex + 4, out uvData))
                    TryReadDecorationUVs(uvs0, vertexIndex, out uvData);
            }

            data = new NativeDecorationData
            {
                yMin = minY,
                yMax = maxY,
                runStartX = minX,
                runEndX = maxX,
                hasRunSpan = true,
                uvData = uvData
            };
            return true;
        }

        static int GetNativeDecorationVertexIndex(in TMP_CharacterInfo character, DecorationBits bit)
        {
            return bit == DecorationBits.Strikethrough
                ? character.strikethroughVertexIndex
                : character.underlineVertexIndex;
        }

        static float GetNativeDecorationOffset(TMP_FontAsset fontAsset, DecorationBits bit)
        {
            if (fontAsset == null) return 0f;
            return bit == DecorationBits.Strikethrough
                ? fontAsset.faceInfo.strikethroughOffset
                : fontAsset.faceInfo.underlineOffset;
        }

        /// <summary>
        /// Look up the UV rectangle of the underscore glyph in the font atlas
        /// and return its center. Using the '_' glyph's UV (rather than a
        /// hard-coded atlas coordinate) guarantees the decoration pixel lives
        /// inside the glyph SDF region on any font the user assigns.
        /// </summary>
        static bool TryGetUnderscoreUV(TMP_FontAsset fontAsset, out Vector4 uvCenter)
        {
            uvCenter = default;
            if (fontAsset == null || fontAsset.characterTable == null) return false;

            TMP_Character glyph = null;
            for (int i = 0; i < fontAsset.characterTable.Count; i++)
            {
                if (fontAsset.characterTable[i].unicode == (uint)'_')
                {
                    glyph = fontAsset.characterTable[i];
                    break;
                }
            }
            if (glyph == null || glyph.glyph == null) return false;

            var atlasWidth = Mathf.Max(1, fontAsset.atlasWidth);
            var atlasHeight = Mathf.Max(1, fontAsset.atlasHeight);
            var rect = glyph.glyph.glyphRect;
            float u = (rect.x + rect.width * 0.5f) / atlasWidth;
            float v = (rect.y + rect.height * 0.5f) / atlasHeight;
            uvCenter = new Vector4(u, v, 0f, 1f);
            return true;
        }

        /// <summary>
        /// Alpha-zero TMP's own underline / strikethrough verts so they don't
        /// draw behind our curved tiles. TMP also appends highlight and other
        /// generated quads after the regular character quads, so suppression
        /// must target TMP_CharacterInfo's underline / strikethrough indices
        /// instead of blanking everything after the final character.
        /// </summary>
        void SuppressTMPNativeDecorations(TMP_TextInfo textInfo)
        {
            if (textInfo == null || textInfo.meshInfo == null)
                return;

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                DecorationBits bits = i < _charBits.Length ? _charBits[i] : DecorationBits.None;
                if ((bits & DecorationBits.Underline) != 0)
                    SuppressNativeDecorationRange(textInfo, in character, character.underlineVertexIndex);
                if ((bits & DecorationBits.Strikethrough) != 0)
                    SuppressNativeDecorationRange(textInfo, in character, character.strikethroughVertexIndex);
            }
        }

        static void SuppressNativeDecorationRange(
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character,
            int vertexIndex)
        {
            if (!IsNativeDecorationVertexIndexValid(textInfo, in character, vertexIndex))
                return;

            ref readonly TMP_MeshInfo meshInfo = ref textInfo.meshInfo[character.materialReferenceIndex];
            Color32[] colors = meshInfo.colors32;
            if (colors == null)
                return;

            int end = Mathf.Min(vertexIndex + 12, colors.Length);
            for (int v = vertexIndex; v < end; v++)
            {
                Color32 color = colors[v];
                color.a = 0;
                colors[v] = color;
            }
        }

        static bool TryReadDecorationUVs(TmpUv0[] uvs0, int vertexIndex, out DecorationUvData uvData)
        {
            uvData = default;
            if (vertexIndex < 0 || vertexIndex + 3 >= uvs0.Length)
                return false;

            uvData = new DecorationUvData
            {
                bottomLeft = ToDecorationUv(uvs0[vertexIndex]),
                topLeft = ToDecorationUv(uvs0[vertexIndex + 1]),
                topRight = ToDecorationUv(uvs0[vertexIndex + 2]),
                bottomRight = ToDecorationUv(uvs0[vertexIndex + 3]),
            };
            return true;
        }

        static Vector4 ToDecorationUv(TmpUv0 uv)
        {
#if UNITY_2023_2_OR_NEWER
            return uv;
#else
            return new Vector4(uv.x, uv.y, 0f, 0f);
#endif
        }

        static bool IsNativeDecorationVertexIndexValid(
            TMP_TextInfo textInfo,
            in TMP_CharacterInfo character,
            int vertexIndex)
        {
            if (textInfo == null || textInfo.meshInfo == null)
                return false;
            if (character.materialReferenceIndex < 0 || character.materialReferenceIndex >= textInfo.meshInfo.Length)
                return false;
            if (vertexIndex < character.vertexIndex + 4)
                return false;

            ref readonly TMP_MeshInfo meshInfo = ref textInfo.meshInfo[character.materialReferenceIndex];
            int vertexCapacity = meshInfo.vertices != null ? meshInfo.vertices.Length : 0;
            int colorCapacity = meshInfo.colors32 != null ? meshInfo.colors32.Length : vertexCapacity;
            int capacity = Mathf.Min(vertexCapacity, colorCapacity);
            if (meshInfo.vertexCount > 0)
                capacity = Mathf.Min(capacity, meshInfo.vertexCount);

            return vertexIndex + 3 < capacity;
        }

        static Color32 GetDecorationColor(TMP_Text tmpText, in TMP_CharacterInfo character, DecorationBits bit)
        {
            Color32 color = bit == DecorationBits.Strikethrough
                ? character.strikethroughColor
                : character.underlineColor;

            if (color.a == 0)
                color = tmpText != null ? (Color32)tmpText.color : new Color32(255, 255, 255, 255);
            return color;
        }

        struct DecorationUvData
        {
            public Vector4 bottomLeft;
            public Vector4 bottomRight;
            public Vector4 topLeft;
            public Vector4 topRight;

            public static DecorationUvData Solid(Vector4 uv)
            {
                return new DecorationUvData
                {
                    bottomLeft = uv,
                    bottomRight = uv,
                    topLeft = uv,
                    topRight = uv,
                };
            }
        }

        struct NativeDecorationData
        {
            public float yMin;
            public float yMax;
            public float runStartX;
            public float runEndX;
            public bool hasRunSpan;
            public DecorationUvData uvData;

            public static NativeDecorationData FromBand(float centerY, float height, DecorationUvData uvData)
            {
                float halfHeight = Mathf.Max(0.01f, height) * 0.5f;
                return new NativeDecorationData
                {
                    yMin = centerY - halfHeight,
                    yMax = centerY + halfHeight,
                    hasRunSpan = false,
                    uvData = uvData,
                };
            }
        }

        void EnsureWorldChild()
        {
            if (_childGO == null)
            {
                var t = _hub.transform.Find(k_ChildName);
                if (t != null) _childGO = t.gameObject;
            }
            if (_childGO == null)
            {
                _childGO = new GameObject(k_ChildName);
                _childGO.transform.SetParent(_hub.transform, false);
            }
            _childGO.hideFlags = k_ChildHideFlags;
            if (_meshFilter == null) _meshFilter = _childGO.GetComponent<MeshFilter>();
            if (_meshFilter == null) _meshFilter = _childGO.AddComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = _childGO.GetComponent<MeshRenderer>();
            if (_meshRenderer == null) _meshRenderer = _childGO.AddComponent<MeshRenderer>();
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "TextStudioDecorations" };
                _mesh.hideFlags = HideFlags.DontSave;
                _mesh.MarkDynamic();
                _meshFilter.sharedMesh = _mesh;
            }
        }

        void EnsureUGUIChild()
        {
            if (_uguiGO == null)
            {
                var t = _hub.transform.Find(k_ChildName);
                if (t != null) _uguiGO = t.gameObject;
            }
            if (_uguiGO == null)
            {
                _uguiGO = new GameObject(k_ChildName);
                var rt = _uguiGO.AddComponent<RectTransform>();
                rt.SetParent(_hub.transform, false);

                // Mirror the hub's RectTransform so tile verts (which are in
                // TMP layout coords, equivalent to the hub's RectTransform
                // local space) land correctly inside the child. Stretching
                // with zero offsets and copying the pivot guarantees the
                // child's origin coincides with the hub's origin, making the
                // mesh math a direct paste-through.
                var parentRT = _hub.transform as RectTransform;
                if (parentRT != null)
                {
                    rt.pivot = parentRT.pivot;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                rt.anchoredPosition3D = Vector3.zero;
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.identity;
            }
            _uguiGO.hideFlags = k_ChildHideFlags;
            if (_uguiGO.GetComponent<CanvasRenderer>() == null)
                _uguiGO.AddComponent<CanvasRenderer>();
            if (_uguiGraphic == null) _uguiGraphic = _uguiGO.GetComponent<TextStudioDecorationGraphic>();
            if (_uguiGraphic == null) _uguiGraphic = _uguiGO.AddComponent<TextStudioDecorationGraphic>();
        }

        void UploadMesh()
        {
            if (_mesh == null) return;
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        void HideChild()
        {
            if (_meshRenderer != null) _meshRenderer.enabled = false;
            if (_mesh != null) _mesh.Clear();
            if (_uguiGraphic != null) _uguiGraphic.ClearGeometry();
        }

        void DestroyWorldChild()
        {
            if (_childGO == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_childGO);
            else UnityEngine.Object.DestroyImmediate(_childGO);
            _childGO = null;
            _meshFilter = null;
            _meshRenderer = null;
            _mesh = null;
        }

        void DestroyUGUIChild()
        {
            if (_uguiGO == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(_uguiGO);
            else UnityEngine.Object.DestroyImmediate(_uguiGO);
            _uguiGO = null;
            _uguiGraphic = null;
        }

        void DestroyChildren()
        {
            DestroyWorldChild();
            DestroyUGUIChild();
        }

        public void Dispose() => DestroyChildren();
    }

    /// <summary>
    /// <see cref="MaskableGraphic"/> used inside <see cref="TextStudioDecorations"/>
    /// to render curved underline / strikethrough / double underline / overline
    /// tiles inside a Canvas. The decoration subsystem calls
    /// <see cref="SetGeometry"/> every tick with updated tile verts/colors/uvs
    /// /triangles computed against the hub's font metrics, placement, and
    /// animation state; this component routes them through the standard UGUI
    /// <see cref="VertexHelper"/> pipeline so Canvas batching, masking, and
    /// draw order behave exactly like they do for a native TMP text graphic.
    /// </summary>
    [AddComponentMenu("")] // internal
    [DisallowMultipleComponent]
    public sealed class TextStudioDecorationGraphic : MaskableGraphic
    {
        readonly List<UIVertex> _vertices = new List<UIVertex>(64);
        readonly List<int> _triangles = new List<int>(96);

        Material _runtimeMaterial;
        Texture _mainTexture;

        internal int DebugVertexCount => _vertices.Count;

        public override Texture mainTexture => _mainTexture != null ? _mainTexture : base.mainTexture;
        public override Material material
        {
            get => _runtimeMaterial != null ? _runtimeMaterial : base.material;
            set => _runtimeMaterial = value;
        }

        /// <summary>
        /// Swap the graphic's material and main texture. Called from the
        /// decoration subsystem to inherit TMP's font material (atlas, SDF
        /// softness, outline / face color) so tiles visually match the text.
        /// </summary>
        public void ApplyMaterial(Material source)
        {
            if (_runtimeMaterial == source) return;
            _runtimeMaterial = source;
            _mainTexture = source != null ? source.mainTexture : null;
            SetMaterialDirty();
        }

        /// <summary>
        /// Upload tile geometry. Lists are copied into <see cref="UIVertex"/>
        /// entries so the UGUI mesh build pipeline can consume them directly.
        /// </summary>
        public void SetGeometry(List<Vector3> verts, List<Color32> colors, List<Vector4> uvs, List<int> tris)
        {
            _vertices.Clear();
            int count = verts.Count;
            for (int i = 0; i < count; i++)
            {
                _vertices.Add(new UIVertex
                {
                    position = verts[i],
                    color = colors[i],
                    uv0 = uvs[i],
                    normal = new Vector3(0f, 0f, -1f),
                    tangent = new Vector4(1f, 0f, 0f, -1f),
                });
            }
            _triangles.Clear();
            for (int i = 0; i < tris.Count; i++) _triangles.Add(tris[i]);
            SetVerticesDirty();
        }

        /// <summary>Drop all geometry - the graphic renders an empty mesh.</summary>
        public void ClearGeometry()
        {
            if (_vertices.Count == 0 && _triangles.Count == 0) return;
            _vertices.Clear();
            _triangles.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_vertices.Count == 0 || _triangles.Count < 3) return;

            for (int i = 0; i < _vertices.Count; i++)
                vh.AddVert(_vertices[i]);
            for (int t = 0; t + 2 < _triangles.Count; t += 3)
                vh.AddTriangle(_triangles[t], _triangles[t + 1], _triangles[t + 2]);
        }
    }
}
