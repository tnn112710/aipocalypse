using System;
using System.Collections.Generic;
using TMPro;
using TextStudio.BuiltIns;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TextCore;

#if UNITY_2023_2_OR_NEWER
using TmpUv0 = UnityEngine.Vector4;
#else
using TmpUv0 = UnityEngine.Vector2;
#endif

namespace TextStudio
{
    internal sealed class TextStudioTMPFrameApplier
    {
        static readonly ProfilerMarker MeshUploadProfilerMarker = new ProfilerMarker("Text Studio TMP Mesh Upload");

        const byte VertexChannel = 1 << 0;
        const byte ColorChannel = 1 << 1;
        const byte MaterialChannel = 1 << 2;
        // Material payload streams of one mesh: UV2, UV3, the local payload (Canvas tangents or world UV4), the source
        // coordinate normals, and the Squiggle UV1, which is sent with every publication because TMP can rewrite UV1.
        const byte Payload0Stream = 1 << 0;
        const byte Payload1Stream = 1 << 1;
        const byte LocalPayloadStream = 1 << 2;
        const byte CoordinateNormalStream = 1 << 3;
        const byte SquiggleStream = 1 << 4;
        const byte AllPayloadStreams = Payload0Stream | Payload1Stream | LocalPayloadStream | CoordinateNormalStream;
        Vector3[][] _cachedVertices;
        Color32[][] _cachedColors;
        TmpUv0[][] _cachedUv0;
        // Source quads and UV0 with each substituted presentation glyph replaced by its own laid-out quad at the same
        // vertex indices; a mesh's copy is created on its first substitution and invalidated whenever the source is recached.
        Vector3[][] _presentationVertices;
        TmpUv0[][] _presentationUv0;
        bool[] _presentationGeometryValid;
        // Code point whose quad each render glyph currently draws in place of its source glyph, or zero for the source.
        uint[] _appliedSubstitutes;
        int _appliedSubstituteCount;
        Vector4[][] _materialPayload0;
        Vector4[][] _materialPayload1;
        Vector4[][] _materialPayload2;
        Vector4[][] _squiggleUv1;
        Vector3[][] _sourceCoordinateNormals;
        // Whether any mesh holds coordinate normals that RestoreSourceNormals would restore; refreshed whenever they are cached.
        bool _hasSourceCoordinateNormals;
        // Set when coordinate normals are cached or uploaded and cleared once RestoreSourceNormals has restored every mesh;
        // until the next upload, restoring again would write the same normals and resubmit the same meshes.
        bool _sourceNormalRestorePending;
        List<Vector3> _canvasNormals;
        List<Vector4> _canvasTangents;
        Matrix4x4 _canvasPayloadMatrix = Matrix4x4.identity;
        Matrix4x4 _canvasPayloadTextMatrix, _canvasPayloadRootMatrix;
        bool _canvasPayloadInputsKnown, _canvasPayloadInputsConfirmed;
        internal long ManagedMeshPayloadBytes => (long)(_canvasNormals?.Capacity ?? 0) * 12 + (long)(_canvasTangents?.Capacity ?? 0) * 16 +
            (long)(_sourceNormalsScratch?.Capacity ?? 0) * 12;
        Vector3[][] _cachedSourceNormals;
        List<Vector3> _sourceNormalsScratch;
        bool[] _hadSourceNormals;
        bool[] _materialMeshDirty;
        // Payload streams per mesh whose uploaded copy differs from the CPU payload; a publication sends only these.
        byte[] _stalePayloadStreams;
        // Canvas payload space of every Canvas tangent and normal copy that has no stale bit.
        Matrix4x4 _uploadedStreamSpace;
        bool _hasUploadedStreamSpace;
        RenderGlyph[] _renderGlyphs;
        int _renderGlyphCount;
        byte[] _appliedChannels;
        int _materialPropertyRegistrationVersion = -1;
        MaterialPropertyBinding _focusProperty;
        MaterialPropertyBinding _dissolveProperty;
        MaterialPropertyBinding _outlineGlowProperty;
        MaterialPropertyBinding _surfaceSweepProperty;
        MaterialPropertyBinding _chromaticSplitProperty;
        MaterialPropertyBinding _pixelGridProperty;
        MaterialPropertyBinding _squiggleProperty;
        bool _hasUniformAppliedState;
        bool _submittedMeshInvalidated;
        EvaluatedGlyphState _uniformAppliedState;
        float _uniformAppliedAlphaMultiplier;
        Vector4 _sourceTextBounds;
        bool _hasSourceTextBounds;
        int _appearanceStyleSlot;
        CompiledTextStudio2DStyle _appearanceStyle;
        readonly TextStudio2DSelectorPass _selectorPass = new TextStudio2DSelectorPass();
        int _writingSourceSampleOffset = -1;

        internal bool HasMaterialPayloadStorage
        {
            get
            {
                return HasPayloadStorage(_materialPayload0) ||
                       HasPayloadStorage(_materialPayload1) ||
                       HasPayloadStorage(_materialPayload2);
            }
        }
        internal bool HasPresentationUvCache => HasUvCache(_cachedUv0);

        public void Clear()
        {
            _cachedVertices = null;
            _cachedColors = null;
            _cachedUv0 = null;
            _presentationVertices = null;
            _presentationUv0 = null;
            _presentationGeometryValid = null;
            _appliedSubstitutes = null;
            _appliedSubstituteCount = 0;
            _materialPayload0 = null;
            _materialPayload1 = null;
            _materialPayload2 = null;
            _squiggleUv1 = null;
            _sourceCoordinateNormals = null;
            _hasSourceCoordinateNormals = false;
            _sourceNormalRestorePending = false;
            _canvasNormals = null; _canvasTangents = null; _canvasPayloadMatrix = Matrix4x4.identity; _canvasPayloadInputsKnown = false;
            _cachedSourceNormals = null;
            _sourceNormalsScratch = null;
            _hadSourceNormals = null;
            _materialMeshDirty = null;
            _stalePayloadStreams = null;
            _hasUploadedStreamSpace = false;
            _renderGlyphs = null;
            _renderGlyphCount = 0;
            _appliedChannels = null;
            _hasUniformAppliedState = false;
            _submittedMeshInvalidated = false;
            _sourceTextBounds = Vector4.zero;
            _hasSourceTextBounds = false;
            _appearanceStyleSlot = 0;
            _appearanceStyle = null;
            _writingSourceSampleOffset = -1;
        }

        internal void InvalidateSubmittedMesh()
        {
            // TMP regeneration replaces submitted vertices and streams even when layout and evaluated state are unchanged.
            _submittedMeshInvalidated = true;
            _hasUniformAppliedState = false;
        }

        internal void SetSourceTextBounds(Vector4 bounds, bool enabled = true)
        {
            _sourceTextBounds = enabled ? bounds : Vector4.zero;
            _hasSourceTextBounds = enabled;
            if (!enabled) _sourceNormalsScratch = null;
        }

        internal void SetAppearanceProgram(
            int styleSlot,
            CompiledTextStudio2DStyle style)
        {
            int resolvedSlot = styleSlot > 0 &&
                               style != null &&
                               !style.IsPassThrough
                ? styleSlot
                : 0;
            if (_appearanceStyleSlot == resolvedSlot &&
                ReferenceEquals(_appearanceStyle, style))
            {
                return;
            }

            _appearanceStyleSlot = resolvedSlot;
            _appearanceStyle = resolvedSlot > 0 ? style : null;
            _hasUniformAppliedState = false;
        }

        internal bool TryGetMaterialPayload(
            int materialIndex,
            int vertexIndex,
            out Vector4 payload0,
            out Vector4 payload1,
            out Vector4 localPayload)
        {
            payload0 = default;
            payload1 = default;
            localPayload = default;
            if (_materialPayload0 == null ||
                materialIndex < 0 ||
                materialIndex >= _materialPayload0.Length ||
                !HasVertex(_materialPayload0[materialIndex], vertexIndex) ||
                !HasVertex(_materialPayload1[materialIndex], vertexIndex) ||
                !HasVertex(_materialPayload2[materialIndex], vertexIndex))
            {
                return false;
            }

            payload0 = _materialPayload0[materialIndex][vertexIndex];
            payload1 = _materialPayload1[materialIndex][vertexIndex];
            localPayload = _materialPayload2[materialIndex][vertexIndex];
            return true;
        }

        internal Vector3 GetSourceCoordinateNormal(int materialIndex, int vertexIndex)
        {
            return _sourceCoordinateNormals != null && materialIndex >= 0 && materialIndex < _sourceCoordinateNormals.Length &&
                HasVertex(_sourceCoordinateNormals[materialIndex], vertexIndex)
                ? _sourceCoordinateNormals[materialIndex][vertexIndex] : new Vector3(0, 0, -1);
        }

        /// <summary>
        /// The quad a character substitution such as a scramble presents in place of a glyph's authored quad: its
        /// source-space corners and atlas UV rectangle. False while the glyph draws its authored quad.
        /// </summary>
        internal bool TryGetSubstitutedQuad(
            int materialIndex,
            int vertexIndex,
            out Vector2 bottomLeft,
            out Vector2 bottomRight,
            out Vector2 topLeft,
            out Vector4 atlasUvRect)
        {
            bottomLeft = bottomRight = topLeft = default;
            atlasUvRect = default;
            if (_appliedSubstituteCount <= 0 || !IsPresentationGeometryValid(materialIndex))
                return false;
            Vector3[] vertices = _presentationVertices[materialIndex];
            TmpUv0[] uv0 = _presentationUv0[materialIndex];
            TmpUv0[] sourceUv0 = _cachedUv0 != null && materialIndex < _cachedUv0.Length ? _cachedUv0[materialIndex] : null;
            if (!HasQuad(vertices, vertexIndex) || !HasQuad(uv0, vertexIndex) || !HasQuad(sourceUv0, vertexIndex))
                return false;
            bool substituted = false;
            for (int corner = 0; corner < 4 && !substituted; corner++)
                substituted = !uv0[vertexIndex + corner].Equals(sourceUv0[vertexIndex + corner]);
            if (!substituted)
                return false;

            // TMP orders quad corners bottom left, top left, top right, bottom right.
            bottomLeft = vertices[vertexIndex];
            topLeft = vertices[vertexIndex + 1];
            bottomRight = vertices[vertexIndex + 3];
            Vector2 minimum = new Vector2(uv0[vertexIndex].x, uv0[vertexIndex].y);
            Vector2 maximum = minimum;
            for (int corner = 1; corner < 4; corner++)
            {
                Vector2 point = new Vector2(uv0[vertexIndex + corner].x, uv0[vertexIndex + corner].y);
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            atlasUvRect = new Vector4(minimum.x, minimum.y, maximum.x, maximum.y);
            return true;
        }

        /// <summary>
        /// The same-font character a character substitution such as a scramble presents in place of a rendered
        /// character. False while the character draws its authored glyph.
        /// </summary>
        internal bool TryGetSubstitutedCharacter(TMP_TextInfo textInfo, int characterIndex, out TMP_Character substitute)
        {
            substitute = null;
            if (_appliedSubstituteCount <= 0 || _appliedSubstitutes == null || textInfo?.characterInfo == null ||
                characterIndex < 0 || characterIndex >= textInfo.characterInfo.Length)
            {
                return false;
            }

            // Render glyphs follow TMP character order, so a binary search finds the one drawing the character.
            int low = 0;
            int high = _renderGlyphCount - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                int current = _renderGlyphs[middle].CharacterInfoIndex;
                if (current < characterIndex)
                {
                    low = middle + 1;
                }
                else if (current > characterIndex)
                {
                    high = middle - 1;
                }
                else
                {
                    uint codePoint = middle < _appliedSubstitutes.Length ? _appliedSubstitutes[middle] : 0u;
                    substitute = codePoint != 0u
                        ? ResolveSubstitute(in textInfo.characterInfo[characterIndex], (int)codePoint)
                        : null;
                    return substitute != null;
                }
            }
            return false;
        }

        static bool HasVertex<T>(T[] values, int index) => values != null && index >= 0 && index < values.Length;

        bool HasAppearanceProgram =>
            _appearanceStyleSlot > 0 &&
            _appearanceStyle != null &&
            !_appearanceStyle.IsPassThrough;
        bool HasWritingSource => _writingSourceSampleOffset >= 0;
        // Set by the bridge while a surface group reads this text's hidden glyph quads.
        internal bool RetainHiddenGlyphGeometry { get; set; }

        public void CacheSourceData(TMP_TextInfo textInfo)
        {
            CacheSourceData(textInfo, null);
        }

        public void CacheSourceData(TMP_TextInfo textInfo, TextStudioCompiledPlan plan)
        {
            _hasUniformAppliedState = false;
            if (textInfo == null || textInfo.meshInfo == null)
            {
                Clear();
                return;
            }

            int meshCount = textInfo.meshInfo.Length;
            bool cachePresentationUv = plan == null || plan.CharacterSubstitutionBindingCount > 0;
            if (_cachedVertices == null || _cachedVertices.Length < meshCount)
                _cachedVertices = new Vector3[meshCount][];
            if (_cachedColors == null || _cachedColors.Length < meshCount)
                _cachedColors = new Color32[meshCount][];
            if (cachePresentationUv && (_cachedUv0 == null || _cachedUv0.Length < meshCount))
                _cachedUv0 = new TmpUv0[meshCount][];
            else if (!cachePresentationUv)
                _cachedUv0 = null;
            if (!cachePresentationUv)
            {
                _presentationVertices = null;
                _presentationUv0 = null;
                _presentationGeometryValid = null;
            }
            else if (_presentationGeometryValid != null)
            {
                Array.Clear(_presentationGeometryValid, 0, _presentationGeometryValid.Length);
            }
            if (_materialPayload0 == null || _materialPayload0.Length < meshCount)
                _materialPayload0 = new Vector4[meshCount][];
            if (_materialPayload1 == null || _materialPayload1.Length < meshCount)
                _materialPayload1 = new Vector4[meshCount][];
            if (_materialPayload2 == null || _materialPayload2.Length < meshCount)
                _materialPayload2 = new Vector4[meshCount][];
            if (_sourceCoordinateNormals == null || _sourceCoordinateNormals.Length < meshCount)
                _sourceCoordinateNormals = new Vector3[meshCount][];
            if (_cachedSourceNormals == null || _cachedSourceNormals.Length < meshCount)
                _cachedSourceNormals = new Vector3[meshCount][];
            if (_hadSourceNormals == null || _hadSourceNormals.Length < meshCount)
                _hadSourceNormals = new bool[meshCount];
            if (_materialMeshDirty == null || _materialMeshDirty.Length < meshCount)
                _materialMeshDirty = new bool[meshCount];
            if (_stalePayloadStreams == null || _stalePayloadStreams.Length < meshCount)
                _stalePayloadStreams = new byte[meshCount];

            for (int m = 0; m < meshCount; m++)
            {
                // Recaching clears the CPU payload and recomputes the source coordinates, so no uploaded stream is current.
                _stalePayloadStreams[m] = AllPayloadStreams;
                Vector3[] verts = textInfo.meshInfo[m].vertices;
                if (verts != null)
                {
                    if (_cachedVertices[m] == null || _cachedVertices[m].Length < verts.Length)
                        _cachedVertices[m] = new Vector3[verts.Length];
                    Array.Copy(verts, _cachedVertices[m], verts.Length);
                }

                Color32[] cols = textInfo.meshInfo[m].colors32;
                if (cols != null)
                {
                    if (_cachedColors[m] == null || _cachedColors[m].Length < cols.Length)
                        _cachedColors[m] = new Color32[cols.Length];
                    Array.Copy(cols, _cachedColors[m], cols.Length);
                }

                TmpUv0[] uvs0 = cachePresentationUv ? textInfo.meshInfo[m].uvs0 : null;
                if (uvs0 != null && _cachedUv0 != null)
                {
                    if (_cachedUv0[m] == null || _cachedUv0[m].Length < uvs0.Length)
                        _cachedUv0[m] = new TmpUv0[uvs0.Length];
                    Array.Copy(uvs0, _cachedUv0[m], uvs0.Length);
                }

                int vertexCount = verts != null ? verts.Length : 0;
                ClearPayloadIfAllocated(ref _materialPayload0[m], vertexCount);
                ClearPayloadIfAllocated(ref _materialPayload1[m], vertexCount);
                ClearPayloadIfAllocated(ref _materialPayload2[m], vertexCount);
                CacheSourceCoordinateNormals(textInfo.meshInfo[m].mesh, m, verts, vertexCount);
            }

            // Entries past the current mesh count keep earlier payloads that RestoreSourceNormals can still reach.
            _hasSourceCoordinateNormals = false;
            for (int m = 0; m < _sourceCoordinateNormals.Length; m++)
                _hasSourceCoordinateNormals |= _sourceCoordinateNormals[m] != null;
            _sourceNormalRestorePending = _hasSourceCoordinateNormals;

            CacheRenderGlyphs(textInfo, plan);
            ApplyWritingSampleIndicesToCache();
        }

        internal bool SetWritingSourceSampleOffset(
            TMP_Text tmpText,
            int sampleOffset)
        {
            int resolved = sampleOffset >= 0 ? sampleOffset : -1;
            bool offsetChanged = _writingSourceSampleOffset != resolved;
            if (offsetChanged)
            {
                _writingSourceSampleOffset = resolved;
                ApplyWritingSampleIndicesToCache();
                MarkStalePayloadStreams(CoordinateNormalStream);
                UploadSourceCoordinateNormals(tmpText);
                _hasUniformAppliedState = false;
            }
            return offsetChanged;
        }

        void ApplyWritingSampleIndicesToCache()
        {
            if (_sourceCoordinateNormals == null || _renderGlyphs == null)
                return;
            for (int renderIndex = 0; renderIndex < _renderGlyphCount; renderIndex++)
            {
                RenderGlyph glyph = _renderGlyphs[renderIndex];
                int materialIndex = glyph.MaterialIndex;
                int vertexIndex = glyph.VertexIndex;
                if (materialIndex < 0 ||
                    materialIndex >= _sourceCoordinateNormals.Length ||
                    !HasQuad(_sourceCoordinateNormals[materialIndex], vertexIndex))
                {
                    continue;
                }
                Vector3[] target = _sourceCoordinateNormals[materialIndex];
                Vector3[] source = _cachedSourceNormals != null &&
                                   materialIndex < _cachedSourceNormals.Length
                    ? _cachedSourceNormals[materialIndex]
                    : null;
                float encodedMagnitude = _writingSourceSampleOffset >= 0
                    ? _writingSourceSampleOffset + glyph.UnitIndex + 2f
                    : 1f;
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    int index = vertexIndex + vertex;
                    float sourceZ = source != null && index < source.Length
                        ? source[index].z
                        : target[index].z;
                    float sign = sourceZ < 0f ? -1f : 1f;
                    target[index].z = sign * encodedMagnitude;
                }
            }
        }

        void UploadSourceCoordinateNormals(TMP_Text tmpText)
        {
            if (tmpText == null || _sourceCoordinateNormals == null)
                return;
            TMP_TextInfo textInfo = tmpText.textInfo;
            if (textInfo?.meshInfo == null)
                return;
            int meshCount = Mathf.Min(
                textInfo.meshInfo.Length,
                _sourceCoordinateNormals.Length);
            bool uploaded = false;
            TextMeshProUGUI canvasText = tmpText as TextMeshProUGUI;
            Matrix4x4 streamSpace = canvasText != null ? ResolveStreamSpace(canvasText) : Matrix4x4.identity;
            for (int materialIndex = 0; materialIndex < meshCount; materialIndex++)
            {
                Vector3[] normals = _sourceCoordinateNormals[materialIndex];
                Mesh mesh = textInfo.meshInfo[materialIndex].mesh;
                if (normals == null || mesh == null || normals.Length != mesh.vertexCount)
                    continue;
                UploadCoordinateNormals(mesh, normals, canvasText != null, streamSpace);
                if (_stalePayloadStreams != null && materialIndex < _stalePayloadStreams.Length)
                    _stalePayloadStreams[materialIndex] &= unchecked((byte)~CoordinateNormalStream);
                tmpText.UpdateGeometry(mesh, materialIndex);
                uploaded = true;
            }
            if (uploaded)
                TextStudioTMPMeshUpdateHooks.NotifyMeshUpdated(tmpText);
        }

        void CacheSourceCoordinateNormals(
            Mesh mesh,
            int materialIndex,
            Vector3[] vertices,
            int vertexCount)
        {
            if (!_hasSourceTextBounds || mesh == null || vertexCount <= 0)
            {
                _sourceCoordinateNormals[materialIndex] = null;
                _cachedSourceNormals[materialIndex] = null;
                _hadSourceNormals[materialIndex] = false;
                return;
            }

            _sourceNormalsScratch ??= new List<Vector3>(vertexCount);
            mesh.GetNormals(_sourceNormalsScratch);
            List<Vector3> sourceNormals = _sourceNormalsScratch;
            bool hadSourceNormals = sourceNormals.Count == vertexCount;
            _hadSourceNormals[materialIndex] = hadSourceNormals;
            if (hadSourceNormals)
            {
                if (_cachedSourceNormals[materialIndex] == null ||
                    _cachedSourceNormals[materialIndex].Length != vertexCount)
                {
                    _cachedSourceNormals[materialIndex] = new Vector3[vertexCount];
                }
                sourceNormals.CopyTo(_cachedSourceNormals[materialIndex]);
            }
            else
            {
                _cachedSourceNormals[materialIndex] = null;
            }

            if (_sourceCoordinateNormals[materialIndex] == null ||
                _sourceCoordinateNormals[materialIndex].Length != vertexCount)
            {
                _sourceCoordinateNormals[materialIndex] = new Vector3[vertexCount];
            }

            float width = _sourceTextBounds.z - _sourceTextBounds.x;
            float height = _sourceTextBounds.w - _sourceTextBounds.y;
            bool degenerateX = Mathf.Abs(width) <= 0.000001f;
            bool degenerateY = Mathf.Abs(height) <= 0.000001f;
            Vector3[] coordinateNormals = _sourceCoordinateNormals[materialIndex];
            for (int i = 0; i < vertexCount; i++)
            {
                Vector3 vertex = vertices[i];
                float normalizedX = degenerateX
                    ? 0.5f
                    : Mathf.Clamp01((vertex.x - _sourceTextBounds.x) / width);
                float normalizedY = degenerateY
                    ? 0.5f
                    : Mathf.Clamp01((vertex.y - _sourceTextBounds.y) / height);
                float sourceNormalZ = hadSourceNormals
                    ? sourceNormals[i].z
                    : -1f;
                coordinateNormals[i] = new Vector3(normalizedX, normalizedY, sourceNormalZ);
            }
        }

        void CacheRenderGlyphs(TMP_TextInfo textInfo, TextStudioCompiledPlan plan)
        {
            _renderGlyphCount = 0;
            // A recached source draws every glyph from its own quad again.
            if (_appliedSubstitutes != null)
                Array.Clear(_appliedSubstitutes, 0, _appliedSubstitutes.Length);
            _appliedSubstituteCount = 0;
            if (textInfo.characterInfo == null || textInfo.meshInfo == null)
                return;

            int characterCount = Mathf.Min(textInfo.characterCount, textInfo.characterInfo.Length);
            int animatedCount = plan != null ? plan.VisibleGlyphCount : characterCount;
            if (_renderGlyphs == null || _renderGlyphs.Length < characterCount)
                _renderGlyphs = new RenderGlyph[TextStudioBufferCapacity.ForCount(characterCount)];
            if (_appliedChannels == null || _appliedChannels.Length < animatedCount)
                _appliedChannels = new byte[TextStudioBufferCapacity.ForCount(animatedCount)];
            else if (animatedCount > 0)
                Array.Clear(_appliedChannels, 0, animatedCount);

            int sourceGlyphIndex = 0;
            int previousUnitIndex = -1;
            for (int i = 0; i < characterCount; i++)
            {
                ref TMP_CharacterInfo character = ref textInfo.characterInfo[i];
                int unitIndex = plan != null
                    ? plan.GetUnitIndexForDisplayChar(character.index)
                    : i;
                if (unitIndex < 0 || unitIndex >= animatedCount) continue;
                int sourceIndex = sourceGlyphIndex++;
                // A unit's characters are contiguous; its first carries the primary code point that presentation glyphs replace.
                bool isUnitPrimary = unitIndex != previousUnitIndex;
                previousUnitIndex = unitIndex;
                if (!character.isVisible) continue;
                int materialIndex = character.materialReferenceIndex;
                int vertexIndex = character.vertexIndex;
                if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length ||
                    vertexIndex < 0)
                {
                    continue;
                }

                _renderGlyphs[_renderGlyphCount++] = new RenderGlyph(
                    i,
                    unitIndex,
                    sourceIndex,
                    materialIndex,
                    vertexIndex,
                    _cachedColors != null &&
                    materialIndex < _cachedColors.Length &&
                    HasQuad(_cachedColors[materialIndex], vertexIndex) &&
                    HasUniformQuadColor(_cachedColors[materialIndex], vertexIndex),
                    isUnitPrimary);
            }
        }

        internal bool RepublishFreshLayoutColors(
            TMP_Text tmpText,
            in TextStudioFrame frame,
            PresentationGlyph[] presentationGlyphs,
            IPresentationGlyphProvider glyphProvider)
        {
            TextStudioCompiledPlan plan = frame.Plan.CompiledPlan;
            TMP_TextInfo info = tmpText != null ? tmpText.textInfo : null;
            if (plan == null || info?.characterInfo == null || info.meshInfo == null ||
                !string.Equals(tmpText.text, plan.DisplayText ?? plan.ResolvedText, StringComparison.Ordinal) ||
                !frame.Animated.IsCreated || frame.AnimatedCount != plan.VisibleGlyphCount ||
                frame.Animated.Length < frame.AnimatedCount || info.characterCount > info.characterInfo.Length)
                return false;

            // Validate the current native mapping in full before writing. The cached render-glyph/material/vertex
            // mapping belongs to the old layout and is deliberately not consulted or replaced here.
            for (int index = 0; index < info.characterCount; index++)
            {
                ref TMP_CharacterInfo character = ref info.characterInfo[index];
                if (!character.isVisible) continue;
                int unit = plan.GetUnitIndexForDisplayChar(character.index);
                if (unit < 0 || unit >= frame.AnimatedCount ||
                    character.materialReferenceIndex < 0 || character.materialReferenceIndex >= info.meshInfo.Length ||
                    !HasQuad(info.meshInfo[character.materialReferenceIndex].colors32, character.vertexIndex))
                    return false;
            }

            bool hasPresentationGlyphs = (glyphProvider != null || plan.CharacterSubstitutionBindingCount > 0) &&
                presentationGlyphs != null && frame.PresentationGlyphCount > 0;
            for (int index = 0; index < info.characterCount; index++)
            {
                ref TMP_CharacterInfo character = ref info.characterInfo[index];
                if (!character.isVisible) continue;
                int unit = plan.GetUnitIndexForDisplayChar(character.index);
                ref readonly EvaluatedGlyphState state = ref frame.Animated.Items[unit];
                float alphaMultiplier = hasPresentationGlyphs && unit < presentationGlyphs.Length
                    ? presentationGlyphs[unit].alphaMultiplier : 1f;
                byte alpha = (byte)(Mathf.Clamp01(state.alpha * alphaMultiplier) * 255f);
                bool applyRgb = (ResolveRenderChannels(in state) & TextStudioRenderChannels.Color) != 0;
                Color32[] colors = info.meshInfo[character.materialReferenceIndex].colors32;
                for (int corner = 0; corner < 4; corner++)
                {
                    int vertex = character.vertexIndex + corner;
                    colors[vertex] = ResolveColor(in state, colors[vertex], alpha, applyRgb);
                }
            }
            // Preserve all new native positions, UVs and materials. The normal layout owner will recache them and
            // publish transforms/material streams; its invalidated mesh and dirty work remain pending.
            tmpText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            return true;
        }

        public void ApplyFrame(
            TMP_Text tmpText,
            in TextStudioFrame frame,
            PresentationGlyph[] presentationGlyphs,
            IPresentationGlyphProvider glyphProvider,
            bool forceAll,
            float alphaMultiplier = 1f,
            bool applyPlacement = true,
            bool notifyMeshUpdated = true)
        {
            if (tmpText == null)
                return;

            TMP_TextInfo textInfo = tmpText.textInfo;
            if (textInfo == null)
                return;
            bool hasComposedGlyphStates = frame.GlyphStates.IsCreated &&
                                          frame.GlyphStateCount > 0;
            if ((!hasComposedGlyphStates && !frame.Animated.IsCreated) ||
                !frame.Dirty.IsCreated)
                return;

            if (_renderGlyphCount == 0)
                return;
            int stateCount = hasComposedGlyphStates
                ? frame.GlyphStateCount
                : frame.AnimatedCount;
            TextStudioCompiledPlan plan = frame.Plan.CompiledPlan;
            bool hasPresentationChanges = glyphProvider != null ||
                                          (plan != null && plan.CharacterSubstitutionBindingCount > 0);
            bool hasPresentationGlyphs = hasPresentationChanges &&
                                         presentationGlyphs != null &&
                                         frame.PresentationGlyphCount > 0;
            // Substitute quads left on the mesh once presentation glyphs disappear restore their source quads in one full presentation pass.
            bool restoreSubstitutes = !hasPresentationGlyphs && _appliedSubstituteCount > 0;
            bool usePresentationGlyphs = hasPresentationGlyphs || restoreSubstitutes;
            // TMP regeneration resubmits source quads, so applied substitutes are written again even without a vertex channel.
            bool rewriteSubstitutes = _submittedMeshInvalidated;
            bool forceComposedSubmission = forceAll || _submittedMeshInvalidated;
            bool applyAll = forceAll || _submittedMeshInvalidated || restoreSubstitutes || frame.Dirty.AreAllDirty(stateCount);
            if (!applyAll && !frame.Dirty.AnyDirtyInRange(0, stateCount))
                return;

            TextStudioReadOnlyBuffer<EvaluatedGlyphState> states = frame.Animated;
            EvaluatedGlyphState[] stateItems = states.Items;
            TextStudioReadOnlyBuffer<GlyphPlacement> placements = frame.Placements;
            int placementCount = frame.PlacementCount;
            bool usePlacement = applyPlacement && placements.IsCreated && placementCount > 0;
            if (usePresentationGlyphs)
                EnsurePresentationUvCache(textInfo);
            // Frame-level views are read once; a frame without render properties gives every glyph an empty view.
            TextStudioRenderPropertyView frameRenderProperties = frame.RenderProperties;
            bool frameHasRenderProperties = !frameRenderProperties.IsEmpty;
            TextStudioReadOnlyBuffer<float> effectUnitScales = frame.EffectUnitScales;
            float fallbackEffectUnitScale = effectUnitScales.IsCreated && effectUnitScales.Count >= stateCount
                ? 1f
                : TextStudioTMPBridge.ResolveEffectUnitScale(tmpText);
            PlacementCoordinateMap placementMap = ResolvePlacementCoordinateMap(textInfo, in frame, usePlacement);
            bool wroteVertices = false;
            bool wroteColors = false;
            bool wroteUv0 = false;
            if (_materialMeshDirty != null)
            {
                Array.Clear(_materialMeshDirty, 0, _materialMeshDirty.Length);
                if (_submittedMeshInvalidated && _materialPayload0 != null)
                {
                    for (int index = 0; index < _materialMeshDirty.Length && index < _materialPayload0.Length; index++)
                    {
                        if (_materialPayload0[index] == null)
                            continue;
                        // TMP regeneration can replace every payload channel, so the mesh republishes them all.
                        MarkPayloadStreams(index, AllPayloadStreams);
                    }
                }
            }
            RefreshMaterialPropertyBindings();
            bool cacheUniformMaterialState =
                !hasComposedGlyphStates &&
                !HasAppearanceProgram &&
                !HasWritingSource &&
                frame.AnimatedStateIsUniform &&
                frame.RenderProperties.IsEmpty &&
                applyAll &&
                !usePlacement &&
                !usePresentationGlyphs &&
                frame.AnimatedCount > 0;
            EvaluatedGlyphState uniformMaterialState = default;
            if (cacheUniformMaterialState)
            {
                uniformMaterialState = states[0];
                TextStudioRenderChannels uniformChannels = ResolveRenderChannels(in uniformMaterialState);
                byte uniformAppliedChannels = ResolveAnimatedChannels(
                    uniformChannels,
                    false,
                    alphaMultiplier,
                    false);
                cacheUniformMaterialState =
                    (uniformAppliedChannels & MaterialChannel) != 0 &&
                    (uniformAppliedChannels & (VertexChannel | ColorChannel)) == 0;
            }

            if (cacheUniformMaterialState &&
                _hasUniformAppliedState &&
                _uniformAppliedAlphaMultiplier.Equals(alphaMultiplier) &&
                _uniformAppliedState.HasSameRenderedValues(in uniformMaterialState))
            {
                return;
            }

            if (!cacheUniformMaterialState)
                _hasUniformAppliedState = false;
            // Plain TMP output multiplies every layer by vertex alpha, so fully transparent glyphs can drop their area.
            bool collapseTransparentGlyphs =
                !RetainHiddenGlyphGeometry &&
                !hasComposedGlyphStates &&
                !HasAppearanceProgram &&
                !HasWritingSource &&
                !usePresentationGlyphs;
            // Without composed states, placement, presentation glyphs, an alpha multiplier, render properties, appearance or writing, a position-only glyph whose last frame wrote no color or material reaches the position path below with no other channel, so these frame invariants let it translate its cached quad before the per-glyph channel resolution.
            bool positionOnlyFrame =
                !hasComposedGlyphStates &&
                !usePlacement &&
                !usePresentationGlyphs &&
                alphaMultiplier == 1f &&
                !frameHasRenderProperties &&
                !HasAppearanceProgram &&
                !HasWritingSource &&
                _cachedVertices != null &&
                _appliedChannels != null;
            float[] effectUnitScaleItems = effectUnitScales.Items;
            int effectUnitScaleCount = effectUnitScales.Count;
            int animatedLength = states.Length;
            int positionMaterialIndex = -1;
            Vector3[] positionSourceVertices = null;
            Vector3[] positionDestinationVertices = null;
            // Every glyph of a uniform frame shares one state, so the color path resolves each distinct uniform source color once.
            bool uniformColorStates = frame.AnimatedStateIsUniform;
            bool hasUniformQuadColor = false;
            Color32 uniformQuadSourceColor = default;
            Color32 uniformQuadColor = default;

            // Only appearance programs read the selector pass, so plain frames skip opening one.
            if (HasAppearanceProgram)
                _selectorPass.Begin();
            for (int renderIndex = 0; renderIndex < _renderGlyphCount; renderIndex++)
            {
                ref RenderGlyph renderGlyph = ref _renderGlyphs[renderIndex];
                int unitIndex = renderGlyph.UnitIndex;
                if (unitIndex < 0 || unitIndex >= stateCount)
                    continue;
                if (!applyAll && !frame.Dirty.IsDirty(unitIndex))
                    continue;

                int matIdx = renderGlyph.MaterialIndex;
                int vertIdx = renderGlyph.VertexIndex;
                if (matIdx < 0 || matIdx >= textInfo.meshInfo.Length)
                    continue;

                if (positionOnlyFrame && unitIndex < animatedLength)
                {
                    ref readonly EvaluatedGlyphState positionState = ref stateItems[unitIndex];
                    TextStudioRenderChannels positionChannels =
                        positionState.renderChannels != TextStudioRenderChannels.None
                            ? positionState.renderChannels
                            : ResolveRenderChannels(in positionState);
                    if (positionChannels == TextStudioRenderChannels.Position &&
                        unitIndex < _appliedChannels.Length &&
                        (_appliedChannels[unitIndex] & (ColorChannel | MaterialChannel)) == 0)
                    {
                        if (matIdx != positionMaterialIndex)
                        {
                            positionMaterialIndex = matIdx;
                            positionSourceVertices = matIdx < _cachedVertices.Length
                                ? _cachedVertices[matIdx]
                                : null;
                            positionDestinationVertices = textInfo.meshInfo[matIdx].vertices;
                        }

                        if (HasQuad(positionSourceVertices, vertIdx) &&
                            HasQuad(positionDestinationVertices, vertIdx))
                        {
                            float positionUnitScale = Mathf.Max(
                                0.0001f,
                                effectUnitScaleItems != null && unitIndex < effectUnitScaleCount
                                    ? effectUnitScaleItems[unitIndex]
                                    : fallbackEffectUnitScale);
                            Vector3 positionTranslation = positionState.positionOffset * positionUnitScale;
                            positionDestinationVertices[vertIdx] = positionSourceVertices[vertIdx] + positionTranslation;
                            positionDestinationVertices[vertIdx + 1] = positionSourceVertices[vertIdx + 1] + positionTranslation;
                            positionDestinationVertices[vertIdx + 2] = positionSourceVertices[vertIdx + 2] + positionTranslation;
                            positionDestinationVertices[vertIdx + 3] = positionSourceVertices[vertIdx + 3] + positionTranslation;
                            wroteVertices = true;
                            _appliedChannels[unitIndex] = VertexChannel;
                            continue;
                        }
                    }
                }

                float resolvedAlphaMultiplier = alphaMultiplier;
                if (hasPresentationGlyphs &&
                    plan != null &&
                    unitIndex < plan.VisibleGlyphCount &&
                    unitIndex < presentationGlyphs.Length)
                {
                    resolvedAlphaMultiplier *= presentationGlyphs[unitIndex].alphaMultiplier;
                }

                // The substitute quad replaces the source quad as this glyph's transform input; a change rewrites its vertices.
                bool presentationQuadChanged = usePresentationGlyphs &&
                    UpdatePresentationGeometry(
                        textInfo,
                        plan,
                        renderIndex,
                        in renderGlyph,
                        hasPresentationGlyphs ? presentationGlyphs : null,
                        frame.PresentationGlyphCount,
                        rewriteSubstitutes);

                if (hasComposedGlyphStates)
                {
                    if (ApplyComposedGlyph(
                            textInfo,
                            in frame,
                            in renderGlyph,
                            unitIndex,
                            resolvedAlphaMultiplier,
                            in placementMap,
                            usePresentationGlyphs,
                            forceComposedSubmission,
                            out bool composedVertices,
                            out bool composedColors,
                            out bool composedUv0))
                    {
                        wroteVertices |= composedVertices;
                        wroteColors |= composedColors;
                        wroteUv0 |= composedUv0;
                    }
                    continue;
                }

                if (unitIndex >= frame.Animated.Length)
                    continue;
                ref readonly EvaluatedGlyphState state = ref stateItems[unitIndex];
                TextStudioRenderPropertyGlyphView glyphProperties = frameHasRenderProperties
                    ? frameRenderProperties.GetGlyph(unitIndex)
                    : default;
                byte appearanceOperationMask = HasAppearanceProgram
                    ? TextStudio2DSelectorEvaluator.EvaluateOperationMask(
                        _appearanceStyle,
                        in frame,
                        unitIndex,
                        0,
                        _selectorPass)
                    : (byte)0;
                float effectUnitScale = Mathf.Max(
                    0.0001f,
                    effectUnitScales.IsCreated && unitIndex < effectUnitScales.Length
                        ? effectUnitScales[unitIndex]
                        : fallbackEffectUnitScale);

                TextStudioRenderChannels stateChannels = ResolveRenderChannels(in state);
                byte currentChannels = ResolveAnimatedChannels(
                    stateChannels,
                    usePlacement,
                    resolvedAlphaMultiplier,
                    !glyphProperties.IsEmpty ||
                    HasAppearanceProgram ||
                    HasWritingSource);
                byte previousChannels = _appliedChannels != null && unitIndex < _appliedChannels.Length
                    ? _appliedChannels[unitIndex]
                    : (byte)0;
                if (!usePlacement &&
                    !usePresentationGlyphs &&
                    resolvedAlphaMultiplier == 1f &&
                    stateChannels == TextStudioRenderChannels.Position &&
                    currentChannels == VertexChannel &&
                    (previousChannels & (ColorChannel | MaterialChannel)) == 0)
                {
                    ref TMP_MeshInfo positionMeshInfo = ref textInfo.meshInfo[matIdx];
                    Vector3[] destinationVertices = positionMeshInfo.vertices;
                    Vector3[] sourceVertices =
                        _cachedVertices != null &&
                        matIdx < _cachedVertices.Length
                            ? _cachedVertices[matIdx]
                            : null;
                    if (HasQuad(sourceVertices, vertIdx) &&
                        HasQuad(destinationVertices, vertIdx))
                    {
                        Vector3 translation = state.positionOffset * effectUnitScale;
                        destinationVertices[vertIdx] = sourceVertices[vertIdx] + translation;
                        destinationVertices[vertIdx + 1] = sourceVertices[vertIdx + 1] + translation;
                        destinationVertices[vertIdx + 2] = sourceVertices[vertIdx + 2] + translation;
                        destinationVertices[vertIdx + 3] = sourceVertices[vertIdx + 3] + translation;
                        wroteVertices = true;
                        if (_appliedChannels != null && unitIndex < _appliedChannels.Length)
                            _appliedChannels[unitIndex] = VertexChannel;
                        continue;
                    }
                }

                // Vertex-only scale, z-rotation, pivot and position combinations rebuild axis-aligned quads directly.
                const TextStudioRenderChannels affineChannels =
                    TextStudioRenderChannels.Position | TextStudioRenderChannels.Rotation |
                    TextStudioRenderChannels.Pivot | TextStudioRenderChannels.Scale;
                if (!usePlacement &&
                    !usePresentationGlyphs &&
                    resolvedAlphaMultiplier == 1f &&
                    (stateChannels & ~affineChannels) == 0 &&
                    (stateChannels & (TextStudioRenderChannels.Rotation | TextStudioRenderChannels.Scale)) != 0 &&
                    currentChannels == VertexChannel &&
                    (previousChannels & (ColorChannel | MaterialChannel)) == 0 &&
                    Mathf.Abs(state.rotation.x) <= 0.000001f &&
                    Mathf.Abs(state.rotation.y) <= 0.000001f)
                {
                    ref TMP_MeshInfo rotationMeshInfo = ref textInfo.meshInfo[matIdx];
                    Vector3[] destinationVertices = rotationMeshInfo.vertices;
                    Vector3[] sourceVertices =
                        _cachedVertices != null &&
                        matIdx < _cachedVertices.Length
                            ? _cachedVertices[matIdx]
                            : null;
                    if (HasQuad(sourceVertices, vertIdx) &&
                        HasQuad(destinationVertices, vertIdx) &&
                        TryApplySpecializedVertexQuad(
                            sourceVertices,
                            destinationVertices,
                            vertIdx,
                            in state,
                            stateChannels,
                            state.positionOffset * effectUnitScale))
                    {
                        wroteVertices = true;
                        if (_appliedChannels != null && unitIndex < _appliedChannels.Length)
                            _appliedChannels[unitIndex] = VertexChannel;
                        continue;
                    }
                }

                // Alpha-only glyphs share this path unless they are fully transparent and must collapse their quad below.
                if (!usePlacement &&
                    !usePresentationGlyphs &&
                    resolvedAlphaMultiplier == 1f &&
                    (stateChannels == TextStudioRenderChannels.Color ||
                     (stateChannels == TextStudioRenderChannels.Alpha &&
                      !(collapseTransparentGlyphs &&
                        (byte)(Mathf.Clamp01(state.alpha * resolvedAlphaMultiplier) * 255f) == 0))) &&
                    currentChannels == ColorChannel &&
                    (previousChannels & (VertexChannel | MaterialChannel)) == 0)
                {
                    ref TMP_MeshInfo colorMeshInfo = ref textInfo.meshInfo[matIdx];
                    Color32[] destinationColors = colorMeshInfo.colors32;
                    Color32[] sourceColors =
                        _cachedColors != null &&
                        matIdx < _cachedColors.Length
                            ? _cachedColors[matIdx]
                            : null;
                    if (HasQuad(sourceColors, vertIdx) &&
                        HasQuad(destinationColors, vertIdx))
                    {
                        if (uniformColorStates && renderGlyph.HasUniformSourceColor)
                        {
                            Color32 sourceColor = sourceColors[vertIdx];
                            if (!hasUniformQuadColor || !HasSameColor(sourceColor, uniformQuadSourceColor))
                            {
                                uniformQuadSourceColor = sourceColor;
                                uniformQuadColor = ResolveUniformQuadColor(
                                    in state,
                                    sourceColor,
                                    stateChannels,
                                    1f);
                                hasUniformQuadColor = true;
                            }

                            destinationColors[vertIdx] = uniformQuadColor;
                            destinationColors[vertIdx + 1] = uniformQuadColor;
                            destinationColors[vertIdx + 2] = uniformQuadColor;
                            destinationColors[vertIdx + 3] = uniformQuadColor;
                        }
                        else
                        {
                            ApplySpecializedColorQuad(
                                sourceColors,
                                destinationColors,
                                vertIdx,
                                renderGlyph.HasUniformSourceColor,
                                in state,
                                stateChannels,
                                1f);
                        }
                        wroteColors = true;
                        if (_appliedChannels != null && unitIndex < _appliedChannels.Length)
                            _appliedChannels[unitIndex] = ColorChannel;
                        continue;
                    }
                }

                // Affine vertex channels with one alpha or color channel, such as Expand's size and fade, rebuild the
                // quad and color it as the general path below would, without its per-glyph setup. Fully transparent
                // glyphs still take that path to collapse their quad.
                TextStudioRenderChannels affineColorChannels = stateChannels & TextStudioRenderChannels.VertexColor;
                if (!usePlacement &&
                    !usePresentationGlyphs &&
                    resolvedAlphaMultiplier == 1f &&
                    (stateChannels & ~(affineChannels | TextStudioRenderChannels.VertexColor)) == 0 &&
                    (stateChannels & affineChannels) != 0 &&
                    (affineColorChannels == TextStudioRenderChannels.Alpha ||
                     affineColorChannels == TextStudioRenderChannels.Color) &&
                    currentChannels == (VertexChannel | ColorChannel) &&
                    (previousChannels & MaterialChannel) == 0 &&
                    Mathf.Abs(state.rotation.x) <= 0.000001f &&
                    Mathf.Abs(state.rotation.y) <= 0.000001f &&
                    !(collapseTransparentGlyphs && (byte)(Mathf.Clamp01(state.alpha) * 255f) == 0))
                {
                    ref TMP_MeshInfo affineMeshInfo = ref textInfo.meshInfo[matIdx];
                    Vector3[] destinationVertices = affineMeshInfo.vertices;
                    Color32[] destinationColors = affineMeshInfo.colors32;
                    Vector3[] sourceVertices =
                        _cachedVertices != null && matIdx < _cachedVertices.Length
                            ? _cachedVertices[matIdx]
                            : null;
                    Color32[] sourceColors =
                        _cachedColors != null && matIdx < _cachedColors.Length
                            ? _cachedColors[matIdx]
                            : null;
                    if (HasQuad(sourceVertices, vertIdx) &&
                        HasQuad(destinationVertices, vertIdx) &&
                        HasQuad(sourceColors, vertIdx) &&
                        HasQuad(destinationColors, vertIdx) &&
                        TryApplyAffineVertexQuad(
                            sourceVertices,
                            destinationVertices,
                            vertIdx,
                            in state,
                            stateChannels & affineChannels,
                            state.positionOffset * effectUnitScale))
                    {
                        ApplySpecializedColorQuad(
                            sourceColors,
                            destinationColors,
                            vertIdx,
                            renderGlyph.HasUniformSourceColor,
                            in state,
                            affineColorChannels,
                            1f);
                        wroteVertices = true;
                        wroteColors = true;
                        if (_appliedChannels != null && unitIndex < _appliedChannels.Length)
                            _appliedChannels[unitIndex] = VertexChannel | ColorChannel;
                        continue;
                    }
                }

                bool applyVertices = ((currentChannels | previousChannels) & VertexChannel) != 0 ||
                                     presentationQuadChanged;
                bool applyColors = ((currentChannels | previousChannels) & ColorChannel) != 0;
                bool applyColorEffects = (currentChannels & ColorChannel) != 0;
                bool applyMaterial = ((currentChannels | previousChannels) & MaterialChannel) != 0;

                ref TMP_MeshInfo meshInfo = ref textInfo.meshInfo[matIdx];
                Vector3[] verts = applyVertices ? meshInfo.vertices : null;
                Color32[] colors = applyColors ? meshInfo.colors32 : null;
                TmpUv0[] uvs0 = usePresentationGlyphs ? meshInfo.uvs0 : null;
                if ((applyVertices && !HasQuad(verts, vertIdx)) ||
                    (applyColors && !HasQuad(colors, vertIdx)))
                    continue;

                bool hasCachedVerts = applyVertices && _cachedVertices != null
                    && matIdx < _cachedVertices.Length
                    && HasQuad(_cachedVertices[matIdx], vertIdx);
                bool hasCachedColors = applyColors && _cachedColors != null
                    && matIdx < _cachedColors.Length
                    && HasQuad(_cachedColors[matIdx], vertIdx);
                bool hasCachedUv0 = usePresentationGlyphs && _cachedUv0 != null
                    && matIdx < _cachedUv0.Length
                    && HasQuad(_cachedUv0[matIdx], vertIdx);

                Vector3[] srcVerts = hasCachedVerts
                    ? ResolveSourceVertices(matIdx, usePresentationGlyphs)
                    : verts;
                Color32[] srcColors = hasCachedColors ? _cachedColors[matIdx] : colors;
                TmpUv0[] srcUv0 = hasCachedUv0 ? ResolveSourceUv0(matIdx) : uvs0;
                bool hasUniformSourceColor = hasCachedColors
                    ? renderGlyph.HasUniformSourceColor
                    : applyColors && HasUniformQuadColor(srcColors, vertIdx);

                GlyphPlacement placement = default;
                if (usePlacement && unitIndex < placementCount)
                {
                    placement = placements[unitIndex];
                    placement.position = placementMap.Transform(placement.position);
                }

                bool usedLinear2DFastPath = !usePlacement && TryApplyLinear2DQuad(
                    srcVerts,
                    srcColors,
                    verts,
                    colors,
                    vertIdx,
                    hasUniformSourceColor,
                    in state,
                    stateChannels,
                    effectUnitScale,
                    resolvedAlphaMultiplier,
                    applyVertices,
                    applyColors,
                    applyColorEffects);
                if (!usedLinear2DFastPath)
                {
                    Vector3 srcCenter = (srcVerts[vertIdx] + srcVerts[vertIdx + 2]) * 0.5f;
                    // Several rendered characters can belong to one grapheme. Preserve their
                    // offsets from its layout anchor when moving or rotating the complete unit.
                    if (usePlacement && unitIndex < placementCount &&
                        frame.BasePositions.IsCreated && unitIndex < frame.BasePositions.Length)
                        srcCenter = placementMap.Transform(frame.BasePositions[unitIndex]);
                    TextStudioGlyphQuadTransform transform = TextStudioGlyphQuadTransformer.CreateFromQuad(
                        srcVerts[vertIdx],
                        srcVerts[vertIdx + 1],
                        srcVerts[vertIdx + 2],
                        srcVerts[vertIdx + 3],
                        srcCenter,
                        in state,
                        in placement,
                        usePlacement && unitIndex < placementCount,
                        effectUnitScale,
                        resolvedAlphaMultiplier);

                    bool hasCornerOffsets = applyVertices && state.cornerOffsets.HasAny;
                    bool hasUniformColor = applyColors && hasUniformSourceColor;
                    Color32 uniformColor = applyColors && hasUniformColor
                        ? (applyColorEffects
                            ? transform.ApplyColor(srcColors[vertIdx])
                            : srcColors[vertIdx])
                        : default;

                    for (int v = 0; v < 4; v++)
                    {
                        if (applyVertices)
                        {
                            Vector3 srcVert = srcVerts[vertIdx + v];
                            verts[vertIdx + v] = hasCornerOffsets
                                ? transform.ApplyPosition(
                                    srcVert,
                                    state.cornerOffsets.GetCanonicalVertexOffset(v),
                                    effectUnitScale)
                                : transform.ApplyPosition(srcVert);
                        }

                        if (applyColors)
                        {
                            colors[vertIdx + v] = hasUniformColor
                                ? uniformColor
                                : (applyColorEffects
                                    ? transform.ApplyColor(srcColors[vertIdx + v])
                                    : srcColors[vertIdx + v]);
                        }
                    }
                }

                // A glyph with zero vertex alpha draws nothing, so its quad collapses to one point instead of being
                // rasterized, for example behind a typewriter's reveal front. Its next visible frame rebuilds the quad.
                if (collapseTransparentGlyphs &&
                    applyColors &&
                    !applyMaterial &&
                    (byte)(Mathf.Clamp01(state.alpha * resolvedAlphaMultiplier) * 255f) == 0 &&
                    HasQuad(meshInfo.vertices, vertIdx))
                {
                    Vector3[] collapsedVertices = meshInfo.vertices;
                    Vector3[] cachedSource = _cachedVertices != null && matIdx < _cachedVertices.Length
                        ? _cachedVertices[matIdx]
                        : null;
                    Vector3 anchor = HasQuad(cachedSource, vertIdx)
                        ? cachedSource[vertIdx]
                        : collapsedVertices[vertIdx];
                    collapsedVertices[vertIdx] = anchor;
                    collapsedVertices[vertIdx + 1] = anchor;
                    collapsedVertices[vertIdx + 2] = anchor;
                    collapsedVertices[vertIdx + 3] = anchor;
                    applyVertices = true;
                    currentChannels |= VertexChannel;
                }

                if (applyVertices)
                    wroteVertices = true;
                if (applyColors)
                    wroteColors = true;
                if (applyMaterial)
                {
                    EnsureMaterialPayloadCapacity(
                        matIdx,
                        meshInfo.vertices != null ? meshInfo.vertices.Length : 0);
                    MarkPayloadStreams(
                        matIdx,
                        ApplyMaterialPayload(
                            matIdx,
                            vertIdx,
                            renderGlyph.SourceIndex,
                            in glyphProperties,
                            appearanceOperationMask));
                }
                if (_appliedChannels != null && unitIndex < _appliedChannels.Length)
                    _appliedChannels[unitIndex] = currentChannels;

                if (uvs0 != null && srcUv0 != null)
                {
                    for (int v = 0; v < 4; v++)
                    {
                        if (vertIdx + v < uvs0.Length && vertIdx + v < srcUv0.Length)
                        {
                            uvs0[vertIdx + v] = srcUv0[vertIdx + v];
                            wroteUv0 = true;
                        }
                    }
                }

                // Presentation UV restoration rewrites UV0 from the TMP source or substitute cache; stamp the identity afterwards.
                if (applyMaterial && StampAppearanceIdentity(meshInfo.uvs0, vertIdx, ResolveAppearanceIdentity(appearanceOperationMask)))
                    wroteUv0 = true;
            }

            try
            {
                TMP_VertexDataUpdateFlags updateFlags = 0;
                if (wroteVertices)
                    updateFlags |= TMP_VertexDataUpdateFlags.Vertices;
                if (wroteColors)
                    updateFlags |= TMP_VertexDataUpdateFlags.Colors32;
                if (wroteUv0)
                    updateFlags |= TMP_VertexDataUpdateFlags.Uv0;
                // Payload streams go up first, so a frame that also writes vertices, colors or UV0 submits each mesh once.
                ApplyMaterialStreams(tmpText, textInfo, updateFlags != 0);
                if (updateFlags != 0)
                {
                    using (MeshUploadProfilerMarker.Auto())
                        tmpText.UpdateVertexData(updateFlags);
                }
                _submittedMeshInvalidated = false;
                // Optional graphics integrations may schedule a Canvas rebuild, so native regeneration leaves notifications to normal frame application.
                if (notifyMeshUpdated)
                    TextStudioTMPMeshUpdateHooks.NotifyMeshUpdated(tmpText);
                if (cacheUniformMaterialState)
                {
                    _uniformAppliedState = uniformMaterialState;
                    _uniformAppliedAlphaMultiplier = alphaMultiplier;
                    _hasUniformAppliedState = true;
                }
            }
            catch (MissingReferenceException)
            {
                _hasUniformAppliedState = false;
            }
        }

        bool ApplyComposedGlyph(
            TMP_TextInfo textInfo,
            in TextStudioFrame frame,
            in RenderGlyph renderGlyph,
            int unitIndex,
            float alphaMultiplier,
            in PlacementCoordinateMap placementMap,
            bool usePresentationGlyphs,
            bool forceSubmission,
            out bool wroteVertices,
            out bool wroteColors,
            out bool wroteUv0)
        {
            wroteVertices = false;
            wroteColors = false;
            wroteUv0 = false;
            int materialIndex = renderGlyph.MaterialIndex;
            int vertexIndex = renderGlyph.VertexIndex;
            if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length ||
                unitIndex < 0 || unitIndex >= frame.GlyphStateCount ||
                unitIndex >= frame.GlyphStates.Length)
            {
                return false;
            }

            ref TMP_MeshInfo meshInfo = ref textInfo.meshInfo[materialIndex];
            Vector3[] destinationVertices = meshInfo.vertices;
            Color32[] destinationColors = meshInfo.colors32;
            Vector3[] sourceVertices = ResolveSourceVertices(materialIndex, usePresentationGlyphs);
            if (!HasQuad(sourceVertices, vertexIndex) ||
                !HasQuad(destinationVertices, vertexIndex) ||
                !HasQuad(destinationColors, vertexIndex))
            {
                return false;
            }

            // The instance state spans several hundred bytes, so it is read in place instead of copied through the indexer.
            ref readonly TextStudioGlyphInstanceState glyphState = ref frame.GlyphStates.Items[unitIndex];
            Vector3 translation = placementMap.TransformDelta(
                glyphState.position - glyphState.basePosition);
            EvaluatedGlyphState transformState = EvaluatedGlyphState.Identity;
            transformState.positionOffset = translation;
            transformState.rotation = glyphState.rotation;
            transformState.scale = glyphState.scale;
            transformState.skew = glyphState.skew;
            transformState.pivotOffset = glyphState.pivotOffset;

            // Settled composed glyphs, such as resting physical text that still composes every step, rewrite the quad the
            // mesh already holds. Only a changed quad or a forced source refresh submits the vertex stream again.
            Vector3 previousVertex0 = destinationVertices[vertexIndex];
            Vector3 previousVertex1 = destinationVertices[vertexIndex + 1];
            Vector3 previousVertex2 = destinationVertices[vertexIndex + 2];
            Vector3 previousVertex3 = destinationVertices[vertexIndex + 3];
            bool hasCornerOffsets = glyphState.cornerOffsets.HasAny;
            bool usedLinear2DFastPath = !hasCornerOffsets && TryApplyLinear2DQuad(
                sourceVertices,
                null,
                destinationVertices,
                null,
                vertexIndex,
                false,
                in transformState,
                ResolveRenderChannels(in transformState),
                1f,
                1f,
                true,
                false,
                false);
            TextStudioGlyphQuadTransform transform = default;
            if (!usedLinear2DFastPath)
            {
                Vector3 sourceCenter =
                    (sourceVertices[vertexIndex] + sourceVertices[vertexIndex + 2]) * 0.5f;
                GlyphPlacement placement = default;
                transform = TextStudioGlyphQuadTransformer.CreateFromQuad(
                    sourceVertices[vertexIndex],
                    sourceVertices[vertexIndex + 1],
                    sourceVertices[vertexIndex + 2],
                    sourceVertices[vertexIndex + 3],
                    sourceCenter,
                    in transformState,
                    in placement,
                    false,
                    1f,
                    1f);
            }
            TextStudioGlyphColorQuad vertexColors = glyphState.vertexColors.IsInitialized
                ? glyphState.vertexColors
                : TextStudioGlyphColorQuad.Uniform(glyphState.color);
            byte resolvedAlpha = (byte)(Mathf.Clamp01(glyphState.alpha * alphaMultiplier) * 255f);
            for (int vertex = 0; vertex < 4; vertex++)
            {
                if (!usedLinear2DFastPath)
                {
                    Vector3 source = sourceVertices[vertexIndex + vertex];
                    destinationVertices[vertexIndex + vertex] = hasCornerOffsets
                        ? transform.ApplyPosition(
                            source,
                            glyphState.cornerOffsets.GetCanonicalVertexOffset(vertex),
                            1f)
                        : transform.ApplyPosition(source);
                }
                Color32 resolvedColor = ResolveComposedColor(
                    GetCanonicalColor(in vertexColors, vertex),
                    resolvedAlpha);
                if (!HasSameColor(destinationColors[vertexIndex + vertex], resolvedColor))
                {
                    destinationColors[vertexIndex + vertex] = resolvedColor;
                    wroteColors = true;
                }
            }
            // A source refresh can replace submitted data even when the CPU-side vertices and colors already match.
            wroteVertices = forceSubmission ||
                            !HasSameVertex(previousVertex0, destinationVertices[vertexIndex]) ||
                            !HasSameVertex(previousVertex1, destinationVertices[vertexIndex + 1]) ||
                            !HasSameVertex(previousVertex2, destinationVertices[vertexIndex + 2]) ||
                            !HasSameVertex(previousVertex3, destinationVertices[vertexIndex + 3]);
            wroteColors |= forceSubmission;

            byte currentChannels = VertexChannel | ColorChannel;
            TextStudioRenderPropertyGlyphView glyphProperties =
                frame.GetRenderProperties(unitIndex);
            byte appearanceOperationMask = HasAppearanceProgram
                ? TextStudio2DSelectorEvaluator.EvaluateOperationMask(
                    _appearanceStyle,
                    in frame,
                    unitIndex,
                    0,
                    _selectorPass)
                : (byte)0;
            if (!glyphProperties.IsEmpty ||
                HasAppearanceProgram ||
                HasWritingSource)
                currentChannels |= MaterialChannel;
            byte previousChannels =
                _appliedChannels != null && unitIndex < _appliedChannels.Length
                    ? _appliedChannels[unitIndex]
                    : (byte)0;
            if (((currentChannels | previousChannels) & MaterialChannel) != 0)
            {
                EnsureMaterialPayloadCapacity(
                    materialIndex,
                    destinationVertices.Length);
                MarkPayloadStreams(
                    materialIndex,
                    ApplyMaterialPayload(
                        materialIndex,
                        vertexIndex,
                        renderGlyph.SourceIndex,
                        in glyphProperties,
                        appearanceOperationMask));
            }
            if (_appliedChannels != null && unitIndex < _appliedChannels.Length)
                _appliedChannels[unitIndex] = currentChannels;

            if (usePresentationGlyphs)
            {
                TmpUv0[] destinationUv0 = meshInfo.uvs0;
                TmpUv0[] sourceUv0 = ResolveSourceUv0(materialIndex);
                if (HasQuad(destinationUv0, vertexIndex) &&
                    HasQuad(sourceUv0, vertexIndex))
                {
                    for (int vertex = 0; vertex < 4; vertex++)
                        destinationUv0[vertexIndex + vertex] = sourceUv0[vertexIndex + vertex];
                    wroteUv0 = true;
                }
            }
            if (((currentChannels | previousChannels) & MaterialChannel) != 0 &&
                StampAppearanceIdentity(meshInfo.uvs0, vertexIndex, ResolveAppearanceIdentity(appearanceOperationMask)))
                wroteUv0 = true;

            return true;
        }

        static Color32 GetCanonicalColor(
            in TextStudioGlyphColorQuad colors,
            int vertexIndex)
        {
            return vertexIndex switch
            {
                0 => colors.BottomLeft,
                1 => colors.TopLeft,
                2 => colors.TopRight,
                _ => colors.BottomRight
            };
        }

        static Color32 ResolveComposedColor(Color32 color, byte alpha)
        {
            color.a = (byte)((color.a * alpha) / 255);
            return color;
        }

        /// <summary>Writes one glyph's material payload and returns the streams whose content changed.</summary>
        byte ApplyMaterialPayload(
            int materialIndex,
            int vertexIndex,
            int glyphIndex,
            in TextStudioRenderPropertyGlyphView properties,
            byte appearanceOperationMask)
        {
            if (_materialPayload0 == null ||
                materialIndex < 0 || materialIndex >= _materialPayload0.Length ||
                !HasQuad(_materialPayload0[materialIndex], vertexIndex) ||
                !HasQuad(_materialPayload1[materialIndex], vertexIndex) ||
                !HasQuad(_materialPayload2[materialIndex], vertexIndex))
            {
                return 0;
            }

            Vector4 focus = ReadProperty(in properties, in _focusProperty);
            Vector4 dissolve = ReadProperty(in properties, in _dissolveProperty);
            Vector4 outlineGlow = ReadProperty(in properties, in _outlineGlowProperty);
            Vector4 sweep = ReadProperty(in properties, in _surfaceSweepProperty);
            Vector4 chromatic = ReadProperty(in properties, in _chromaticSplitProperty);
            Vector4 pixelGrid = ReadProperty(in properties, in _pixelGridProperty);
            Vector4 squiggle = ReadProperty(in properties, in _squiggleProperty);
            Vector4 payload0 = new Vector4(
                Mathf.Clamp01(focus.x),
                Mathf.Clamp(dissolve.x, -1f, 1f),
                Mathf.Clamp(dissolve.y, -1f, 1f),
                Mathf.Max(0f, outlineGlow.x));
            float sweepBrightness = new Vector2(sweep.x, sweep.y).magnitude;
            Vector2 sweepDirection = sweepBrightness > 0.0001f
                ? new Vector2(sweep.x, sweep.y) / sweepBrightness
                : Vector2.zero;
            float sweepCenter = sweepBrightness > 0.0001f
                ? sweep.z / sweepBrightness
                : 0f;
            float sweepWidth = sweepBrightness > 0.0001f
                ? Mathf.Max(0f, sweep.w / sweepBrightness)
                : 0f;
            Vector4 payload1 = new Vector4(
                Mathf.Max(0f, outlineGlow.y),
                0f,
                sweepWidth > .0001f ? sweepBrightness : 0f,
                chromatic.x);
            float pixelCellSize = Mathf.Clamp(pixelGrid.x, 0f, 64f);
            float pixelAmount = Mathf.Clamp01(pixelGrid.y);
            float signedPixelCellSize = pixelGrid.z >= 0.5f ? -pixelCellSize : pixelCellSize;
            float signedPixelAmount = pixelGrid.w >= 0.5f ? -pixelAmount : pixelAmount;

            Vector4[] stream0 = _materialPayload0[materialIndex];
            Vector4[] stream1 = _materialPayload1[materialIndex];
            Vector4[] stream2 = _materialPayload2[materialIndex];
            // A substituted glyph sweeps across its own quad, which is the quad the mesh draws.
            Vector3[] sweepSourceVertices = sweepBrightness > 0.0001f
                ? ResolveSourceVertices(materialIndex, true)
                : null;
            byte changed = ApplySquigglePayload(materialIndex, vertexIndex, squiggle) ? SquiggleStream : (byte)0;
            for (int vertex = 0; vertex < 4; vertex++)
            {
                int index = vertexIndex + vertex;
                if (WritePayloadIfChanged(stream0, index, payload0))
                    changed |= Payload0Stream;
                Vector4 vertexPayload1 = payload1;
                if (sweepSourceVertices != null &&
                    index < sweepSourceVertices.Length)
                {
                    Vector3 sourceVertex = sweepSourceVertices[index];
                    // Normalized distance leaves the second sweep scalar free for intensity;
                    // normal.z belongs to Writing's sample index.
                    vertexPayload1.y = (Vector2.Dot(
                        new Vector2(sourceVertex.x, sourceVertex.y),
                        sweepDirection) - sweepCenter) / Mathf.Max(.0001f, sweepWidth);
                }
                if (WritePayloadIfChanged(stream1, index, vertexPayload1))
                    changed |= Payload1Stream;
            }
            float packedPixelCellSize = PackGlyphAndPixelCell(signedPixelCellSize, glyphIndex);
            bool localChanged = WritePayloadIfChanged(stream2, vertexIndex,
                new Vector4(0f, 0f, packedPixelCellSize, signedPixelAmount));
            localChanged |= WritePayloadIfChanged(stream2, vertexIndex + 1,
                new Vector4(0f, 1f, packedPixelCellSize, signedPixelAmount));
            localChanged |= WritePayloadIfChanged(stream2, vertexIndex + 2,
                new Vector4(1f, 1f, packedPixelCellSize, signedPixelAmount));
            localChanged |= WritePayloadIfChanged(stream2, vertexIndex + 3,
                new Vector4(1f, 0f, packedPixelCellSize, signedPixelAmount));
            if (localChanged)
                changed |= LocalPayloadStream;
            return changed;
        }

        // A changed stream publishes its mesh this frame and stays stale until that publication sends it.
        void MarkPayloadStreams(int materialIndex, byte streams)
        {
            if (streams == 0 || _materialMeshDirty == null || materialIndex >= _materialMeshDirty.Length)
                return;
            _materialMeshDirty[materialIndex] = true;
            if (_stalePayloadStreams != null && materialIndex < _stalePayloadStreams.Length)
                _stalePayloadStreams[materialIndex] |= streams;
        }

        void MarkStalePayloadStreams(byte streams)
        {
            if (_stalePayloadStreams == null)
                return;
            for (int index = 0; index < _stalePayloadStreams.Length; index++)
                _stalePayloadStreams[index] |= streams;
        }

        // Canvas tangents and normals are uploaded in the text's canvas payload space; when that space moves, every
        // uploaded copy needs the new transform before it can count as current.
        Matrix4x4 ResolveStreamSpace(TextMeshProUGUI text)
        {
            return AdoptStreamSpace(TextStudioCanvasPayloadSpace.Resolve(text.transform, text.canvas));
        }

        Matrix4x4 AdoptStreamSpace(Matrix4x4 space)
        {
            if (!_hasUploadedStreamSpace || !_uploadedStreamSpace.Equals(space))
            {
                MarkStalePayloadStreams(LocalPayloadStream | CoordinateNormalStream);
                _uploadedStreamSpace = space;
                _hasUploadedStreamSpace = true;
            }
            return space;
        }

        float PackGlyphAndPixelCell(float pixelCellSize, int glyphIndex)
        {
            if (!HasAppearanceProgram)
                return pixelCellSize;

            const float glyphIndexStride = 128f;
            float pixelSign = pixelCellSize < 0f ? -1f : 1f;
            return ((Mathf.Max(0, glyphIndex) + 1) * glyphIndexStride +
                    Mathf.Abs(pixelCellSize)) * pixelSign;
        }

        internal float ResolveAppearanceIdentity(byte appearanceOperationMask) =>
            HasAppearanceProgram ? TextStudio2DStyleIdentity.Pack(_appearanceStyleSlot, appearanceOperationMask) : 0f;

        /// <summary>Writes the exact style identity into TMP's otherwise unused UV0.z for one quad.</summary>
        static bool StampAppearanceIdentity(TmpUv0[] uvs0, int vertexIndex, float identity)
        {
#if UNITY_2023_2_OR_NEWER
            if (!HasQuad(uvs0, vertexIndex)) return false;
            bool changed = false;
            for (int corner = 0; corner < 4; corner++)
            {
                ref TmpUv0 uv = ref uvs0[vertexIndex + corner];
                if (uv.z == identity) continue;
                uv.z = identity;
                changed = true;
            }
            return changed;
#else
            return false;
#endif
        }

        static bool WritePayloadIfChanged(Vector4[] stream, int index, Vector4 value)
        {
            if (stream[index].Equals(value))
                return false;

            stream[index] = value;
            return true;
        }

        bool ApplySquigglePayload(int materialIndex, int vertexIndex, Vector4 sample)
        {
            bool active = sample.x > 0f;
            if (!active && (_squiggleUv1 == null || materialIndex >= _squiggleUv1.Length || _squiggleUv1[materialIndex] == null))
                return false;
            if (_squiggleUv1 == null || _squiggleUv1.Length != _materialPayload0.Length)
                Array.Resize(ref _squiggleUv1, _materialPayload0.Length);
            int count = _materialPayload0[materialIndex].Length;
            if (_squiggleUv1[materialIndex] == null || _squiggleUv1[materialIndex].Length != count)
                _squiggleUv1[materialIndex] = new Vector4[count];
            // TMP owns XY. Integer detail plus fractional strength and a stepped seed use only ZW.
            float packed = active ? Mathf.Clamp(Mathf.Round(sample.y), 1f, 12f) + Mathf.Clamp(sample.x, 0f, .08f) : 0f;
            float seed = active ? sample.z : 0f;
            bool changed = false;
            for (int corner = 0; corner < 4; corner++)
            {
                ref Vector4 uv = ref _squiggleUv1[materialIndex][vertexIndex + corner];
                changed |= uv.z != packed || uv.w != seed;
                uv.z = packed;
                uv.w = seed;
            }
            return changed;
        }

        internal Vector2 GetSquigglePayload(int materialIndex, int vertexIndex)
        {
            if (_squiggleUv1 == null || materialIndex < 0 || materialIndex >= _squiggleUv1.Length || !HasVertex(_squiggleUv1[materialIndex], vertexIndex))
                return Vector2.zero;
            Vector4 uv = _squiggleUv1[materialIndex][vertexIndex];
            return new Vector2(uv.z, uv.w);
        }

        void UploadSquiggleUv1(int materialIndex, in TMP_MeshInfo meshInfo)
        {
            if (_squiggleUv1 == null || materialIndex >= _squiggleUv1.Length) return;
            Vector4[] values = _squiggleUv1[materialIndex];
            if (values == null || meshInfo.uvs2 == null || values.Length != meshInfo.uvs2.Length) return;
            for (int index = 0; index < values.Length; index++)
            {
                values[index].x = meshInfo.uvs2[index].x;
                values[index].y = meshInfo.uvs2[index].y;
            }
            meshInfo.mesh.SetUVs(1, values);
            // RestoreSourceNormals also restores this stream.
            _sourceNormalRestorePending = true;
        }

        internal bool NeedsCanvasPayloadRefresh(TMP_Text text)
        {
            if (!(text is TextMeshProUGUI ui) || !HasMaterialPayloadStorage) return false;
            // Sleeping text checks every tick; unchanged transforms keep the payload space without resolving its inverse.
            if (HaveCanvasPayloadInputs(ui)) { _canvasPayloadInputsConfirmed = true; return false; }
            if (!_canvasPayloadMatrix.Equals(TextStudioCanvasPayloadSpace.Resolve(ui.transform, ui.canvas))) return true;
            RecordCanvasPayloadInputs(ui);
            return false;
        }

        // The inputs a parked text compares in its scheduler slot in place of this check, which returns false while they
        // match. Only inputs the last check found unchanged qualify: a text whose pose keeps changing records new ones every
        // frame and keeps polling in its hub rather than leaving its slot each frame.
        internal bool TryGetConfirmedCanvasPayloadInputs(out Matrix4x4 text, out Matrix4x4 root)
        {
            text = _canvasPayloadTextMatrix;
            root = _canvasPayloadRootMatrix;
            return _canvasPayloadInputsKnown && _canvasPayloadInputsConfirmed;
        }

        // TMP caches its RectTransform, and the texts on a canvas share one read of its root per player frame.
        bool HaveCanvasPayloadInputs(TextMeshProUGUI text)
        {
            Canvas canvas = text.canvas;
            return _canvasPayloadInputsKnown && canvas != null &&
                _canvasPayloadTextMatrix.Equals(text.rectTransform.localToWorldMatrix) &&
                _canvasPayloadRootMatrix.Equals(TextStudioCanvasRootSnapshot.Of(canvas).LocalToWorld);
        }

        void RecordCanvasPayloadInputs(TextMeshProUGUI text)
        {
            Canvas canvas = text.canvas;
            _canvasPayloadInputsKnown = canvas != null;
            _canvasPayloadInputsConfirmed = false;
            if (canvas == null) return;
            _canvasPayloadTextMatrix = text.transform.localToWorldMatrix;
            _canvasPayloadRootMatrix = canvas.rootCanvas.transform.localToWorldMatrix;
        }

        internal void RefreshCanvasPayloadTransform(TMP_Text text)
        {
            if (!(text is TextMeshProUGUI ui) || !HasMaterialPayloadStorage) return;
            Matrix4x4 matrix = TextStudioCanvasPayloadSpace.Resolve(ui.transform, ui.canvas);
            RecordCanvasPayloadInputs(ui);
            if (_canvasPayloadMatrix.Equals(matrix)) return;
            _canvasPayloadMatrix = matrix;
            TMP_MeshInfo[] meshes = text.textInfo?.meshInfo;
            if (meshes == null || _materialPayload2 == null) return;
            matrix = AdoptStreamSpace(matrix);
            for (int index = 0; index < meshes.Length && index < _materialPayload2.Length; index++)
            {
                Mesh mesh = meshes[index].mesh;
                if (mesh == null || _materialPayload2[index] == null) continue;
                UploadCanvasTangents(mesh, _materialPayload2[index], matrix);
                if (_sourceCoordinateNormals != null && index < _sourceCoordinateNormals.Length && _sourceCoordinateNormals[index] != null)
                    UploadCoordinateNormals(mesh, _sourceCoordinateNormals[index], true, matrix);
                if (_stalePayloadStreams != null && index < _stalePayloadStreams.Length)
                    _stalePayloadStreams[index] &= unchecked((byte)~(LocalPayloadStream | CoordinateNormalStream));
                ui.UpdateGeometry(mesh, index);
            }
        }

        void UploadCanvasTangents(Mesh mesh, Vector4[] values, Matrix4x4 matrix)
        {
            if (matrix.Equals(Matrix4x4.identity)) { mesh.SetTangents(values); return; }
            _canvasTangents ??= new List<Vector4>(values.Length);
            _canvasTangents.Clear();
            for (int index = 0; index < values.Length; index++)
                _canvasTangents.Add(TextStudioCanvasPayloadSpace.Tangent(in matrix, values[index]));
            mesh.SetTangents(_canvasTangents);
        }

        void UploadCoordinateNormals(Mesh mesh, Vector3[] values, bool canvasSpace, Matrix4x4 matrix)
        {
            _sourceNormalRestorePending = true;
            if (!canvasSpace || matrix.Equals(Matrix4x4.identity)) { mesh.SetNormals(values); return; }
            _canvasNormals ??= new List<Vector3>(values.Length);
            _canvasNormals.Clear();
            for (int index = 0; index < values.Length; index++) _canvasNormals.Add(matrix.MultiplyVector(values[index]));
            mesh.SetNormals(_canvasNormals);
        }

        /// <summary>
        /// Publishes the payload streams of every mesh whose payload changed this frame. Only stale streams are sent, and when
        /// <paramref name="vertexDataFollows"/> is set the following UpdateVertexData submits those meshes, so each mesh
        /// reaches TMP once per frame: one Canvas mesh copy or one bounds pass instead of two.
        /// </summary>
        void ApplyMaterialStreams(TMP_Text tmpText, TMP_TextInfo textInfo, bool vertexDataFollows)
        {
            if (_materialMeshDirty == null)
                return;

            TextMeshProUGUI canvasText = tmpText as TextMeshProUGUI;
            bool hasStreamSpace = false;
            Matrix4x4 streamSpace = Matrix4x4.identity;
            int meshCount = Mathf.Min(textInfo.meshInfo.Length, _materialMeshDirty.Length);
            for (int materialIndex = 0; materialIndex < meshCount; materialIndex++)
            {
                if (!_materialMeshDirty[materialIndex])
                    continue;
                Mesh mesh = textInfo.meshInfo[materialIndex].mesh;
                if (mesh == null)
                    continue;
                if (canvasText != null && !hasStreamSpace)
                {
                    streamSpace = ResolveStreamSpace(canvasText);
                    hasStreamSpace = true;
                }

                byte staleStreams = _stalePayloadStreams != null && materialIndex < _stalePayloadStreams.Length
                    ? _stalePayloadStreams[materialIndex]
                    : AllPayloadStreams;
                if ((staleStreams & Payload0Stream) != 0)
                    mesh.SetUVs(2, _materialPayload0[materialIndex]);
                if ((staleStreams & Payload1Stream) != 0)
                    mesh.SetUVs(3, _materialPayload1[materialIndex]);
                UploadSquiggleUv1(materialIndex, in textInfo.meshInfo[materialIndex]);
                if ((staleStreams & LocalPayloadStream) != 0)
                {
                    if (canvasText != null)
                        UploadCanvasTangents(mesh, _materialPayload2[materialIndex], streamSpace);
                    else
                        mesh.SetUVs(4, _materialPayload2[materialIndex]);
                }
                if ((staleStreams & CoordinateNormalStream) != 0 &&
                    _sourceCoordinateNormals != null &&
                    materialIndex < _sourceCoordinateNormals.Length &&
                    _sourceCoordinateNormals[materialIndex] != null)
                {
                    UploadCoordinateNormals(mesh, _sourceCoordinateNormals[materialIndex], canvasText != null, streamSpace);
                }
                if (_stalePayloadStreams != null && materialIndex < _stalePayloadStreams.Length)
                    _stalePayloadStreams[materialIndex] = 0;
                // UpdateVertexData submits only meshes below the material count.
                if (!vertexDataFollows || materialIndex >= textInfo.materialCount)
                    tmpText.UpdateGeometry(mesh, materialIndex);
            }
        }

        internal bool InitializeMaterialPayloadStreams(TMP_Text tmpText)
        {
            if (tmpText == null ||
                _materialPayload0 == null ||
                _materialPayload1 == null ||
                _materialPayload2 == null)
            {
                return false;
            }

            TMP_TextInfo textInfo = tmpText.textInfo;
            if (textInfo == null || textInfo.meshInfo == null)
                return false;

            bool initializedAny = false;
            TextMeshProUGUI canvasText = tmpText as TextMeshProUGUI;
            bool hasStreamSpace = false;
            Matrix4x4 streamSpace = Matrix4x4.identity;
            int meshCount = Mathf.Min(
                textInfo.meshInfo.Length,
                _materialPayload0.Length);
            for (int materialIndex = 0; materialIndex < meshCount; materialIndex++)
            {
                ref TMP_MeshInfo meshInfo = ref textInfo.meshInfo[materialIndex];
                Mesh mesh = meshInfo.mesh;
                int vertexCount = meshInfo.vertices != null
                    ? meshInfo.vertices.Length
                    : 0;
                if (mesh == null || vertexCount <= 0)
                    continue;
                if (canvasText != null && !hasStreamSpace)
                {
                    streamSpace = ResolveStreamSpace(canvasText);
                    hasStreamSpace = true;
                }

                EnsureMaterialPayloadCapacity(materialIndex, vertexCount);
                Array.Clear(_materialPayload0[materialIndex], 0, vertexCount);
                Array.Clear(_materialPayload1[materialIndex], 0, vertexCount);
                Array.Clear(_materialPayload2[materialIndex], 0, vertexCount);
                if (_squiggleUv1 != null && materialIndex < _squiggleUv1.Length)
                {
                    if (_squiggleUv1[materialIndex] != null)
                        mesh.SetUVs(1, meshInfo.uvs2);
                    _squiggleUv1[materialIndex] = null;
                }
                if (InitializeAppearancePayloadForMaterial(materialIndex, meshInfo.uvs0))
                    mesh.SetUVs(0, meshInfo.uvs0);
                mesh.SetUVs(2, _materialPayload0[materialIndex]);
                mesh.SetUVs(3, _materialPayload1[materialIndex]);
                if (canvasText != null)
                    UploadCanvasTangents(mesh, _materialPayload2[materialIndex], streamSpace);
                else
                    mesh.SetUVs(4, _materialPayload2[materialIndex]);
                if (_sourceCoordinateNormals != null &&
                    materialIndex < _sourceCoordinateNormals.Length &&
                    _sourceCoordinateNormals[materialIndex] != null)
                {
                    UploadCoordinateNormals(mesh, _sourceCoordinateNormals[materialIndex], canvasText != null, streamSpace);
                }
                // Every payload stream of this mesh now holds its CPU content.
                if (_stalePayloadStreams != null && materialIndex < _stalePayloadStreams.Length)
                    _stalePayloadStreams[materialIndex] = 0;
                tmpText.UpdateGeometry(mesh, materialIndex);
                initializedAny = true;
            }

            if (initializedAny)
                TextStudioTMPMeshUpdateHooks.NotifyMeshUpdated(tmpText);
            return initializedAny;
        }

        bool InitializeAppearancePayloadForMaterial(int materialIndex, TmpUv0[] uvs0)
        {
            if (!HasAppearanceProgram ||
                _materialPayload2 == null ||
                materialIndex < 0 ||
                materialIndex >= _materialPayload2.Length)
            {
                return false;
            }

            int operationCount = Mathf.Min(
                _appearanceStyle.OperationCount,
                TextStudio2DStyleTable.MaximumFusedOperations);
            byte operationMask = operationCount >= 8
                ? byte.MaxValue
                : (byte)((1 << operationCount) - 1);
            Vector4[] stream = _materialPayload2[materialIndex];
            float identity = ResolveAppearanceIdentity(operationMask);
            bool stampedIdentity = false;
            for (int renderIndex = 0; renderIndex < _renderGlyphCount; renderIndex++)
            {
                RenderGlyph glyph = _renderGlyphs[renderIndex];
                if (glyph.MaterialIndex != materialIndex ||
                    !HasQuad(stream, glyph.VertexIndex))
                {
                    continue;
                }

                int vertex = glyph.VertexIndex;
                float packedPixelCellSize = PackGlyphAndPixelCell(0f, glyph.SourceIndex);
                stream[vertex] = new Vector4(0f, 0f, packedPixelCellSize, 0f);
                stream[vertex + 1] = new Vector4(0f, 1f, packedPixelCellSize, 0f);
                stream[vertex + 2] = new Vector4(1f, 1f, packedPixelCellSize, 0f);
                stream[vertex + 3] = new Vector4(1f, 0f, packedPixelCellSize, 0f);
                stampedIdentity |= StampAppearanceIdentity(uvs0, vertex, identity);
            }
            return stampedIdentity;
        }

        // Whether RestoreSourceNormals would write anything: coordinate normals were cached or uploaded since it last restored
        // every mesh.
        internal bool NeedsSourceNormalRestore => _hasSourceCoordinateNormals && _sourceNormalRestorePending;

        internal void RestoreSourceNormals(TMP_Text tmpText)
        {
            // Text without coordinate normals is released every frame and has none to restore, and text whose meshes were
            // restored since the last upload holds the source normals already.
            if (!_hasSourceCoordinateNormals ||
                !_sourceNormalRestorePending ||
                tmpText == null ||
                _hadSourceNormals == null ||
                _cachedSourceNormals == null ||
                _sourceCoordinateNormals == null)
            {
                return;
            }

            TMP_TextInfo textInfo = tmpText.textInfo;
            if (textInfo == null || textInfo.meshInfo == null)
                return;

            bool restoredEveryMesh = true;
            int meshCount = Mathf.Min(textInfo.meshInfo.Length, _hadSourceNormals.Length);
            for (int materialIndex = 0; materialIndex < meshCount; materialIndex++)
            {
                if (_sourceCoordinateNormals[materialIndex] == null)
                    continue;

                Mesh mesh = textInfo.meshInfo[materialIndex].mesh;
                if (mesh == null)
                {
                    restoredEveryMesh = false;
                    continue;
                }

                // TMP can regenerate geometry before serialization restores transient state.
                // Its new mesh already owns fresh normals; the old payload belongs to a different vertex buffer.
                if (_sourceCoordinateNormals[materialIndex].Length != mesh.vertexCount)
                {
                    restoredEveryMesh = false;
                    continue;
                }

                if (_hadSourceNormals[materialIndex] &&
                    _cachedSourceNormals[materialIndex] != null)
                {
                    mesh.SetNormals(_cachedSourceNormals[materialIndex]);
                }
                else
                {
                    mesh.normals = null;
                }
                // The mesh holds TMP's normals again, so a later publication must send the source coordinates.
                if (_stalePayloadStreams != null && materialIndex < _stalePayloadStreams.Length)
                    _stalePayloadStreams[materialIndex] |= CoordinateNormalStream;
                if (_squiggleUv1 != null && materialIndex < _squiggleUv1.Length && _squiggleUv1[materialIndex] != null)
                {
                    mesh.SetUVs(1, textInfo.meshInfo[materialIndex].uvs2);
                    _squiggleUv1[materialIndex] = null;
                }
                tmpText.UpdateGeometry(mesh, materialIndex);
            }

            if (restoredEveryMesh)
                _sourceNormalRestorePending = false;
        }

        /// <summary>
        /// Returns the meshes to TMP's own streams for a material that reads no Text Studio payload, the TextMeshPro route:
        /// restores TMP's normals and second UV channel, then drops the payload and source coordinates, so no later publication
        /// writes them again until <see cref="InitializeMaterialPayloadStreams"/> prepares a production material.
        /// </summary>
        internal void ReleaseMaterialStreams(TMP_Text tmpText)
        {
            RestoreSourceNormals(tmpText);
            TMP_TextInfo textInfo = tmpText != null ? tmpText.textInfo : null;
            if (_squiggleUv1 != null && textInfo?.meshInfo != null)
            {
                int meshCount = Mathf.Min(_squiggleUv1.Length, textInfo.meshInfo.Length);
                for (int materialIndex = 0; materialIndex < meshCount; materialIndex++)
                {
                    if (_squiggleUv1[materialIndex] == null)
                        continue;
                    Mesh mesh = textInfo.meshInfo[materialIndex].mesh;
                    Vector2[] uvs2 = textInfo.meshInfo[materialIndex].uvs2;
                    if (mesh != null && uvs2 != null && uvs2.Length == mesh.vertexCount)
                    {
                        mesh.SetUVs(1, uvs2);
                        tmpText.UpdateGeometry(mesh, materialIndex);
                    }
                    _squiggleUv1[materialIndex] = null;
                }
            }
            ClearMeshStreams(_materialPayload0);
            ClearMeshStreams(_materialPayload1);
            ClearMeshStreams(_materialPayload2);
            ClearMeshStreams(_sourceCoordinateNormals);
            _hasSourceCoordinateNormals = false;
            _sourceNormalRestorePending = false;
            _hasUniformAppliedState = false;
            // No glyph holds a payload any more, so the next frame neither resets one nor allocates the streams again.
            if (_appliedChannels != null)
            {
                for (int index = 0; index < _appliedChannels.Length; index++)
                    _appliedChannels[index] &= unchecked((byte)~MaterialChannel);
            }
        }

        static void ClearMeshStreams<T>(T[][] streams)
        {
            if (streams != null)
                Array.Clear(streams, 0, streams.Length);
        }

        void RefreshMaterialPropertyBindings()
        {
            int registrationVersion = TextStudioRenderPropertyRegistry.RegistrationVersion;
            if (_materialPropertyRegistrationVersion == registrationVersion)
                return;

            _focusProperty = MaterialPropertyBinding.Resolve(TextStudio2DMaterialProperties.Focus);
            _dissolveProperty = MaterialPropertyBinding.Resolve(TextStudio2DMaterialProperties.Dissolve);
            _outlineGlowProperty = MaterialPropertyBinding.Resolve(TextStudio2DMaterialProperties.OutlineGlow);
            _surfaceSweepProperty = MaterialPropertyBinding.Resolve(TextStudio2DMaterialProperties.SurfaceSweep);
            _chromaticSplitProperty = MaterialPropertyBinding.Resolve(TextStudio2DMaterialProperties.ChromaticSplit);
            _pixelGridProperty = MaterialPropertyBinding.Resolve(TextStudio2DMaterialProperties.PixelGrid);
            _squiggleProperty = MaterialPropertyBinding.Resolve(TextStudio2DMaterialProperties.Squiggle);
            _materialPropertyRegistrationVersion = registrationVersion;
        }

        static Vector4 ReadProperty(
            in TextStudioRenderPropertyGlyphView properties,
            in MaterialPropertyBinding binding)
        {
            if (!binding.IsValid)
                return Vector4.zero;
            return properties.TryGet(
                    in binding.Registration,
                    out TextStudioRenderPropertyValue value,
                    out _)
                ? value.Vector
                : binding.Registration.Descriptor.DefaultValue;
        }

        static void EnsurePayloadCapacity(ref Vector4[] payload, int vertexCount)
        {
            if (vertexCount <= 0)
            {
                payload = null;
                return;
            }
            if (payload == null || payload.Length != vertexCount)
                payload = new Vector4[vertexCount];
            else
                Array.Clear(payload, 0, payload.Length);
        }

        void EnsureMaterialPayloadCapacity(int materialIndex, int vertexCount)
        {
            if (_materialPayload0 == null ||
                materialIndex < 0 ||
                materialIndex >= _materialPayload0.Length)
            {
                return;
            }

            EnsurePayloadStorage(ref _materialPayload0[materialIndex], vertexCount);
            EnsurePayloadStorage(ref _materialPayload1[materialIndex], vertexCount);
            EnsurePayloadStorage(ref _materialPayload2[materialIndex], vertexCount);
        }

        static void ClearPayloadIfAllocated(ref Vector4[] payload, int vertexCount)
        {
            if (payload != null)
                EnsurePayloadCapacity(ref payload, vertexCount);
        }

        static void EnsurePayloadStorage(ref Vector4[] payload, int vertexCount)
        {
            if (vertexCount <= 0)
            {
                payload = null;
                return;
            }
            if (payload == null || payload.Length != vertexCount)
                payload = new Vector4[vertexCount];
        }

        static bool HasPayloadStorage(Vector4[][] payloads)
        {
            if (payloads == null)
                return false;

            for (int i = 0; i < payloads.Length; i++)
            {
                if (payloads[i] != null && payloads[i].Length > 0)
                    return true;
            }

            return false;
        }

        static bool HasUvCache(TmpUv0[][] uvCache)
        {
            if (uvCache == null)
                return false;

            for (int i = 0; i < uvCache.Length; i++)
            {
                if (uvCache[i] != null && uvCache[i].Length > 0)
                    return true;
            }

            return false;
        }

        void EnsurePresentationUvCache(TMP_TextInfo textInfo)
        {
            if (textInfo == null || textInfo.meshInfo == null)
                return;
            if (HasUvCache(_cachedUv0))
                return;

            int meshCount = textInfo.meshInfo.Length;
            if (_cachedUv0 == null || _cachedUv0.Length < meshCount)
                _cachedUv0 = new TmpUv0[meshCount][];

            for (int i = 0; i < meshCount; i++)
            {
                TmpUv0[] source = textInfo.meshInfo[i].uvs0;
                if (source == null)
                    continue;
                if (_cachedUv0[i] == null || _cachedUv0[i].Length < source.Length)
                    _cachedUv0[i] = new TmpUv0[source.Length];
                Array.Copy(source, _cachedUv0[i], source.Length);
            }
        }

        static bool TryApplyLinear2DQuad(
            Vector3[] sourceVertices,
            Color32[] sourceColors,
            Vector3[] destinationVertices,
            Color32[] destinationColors,
            int vertexIndex,
            bool hasUniformSourceColor,
            in EvaluatedGlyphState state,
            TextStudioRenderChannels stateChannels,
            float effectUnitScale,
            float alphaMultiplier,
            bool applyVertices,
            bool applyColors,
            bool applyColorEffects)
        {
            Quaternion rotation = state.rotation;
            bool includesRotation = (stateChannels & TextStudioRenderChannels.Rotation) != 0;
            if (applyVertices && includesRotation &&
                (Mathf.Abs(rotation.x) > 0.000001f || Mathf.Abs(rotation.y) > 0.000001f))
                return false;

            Vector3 translation = state.positionOffset * effectUnitScale;
            TextStudioRenderChannels vertexChannels = stateChannels & TextStudioRenderChannels.Vertex;
            bool appliedSpecializedVertices = false;
            if (applyVertices && vertexChannels == TextStudioRenderChannels.Position)
            {
                destinationVertices[vertexIndex] = sourceVertices[vertexIndex] + translation;
                destinationVertices[vertexIndex + 1] = sourceVertices[vertexIndex + 1] + translation;
                destinationVertices[vertexIndex + 2] = sourceVertices[vertexIndex + 2] + translation;
                destinationVertices[vertexIndex + 3] = sourceVertices[vertexIndex + 3] + translation;
                appliedSpecializedVertices = true;
            }
            else if (applyVertices)
            {
                appliedSpecializedVertices = TryApplySpecializedVertexQuad(
                    sourceVertices,
                    destinationVertices,
                    vertexIndex,
                    in state,
                    vertexChannels,
                    in translation);
            }

            if (appliedSpecializedVertices)
            {
                if (!applyColors)
                    return true;
                applyVertices = false;
            }

            TextStudioRenderChannels colorChannels =
                stateChannels & TextStudioRenderChannels.VertexColor;
            if (!applyVertices &&
                applyColors &&
                (colorChannels == TextStudioRenderChannels.Alpha ||
                 colorChannels == TextStudioRenderChannels.Color))
            {
                ApplySpecializedColorQuad(
                    sourceColors,
                    destinationColors,
                    vertexIndex,
                    hasUniformSourceColor,
                    in state,
                    colorChannels,
                    alphaMultiplier);
                return true;
            }

            Vector3 scale = state.scale;
            Vector2 skew = state.skew;
            bool hasScale = (stateChannels & TextStudioRenderChannels.Scale) != 0;
            bool hasSkew = (stateChannels & TextStudioRenderChannels.Skew) != 0;
            bool hasCornerOffsets = (stateChannels & TextStudioRenderChannels.CornerOffsets) != 0;
            bool hasRotation = includesRotation;
            bool transformsAroundPivot = applyVertices && (hasScale || hasSkew || hasRotation);
            Vector3 pivotCenter = Vector3.zero;
            if (transformsAroundPivot)
            {
                Vector3 sourceCenter = (sourceVertices[vertexIndex] + sourceVertices[vertexIndex + 2]) * 0.5f;
                pivotCenter = sourceCenter;
                if (state.pivotOffset.sqrMagnitude > 0.000001f)
                {
                    Vector3 vertex0 = sourceVertices[vertexIndex];
                    Vector3 vertex1 = sourceVertices[vertexIndex + 1];
                    Vector3 vertex2 = sourceVertices[vertexIndex + 2];
                    Vector3 vertex3 = sourceVertices[vertexIndex + 3];
                    float minX = Mathf.Min(Mathf.Min(vertex0.x, vertex1.x), Mathf.Min(vertex2.x, vertex3.x));
                    float maxX = Mathf.Max(Mathf.Max(vertex0.x, vertex1.x), Mathf.Max(vertex2.x, vertex3.x));
                    float minY = Mathf.Min(Mathf.Min(vertex0.y, vertex1.y), Mathf.Min(vertex2.y, vertex3.y));
                    float maxY = Mathf.Max(Mathf.Max(vertex0.y, vertex1.y), Mathf.Max(vertex2.y, vertex3.y));
                    Vector2 pivot = state.Pivot;
                    pivotCenter = new Vector3(
                        Mathf.LerpUnclamped(minX, maxX, pivot.x),
                        Mathf.LerpUnclamped(minY, maxY, pivot.y),
                        sourceCenter.z);
                }
            }

            float doubledZ = 2f * rotation.z;
            float diagonal = 1f - doubledZ * rotation.z;
            float cross = doubledZ * rotation.w;

            bool hasUniformColor = applyColors && hasUniformSourceColor;
            byte alpha = (byte)(Mathf.Clamp01(state.alpha * alphaMultiplier) * 255f);
            bool applyRgbEffects = (stateChannels & TextStudioRenderChannels.Color) != 0;
            Color32 uniformColor = applyColors && hasUniformColor
                ? (applyColorEffects
                    ? ResolveColor(in state, sourceColors[vertexIndex], alpha, applyRgbEffects)
                    : sourceColors[vertexIndex])
                : default;

            for (int v = 0; v < 4; v++)
            {
                if (applyVertices)
                {
                    Vector3 source = sourceVertices[vertexIndex + v];
                    if (hasCornerOffsets)
                    {
                        Vector2 corner = state.cornerOffsets.GetCanonicalVertexOffset(v);
                        source.x += corner.x * effectUnitScale;
                        source.y += corner.y * effectUnitScale;
                    }

                    Vector3 destination;
                    if (!transformsAroundPivot)
                    {
                        destination = source + translation;
                    }
                    else
                    {
                        Vector3 offset = source - pivotCenter;
                        if (hasScale)
                        {
                            offset.x *= scale.x;
                            offset.y *= scale.y;
                            offset.z *= scale.z;
                        }
                        if (hasSkew)
                        {
                            float x = offset.x;
                            float y = offset.y;
                            offset.x = x + y * skew.x;
                            offset.y = y + x * skew.y;
                        }

                        if (hasRotation)
                        {
                            float x = offset.x;
                            float y = offset.y;
                            offset.x = diagonal * x - cross * y;
                            offset.y = cross * x + diagonal * y;
                        }

                        destination = pivotCenter + offset + translation;
                    }

                    destinationVertices[vertexIndex + v] = destination;
                }
                if (applyColors)
                {
                    destinationColors[vertexIndex + v] = hasUniformColor
                        ? uniformColor
                        : (applyColorEffects
                            ? ResolveColor(in state, sourceColors[vertexIndex + v], alpha, applyRgbEffects)
                            : sourceColors[vertexIndex + v]);
                }
            }

            return true;
        }

        // The vertex half of TryApplyLinear2DQuad: a translation alone moves the quad, other affine channels rebuild it.
        static bool TryApplyAffineVertexQuad(
            Vector3[] sourceVertices,
            Vector3[] destinationVertices,
            int vertexIndex,
            in EvaluatedGlyphState state,
            TextStudioRenderChannels vertexChannels,
            in Vector3 translation)
        {
            if (vertexChannels != TextStudioRenderChannels.Position)
            {
                return TryApplySpecializedVertexQuad(
                    sourceVertices,
                    destinationVertices,
                    vertexIndex,
                    in state,
                    vertexChannels,
                    in translation);
            }

            destinationVertices[vertexIndex] = sourceVertices[vertexIndex] + translation;
            destinationVertices[vertexIndex + 1] = sourceVertices[vertexIndex + 1] + translation;
            destinationVertices[vertexIndex + 2] = sourceVertices[vertexIndex + 2] + translation;
            destinationVertices[vertexIndex + 3] = sourceVertices[vertexIndex + 3] + translation;
            return true;
        }

        static bool TryApplySpecializedVertexQuad(
            Vector3[] sourceVertices,
            Vector3[] destinationVertices,
            int vertexIndex,
            in EvaluatedGlyphState state,
            TextStudioRenderChannels vertexChannels,
            in Vector3 translation)
        {
            // The branches below rebuild a quad from two corners, which is exact only for axis-aligned quads. Sheared
            // quads, such as TMP italics, take the per-vertex path so rotation and scale keep their slant.
            Vector3 cornerBottomLeft = sourceVertices[vertexIndex];
            Vector3 cornerTopLeft = sourceVertices[vertexIndex + 1];
            Vector3 cornerTopRight = sourceVertices[vertexIndex + 2];
            Vector3 cornerBottomRight = sourceVertices[vertexIndex + 3];
            if (cornerBottomLeft.x != cornerTopLeft.x || cornerTopRight.x != cornerBottomRight.x ||
                cornerBottomLeft.y != cornerBottomRight.y || cornerTopLeft.y != cornerTopRight.y)
                return false;

            const TextStudioRenderChannels rotationAndPivot =
                TextStudioRenderChannels.Rotation | TextStudioRenderChannels.Pivot;
            if (vertexChannels == TextStudioRenderChannels.Rotation ||
                vertexChannels == (TextStudioRenderChannels.Position | TextStudioRenderChannels.Rotation))
            {
                Vector3 bottomLeft = sourceVertices[vertexIndex];
                Vector3 topRight = sourceVertices[vertexIndex + 2];
                float centerX = (bottomLeft.x + topRight.x) * 0.5f + translation.x;
                float centerY = (bottomLeft.y + topRight.y) * 0.5f + translation.y;
                float halfWidth = (topRight.x - bottomLeft.x) * 0.5f;
                float halfHeight = (topRight.y - bottomLeft.y) * 0.5f;
                Quaternion rotation = state.rotation;
                float doubledZ = 2f * rotation.z;
                float diagonal = 1f - doubledZ * rotation.z;
                float cross = doubledZ * rotation.w;
                float rotatedHalfWidthX = diagonal * halfWidth;
                float rotatedHalfWidthY = cross * halfWidth;
                float rotatedHalfHeightX = cross * halfHeight;
                float rotatedHalfHeightY = diagonal * halfHeight;
                destinationVertices[vertexIndex] = new Vector3(
                    centerX - rotatedHalfWidthX + rotatedHalfHeightX,
                    centerY - rotatedHalfWidthY - rotatedHalfHeightY,
                    bottomLeft.z + translation.z);
                destinationVertices[vertexIndex + 1] = new Vector3(
                    centerX - rotatedHalfWidthX - rotatedHalfHeightX,
                    centerY - rotatedHalfWidthY + rotatedHalfHeightY,
                    sourceVertices[vertexIndex + 1].z + translation.z);
                destinationVertices[vertexIndex + 2] = new Vector3(
                    centerX + rotatedHalfWidthX - rotatedHalfHeightX,
                    centerY + rotatedHalfWidthY + rotatedHalfHeightY,
                    topRight.z + translation.z);
                destinationVertices[vertexIndex + 3] = new Vector3(
                    centerX + rotatedHalfWidthX + rotatedHalfHeightX,
                    centerY + rotatedHalfWidthY - rotatedHalfHeightY,
                    sourceVertices[vertexIndex + 3].z + translation.z);
                return true;
            }

            if (vertexChannels == rotationAndPivot ||
                vertexChannels == (TextStudioRenderChannels.Position | rotationAndPivot))
            {
                Vector3 bottomLeft = sourceVertices[vertexIndex];
                Vector3 topRight = sourceVertices[vertexIndex + 2];
                float sourceCenterX = (bottomLeft.x + topRight.x) * 0.5f;
                float sourceCenterY = (bottomLeft.y + topRight.y) * 0.5f;
                float halfWidth = (topRight.x - bottomLeft.x) * 0.5f;
                float halfHeight = (topRight.y - bottomLeft.y) * 0.5f;
                Quaternion rotation = state.rotation;
                float doubledZ = 2f * rotation.z;
                float diagonal = 1f - doubledZ * rotation.z;
                float cross = doubledZ * rotation.w;
                Vector2 pivotOffset = state.pivotOffset;
                float centerX;
                float centerY;
                if (Mathf.Abs(pivotOffset.x) <= 0.000001f &&
                    Mathf.Abs(pivotOffset.y - 0.5f) <= 0.000001f)
                {
                    centerX = sourceCenterX + cross * halfHeight + translation.x;
                    centerY = topRight.y - diagonal * halfHeight + translation.y;
                }
                else
                {
                    float pivotX = sourceCenterX + (topRight.x - bottomLeft.x) * pivotOffset.x;
                    float pivotY = sourceCenterY + (topRight.y - bottomLeft.y) * pivotOffset.y;
                    float centerOffsetX = sourceCenterX - pivotX;
                    float centerOffsetY = sourceCenterY - pivotY;
                    centerX = pivotX + diagonal * centerOffsetX - cross * centerOffsetY + translation.x;
                    centerY = pivotY + cross * centerOffsetX + diagonal * centerOffsetY + translation.y;
                }
                float rotatedHalfWidthX = diagonal * halfWidth;
                float rotatedHalfWidthY = cross * halfWidth;
                float rotatedHalfHeightX = cross * halfHeight;
                float rotatedHalfHeightY = diagonal * halfHeight;
                destinationVertices[vertexIndex] = new Vector3(
                    centerX - rotatedHalfWidthX + rotatedHalfHeightX,
                    centerY - rotatedHalfWidthY - rotatedHalfHeightY,
                    bottomLeft.z + translation.z);
                destinationVertices[vertexIndex + 1] = new Vector3(
                    centerX - rotatedHalfWidthX - rotatedHalfHeightX,
                    centerY - rotatedHalfWidthY + rotatedHalfHeightY,
                    sourceVertices[vertexIndex + 1].z + translation.z);
                destinationVertices[vertexIndex + 2] = new Vector3(
                    centerX + rotatedHalfWidthX - rotatedHalfHeightX,
                    centerY + rotatedHalfWidthY + rotatedHalfHeightY,
                    topRight.z + translation.z);
                destinationVertices[vertexIndex + 3] = new Vector3(
                    centerX + rotatedHalfWidthX + rotatedHalfHeightX,
                    centerY + rotatedHalfWidthY - rotatedHalfHeightY,
                    sourceVertices[vertexIndex + 3].z + translation.z);
                return true;
            }

            if (vertexChannels == TextStudioRenderChannels.Scale ||
                vertexChannels == (TextStudioRenderChannels.Position | TextStudioRenderChannels.Scale))
            {
                Vector3 bottomLeft = sourceVertices[vertexIndex];
                Vector3 topRight = sourceVertices[vertexIndex + 2];
                Vector3 scale = state.scale;
                float centerX = (bottomLeft.x + topRight.x) * 0.5f + translation.x;
                float centerY = (bottomLeft.y + topRight.y) * 0.5f + translation.y;
                float halfWidth = (topRight.x - bottomLeft.x) * 0.5f * scale.x;
                float halfHeight = (topRight.y - bottomLeft.y) * 0.5f * scale.y;
                float centerZ = (bottomLeft.z + topRight.z) * 0.5f + translation.z;
                float bottomZ = centerZ + (bottomLeft.z - (bottomLeft.z + topRight.z) * 0.5f) * scale.z;
                float topZ = centerZ + (topRight.z - (bottomLeft.z + topRight.z) * 0.5f) * scale.z;
                destinationVertices[vertexIndex] = new Vector3(
                    centerX - halfWidth, centerY - halfHeight, bottomZ);
                destinationVertices[vertexIndex + 1] = new Vector3(
                    centerX - halfWidth, centerY + halfHeight,
                    centerZ + (sourceVertices[vertexIndex + 1].z - (bottomLeft.z + topRight.z) * 0.5f) * scale.z);
                destinationVertices[vertexIndex + 2] = new Vector3(
                    centerX + halfWidth, centerY + halfHeight, topZ);
                destinationVertices[vertexIndex + 3] = new Vector3(
                    centerX + halfWidth, centerY - halfHeight,
                    centerZ + (sourceVertices[vertexIndex + 3].z - (bottomLeft.z + topRight.z) * 0.5f) * scale.z);
                return true;
            }

            const TextStudioRenderChannels affineChannels =
                TextStudioRenderChannels.Position | TextStudioRenderChannels.Rotation |
                TextStudioRenderChannels.Pivot | TextStudioRenderChannels.Scale;
            // Without rotation or scale the per-vertex path copies source vertices exactly, which restores untransformed glyphs.
            if ((vertexChannels & ~affineChannels) == 0 &&
                (vertexChannels & (TextStudioRenderChannels.Rotation | TextStudioRenderChannels.Scale)) != 0)
            {
                // Remaining combinations scale and then rotate about the pivot, as the per-vertex path does, so the
                // transformed center and half extents give all four corners.
                Vector3 bottomLeft = sourceVertices[vertexIndex];
                Vector3 topRight = sourceVertices[vertexIndex + 2];
                float sourceCenterX = (bottomLeft.x + topRight.x) * 0.5f;
                float sourceCenterY = (bottomLeft.y + topRight.y) * 0.5f;
                float pivotZ = (bottomLeft.z + topRight.z) * 0.5f;
                float pivotX = sourceCenterX;
                float pivotY = sourceCenterY;
                if (state.pivotOffset.sqrMagnitude > 0.000001f)
                {
                    Vector2 pivot = state.Pivot;
                    pivotX = Mathf.LerpUnclamped(bottomLeft.x, topRight.x, pivot.x);
                    pivotY = Mathf.LerpUnclamped(bottomLeft.y, topRight.y, pivot.y);
                }

                Vector3 scale = (vertexChannels & TextStudioRenderChannels.Scale) != 0
                    ? state.scale
                    : Vector3.one;
                float diagonal = 1f;
                float cross = 0f;
                if ((vertexChannels & TextStudioRenderChannels.Rotation) != 0)
                {
                    Quaternion rotation = state.rotation;
                    float doubledZ = 2f * rotation.z;
                    diagonal = 1f - doubledZ * rotation.z;
                    cross = doubledZ * rotation.w;
                }

                float centerOffsetX = (sourceCenterX - pivotX) * scale.x;
                float centerOffsetY = (sourceCenterY - pivotY) * scale.y;
                float centerX = pivotX + diagonal * centerOffsetX - cross * centerOffsetY + translation.x;
                float centerY = pivotY + cross * centerOffsetX + diagonal * centerOffsetY + translation.y;
                float halfWidth = (topRight.x - bottomLeft.x) * 0.5f * scale.x;
                float halfHeight = (topRight.y - bottomLeft.y) * 0.5f * scale.y;
                float rotatedHalfWidthX = diagonal * halfWidth;
                float rotatedHalfWidthY = cross * halfWidth;
                float rotatedHalfHeightX = cross * halfHeight;
                float rotatedHalfHeightY = diagonal * halfHeight;
                destinationVertices[vertexIndex] = new Vector3(
                    centerX - rotatedHalfWidthX + rotatedHalfHeightX,
                    centerY - rotatedHalfWidthY - rotatedHalfHeightY,
                    pivotZ + (bottomLeft.z - pivotZ) * scale.z + translation.z);
                destinationVertices[vertexIndex + 1] = new Vector3(
                    centerX - rotatedHalfWidthX - rotatedHalfHeightX,
                    centerY - rotatedHalfWidthY + rotatedHalfHeightY,
                    pivotZ + (sourceVertices[vertexIndex + 1].z - pivotZ) * scale.z + translation.z);
                destinationVertices[vertexIndex + 2] = new Vector3(
                    centerX + rotatedHalfWidthX - rotatedHalfHeightX,
                    centerY + rotatedHalfWidthY + rotatedHalfHeightY,
                    pivotZ + (topRight.z - pivotZ) * scale.z + translation.z);
                destinationVertices[vertexIndex + 3] = new Vector3(
                    centerX + rotatedHalfWidthX + rotatedHalfHeightX,
                    centerY + rotatedHalfWidthY - rotatedHalfHeightY,
                    pivotZ + (sourceVertices[vertexIndex + 3].z - pivotZ) * scale.z + translation.z);
                return true;
            }

            return false;
        }

        static void ApplySpecializedColorQuad(
            Color32[] sourceColors,
            Color32[] destinationColors,
            int vertexIndex,
            bool hasUniformSourceColor,
            in EvaluatedGlyphState state,
            TextStudioRenderChannels colorChannels,
            float alphaMultiplier)
        {
            if (hasUniformSourceColor)
            {
                Color32 color = ResolveUniformQuadColor(
                    in state,
                    sourceColors[vertexIndex],
                    colorChannels,
                    alphaMultiplier);
                destinationColors[vertexIndex] = color;
                destinationColors[vertexIndex + 1] = color;
                destinationColors[vertexIndex + 2] = color;
                destinationColors[vertexIndex + 3] = color;
                return;
            }

            byte alpha = (byte)(Mathf.Clamp01(state.alpha * alphaMultiplier) * 255f);
            bool applyRgbEffects = colorChannels == TextStudioRenderChannels.Color;
            destinationColors[vertexIndex] = ResolveColor(
                in state,
                sourceColors[vertexIndex],
                alpha,
                applyRgbEffects);
            destinationColors[vertexIndex + 1] = ResolveColor(
                in state,
                sourceColors[vertexIndex + 1],
                alpha,
                applyRgbEffects);
            destinationColors[vertexIndex + 2] = ResolveColor(
                in state,
                sourceColors[vertexIndex + 2],
                alpha,
                applyRgbEffects);
            destinationColors[vertexIndex + 3] = ResolveColor(
                in state,
                sourceColors[vertexIndex + 3],
                alpha,
                applyRgbEffects);
        }

        // The color all four vertices of a quad with one source color receive from ApplySpecializedColorQuad.
        static Color32 ResolveUniformQuadColor(
            in EvaluatedGlyphState state,
            Color32 sourceColor,
            TextStudioRenderChannels colorChannels,
            float alphaMultiplier)
        {
            byte alpha = (byte)(Mathf.Clamp01(state.alpha * alphaMultiplier) * 255f);
            return ResolveColor(
                in state,
                sourceColor,
                alpha,
                colorChannels == TextStudioRenderChannels.Color);
        }

        static bool HasSameColor(Color32 left, Color32 right)
        {
            return left.r == right.r &&
                   left.g == right.g &&
                   left.b == right.b &&
                   left.a == right.a;
        }

        // Exact component equality; Vector3 == tolerates small differences.
        static bool HasSameVertex(Vector3 left, Vector3 right)
        {
            return left.x == right.x &&
                   left.y == right.y &&
                   left.z == right.z;
        }

        static byte ResolveAnimatedChannels(
            TextStudioRenderChannels stateChannels,
            bool usePlacement,
            float alphaMultiplier,
            bool hasMaterialChannels)
        {
            byte channels = 0;
            if (usePlacement ||
                (stateChannels & TextStudioRenderChannels.Vertex) != 0)
            {
                channels |= VertexChannel;
            }

            if (alphaMultiplier != 1f ||
                (stateChannels & TextStudioRenderChannels.VertexColor) != 0)
            {
                channels |= ColorChannel;
            }

            if (hasMaterialChannels)
                channels |= MaterialChannel;

            return channels;
        }

        static TextStudioRenderChannels ResolveRenderChannels(in EvaluatedGlyphState state)
        {
            if (state.renderChannels != TextStudioRenderChannels.None)
                return state.renderChannels;

            TextStudioRenderChannels channels = TextStudioRenderChannels.None;
            Quaternion rotation = state.rotation;
            Vector3 scale = state.scale;
            if (state.positionOffset != Vector3.zero)
                channels |= TextStudioRenderChannels.Position;
            if (Mathf.Abs(rotation.x) > 0.000001f ||
                Mathf.Abs(rotation.y) > 0.000001f ||
                Mathf.Abs(rotation.z) > 0.000001f ||
                Mathf.Abs(rotation.w - 1f) > 0.000001f)
            {
                channels |= TextStudioRenderChannels.Rotation;
            }
            if (scale.x != 1f || scale.y != 1f || scale.z != 1f)
                channels |= TextStudioRenderChannels.Scale;
            if (!Mathf.Approximately(state.alpha, 1f))
                channels |= TextStudioRenderChannels.Alpha;
            if (state.colorCompositionVersion != 0u)
            {
                channels |= TextStudioRenderChannels.Color;
            }
            if (state.skew.sqrMagnitude > 0.000001f)
                channels |= TextStudioRenderChannels.Skew;
            if (state.pivotOffset.sqrMagnitude > 0.000001f)
                channels |= TextStudioRenderChannels.Pivot;
            if (state.cornerOffsets.HasAny)
                channels |= TextStudioRenderChannels.CornerOffsets;
            if (state.uvOffset.sqrMagnitude > 0.000001f)
                channels |= TextStudioRenderChannels.UvOffset;
            return channels;
        }

        static Color32 ResolveColor(
            in EvaluatedGlyphState state,
            Color32 source,
            byte alpha,
            bool applyRgbEffects)
        {
            Color32 resolved = applyRgbEffects ? state.ResolveColor(source) : source;
            resolved.a = (byte)((source.a * alpha) / 255);
            return resolved;
        }

        static bool HasUniformQuadColor(Color32[] colors, int vertexIndex)
        {
            return colors[vertexIndex].Equals(colors[vertexIndex + 1]) &&
                   colors[vertexIndex].Equals(colors[vertexIndex + 2]) &&
                   colors[vertexIndex].Equals(colors[vertexIndex + 3]);
        }

        PlacementCoordinateMap ResolvePlacementCoordinateMap(
            TMP_TextInfo textInfo,
            in TextStudioFrame frame,
            bool usePlacement)
        {
            TextStudioReadOnlyBuffer<Vector3> basePositions = frame.BasePositions;
            if (!usePlacement || textInfo == null || !basePositions.IsCreated || !frame.Plan.IsCreated)
                return PlacementCoordinateMap.Identity;

            bool hasAnchor = false;
            Vector3 sourceAnchor = Vector3.zero;
            Vector3 targetAnchor = Vector3.zero;
            float scale = 1f;

            int characterCount = textInfo.characterInfo != null
                ? Mathf.Min(textInfo.characterCount, textInfo.characterInfo.Length)
                : 0;
            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (!character.isVisible || !TryGetCachedCharacterCenter(in character, out Vector3 targetCenter))
                    continue;

                int unitIndex = frame.Plan.CompiledPlan.GetUnitIndexForDisplayChar(character.index);
                if (unitIndex < 0 || unitIndex >= frame.PlacementCount || unitIndex >= basePositions.Length)
                    continue;

                Vector3 sourceCenter = basePositions[unitIndex];
                if (!hasAnchor)
                {
                    sourceAnchor = sourceCenter;
                    targetAnchor = targetCenter;
                    hasAnchor = true;
                    continue;
                }

                Vector3 sourceDelta = sourceCenter - sourceAnchor;
                float sourceDistanceSquared = sourceDelta.sqrMagnitude;
                if (sourceDistanceSquared <= 0.000001f)
                    continue;

                float targetDistance = (targetCenter - targetAnchor).magnitude;
                float candidateScale = targetDistance / Mathf.Sqrt(sourceDistanceSquared);
                if (float.IsNaN(candidateScale) || float.IsInfinity(candidateScale) || candidateScale <= 0.0001f)
                    continue;

                scale = candidateScale;
                break;
            }

            if (!hasAnchor)
                return PlacementCoordinateMap.Identity;

            Vector3 offset = targetAnchor - sourceAnchor * scale;
            return new PlacementCoordinateMap(scale, offset);
        }

        bool TryGetCachedCharacterCenter(in TMP_CharacterInfo character, out Vector3 center)
        {
            center = Vector3.zero;
            int materialIndex = character.materialReferenceIndex;
            int vertexIndex = character.vertexIndex;
            if (_cachedVertices == null ||
                materialIndex < 0 ||
                materialIndex >= _cachedVertices.Length ||
                !HasQuad(_cachedVertices[materialIndex], vertexIndex))
            {
                return false;
            }

            Vector3[] vertices = _cachedVertices[materialIndex];
            center = (vertices[vertexIndex] + vertices[vertexIndex + 2]) * 0.5f;
            return true;
        }

        readonly struct PlacementCoordinateMap
        {
            public static readonly PlacementCoordinateMap Identity = new PlacementCoordinateMap(1f, Vector3.zero);

            readonly float _scale;
            readonly Vector3 _offset;

            public PlacementCoordinateMap(float scale, Vector3 offset)
            {
                _scale = scale;
                _offset = offset;
            }

            public Vector3 Transform(Vector3 position)
            {
                return position * _scale + _offset;
            }

            public Vector3 TransformDelta(Vector3 delta)
            {
                return delta * _scale;
            }
        }

        static bool HasQuad(Vector3[] values, int vertexIndex)
        {
            return values != null && vertexIndex >= 0 && vertexIndex + 3 < values.Length;
        }

        static bool HasQuad(Color32[] values, int vertexIndex)
        {
            return values != null && vertexIndex >= 0 && vertexIndex + 3 < values.Length;
        }

        static bool HasQuad(TmpUv0[] values, int vertexIndex)
        {
            return values != null && vertexIndex >= 0 && vertexIndex + 3 < values.Length;
        }

#if !UNITY_2023_2_OR_NEWER
        static bool HasQuad(Vector4[] values, int vertexIndex)
        {
            return values != null && vertexIndex >= 0 && vertexIndex + 3 < values.Length;
        }
#endif

        bool IsPresentationGeometryValid(int materialIndex)
        {
            return _presentationGeometryValid != null &&
                   materialIndex >= 0 &&
                   materialIndex < _presentationGeometryValid.Length &&
                   _presentationGeometryValid[materialIndex];
        }

        // Transform input for a glyph quad: the presentation copy while substitution is in use, otherwise the TMP source.
        Vector3[] ResolveSourceVertices(int materialIndex, bool usePresentationGeometry)
        {
            if (usePresentationGeometry && IsPresentationGeometryValid(materialIndex))
                return _presentationVertices[materialIndex];
            return _cachedVertices != null && materialIndex >= 0 && materialIndex < _cachedVertices.Length
                ? _cachedVertices[materialIndex]
                : null;
        }

        TmpUv0[] ResolveSourceUv0(int materialIndex)
        {
            if (IsPresentationGeometryValid(materialIndex))
                return _presentationUv0[materialIndex];
            return _cachedUv0 != null && materialIndex >= 0 && materialIndex < _cachedUv0.Length
                ? _cachedUv0[materialIndex]
                : null;
        }

        /// <summary>
        /// Keeps the presentation quad of one render glyph in step with its presentation glyph and reports whether it
        /// changed, so the caller rewrites its vertices even when the animated state has no vertex channel.
        /// </summary>
        bool UpdatePresentationGeometry(
            TMP_TextInfo textInfo,
            TextStudioCompiledPlan plan,
            int renderIndex,
            in RenderGlyph renderGlyph,
            PresentationGlyph[] presentationGlyphs,
            int presentationGlyphCount,
            bool rewriteApplied)
        {
            int characterIndex = renderGlyph.CharacterInfoIndex;
            if (textInfo.characterInfo == null ||
                characterIndex < 0 ||
                characterIndex >= textInfo.characterInfo.Length)
            {
                return false;
            }

            ref TMP_CharacterInfo character = ref textInfo.characterInfo[characterIndex];
            int unitIndex = renderGlyph.UnitIndex;
            // Combining marks and other trailing characters of a unit keep their authored glyphs.
            int glyphId = presentationGlyphs != null &&
                          renderGlyph.IsUnitPrimary &&
                          unitIndex < presentationGlyphCount &&
                          unitIndex < presentationGlyphs.Length
                ? presentationGlyphs[unitIndex].glyphId
                : 0;
            // The canonical code point is what TMP already lays out, possibly case-mapped or replaced by a fallback glyph.
            if (glyphId > 0 &&
                glyphId != character.character &&
                plan != null &&
                glyphId == plan.GetPrimaryCodePoint(unitIndex))
            {
                glyphId = 0;
            }
            TMP_Character target = ResolveSubstitute(in character, glyphId);
            uint targetCodePoint = target != null ? (uint)glyphId : 0u;
            uint applied = _appliedSubstitutes != null && renderIndex < _appliedSubstitutes.Length
                ? _appliedSubstitutes[renderIndex]
                : 0u;
            if (targetCodePoint == applied)
                return rewriteApplied && applied != 0u;

            int materialIndex = renderGlyph.MaterialIndex;
            int vertexIndex = renderGlyph.VertexIndex;
            if (target != null &&
                (!EnsurePresentationGeometry(materialIndex) ||
                 !TryWriteSubstituteQuad(in character, target, materialIndex, vertexIndex)))
            {
                // A substitute that cannot be laid out keeps the authored glyph.
                targetCodePoint = 0u;
                if (applied == 0u)
                    return false;
            }

            if (targetCodePoint == 0u)
                RestoreSourceQuad(materialIndex, vertexIndex);
            else if (_appliedSubstitutes == null || _appliedSubstitutes.Length <= renderIndex)
                Array.Resize(ref _appliedSubstitutes, Mathf.Max(renderIndex + 1, _renderGlyphs.Length));

            if (_appliedSubstitutes != null && renderIndex < _appliedSubstitutes.Length)
                _appliedSubstitutes[renderIndex] = targetCodePoint;
            if (applied == 0u)
                _appliedSubstituteCount++;
            else if (targetCodePoint == 0u)
                _appliedSubstituteCount--;
            return true;
        }

        /// <summary>
        /// Returns the same-font character a presentation glyph substitutes for the rendered character, or null when the
        /// rendered character keeps its authored glyph.
        /// </summary>
        static TMP_Character ResolveSubstitute(in TMP_CharacterInfo character, int glyphId)
        {
            if (glyphId <= 0 ||
                glyphId == character.character ||
                character.elementType != TMP_TextElementType.Character)
            {
                return null;
            }

            TMP_FontAsset font = character.fontAsset;
            TMP_TextElement source = character.textElement;
            Glyph sourceGlyph = ResolveSourceGlyph(in character);
            if (font == null ||
                source == null ||
                sourceGlyph == null ||
                !font.characterLookupTable.TryGetValue((uint)glyphId, out TMP_Character target) ||
                target == null ||
                target.glyph == null ||
                ReferenceEquals(target, source))
            {
                return null;
            }

            // The quad keeps the source glyph's material, so a substitute on another atlas page keeps the authored glyph.
            return target.glyph.atlasIndex == sourceGlyph.atlasIndex ? target : null;
        }

        static Glyph ResolveSourceGlyph(in TMP_CharacterInfo character)
        {
#if UNITY_6000_0_OR_NEWER
            if (character.alternativeGlyph != null)
                return character.alternativeGlyph;
#endif
            return character.textElement?.glyph;
        }

        bool EnsurePresentationGeometry(int materialIndex)
        {
            if (IsPresentationGeometryValid(materialIndex))
                return true;

            Vector3[] sourceVertices = _cachedVertices != null && materialIndex >= 0 && materialIndex < _cachedVertices.Length
                ? _cachedVertices[materialIndex]
                : null;
            TmpUv0[] sourceUv0 = _cachedUv0 != null && materialIndex >= 0 && materialIndex < _cachedUv0.Length
                ? _cachedUv0[materialIndex]
                : null;
            if (sourceVertices == null || sourceUv0 == null)
                return false;

            int meshCount = _cachedVertices.Length;
            if (_presentationVertices == null || _presentationVertices.Length < meshCount)
                Array.Resize(ref _presentationVertices, meshCount);
            if (_presentationUv0 == null || _presentationUv0.Length < meshCount)
                Array.Resize(ref _presentationUv0, meshCount);
            if (_presentationGeometryValid == null || _presentationGeometryValid.Length < meshCount)
                Array.Resize(ref _presentationGeometryValid, meshCount);
            if (_presentationVertices[materialIndex] == null ||
                _presentationVertices[materialIndex].Length < sourceVertices.Length)
            {
                _presentationVertices[materialIndex] = new Vector3[sourceVertices.Length];
            }
            if (_presentationUv0[materialIndex] == null ||
                _presentationUv0[materialIndex].Length < sourceUv0.Length)
            {
                _presentationUv0[materialIndex] = new TmpUv0[sourceUv0.Length];
            }
            Array.Copy(sourceVertices, _presentationVertices[materialIndex], sourceVertices.Length);
            Array.Copy(sourceUv0, _presentationUv0[materialIndex], sourceUv0.Length);
            _presentationGeometryValid[materialIndex] = true;
            return true;
        }

        void RestoreSourceQuad(int materialIndex, int vertexIndex)
        {
            if (!IsPresentationGeometryValid(materialIndex))
                return;
            Vector3[] sourceVertices = _cachedVertices != null && materialIndex < _cachedVertices.Length
                ? _cachedVertices[materialIndex]
                : null;
            TmpUv0[] sourceUv0 = _cachedUv0 != null && materialIndex < _cachedUv0.Length
                ? _cachedUv0[materialIndex]
                : null;
            if (HasQuad(sourceVertices, vertexIndex) && HasQuad(_presentationVertices[materialIndex], vertexIndex))
                Array.Copy(sourceVertices, vertexIndex, _presentationVertices[materialIndex], vertexIndex, 4);
            if (HasQuad(sourceUv0, vertexIndex) && HasQuad(_presentationUv0[materialIndex], vertexIndex))
                Array.Copy(sourceUv0, vertexIndex, _presentationUv0[materialIndex], vertexIndex, 4);
        }

        /// <summary>
        /// Lays out a same-font substitute the way TMP lays out its own glyph: the target's bearing, size and padded atlas
        /// rect at the source glyph's origin, baseline, scale and slant. The source quad maps glyph metric units to mesh
        /// space; TMP pads it horizontally by the UV padding and vertically by the material padding alone, so the
        /// horizontal padding comes from the source UV margin and the vertical padding from the quad height at the
        /// character scale.
        /// </summary>
        bool TryWriteSubstituteQuad(
            in TMP_CharacterInfo character,
            TMP_Character target,
            int materialIndex,
            int vertexIndex)
        {
            Vector3[] sourceVertices = _cachedVertices[materialIndex];
            TmpUv0[] sourceUv0 = _cachedUv0[materialIndex];
            Vector3[] vertices = _presentationVertices[materialIndex];
            TmpUv0[] uv0 = _presentationUv0[materialIndex];
            TMP_FontAsset font = character.fontAsset;
            TMP_TextElement sourceElement = character.textElement;
            Glyph sourceGlyph = ResolveSourceGlyph(in character);
            Glyph targetGlyph = target.glyph;
            if (!HasQuad(sourceVertices, vertexIndex) ||
                !HasQuad(sourceUv0, vertexIndex) ||
                !HasQuad(vertices, vertexIndex) ||
                !HasQuad(uv0, vertexIndex) ||
                font == null ||
                sourceElement == null ||
                sourceElement.glyph == null ||
                sourceGlyph == null)
            {
                return false;
            }

            int atlasWidth = font.atlasWidth;
            int atlasHeight = font.atlasHeight;
            float sourceScale = sourceElement.scale * sourceElement.glyph.scale;
            float targetScale = target.scale * targetGlyph.scale;
            if (atlasWidth <= 0 || atlasHeight <= 0 ||
                character.scale <= 0f || sourceScale <= 0f || targetScale <= 0f)
            {
                return false;
            }

            Vector3 bottomLeft = sourceVertices[vertexIndex];
            Vector3 baseEdge = sourceVertices[vertexIndex + 3] - bottomLeft;
            Vector3 sideEdge = sourceVertices[vertexIndex + 1] - bottomLeft;
            float baseLength = Mathf.Sqrt(baseEdge.x * baseEdge.x + baseEdge.y * baseEdge.y);
            if (baseLength <= 0.000001f)
                return false;
            float quadHeight = Mathf.Abs(baseEdge.x * sideEdge.y - baseEdge.y * sideEdge.x) / baseLength;

            GlyphRect sourceRect = sourceGlyph.glyphRect;
            GlyphMetrics sourceMetrics = sourceGlyph.metrics;
            float paddingX = sourceRect.x - sourceUv0[vertexIndex].x * atlasWidth;
            float paddingY = sourceRect.y - sourceUv0[vertexIndex].y * atlasHeight;
            float quadPaddingY = (quadHeight / character.scale - sourceMetrics.height) * 0.5f;
            float sourceLeft = sourceMetrics.horizontalBearingX - paddingX;
            float sourceRight = sourceMetrics.horizontalBearingX + sourceMetrics.width + paddingX;
            float sourceBottom = sourceMetrics.horizontalBearingY - sourceMetrics.height - quadPaddingY;
            float sourceTop = sourceMetrics.horizontalBearingY + quadPaddingY;
            if (sourceRight - sourceLeft <= 0.000001f || sourceTop - sourceBottom <= 0.000001f)
                return false;

            // TMP scales a glyph, padding included, by its character and glyph scale.
            float ratio = targetScale / sourceScale;
            Vector3 unitX = baseEdge / (sourceRight - sourceLeft);
            Vector3 unitY = sideEdge / (sourceTop - sourceBottom);
            Vector3 originOffset = bottomLeft - sourceLeft * unitX - sourceBottom * unitY;
            GlyphRect targetRect = targetGlyph.glyphRect;
            GlyphMetrics targetMetrics = targetGlyph.metrics;
            if (targetRect.width <= 0 || targetRect.height <= 0)
            {
                // An empty substitute such as a space draws nothing, as TMP draws no quad for it.
                Vector3 origin = originOffset + targetMetrics.horizontalBearingX * ratio * unitX;
                float u = targetRect.x / (float)atlasWidth;
                float v = targetRect.y / (float)atlasHeight;
                for (int corner = 0; corner < 4; corner++)
                {
                    vertices[vertexIndex + corner] = origin;
                    SetGlyphUv0(uv0, sourceUv0, vertexIndex + corner, u, v, ratio);
                }
                return true;
            }

            float left = (targetMetrics.horizontalBearingX - paddingX) * ratio;
            float right = (targetMetrics.horizontalBearingX + targetMetrics.width + paddingX) * ratio;
            float bottom = (targetMetrics.horizontalBearingY - targetMetrics.height - quadPaddingY) * ratio;
            float top = (targetMetrics.horizontalBearingY + quadPaddingY) * ratio;
            vertices[vertexIndex] = originOffset + left * unitX + bottom * unitY;
            vertices[vertexIndex + 1] = originOffset + left * unitX + top * unitY;
            vertices[vertexIndex + 2] = originOffset + right * unitX + top * unitY;
            vertices[vertexIndex + 3] = originOffset + right * unitX + bottom * unitY;

            float uMin = (targetRect.x - paddingX) / atlasWidth;
            float vMin = (targetRect.y - paddingY) / atlasHeight;
            float uMax = (targetRect.x + targetRect.width + paddingX) / atlasWidth;
            float vMax = (targetRect.y + targetRect.height + paddingY) / atlasHeight;
            SetGlyphUv0(uv0, sourceUv0, vertexIndex, uMin, vMin, ratio);
            SetGlyphUv0(uv0, sourceUv0, vertexIndex + 1, uMin, vMax, ratio);
            SetGlyphUv0(uv0, sourceUv0, vertexIndex + 2, uMax, vMax, ratio);
            SetGlyphUv0(uv0, sourceUv0, vertexIndex + 3, uMax, vMin, ratio);
            return true;
        }

        // Keeps the source UV0 z, which the appearance identity stamp owns, and w, TMP's signed SDF scale, which follows the glyph scale.
        static void SetGlyphUv0(TmpUv0[] destination, TmpUv0[] source, int vertexIndex, float x, float y, float scaleRatio)
        {
#if UNITY_2023_2_OR_NEWER
            TmpUv0 sourceUv = source[vertexIndex];
            destination[vertexIndex] = new TmpUv0(x, y, sourceUv.z, sourceUv.w * scaleRatio);
#else
            destination[vertexIndex] = new TmpUv0(x, y);
#endif
        }

        struct RenderGlyph
        {
            public readonly int CharacterInfoIndex;
            public readonly int UnitIndex;
            public readonly int SourceIndex;
            public readonly int MaterialIndex;
            public readonly int VertexIndex;
            public readonly bool HasUniformSourceColor;
            public readonly bool IsUnitPrimary;

            public RenderGlyph(
                int characterInfoIndex,
                int unitIndex,
                int sourceIndex,
                int materialIndex,
                int vertexIndex,
                bool hasUniformSourceColor,
                bool isUnitPrimary)
            {
                CharacterInfoIndex = characterInfoIndex;
                UnitIndex = unitIndex;
                SourceIndex = sourceIndex;
                MaterialIndex = materialIndex;
                VertexIndex = vertexIndex;
                HasUniformSourceColor = hasUniformSourceColor;
                IsUnitPrimary = isUnitPrimary;
            }
        }

        readonly struct MaterialPropertyBinding
        {
            internal readonly TextStudioRenderPropertyRegistration Registration;

            internal bool IsValid => !string.IsNullOrEmpty(Registration.Descriptor.Id);

            MaterialPropertyBinding(
                in TextStudioRenderPropertyRegistration registration)
            {
                Registration = registration;
            }

            internal static MaterialPropertyBinding Resolve(string descriptorId)
            {
                return TextStudioRenderPropertyRegistry.TryGetRegistration(
                    descriptorId,
                    out TextStudioRenderPropertyRegistration registration)
                    ? new MaterialPropertyBinding(in registration)
                    : default;
            }
        }
    }
}
