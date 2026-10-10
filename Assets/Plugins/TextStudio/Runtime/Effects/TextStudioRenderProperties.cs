using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioRenderPropertyValueType : byte
    {
        Float = 0,
        Vector2 = 1,
        Vector3 = 2,
        Vector4 = 3,
        Color = 4,
        UInt = 5
    }

    public enum TextStudioRenderPropertyStage : byte
    {
        Glyph = 0,
        Vertex = 1,
        Material = 2,
        Geometry = 3,
        Topology = 4,
        Simulation = 5
    }

    public enum TextStudioRenderPropertyPerformanceClass : byte
    {
        Realtime = 0,
        VertexStream = 1,
        GeometryRefresh = 2,
        PreparedDeformation = 3,
        Simulation = 4
    }

    [Flags]
    public enum TextStudioRenderPropertyBlendModes : byte
    {
        None = 0,
        Additive = 1 << 0,
        Multiply = 1 << 1,
        Override = 1 << 2,
        Lerp = 1 << 3,
        All = Additive | Multiply | Override | Lerp
    }

    internal enum TextStudioRenderPropertyStorageMode : byte
    {
        None = 0,
        Uniform = 1,
        Ranges = 2,
        Glyphs = 3
    }

    /// <summary>Stable serialized identifier for a renderer property.</summary>
    public readonly struct TextStudioRenderPropertyId : IEquatable<TextStudioRenderPropertyId>
    {
        readonly string _value;

        /// <summary>Creates a render-property identifier, normalizing a null string to an empty identifier.</summary>
        public TextStudioRenderPropertyId(string value)
        {
            _value = value ?? string.Empty;
        }

        public string Value => _value ?? string.Empty;
        public bool IsValid => !string.IsNullOrWhiteSpace(_value);

        public bool Equals(TextStudioRenderPropertyId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioRenderPropertyId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        /// <summary>Returns whether two render-property identifiers are equal using ordinal case-sensitive comparison.</summary>
        public static bool operator ==(
            TextStudioRenderPropertyId left,
            TextStudioRenderPropertyId right)
        {
            return left.Equals(right);
        }

        /// <summary>Returns whether two render-property identifiers differ using ordinal case-sensitive comparison.</summary>
        public static bool operator !=(
            TextStudioRenderPropertyId left,
            TextStudioRenderPropertyId right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Typed scalar, vector, color, or unsigned renderer-property value.</summary>
    public readonly struct TextStudioRenderPropertyValue :
        IEquatable<TextStudioRenderPropertyValue>
    {
        readonly Vector4 _vector;
        readonly uint _unsigned;
        readonly TextStudioRenderPropertyValueType _valueType;

        TextStudioRenderPropertyValue(
            Vector4 vector,
            uint unsigned,
            TextStudioRenderPropertyValueType valueType)
        {
            _vector = vector;
            _unsigned = unsigned;
            _valueType = valueType;
        }

        public TextStudioRenderPropertyValueType ValueType => _valueType;
        public Vector4 Vector => _vector;
        public uint Unsigned => _unsigned;
        public float Float => _vector.x;
        public Vector2 Vector2 => new Vector2(_vector.x, _vector.y);
        public Vector3 Vector3 => new Vector3(_vector.x, _vector.y, _vector.z);
        public Color Color => _vector;

        /// <summary>Creates a typed scalar render-property value with the remaining vector components set to zero.</summary>
        public static TextStudioRenderPropertyValue FromFloat(float value)
        {
            return new TextStudioRenderPropertyValue(
                new Vector4(value, 0f, 0f, 0f),
                0u,
                TextStudioRenderPropertyValueType.Float);
        }

        /// <summary>Creates a typed two-component render-property value with the remaining vector components set to zero.</summary>
        public static TextStudioRenderPropertyValue FromVector2(Vector2 value)
        {
            return new TextStudioRenderPropertyValue(
                new Vector4(value.x, value.y, 0f, 0f),
                0u,
                TextStudioRenderPropertyValueType.Vector2);
        }

        /// <summary>Creates a typed three-component render-property value with the fourth vector component set to zero.</summary>
        public static TextStudioRenderPropertyValue FromVector3(Vector3 value)
        {
            return new TextStudioRenderPropertyValue(
                new Vector4(value.x, value.y, value.z, 0f),
                0u,
                TextStudioRenderPropertyValueType.Vector3);
        }

        /// <summary>Creates a typed four-component render-property value.</summary>
        public static TextStudioRenderPropertyValue FromVector4(Vector4 value)
        {
            return new TextStudioRenderPropertyValue(
                value,
                0u,
                TextStudioRenderPropertyValueType.Vector4);
        }

        /// <summary>Creates a typed RGBA color render-property value.</summary>
        public static TextStudioRenderPropertyValue FromColor(Color value)
        {
            return new TextStudioRenderPropertyValue(
                value,
                0u,
                TextStudioRenderPropertyValueType.Color);
        }

        /// <summary>Creates a typed unsigned integer render-property value without converting it to floating point.</summary>
        public static TextStudioRenderPropertyValue FromUInt(uint value)
        {
            return new TextStudioRenderPropertyValue(
                Vector4.zero,
                value,
                TextStudioRenderPropertyValueType.UInt);
        }

        public bool Equals(TextStudioRenderPropertyValue other)
        {
            return _vector.Equals(other._vector) &&
                   _unsigned == other._unsigned &&
                   _valueType == other._valueType;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioRenderPropertyValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (_vector.GetHashCode() * 397) ^ (int)_unsigned;
                return (hash * 397) ^ (int)_valueType;
            }
        }

        internal static TextStudioRenderPropertyValue FromVector(
            TextStudioRenderPropertyValueType type,
            Vector4 value,
            uint unsigned = 0u)
        {
            return type switch
            {
                TextStudioRenderPropertyValueType.Float => FromFloat(value.x),
                TextStudioRenderPropertyValueType.Vector2 =>
                    FromVector2(new Vector2(value.x, value.y)),
                TextStudioRenderPropertyValueType.Vector3 =>
                    FromVector3(new Vector3(value.x, value.y, value.z)),
                TextStudioRenderPropertyValueType.Color => FromColor(value),
                TextStudioRenderPropertyValueType.UInt => FromUInt(unsigned),
                _ => FromVector4(value)
            };
        }

        internal static TextStudioRenderPropertyValue Lerp(
            in TextStudioRenderPropertyValue first,
            in TextStudioRenderPropertyValue second,
            float amount,
            TextStudioRenderPropertyValueType type)
        {
            float t = Mathf.Clamp01(amount);
            if (type == TextStudioRenderPropertyValueType.UInt)
                return t < 0.5f ? first : second;
            return FromVector(type, Vector4.LerpUnclamped(first.Vector, second.Vector, t));
        }

        internal static TextStudioRenderPropertyValue Blend(
            in TextStudioRenderPropertyValue current,
            in TextStudioRenderPropertyValue target,
            TextStudioRenderPropertyValueType valueType,
            float influence,
            EffectBlendMode blendMode)
        {
            float weight = Mathf.Clamp01(influence);
            if (valueType == TextStudioRenderPropertyValueType.UInt)
            {
                if (blendMode == EffectBlendMode.Additive)
                {
                    ulong addition = (ulong)Math.Round(
                        target.Unsigned * (double)weight,
                        MidpointRounding.AwayFromZero);
                    return FromUInt((uint)Math.Min(
                        uint.MaxValue,
                        (ulong)current.Unsigned + addition));
                }
                if (blendMode == EffectBlendMode.Multiply)
                {
                    double multiplier = Mathf.LerpUnclamped(
                        1f,
                        target.Unsigned,
                        weight);
                    return FromUInt((uint)Math.Min(
                        uint.MaxValue,
                        Math.Max(0d, current.Unsigned * multiplier)));
                }
                return weight < 0.5f ? current : target;
            }

            Vector4 currentVector = current.Vector;
            Vector4 targetVector = target.Vector;
            Vector4 resolved = blendMode switch
            {
                EffectBlendMode.Override =>
                    Vector4.LerpUnclamped(currentVector, targetVector, weight),
                EffectBlendMode.Multiply => new Vector4(
                    currentVector.x * Mathf.LerpUnclamped(1f, targetVector.x, weight),
                    currentVector.y * Mathf.LerpUnclamped(1f, targetVector.y, weight),
                    currentVector.z * Mathf.LerpUnclamped(1f, targetVector.z, weight),
                    currentVector.w * Mathf.LerpUnclamped(1f, targetVector.w, weight)),
                EffectBlendMode.Lerp =>
                    Vector4.LerpUnclamped(currentVector, targetVector, weight),
                _ => currentVector + targetVector * weight
            };
            return FromVector(valueType, resolved);
        }
    }

    /// <summary>Immutable registration contract for one namespaced renderer property.</summary>
    public readonly struct TextStudioRenderPropertyDescriptor
    {
        public string Id { get; }
        public TextStudioRenderPropertyId PropertyId => new TextStudioRenderPropertyId(Id);
        public string DisplayName { get; }
        public string Category { get; }
        public string Description { get; }
        public string Units { get; }
        public TextStudioRenderPropertyValueType ValueType { get; }
        public TextStudioRenderPropertyStage Stage { get; }
        public TextStudioRenderPropertyPerformanceClass PerformanceClass { get; }
        public TextStudioRenderPropertyBlendModes BlendModes { get; }
        public TextStudioRenderPropertyValue Default { get; }
        public Vector4 DefaultValue => Default.Vector;
        public uint DefaultUnsigned => Default.Unsigned;
        public Vector4 RecommendedMinimum { get; }
        public Vector4 RecommendedMaximum { get; }
        public uint RecommendedMinimumUnsigned { get; }
        public uint RecommendedMaximumUnsigned { get; }
        public bool SupportsSurfaceMask { get; }
        public TextStudioRenderDomain SupportedRenderDomains { get; }
        public TextStudioReducedMotionBehavior ReducedMotionBehavior { get; }
        public TextStudioEffectBoundsBehavior BoundsBehavior { get; }

        /// <summary>Creates a typed render-property contract with defaults, recommended ranges, blend modes, surface and domain support, motion policy, and bounds behavior.</summary>
        public TextStudioRenderPropertyDescriptor(
            string id,
            string displayName,
            string category,
            string description,
            TextStudioRenderPropertyValueType valueType,
            TextStudioRenderPropertyStage stage,
            TextStudioRenderPropertyPerformanceClass performanceClass,
            Vector4 defaultValue,
            Vector4 recommendedMinimum,
            Vector4 recommendedMaximum,
            string units = "",
            TextStudioRenderPropertyBlendModes blendModes =
                TextStudioRenderPropertyBlendModes.All,
            bool supportsSurfaceMask = false,
            TextStudioRenderDomain supportedRenderDomains = TextStudioRenderDomain.All,
            uint defaultUnsigned = 0u,
            uint recommendedMinimumUnsigned = 0u,
            uint recommendedMaximumUnsigned = 1u,
            TextStudioReducedMotionBehavior reducedMotionBehavior =
                TextStudioReducedMotionBehavior.Reduce,
            TextStudioEffectBoundsBehavior boundsBehavior =
                TextStudioEffectBoundsBehavior.Unchanged)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            ValueType = valueType;
            Stage = stage;
            PerformanceClass = performanceClass;
            Default = TextStudioRenderPropertyValue.FromVector(
                valueType,
                defaultValue,
                defaultUnsigned);
            RecommendedMinimum = recommendedMinimum;
            RecommendedMaximum = recommendedMaximum;
            RecommendedMinimumUnsigned = recommendedMinimumUnsigned;
            RecommendedMaximumUnsigned = recommendedMaximumUnsigned;
            Units = units ?? string.Empty;
            BlendModes = blendModes;
            SupportsSurfaceMask = supportsSurfaceMask;
            SupportedRenderDomains = supportedRenderDomains;
            ReducedMotionBehavior = reducedMotionBehavior;
            BoundsBehavior = boundsBehavior;
        }
    }

    internal readonly struct TextStudioRenderPropertyRegistration
    {
        internal readonly TextStudioRenderPropertyDescriptor Descriptor;
        internal readonly int RuntimeIndex;
        internal readonly bool IsBuiltIn;

        internal TextStudioRenderPropertyRegistration(
            in TextStudioRenderPropertyDescriptor descriptor,
            int runtimeIndex,
            bool isBuiltIn)
        {
            Descriptor = descriptor;
            RuntimeIndex = runtimeIndex;
            IsBuiltIn = isBuiltIn;
        }
    }

    /// <summary>Registry for typed renderer properties. Serialized assets retain stable IDs.</summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class TextStudioRenderPropertyRegistry
    {
        static readonly Dictionary<string, TextStudioRenderPropertyRegistration> s_registrations =
            new Dictionary<string, TextStudioRenderPropertyRegistration>(StringComparer.Ordinal);
        static readonly List<TextStudioRenderPropertyRegistration?> s_runtimeRegistrations =
            new List<TextStudioRenderPropertyRegistration?>();
        static readonly Stack<int> s_freeRuntimeIndices = new Stack<int>();
        static int s_registrationVersion;

        internal static int RegistrationVersion => s_registrationVersion;
        internal static int RuntimeSlotCount => s_runtimeRegistrations.Count;

        /// <summary>Registers a valid render-property contract, accepting compatible existing registrations and rejecting conflicting contracts.</summary>
        public static bool Register(in TextStudioRenderPropertyDescriptor descriptor)
        {
            return Register(in descriptor, false);
        }

        internal static bool RegisterBuiltIn(
            in TextStudioRenderPropertyDescriptor descriptor)
        {
            return Register(in descriptor, true);
        }

        static bool Register(
            in TextStudioRenderPropertyDescriptor descriptor,
            bool isBuiltIn)
        {
            if (!IsValidContract(in descriptor))
                return false;

            if (s_registrations.TryGetValue(
                    descriptor.Id,
                    out TextStudioRenderPropertyRegistration existing))
            {
                if (!HasCompatibleContract(
                        in existing.Descriptor,
                        in descriptor))
                {
                    return false;
                }
                if (isBuiltIn && !existing.IsBuiltIn)
                {
                    TextStudioRenderPropertyRegistration upgraded =
                        new TextStudioRenderPropertyRegistration(
                            in descriptor,
                            existing.RuntimeIndex,
                            true);
                    s_registrations[descriptor.Id] = upgraded;
                    s_runtimeRegistrations[existing.RuntimeIndex] = upgraded;
                    s_registrationVersion++;
                }
                return true;
            }

            int runtimeIndex = s_freeRuntimeIndices.Count > 0
                ? s_freeRuntimeIndices.Pop()
                : s_runtimeRegistrations.Count;
            TextStudioRenderPropertyRegistration registration =
                new TextStudioRenderPropertyRegistration(
                    in descriptor,
                    runtimeIndex,
                    isBuiltIn);
            s_registrations.Add(descriptor.Id, registration);
            if (runtimeIndex == s_runtimeRegistrations.Count)
                s_runtimeRegistrations.Add(registration);
            else
                s_runtimeRegistrations[runtimeIndex] = registration;
            s_registrationVersion++;
            return true;
        }

        /// <summary>Removes a custom render-property registration, returning false for missing identifiers or built-in properties.</summary>
        public static bool Unregister(string descriptorId)
        {
            if (string.IsNullOrEmpty(descriptorId) ||
                !s_registrations.TryGetValue(
                    descriptorId,
                    out TextStudioRenderPropertyRegistration registration) ||
                registration.IsBuiltIn)
            {
                return false;
            }

            s_registrations.Remove(descriptorId);
            s_runtimeRegistrations[registration.RuntimeIndex] = null;
            s_freeRuntimeIndices.Push(registration.RuntimeIndex);
            s_registrationVersion++;
            return true;
        }

        /// <summary>Attempts to retrieve the registered render-property descriptor for the supplied identifier.</summary>
        public static bool TryGet(
            string descriptorId,
            out TextStudioRenderPropertyDescriptor descriptor)
        {
            if (!string.IsNullOrEmpty(descriptorId) &&
                s_registrations.TryGetValue(
                    descriptorId,
                    out TextStudioRenderPropertyRegistration registration))
            {
                descriptor = registration.Descriptor;
                return true;
            }

            descriptor = default;
            return false;
        }

        internal static bool TryGetRegistration(
            string descriptorId,
            out TextStudioRenderPropertyRegistration registration)
        {
            registration = default;
            return !string.IsNullOrEmpty(descriptorId) &&
                   s_registrations.TryGetValue(descriptorId, out registration);
        }

        internal static bool TryGetRegistration(
            int runtimeIndex,
            out TextStudioRenderPropertyRegistration registration)
        {
            if (runtimeIndex >= 0 &&
                runtimeIndex < s_runtimeRegistrations.Count &&
                s_runtimeRegistrations[runtimeIndex].HasValue)
            {
                registration = s_runtimeRegistrations[runtimeIndex].Value;
                return true;
            }

            registration = default;
            return false;
        }

        internal static bool IsBuiltIn(string descriptorId)
        {
            return !string.IsNullOrEmpty(descriptorId) &&
                   s_registrations.TryGetValue(
                       descriptorId,
                       out TextStudioRenderPropertyRegistration registration) &&
                   registration.IsBuiltIn;
        }

        /// <summary>Replaces a supplied list with all registered descriptors sorted by category, display name, and identifier.</summary>
        public static void GetAll(List<TextStudioRenderPropertyDescriptor> descriptors)
        {
            if (descriptors == null)
                return;
            descriptors.Clear();
            foreach (KeyValuePair<string, TextStudioRenderPropertyRegistration> pair in
                     s_registrations)
            {
                descriptors.Add(pair.Value.Descriptor);
            }
            descriptors.Sort((first, second) =>
            {
                int category = string.Compare(
                    first.Category,
                    second.Category,
                    StringComparison.Ordinal);
                return category != 0
                    ? category
                    : CompareDisplayNameAndId(in first, in second);
            });
        }

        internal static void ResetForRuntimeSession()
        {
            if (s_registrations.Count == 0 && s_runtimeRegistrations.Count == 0)
                return;
            s_registrations.Clear();
            s_runtimeRegistrations.Clear();
            s_freeRuntimeIndices.Clear();
            s_registrationVersion++;
        }

        static bool HasCompatibleContract(
            in TextStudioRenderPropertyDescriptor first,
            in TextStudioRenderPropertyDescriptor second)
        {
            return string.Equals(first.Id, second.Id, StringComparison.Ordinal) &&
                   string.Equals(
                       first.DisplayName,
                       second.DisplayName,
                       StringComparison.Ordinal) &&
                   string.Equals(first.Category, second.Category, StringComparison.Ordinal) &&
                   string.Equals(
                       first.Description,
                       second.Description,
                       StringComparison.Ordinal) &&
                   string.Equals(first.Units, second.Units, StringComparison.Ordinal) &&
                   first.ValueType == second.ValueType &&
                   first.Stage == second.Stage &&
                   first.PerformanceClass == second.PerformanceClass &&
                   first.BlendModes == second.BlendModes &&
                   first.Default.Equals(second.Default) &&
                   first.RecommendedMinimum.Equals(second.RecommendedMinimum) &&
                   first.RecommendedMaximum.Equals(second.RecommendedMaximum) &&
                   first.RecommendedMinimumUnsigned ==
                       second.RecommendedMinimumUnsigned &&
                   first.RecommendedMaximumUnsigned ==
                       second.RecommendedMaximumUnsigned &&
                   first.SupportsSurfaceMask == second.SupportsSurfaceMask &&
                   first.SupportedRenderDomains == second.SupportedRenderDomains &&
                   first.ReducedMotionBehavior == second.ReducedMotionBehavior &&
                   first.BoundsBehavior == second.BoundsBehavior;
        }

        static int CompareDisplayNameAndId(
            in TextStudioRenderPropertyDescriptor first,
            in TextStudioRenderPropertyDescriptor second)
        {
            int displayName = string.Compare(
                first.DisplayName,
                second.DisplayName,
                StringComparison.Ordinal);
            return displayName != 0
                ? displayName
                : string.Compare(first.Id, second.Id, StringComparison.Ordinal);
        }

        static bool IsValidContract(
            in TextStudioRenderPropertyDescriptor descriptor)
        {
            if (string.IsNullOrWhiteSpace(descriptor.Id) ||
                string.IsNullOrWhiteSpace(descriptor.DisplayName) ||
                descriptor.Id.IndexOf('.') <= 0 ||
                !string.Equals(descriptor.Id, descriptor.Id.Trim(), StringComparison.Ordinal) ||
                ContainsWhitespace(descriptor.Id) ||
                descriptor.ValueType > TextStudioRenderPropertyValueType.UInt ||
                descriptor.Stage > TextStudioRenderPropertyStage.Simulation ||
                descriptor.PerformanceClass >
                    TextStudioRenderPropertyPerformanceClass.Simulation ||
                descriptor.ReducedMotionBehavior < TextStudioReducedMotionBehavior.Preserve ||
                descriptor.ReducedMotionBehavior > TextStudioReducedMotionBehavior.Disable ||
                descriptor.BoundsBehavior < TextStudioEffectBoundsBehavior.Unchanged ||
                descriptor.BoundsBehavior > TextStudioEffectBoundsBehavior.GeometryRefresh ||
                descriptor.BlendModes == TextStudioRenderPropertyBlendModes.None ||
                (descriptor.BlendModes & ~TextStudioRenderPropertyBlendModes.All) != 0 ||
                descriptor.SupportedRenderDomains == TextStudioRenderDomain.None ||
                (descriptor.SupportedRenderDomains & ~TextStudioRenderDomain.All) != 0)
            {
                return false;
            }

            return IsFinite(descriptor.DefaultValue) &&
                   IsFinite(descriptor.RecommendedMinimum) &&
                   IsFinite(descriptor.RecommendedMaximum) &&
                   HasOrderedBounds(
                       descriptor.ValueType,
                       descriptor.RecommendedMinimum,
                       descriptor.RecommendedMaximum) &&
                   (descriptor.ValueType != TextStudioRenderPropertyValueType.UInt ||
                    (descriptor.RecommendedMinimumUnsigned <= descriptor.DefaultUnsigned &&
                     descriptor.DefaultUnsigned <= descriptor.RecommendedMaximumUnsigned));
        }

        static bool ContainsWhitespace(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (char.IsWhiteSpace(value[i]))
                    return true;
            }
            return false;
        }

        static bool IsFinite(Vector4 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z) &&
                   float.IsFinite(value.w);
        }

        static bool HasOrderedBounds(
            TextStudioRenderPropertyValueType valueType,
            Vector4 minimum,
            Vector4 maximum)
        {
            if (valueType == TextStudioRenderPropertyValueType.UInt)
                return true;
            int componentCount = valueType switch
            {
                TextStudioRenderPropertyValueType.Float => 1,
                TextStudioRenderPropertyValueType.Vector2 => 2,
                TextStudioRenderPropertyValueType.Vector3 => 3,
                _ => 4
            };
            for (int component = 0; component < componentCount; component++)
            {
                if (minimum[component] > maximum[component])
                    return false;
            }
            return true;
        }
    }

    internal static class TextStudioRenderPropertyCapabilities
    {
        internal static TextStudioEffectPerformanceTier ResolvePerformanceTier(
            TextStudioRenderPropertyPerformanceClass performanceClass)
        {
            return performanceClass switch
            {
                TextStudioRenderPropertyPerformanceClass.Realtime =>
                    TextStudioEffectPerformanceTier.Realtime,
                TextStudioRenderPropertyPerformanceClass.VertexStream =>
                    TextStudioEffectPerformanceTier.VertexStream,
                TextStudioRenderPropertyPerformanceClass.GeometryRefresh =>
                    TextStudioEffectPerformanceTier.GeometryRefresh,
                TextStudioRenderPropertyPerformanceClass.PreparedDeformation =>
                    TextStudioEffectPerformanceTier.PreparedDeformation,
                TextStudioRenderPropertyPerformanceClass.Simulation =>
                    TextStudioEffectPerformanceTier.Simulation,
                _ => TextStudioEffectPerformanceTier.Realtime
            };
        }
    }

    internal abstract class TextStudioRenderPropertySink
    {
        internal abstract bool Apply(
            in TextStudioRenderPropertyRegistration registration,
            int firstGlyphIndex,
            int glyphCount,
            in TextStudioRenderPropertyValue target,
            float influence,
            EffectBlendMode blendMode,
            TextStudioSurfaceMask surfaceMask);
    }

    /// <summary>Scoped allocation-free writer passed to effects.</summary>
    public readonly struct TextStudioRenderPropertyWriter
    {
        readonly TextStudioRenderPropertySink _sink;
        readonly int _firstGlyphIndex;
        readonly int _glyphCount;
        readonly float _influence;

        internal TextStudioRenderPropertyWriter(
            TextStudioRenderPropertySink sink,
            int firstGlyphIndex,
            int glyphCount,
            float influence = 1f)
        {
            _sink = sink;
            _firstGlyphIndex = Mathf.Max(0, firstGlyphIndex);
            _glyphCount = Mathf.Max(0, glyphCount);
            _influence = Mathf.Clamp01(influence);
        }

        public bool IsValid => _sink != null && _glyphCount > 0 && _influence > 0f;
        public int FirstGlyphIndex => _firstGlyphIndex;
        public int GlyphCount => _glyphCount;

        /// <summary>Returns a writer scoped to the requested glyph within this writer's existing glyph range.</summary>
        public TextStudioRenderPropertyWriter ForGlyph(int glyphIndex)
        {
            return ForRange(glyphIndex, 1);
        }

        /// <summary>Returns a writer scoped to the intersection of the requested glyph range and this writer's existing range.</summary>
        public TextStudioRenderPropertyWriter ForRange(
            int firstGlyphIndex,
            int glyphCount)
        {
            int first = Mathf.Max(_firstGlyphIndex, firstGlyphIndex);
            long requestedEnd = (long)firstGlyphIndex + Mathf.Max(0, glyphCount);
            int end = (int)Math.Min(
                (long)_firstGlyphIndex + _glyphCount,
                Math.Min(int.MaxValue, requestedEnd));
            return new TextStudioRenderPropertyWriter(
                _sink,
                first,
                Mathf.Max(0, end - first),
                _influence);
        }

        /// <summary>Attempts to blend a registered non-unsigned property value into the scoped glyphs using influence and the supported surface mask.</summary>
        public bool Apply(
            string propertyId,
            Vector4 value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            if (!TextStudioRenderPropertyRegistry.TryGetRegistration(
                    propertyId,
                    out TextStudioRenderPropertyRegistration registration) ||
                registration.Descriptor.ValueType == TextStudioRenderPropertyValueType.UInt)
            {
                return false;
            }
            TextStudioRenderPropertyValue target =
                TextStudioRenderPropertyValue.FromVector(
                    registration.Descriptor.ValueType,
                    value);
            return Apply(
                in registration,
                in target,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend a matching typed property value into the scoped glyphs using influence and the supported surface mask.</summary>
        public bool Apply(
            TextStudioRenderPropertyId propertyId,
            in TextStudioRenderPropertyValue value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            if (!TextStudioRenderPropertyRegistry.TryGetRegistration(
                    propertyId.Value,
                    out TextStudioRenderPropertyRegistration registration) ||
                value.ValueType != registration.Descriptor.ValueType)
            {
                return false;
            }
            return Apply(
                in registration,
                in value,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend an unsigned integer property value into the scoped glyphs using influence and the supported surface mask.</summary>
        public bool ApplyUInt(
            string propertyId,
            uint value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            if (!TextStudioRenderPropertyRegistry.TryGetRegistration(
                    propertyId,
                    out TextStudioRenderPropertyRegistration registration) ||
                registration.Descriptor.ValueType != TextStudioRenderPropertyValueType.UInt)
            {
                return false;
            }
            TextStudioRenderPropertyValue target =
                TextStudioRenderPropertyValue.FromUInt(value);
            return Apply(
                in registration,
                in target,
                influence,
                blendMode,
                surfaceMask);
        }

        bool Apply(
            in TextStudioRenderPropertyRegistration registration,
            in TextStudioRenderPropertyValue target,
            float influence,
            EffectBlendMode blendMode,
            TextStudioSurfaceMask surfaceMask)
        {
            if (!IsValid)
                return false;
            TextStudioRenderPropertyBlendModes requestedBlend =
                (TextStudioRenderPropertyBlendModes)(1 << (int)blendMode);
            if ((registration.Descriptor.BlendModes & requestedBlend) == 0)
                return false;
            float resolvedInfluence = Mathf.Clamp01(influence) * _influence;
            if (resolvedInfluence <= 0f)
                return false;
            TextStudioSurfaceMask resolvedSurfaceMask =
                registration.Descriptor.SupportsSurfaceMask
                    ? surfaceMask
                    : TextStudioSurfaceMask.All;
            return _sink.Apply(
                in registration,
                _firstGlyphIndex,
                _glyphCount,
                in target,
                resolvedInfluence,
                blendMode,
                resolvedSurfaceMask);
        }
    }

    /// <summary>Read-only renderer-property values for one glyph.</summary>
    public readonly struct TextStudioRenderPropertyGlyphView
    {
        readonly TextStudioRenderPropertyView _properties;
        readonly int _glyphIndex;

        internal TextStudioRenderPropertyGlyphView(
            TextStudioRenderPropertyView properties,
            int glyphIndex)
        {
            _properties = properties;
            _glyphIndex = glyphIndex;
        }

        public int GlyphIndex => _glyphIndex;
        public bool IsEmpty => !_properties.HasAnyForGlyph(_glyphIndex);

        /// <summary>Attempts to read a non-unsigned render-property value for this glyph.</summary>
        public bool TryGet(
            string propertyId,
            out Vector4 value)
        {
            return TryGet(propertyId, out value, out _);
        }

        /// <summary>Attempts to read a non-unsigned render-property value and its surface mask for this glyph.</summary>
        public bool TryGet(
            string propertyId,
            out Vector4 value,
            out TextStudioSurfaceMask surfaceMask)
        {
            if (_properties.TryGet(
                    propertyId,
                    _glyphIndex,
                    out TextStudioRenderPropertyValue propertyValue,
                    out surfaceMask) &&
                propertyValue.ValueType != TextStudioRenderPropertyValueType.UInt)
            {
                value = propertyValue.Vector;
                return true;
            }
            value = default;
            return false;
        }

        /// <summary>Attempts to read an unsigned integer render-property value for this glyph.</summary>
        public bool TryGetUInt(string propertyId, out uint value)
        {
            if (_properties.TryGet(
                    propertyId,
                    _glyphIndex,
                    out TextStudioRenderPropertyValue propertyValue,
                    out _) &&
                propertyValue.ValueType == TextStudioRenderPropertyValueType.UInt)
            {
                value = propertyValue.Unsigned;
                return true;
            }
            value = 0u;
            return false;
        }

        internal bool TryGet(
            in TextStudioRenderPropertyRegistration registration,
            out TextStudioRenderPropertyValue value,
            out TextStudioSurfaceMask surfaceMask)
        {
            return _properties.TryGet(
                in registration,
                _glyphIndex,
                out value,
                out surfaceMask);
        }
    }

    /// <summary>Transient read-only view over pooled renderer-property planes.</summary>
    public readonly struct TextStudioRenderPropertyView
    {
        readonly TextStudioRenderPropertyBuffer _buffer;
        readonly uint _generation;
        readonly uint _contentVersion;

        internal TextStudioRenderPropertyView(TextStudioRenderPropertyBuffer buffer)
        {
            _buffer = buffer;
            _generation = buffer != null ? buffer.Generation : 0u;
            _contentVersion = buffer != null ? buffer.ContentVersion : 0u;
        }

        internal TextStudioRenderPropertyBuffer Buffer =>
            IsCreated ? _buffer : null;
        internal uint Generation => _generation;
        internal int ActiveEntryCount => IsCreated
            ? _buffer.ActivePropertyCount
            : 0;
        public bool IsCreated => _buffer != null && _buffer.Generation == _generation;
        public bool IsEmpty => PropertyCount == 0;
        public int PropertyCount => IsCreated
            ? _buffer.GetValidActivePropertyCount(_generation)
            : 0;
        public int GlyphCount => IsCreated ? _buffer.GlyphCount : 0;
        public uint ContentVersion => _contentVersion;

        /// <summary>Creates a read-only view scoped to the supplied glyph index.</summary>
        public TextStudioRenderPropertyGlyphView GetGlyph(int glyphIndex)
        {
            return new TextStudioRenderPropertyGlyphView(this, glyphIndex);
        }

        /// <summary>Returns whether this valid property view contains any applied render-property value for the supplied glyph.</summary>
        public bool HasAnyForGlyph(int glyphIndex)
        {
            return IsCreated && _buffer.HasAnyForGlyph(_generation, glyphIndex);
        }

        /// <summary>Attempts to read an applied typed render-property value and its surface mask for the supplied glyph.</summary>
        public bool TryGet(
            string propertyId,
            int glyphIndex,
            out TextStudioRenderPropertyValue value,
            out TextStudioSurfaceMask surfaceMask)
        {
            if (IsCreated &&
                TextStudioRenderPropertyRegistry.TryGetRegistration(
                    propertyId,
                    out TextStudioRenderPropertyRegistration registration))
            {
                return _buffer.TryGet(
                    _generation,
                    in registration,
                    glyphIndex,
                    out value,
                    out surfaceMask);
            }
            value = default;
            surfaceMask = TextStudioSurfaceMask.All;
            return false;
        }

        internal bool TryGet(
            in TextStudioRenderPropertyRegistration registration,
            int glyphIndex,
            out TextStudioRenderPropertyValue value,
            out TextStudioSurfaceMask surfaceMask)
        {
            if (IsCreated)
            {
                return _buffer.TryGet(
                    _generation,
                    in registration,
                    glyphIndex,
                    out value,
                    out surfaceMask);
            }
            value = registration.Descriptor.Default;
            surfaceMask = TextStudioSurfaceMask.All;
            return false;
        }

        internal TextStudioRenderPropertyStorageMode GetStorageMode(string propertyId)
        {
            if (!IsCreated ||
                !TextStudioRenderPropertyRegistry.TryGetRegistration(
                    propertyId,
                    out TextStudioRenderPropertyRegistration registration))
            {
                return TextStudioRenderPropertyStorageMode.None;
            }
            return _buffer.GetStorageMode(_generation, in registration);
        }

        internal int GetActiveRuntimeIndex(int activeIndex)
        {
            return IsCreated
                ? _buffer.GetActiveRuntimeIndex(_generation, activeIndex)
                : -1;
        }

        internal bool HasSameValuesForGlyph(
            int glyphIndex,
            in TextStudioRenderPropertyView other)
        {
            if (!IsCreated)
                return !other.HasAnyForGlyph(glyphIndex);
            return _buffer.HasSameValuesForGlyph(
                _generation,
                glyphIndex,
                in other);
        }

        internal bool HasSameValues(in TextStudioRenderPropertyView other)
        {
            if (GlyphCount != other.GlyphCount || PropertyCount != other.PropertyCount)
                return false;
            if (PropertyCount == 0)
                return true;
            // A property written for every glyph holds one value, so uniform frames compare once instead of per glyph.
            bool? uniformMatch = _buffer.CompareUniformValues(_generation, in other);
            if (uniformMatch.HasValue)
                return uniformMatch.Value;
            for (int glyphIndex = 0; glyphIndex < GlyphCount; glyphIndex++)
            {
                if (!HasSameValuesForGlyph(glyphIndex, in other))
                    return false;
            }
            return true;
        }
    }

    internal sealed class TextStudioRenderPropertyBuffer : TextStudioRenderPropertySink
    {
        const int MaximumRetainedRanges = 32;

        struct RangeEntry
        {
            internal int First;
            internal int Count;
            internal TextStudioRenderPropertyValue Value;
            internal TextStudioSurfaceMask SurfaceMask;
        }

        sealed class Plane
        {
            internal uint Generation;
            internal string PropertyId = string.Empty;
            internal TextStudioRenderPropertyStorageMode Mode;
            internal bool HasUniform;
            internal TextStudioRenderPropertyValue UniformValue;
            internal TextStudioSurfaceMask UniformSurfaceMask;
            internal RangeEntry[] Ranges = Array.Empty<RangeEntry>();
            internal int RangeCount;
            internal float[] GlyphFloats = Array.Empty<float>();
            internal Vector2[] GlyphVector2Values = Array.Empty<Vector2>();
            internal Vector3[] GlyphVector3Values = Array.Empty<Vector3>();
            internal Vector4[] GlyphVector4Values = Array.Empty<Vector4>();
            internal uint[] GlyphUnsignedValues = Array.Empty<uint>();
            internal TextStudioSurfaceMask[] GlyphSurfaceMasks =
                Array.Empty<TextStudioSurfaceMask>();
            internal uint[] GlyphGenerations = Array.Empty<uint>();

            internal bool Activate(uint generation, string propertyId)
            {
                if (Generation == generation &&
                    string.Equals(PropertyId, propertyId, StringComparison.Ordinal))
                {
                    return false;
                }
                bool newlyActive = Generation != generation;
                Generation = generation;
                PropertyId = propertyId ?? string.Empty;
                Mode = TextStudioRenderPropertyStorageMode.None;
                HasUniform = false;
                RangeCount = 0;
                UniformValue = default;
                UniformSurfaceMask = TextStudioSurfaceMask.All;
                return newlyActive;
            }

            internal void EnsureRangeCapacity(int count)
            {
                if (Ranges.Length >= count)
                    return;
                Array.Resize(ref Ranges, TextStudioBufferCapacity.ForCount(count));
            }

            internal void EnsureGlyphCapacity(
                int count,
                TextStudioRenderPropertyValueType valueType)
            {
                int capacity = TextStudioBufferCapacity.ForCount(count);
                if (GlyphSurfaceMasks.Length < count)
                    Array.Resize(ref GlyphSurfaceMasks, capacity);
                if (GlyphGenerations.Length < count)
                    Array.Resize(ref GlyphGenerations, capacity);
                switch (valueType)
                {
                    case TextStudioRenderPropertyValueType.Float:
                        if (GlyphFloats.Length < count)
                            Array.Resize(ref GlyphFloats, capacity);
                        break;
                    case TextStudioRenderPropertyValueType.Vector2:
                        if (GlyphVector2Values.Length < count)
                            Array.Resize(ref GlyphVector2Values, capacity);
                        break;
                    case TextStudioRenderPropertyValueType.Vector3:
                        if (GlyphVector3Values.Length < count)
                            Array.Resize(ref GlyphVector3Values, capacity);
                        break;
                    case TextStudioRenderPropertyValueType.UInt:
                        if (GlyphUnsignedValues.Length < count)
                            Array.Resize(ref GlyphUnsignedValues, capacity);
                        break;
                    default:
                        if (GlyphVector4Values.Length < count)
                            Array.Resize(ref GlyphVector4Values, capacity);
                        break;
                }
            }

            internal void HardReset()
            {
                Generation = 0u;
                PropertyId = string.Empty;
                Mode = TextStudioRenderPropertyStorageMode.None;
                HasUniform = false;
                RangeCount = 0;
                if (GlyphGenerations.Length > 0)
                    Array.Clear(GlyphGenerations, 0, GlyphGenerations.Length);
            }
        }

        Plane[] _planes = Array.Empty<Plane>();
        int[] _activeRuntimeIndices = Array.Empty<int>();
        int _glyphCount;
        int _activePropertyCount;
        uint _generation = 1u;
        uint _contentVersion;

        internal uint Generation => _generation;
        internal uint ContentVersion => _contentVersion;
        internal int GlyphCount => _glyphCount;
        internal int ActivePropertyCount => _activePropertyCount;
        internal TextStudioRenderPropertyView View =>
            new TextStudioRenderPropertyView(this);

        internal void Clear(int glyphCount)
        {
            _glyphCount = Mathf.Max(0, glyphCount);
            _activePropertyCount = 0;
            unchecked
            {
                _generation++;
                _contentVersion++;
            }
            if (_generation != 0u)
                return;
            _generation = 1u;
            for (int i = 0; i < _planes.Length; i++)
                _planes[i]?.HardReset();
        }

        internal TextStudioRenderPropertyWriter CreateWriter(
            int firstGlyphIndex,
            int glyphCount,
            float influence = 1f)
        {
            int first = Mathf.Clamp(firstGlyphIndex, 0, _glyphCount);
            int count = Mathf.Clamp(glyphCount, 0, _glyphCount - first);
            return new TextStudioRenderPropertyWriter(this, first, count, influence);
        }

        internal void CopyFrom(
            in TextStudioRenderPropertyView source,
            int glyphCount)
        {
            Clear(glyphCount);
            TextStudioRenderPropertyBuffer sourceBuffer = source.Buffer;
            if (sourceBuffer == null)
                return;
            sourceBuffer.CopyActivePlanesTo(
                source.Generation,
                this,
                Mathf.Min(glyphCount, source.GlyphCount));
        }

        internal bool HasAnyForGlyph(uint generation, int glyphIndex)
        {
            if (generation != _generation ||
                glyphIndex < 0 ||
                glyphIndex >= _glyphCount)
            {
                return false;
            }
            for (int activeIndex = 0;
                 activeIndex < _activePropertyCount;
                 activeIndex++)
            {
                int runtimeIndex = _activeRuntimeIndices[activeIndex];
                Plane plane = _planes[runtimeIndex];
                if (plane == null ||
                    plane.Generation != generation ||
                    !TextStudioRenderPropertyRegistry.TryGetRegistration(
                        runtimeIndex,
                        out TextStudioRenderPropertyRegistration registration) ||
                    !string.Equals(
                        plane.PropertyId,
                        registration.Descriptor.Id,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                if (TryResolve(
                        plane,
                        generation,
                        glyphIndex,
                        registration.Descriptor.ValueType,
                        out _,
                        out _))
                {
                    return true;
                }
            }
            return false;
        }

        internal bool HasSameValuesForGlyph(
            uint generation,
            int glyphIndex,
            in TextStudioRenderPropertyView other)
        {
            if (generation != _generation ||
                glyphIndex < 0 ||
                glyphIndex >= _glyphCount ||
                glyphIndex >= other.GlyphCount)
            {
                return false;
            }

            for (int activeIndex = 0;
                 activeIndex < _activePropertyCount;
                 activeIndex++)
            {
                int runtimeIndex = GetActiveRuntimeIndex(generation, activeIndex);
                if (runtimeIndex < 0 ||
                    !TextStudioRenderPropertyRegistry.TryGetRegistration(
                        runtimeIndex,
                        out TextStudioRenderPropertyRegistration registration))
                {
                    continue;
                }
                if (!HasSameResolvedValue(
                        generation,
                        glyphIndex,
                        in registration,
                        in other))
                {
                    return false;
                }
            }

            for (int activeIndex = 0;
                 activeIndex < other.ActiveEntryCount;
                 activeIndex++)
            {
                int runtimeIndex = other.GetActiveRuntimeIndex(activeIndex);
                if (runtimeIndex < 0 ||
                    !TextStudioRenderPropertyRegistry.TryGetRegistration(
                        runtimeIndex,
                        out TextStudioRenderPropertyRegistration registration))
                {
                    continue;
                }
                if (!HasSameResolvedValue(
                        generation,
                        glyphIndex,
                        in registration,
                        in other))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Compares two views whose active properties each hold one value for every glyph, or returns null when a
        /// property varies by range or glyph so the caller compares each glyph.
        /// </summary>
        internal bool? CompareUniformValues(uint generation, in TextStudioRenderPropertyView other)
        {
            TextStudioRenderPropertyBuffer otherBuffer = other.Buffer;
            if (generation != _generation || otherBuffer == null || other.ActiveEntryCount != _activePropertyCount)
                return null;
            uint otherGeneration = other.Generation;
            for (int activeIndex = 0; activeIndex < _activePropertyCount; activeIndex++)
            {
                int runtimeIndex = _activeRuntimeIndices[activeIndex];
                Plane plane = runtimeIndex >= 0 && runtimeIndex < _planes.Length ? _planes[runtimeIndex] : null;
                Plane otherPlane = runtimeIndex >= 0 && runtimeIndex < otherBuffer._planes.Length
                    ? otherBuffer._planes[runtimeIndex]
                    : null;
                if (!IsUniformOnly(plane, generation) ||
                    !IsUniformOnly(otherPlane, otherGeneration) ||
                    !TextStudioRenderPropertyRegistry.TryGetRegistration(
                        runtimeIndex,
                        out TextStudioRenderPropertyRegistration registration) ||
                    !string.Equals(plane.PropertyId, registration.Descriptor.Id, StringComparison.Ordinal) ||
                    !string.Equals(otherPlane.PropertyId, registration.Descriptor.Id, StringComparison.Ordinal))
                {
                    return null;
                }
                if (!plane.UniformValue.Equals(otherPlane.UniformValue) ||
                    plane.UniformSurfaceMask != otherPlane.UniformSurfaceMask)
                {
                    return false;
                }
            }
            return true;
        }

        static bool IsUniformOnly(Plane plane, uint generation)
        {
            return plane != null &&
                   plane.Generation == generation &&
                   plane.Mode == TextStudioRenderPropertyStorageMode.Uniform &&
                   plane.HasUniform &&
                   plane.RangeCount == 0;
        }

        bool HasSameResolvedValue(
            uint generation,
            int glyphIndex,
            in TextStudioRenderPropertyRegistration registration,
            in TextStudioRenderPropertyView other)
        {
            bool hasCurrent = TryGet(
                generation,
                in registration,
                glyphIndex,
                out TextStudioRenderPropertyValue current,
                out TextStudioSurfaceMask currentMask);
            bool hasOther = other.TryGet(
                in registration,
                glyphIndex,
                out TextStudioRenderPropertyValue previous,
                out TextStudioSurfaceMask previousMask);
            return hasCurrent == hasOther &&
                   (!hasCurrent ||
                    (current.Equals(previous) && currentMask == previousMask));
        }

        internal int GetActiveRuntimeIndex(uint generation, int activeIndex)
        {
            if (generation != _generation ||
                activeIndex < 0 ||
                activeIndex >= _activePropertyCount)
            {
                return -1;
            }
            int runtimeIndex = _activeRuntimeIndices[activeIndex];
            if (!TextStudioRenderPropertyRegistry.TryGetRegistration(
                    runtimeIndex,
                    out TextStudioRenderPropertyRegistration registration))
            {
                return -1;
            }
            Plane plane = _planes[runtimeIndex];
            return plane != null &&
                   plane.Generation == generation &&
                   string.Equals(
                       plane.PropertyId,
                       registration.Descriptor.Id,
                       StringComparison.Ordinal)
                ? runtimeIndex
                : -1;
        }

        internal int GetValidActivePropertyCount(uint generation)
        {
            if (generation != _generation)
                return 0;
            int count = 0;
            for (int activeIndex = 0;
                 activeIndex < _activePropertyCount;
                 activeIndex++)
            {
                if (GetActiveRuntimeIndex(generation, activeIndex) >= 0)
                    count++;
            }
            return count;
        }

        internal bool TryGet(
            uint generation,
            in TextStudioRenderPropertyRegistration registration,
            int glyphIndex,
            out TextStudioRenderPropertyValue value,
            out TextStudioSurfaceMask surfaceMask)
        {
            if (generation != _generation ||
                glyphIndex < 0 ||
                glyphIndex >= _glyphCount ||
                registration.RuntimeIndex < 0 ||
                registration.RuntimeIndex >= _planes.Length)
            {
                value = registration.Descriptor.Default;
                surfaceMask = TextStudioSurfaceMask.All;
                return false;
            }

            Plane plane = _planes[registration.RuntimeIndex];
            if (plane == null ||
                !string.Equals(
                    plane.PropertyId,
                    registration.Descriptor.Id,
                    StringComparison.Ordinal) ||
                !TryResolve(
                    plane,
                    generation,
                    glyphIndex,
                    registration.Descriptor.ValueType,
                    out value,
                    out surfaceMask))
            {
                value = registration.Descriptor.Default;
                surfaceMask = TextStudioSurfaceMask.All;
                return false;
            }
            return true;
        }

        internal TextStudioRenderPropertyStorageMode GetStorageMode(
            uint generation,
            in TextStudioRenderPropertyRegistration registration)
        {
            int runtimeIndex = registration.RuntimeIndex;
            if (generation != _generation ||
                runtimeIndex < 0 ||
                runtimeIndex >= _planes.Length ||
                _planes[runtimeIndex] == null ||
                _planes[runtimeIndex].Generation != generation ||
                !string.Equals(
                    _planes[runtimeIndex].PropertyId,
                    registration.Descriptor.Id,
                    StringComparison.Ordinal))
            {
                return TextStudioRenderPropertyStorageMode.None;
            }
            return _planes[runtimeIndex].Mode;
        }

        internal void BlendGlyph(
            int glyphIndex,
            TextStudioRenderPropertyGlyphAccumulator first,
            TextStudioRenderPropertyGlyphAccumulator second,
            float amount)
        {
            float t = Mathf.Clamp01(amount);
            for (int usedIndex = 0; usedIndex < first.UsedCount; usedIndex++)
            {
                BlendGlyphProperty(
                    glyphIndex,
                    first.GetUsedRuntimeIndex(usedIndex),
                    first,
                    second,
                    t);
            }
            for (int usedIndex = 0; usedIndex < second.UsedCount; usedIndex++)
            {
                int runtimeIndex = second.GetUsedRuntimeIndex(usedIndex);
                if (first.Contains(runtimeIndex))
                    continue;
                BlendGlyphProperty(
                    glyphIndex,
                    runtimeIndex,
                    first,
                    second,
                    t);
            }
        }

        void BlendGlyphProperty(
            int glyphIndex,
            int runtimeIndex,
            TextStudioRenderPropertyGlyphAccumulator first,
            TextStudioRenderPropertyGlyphAccumulator second,
            float amount)
        {
            bool hasFirst = first.TryGet(
                runtimeIndex,
                out TextStudioRenderPropertyValue firstValue,
                out TextStudioSurfaceMask firstMask);
            bool hasSecond = second.TryGet(
                runtimeIndex,
                out TextStudioRenderPropertyValue secondValue,
                out TextStudioSurfaceMask secondMask);
            if (!TextStudioRenderPropertyRegistry.TryGetRegistration(
                    runtimeIndex,
                    out TextStudioRenderPropertyRegistration registration))
            {
                return;
            }
            if (amount <= 0f)
            {
                if (hasFirst)
                    WriteGlyph(in registration, glyphIndex, in firstValue, firstMask);
                return;
            }
            if (amount >= 1f)
            {
                if (hasSecond)
                    WriteGlyph(in registration, glyphIndex, in secondValue, secondMask);
                return;
            }
            if (!hasFirst)
                firstValue = registration.Descriptor.Default;
            if (!hasSecond)
                secondValue = registration.Descriptor.Default;
            TextStudioRenderPropertyValue value =
                TextStudioRenderPropertyValue.Lerp(
                    in firstValue,
                    in secondValue,
                    amount,
                    registration.Descriptor.ValueType);
            TextStudioSurfaceMask mask = hasFirst && hasSecond
                ? (amount < 0.5f ? firstMask : secondMask)
                : (hasFirst ? firstMask : secondMask);
            WriteGlyph(in registration, glyphIndex, in value, mask);
        }

        internal override bool Apply(
            in TextStudioRenderPropertyRegistration registration,
            int firstGlyphIndex,
            int glyphCount,
            in TextStudioRenderPropertyValue target,
            float influence,
            EffectBlendMode blendMode,
            TextStudioSurfaceMask surfaceMask)
        {
            if (_glyphCount <= 0 || glyphCount <= 0 || influence <= 0f)
                return false;
            int first = Mathf.Clamp(firstGlyphIndex, 0, _glyphCount);
            int count = Mathf.Clamp(glyphCount, 0, _glyphCount - first);
            if (count <= 0)
                return false;

            Plane plane = GetOrCreatePlane(registration.RuntimeIndex);
            ActivatePlane(
                plane,
                registration.RuntimeIndex,
                registration.Descriptor.Id);

            if (blendMode == EffectBlendMode.Override && influence >= 0.999999f)
            {
                TextStudioRenderPropertyValue defaultValue =
                    registration.Descriptor.Default;
                WriteResolvedRange(
                    plane,
                    first,
                    count,
                    in defaultValue,
                    in target,
                    registration.Descriptor.ValueType,
                    surfaceMask);
                unchecked { _contentVersion++; }
                return true;
            }

            PromoteToGlyphs(
                plane,
                registration.Descriptor.Default,
                _generation,
                _glyphCount,
                registration.Descriptor.ValueType);
            for (int glyphIndex = first; glyphIndex < first + count; glyphIndex++)
            {
                TextStudioRenderPropertyValue current =
                    plane.GlyphGenerations[glyphIndex] == _generation
                        ? GetGlyphValue(
                            plane,
                            glyphIndex,
                            registration.Descriptor.ValueType)
                        : registration.Descriptor.Default;
                TextStudioRenderPropertyValue resolved =
                    TextStudioRenderPropertyValue.Blend(
                        in current,
                        in target,
                        registration.Descriptor.ValueType,
                        influence,
                        blendMode);
                SetGlyphValue(
                    plane,
                    glyphIndex,
                    in resolved,
                    registration.Descriptor.ValueType);
                plane.GlyphSurfaceMasks[glyphIndex] = surfaceMask;
                plane.GlyphGenerations[glyphIndex] = _generation;
            }
            unchecked { _contentVersion++; }
            return true;
        }

        void WriteGlyph(
            in TextStudioRenderPropertyRegistration registration,
            int glyphIndex,
            in TextStudioRenderPropertyValue value,
            TextStudioSurfaceMask surfaceMask)
        {
            if (glyphIndex < 0 || glyphIndex >= _glyphCount)
                return;
            Plane plane = GetOrCreatePlane(registration.RuntimeIndex);
            ActivatePlane(
                plane,
                registration.RuntimeIndex,
                registration.Descriptor.Id);
            PromoteToGlyphs(
                plane,
                registration.Descriptor.Default,
                _generation,
                _glyphCount,
                registration.Descriptor.ValueType);
            SetGlyphValue(
                plane,
                glyphIndex,
                in value,
                registration.Descriptor.ValueType);
            plane.GlyphSurfaceMasks[glyphIndex] = surfaceMask;
            plane.GlyphGenerations[glyphIndex] = _generation;
            unchecked { _contentVersion++; }
        }

        Plane GetOrCreatePlane(int runtimeIndex)
        {
            if (_planes.Length <= runtimeIndex)
                Array.Resize(ref _planes, TextStudioBufferCapacity.ForCount(runtimeIndex + 1));
            return _planes[runtimeIndex] ??= new Plane();
        }

        void ActivatePlane(
            Plane plane,
            int runtimeIndex,
            string propertyId)
        {
            if (!plane.Activate(_generation, propertyId))
                return;
            if (_activeRuntimeIndices.Length <= _activePropertyCount)
            {
                Array.Resize(
                    ref _activeRuntimeIndices,
                    TextStudioBufferCapacity.ForCount(_activePropertyCount + 1));
            }
            _activeRuntimeIndices[_activePropertyCount++] = runtimeIndex;
        }

        void WriteResolvedRange(
            Plane plane,
            int first,
            int count,
            in TextStudioRenderPropertyValue defaultValue,
            in TextStudioRenderPropertyValue value,
            TextStudioRenderPropertyValueType valueType,
            TextStudioSurfaceMask surfaceMask)
        {
            if (first == 0 && count == _glyphCount)
            {
                plane.Mode = TextStudioRenderPropertyStorageMode.Uniform;
                plane.HasUniform = true;
                plane.UniformValue = value;
                plane.UniformSurfaceMask = surfaceMask;
                plane.RangeCount = 0;
                return;
            }

            if (plane.Mode == TextStudioRenderPropertyStorageMode.Glyphs ||
                plane.RangeCount >= MaximumRetainedRanges)
            {
                if (plane.Mode != TextStudioRenderPropertyStorageMode.Glyphs)
                {
                    PromoteToGlyphs(
                        plane,
                        in defaultValue,
                        _generation,
                        _glyphCount,
                        valueType);
                }
                for (int glyphIndex = first; glyphIndex < first + count; glyphIndex++)
                {
                    SetGlyphValue(
                        plane,
                        glyphIndex,
                        in value,
                        valueType);
                    plane.GlyphSurfaceMasks[glyphIndex] = surfaceMask;
                    plane.GlyphGenerations[glyphIndex] = _generation;
                }
                return;
            }

            plane.EnsureRangeCapacity(plane.RangeCount + 1);
            plane.Ranges[plane.RangeCount++] = new RangeEntry
            {
                First = first,
                Count = count,
                Value = value,
                SurfaceMask = surfaceMask
            };
            plane.Mode = TextStudioRenderPropertyStorageMode.Ranges;
        }

        static void PromoteToGlyphs(
            Plane plane,
            in TextStudioRenderPropertyValue defaultValue,
            uint generation,
            int glyphCount,
            TextStudioRenderPropertyValueType valueType)
        {
            if (plane.Mode == TextStudioRenderPropertyStorageMode.Glyphs)
            {
                plane.EnsureGlyphCapacity(glyphCount, valueType);
                return;
            }

            plane.EnsureGlyphCapacity(glyphCount, valueType);
            for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
            {
                if (!TryResolve(
                        plane,
                        generation,
                        glyphIndex,
                        valueType,
                        out TextStudioRenderPropertyValue value,
                        out TextStudioSurfaceMask surfaceMask))
                {
                    plane.GlyphGenerations[glyphIndex] = 0u;
                    continue;
                }
                SetGlyphValue(
                    plane,
                    glyphIndex,
                    in value,
                    valueType);
                plane.GlyphSurfaceMasks[glyphIndex] = surfaceMask;
                plane.GlyphGenerations[glyphIndex] = generation;
            }
            plane.Mode = TextStudioRenderPropertyStorageMode.Glyphs;
            plane.RangeCount = 0;
        }

        static bool TryResolve(
            Plane plane,
            uint generation,
            int glyphIndex,
            TextStudioRenderPropertyValueType valueType,
            out TextStudioRenderPropertyValue value,
            out TextStudioSurfaceMask surfaceMask)
        {
            if (plane == null || plane.Generation != generation)
            {
                value = default;
                surfaceMask = TextStudioSurfaceMask.All;
                return false;
            }

            if (plane.Mode == TextStudioRenderPropertyStorageMode.Glyphs)
            {
                if (glyphIndex >= 0 &&
                    glyphIndex < plane.GlyphGenerations.Length &&
                    plane.GlyphGenerations[glyphIndex] == generation)
                {
                    value = GetGlyphValue(plane, glyphIndex, valueType);
                    surfaceMask = plane.GlyphSurfaceMasks[glyphIndex];
                    return true;
                }
                value = default;
                surfaceMask = TextStudioSurfaceMask.All;
                return false;
            }

            for (int rangeIndex = plane.RangeCount - 1; rangeIndex >= 0; rangeIndex--)
            {
                RangeEntry range = plane.Ranges[rangeIndex];
                if (glyphIndex < range.First || glyphIndex >= range.First + range.Count)
                    continue;
                value = range.Value;
                surfaceMask = range.SurfaceMask;
                return true;
            }

            if (plane.HasUniform)
            {
                value = plane.UniformValue;
                surfaceMask = plane.UniformSurfaceMask;
                return true;
            }

            value = default;
            surfaceMask = TextStudioSurfaceMask.All;
            return false;
        }

        static TextStudioRenderPropertyValue GetGlyphValue(
            Plane plane,
            int glyphIndex,
            TextStudioRenderPropertyValueType valueType)
        {
            return valueType switch
            {
                TextStudioRenderPropertyValueType.Float =>
                    TextStudioRenderPropertyValue.FromFloat(
                        plane.GlyphFloats[glyphIndex]),
                TextStudioRenderPropertyValueType.Vector2 =>
                    TextStudioRenderPropertyValue.FromVector2(
                        plane.GlyphVector2Values[glyphIndex]),
                TextStudioRenderPropertyValueType.Vector3 =>
                    TextStudioRenderPropertyValue.FromVector3(
                        plane.GlyphVector3Values[glyphIndex]),
                TextStudioRenderPropertyValueType.UInt =>
                    TextStudioRenderPropertyValue.FromUInt(
                        plane.GlyphUnsignedValues[glyphIndex]),
                TextStudioRenderPropertyValueType.Color =>
                    TextStudioRenderPropertyValue.FromColor(
                        plane.GlyphVector4Values[glyphIndex]),
                _ => TextStudioRenderPropertyValue.FromVector4(
                    plane.GlyphVector4Values[glyphIndex])
            };
        }

        static void SetGlyphValue(
            Plane plane,
            int glyphIndex,
            in TextStudioRenderPropertyValue value,
            TextStudioRenderPropertyValueType valueType)
        {
            switch (valueType)
            {
                case TextStudioRenderPropertyValueType.Float:
                    plane.GlyphFloats[glyphIndex] = value.Float;
                    break;
                case TextStudioRenderPropertyValueType.Vector2:
                    plane.GlyphVector2Values[glyphIndex] = value.Vector2;
                    break;
                case TextStudioRenderPropertyValueType.Vector3:
                    plane.GlyphVector3Values[glyphIndex] = value.Vector3;
                    break;
                case TextStudioRenderPropertyValueType.UInt:
                    plane.GlyphUnsignedValues[glyphIndex] = value.Unsigned;
                    break;
                default:
                    plane.GlyphVector4Values[glyphIndex] = value.Vector;
                    break;
            }
        }

        void CopyActivePlanesTo(
            uint sourceGeneration,
            TextStudioRenderPropertyBuffer destination,
            int glyphCount)
        {
            for (int activeIndex = 0;
                 activeIndex < _activePropertyCount;
                 activeIndex++)
            {
                int runtimeIndex = _activeRuntimeIndices[activeIndex];
                Plane source = _planes[runtimeIndex];
                if (source == null ||
                    source.Generation != sourceGeneration ||
                    !TextStudioRenderPropertyRegistry.TryGetRegistration(
                        runtimeIndex,
                        out TextStudioRenderPropertyRegistration registration) ||
                    !string.Equals(
                        source.PropertyId,
                        registration.Descriptor.Id,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                Plane target = destination.GetOrCreatePlane(runtimeIndex);
                destination.ActivatePlane(
                    target,
                    runtimeIndex,
                    registration.Descriptor.Id);
                target.Mode = source.Mode;
                target.HasUniform = source.HasUniform;
                target.UniformValue = source.UniformValue;
                target.UniformSurfaceMask = source.UniformSurfaceMask;
                target.RangeCount = source.RangeCount;
                if (source.RangeCount > 0)
                {
                    target.EnsureRangeCapacity(source.RangeCount);
                    Array.Copy(source.Ranges, target.Ranges, source.RangeCount);
                }
                if (source.Mode != TextStudioRenderPropertyStorageMode.Glyphs)
                    continue;
                target.EnsureGlyphCapacity(
                    glyphCount,
                    registration.Descriptor.ValueType);
                for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
                {
                    if (source.GlyphGenerations[glyphIndex] != sourceGeneration)
                        continue;
                    TextStudioRenderPropertyValue value = GetGlyphValue(
                        source,
                        glyphIndex,
                        registration.Descriptor.ValueType);
                    SetGlyphValue(
                        target,
                        glyphIndex,
                        in value,
                        registration.Descriptor.ValueType);
                    target.GlyphSurfaceMasks[glyphIndex] =
                        source.GlyphSurfaceMasks[glyphIndex];
                    target.GlyphGenerations[glyphIndex] = destination._generation;
                }
            }
            unchecked { destination._contentVersion++; }
        }
    }

    internal sealed class TextStudioRenderPropertyGlyphAccumulator :
        TextStudioRenderPropertySink
    {
        TextStudioRenderPropertyValue[] _values =
            Array.Empty<TextStudioRenderPropertyValue>();
        TextStudioSurfaceMask[] _surfaceMasks =
            Array.Empty<TextStudioSurfaceMask>();
        bool[] _present = Array.Empty<bool>();
        int[] _usedIndices = Array.Empty<int>();
        int _usedCount;
        int _glyphIndex;

        internal int UsedCount => _usedCount;

        internal int GetUsedRuntimeIndex(int usedIndex)
        {
            return usedIndex >= 0 && usedIndex < _usedCount
                ? _usedIndices[usedIndex]
                : -1;
        }

        internal bool Contains(int runtimeIndex)
        {
            return runtimeIndex >= 0 &&
                   runtimeIndex < _present.Length &&
                   _present[runtimeIndex];
        }

        internal void Begin(
            int glyphIndex,
            in TextStudioRenderPropertyView source)
        {
            for (int i = 0; i < _usedCount; i++)
                _present[_usedIndices[i]] = false;
            _usedCount = 0;
            _glyphIndex = glyphIndex;
            EnsureCapacity(TextStudioRenderPropertyRegistry.RuntimeSlotCount);
            for (int activeIndex = 0;
                 activeIndex < source.ActiveEntryCount;
                 activeIndex++)
            {
                int runtimeIndex = source.GetActiveRuntimeIndex(activeIndex);
                if (!TextStudioRenderPropertyRegistry.TryGetRegistration(
                        runtimeIndex,
                        out TextStudioRenderPropertyRegistration registration) ||
                    !source.TryGet(
                        registration.Descriptor.Id,
                        glyphIndex,
                        out TextStudioRenderPropertyValue value,
                        out TextStudioSurfaceMask surfaceMask))
                {
                    continue;
                }
                Set(runtimeIndex, in value, surfaceMask);
            }
        }

        internal TextStudioRenderPropertyWriter CreateWriter(float influence = 1f)
        {
            return new TextStudioRenderPropertyWriter(
                this,
                _glyphIndex,
                1,
                influence);
        }

        internal bool TryGet(
            int runtimeIndex,
            out TextStudioRenderPropertyValue value,
            out TextStudioSurfaceMask surfaceMask)
        {
            if (runtimeIndex >= 0 &&
                runtimeIndex < _present.Length &&
                _present[runtimeIndex])
            {
                value = _values[runtimeIndex];
                surfaceMask = _surfaceMasks[runtimeIndex];
                return true;
            }
            value = default;
            surfaceMask = TextStudioSurfaceMask.All;
            return false;
        }

        internal override bool Apply(
            in TextStudioRenderPropertyRegistration registration,
            int firstGlyphIndex,
            int glyphCount,
            in TextStudioRenderPropertyValue target,
            float influence,
            EffectBlendMode blendMode,
            TextStudioSurfaceMask surfaceMask)
        {
            if (glyphCount <= 0 ||
                firstGlyphIndex > _glyphIndex ||
                firstGlyphIndex + glyphCount <= _glyphIndex)
            {
                return false;
            }
            TextStudioRenderPropertyValue current = TryGet(
                registration.RuntimeIndex,
                out TextStudioRenderPropertyValue existing,
                out _)
                ? existing
                : registration.Descriptor.Default;
            TextStudioRenderPropertyValue resolved =
                TextStudioRenderPropertyValue.Blend(
                    in current,
                    in target,
                    registration.Descriptor.ValueType,
                    influence,
                    blendMode);
            Set(registration.RuntimeIndex, in resolved, surfaceMask);
            return true;
        }

        void Set(
            int runtimeIndex,
            in TextStudioRenderPropertyValue value,
            TextStudioSurfaceMask surfaceMask)
        {
            EnsureCapacity(runtimeIndex + 1);
            if (!_present[runtimeIndex])
            {
                if (_usedIndices.Length <= _usedCount)
                {
                    Array.Resize(
                        ref _usedIndices,
                        TextStudioBufferCapacity.ForCount(_usedCount + 1));
                }
                _usedIndices[_usedCount++] = runtimeIndex;
                _present[runtimeIndex] = true;
            }
            _values[runtimeIndex] = value;
            _surfaceMasks[runtimeIndex] = surfaceMask;
        }

        void EnsureCapacity(int count)
        {
            if (_values.Length >= count)
                return;
            int capacity = TextStudioBufferCapacity.ForCount(count);
            Array.Resize(ref _values, capacity);
            Array.Resize(ref _surfaceMasks, capacity);
            Array.Resize(ref _present, capacity);
        }

    }
}
