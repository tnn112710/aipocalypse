using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace TextStudio.Examples.CustomEffects
{
    [Preserve]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class TextStudioOrbitEffectSampleFactory : ITextStudioEffectFactory,
        ITextStudioEffectMetadataProvider,
        ITextStudioEffectCapabilitiesProvider
    {
        public const string EffectId = "sampleOrbit";

        const float DefaultRadius = 0.14f;
        const float DefaultSpeed = 1.1f;
        const float DefaultTilt = 7f;

        static readonly TextStudioEffectMetadata s_metadata = new TextStudioEffectMetadata(
            TextStudioTagCategories.Effects,
            "Runtime Orbit",
            "Runtime-registered orbit effect used by the custom code lesson.",
            "<sampleOrbit radius=0.14 speed=1.1 tilt=7>",
            TextStudioPhaseMask.Active,
            new[]
            {
                new TextStudioTagParameterDefinition(
                    "radius",
                    TextStudioTagParameterKind.Float,
                    displayName: "Radius",
                    description: "Orbit radius in local text units.",
                    aliases: new[] { "r" },
                    defaultValue: "0.14",
                    minimum: 0f,
                    maximum: 2f),
                new TextStudioTagParameterDefinition(
                    "speed",
                    TextStudioTagParameterKind.Float,
                    displayName: "Speed",
                    description: "Orbit cycles per second.",
                    aliases: new[] { "s" },
                    defaultValue: "1.1",
                    minimum: 0f,
                    maximum: 20f),
                new TextStudioTagParameterDefinition(
                    "tilt",
                    TextStudioTagParameterKind.Float,
                    displayName: "Tilt",
                    description: "Maximum glyph tilt in degrees.",
                    aliases: new[] { "t" },
                    defaultValue: "7",
                    minimum: -30f,
                    maximum: 30f)
            });

        public string Id => EffectId;
        public TextStudioEffectAreaSettings DefaultArea => TextStudioEffectAreaSettings.Default;
        public TextStudioEffectMetadata Metadata => s_metadata;
        public TextStudioEffectCapabilities Capabilities => new TextStudioEffectCapabilities(
            TextStudioRenderChannels.Position | TextStudioRenderChannels.Rotation,
            TextStudioPhaseMask.All);

        public ITextStudioEffect CreateEffect(in TextStudioTagParameters parameters)
        {
            float radius = DefaultRadius;
            float speed = DefaultSpeed;
            float tilt = DefaultTilt;
            if (parameters.TryResolveFloat(radius, "radius", out float resolvedRadius, "r"))
                radius = Mathf.Max(0f, resolvedRadius);
            if (parameters.TryResolveFloat(speed, "speed", out float resolvedSpeed, "s"))
                speed = Mathf.Max(0f, resolvedSpeed);
            if (parameters.TryResolveFloat(tilt, "tilt", out float resolvedTilt, "t"))
                tilt = Mathf.Clamp(resolvedTilt, -30f, 30f);
            return new Evaluator(radius, speed, tilt);
        }

        [Preserve]
        sealed class Evaluator : ITextStudioEffect, ITextStudioEffectCapabilitiesProvider
        {
            readonly float _radius;
            readonly float _speed;
            readonly float _tilt;

            public string Id => EffectId;
            public TextStudioEffectAreaSettings DefaultArea => TextStudioEffectAreaSettings.Default;
            public TextStudioEffectCapabilities Capabilities => new TextStudioEffectCapabilities(
                TextStudioRenderChannels.Position | TextStudioRenderChannels.Rotation,
                TextStudioPhaseMask.All);

            internal Evaluator(float radius, float speed, float tilt)
            {
                _radius = radius;
                _speed = speed;
                _tilt = tilt;
            }

            public void Reset()
            {
            }

            public void Evaluate(ref EvaluatedGlyphState state, in EffectEvaluationContext context)
            {
                float phase = (context.time * _speed + context.normalizedUnitIndex) * Mathf.PI * 2f;
                float sine = Mathf.Sin(phase);
                state.positionOffset += new Vector3(Mathf.Cos(phase), sine, 0f) * _radius;
                state.rotation = Quaternion.Euler(0f, 0f, sine * _tilt) * state.rotation;
            }
        }
    }

    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    [Preserve]
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed partial class TextStudioCustomEffectSampleHost : MonoBehaviour
    {
        // These registrations belong to live ExecuteAlways owners across editor/play transitions.
        static readonly TextStudioOrbitEffectSampleFactory s_factory = new TextStudioOrbitEffectSampleFactory();
        static readonly Dictionary<ITextStudioEffectDefinition, int> s_owners = new Dictionary<ITextStudioEffectDefinition, int>();
        [SerializeField] TextStudioEffectDefinitionAsset[] _effectAssets =
            Array.Empty<TextStudioEffectDefinitionAsset>();

        bool _registered;

        public void Configure(TextStudioEffectDefinitionAsset[] effectAssets)
        {
            if (_registered)
                UnregisterAssetEffects();

            _effectAssets = effectAssets ?? Array.Empty<TextStudioEffectDefinitionAsset>();

            if (_registered)
                RegisterAssetEffects();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RestoreRegistrations()
        {
            foreach (ITextStudioEffectDefinition definition in s_owners.Keys) TextStudioEffects.Register(definition);
        }

        static void Acquire(ITextStudioEffectDefinition definition)
        {
            if (s_owners.TryGetValue(definition, out int count)) { s_owners[definition] = count + 1; return; }
            s_owners.Add(definition, 1); TextStudioEffects.Register(definition);
        }

        static void Release(ITextStudioEffectDefinition definition)
        {
            if (!s_owners.TryGetValue(definition, out int count)) return;
            if (count > 1) { s_owners[definition] = count - 1; return; }
            s_owners.Remove(definition);
            if (TextStudioEffects.TryGetDefinition(definition.Id, out ITextStudioEffectDefinition current) && ReferenceEquals(current, definition))
                TextStudioEffects.Unregister(definition.Id);
        }

        void OnEnable()
        {
            if (_registered) return;
            Acquire(s_factory);
            _registered = true;
            RegisterAssetEffects();
        }

        void OnDisable()
        {
            if (!_registered)
                return;

            UnregisterAssetEffects();
            Release(s_factory);
            _registered = false;
        }

        void RegisterAssetEffects()
        {
            for (int i = 0; i < _effectAssets.Length; i++)
            {
                TextStudioEffectDefinitionAsset effect = _effectAssets[i];
                if (effect != null)
                    Acquire(effect);
            }
        }

        void UnregisterAssetEffects()
        {
            for (int i = _effectAssets.Length - 1; i >= 0; i--)
            {
                TextStudioEffectDefinitionAsset effect = _effectAssets[i];
                if (effect != null)
                    Release(effect);
            }
        }
    }
}
