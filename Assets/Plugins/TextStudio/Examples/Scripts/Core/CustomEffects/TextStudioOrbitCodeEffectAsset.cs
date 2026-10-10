using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace TextStudio.Examples.CustomEffects
{
    [Preserve]
    public struct TextStudioOrbitCodeEffectState
    {
    }

    [Serializable]
    [Preserve]
    public struct TextStudioOrbitCodeEffectParameters
    {
        [Tooltip("Orbit radius in local text units.")]
        [TextStudioRange(0f, 2f, 0.01f)]
        [TextStudioParameterAlias("r")]
        public float radius;

        [Tooltip("Orbit cycles per second.")]
        [TextStudioRange(0f, 20f, 0.1f)]
        [TextStudioParameterAlias("s")]
        public float speed;

        [Tooltip("Maximum glyph tilt in degrees.")]
        [TextStudioRange(-30f, 30f, 0.5f, "°")]
        [TextStudioParameterAlias("t")]
        public float tilt;
    }

    /// <summary>Asset-backed custom Code Effect route demonstrated by the custom-effect lesson.</summary>
    [Preserve]
    public sealed class TextStudioOrbitCodeEffectAsset :
        TextStudioCodeEffectAsset<TextStudioOrbitCodeEffectState, TextStudioOrbitCodeEffectParameters>
    {
        protected override string DefaultId => "sampleOrbitAsset";
        protected override string DefaultDisplayName => "Orbit Code Effect";
        protected override string DefaultDescription =>
            "Asset-backed orbit primitive with typed defaults, aliases, and ranges.";

        protected override TextStudioOrbitCodeEffectParameters CreateDefaultParameters()
        {
            return new TextStudioOrbitCodeEffectParameters
            {
                radius = 0.14f,
                speed = 1.1f,
                tilt = 7f
            };
        }

        protected override void EvaluateEffect(
            ref EvaluatedGlyphState glyphState,
            ref TextStudioOrbitCodeEffectState effectState,
            in TextStudioOrbitCodeEffectParameters parameters,
            in EffectEvaluationContext context,
            EffectBlendMode blendMode)
        {
            float phase = (context.time * parameters.speed + context.normalizedUnitIndex) *
                          Mathf.PI * 2f;
            float sine = Mathf.Sin(phase);
            Vector3 offset = new Vector3(Mathf.Cos(phase), sine, 0f) * parameters.radius;
            Quaternion rotation = Quaternion.Euler(0f, 0f, sine * parameters.tilt);
            if (blendMode == EffectBlendMode.Override)
            {
                glyphState.positionOffset = offset;
                glyphState.rotation = rotation;
                return;
            }
            glyphState.positionOffset += offset;
            glyphState.rotation = rotation * glyphState.rotation;
        }
    }
}
