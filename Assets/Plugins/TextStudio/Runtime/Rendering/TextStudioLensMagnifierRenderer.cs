using TextStudio.BuiltIns;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Immutable terminal resampling request prepared from a renderer-owned magnifier binding.
    /// The target remains in the owning hub's local text space so world and canvas TMP share math.
    /// </summary>
    internal readonly struct TextStudioLensMagnifierDescriptor
    {
        internal Vector2 TargetLocal { get; }
        internal float Radius { get; }
        internal float CenterMagnification { get; }
        internal float EdgeMagnification { get; }
        internal float Bulge { get; }
        internal float Feather { get; }
        internal float MaximumMagnification =>
            Mathf.Max(CenterMagnification, EdgeMagnification);
        internal bool IsActive => Radius > 0.001f && MaximumMagnification > 1.0001f;

        TextStudioLensMagnifierDescriptor(
            Vector2 targetLocal,
            LensMagnifierEffect effect,
            float intensity)
        {
            TargetLocal = targetLocal;
            Radius = Mathf.Max(0.001f, effect.radius);
            float weight = Mathf.Clamp01(intensity);
            float center = Mathf.Clamp(effect.centerMagnification, 1f, 8f);
            float edge = Mathf.Clamp(effect.edgeMagnification, 1f, center);
            CenterMagnification = Mathf.Lerp(1f, center, weight);
            EdgeMagnification = Mathf.Lerp(1f, edge, weight);
            Bulge = Mathf.Clamp01(effect.bulge);
            Feather = Mathf.Clamp(effect.feather, 0f, 0.25f);
        }

        internal static bool CanExecuteExactly(
            in TextStudioRendererEffectExecutionRequest request,
            TextStudioText hub)
        {
            if (hub == null || !hub.HasInfluenceTarget ||
                request.Effect is not LensMagnifierEffect effect ||
                !request.AllGlyphsPersistent ||
                !request.PersistentEffectsEnabled ||
                request.Binding.scope != null ||
                (request.Binding.envelope != null &&
                 !request.Binding.envelope.IsConstantFullEnvelope) ||
                request.Binding.glyphStart != 0 ||
                request.Binding.glyphCount < request.Plan.VisibleGlyphCount ||
                (request.Binding.phaseMask & TextStudioPhaseMask.Active) == 0 ||
                effect.radius <= 0.001f ||
                Mathf.Max(effect.centerMagnification, effect.edgeMagnification) <= 1.0001f)
            {
                return false;
            }

            int lensCount = 0;
            TextStudioReadOnlyBuffer<EffectBinding> bindings =
                request.Plan.EffectBindings;
            for (int index = 0; index < bindings.Length; index++)
            {
                EffectBinding binding = bindings[index];
                if (binding.effect is LensMagnifierEffect ||
                    string.Equals(
                        binding.effectId,
                        TextStudioBuiltInEffectIds.LensMagnifier,
                        System.StringComparison.OrdinalIgnoreCase))
                {
                    lensCount++;
                }
                if (binding.effect is TextStudioCompositionLayer layer &&
                    layer.Kind == TextStudioCompositionLayerKind.Writing)
                {
                    return false;
                }
            }
            return lensCount == 1;
        }

        internal static bool TryPrepare(
            in TextStudioFrame frame,
            TextStudioText hub,
            out TextStudioLensMagnifierDescriptor descriptor)
        {
            descriptor = default;
            if (hub == null || !hub.HasInfluenceTarget ||
                !frame.RendererEffectExecution.IsCreated)
            {
                return false;
            }

            TextStudioReadOnlyBuffer<EffectBinding> bindings =
                frame.Plan.EffectBindings;
            bool found = false;
            for (int index = 0; index < bindings.Length; index++)
            {
                if (!frame.RendererEffectExecution.IsRendererOwned(index))
                    continue;
                EffectBinding binding = bindings[index];
                ITextStudioEffect resolved = binding.effect;
                if (resolved == null &&
                    !TextStudioEffects.TryCreate(
                        binding.effectId,
                        in binding.parameters,
                        out resolved))
                {
                    continue;
                }
                if (resolved is not LensMagnifierEffect effect)
                    continue;
                if (found)
                    return false;

                Vector3 target = hub.transform.InverseTransformPoint(
                    hub.InfluenceTargetWorldPosition);
                descriptor = new TextStudioLensMagnifierDescriptor(
                    target,
                    effect,
                    binding.intensity);
                found = descriptor.IsActive;
            }
            return found;
        }
    }

    /// <summary>Supplies a stable white source-fill program when a lens has no authored Appearance.</summary>
#if UNITY_6000_7_OR_NEWER
    // The immutable fallback asset is shared in Edit Mode too and owns a native object until code unload.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioLensMagnifierHeroStyle
    {
        static TextStudio2DStyleAsset s_source;
        static CompiledTextStudio2DStyle s_compiled;

#if UNITY_6000_7_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ReleaseSource()
        {
            if (s_source != null)
            {
                if (Application.isPlaying)
                    UnityEngine.Object.Destroy(s_source);
                else
                    UnityEngine.Object.DestroyImmediate(s_source);
            }
            s_source = null;
            s_compiled = null;
        }

        internal static CompiledTextStudio2DStyle Resolve(
            CompiledTextStudio2DStyle appearance)
        {
            if (appearance != null && appearance.IsValid && !appearance.IsPassThrough)
                return appearance;
            if (s_compiled != null && s_compiled.IsValid && !s_compiled.IsPassThrough)
                return s_compiled;

            ReleaseSource();
            s_source = ScriptableObject.CreateInstance<TextStudio2DStyleAsset>();
            s_source.name = "Text Studio Lens Source Fill";
            s_source.hideFlags = HideFlags.HideAndDontSave;
            TextStudio2DLayer fill = TextStudio2DLayer.CreateFill(
                    "Source Fill",
                    TextStudio2DPaint.Solid(Color.white))
                .WithIdentity(TextStudioStackItemId.Create(
                    "textstudio.lens.source-fill"))
                .WithExecution(
                    TextStudio2DExecutionPath.HeroCompositor,
                    TextStudio2DQualityTier.Hero);
            s_source.SetLayers(new[] { fill });
            s_compiled = TextStudio2DStyleCompiler.Compile(
                s_source,
                TextStudio2DCompileContext.Full);
            return s_compiled;
        }
    }
}
