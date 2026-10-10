using UnityEngine;

namespace TextStudio
{
    /// <summary>Single CPU packing contract shared by fused, retained, and Hero style tables.</summary>
    internal static class TextStudio2DGpuStylePacking
    {
        internal const int SchemaVersion = 13;

        internal static TextStudio2DGpuStyleHeader PackHeader(
            CompiledTextStudio2DStyle style)
        {
            return new TextStudio2DGpuStyleHeader
            {
                Metadata = new Vector4(
                    SchemaVersion,
                    style.OperationCount,
                    style.GradientStopCount,
                    (float)ResolveFeatures(style)),
                BoundsExpansion = style.BoundsExpansion.Vector
            };
        }

        internal static TextStudio2DGpuOperation PackOperation(
            CompiledTextStudio2DStyle style,
            int operationIndex,
            int textureSlice,
            TextStudio2DVisualModifierOperationFlags visualModifierFlags =
                TextStudio2DVisualModifierOperationFlags.None)
        {
            ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
            bool usesPaint = OperationUsesPaint(operation.Kind);
            TextStudio2DCompiledPaint paint = usesPaint
                ? style.GetPaint(operation.PaintIndex)
                : default;
            Vector4 paintMetadata = usesPaint
                ? new Vector4(
                    (float)paint.Kind,
                    paint.Mapping == TextStudio2DMappingSpace.Range &&
                    paint.RangeMapping == TextStudio2DRangeMapping.EachMatch ? 8f : (float)paint.Mapping,
                    paint.GradientStopStart,
                    paint.GradientStopCount)
                : new Vector4(-1f, -1f, 0f, 0f);
            Vector4 color = usesPaint
                ? ConvertColor(paint.Color)
                : Vector4.zero;
            Vector4 paintTransform0 = usesPaint
                ? new Vector4(
                    paint.Angle,
                    paint.Center.x,
                    paint.Center.y,
                    paint.Kind == TextStudio2DPaintKind.ProceduralNoise
                        ? paint.NoiseScale
                        : (float)paint.Spread)
                : Vector4.zero;
            Vector4 paintTransform1 = usesPaint
                ? new Vector4(
                    paint.Scale.x,
                    paint.Scale.y,
                    paint.Offset.x,
                    paint.Offset.y)
                : Vector4.zero;
            Vector4 parameters2 = Vector4.zero;
            Vector4 parameters3 = Vector4.zero;
            Vector4 parameters4 = Vector4.zero;
            Vector4 parameters5 = Vector4.zero;
            Vector4 parameters6 = Vector4.zero;
            Vector4 parameters7 = Vector4.zero;
            Vector4 parameters8 = Vector4.zero;
            Vector4 parameters9 = Vector4.zero;
            Vector4 parameters10 = Vector4.zero;
            Vector4 parameters11 = Vector4.zero;
            Vector4 parameters12 = Vector4.zero;
            Vector4 parameters13 = Vector4.zero;
            Vector4 auxiliaryColor0 = Vector4.zero;
            Vector4 auxiliaryColor1 = Vector4.zero;
            Vector4 auxiliaryColor2 = Vector4.zero;
            Vector4 auxiliaryColor3 = Vector4.zero;
            Vector4 auxiliaryColor4 = Vector4.zero;
            Vector4 auxiliaryColor5 = Vector4.zero;
            if (usesPaint && paint.Kind == TextStudio2DPaintKind.ProceduralNoise)
            {
                uint seed = unchecked((uint)paint.NoiseSeed);
                parameters2 = new Vector4(
                    paint.NoiseDetail,
                    seed & 0xffffu,
                    seed >> 16,
                    0f);
            }
            else if (usesPaint && paint.Kind == TextStudio2DPaintKind.DistanceGradient)
            {
                parameters2 = new Vector4(
                    (float)paint.DistanceDomain,
                    paint.DistanceRange.x,
                    paint.DistanceRange.y,
                    0f);
            }
            else if (usesPaint &&
                (paint.Kind == TextStudio2DPaintKind.Texture ||
                 paint.Kind == TextStudio2DPaintKind.Pattern))
            {
                paintMetadata.z = textureSlice;
                paintMetadata.w = 0f;
                paintTransform0.w = (float)paint.TextureFit;
                parameters2.w = paint.Texture != null && paint.Texture.height > 0
                    ? (float)paint.Texture.width / paint.Texture.height
                    : 1f;
            }
            else if (operation.Kind == TextStudio2DLayerKind.Bevel)
            {
                color = ConvertColor(operation.Bevel.Highlight);
                paintTransform0 = ConvertColor(operation.Bevel.Shadow);
            }
            else if (operation.Kind == TextStudio2DLayerKind.MaterialResponse)
            {
                TextStudio2DMaterialResponseSpec response =
                    operation.MaterialResponse;
                color = ConvertColor(response.Highlight);
                paintTransform0 = ConvertColor(response.Shadow);
                paintTransform1 = ConvertColor(response.Environment);
                paintMetadata.y = (float)response.Kind;
                paintMetadata.z = response.Seed;
                parameters2 = new Vector4(
                    response.Anisotropy,
                    response.Iridescence,
                    response.Sparkle,
                    response.Emission);
            }
            else if (operation.Kind == TextStudio2DLayerKind.GraphicEffect)
            {
                TextStudio2DGraphicEffectSpec graphic = operation.GraphicEffect;
                TextStudio2DHalftoneSizeRamp ramp = graphic.HalftoneSizeRamp;
                uint seed = unchecked((uint)graphic.Seed);
                paintMetadata = new Vector4(
                    -2f,
                    (float)graphic.Kind,
                    (float)graphic.Mapping,
                    0f);
                color = ConvertColor(graphic.PrimaryColor);
                paintTransform0 = ConvertColor(graphic.SecondaryColor);
                parameters2 = new Vector4(
                    0f,
                    seed & 0xffffu,
                    seed >> 16,
                    0f);
                parameters3 = new Vector4(
                    ramp.Enabled ? 1f : 0f,
                    ramp.MinimumRadius,
                    ramp.MaximumRadius,
                    ramp.AngleDegrees);
            }
            else if (operation.Kind == TextStudio2DLayerKind.AnimatedEffect)
            {
                TextStudio2DAnimatedEffectSpec animated = operation.AnimatedEffect;
                uint seed = unchecked((uint)animated.Seed);
                paintMetadata = new Vector4(
                    -3f,
                    (float)animated.Kind,
                    (float)animated.Mapping,
                    (float)animated.ReducedMotionBehavior);
                color = ConvertColor(animated.PrimaryColor);
                paintTransform0 = ConvertColor(animated.SecondaryColor);
                paintTransform1 = new Vector4(
                    animated.Direction.x,
                    animated.Direction.y,
                    0f,
                    0f);
                parameters2 = new Vector4(
                    animated.HistoryFrames,
                    seed & 0xffffu,
                    seed >> 16,
                    0f);
            }
            else if (TextStudio2DColorAdjustmentUtility.IsAdjustment(operation.Kind))
            {
                TextStudio2DColorAdjustmentSpec filter = operation.ColorAdjustment;
                paintMetadata = new Vector4(-4f, 0f, 0f, 0f);
                parameters2 = filter.Red;
                parameters3 = filter.Green;
                parameters4 = filter.Blue;
                parameters5 = filter.Alpha;
                parameters6 = filter.Offset;
                // w marks a root-scope adjustment, which affects every later row, including rows beyond the 64-row mask.
                parameters7 = new Vector4(
                    filter.Strength,
                    filter.ClampOutput ? 1f : 0f,
                    (float)filter.SurfaceSelection,
                    operation.ParentOperationIndex < 0 ? 1f : 0f);
                parameters8 = TextStudio2DColorAdjustmentUtility.ContributionMask(style, operationIndex);
            }

            if (operation.Kind == TextStudio2DLayerKind.Decoration)
            {
                TextStudio2DDecorationSpec decoration = operation.Decoration;
                int shapeMetadata =
                    (int)decoration.VerticalAnchor << 2 |
                    (int)decoration.LeftEnd << 4 |
                    (int)decoration.RightEnd << 7;
                parameters3 = new Vector4(
                    decoration.PlateHeight,
                    decoration.CapSize,
                    shapeMetadata,
                    0f);
                parameters4 = decoration.CornerRadii;
                parameters5 = new Vector4(
                    (float)decoration.Shape,
                    decoration.SquirclePower,
                    decoration.PolygonSides,
                    decoration.StarInset);
                Vector4[] packedPoints =
                {
                    Vector4.zero,
                    Vector4.zero,
                    Vector4.zero,
                    Vector4.zero
                };
                int pointCount = Mathf.Min(8, decoration.CustomPointCount);
                for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
                {
                    Vector2 point = decoration.GetCustomPoint(pointIndex);
                    int pair = pointIndex >> 1;
                    if ((pointIndex & 1) == 0)
                    {
                        packedPoints[pair].x = point.x;
                        packedPoints[pair].y = point.y;
                    }
                    else
                    {
                        packedPoints[pair].z = point.x;
                        packedPoints[pair].w = point.y;
                    }
                }
                parameters6 = packedPoints[0];
                parameters7 = packedPoints[1];
                parameters8 = packedPoints[2];
                parameters9 = packedPoints[3];
                parameters10 = decoration.GetTreatmentExpansion(TextStudio2DUnit.Em);
                parameters11 = decoration.GetTreatmentExpansion(TextStudio2DUnit.Pixels);
                parameters12.w = pointCount;
                int treatmentCount = 0;
                for (int child = operationIndex + 1; child < style.OperationCount &&
                    style.OperationAt(child).SurfaceOwnerOperationIndex == operationIndex; child++) treatmentCount++;
                parameters13 = new Vector4((float)decoration.Topology, operationIndex + 1, treatmentCount, 0f);
                if (decoration.Kind == TextStudio2DDecorationKind.Underline ||
                    decoration.Kind == TextStudio2DDecorationKind.Strikethrough ||
                    decoration.Kind == TextStudio2DDecorationKind.Overline)
                {
                    TextStudio2DDecorationLineSpec line = decoration.Line;
                    parameters3 = new Vector4((float)line.Pattern, (float)line.ThicknessMode, (float)line.OffsetMode, (float)line.Units);
                    parameters4 = new Vector4(line.DashLength, line.Gap, line.DotSpacing, line.WaveAmplitude);
                    parameters5 = new Vector4(line.Wavelength, line.DoubleSeparation, line.Phase,
                        (int)line.PhaseRestart | (int)line.Cap << 2);
                }
            }
            else if (operation.Kind == TextStudio2DLayerKind.FauxDepth)
            {
                TextStudio2DFauxDepthSpec depth = operation.FauxDepth;
                parameters3 = new Vector4(
                    depth.SideLightAngle,
                    depth.SideLightStrength,
                    depth.FrontBevelWidth,
                    depth.FrontBevelStrength);
                parameters2.w = depth.FarCapShade;
                parameters4.x = depth.SilhouetteOutset;
            }

            return new TextStudio2DGpuOperation
            {
                LayerMetadata = new Vector4(
                    (float)operation.Kind,
                    (float)operation.BlendMode,
                    operation.Opacity,
                    operation.ParticipatesInAnimatedTint ? 1f : 0f),
                ExecutionMetadata = new Vector4(
                    (float)operation.ExecutionPath,
                    (float)operation.QualityTier,
                    (float)operation.Features,
                    (float)visualModifierFlags),
                PaintMetadata = paintMetadata,
                Color = color,
                PaintTransform0 = paintTransform0,
                PaintTransform1 = paintTransform1,
                Parameters0 = operation.Parameters0,
                Parameters1 = operation.Parameters1,
                Parameters2 = parameters2,
                Parameters3 = parameters3,
                Parameters4 = parameters4,
                Parameters5 = parameters5,
                Parameters6 = parameters6,
                Parameters7 = parameters7,
                Parameters8 = parameters8,
                Parameters9 = parameters9,
                Parameters10 = parameters10,
                Parameters11 = parameters11,
                Parameters12 = parameters12,
                Parameters13 = parameters13,
                AuxiliaryColor0 = auxiliaryColor0,
                AuxiliaryColor1 = auxiliaryColor1,
                AuxiliaryColor2 = auxiliaryColor2,
                AuxiliaryColor3 = auxiliaryColor3,
                AuxiliaryColor4 = auxiliaryColor4,
                AuxiliaryColor5 = auxiliaryColor5,
                SelectorMetadata = new Vector4(
                    operation.SelectorIndex,
                    operation.ParentOperationIndex,
                    operation.MaskOperationIndex,
                    operation.SurfaceOwnerOperationIndex + 1)
            };
        }

        internal static TextStudio2DGpuGradientStop PackGradient(
            TextStudio2DGradientStop stop)
        {
            return new TextStudio2DGpuGradientStop
            {
                Metadata = new Vector4(stop.Position, 0f, 0f, 0f),
                Color = ConvertColor(stop.Color)
            };
        }

        internal static bool OperationUsesPaint(TextStudio2DLayerKind kind)
        {
            return kind != TextStudio2DLayerKind.Decoration &&
                   kind != TextStudio2DLayerKind.Bevel &&
                   kind != TextStudio2DLayerKind.MaterialResponse &&
                   kind != TextStudio2DLayerKind.GraphicEffect &&
                   kind != TextStudio2DLayerKind.AnimatedEffect &&
                   kind != TextStudio2DLayerKind.ColorAdjustment &&
                   kind != TextStudio2DLayerKind.CompositeAdjustment &&
                   kind != TextStudio2DLayerKind.Mask &&
                   kind != TextStudio2DLayerKind.Group;
        }

        static TextStudio2DShaderFeatures ResolveFeatures(
            CompiledTextStudio2DStyle style)
        {
            TextStudio2DShaderFeatures features = TextStudio2DShaderFeatures.None;
            for (int index = 0; index < style.OperationCount; index++)
                features |= style.OperationAt(index).Features;
            if (style.WorldShadowMode == TextStudio2DWorldShadowMode.AlphaClipped)
                features |= TextStudio2DShaderFeatures.WorldShadowCaster;
            return features;
        }

        static Vector4 ConvertColor(Color color)
        {
            Color converted = QualitySettings.activeColorSpace == ColorSpace.Linear
                ? color.linear
                : color;
            return converted;
        }
    }
}
