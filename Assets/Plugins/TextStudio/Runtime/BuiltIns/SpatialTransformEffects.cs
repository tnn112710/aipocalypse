using UnityEngine;

namespace TextStudio.BuiltIns
{
    static class SpatialTransformEffectMath
    {
        internal static float Transition(in EffectEvaluationContext context)
        {
            return context.phase == AnimationPhase.Persistent
                ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(context.phaseProgress));
        }

        internal static float Hash01(int value, int seed)
        {
            uint hash = unchecked((uint)value * 747796405u + (uint)seed * 2891336453u);
            hash = (hash >> ((int)(hash >> 28) + 4)) ^ hash;
            hash *= 277803737u;
            hash = (hash >> 22) ^ hash;
            return (hash & 0x00ffffffu) / 16777215f;
        }
    }

    internal abstract class SpatialTransformEffectBase : CodeEffectBase
    {
        public override string Category => "Spatial Motion";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Reveal;
    }

    internal sealed class HeroDepthRiseEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.HeroDepthRise;
        public override string DisplayName => "Hero Depth Rise";
        public override string Description => "Letters rise forward from depth with a restrained cinematic tilt.";
        public float strength = 0.75f;
        public float lift = 0.16f;
        public float tilt = 24f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float remaining = 1f - SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.25f : 1f;
            state.positionOffset += new Vector3(0f, -lift, -strength) * (remaining * motionScale);
            state.rotation = Quaternion.Euler(tilt * remaining * motionScale, 0f, 0f) * state.rotation;
        }
    }

    internal sealed class CardFlipYEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.CardFlipY;
        public override string DisplayName => "Card Flip Y";
        public override string Description => "Each glyph turns around its vertical axis like a premium split-flap card.";
        public float angle = 92f;
        public float depth = 0.12f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float remaining = 1f - SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.2f : 1f;
            state.rotation = Quaternion.Euler(0f, angle * remaining * motionScale, 0f) * state.rotation;
            state.positionOffset.z -= Mathf.Sin(remaining * Mathf.PI) * depth * motionScale;
        }
    }

    internal sealed class TopHingeEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.TopHinge;
        public override string DisplayName => "Top Hinge";
        public override string Description => "Letters unfold from their top edge with a convincing hinged motion.";
        public float angle = -88f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float remaining = 1f - SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.22f : 1f;
            state.pivotOffset.y += 0.5f;
            state.rotation = Quaternion.Euler(angle * remaining * motionScale, 0f, 0f) * state.rotation;
        }
    }

    internal sealed class DominoLineEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.DominoLine;
        public override string DisplayName => "Domino Line";
        public override string Description => "A stagger-ready sideways fall and recovery across the text.";
        public float angle = 72f;
        public float depth = 0.08f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float remaining = 1f - SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.2f : 1f;
            state.pivotOffset.x -= 0.5f;
            state.rotation = Quaternion.Euler(0f, 0f, angle * remaining * motionScale) * state.rotation;
            state.positionOffset.z -= Mathf.Sin(remaining * Mathf.PI) * depth * motionScale;
        }
    }

    internal sealed class SpiralAssembleEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.SpiralAssemble;
        public override string DisplayName => "Spiral Assemble";
        public override string Description => "Letters arrive from a three-dimensional spiral and settle into readable text.";
        public float radius = 0.65f;
        public float depth = 0.8f;
        public float turns = 1.25f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float remaining = 1f - SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.22f : 1f;
            float angle = (context.normalizedIndex * turns + remaining) * Mathf.PI * 2f;
            state.positionOffset += new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                -depth) * (remaining * motionScale);
            state.rotation = Quaternion.Euler(0f, remaining * 180f * motionScale, 0f) * state.rotation;
        }
    }

    internal sealed class SpatialScatterEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.SpatialScatter;
        public override string DisplayName => "3D Scatter & Return";
        public override string Description => "Deterministically scatters glyphs through volume, then returns them cleanly.";
        public float strength = 0.85f;
        public int seed = 1729;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float remaining = 1f - SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.18f : 1f;
            Vector3 direction = new Vector3(
                SpatialTransformEffectMath.Hash01(context.unitIndex * 3, seed) * 2f - 1f,
                SpatialTransformEffectMath.Hash01(context.unitIndex * 3 + 1, seed) * 2f - 1f,
                SpatialTransformEffectMath.Hash01(context.unitIndex * 3 + 2, seed) * 2f - 1f);
            if (direction.sqrMagnitude > 0.0001f)
                direction.Normalize();
            state.positionOffset += direction * (strength * remaining * motionScale);
            state.rotation = Quaternion.Euler(direction * (remaining * 95f * motionScale)) * state.rotation;
        }
    }

    internal sealed class FoldAwayEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.FoldAway;
        public override string DisplayName => "Fold Away";
        public override string Description =>
            "Folds letters backward into depth, then fades near the end of the hide transition.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Hide;
        public float angle = 90f;
        public float depth = 0.35f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float progress = SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.2f : 1f;
            state.pivotOffset.y += 0.5f;
            state.rotation = Quaternion.Euler(angle * progress * motionScale, 0f, 0f) * state.rotation;
            state.positionOffset.z -= depth * progress * motionScale;
            float fadeProgress = Mathf.InverseLerp(0.55f, 1f, progress);
            state.alpha *= 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
        }
    }

    internal sealed class RollingRibbonEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.RollingRibbon;
        public override string DisplayName => "Rolling Ribbon";
        public override string Description => "A smooth rolling wave travels through position and Y-axis orientation.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Active;
        public float strength = 0.16f;
        public float angle = 18f;
        public float speed = 0.75f;
        public float spread = 0.8f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            if (reducedMotion) return;
            float wave = Mathf.Sin((context.time * speed - context.normalizedIndex * spread) * Mathf.PI * 2f);
            state.positionOffset += new Vector3(0f, wave * strength, -Mathf.Abs(wave) * strength * 0.55f);
            state.rotation = Quaternion.Euler(0f, wave * angle, wave * angle * 0.35f) * state.rotation;
        }
    }

    internal sealed class SoftSpatialFloatEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.SoftSpatialFloat;
        public override string DisplayName => "Soft Spatial Float";
        public override string Description => "Slow, readable three-axis drift with a reduced-motion-safe still variant.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Active;
        public float strength = 0.075f;
        public float speed = 0.32f;
        public float spread = 0.45f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            if (reducedMotion) return;
            float phase = (context.time * speed + context.normalizedIndex * spread) * Mathf.PI * 2f;
            state.positionOffset += new Vector3(
                Mathf.Sin(phase * 0.73f) * strength * 0.45f,
                Mathf.Sin(phase) * strength,
                Mathf.Cos(phase * 0.61f) * strength * 0.7f);
        }
    }

    internal sealed class AccordionEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Accordion;
        public override string DisplayName => "Accordion";
        public override string Description => "Alternating glyphs compress in width and depth before opening into place.";
        public float compression = 0.72f;
        public float depth = 0.28f;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            float remaining = 1f - SpatialTransformEffectMath.Transition(in context);
            float motionScale = reducedMotion ? 0.2f : 1f;
            float direction = (context.unitIndex & 1) == 0 ? -1f : 1f;
            state.scale.x *= 1f - compression * remaining * motionScale;
            state.positionOffset.z += direction * depth * remaining * motionScale;
            state.rotation = Quaternion.Euler(0f, direction * 38f * remaining * motionScale, 0f) * state.rotation;
        }
    }

    internal sealed class OrbitEffect : SpatialTransformEffectBase
    {
        public override string Id => TextStudioBuiltInEffectIds.Orbit;
        public override string DisplayName => "Orbit";
        public override string Description => "Letters orbit their authored positions while remaining individually editable.";
        public override TextStudioPhaseMask DefaultPhases => TextStudioPhaseMask.Active;
        public float radius = 0.12f;
        public float speed = 0.55f;
        public float spread = 0.85f;
        public bool faceMotion = true;
        public bool reducedMotion;

        public override void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
        {
            if (reducedMotion) return;
            float angle = (context.time * speed + context.normalizedIndex * spread) * Mathf.PI * 2f;
            state.positionOffset += new Vector3(Mathf.Cos(angle), Mathf.Sin(angle) * 0.55f, Mathf.Sin(angle)) * radius;
            if (faceMotion)
                state.rotation = Quaternion.Euler(0f, -Mathf.Cos(angle) * 16f, 0f) * state.rotation;
        }
    }
}
