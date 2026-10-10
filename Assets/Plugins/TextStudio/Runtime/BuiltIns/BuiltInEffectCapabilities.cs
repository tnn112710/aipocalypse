namespace TextStudio.BuiltIns
{
    static class BuiltInEffectCapabilities
    {
        public static TextStudioEffectCapabilities For(string id, TextStudioPhaseMask phases)
        {
            TextStudioRenderChannels channels = ResolveChannels(id);
            bool hasPrimitive = TextStudioVisualPrimitiveCatalog.TryGet(
                id,
                out TextStudioVisualPrimitiveContract primitive);
            TextStudioReducedMotionBehavior reducedMotion = hasPrimitive
                ? primitive.ReducedMotionBehavior
                : ResolveReducedMotion(id, channels);
            bool canFlash = id == TextStudioBuiltInEffectIds.Blink ||
                            id == TextStudioBuiltInEffectIds.Glitch ||
                            id == TextStudioBuiltInEffectIds.ColorPulse ||
                            hasPrimitive && primitive.CanFlash;
            TextStudioEffectPerformanceTier performance = hasPrimitive
                ? primitive.PerformanceTier
                : TextStudioEffectPerformanceTier.Realtime;
            return new TextStudioEffectCapabilities(
                channels,
                phases,
                performance,
                reducedMotion,
                isDeterministic: true,
                canFlash: canFlash,
                supportedRenderDomains: ResolveRenderDomains(id),
                boundsBehavior: hasPrimitive &&
                    primitive.BoundsMode != TextStudioVisualPrimitiveBoundsMode.None
                        ? TextStudioEffectBoundsBehavior.DynamicOutput
                        : TextStudioEffectBoundsBehavior.Unchanged,
                readsInfluenceTarget: id == TextStudioBuiltInEffectIds.LensMagnifier);
        }

        static TextStudioRenderDomain ResolveRenderDomains(string id)
        {
            if (TextStudioVisualPrimitiveCatalog.TryGet(
                    id,
                    out TextStudioVisualPrimitiveContract primitive))
            {
                return primitive.SupportedRenderDomains;
            }
            switch (id)
            {
                case TextStudioBuiltInEffectIds.HeroDepthRise:
                case TextStudioBuiltInEffectIds.CardFlipY:
                case TextStudioBuiltInEffectIds.TopHinge:
                case TextStudioBuiltInEffectIds.DominoLine:
                case TextStudioBuiltInEffectIds.SpiralAssemble:
                case TextStudioBuiltInEffectIds.SpatialScatter:
                case TextStudioBuiltInEffectIds.FoldAway:
                case TextStudioBuiltInEffectIds.RollingRibbon:
                case TextStudioBuiltInEffectIds.SoftSpatialFloat:
                case TextStudioBuiltInEffectIds.Accordion:
                case TextStudioBuiltInEffectIds.Orbit:
                    return TextStudioRenderDomain.Extruded3D;
                default:
                    return TextStudioRenderDomain.All;
            }
        }

        static TextStudioRenderChannels ResolveChannels(string id)
        {
            switch (id)
            {
                case TextStudioBuiltInEffectIds.Wave:
                case TextStudioBuiltInEffectIds.Shake:
                case TextStudioBuiltInEffectIds.Bounce:
                case TextStudioBuiltInEffectIds.Float:
                case TextStudioBuiltInEffectIds.SlideHorizontal:
                case TextStudioBuiltInEffectIds.SlideVertical:
                case TextStudioBuiltInEffectIds.Decrypt:
                case TextStudioBuiltInEffectIds.Tracking:
                case TextStudioBuiltInEffectIds.Scatter2D:
                    return TextStudioRenderChannels.Position;
                case TextStudioBuiltInEffectIds.Wiggle:
                    return TextStudioRenderChannels.Position | TextStudioRenderChannels.Rotation;
                case TextStudioBuiltInEffectIds.Dangle:
                case TextStudioBuiltInEffectIds.Swing:
                case TextStudioBuiltInEffectIds.Pendulum:
                case TextStudioBuiltInEffectIds.Rotate:
                    return TextStudioRenderChannels.Rotation | TextStudioRenderChannels.Pivot;
                case TextStudioBuiltInEffectIds.Pulse:
                case TextStudioBuiltInEffectIds.Pop:
                case TextStudioBuiltInEffectIds.Expand:
                case TextStudioBuiltInEffectIds.Elastic:
                    return id == TextStudioBuiltInEffectIds.Expand
                        ? TextStudioRenderChannels.Scale | TextStudioRenderChannels.Alpha | TextStudioRenderChannels.Pivot
                        : TextStudioRenderChannels.Scale | TextStudioRenderChannels.Alpha;
                case TextStudioBuiltInEffectIds.Blink:
                case TextStudioBuiltInEffectIds.Typewriter:
                case TextStudioBuiltInEffectIds.Fade:
                    return TextStudioRenderChannels.Alpha;
                case TextStudioBuiltInEffectIds.Shimmer:
                    return TextStudioRenderChannels.Scale | TextStudioRenderChannels.Color;
                case TextStudioBuiltInEffectIds.GradientFlow:
                case TextStudioBuiltInEffectIds.ColorSweep:
                case TextStudioBuiltInEffectIds.ColorPulse:
                case TextStudioBuiltInEffectIds.Rainbow:
                    return TextStudioRenderChannels.Color;
                case TextStudioBuiltInEffectIds.Dissolve:
                case TextStudioBuiltInEffectIds.Burn:
                case TextStudioBuiltInEffectIds.SurfaceSweep:
                case TextStudioBuiltInEffectIds.OutlineGlow:
                case TextStudioBuiltInEffectIds.PixelGrid:
                case TextStudioBuiltInEffectIds.Squiggle:
                case TextStudioBuiltInEffectIds.Focus:
                    return TextStudioRenderChannels.None;
                case TextStudioBuiltInEffectIds.Size:
                case TextStudioBuiltInEffectIds.Emphasis:
                    return TextStudioRenderChannels.Scale;
                case TextStudioBuiltInEffectIds.Shear:
                    return TextStudioRenderChannels.Skew;
                case TextStudioBuiltInEffectIds.Glitch:
                    return TextStudioRenderChannels.Position | TextStudioRenderChannels.Color;
                case TextStudioBuiltInEffectIds.ChromaticGlitch:
                    return TextStudioRenderChannels.Position;
                case TextStudioBuiltInEffectIds.CornerWarp:
                    return TextStudioRenderChannels.CornerOffsets;
                case TextStudioBuiltInEffectIds.LensMagnifier:
                    return TextStudioRenderChannels.CornerOffsets;
                default:
                    return TextStudioRenderChannels.All;
            }
        }

        static TextStudioReducedMotionBehavior ResolveReducedMotion(
            string id,
            TextStudioRenderChannels channels)
        {
            if (id == TextStudioBuiltInEffectIds.Blink ||
                id == TextStudioBuiltInEffectIds.Glitch ||
                id == TextStudioBuiltInEffectIds.ChromaticGlitch ||
                id == TextStudioBuiltInEffectIds.PixelGrid ||
                id == TextStudioBuiltInEffectIds.Squiggle ||
                id == TextStudioBuiltInEffectIds.Shake)
                return TextStudioReducedMotionBehavior.Disable;
            if ((channels & TextStudioRenderChannels.Vertex) != 0)
                return TextStudioReducedMotionBehavior.Reduce;
            return TextStudioReducedMotionBehavior.Preserve;
        }
    }
}
