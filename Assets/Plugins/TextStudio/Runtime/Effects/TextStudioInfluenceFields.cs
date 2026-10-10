using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioInfluenceFieldShape
    {
        Linear,
        Sphere,
        Box,
        Cylinder,
        Torus,
        TargetDistance,
        Noise
    }

    public enum TextStudioInfluenceFieldSpace
    {
        SourceLayout,
        SourceTextNormalized,
        PlacedText,
        World
    }

    public enum TextStudioInfluenceFieldAxis
    {
        X,
        Y,
        Z
    }

    public enum TextStudioInfluenceFieldCombine
    {
        Multiply,
        Add,
        Maximum,
        Minimum
    }

    /// <summary>One spatial falloff shape that multiplies a composed layer's semantic area influence.</summary>
    [Serializable]
    public sealed class TextStudioInfluenceField
    {
        [SerializeField, Tooltip("Whether this spatial field contributes to the layer.")]
        bool _enabled = true;
        [SerializeField, Tooltip("Spatial shape used to calculate influence.")]
        TextStudioInfluenceFieldShape _shape = TextStudioInfluenceFieldShape.Linear;
        [SerializeField, Tooltip("Choose source layout, placed text, or world coordinates.")]
        TextStudioInfluenceFieldSpace _space = TextStudioInfluenceFieldSpace.PlacedText;
        [SerializeField, Tooltip("How this field combines with earlier fields in the stack.")]
        TextStudioInfluenceFieldCombine _combine = TextStudioInfluenceFieldCombine.Multiply;
        [SerializeField, Tooltip("Primary axis for linear, cylinder, and torus fields.")]
        TextStudioInfluenceFieldAxis _axis = TextStudioInfluenceFieldAxis.X;
        [SerializeField, Tooltip("Center or target position in the selected coordinate space.")]
        Vector3 _center;
        [SerializeField, Tooltip("Full box dimensions. Each component is clamped above zero.")]
        Vector3 _size = Vector3.one * 2f;
        [SerializeField, Min(0.0001f), Tooltip("Radius or directional range in Text Studio units.")]
        float _radius = 2f;
        [SerializeField, Min(0f), Tooltip("Torus ring radius. The Radius setting controls tube falloff.")]
        float _majorRadius = 1f;
        [SerializeField, Min(0.0001f), Tooltip("Frequency of deterministic spatial noise.")]
        float _noiseScale = 1f;
        [SerializeField, Tooltip("Stable seed for deterministic spatial noise.")]
        int _seed = 1729;
        [SerializeField, Range(0f, 1f), Tooltip("Blend between no field influence and the complete field result.")]
        float _strength = 1f;
        [SerializeField, Tooltip("Invert the field after falloff is evaluated.")]
        bool _invert;
        [SerializeField, Tooltip("Remap normalized distance or noise into final influence.")]
        AnimationCurve _falloff = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public bool Enabled => _enabled;
        public TextStudioInfluenceFieldShape Shape => _shape;
        public TextStudioInfluenceFieldSpace Space => _space;
        public TextStudioInfluenceFieldCombine Combine => _combine;
        internal TextStudioInfluenceFieldAxis Axis => _axis;
        internal Vector3 Center => _center;
        internal Vector3 Size => _size;
        internal float Radius => Mathf.Max(0.0001f, _radius);
        internal float MajorRadius => Mathf.Max(0f, _majorRadius);
        internal AnimationCurve Falloff => _falloff;
        public float Strength => Mathf.Clamp01(_strength);
        internal bool RequiresSourceTextNormalizedPosition =>
            _enabled && _space == TextStudioInfluenceFieldSpace.SourceTextNormalized;

        /// <summary>Configures shape, coordinate space, center, size or radius, falloff, and inversion.</summary>
        public void Configure(
            TextStudioInfluenceFieldShape shape,
            TextStudioInfluenceFieldSpace space,
            Vector3 center,
            float radius,
            float strength = 1f,
            bool invert = false,
            AnimationCurve falloff = null,
            Vector3? size = null,
            TextStudioInfluenceFieldAxis? axis = null,
            TextStudioInfluenceFieldCombine? combine = null,
            float? majorRadius = null,
            float? noiseScale = null,
            int? seed = null)
        {
            _enabled = true;
            _shape = shape;
            _space = space;
            _center = center;
            _radius = Mathf.Max(0.0001f, radius);
            _strength = Mathf.Clamp01(strength);
            _invert = invert;
            if (size.HasValue) _size = new Vector3(Mathf.Max(.0001f, size.Value.x), Mathf.Max(.0001f, size.Value.y), Mathf.Max(.0001f, size.Value.z));
            if (axis.HasValue) _axis = axis.Value;
            if (combine.HasValue) _combine = combine.Value;
            if (majorRadius.HasValue) _majorRadius = Mathf.Max(0f, majorRadius.Value);
            if (noiseScale.HasValue) _noiseScale = Mathf.Max(.0001f, noiseScale.Value);
            if (seed.HasValue) _seed = seed.Value;
            if (falloff != null)
            {
                _falloff = new AnimationCurve(falloff.keys)
                {
                    preWrapMode = falloff.preWrapMode,
                    postWrapMode = falloff.postWrapMode
                };
            }
            TextStudioEffectDefinitionAsset.NotifyEffectContentChanged();
        }

        internal float Evaluate(in EffectEvaluationContext context)
        {
            if (!_enabled)
                return 1f;
            Vector3 position = _space switch
            {
                TextStudioInfluenceFieldSpace.SourceLayout => context.sourcePosition,
                TextStudioInfluenceFieldSpace.SourceTextNormalized => context.sourceTextNormalizedPosition,
                TextStudioInfluenceFieldSpace.World => context.worldPosition,
                _ => context.spatialPosition
            };
            Vector3 center = _center;
            if (_shape == TextStudioInfluenceFieldShape.TargetDistance && context.hasInfluenceTarget)
            {
                center = _space switch
                {
                    TextStudioInfluenceFieldSpace.SourceTextNormalized =>
                        context.influenceTargetSourceTextNormalizedPosition,
                    TextStudioInfluenceFieldSpace.World =>
                        context.influenceTargetWorldPosition,
                    _ => context.influenceTargetLocalPosition
                };
            }
            Vector3 local = position - center;
            Vector3 axis = ResolveAxis(_axis);
            float radius = Mathf.Max(0.0001f, _radius);
            float raw = _shape switch
            {
                TextStudioInfluenceFieldShape.Sphere =>
                    1f - local.magnitude / radius,
                TextStudioInfluenceFieldShape.Box =>
                    EvaluateBox(local, _size),
                TextStudioInfluenceFieldShape.Cylinder =>
                    1f - Vector3.ProjectOnPlane(local, axis).magnitude / radius,
                TextStudioInfluenceFieldShape.Torus =>
                    EvaluateTorus(local, axis, Mathf.Max(0f, _majorRadius), radius),
                TextStudioInfluenceFieldShape.TargetDistance =>
                    1f - local.magnitude / radius,
                TextStudioInfluenceFieldShape.Noise =>
                    EvaluateNoise(position, Mathf.Max(0.0001f, _noiseScale), _seed),
                _ => 0.5f + Vector3.Dot(local, axis) / (radius * 2f)
            };
            raw = Mathf.Clamp01(raw);
            float mapped = _falloff != null ? Mathf.Clamp01(_falloff.Evaluate(raw)) : raw;
            if (_invert)
                mapped = 1f - mapped;
            return Mathf.LerpUnclamped(1f, mapped, Strength);
        }

        static Vector3 ResolveAxis(TextStudioInfluenceFieldAxis axis)
        {
            return axis switch
            {
                TextStudioInfluenceFieldAxis.Y => Vector3.up,
                TextStudioInfluenceFieldAxis.Z => Vector3.forward,
                _ => Vector3.right
            };
        }

        static float EvaluateBox(Vector3 local, Vector3 size)
        {
            Vector3 half = new Vector3(
                Mathf.Max(0.0001f, Mathf.Abs(size.x) * 0.5f),
                Mathf.Max(0.0001f, Mathf.Abs(size.y) * 0.5f),
                Mathf.Max(0.0001f, Mathf.Abs(size.z) * 0.5f));
            float normalized = Mathf.Max(
                Mathf.Abs(local.x) / half.x,
                Mathf.Max(Mathf.Abs(local.y) / half.y, Mathf.Abs(local.z) / half.z));
            return 1f - normalized;
        }

        static float EvaluateTorus(Vector3 local, Vector3 axis, float majorRadius, float tubeRadius)
        {
            float axial = Vector3.Dot(local, axis);
            float radial = Vector3.ProjectOnPlane(local, axis).magnitude;
            Vector2 ringDistance = new Vector2(radial - majorRadius, axial);
            return 1f - ringDistance.magnitude / tubeRadius;
        }

        static float EvaluateNoise(Vector3 position, float scale, int seed)
        {
            Vector3 sample = position * scale + new Vector3(
                seed * 0.00137f,
                seed * 0.00211f,
                seed * 0.00317f);
            float xy = Mathf.PerlinNoise(sample.x, sample.y);
            float yz = Mathf.PerlinNoise(sample.y + 19.19f, sample.z + 7.73f);
            float zx = Mathf.PerlinNoise(sample.z + 31.31f, sample.x + 13.13f);
            return (xy + yz + zx) / 3f;
        }
    }

    /// <summary>Ordered spatial influence fields combined into one allocation-free per-glyph weight.</summary>
    [Serializable]
    public sealed class TextStudioInfluenceFieldStack
    {
        [SerializeField] List<TextStudioInfluenceField> _fields = new List<TextStudioInfluenceField>();

        public IReadOnlyList<TextStudioInfluenceField> Fields => _fields;
        public int Count => _fields?.Count ?? 0;
        internal bool HasEnabledFields
        {
            get
            {
                if (_fields == null)
                    return false;
                for (int i = 0; i < _fields.Count; i++)
                {
                    if (_fields[i] != null && _fields[i].Enabled)
                        return true;
                }
                return false;
            }
        }
        internal bool HasEnabledTargetDistanceField
        {
            get
            {
                if (_fields == null)
                    return false;
                for (int i = 0; i < _fields.Count; i++)
                {
                    TextStudioInfluenceField field = _fields[i];
                    if (field != null &&
                        field.Enabled &&
                        field.Shape == TextStudioInfluenceFieldShape.TargetDistance)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        internal bool RequiresSourceTextNormalizedPosition
        {
            get
            {
                if (_fields == null)
                    return false;
                for (int i = 0; i < _fields.Count; i++)
                {
                    if (_fields[i] != null && _fields[i].RequiresSourceTextNormalizedPosition)
                        return true;
                }
                return false;
            }
        }

        internal void AppendEnabledTargetDistanceFields(
            List<TextStudioInfluenceField> output)
        {
            if (_fields == null || output == null)
                return;
            for (int i = 0; i < _fields.Count; i++)
            {
                TextStudioInfluenceField field = _fields[i];
                if (field != null &&
                    field.Enabled &&
                    field.Shape == TextStudioInfluenceFieldShape.TargetDistance)
                {
                    output.Add(field);
                }
            }
        }

        internal void AppendEnabledFields(
            List<TextStudioInfluenceField> output)
        {
            if (_fields == null || output == null)
                return;
            for (int i = 0; i < _fields.Count; i++)
            {
                TextStudioInfluenceField field = _fields[i];
                if (field != null && field.Enabled)
                    output.Add(field);
            }
        }

        /// <summary>Appends a field with the requested shape and returns it for further configuration.</summary>
        public TextStudioInfluenceField Add(TextStudioInfluenceFieldShape shape)
        {
            _fields ??= new List<TextStudioInfluenceField>();
            TextStudioInfluenceField field = new TextStudioInfluenceField();
            field.Configure(shape, TextStudioInfluenceFieldSpace.PlacedText, Vector3.zero, 2f);
            _fields.Add(field);
            return field;
        }

        /// <summary>Removes every influence field from the stack.</summary>
        public void Clear()
        {
            _fields?.Clear();
        }

        internal float Evaluate(in EffectEvaluationContext context)
        {
            if (_fields == null || _fields.Count == 0)
                return 1f;
            float result = 1f;
            bool hasResult = false;
            for (int i = 0; i < _fields.Count; i++)
            {
                TextStudioInfluenceField field = _fields[i];
                if (field == null || !field.Enabled)
                    continue;
                float value = field.Evaluate(in context);
                if (!hasResult)
                {
                    result = value;
                    hasResult = true;
                    continue;
                }
                result = field.Combine switch
                {
                    TextStudioInfluenceFieldCombine.Add => Mathf.Clamp01(result + value - 1f),
                    TextStudioInfluenceFieldCombine.Maximum => Mathf.Max(result, value),
                    TextStudioInfluenceFieldCombine.Minimum => Mathf.Min(result, value),
                    _ => result * value
                };
            }
            return hasResult ? Mathf.Clamp01(result) : 1f;
        }
    }

    public enum TextStudioSourceColorMetric
    {
        Hue,
        Saturation,
        PerceptualLuminance,
        Alpha
    }

    /// <summary>Optional source-color range selector that filters a composed layer by hue, saturation, luminance, or alpha.</summary>
    [Serializable]
    public sealed class TextStudioSourceColorSelector
    {
        [SerializeField, Tooltip("Filter this layer using each glyph's original source color.")]
        bool _enabled;
        [SerializeField, Tooltip("Source-color property used for selection.")]
        TextStudioSourceColorMetric _metric = TextStudioSourceColorMetric.Hue;
        [SerializeField, Tooltip("Target hue. Saturation and brightness are ignored for Hue selection.")]
        Color _targetColor = Color.red;
        [SerializeField, Range(0f, 1f), Tooltip("Target value for saturation, perceptual luminance, or alpha.")]
        float _target = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Distance from the target that remains fully selected.")]
        float _tolerance = 0.08f;
        [SerializeField, Range(0f, 1f), Tooltip("Width of the smooth transition outside the tolerance.")]
        float _softness = 0.12f;
        [SerializeField, Tooltip("Select colors outside the target range.")]
        bool _invert;

        public bool Enabled => _enabled;
        public TextStudioSourceColorMetric Metric => _metric;
        public Color TargetColor => _targetColor;
        public float Target => Mathf.Clamp01(_target);
        public float Tolerance => Mathf.Clamp01(_tolerance);
        public float Softness => Mathf.Clamp01(_softness);
        public bool Invert => _invert;

        /// <summary>Configures color metric, target, range, feathering, and inversion.</summary>
        public void Configure(
            TextStudioSourceColorMetric metric,
            Color targetColor,
            float target,
            float tolerance,
            float softness,
            bool invert = false)
        {
            _enabled = true;
            _metric = metric;
            _targetColor = targetColor;
            _target = Mathf.Clamp01(target);
            _tolerance = Mathf.Clamp01(tolerance);
            _softness = Mathf.Clamp01(softness);
            _invert = invert;
        }

        /// <summary>Disables source-color filtering while retaining its authored values.</summary>
        public void Disable()
        {
            _enabled = false;
        }

        internal float Evaluate(in EffectEvaluationContext context)
        {
            if (!_enabled || !context.hasSourceColor)
                return 1f;

            Color source = context.sourceColor;
            float distance;
            if (_metric == TextStudioSourceColorMetric.Hue)
            {
                Color.RGBToHSV(source, out float sourceHue, out _, out _);
                Color.RGBToHSV(_targetColor, out float targetHue, out _, out _);
                float circularDistance = Mathf.Abs(sourceHue - targetHue);
                distance = Mathf.Min(circularDistance, 1f - circularDistance) * 2f;
            }
            else
            {
                float value = _metric switch
                {
                    TextStudioSourceColorMetric.Saturation => ResolveSaturation(source),
                    TextStudioSourceColorMetric.PerceptualLuminance => ResolvePerceptualLuminance(source),
                    TextStudioSourceColorMetric.Alpha => source.a,
                    _ => 0f
                };
                distance = Mathf.Abs(value - Mathf.Clamp01(_target));
            }

            float tolerance = Mathf.Clamp01(_tolerance);
            float softness = Mathf.Clamp01(_softness);
            float influence;
            if (softness <= 0.0001f)
            {
                influence = distance <= tolerance ? 1f : 0f;
            }
            else
            {
                float transition = Mathf.Clamp01((distance - tolerance) / softness);
                influence = 1f - transition * transition * (3f - 2f * transition);
            }

            return _invert ? 1f - influence : influence;
        }

        static float ResolveSaturation(Color color)
        {
            Color.RGBToHSV(color, out _, out float saturation, out _);
            return saturation;
        }

        static float ResolvePerceptualLuminance(Color color)
        {
            Color linear = color.linear;
            return Mathf.Clamp01(
                linear.r * 0.2126f +
                linear.g * 0.7152f +
                linear.b * 0.0722f);
        }
    }

    [Flags]
    internal enum TextStudioSourceContextRequirements
    {
        None = 0,
        SourceTextNormalizedPosition = 1 << 0,
        SourceColor = 1 << 1,
        SourceLayoutPosition = 1 << 2
    }

    internal sealed class TextStudioEffectScopeChain
    {
        readonly TextStudioEffectScopeChain _parent;
        readonly TextStudioInfluenceFieldStack _spatialInfluence;
        readonly TextStudioSourceColorSelector _sourceColorSelector;

        TextStudioEffectScopeChain(
            TextStudioEffectScopeChain parent,
            TextStudioInfluenceFieldStack spatialInfluence,
            TextStudioSourceColorSelector sourceColorSelector)
        {
            _parent = parent;
            _spatialInfluence = spatialInfluence;
            _sourceColorSelector = sourceColorSelector;

            TextStudioSourceContextRequirements requirements =
                parent?.Requirements ?? TextStudioSourceContextRequirements.None;
            if (spatialInfluence != null && spatialInfluence.HasEnabledFields)
                requirements |= TextStudioSourceContextRequirements.SourceLayoutPosition;
            if (spatialInfluence != null &&
                spatialInfluence.RequiresSourceTextNormalizedPosition)
            {
                requirements |= TextStudioSourceContextRequirements.SourceTextNormalizedPosition;
            }
            if (sourceColorSelector != null && sourceColorSelector.Enabled)
                requirements |= TextStudioSourceContextRequirements.SourceColor;
            Requirements = requirements;
        }

        public TextStudioSourceContextRequirements Requirements { get; }
        public bool IsGlyphDependent => Requirements != TextStudioSourceContextRequirements.None ||
                                        HasAnySpatialInfluence;
        internal bool HasTargetDistanceInfluence =>
            (_spatialInfluence != null &&
             _spatialInfluence.HasEnabledTargetDistanceField) ||
            (_parent != null && _parent.HasTargetDistanceInfluence);

        internal void AppendTargetDistanceFields(
            List<TextStudioInfluenceField> output)
        {
            if (output == null)
                return;
            for (TextStudioEffectScopeChain scope = this;
                 scope != null;
                 scope = scope._parent)
            {
                scope._spatialInfluence?.AppendEnabledTargetDistanceFields(
                    output);
            }
        }

        internal void AppendInfluenceFields(
            List<TextStudioInfluenceField> output)
        {
            if (output == null)
                return;
            for (TextStudioEffectScopeChain scope = this;
                 scope != null;
                 scope = scope._parent)
            {
                scope._spatialInfluence?.AppendEnabledFields(output);
            }
        }

        bool HasAnySpatialInfluence =>
            (_spatialInfluence != null && _spatialInfluence.HasEnabledFields) ||
            (_parent != null && _parent.HasAnySpatialInfluence);

        public static TextStudioEffectScopeChain Append(
            TextStudioEffectScopeChain parent,
            TextStudioInfluenceFieldStack spatialInfluence,
            TextStudioSourceColorSelector sourceColorSelector)
        {
            bool hasSpatial = spatialInfluence != null && spatialInfluence.HasEnabledFields;
            bool hasColor = sourceColorSelector != null && sourceColorSelector.Enabled;
            return hasSpatial || hasColor
                ? new TextStudioEffectScopeChain(parent, spatialInfluence, sourceColorSelector)
                : parent;
        }

        public float Evaluate(in EffectEvaluationContext context)
        {
            float influence = 1f;
            for (TextStudioEffectScopeChain scope = this; scope != null; scope = scope._parent)
            {
                if (scope._spatialInfluence != null)
                    influence *= scope._spatialInfluence.Evaluate(in context);
                if (scope._sourceColorSelector != null)
                    influence *= scope._sourceColorSelector.Evaluate(in context);
                if (influence <= 0f)
                    return 0f;
            }
            return Mathf.Clamp01(influence);
        }
    }
}
