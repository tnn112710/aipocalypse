using UnityEngine;

namespace TextStudio
{
    /// <summary>Shared defaults for layer authoring and ad-hoc appearance tags.</summary>
    public static class TextStudio2DLayerDefaults
    {
        /// <summary>A fill that preserves the complete native face, outline, textures and underlay.</summary>
        internal static TextStudio2DLayer CreateNativeFill()
            => TextStudio2DLayer.CreateFill("Native appearance", TextStudio2DPaint.Solid(Color.white),
                default(TextStudio2DFillSpec).WithNativeAppearance());

        /// <summary>Creates the requested layer kind with standard authoring defaults, falling back to a white fill for an unknown kind.</summary>
        public static TextStudio2DLayer Create(TextStudio2DLayerKind kind)
        {
            return kind switch
            {
                TextStudio2DLayerKind.Fill =>
                    TextStudio2DLayer.CreateFill("Fill", TextStudio2DPaint.Solid(Color.white)),
                TextStudio2DLayerKind.Stroke =>
                    TextStudio2DLayer.CreateStroke(
                        "Stroke",
                        TextStudio2DPaint.Solid(Color.black),
                        new TextStudio2DStrokeSpec(0.05f)),
                TextStudio2DLayerKind.InnerShadow =>
                    TextStudio2DLayer.CreateShadow(
                        "Inner Shadow",
                        true,
                        TextStudio2DPaint.Solid(new Color(0f, 0f, 0f, 0.55f)),
                        new TextStudio2DShadowSpec(new Vector2(0.04f, -0.04f), 0.05f)),
                TextStudio2DLayerKind.OuterShadow =>
                    TextStudio2DLayer.CreateShadow(
                        "Outer Shadow",
                        false,
                        TextStudio2DPaint.Solid(new Color(0f, 0f, 0f, 0.5f)),
                        new TextStudio2DShadowSpec(new Vector2(0.06f, -0.06f), 0.08f)),
                TextStudio2DLayerKind.InnerGlow =>
                    TextStudio2DLayer.CreateGlow(
                        "Inner Glow",
                        true,
                        TextStudio2DPaint.Solid(Color.white),
                        new TextStudio2DGlowSpec(0.1f)),
                TextStudio2DLayerKind.OuterGlow =>
                    TextStudio2DLayer.CreateGlow(
                        "Outer Glow",
                        false,
                        TextStudio2DPaint.Solid(Color.white),
                        new TextStudio2DGlowSpec(0.12f)),
                TextStudio2DLayerKind.Bevel =>
                    TextStudio2DLayer.CreateBevel(
                        "Bevel",
                        new TextStudio2DBevelSpec(
                            0.05f,
                            0.02f,
                            0.5f,
                            135f,
                            45f,
                            Color.white,
                            Color.black)),
                TextStudio2DLayerKind.FauxDepth =>
                    TextStudio2DLayer.CreateFauxDepth(
                        "Faux Depth",
                        TextStudio2DPaint.Solid(Color.black),
                        new TextStudio2DFauxDepthSpec(new Vector2(0.08f, -0.08f))),
                TextStudio2DLayerKind.Decoration =>
                    TextStudio2DLayer.CreateDecoration(
                        "Decoration",
                        TextStudio2DPaint.Solid(Color.white),
                        new TextStudio2DDecorationSpec(
                            TextStudio2DDecorationKind.Underline,
                            0.05f)),
                TextStudio2DLayerKind.Mask =>
                    TextStudio2DLayer.CreateMask("Mask", new TextStudio2DMaskSpec(default)),
                TextStudio2DLayerKind.Group =>
                    TextStudio2DLayer.CreateGroup("Group"),
                TextStudio2DLayerKind.MaterialResponse =>
                    TextStudio2DLayer.CreateMaterialResponse(
                        "Material Response",
                        TextStudio2DMaterialResponseSpec.Preset(
                            TextStudio2DMaterialResponseKind.Emboss)),
                TextStudio2DLayerKind.GraphicEffect =>
                    TextStudio2DLayer.CreateGraphicEffect(
                        "Graphic Effect",
                        TextStudio2DGraphicEffectSpec.Preset(
                            TextStudio2DGraphicEffectKind.Halftone)),
                TextStudio2DLayerKind.AnimatedEffect =>
                    TextStudio2DLayer.CreateAnimatedEffect(
                        "Animated Effect",
                        TextStudio2DAnimatedEffectSpec.Preset(
                            TextStudio2DAnimatedEffectKind.LightSweep)),
                TextStudio2DLayerKind.ColorAdjustment =>
                    TextStudio2DLayer.CreateColorAdjustment(
                        "Color Adjustment",
                        TextStudio2DColorAdjustmentSpec.Adjust(grayscale: .5f)),
                TextStudio2DLayerKind.CompositeAdjustment =>
                    TextStudio2DLayer.CreateCompositeAdjustment("Composite Adjustment", TextStudio2DColorAdjustmentSpec.Adjust(grayscale: .5f)),
                _ => TextStudio2DLayer.CreateFill(
                    "Fill",
                    TextStudio2DPaint.Solid(Color.white))
            };
        }
    }
}
