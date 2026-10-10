using UnityEngine;

namespace TextStudio.BuiltIns
{
    [TextStudioEffectAlias("squash stretch")]
    [TextStudioEffectAlias("jelly")]
    internal sealed class ElasticEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Elastic;
        public override string DisplayName => "Elastic";
        public override string Description => "Squash-and-stretch with anticipation, overshoot, and a clean settle.";
        public override TextStudioPhaseMask DefaultPhases =>
            TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide;

        public float strength = 0.32f;
        public float oscillations = 2.25f;
        public float damping = 5.5f;
        public float anticipation = 0.12f;
        public bool preserveArea = true;

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;
        internal override bool ReusesPersistentStateBetweenSteps => true;

        internal override bool HasSamePersistentState(float previousTime, float currentTime)
        {
            return true;
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            if (context.phase == AnimationPhase.Persistent)
                return;

            float progress = context.phase == AnimationPhase.Disappearance
                ? 1f - Mathf.Clamp01(context.phaseProgress)
                : Mathf.Clamp01(context.phaseProgress);

            float envelope = Mathf.Exp(-damping * progress);
            float wave = Mathf.Sin((progress * oscillations - anticipation) * Mathf.PI * 2f) *
                         strength * envelope;
            float vertical = Mathf.Max(0.05f, progress + wave);
            float horizontal = preserveArea
                ? Mathf.Clamp(1f / Mathf.Sqrt(vertical), 0.55f, 1.8f)
                : 1f - wave * 0.45f;
            state.scale = Vector3.Scale(state.scale, new Vector3(horizontal, vertical, 1f));
            state.alpha *= Mathf.Clamp01(progress * 2f);
        }
    }

    [TextStudioEffectAlias("fly")]
    [TextStudioEffectAlias("burst")]
    [TextStudioEffectAlias("explode")]
    internal sealed class Scatter2DEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Scatter2D;
        public override string DisplayName => "Scatter 2D";
        public override string Description => "Seeded directional or radial fly-in and fly-out for readable 2D text.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide;

        public float distance = 0.65f;
        public Vector2 direction = Vector2.up;
        [Range(0f, 360f)] public float coneAngle = 120f;
        public bool radial = true;
        public int seed = 1729;
        public float overshoot = 0.08f;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            if (context.phase == AnimationPhase.Persistent)
                return;

            float progress = Mathf.Clamp01(context.phaseProgress);
            float remaining = context.phase == AnimationPhase.Disappearance ? progress : 1f - progress;
            float random = SpatialTransformEffectMath.Hash01(context.randomSeedIndex, seed);
            float angle;
            if (radial)
            {
                angle = random * Mathf.PI * 2f;
            }
            else
            {
                Vector2 axis = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
                angle = Mathf.Atan2(axis.y, axis.x) + (random - 0.5f) * coneAngle * Mathf.Deg2Rad;
            }

            Vector2 scatterDirection = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float settle = context.phase == AnimationPhase.Appearance
                ? Mathf.Sin(progress * Mathf.PI) * overshoot
                : 0f;
            state.positionOffset += (Vector3)(scatterDirection * distance * (remaining - settle));
        }
    }

    [TextStudioEffectAlias("letter spacing")]
    [TextStudioEffectAlias("title tracking")]
    internal sealed class VisualTrackingEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Tracking;
        public override string DisplayName => "Visual Tracking";
        public override string Description => "Animates apparent character spacing without rebuilding the text layout.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Reveal | TextStudioPhaseMask.Hide;

        public float amount = 0.35f;
        public string anchor = "center";

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float influence = context.phase == AnimationPhase.Disappearance
                ? Mathf.Clamp01(context.phaseProgress)
                : 1f - Mathf.Clamp01(context.phaseProgress);
            float origin = ResolveOrigin(anchor);
            state.positionOffset.x += (context.normalizedIndexInBinding - origin) * amount * influence;
        }

        static float ResolveOrigin(string value)
        {
            if (string.Equals(value, "left", System.StringComparison.OrdinalIgnoreCase)) return 0f;
            if (string.Equals(value, "right", System.StringComparison.OrdinalIgnoreCase)) return 1f;
            return 0.5f;
        }
    }

    [TextStudioEffectAlias("flag")]
    [TextStudioEffectAlias("ripple")]
    [TextStudioEffectAlias("quad warp")]
    internal sealed class CornerWarpEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.CornerWarp;
        public override string DisplayName => "Corner Warp";
        public override string Description => "Flag, ripple, jelly, squeeze, and bulge motion using controlled quad deformation.";

        public string mode = "ripple";
        public float strength = 0.12f;
        public float speed = 0.8f;
        public float spread = 1.1f;

        internal override bool SupportsPersistentFastPath => true;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float envelope = context.phase == AnimationPhase.Persistent
                ? 1f
                : Mathf.Sin(Mathf.Clamp01(context.phaseProgress) * Mathf.PI);
            ApplyWarp(
                ref state,
                context.time,
                context.normalizedIndex,
                envelope);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex)
        {
            ApplyWarp(ref state, time, normalizedIndex, 1f);
        }

        void ApplyWarp(
            ref EvaluatedGlyphState state,
            float time,
            float normalizedIndex,
            float envelope)
        {
            float phase = (time * speed - normalizedIndex * spread) * Mathf.PI * 2f;
            float wave = Mathf.Sin(phase) * strength * envelope;
            TextStudioCornerOffsets offsets;

            if (string.Equals(mode, "squeeze", System.StringComparison.OrdinalIgnoreCase))
            {
                offsets = new TextStudioCornerOffsets(
                    new Vector2(wave, 0f), new Vector2(-wave, 0f),
                    new Vector2(wave, 0f), new Vector2(-wave, 0f));
            }
            else if (string.Equals(mode, "bulge", System.StringComparison.OrdinalIgnoreCase))
            {
                float bulge = Mathf.Abs(wave);
                offsets = new TextStudioCornerOffsets(
                    new Vector2(-bulge, -bulge), new Vector2(-bulge, bulge),
                    new Vector2(bulge, bulge), new Vector2(bulge, -bulge));
            }
            else
            {
                float opposite = Mathf.Sin(phase + Mathf.PI * 0.7f) * strength * envelope;
                offsets = new TextStudioCornerOffsets(
                    new Vector2(0f, -opposite), new Vector2(0f, wave),
                    new Vector2(0f, opposite), new Vector2(0f, -wave));
            }

            state.cornerOffsets = TextStudioCornerOffsets.Add(in state.cornerOffsets, in offsets);
        }
    }

    [TextStudioEffectAlias("magnifying glass")]
    [TextStudioEffectAlias("lens")]
    internal sealed class LensMagnifierEffect : CodeEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.LensMagnifier;
        public override string Category => "Spatial";
        public override string DisplayName => "Lens Magnifier";
        public override string Description =>
            "Target-centered optical magnification with radial spacing, bending, and apparent glyph rotation.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Active;

        [Min(0.001f)] public float radius = 123.5f;
        [Range(1f, 8f)] public float centerMagnification = 2.1f;
        [Range(1f, 8f)] public float edgeMagnification = 1.75f;
        [Range(0f, 1f)] public float bulge = 0.45f;
        [Range(0f, 0.25f)] public float feather = 0.02f;

        public override void Evaluate(
            ref EvaluatedGlyphState state,
            in EffectEvaluationContext context)
        {
            if (!context.hasInfluenceTarget)
                return;

            float unitScale = Mathf.Max(0.0001f, context.effectUnitScale);
            Vector2 target = context.influenceTargetLocalPosition;
            if (!context.hasSourceAreaBounds || !context.sourceAreaBounds.IsValid)
            {
                Vector2 point = context.spatialPosition;
                Vector2 mapped = LensMagnifierEffectMath.MapSourcePoint(
                    point,
                    target,
                    radius,
                    centerMagnification,
                    edgeMagnification,
                    bulge,
                    feather);
                state.positionOffset += (Vector3)((mapped - point) / unitScale);
                return;
            }

            Vector2 placementDelta =
                (Vector2)(context.spatialPosition - context.sourcePosition);
            Vector2 minimum = context.sourceAreaBounds.Minimum + placementDelta;
            Vector2 maximum = context.sourceAreaBounds.Maximum + placementDelta;
            Vector2 bottomLeft = MapCorner(new Vector2(minimum.x, minimum.y));
            Vector2 topLeft = MapCorner(new Vector2(minimum.x, maximum.y));
            Vector2 topRight = MapCorner(new Vector2(maximum.x, maximum.y));
            Vector2 bottomRight = MapCorner(new Vector2(maximum.x, minimum.y));
            TextStudioCornerOffsets offsets = new TextStudioCornerOffsets(
                bottomLeft,
                topLeft,
                topRight,
                bottomRight);
            state.cornerOffsets = TextStudioCornerOffsets.Add(
                in state.cornerOffsets,
                in offsets);

            Vector2 MapCorner(Vector2 point)
            {
                Vector2 mapped = LensMagnifierEffectMath.MapSourcePoint(
                    point,
                    target,
                    radius,
                    centerMagnification,
                    edgeMagnification,
                    bulge,
                    feather);
                return (mapped - point) / unitScale;
            }
        }
    }

    internal static class LensMagnifierEffectMath
    {
        const int InverseIterations = 14;
        const float MinimumRadius = 0.001f;

        internal static Vector2 MapSourcePoint(
            Vector2 source,
            Vector2 target,
            float radius,
            float centerMagnification,
            float edgeMagnification,
            float bulge,
            float feather)
        {
            float safeRadius = Mathf.Max(MinimumRadius, radius);
            float safeCenter = Mathf.Clamp(centerMagnification, 1f, 8f);
            float safeEdge = Mathf.Clamp(edgeMagnification, 1f, safeCenter);
            Vector2 delta = source - target;
            float sourceDistance = delta.magnitude;
            float maximumSampledDistance = safeRadius / safeEdge;
            if (sourceDistance > maximumSampledDistance || sourceDistance <= 0.000001f)
                return sourceDistance <= 0.000001f ? target : source;

            float lower = 0f;
            float upper = safeRadius;
            for (int iteration = 0; iteration < InverseIterations; iteration++)
            {
                float destinationDistance = (lower + upper) * 0.5f;
                float magnification = ResolveMagnification(
                    destinationDistance / safeRadius,
                    safeCenter,
                    safeEdge,
                    bulge);
                float sampledDistance = destinationDistance / magnification;
                if (sampledDistance < sourceDistance)
                    lower = destinationDistance;
                else
                    upper = destinationDistance;
            }

            float resolvedDistance = (lower + upper) * 0.5f;
            Vector2 mapped = target + delta * (resolvedDistance / sourceDistance);
            float aperture = ResolveAperture(
                resolvedDistance / safeRadius,
                feather);
            return Vector2.LerpUnclamped(source, mapped, aperture);
        }

        internal static float ResolveMagnification(
            float normalizedDestinationRadius,
            float centerMagnification,
            float edgeMagnification,
            float bulge)
        {
            float t = SmoothStep01(normalizedDestinationRadius);
            float exponent = Mathf.Lerp(0.5f, 3f, Mathf.Clamp01(bulge));
            float profile = Mathf.Pow(t, exponent);
            return Mathf.Lerp(
                Mathf.Clamp(centerMagnification, 1f, 8f),
                Mathf.Clamp(edgeMagnification, 1f, 8f),
                profile);
        }

        internal static float ResolveAperture(float normalizedRadius, float feather)
        {
            float safeFeather = Mathf.Clamp(feather, 0f, 0.25f);
            if (safeFeather <= 0.000001f)
                return normalizedRadius <= 1f ? 1f : 0f;
            return 1f - Mathf.SmoothStep(
                1f - safeFeather,
                1f,
                Mathf.Clamp01(normalizedRadius));
        }

        static float SmoothStep01(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * (3f - 2f * t);
        }
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    static class TextStudio2DMaterialProperties
    {
        internal const string Focus = "textstudio.2d.focus";
        internal const string Dissolve = "textstudio.2d.dissolve";
        internal const string OutlineGlow = "textstudio.2d.outlineGlow";
        internal const string SurfaceSweep = "textstudio.2d.surfaceSweep";
        internal const string ChromaticSplit = "textstudio.2d.chromaticSplit";
        internal const string PixelGrid = "textstudio.2d.pixelGrid";
        internal const string Squiggle = "textstudio.2d.squiggle";

        static bool s_registered;

        internal static void EnsureRegistered()
        {
            if (s_registered &&
                TextStudioRenderPropertyRegistry.TryGet(Focus, out _) &&
                TextStudioRenderPropertyRegistry.TryGet(Squiggle, out _))
            {
                return;
            }

            bool registered = true;
            registered &= RegisterFloat(
                Focus,
                TextStudioBuiltInEffectIds.Focus,
                "Focus",
                "Blur/Focus amount.",
                0f,
                1f);
            registered &= RegisterVector2(
                Dissolve,
                TextStudioBuiltInEffectIds.Dissolve,
                "Dissolve",
                "Threshold and edge softness.",
                Vector2.zero,
                Vector2.one);
            registered &= RegisterVector2(
                OutlineGlow,
                TextStudioBuiltInEffectIds.OutlineGlow,
                "Outline & Glow",
                "Outline width and glow strength.",
                Vector2.zero,
                Vector2.one);
            registered &= RegisterVector4(
                SurfaceSweep,
                TextStudioBuiltInEffectIds.SurfaceSweep,
                "Surface Sweep",
                "Direction, projected center, half-width, and brightness for one source-area sweep.",
                Vector4.zero,
                new Vector4(-10000f, -10000f, -10000f, 0f),
                new Vector4(10000f, 10000f, 10000f, 10000f));
            registered &= RegisterFloat(
                ChromaticSplit,
                TextStudioBuiltInEffectIds.ChromaticGlitch,
                "Chromatic Split",
                "Horizontal RGB separation.",
                -1f,
                1f);
            registered &= RegisterVector4(
                PixelGrid,
                TextStudioBuiltInEffectIds.PixelGrid,
                "Pixel Grid",
                "Cell size, amount, coordinate space, and sampling quality.",
                new Vector4(8f, 0f, 0f, 0f),
                new Vector4(1f, 0f, 0f, 0f),
                new Vector4(64f, 1f, 1f, 1f));
            registered &= RegisterVector4(
                Squiggle,
                TextStudioBuiltInEffectIds.Squiggle,
                "Squiggle",
                "Local contour strength, noise detail and deterministic redraw seed.",
                Vector4.zero,
                Vector4.zero,
                new Vector4(.08f, 12f, 65535f, 0f));
            s_registered = registered;
        }

        internal static void ResetForRuntimeSession()
        {
            s_registered = false;
        }

        static bool RegisterFloat(
            string id,
            string effectId,
            string name,
            string description,
            float minimum,
            float maximum)
        {
            ResolvePrimitiveMetadata(
                effectId,
                out TextStudioReducedMotionBehavior reducedMotionBehavior,
                out TextStudioEffectBoundsBehavior boundsBehavior);
            TextStudioRenderPropertyDescriptor descriptor = new TextStudioRenderPropertyDescriptor(
                id, name, "2D Material", description, TextStudioRenderPropertyValueType.Float,
                TextStudioRenderPropertyStage.Material, TextStudioRenderPropertyPerformanceClass.VertexStream,
                Vector4.zero, new Vector4(minimum, 0f, 0f, 0f), new Vector4(maximum, 0f, 0f, 0f),
                supportedRenderDomains: TextStudioRenderDomain.FlatText,
                reducedMotionBehavior: reducedMotionBehavior,
                boundsBehavior: boundsBehavior);
            return TextStudioRenderPropertyRegistry.RegisterBuiltIn(in descriptor);
        }

        static bool RegisterVector2(
            string id,
            string effectId,
            string name,
            string description,
            Vector2 minimum,
            Vector2 maximum)
        {
            ResolvePrimitiveMetadata(
                effectId,
                out TextStudioReducedMotionBehavior reducedMotionBehavior,
                out TextStudioEffectBoundsBehavior boundsBehavior);
            TextStudioRenderPropertyDescriptor descriptor = new TextStudioRenderPropertyDescriptor(
                id, name, "2D Material", description, TextStudioRenderPropertyValueType.Vector2,
                TextStudioRenderPropertyStage.Material, TextStudioRenderPropertyPerformanceClass.VertexStream,
                Vector4.zero,
                new Vector4(minimum.x, minimum.y, 0f, 0f),
                new Vector4(maximum.x, maximum.y, 0f, 0f),
                supportedRenderDomains: TextStudioRenderDomain.FlatText,
                reducedMotionBehavior: reducedMotionBehavior,
                boundsBehavior: boundsBehavior);
            return TextStudioRenderPropertyRegistry.RegisterBuiltIn(in descriptor);
        }

        static bool RegisterVector4(
            string id,
            string effectId,
            string name,
            string description,
            Vector4 defaultValue,
            Vector4 minimum,
            Vector4 maximum)
        {
            ResolvePrimitiveMetadata(
                effectId,
                out TextStudioReducedMotionBehavior reducedMotionBehavior,
                out TextStudioEffectBoundsBehavior boundsBehavior);
            TextStudioRenderPropertyDescriptor descriptor = new TextStudioRenderPropertyDescriptor(
                id, name, "2D Material", description, TextStudioRenderPropertyValueType.Vector4,
                TextStudioRenderPropertyStage.Material, TextStudioRenderPropertyPerformanceClass.VertexStream,
                defaultValue, minimum, maximum,
                supportedRenderDomains: TextStudioRenderDomain.FlatText,
                reducedMotionBehavior: reducedMotionBehavior,
                boundsBehavior: boundsBehavior);
            return TextStudioRenderPropertyRegistry.RegisterBuiltIn(in descriptor);
        }

        static void ResolvePrimitiveMetadata(
            string effectId,
            out TextStudioReducedMotionBehavior reducedMotionBehavior,
            out TextStudioEffectBoundsBehavior boundsBehavior)
        {
            if (TextStudioVisualPrimitiveCatalog.TryGet(
                    effectId,
                    out TextStudioVisualPrimitiveContract contract))
            {
                reducedMotionBehavior = contract.ReducedMotionBehavior;
                boundsBehavior = contract.BoundsMode ==
                    TextStudioVisualPrimitiveBoundsMode.None
                        ? TextStudioEffectBoundsBehavior.Unchanged
                        : TextStudioEffectBoundsBehavior.DynamicOutput;
                return;
            }

            reducedMotionBehavior = TextStudioReducedMotionBehavior.Reduce;
            boundsBehavior = TextStudioEffectBoundsBehavior.Unchanged;
        }
    }

    internal abstract class TextStudio2DMaterialEffectBase : CodeEffectBase
    {
        protected TextStudio2DMaterialEffectBase()
        {
            TextStudio2DMaterialProperties.EnsureRegistered();
        }

        public override string Category => "2D Material";

        internal override bool WritesPersistentRenderProperties => true;
    }

    internal sealed class FocusEffect : TextStudio2DMaterialEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Focus;
        public override string DisplayName => "Focus";
        public override string Description => "Animates text between defocused and sharp states.";
        public float amount = 1f;

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            renderProperties.Apply(
                TextStudio2DMaterialProperties.Focus,
                new Vector4(Mathf.Clamp01((1f - phaseProgress) * amount), 0f, 0f, 0f));
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float value = context.phase == AnimationPhase.Disappearance
                ? context.phaseProgress
                : 1f - context.phaseProgress;
            context.RenderProperties.Apply(
                TextStudio2DMaterialProperties.Focus,
                new Vector4(Mathf.Clamp01(value * amount), 0f, 0f, 0f));
        }
    }

    internal sealed class DissolveEffect : TextStudio2DMaterialEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Dissolve;
        public override string DisplayName => "Dissolve";
        public override string Description => "Soft directional or noise-threshold dissolve for reveal and hide.";
        public float softness = 0.12f;
        public string space = "glyph";

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            renderProperties.Apply(
                TextStudio2DMaterialProperties.Dissolve,
                new Vector4(
                    EncodeThreshold(1f - phaseProgress, space),
                    Mathf.Max(0.001f, softness),
                    0f,
                    0f));
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float threshold = context.phase == AnimationPhase.Disappearance
                ? context.phaseProgress
                : 1f - context.phaseProgress;
            context.RenderProperties.Apply(
                TextStudio2DMaterialProperties.Dissolve,
                new Vector4(
                    EncodeThreshold(Mathf.Clamp01(threshold), space),
                    Mathf.Max(0.001f, softness),
                    0f,
                    0f));
        }

        internal static float EncodeThreshold(float threshold, string space)
        {
            float value = Mathf.Clamp01(threshold);
            if (value <= 0f)
                return 0f;
            return string.Equals(space, "text", System.StringComparison.OrdinalIgnoreCase)
                ? -value
                : value;
        }
    }

    [TextStudioEffectAlias("burn away")]
    [TextStudioEffectAlias("embers")]
    [TextStudioEffectAlias("char burn")]
    internal sealed class BurnEffect : TextStudio2DMaterialEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Burn;
        public override string DisplayName => "Burn";
        public override string Description => "Procedural hot-edge and ember burn across the complete text or each glyph.";
        public float softness = 0.16f;
        public string space = "text";

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            Apply(in renderProperties, 1f - phaseProgress);
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float threshold = context.phase == AnimationPhase.Disappearance
                ? context.phaseProgress
                : 1f - context.phaseProgress;
            TextStudioRenderPropertyWriter renderProperties = context.RenderProperties;
            Apply(in renderProperties, threshold);
        }

        void Apply(
            in TextStudioRenderPropertyWriter renderProperties,
            float threshold)
        {
            renderProperties.Apply(
                TextStudio2DMaterialProperties.Dissolve,
                new Vector4(
                    DissolveEffect.EncodeThreshold(threshold, space),
                    -Mathf.Max(0.001f, softness),
                    0f,
                    0f));
        }
    }

    internal sealed class OutlineGlowEffect : TextStudio2DMaterialEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.OutlineGlow;
        public override string DisplayName => "Outline & Glow";
        public override string Description => "Animates SDF outline width and glow strength for emphasis.";
        public float outline = 0.25f;
        public float glow = 0.65f;
        public float speed = 1f;
        public float floor;

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            float pulse = EvaluatePulse(time);
            renderProperties.Apply(
                TextStudio2DMaterialProperties.OutlineGlow,
                new Vector4(outline * pulse, glow * pulse, 0f, 0f));
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float pulse = EvaluatePulse(context.time);
            context.RenderProperties.Apply(
                TextStudio2DMaterialProperties.OutlineGlow,
                new Vector4(outline * pulse, glow * pulse, 0f, 0f));
        }

        float EvaluatePulse(float time)
        {
            if (Mathf.Approximately(speed, 0f))
                return 1f;
            float wave = (Mathf.Sin(time * speed * Mathf.PI * 2f) + 1f) * 0.5f;
            return Mathf.Lerp(Mathf.Clamp01(floor), 1f, wave);
        }
    }

    internal sealed class SurfaceSweepEffect : TextStudio2DMaterialEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.SurfaceSweep;
        public override string DisplayName => "Surface Sweep";
        public override string Description => "An angled material highlight band that travels continuously across the selected text area.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Active;
        public override TextStudioEffectAreaSettings DefaultArea => TextStudioEffectAreaSettings.Range;
        public override TextStudioEffectPlaybackRole PlaybackRole => TextStudioEffectPlaybackRole.Progress;
        public override TextStudioEffectPlaybackPreset DefaultPlayback => TextStudioEffectPlaybackPreset.Loop;
        public override TextStudioEffectCurvePreset DefaultCurve => TextStudioEffectCurvePreset.Linear;
        public override float DefaultPlaybackDuration => 1f / Mathf.Max(0.01f, speed);
        public override float DefaultDelayBetweenCycles => BuiltInProgressTiming.DefaultDelayBetweenCycles;

        public float angle = 20f;
        public float speed = 0.55f;
        public float width = 0.15f;
        public float brightness = 1f;
        public string playback = "loop";
        public string curve = "linear";

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float strength = Mathf.Max(0f, brightness);
            if (strength <= 0f)
                return;

            float radians = angle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
            TextStudioGlyphVisualBounds bounds = context.hasSourceAreaBounds
                ? context.sourceAreaBounds
                : new TextStudioGlyphVisualBounds(
                    new Vector2(context.sourcePosition.x, context.sourcePosition.y),
                    new Vector2(context.sourcePosition.x, context.sourcePosition.y),
                    true);
            ProjectBounds(in bounds, direction, out float minimum, out float maximum);
            float span = Mathf.Max(0.0001f, maximum - minimum);
            float halfWidth = Mathf.Max(0.0001f, width * span);
            float progress = BuiltInProgressTiming.Evaluate(
                in context,
                speed,
                playback,
                curve);
            float center = Mathf.LerpUnclamped(
                minimum - halfWidth,
                maximum + halfWidth,
                progress);
            context.RenderProperties.Apply(
                TextStudio2DMaterialProperties.SurfaceSweep,
                new Vector4(direction.x, direction.y, center, halfWidth) * strength);
        }

        static void ProjectBounds(
            in TextStudioGlyphVisualBounds bounds,
            Vector2 direction,
            out float minimum,
            out float maximum)
        {
            Vector2 min = bounds.Minimum;
            Vector2 max = bounds.Maximum;
            float a = Vector2.Dot(new Vector2(min.x, min.y), direction);
            float b = Vector2.Dot(new Vector2(min.x, max.y), direction);
            float c = Vector2.Dot(new Vector2(max.x, min.y), direction);
            float d = Vector2.Dot(new Vector2(max.x, max.y), direction);
            minimum = Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
            maximum = Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
        }
    }

    internal sealed class ChromaticGlitchEffect : TextStudio2DMaterialEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.ChromaticGlitch;
        public override string DisplayName => "Chromatic Glitch";
        public override string Description => "Seeded RGB separation paired with restrained horizontal slicing.";
        public float strength = 0.08f;
        public float frequency = 7f;
        public int seed = 4813;
        [Range(0f, 1f)] public float activity = 0.18f;

        internal override bool SupportsPersistentFastPath => true;
        internal override bool ReusesPersistentStateBetweenSteps => true;

        internal override bool HasSamePersistentState(float previousTime, float currentTime)
        {
            return Mathf.FloorToInt(previousTime * frequency) ==
                   Mathf.FloorToInt(currentTime * frequency);
        }

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            ApplyPersistent(ref state, in renderProperties, time, unitIndex);
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            TextStudioRenderPropertyWriter renderProperties = context.RenderProperties;
            ApplyPersistent(
                ref state,
                in renderProperties,
                context.time,
                context.randomSeedIndex);
        }

        void ApplyPersistent(
            ref EvaluatedGlyphState state,
            in TextStudioRenderPropertyWriter renderProperties,
            float time,
            int randomSeedIndex)
        {
            float noise = SpatialTransformEffectMath.Hash01(
                randomSeedIndex + Mathf.FloorToInt(time * frequency),
                seed);
            float signed = noise * 2f - 1f;
            renderProperties.Apply(
                TextStudio2DMaterialProperties.ChromaticSplit,
                new Vector4(signed * strength, 0f, 0f, 0f));
            if (noise > 1f - Mathf.Clamp01(activity))
                state.positionOffset.x += signed * strength;
        }
    }

    internal sealed class SquiggleEffect : TextStudio2DMaterialEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Squiggle;
        public override string DisplayName => "Squiggle";
        public override string Description => "Stepped hand-drawn contour distortion inside flat TMP glyphs.";
        public float strength = .035f;
        public float detail = 4f;
        public float rate = 6f;
        public float seed = 17f;

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;

        internal Vector4 ResolveSample(float time)
        {
            float frame = Mathf.Floor(Mathf.Max(0f, time) * Mathf.Clamp(rate, 0f, 12f));
            float noiseSeed = Mathf.Repeat(Mathf.Round(seed) + frame * 97f, 65536f);
            return new Vector4(Mathf.Clamp(strength, 0f, .08f), Mathf.Clamp(Mathf.Round(detail), 1f, 12f), noiseSeed, 0f);
        }

        internal override void EvaluatePersistentFast(ref EvaluatedGlyphState state, float time, float playbackTime, float phaseProgress, int unitIndex, float normalizedIndex, in TextStudioRenderPropertyWriter renderProperties)
        {
            renderProperties.Apply(TextStudio2DMaterialProperties.Squiggle, ResolveSample(time));
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            context.RenderProperties.Apply(TextStudio2DMaterialProperties.Squiggle, ResolveSample(context.time));
        }
    }

    [TextStudioEffectAlias("pixelate", "True SDF cell pixelation.")]
    [TextStudioEffectAlias("retro", "Low-resolution digital text.")]
    [TextStudioEffectAlias("low resolution")]
    internal sealed class PixelGridEffect : TextStudio2DMaterialEffectBase
    {
        const float MinimumCellSize = 1f;
        const float MaximumCellSize = 64f;
        const float MaximumPulseSpeed = 20f;

        public override string Id => TextStudioBuiltInEffectIds.PixelGrid;
        public override string DisplayName => "Pixel Grid";
        public override string Description =>
            "True SDF pixelation with screen-stable or glyph-anchored cells and coverage-preserving sampling.";

        public float cellSize = 8f;
        public float activeAmount = 1f;
        public float transitionAmount = 1f;
        public string space = "screen";
        public string quality = "preserve";
        public float pulseSpeed;
        public float minCellSize = 4f;
        public float maxCellSize = 10f;

        internal override bool SupportsPersistentFastPath => true;
        internal override bool SupportsUniformPersistentState => true;

        internal override void EvaluatePersistentFast(
            ref EvaluatedGlyphState state,
            float time,
            float playbackTime,
            float phaseProgress,
            int unitIndex,
            float normalizedIndex,
            in TextStudioRenderPropertyWriter renderProperties)
        {
            ApplyPersistent(in renderProperties, time);
        }

        public override ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters)
        {
            // Unity hot reload preserves existing static prototype field values. Reassert the
            // current default before returning or cloning the registered built-in definition.
            activeAmount = 1f;
            return base.CreateEffect(in parameters);
        }

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float restingSize = Mathf.Clamp(cellSize, MinimumCellSize, MaximumCellSize);
            float minimum = Mathf.Clamp(minCellSize, MinimumCellSize, MaximumCellSize);
            float maximum = Mathf.Clamp(maxCellSize, MinimumCellSize, MaximumCellSize);
            if (minimum > maximum)
                (minimum, maximum) = (maximum, minimum);

            float active = Mathf.Clamp01(activeAmount);
            float transition = Mathf.Clamp01(transitionAmount);
            float progress = Mathf.Clamp01(context.phaseProgress);
            float eased = progress * progress * (3f - 2f * progress);
            float amount;
            float resolvedSize;

            switch (context.phase)
            {
                case AnimationPhase.Appearance:
                    amount = Mathf.LerpUnclamped(transition, active, eased);
                    resolvedSize = Mathf.LerpUnclamped(maximum, restingSize, eased);
                    break;
                case AnimationPhase.Disappearance:
                    amount = Mathf.LerpUnclamped(active, transition, eased);
                    resolvedSize = Mathf.LerpUnclamped(restingSize, maximum, eased);
                    break;
                default:
                    TextStudioRenderPropertyWriter renderProperties =
                        context.RenderProperties;
                    ApplyPersistent(in renderProperties, context.time);
                    return;
            }

            float spaceCode = string.Equals(space, "glyph", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
            float qualityCode = string.Equals(quality, "crisp", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
            context.RenderProperties.Apply(
                TextStudio2DMaterialProperties.PixelGrid,
                new Vector4(resolvedSize, amount, spaceCode, qualityCode));
        }

        void ApplyPersistent(
            in TextStudioRenderPropertyWriter renderProperties,
            float time)
        {
            float restingSize = Mathf.Clamp(cellSize, MinimumCellSize, MaximumCellSize);
            float amount = Mathf.Clamp01(activeAmount);
            float minimum = Mathf.Clamp(minCellSize, MinimumCellSize, MaximumCellSize);
            float maximum = Mathf.Clamp(maxCellSize, MinimumCellSize, MaximumCellSize);
            if (minimum > maximum)
                (minimum, maximum) = (maximum, minimum);

            float speed = Mathf.Clamp(pulseSpeed, 0f, MaximumPulseSpeed);
            float resolvedSize = speed <= 0f || amount <= 0f || Mathf.Approximately(minimum, maximum)
                ? restingSize
                : Mathf.LerpUnclamped(
                    minimum,
                    maximum,
                    (Mathf.Sin(time * speed * Mathf.PI * 2f) + 1f) * 0.5f);
            float spaceCode = string.Equals(space, "glyph", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
            float qualityCode = string.Equals(quality, "crisp", System.StringComparison.OrdinalIgnoreCase) ? 1f : 0f;
            renderProperties.Apply(
                TextStudio2DMaterialProperties.PixelGrid,
                new Vector4(resolvedSize, amount, spaceCode, qualityCode));
        }
    }
}
