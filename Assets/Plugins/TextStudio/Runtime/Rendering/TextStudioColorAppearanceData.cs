using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>A prepared scalar silhouette and the exact static image geometry from which it was produced.</summary>
    [Serializable]
    public sealed class TextStudioPreparedColorField
    {
        [SerializeField] Texture2D _sourceTexture;
        [SerializeField] Vector2[] _positions;
        [SerializeField] Vector2[] _uv;
        [SerializeField] int[] _triangles;
        [SerializeField] float _threshold;
        [SerializeField] bool _premultipliedAlpha;
        [SerializeField] Hash128 _sourceHash;
        [SerializeField] Texture2D _field;
        [SerializeField] Rect _bounds;
        [SerializeField] float _density, _requestedDensity, _padding, _distanceRange;
        [NonSerialized] TextStudioColorSource _source;
        [NonSerialized] uint _textureRevision;

        public Texture2D SourceTexture => _sourceTexture;
        public Texture2D Field => _field;
        public Rect Bounds => _bounds;
        public float PixelsPerEm => _density;
        public float RequestedPixelsPerEm => _requestedDensity;
        public float PaddingEm => _padding;
        public float DistanceRangeEm => _distanceRange;
        public long TextureBytes => _field != null ? (long)_field.width * _field.height * 4 : 0;

        /// <summary>Creates and validates a prepared silhouette field using copied static Texture2D source geometry and the supplied bounds, density, padding, and distance interval.</summary>
        public TextStudioPreparedColorField(TextStudioColorSource source, Texture2D field, Rect bounds,
            float pixelsPerEm, float requestedPixelsPerEm, float paddingEm, float distanceRangeEm)
        {
            if (source == null || source.Texture is not Texture2D texture || source.Revision != 0)
                throw new ArgumentException("Prepared images require a static Texture2D source without a backend content revision.", nameof(source));
            _sourceTexture = texture;
            _positions = new Vector2[source.Positions.Length];
            _uv = new Vector2[source.Uv.Length];
            _triangles = new int[source.Triangles.Length];
            for (int i = 0; i < _positions.Length; i++) { _positions[i] = source.Positions[i]; _uv[i] = source.Uv[i]; }
            for (int i = 0; i < _triangles.Length; i++) _triangles[i] = source.Triangles[i];
            _threshold = source.AlphaThreshold; _premultipliedAlpha = source.PremultipliedAlpha;
            _field = field; _bounds = bounds; _density = pixelsPerEm; _requestedDensity = requestedPixelsPerEm;
            _padding = paddingEm; _distanceRange = distanceRangeEm;
#if UNITY_EDITOR
            _sourceHash = texture.imageContentsHash;
#endif
            ResetSource();
            if (!Validate(out string error)) throw new ArgumentException(error);
        }

        internal TextStudioColorSource CreateSource() => new TextStudioColorSource(_sourceTexture, _positions, _uv, _triangles, _threshold, _premultipliedAlpha);

        internal void ResetSource()
        {
            _source = null;
            _textureRevision = _sourceTexture != null ? _sourceTexture.updateCount : 0;
        }

        internal bool Matches(TextStudioColorSource source)
        {
            if (source == null || _sourceTexture == null || source.Texture != _sourceTexture || _field == null ||
                source.Revision != 0 || _sourceTexture.updateCount != _textureRevision || !SourceContentMatches()) return false;
            _source ??= CreateSource();
            return _source.Identity == source.Identity;
        }

        internal bool SourceContentMatches()
        {
            if (_sourceTexture == null) return false;
#if UNITY_EDITOR
            if (_sourceTexture.imageContentsHash != _sourceHash) return false;
#endif
            return true;
        }

        internal bool Validate(out string error)
        {
            error = string.Empty;
            if (_sourceTexture == null || _positions == null || _positions.Length > 65536 || _triangles == null || _triangles.Length > 196608)
                error = "A prepared image has missing or excessive source geometry.";
            else if (_field == null || _field.format != TextureFormat.RFloat || _field.mipmapCount != 1 ||
                _field.wrapMode != TextureWrapMode.Clamp || _field.width > TextStudioColorFieldCache.MaximumTileSize || _field.height > TextStudioColorFieldCache.MaximumTileSize)
                error = "Prepared image fields require scalar RFloat textures up to 1024 pixels, without mipmaps and with clamp sampling.";
            else if (!float.IsFinite(_density) || _density <= 0 || !float.IsFinite(_requestedDensity) || _requestedDensity < 16 || _requestedDensity > 1024 ||
                !float.IsFinite(_padding) || _padding <= 0 || _padding > 16 || !float.IsFinite(_distanceRange) || _distanceRange <= 0 ||
                !float.IsFinite(_bounds.x) || !float.IsFinite(_bounds.y) || !float.IsFinite(_bounds.width) || !float.IsFinite(_bounds.height) || _bounds.width <= 0 || _bounds.height <= 0)
                error = "A prepared image has invalid field dimensions, density, or distance extent.";
            if (error.Length > 0) return false;
            try
            {
                TextStudioColorSource source = CreateSource();
                Rect native = source.Bounds;
                const float tolerance = .00001f;
                if (source.IsEmpty || _bounds.xMin > native.xMin - _padding + tolerance || _bounds.yMin > native.yMin - _padding + tolerance ||
                    _bounds.xMax < native.xMax + _padding - tolerance || _bounds.yMax < native.yMax + _padding - tolerance ||
                    Mathf.Abs(_density - Mathf.Min(_field.width / _bounds.width, _field.height / _bounds.height)) > .01f)
                    error = "The prepared image field does not cover its source geometry at the declared density and extent.";
            }
            catch (ArgumentException exception) { error = exception.Message; }
            return error.Length == 0;
        }
    }

    /// <summary>Build-reachable static image silhouettes. Original color textures remain independent from the scalar fields.</summary>
    public sealed class TextStudioColorAppearanceData : ScriptableObject
    {
        public const int CurrentSchema = 1;
        [SerializeField] int _schema = CurrentSchema;
        [SerializeField] TextStudioPreparedColorField[] _fields = Array.Empty<TextStudioPreparedColorField>();
        [NonSerialized] bool _validated, _valid;
        public int FieldCount => _fields?.Length ?? 0;
        /// <summary>Returns the prepared color-silhouette field at the supplied array index.</summary>
        public TextStudioPreparedColorField GetField(int index) => _fields[index];
        public long TextureBytes
        {
            get { long bytes = 0; for (int i = 0; i < FieldCount; i++) bytes += _fields[i]?.TextureBytes ?? 0; return bytes; }
        }

        /// <summary>Validates and stores a copied field array, restoring the previous array and throwing if the replacement is invalid.</summary>
        public void SetFields(TextStudioPreparedColorField[] fields)
        {
            TextStudioPreparedColorField[] previous = _fields;
            _fields = fields == null ? Array.Empty<TextStudioPreparedColorField>() : (TextStudioPreparedColorField[])fields.Clone();
            _validated = false;
            if (Validate(out string error)) return;
            _fields = previous; _validated = false;
            throw new ArgumentException(error, nameof(fields));
        }

        /// <summary>Checks prepared image schema, resource limits, field validity, and unique source identities, returning a diagnostic on failure.</summary>
        public bool Validate(out string error)
        {
            error = string.Empty;
            if (_schema != CurrentSchema) error = "Prepare the image data again with the current Text Studio version.";
            else if (FieldCount == 0 || FieldCount > TextStudioColorFieldCache.MaximumSources || TextureBytes > TextStudioColorFieldCache.MaximumTextureBytes)
                error = "Prepared image data must contain 1 to 128 sources within 64 MiB of field textures.";
            HashSet<ulong> identities = new HashSet<ulong>();
            for (int i = 0; i < FieldCount && error.Length == 0; i++)
            {
                if (_fields[i] == null) error = "A prepared image field is missing.";
                else if (_fields[i].Validate(out error) && !identities.Add(_fields[i].CreateSource().Identity)) error = "A prepared image source is duplicated.";
            }
            _validated = true; _valid = error.Length == 0;
            return _valid;
        }

        internal TextStudioPreparedColorField Find(TextStudioColorSource source)
        {
            if (!_validated) Validate(out _);
            if (!_valid) return null;
            for (int i = 0; i < FieldCount; i++) if (_fields[i].Matches(source)) return _fields[i];
            return null;
        }

        void OnEnable()
        {
            _validated = false;
            for (int i = 0; i < FieldCount; i++) _fields[i]?.ResetSource();
        }
        void OnValidate() => OnEnable();
    }
}
