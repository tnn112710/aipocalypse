using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>One independently painted treatment of an owned decoration surface.</summary>
    [Serializable]
    public partial struct TextStudio2DSurfaceTreatment
    {
        [SerializeField] TextStudioStackItemId _itemId;
        [SerializeField] string _displayName;
        [SerializeField] TextStudio2DLayerKind _kind;
        [SerializeField] bool _enabled;
        [SerializeField, Range(0f, 1f)] float _opacity;
        [SerializeField] TextStudio2DBlendMode _blendMode;
        [SerializeField] bool _participatesInAnimatedTint;
        [SerializeField] TextStudio2DPaint _paint;
        [SerializeField] TextStudio2DFillSpec _fill;
        [SerializeField] TextStudio2DStrokeSpec _stroke;
        [SerializeField] TextStudio2DShadowSpec _shadow;
        [SerializeField] TextStudio2DGlowSpec _glow;
        [SerializeField] TextStudio2DBevelSpec _bevel;
        [SerializeField] TextStudio2DMaterialResponseSpec _materialResponse;

        public TextStudioStackItemId ItemId => _itemId;
        public string DisplayName => _displayName ?? string.Empty;
        public TextStudio2DLayerKind Kind => _kind;
        public bool IsEnabled => _enabled;
        public float Opacity => _opacity;
        public TextStudio2DBlendMode BlendMode => _blendMode;
        public bool ParticipatesInAnimatedTint => _participatesInAnimatedTint;
        public TextStudio2DPaint Paint => _paint;
        public TextStudio2DFillSpec Fill => _fill;
        public TextStudio2DStrokeSpec Stroke => _stroke;
        public TextStudio2DShadowSpec Shadow => _shadow;
        public TextStudio2DGlowSpec Glow => _glow;
        public TextStudio2DBevelSpec Bevel => _bevel;
        public TextStudio2DMaterialResponseSpec MaterialResponse => _materialResponse;
        internal TextStudio2DUnit Units => _kind switch
        {
            TextStudio2DLayerKind.Fill => _fill.Units,
            TextStudio2DLayerKind.Stroke => _stroke.Units,
            TextStudio2DLayerKind.InnerShadow or TextStudio2DLayerKind.OuterShadow => _shadow.Units,
            TextStudio2DLayerKind.InnerGlow or TextStudio2DLayerKind.OuterGlow => _glow.Units,
            TextStudio2DLayerKind.Bevel => _bevel.Units,
            _ => TextStudio2DUnit.Em
        };

        /// <summary>Creates a named decoration-surface fill treatment with an independent paint copy and the supplied fill settings.</summary>
        public static TextStudio2DSurfaceTreatment CreateFill(string name, TextStudio2DPaint paint, TextStudio2DFillSpec fill = default)
        {
            TextStudio2DSurfaceTreatment value = Defaults(TextStudio2DLayerKind.Fill, name, paint);
            value._fill = fill;
            return value;
        }

        /// <summary>Creates a named decoration-surface stroke treatment with independent paint and stroke-pattern copies.</summary>
        public static TextStudio2DSurfaceTreatment CreateStroke(string name, TextStudio2DPaint paint, TextStudio2DStrokeSpec stroke)
        {
            TextStudio2DSurfaceTreatment value = Defaults(TextStudio2DLayerKind.Stroke, name, paint);
            value._stroke = stroke.Clone();
            return value;
        }

        /// <summary>Creates a named inner or outer shadow treatment for a decoration surface using the supplied paint and shadow settings.</summary>
        public static TextStudio2DSurfaceTreatment CreateShadow(string name, bool inner, TextStudio2DPaint paint, TextStudio2DShadowSpec shadow)
        {
            TextStudio2DSurfaceTreatment value = Defaults(inner ? TextStudio2DLayerKind.InnerShadow : TextStudio2DLayerKind.OuterShadow, name, paint);
            value._shadow = shadow;
            return value;
        }

        /// <summary>Creates a named inner or outer glow treatment for a decoration surface using the supplied paint and glow settings.</summary>
        public static TextStudio2DSurfaceTreatment CreateGlow(string name, bool inner, TextStudio2DPaint paint, TextStudio2DGlowSpec glow)
        {
            TextStudio2DSurfaceTreatment value = Defaults(inner ? TextStudio2DLayerKind.InnerGlow : TextStudio2DLayerKind.OuterGlow, name, paint);
            value._glow = glow;
            return value;
        }

        /// <summary>Creates a named bevel treatment for a decoration surface with the supplied relief and lighting settings.</summary>
        public static TextStudio2DSurfaceTreatment CreateBevel(string name, TextStudio2DBevelSpec bevel)
        {
            TextStudio2DSurfaceTreatment value = Defaults(TextStudio2DLayerKind.Bevel, name, default);
            value._bevel = bevel;
            return value;
        }

        /// <summary>Creates a named material-response treatment for a decoration surface with the supplied surface and lighting settings.</summary>
        public static TextStudio2DSurfaceTreatment CreateMaterialResponse(string name, TextStudio2DMaterialResponseSpec response)
        {
            TextStudio2DSurfaceTreatment value = Defaults(TextStudio2DLayerKind.MaterialResponse, name, default);
            value._materialResponse = response;
            return value;
        }

        /// <summary>Returns a surface-treatment copy with the supplied persistent stack-item identity.</summary>
        public TextStudio2DSurfaceTreatment WithIdentity(TextStudioStackItemId identity)
        {
            TextStudio2DSurfaceTreatment value = this;
            value._itemId = identity;
            return value;
        }

        /// <summary>Returns a surface-treatment copy with the supplied enabled state.</summary>
        public TextStudio2DSurfaceTreatment WithState(bool enabled)
        {
            TextStudio2DSurfaceTreatment value = this;
            value._enabled = enabled;
            return value;
        }

        /// <summary>Returns a surface-treatment copy with the supplied opacity and blend mode.</summary>
        public TextStudio2DSurfaceTreatment WithCompositing(float opacity, TextStudio2DBlendMode blendMode = TextStudio2DBlendMode.Normal)
        {
            TextStudio2DSurfaceTreatment value = this;
            value._opacity = opacity;
            value._blendMode = blendMode;
            return value;
        }

        /// <summary>Returns a surface-treatment copy with the supplied participation in animated tinting.</summary>
        public TextStudio2DSurfaceTreatment WithAnimatedTint(bool participates)
        {
            TextStudio2DSurfaceTreatment value = this;
            value._participatesInAnimatedTint = participates;
            return value;
        }

        /// <summary>Returns a surface-treatment copy with an independently copied paint binding.</summary>
        public TextStudio2DSurfaceTreatment WithPaint(TextStudio2DPaint paint)
        {
            TextStudio2DSurfaceTreatment value = this;
            value._paint = paint.Clone();
            return value;
        }

        internal TextStudio2DSurfaceTreatment Clone()
        {
            TextStudio2DSurfaceTreatment value = this;
            value._paint = _paint.Clone();
            value._stroke = _stroke.Clone();
            return value;
        }

        internal TextStudio2DLayer ToLayer(in TextStudio2DLayer owner)
        {
            TextStudio2DLayer layer = _kind switch
            {
                TextStudio2DLayerKind.Fill => TextStudio2DLayer.CreateFill(DisplayName, _paint, _fill),
                TextStudio2DLayerKind.Stroke => TextStudio2DLayer.CreateStroke(DisplayName, _paint, _stroke),
                TextStudio2DLayerKind.InnerShadow => TextStudio2DLayer.CreateShadow(DisplayName, true, _paint, _shadow),
                TextStudio2DLayerKind.OuterShadow => TextStudio2DLayer.CreateShadow(DisplayName, false, _paint, _shadow),
                TextStudio2DLayerKind.InnerGlow => TextStudio2DLayer.CreateGlow(DisplayName, true, _paint, _glow),
                TextStudio2DLayerKind.OuterGlow => TextStudio2DLayer.CreateGlow(DisplayName, false, _paint, _glow),
                TextStudio2DLayerKind.Bevel => TextStudio2DLayer.CreateBevel(DisplayName, _bevel),
                TextStudio2DLayerKind.MaterialResponse => TextStudio2DLayer.CreateMaterialResponse(DisplayName, _materialResponse),
                _ => throw new InvalidOperationException("This treatment cannot be applied to a decoration surface.")
            };
            return layer.WithIdentity(_itemId)
                .WithState(owner.IsEnabled && _enabled, owner.IsSolo)
                .WithCompositing(_opacity, _blendMode)
                .WithExecution(TextStudio2DExecutionPath.RetainedGeometry, owner.QualityTier)
                .WithSelector(owner.Selector)
                .WithRelationships(owner.ParentId, owner.MaskId)
                .WithAnimatedTint(owner.ParticipatesInAnimatedTint && _participatesInAnimatedTint)
                .WithSurfaceOwner(owner.ItemId);
        }

        internal static bool IsSupportedKind(TextStudio2DLayerKind kind)
        {
            return kind >= TextStudio2DLayerKind.Fill && kind <= TextStudio2DLayerKind.Bevel ||
                   kind == TextStudio2DLayerKind.MaterialResponse;
        }

        internal Vector4 GetExpansion(TextStudio2DUnit units)
        {
            if (!_enabled || Units != units) return Vector4.zero;
            float amount = _kind switch
            {
                TextStudio2DLayerKind.Fill => Mathf.Max(0f, _fill.Dilation) + Mathf.Max(0f, _fill.Softness),
                TextStudio2DLayerKind.Stroke => Mathf.Max(0f, _stroke.Width) * Mathf.Clamp01((_stroke.Alignment + 1f) * .5f) + Mathf.Max(0f, _stroke.Softness),
                TextStudio2DLayerKind.OuterGlow => Mathf.Max(0f, _glow.Radius + _glow.Spread),
                TextStudio2DLayerKind.OuterShadow => Mathf.Max(0f, _shadow.Blur + _shadow.Spread),
                _ => 0f
            };
            Vector2 offset = _kind == TextStudio2DLayerKind.OuterShadow ? _shadow.Offset : Vector2.zero;
            return new Vector4(amount + Mathf.Max(0f, -offset.x), amount + Mathf.Max(0f, -offset.y),
                amount + Mathf.Max(0f, offset.x), amount + Mathf.Max(0f, offset.y));
        }

        static TextStudio2DSurfaceTreatment Defaults(TextStudio2DLayerKind kind, string name, TextStudio2DPaint paint)
        {
            return new TextStudio2DSurfaceTreatment
            {
                _itemId = TextStudioStackItemId.CreateUnique("surface"),
                _displayName = string.IsNullOrWhiteSpace(name) ? kind.ToString() : name,
                _kind = kind,
                _enabled = true,
                _opacity = 1f,
                _blendMode = TextStudio2DBlendMode.Normal,
                _participatesInAnimatedTint = true,
                _paint = paint.Clone()
            };
        }
    }
}
