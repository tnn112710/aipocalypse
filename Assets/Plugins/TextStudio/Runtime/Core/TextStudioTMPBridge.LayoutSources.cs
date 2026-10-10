using System;
using System.Runtime.CompilerServices;
using ImpossibleRobert.Common;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;

namespace TextStudio
{
    internal sealed partial class TextStudioTMPBridge
    {
        readonly LayoutSourceCache _layoutSources = new LayoutSourceCache();
        // The visual version selects fonts in its own cache, so glyph building still registers each font source when it first meets it.
        readonly LayoutSourceCache _versionSources = new LayoutSourceCache();
        // Units whose visual glyph the record pass rebuilds from final source bounds. Every other glyph already carries them.
        int[] _widenedLayoutUnits = Array.Empty<int>();

        /// <summary>
        /// Resolves per-font and per-material source data once per layout pass. The glyphs of a pass share one or a few
        /// fonts and materials, whose identities, atlas metrics and field contracts otherwise cost native property reads,
        /// string allocations and string hashing for every glyph. Each pass starts with <see cref="Reset"/>, so atlas
        /// growth and material edits between passes are observed.
        /// </summary>
        sealed class LayoutSourceCache
        {
            static readonly int s_gradientScaleId = Shader.PropertyToID("_GradientScale");

            bool _hasFont;
            TMP_FontAsset _font;
            bool _fontExists;
            ulong _fontIdentity;
            ulong _shapingIdentity;
            float _pointSize;
            Texture2D[] _atlases;
            AtlasSource[] _atlasSources = Array.Empty<AtlasSource>();

            bool _hasMaterial;
            Material _material;
            float _gradientScale;
            TextStudioGlyphFieldFormat _fieldFormat;
            float _declaredPixelsPerEm;
            float _declaredSafeDistanceEm;

            struct AtlasSource
            {
                internal bool Resolved;
                internal bool Exists;
                internal int Width;
                internal int Height;
                internal int StableId;
                internal uint UpdateCount;
            }

            internal ulong FontIdentity => _fontIdentity;
            internal ulong ShapingIdentity => _shapingIdentity;
            internal float FallbackPixelsPerEm => _fontExists ? Mathf.Max(0f, _pointSize) : 0f;
            /// <summary>The selected font's face point size, or zero without a font, which the effect unit scale of a font glyph reads.</summary>
            internal float FacePointSize => _pointSize;
            internal float GradientScale => _gradientScale;
            internal TextStudioGlyphFieldFormat FieldFormat => _fieldFormat;

            internal void Reset()
            {
                _hasFont = false;
                _font = null;
                _atlases = null;
                _hasMaterial = false;
                _material = null;
            }

            /// <summary>Selects the next glyph's font and returns true when it differs from the previous glyph's font.</summary>
            internal bool SelectFont(TMP_FontAsset font)
            {
                if (_hasFont && ReferenceEquals(font, _font))
                    return false;

                _hasFont = true;
                _font = font;
                _fontExists = font != null;
                _fontIdentity = ResolveFontIdentity(font);
                _shapingIdentity = TextStudioTMPFontMetrics.ShapingIdentity(font);
                _pointSize = _fontExists ? font.faceInfo.pointSize : 0f;
                _atlases = _fontExists ? font.atlasTextures : null;
                int atlasCount = _atlases?.Length ?? 0;
                if (_atlasSources.Length < atlasCount)
                    _atlasSources = new AtlasSource[atlasCount];
                else
                    Array.Clear(_atlasSources, 0, atlasCount);
                return true;
            }

            /// <summary>Selects the next glyph's material, reading its field contract only when it changed.</summary>
            internal void SelectMaterial(Material material)
            {
                if (_hasMaterial && ReferenceEquals(material, _material))
                    return;

                _hasMaterial = true;
                _material = material;
                _gradientScale = material != null && material.HasProperty(s_gradientScaleId)
                    ? material.GetFloat(s_gradientScaleId)
                    : 0f;
                _fieldFormat = TextStudioGlyphFieldContract.ResolveFormat(material);
                // A zero fallback leaves only a positive declared value, as the contract returns it before any fallback.
                _declaredPixelsPerEm = TextStudioGlyphFieldContract.ResolvePixelsPerEm(material, 0f);
                _declaredSafeDistanceEm = TextStudioGlyphFieldContract.ResolveSafeDistanceEm(material, 0f);
            }

            internal float ResolvePixelsPerEm(float fallback)
            {
                return _declaredPixelsPerEm > 0f ? _declaredPixelsPerEm : Mathf.Max(0f, fallback);
            }

            internal float ResolveSafeDistanceEm(float fallback)
            {
                return _declaredSafeDistanceEm > 0f ? _declaredSafeDistanceEm : Mathf.Max(0f, fallback);
            }

            /// <summary>Matches <see cref="TextStudioTMPFontMetrics.AtlasToEm"/> for the selected font.</summary>
            internal Vector4 AtlasToEm(Glyph glyph)
            {
                if (!_fontExists || glyph == null || _pointSize <= 0f || _atlases == null ||
                    glyph.atlasIndex < 0 || glyph.atlasIndex >= _atlases.Length)
                    return default;
                ref AtlasSource atlas = ref ResolveAtlas(glyph.atlasIndex);
                if (!atlas.Exists)
                    return default;
                float density = _pointSize;
                return new Vector4(atlas.Width / density, atlas.Height / density,
                    (glyph.metrics.horizontalBearingX - glyph.glyphRect.x) / density,
                    (glyph.metrics.horizontalBearingY - glyph.metrics.height - glyph.glyphRect.y) / density);
            }

            /// <summary>Adds the values <see cref="AddFontAtlasVersion"/> adds for one atlas page of the selected font.</summary>
            internal void AddAtlasVersion(ref VisualVersionHash hash, int selectedAtlasIndex)
            {
                if (selectedAtlasIndex < 0)
                {
                    AddFontAtlasVersion(ref hash, _font, selectedAtlasIndex);
                    return;
                }

                hash.Add(_fontIdentity);
                if (_atlases == null)
                {
                    hash.Add(0);
                    return;
                }

                hash.Add(_atlases.Length);
                hash.Add(selectedAtlasIndex);
                if (selectedAtlasIndex < _atlases.Length)
                {
                    ref AtlasSource atlas = ref ResolveAtlas(selectedAtlasIndex);
                    hash.Add(atlas.Exists ? atlas.StableId : 0);
                    hash.Add(atlas.Exists ? atlas.UpdateCount : 0u);
                }
                else
                {
                    hash.Add(0);
                    hash.Add(0u);
                }
            }

            ref AtlasSource ResolveAtlas(int index)
            {
                ref AtlasSource source = ref _atlasSources[index];
                if (source.Resolved)
                    return ref source;

                Texture2D atlas = _atlases[index];
                source.Resolved = true;
                source.Exists = atlas != null;
                if (source.Exists)
                {
                    source.Width = atlas.width;
                    source.Height = atlas.height;
                    source.StableId = atlas.GetStableId();
                    source.UpdateCount = atlas.updateCount;
                }
                return ref source;
            }
        }

        /// <summary>
        /// Order-sensitive hash of 32-bit words for visual versions, which only invalidate in-memory renderer caches. One multiply per word replaces the byte-wise <see cref="TextStudioDeterministicHash64"/>, which persisted fingerprints keep using.
        /// </summary>
        internal struct VisualVersionHash
        {
            const ulong Offset = 14695981039346656037UL;
            const ulong Prime = 1099511628211UL;
            ulong _value;

            internal static VisualVersionHash Create()
            {
                VisualVersionHash hash = default;
                hash._value = Offset;
                return hash;
            }

            /// <summary>Folds the high half into the low half, so every word reaches the 32-bit version.</summary>
            internal uint Value => unchecked((uint)(_value ^ (_value >> 32)));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Add(uint value) => _value = unchecked((_value ^ value) * Prime);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Add(int value) => Add(unchecked((uint)value));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void Add(float value) => Add(BitConverter.SingleToInt32Bits(value));

            internal void Add(bool value) => Add(value ? 1u : 0u);

            internal void Add(ulong value)
            {
                Add(unchecked((uint)value));
                Add(unchecked((uint)(value >> 32)));
            }

            internal void Add(Vector3 value)
            {
                Add(value.x);
                Add(value.y);
                Add(value.z);
            }

            internal void Add(Vector4 value)
            {
                Add(value.x);
                Add(value.y);
                Add(value.z);
                Add(value.w);
            }

            internal void Add(string value)
            {
                string resolved = value ?? string.Empty;
                Add(resolved.Length);
                for (int i = 0; i < resolved.Length; i++)
                    Add((uint)resolved[i]);
            }
        }

        /// <summary>Matches ResolveEffectUnitScale for a glyph whose font this pass has selected, reading the cached face point size instead of copying the face info per glyph.</summary>
        float ResolveLayoutEffectUnitScale(in TMP_CharacterInfo character)
        {
            float facePointSize = _layoutSources.FacePointSize;
            if (character.elementType != TMP_TextElementType.Sprite && character.scale > 0f && facePointSize > 0f)
                return Mathf.Max(0.0001f, character.scale * facePointSize);
            return ResolveEffectUnitScale(_tmpText, in character);
        }

        /// <summary>Starts the layout fingerprint. RunLayout and <see cref="ComputeLayoutFingerprint"/> share these steps, because HandleTMPTextChanged compares their results.</summary>
        static int BeginLayoutFingerprint(TMP_Text tmpText, int characterCount)
        {
            int hash = 17;
            string text = tmpText.text ?? string.Empty;
            hash = CombineHash(hash, text.GetHashCode());
            hash = CombineHash(hash, tmpText.font != null ? tmpText.font.GetStableId() : 0);
            hash = CombineHash(hash, tmpText.fontSize.GetHashCode());
            hash = CombineHash(hash, (int)tmpText.fontStyle);
            hash = CombineHash(hash, tmpText.isOrthographic ? 1 : 0);
            return CombineHash(hash, characterCount);
        }

        /// <summary>Adds one character record to the layout fingerprint, in character order.</summary>
        static int AddLayoutFingerprintCharacter(int hash, TMP_TextInfo textInfo, in TMP_CharacterInfo character)
        {
            hash = CombineHash(hash, character.index);
            hash = CombineHash(hash, character.character);
            hash = CombineHash(hash, (int)character.elementType);
            hash = CombineHash(hash, character.fontAsset != null ? character.fontAsset.GetStableId() : 0);
            hash = CombineHash(hash, character.textElement != null ? unchecked((int)character.textElement.glyphIndex) : 0);
            hash = CombineHash(hash, character.materialReferenceIndex);
            hash = CombineHash(hash, character.vertexIndex);
            hash = CombineHash(hash, character.lineNumber);
            hash = CombineHash(hash, character.pointSize.GetHashCode());
            hash = CombineHash(hash, character.scale.GetHashCode());
            hash = CombineHash(hash, character.baseLine.GetHashCode());
            hash = CombineHash(hash, character.origin.GetHashCode());
            hash = CombineHash(hash, character.xAdvance.GetHashCode());
            hash = CombineHash(hash, (int)character.style);
            hash = CombineHash(hash, character.isVisible ? 1 : 0);
            hash = CombineHash(hash, character.bottomLeft.GetHashCode());
            hash = CombineHash(hash, character.topLeft.GetHashCode());
            hash = CombineHash(hash, character.topRight.GetHashCode());
            hash = CombineHash(hash, character.bottomRight.GetHashCode());
            hash = CombineHash(hash, character.color.GetHashCode());
            hash = CombineHash(hash, character.underlineColor.GetHashCode());
            hash = CombineHash(hash, character.strikethroughColor.GetHashCode());

            if (!character.isVisible ||
                !IsCharacterVertexRangeValid(textInfo, in character))
            {
                return hash;
            }

            ref readonly TMP_MeshInfo meshInfo = ref textInfo.meshInfo[character.materialReferenceIndex];
            int vertexIndex = character.vertexIndex;
            for (int vertexOffset = 0; vertexOffset < 4; vertexOffset++)
            {
                int index = vertexIndex + vertexOffset;
                hash = CombineHash(hash, meshInfo.vertices[index].GetHashCode());
                if (meshInfo.colors32 != null && index < meshInfo.colors32.Length)
                    hash = CombineHash(hash, meshInfo.colors32[index].GetHashCode());
            }

            return hash;
        }

        static VisualVersionHash BeginGeometryVersion(TMP_TextInfo textInfo, int characterCount)
        {
            VisualVersionHash geometry = VisualVersionHash.Create();
            geometry.Add(characterCount);
            if (textInfo != null && textInfo.meshInfo != null)
            {
                geometry.Add(textInfo.meshInfo.Length);
                for (int i = 0; i < textInfo.meshInfo.Length; i++)
                {
                    ref readonly TMP_MeshInfo meshInfo = ref textInfo.meshInfo[i];
                    Vector3[] vertices = meshInfo.vertices;
                    geometry.Add(meshInfo.vertexCount);
                    geometry.Add(vertices?.Length ?? 0);
                }
            }
            return geometry;
        }

        static void AddGeometryVersion(ref VisualVersionHash geometry, TMP_TextInfo textInfo, in TMP_CharacterInfo character)
        {
            geometry.Add(character.isVisible);
            geometry.Add(character.materialReferenceIndex);
            geometry.Add(character.vertexIndex);
            if (!IsCharacterVertexRangeValid(textInfo, in character))
                return;
            Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            for (int corner = 0; corner < 4; corner++)
                geometry.Add(vertices[character.vertexIndex + corner]);
        }

        /// <summary>Adds one character's glyph, atlas page and atlas rectangle. The caller passes the rectangle a visual glyph already read, so it is resolved once per character.</summary>
        void AddCharacterAtlasVersion(ref VisualVersionHash atlas, in TMP_CharacterInfo character, Vector4 atlasUvRect)
        {
            int atlasIndex = character.textElement != null &&
                             character.textElement.glyph != null
                ? character.textElement.glyph.atlasIndex
                : 0;
            atlas.Add(character.textElement != null
                ? character.textElement.glyphIndex
                : character.character);
            _versionSources.SelectFont(character.fontAsset);
            _versionSources.AddAtlasVersion(ref atlas, atlasIndex);
            atlas.Add(atlasUvRect);
        }

        static TextStudioGlyphVisualBounds ResolveGlyphVisualBounds(in TMP_CharacterInfo character, in TextStudioGlyphColorBounds colorBounds)
        {
            return new TextStudioGlyphVisualBounds(
                new Vector2(character.origin + colorBounds.Minimum.x, character.baseLine + colorBounds.Minimum.y),
                new Vector2(character.origin + colorBounds.Maximum.x, character.baseLine + colorBounds.Maximum.y), character.isVisible);
        }

        static TextStudioGlyphVisualBounds UniteGlyphBounds(in TextStudioGlyphVisualBounds unitBounds, in TextStudioGlyphVisualBounds bounds)
        {
            return new TextStudioGlyphVisualBounds(
                unitBounds.IsValid ? Vector2.Min(unitBounds.Minimum, bounds.Minimum) : bounds.Minimum,
                unitBounds.IsValid ? Vector2.Max(unitBounds.Maximum, bounds.Maximum) : bounds.Maximum, true);
        }

        static bool HasSameBits(Vector2 first, Vector2 second)
        {
            return BitConverter.SingleToInt32Bits(first.x) == BitConverter.SingleToInt32Bits(second.x) &&
                   BitConverter.SingleToInt32Bits(first.y) == BitConverter.SingleToInt32Bits(second.y);
        }

        int[] EnsureWidenedLayoutUnitCapacity(int count)
        {
            if (_widenedLayoutUnits.Length < count)
                _widenedLayoutUnits = new int[TextStudioBufferCapacity.ForCount(count)];
            return _widenedLayoutUnits;
        }
    }
}
