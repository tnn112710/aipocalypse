using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Version information for renderer-neutral Text Studio extension contracts.</summary>
    public static class TextStudioExtensionApi
    {
        public const int Major = 2;
        public const int Minor = 0;
        public const int Patch = 0;

        /// <summary>Returns whether an extension compiled for the supplied version can use this API.</summary>
        public static bool IsCompatible(int requiredMajor, int requiredMinor = 0)
        {
            return requiredMajor == Major && requiredMinor <= Minor;
        }
    }

    /// <summary>Generation-safe identifier used by extension registrations and authority leases.</summary>
    public readonly struct TextStudioGenerationHandle : IEquatable<TextStudioGenerationHandle>
    {
        readonly int _slot;
        readonly uint _generation;

        internal TextStudioGenerationHandle(int slot, uint generation)
        {
            _slot = slot;
            _generation = generation;
        }

        public int Slot => _slot;
        public uint Generation => _generation;
        public bool IsValid => _slot >= 0 && _generation != 0u;

        public bool Equals(TextStudioGenerationHandle other)
        {
            return _slot == other._slot && _generation == other._generation;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioGenerationHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_slot * 397) ^ (int)_generation;
            }
        }

        /// <summary>Reports whether two generation handles identify the same live slot generation.</summary>
        public static bool operator ==(
            TextStudioGenerationHandle left,
            TextStudioGenerationHandle right)
        {
            return left.Equals(right);
        }

        /// <summary>Reports whether two generation handles identify different slot generations.</summary>
        public static bool operator !=(
            TextStudioGenerationHandle left,
            TextStudioGenerationHandle right)
        {
            return !left.Equals(right);
        }
    }

    public enum TextStudioFrameTargetKind : byte
    {
        TextBackend = 0,
        ExternalRenderer = 1,
        UIToolkit = 2,
        Custom = 3
    }

    /// <summary>Reference-identity target for one independently composed Text Studio output.</summary>
    public readonly struct TextStudioFrameTarget : IEquatable<TextStudioFrameTarget>
    {
        readonly object _owner;
        readonly object _target;
        readonly TextStudioFrameTargetKind _kind;

        internal TextStudioFrameTarget(
            object owner,
            object target,
            TextStudioFrameTargetKind kind)
        {
            _owner = owner;
            _target = target;
            _kind = kind;
        }

        internal object Owner => _owner;
        public object Target => _target;
        public TextStudioFrameTargetKind Kind => _kind;
        public bool IsValid => _owner != null && _target != null;

        public bool Equals(TextStudioFrameTarget other)
        {
            return ReferenceEquals(_owner, other._owner) &&
                   ReferenceEquals(_target, other._target) &&
                   _kind == other._kind;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioFrameTarget other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int ownerHash = _owner != null ? RuntimeHelpers.GetHashCode(_owner) : 0;
                int targetHash = _target != null ? RuntimeHelpers.GetHashCode(_target) : 0;
                return ((ownerHash * 397) ^ targetHash) * 397 ^ (int)_kind;
            }
        }

        /// <summary>Reports whether two frame targets refer to the same owner, target, and target kind.</summary>
        public static bool operator ==(TextStudioFrameTarget left, TextStudioFrameTarget right)
        {
            return left.Equals(right);
        }

        /// <summary>Reports whether two frame targets refer to different output targets.</summary>
        public static bool operator !=(TextStudioFrameTarget left, TextStudioFrameTarget right)
        {
            return !left.Equals(right);
        }
    }

    /// <summary>Stable semantic identity for one glyph within a compiled source generation.</summary>
    public readonly struct TextStudioGlyphIdentity : IEquatable<TextStudioGlyphIdentity>
    {
        public uint SourceVersion { get; }
        public int GlyphIndex { get; }
        public int ResolvedStart { get; }
        public int ResolvedLength { get; }
        public int SourceStart { get; }
        public int SourceLength { get; }

        public bool IsValid => SourceVersion != 0u && GlyphIndex >= 0;

        /// <summary>Creates a semantic glyph identity from source and resolved-text coordinates.</summary>
        public TextStudioGlyphIdentity(
            uint sourceVersion,
            int glyphIndex,
            int resolvedStart,
            int resolvedLength,
            int sourceStart,
            int sourceLength)
        {
            SourceVersion = sourceVersion;
            GlyphIndex = glyphIndex;
            ResolvedStart = resolvedStart;
            ResolvedLength = resolvedLength;
            SourceStart = sourceStart;
            SourceLength = sourceLength;
        }

        /// <summary>Returns the semantic identity of a glyph in a frame, or an invalid identity when unavailable.</summary>
        public static TextStudioGlyphIdentity FromFrame(
            in TextStudioFrame frame,
            int glyphIndex)
        {
            if (!frame.GlyphStates.IsCreated ||
                glyphIndex < 0 ||
                glyphIndex >= frame.GlyphStateCount)
            {
                return default;
            }

            TextStudioGlyphInstanceState state = frame.GlyphStates[glyphIndex];
            return new TextStudioGlyphIdentity(
                frame.Plan.SourceVersion,
                state.glyphIndex,
                state.resolvedStart,
                state.resolvedLength,
                state.sourceStart,
                state.sourceLength);
        }

        public bool Equals(TextStudioGlyphIdentity other)
        {
            return SourceVersion == other.SourceVersion &&
                   GlyphIndex == other.GlyphIndex &&
                   ResolvedStart == other.ResolvedStart &&
                   ResolvedLength == other.ResolvedLength &&
                   SourceStart == other.SourceStart &&
                   SourceLength == other.SourceLength;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioGlyphIdentity other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)SourceVersion;
                hash = (hash * 397) ^ GlyphIndex;
                hash = (hash * 397) ^ ResolvedStart;
                hash = (hash * 397) ^ ResolvedLength;
                hash = (hash * 397) ^ SourceStart;
                return (hash * 397) ^ SourceLength;
            }
        }
    }

    [Flags]
    public enum TextStudioGlyphAuthorityChannels : byte
    {
        None = 0,
        Transform = 1 << 0,
        Velocity = 1 << 1,
        VisualResponse = 1 << 2,
        All = Transform | Velocity | VisualResponse
    }

    /// <summary>Exclusive channel and glyph range requested by a stateful extension.</summary>
    public readonly struct TextStudioGlyphAuthorityRequest
    {
        public TextStudioGlyphAuthorityChannels Channels { get; }
        public int FirstGlyphIndex { get; }
        public int GlyphCount { get; }
        public bool CoversAllGlyphs => GlyphCount < 0;
        public bool IsValid => Channels != TextStudioGlyphAuthorityChannels.None &&
                               FirstGlyphIndex >= 0 &&
                               (GlyphCount > 0 || CoversAllGlyphs);

        /// <summary>Creates an authority request for the specified channels and glyph range.</summary>
        public TextStudioGlyphAuthorityRequest(
            TextStudioGlyphAuthorityChannels channels,
            int firstGlyphIndex,
            int glyphCount)
        {
            Channels = channels;
            FirstGlyphIndex = Mathf.Max(0, firstGlyphIndex);
            GlyphCount = glyphCount < 0 ? -1 : glyphCount;
        }

        /// <summary>Creates an authority request that covers every glyph in the target.</summary>
        public static TextStudioGlyphAuthorityRequest WholeTarget(
            TextStudioGlyphAuthorityChannels channels)
        {
            return new TextStudioGlyphAuthorityRequest(channels, 0, -1);
        }

        internal bool Overlaps(in TextStudioGlyphAuthorityRequest other)
        {
            if ((Channels & other.Channels) == 0)
                return false;
            if (CoversAllGlyphs || other.CoversAllGlyphs)
                return true;
            long firstEnd = (long)FirstGlyphIndex + GlyphCount;
            long secondEnd = (long)other.FirstGlyphIndex + other.GlyphCount;
            return FirstGlyphIndex < secondEnd && other.FirstGlyphIndex < firstEnd;
        }

        internal bool Contains(int glyphIndex)
        {
            return glyphIndex >= FirstGlyphIndex &&
                   (CoversAllGlyphs || (long)glyphIndex < (long)FirstGlyphIndex + GlyphCount);
        }
    }

    /// <summary>Generation-safe proof of exclusive target authority.</summary>
    public readonly struct TextStudioGlyphAuthorityLease
    {
        readonly TextStudioExtensionRegistry _registry;
        readonly TextStudioGenerationHandle _handle;

        internal TextStudioGlyphAuthorityLease(
            TextStudioExtensionRegistry registry,
            TextStudioGenerationHandle handle)
        {
            _registry = registry;
            _handle = handle;
        }

        internal TextStudioExtensionRegistry Registry => _registry;
        internal TextStudioGenerationHandle Handle => _handle;
        public bool IsValid => _registry != null && _registry.IsAuthorityLeaseCurrent(this);
    }

    public enum TextStudioExtensionRegistrationFailure : byte
    {
        None = 0,
        InvalidTarget = 1,
        InvalidProvider = 2,
        InvalidAuthority = 3,
        AuthorityConflict = 4
    }

    /// <summary>Registration token for one frame composer and its authority lease.</summary>
    public readonly struct TextStudioFrameComposerRegistration
    {
        internal TextStudioFrameComposerRegistration(
            TextStudioGenerationHandle composerHandle,
            TextStudioGlyphAuthorityLease authorityLease)
        {
            ComposerHandle = composerHandle;
            AuthorityLease = authorityLease;
        }

        public TextStudioGenerationHandle ComposerHandle { get; }
        public TextStudioGlyphAuthorityLease AuthorityLease { get; }
        public bool IsValid => ComposerHandle.IsValid && AuthorityLease.IsValid;
    }

    /// <summary>Canonical frame plus the independently composed target receiving it.</summary>
    public readonly struct TextStudioFrameCompositionContext
    {
        internal TextStudioFrameCompositionContext(
            in TextStudioFrame frame,
            in TextStudioFrameTarget target,
            in TextStudioGlyphChangeSet textChanges)
        {
            Frame = frame;
            Target = target;
            TextChanges = textChanges;
        }

        public TextStudioFrame Frame { get; }
        public TextStudioFrameTarget Target { get; }
        public TextStudioGlyphChangeSet TextChanges { get; }
        public uint SourceVersion => Frame.Plan.SourceVersion;
        public int GlyphCount => Frame.GlyphStateCount;
    }

    /// <summary>Rest-relative or absolute pose contribution for one composed glyph.</summary>
    public readonly struct TextStudioGlyphPose
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public Vector3 Scale { get; }

        /// <summary>Creates a glyph pose from position, rotation, and scale values.</summary>
        public TextStudioGlyphPose(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            Position = position;
            float rotationMagnitude =
                rotation.x * rotation.x +
                rotation.y * rotation.y +
                rotation.z * rotation.z +
                rotation.w * rotation.w;
            Rotation = rotationMagnitude <= 0.0000001f
                ? Quaternion.identity
                : Quaternion.Normalize(rotation);
            Scale = scale;
        }
    }

    [Flags]
    public enum TextStudioGlyphVisualResponseChannels : byte
    {
        None = 0,
        Color = 1 << 0,
        Alpha = 1 << 1,
        Scale = 1 << 2
    }

    /// <summary>Optional backend or renderer contract for encoding authored sRGB colors into its native glyph vertex format.</summary>
    public interface ITextStudioGlyphColorEncoder
    {
        /// <summary>Converts an authored sRGB color into the backend or renderer's native glyph vertex representation.</summary>
        Color32 EncodeGlyphColor(Color32 authoredColor);
    }

    /// <summary>Bounded visual response contribution derived from simulation signals.</summary>
    public readonly struct TextStudioGlyphVisualResponse
    {
        public TextStudioGlyphVisualResponseChannels Channels { get; }
        /// <summary>Authored sRGB color; the target encodes it for its native vertex format.</summary>
        public Color32 Color { get; }
        public float AlphaMultiplier { get; }
        public Vector3 ScaleMultiplier { get; }

        /// <summary>Creates a bounded visual response for the selected response channels.</summary>
        public TextStudioGlyphVisualResponse(
            TextStudioGlyphVisualResponseChannels channels,
            Color32 color,
            float alphaMultiplier,
            Vector3 scaleMultiplier)
        {
            Channels = channels;
            Color = color;
            AlphaMultiplier = Mathf.Max(0f, alphaMultiplier);
            ScaleMultiplier = scaleMultiplier;
        }
    }

    /// <summary>Allocation-free writer scoped to one registered authority lease.</summary>
    public readonly struct TextStudioGlyphStateWriter
    {
        readonly TextStudioGlyphInstanceState[] _states;
        readonly GlyphDirtyBits _dirty;
        readonly int _count;
        readonly TextStudioGlyphAuthorityChannels _channels;
        readonly TextStudioGlyphAuthorityRequest _request;

        readonly ITextStudioGlyphColorEncoder _colorEncoder;

        internal TextStudioGlyphStateWriter(
            TextStudioGlyphInstanceState[] states,
            int count,
            GlyphDirtyBits dirty,
            TextStudioGlyphAuthorityChannels channels,
            in TextStudioGlyphAuthorityRequest request,
            ITextStudioGlyphColorEncoder colorEncoder = null)
        {
            _colorEncoder = colorEncoder;
            _states = states;
            _count = count;
            _dirty = dirty;
            _channels = channels;
            _request = request;
        }

        public int Count => _count;
        public TextStudioGlyphAuthorityChannels Channels => _channels;

        /// <summary>Returns the current state of an accessible glyph.</summary>
        public bool TryGetState(int glyphIndex, out TextStudioGlyphInstanceState state)
        {
            if (!CanAccess(glyphIndex))
            {
                state = default;
                return false;
            }

            state = _states[glyphIndex];
            return true;
        }

        /// <summary>Blends a pose into an accessible glyph when transform authority is available.</summary>
        public bool ApplyPose(int glyphIndex, in TextStudioGlyphPose pose, float weight = 1f)
        {
            if ((_channels & TextStudioGlyphAuthorityChannels.Transform) == 0 ||
                !CanAccess(glyphIndex))
            {
                return false;
            }

            float blend = Mathf.Clamp01(weight);
            ref TextStudioGlyphInstanceState state = ref _states[glyphIndex];
            state.position = Vector3.LerpUnclamped(state.position, pose.Position, blend);
            state.rotation = Quaternion.SlerpUnclamped(state.rotation, pose.Rotation, blend);
            state.scale = Vector3.LerpUnclamped(state.scale, pose.Scale, blend);
            state.flags |= GlyphInstanceFlags.Simulated | GlyphInstanceFlags.Dirty;
            _dirty.SetDirty(glyphIndex);
            return true;
        }

        /// <summary>Sets linear and angular velocity for an accessible glyph when velocity authority is available.</summary>
        public bool SetVelocities(
            int glyphIndex,
            Vector3 velocity,
            Vector3 angularVelocity)
        {
            if ((_channels & TextStudioGlyphAuthorityChannels.Velocity) == 0 ||
                !CanAccess(glyphIndex))
            {
                return false;
            }

            ref TextStudioGlyphInstanceState state = ref _states[glyphIndex];
            state.velocity = velocity;
            state.angularVelocity = angularVelocity;
            state.flags |= GlyphInstanceFlags.Simulated | GlyphInstanceFlags.Dirty;
            _dirty.SetDirty(glyphIndex);
            return true;
        }

        /// <summary>Applies color, alpha, or scale response values to an accessible glyph.</summary>
        public bool ApplyVisualResponse(
            int glyphIndex,
            in TextStudioGlyphVisualResponse response)
        {
            if ((_channels & TextStudioGlyphAuthorityChannels.VisualResponse) == 0 ||
                !CanAccess(glyphIndex))
            {
                return false;
            }

            ref TextStudioGlyphInstanceState state = ref _states[glyphIndex];
            if ((response.Channels & TextStudioGlyphVisualResponseChannels.Color) != 0)
            {
                Color32 color = _colorEncoder != null ? _colorEncoder.EncodeGlyphColor(response.Color) : response.Color;
                state.color = color;
                state.vertexColors = TextStudioGlyphColorQuad.Uniform(color);
            }
            if ((response.Channels & TextStudioGlyphVisualResponseChannels.Alpha) != 0)
                state.alpha = Mathf.Clamp01(state.alpha * response.AlphaMultiplier);
            if ((response.Channels & TextStudioGlyphVisualResponseChannels.Scale) != 0)
                state.scale = Vector3.Scale(state.scale, response.ScaleMultiplier);
            state.flags |= GlyphInstanceFlags.Dirty;
            _dirty.SetDirty(glyphIndex);
            return true;
        }

        bool CanAccess(int glyphIndex)
        {
            return _states != null &&
                   glyphIndex >= 0 &&
                   glyphIndex < _count &&
                   _request.Contains(glyphIndex);
        }
    }

    /// <summary>Allocation-free writer for target-scoped typed renderer directives.</summary>
    public readonly struct TextStudioRenderDirectiveWriter
    {
        readonly TextStudioRenderPropertyBuffer _properties;
        readonly GlyphDirtyBits _dirty;
        readonly int _count;
        readonly TextStudioGlyphAuthorityChannels _channels;
        readonly TextStudioGlyphAuthorityRequest _request;

        internal TextStudioRenderDirectiveWriter(
            TextStudioRenderPropertyBuffer properties,
            int count,
            GlyphDirtyBits dirty,
            TextStudioGlyphAuthorityChannels channels,
            in TextStudioGlyphAuthorityRequest request)
        {
            _properties = properties;
            _count = count;
            _dirty = dirty;
            _channels = channels;
            _request = request;
        }

        public int Count => _count;
        public TextStudioGlyphAuthorityChannels Channels => _channels;

        /// <summary>Attempts to blend a renderer property onto one glyph within this writer's visual authority and marks it dirty on success The registered property determines the vector value type.</summary>
        public bool ApplyGlyph(
            int glyphIndex,
            string propertyId,
            Vector4 value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            if (!CanAccess(glyphIndex))
                return false;
            if (!TryCreateVectorValue(
                    propertyId,
                    value,
                    out TextStudioRenderPropertyId resolvedId,
                    out TextStudioRenderPropertyValue resolvedValue))
            {
                return false;
            }
            return ApplyResolvedRange(
                glyphIndex,
                1,
                resolvedId,
                in resolvedValue,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend a renderer property onto one glyph within this writer's visual authority and marks it dirty on success.</summary>
        public bool ApplyGlyph(
            int glyphIndex,
            TextStudioRenderPropertyId propertyId,
            in TextStudioRenderPropertyValue value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            if (!CanAccess(glyphIndex))
                return false;
            return ApplyResolvedRange(
                glyphIndex,
                1,
                propertyId,
                in value,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend an unsigned renderer property onto one authorized glyph and marks it dirty on success.</summary>
        public bool ApplyGlyphUInt(
            int glyphIndex,
            string propertyId,
            uint value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            if (!CanAccess(glyphIndex))
                return false;
            TextStudioRenderPropertyId resolvedId =
                new TextStudioRenderPropertyId(propertyId);
            TextStudioRenderPropertyValue resolvedValue =
                TextStudioRenderPropertyValue.FromUInt(value);
            return ApplyResolvedRange(
                glyphIndex,
                1,
                resolvedId,
                in resolvedValue,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend a renderer property across an authorized glyph range and marks affected glyphs dirty on success The registered property determines the vector value type.</summary>
        public bool ApplyRange(
            int firstGlyphIndex,
            int glyphCount,
            string propertyId,
            Vector4 value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            if (!TryCreateVectorValue(
                    propertyId,
                    value,
                    out TextStudioRenderPropertyId resolvedId,
                    out TextStudioRenderPropertyValue resolvedValue))
            {
                return false;
            }
            return ApplyResolvedRange(
                firstGlyphIndex,
                glyphCount,
                resolvedId,
                in resolvedValue,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend a renderer property across an authorized glyph range and marks affected glyphs dirty on success.</summary>
        public bool ApplyRange(
            int firstGlyphIndex,
            int glyphCount,
            TextStudioRenderPropertyId propertyId,
            in TextStudioRenderPropertyValue value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            return ApplyResolvedRange(
                firstGlyphIndex,
                glyphCount,
                propertyId,
                in value,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend an unsigned renderer property across an authorized glyph range and marks affected glyphs dirty on success.</summary>
        public bool ApplyRangeUInt(
            int firstGlyphIndex,
            int glyphCount,
            string propertyId,
            uint value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            TextStudioRenderPropertyId resolvedId =
                new TextStudioRenderPropertyId(propertyId);
            TextStudioRenderPropertyValue resolvedValue =
                TextStudioRenderPropertyValue.FromUInt(value);
            return ApplyResolvedRange(
                firstGlyphIndex,
                glyphCount,
                resolvedId,
                in resolvedValue,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend a renderer property across all glyphs when the range is covered by this writer's visual authority The registered property determines the vector value type.</summary>
        public bool ApplyAll(
            string propertyId,
            Vector4 value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            return ApplyRange(
                0,
                _count,
                propertyId,
                value,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend a renderer property across all glyphs when the range is covered by this writer's visual authority.</summary>
        public bool ApplyAll(
            TextStudioRenderPropertyId propertyId,
            in TextStudioRenderPropertyValue value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            return ApplyRange(
                0,
                _count,
                propertyId,
                in value,
                influence,
                blendMode,
                surfaceMask);
        }

        /// <summary>Attempts to blend an unsigned renderer property across all glyphs when the range is covered by this writer's visual authority.</summary>
        public bool ApplyAllUInt(
            string propertyId,
            uint value,
            float influence = 1f,
            EffectBlendMode blendMode = EffectBlendMode.Override,
            TextStudioSurfaceMask surfaceMask = TextStudioSurfaceMask.All)
        {
            return ApplyRangeUInt(
                0,
                _count,
                propertyId,
                value,
                influence,
                blendMode,
                surfaceMask);
        }

        bool CanAccess(int glyphIndex)
        {
            return _properties != null &&
                   (_channels & TextStudioGlyphAuthorityChannels.VisualResponse) != 0 &&
                   glyphIndex >= 0 &&
                   glyphIndex < _count &&
                   _request.Contains(glyphIndex);
        }

        bool CanAccessRange(int firstGlyphIndex, int glyphCount)
        {
            return CanAccess(firstGlyphIndex) &&
                   CanAccess(firstGlyphIndex + glyphCount - 1);
        }

        bool ApplyResolvedRange(
            int firstGlyphIndex,
            int glyphCount,
            TextStudioRenderPropertyId propertyId,
            in TextStudioRenderPropertyValue value,
            float influence,
            EffectBlendMode blendMode,
            TextStudioSurfaceMask surfaceMask)
        {
            int first = Mathf.Max(0, firstGlyphIndex);
            if (first >= _count)
                return false;
            int count = Mathf.Clamp(glyphCount, 0, _count - first);
            if (count <= 0 || !CanAccessRange(first, count))
                return false;
            TextStudioRenderPropertyWriter writer =
                _properties.CreateWriter(first, count);
            if (!writer.Apply(
                    propertyId,
                    in value,
                    influence,
                    blendMode,
                    surfaceMask))
            {
                return false;
            }
            for (int glyphIndex = first; glyphIndex < first + count; glyphIndex++)
                _dirty.SetDirty(glyphIndex);
            return true;
        }

        static bool TryCreateVectorValue(
            string propertyId,
            Vector4 value,
            out TextStudioRenderPropertyId resolvedId,
            out TextStudioRenderPropertyValue resolvedValue)
        {
            resolvedId = new TextStudioRenderPropertyId(propertyId);
            if (!TextStudioRenderPropertyRegistry.TryGetRegistration(
                    propertyId,
                    out TextStudioRenderPropertyRegistration registration) ||
                registration.Descriptor.ValueType == TextStudioRenderPropertyValueType.UInt)
            {
                resolvedValue = default;
                return false;
            }
            resolvedValue = TextStudioRenderPropertyValue.FromVector(
                registration.Descriptor.ValueType,
                value);
            return true;
        }
    }

    /// <summary>Target-scoped stateful pose and response contributor.</summary>
    public interface ITextStudioFrameComposer
    {
        bool IsActive { get; }
        int Order { get; }
        /// <summary>Composes target-scoped glyph state into the canonical frame.</summary>
        void Compose(
            in TextStudioFrameCompositionContext context,
            in TextStudioGlyphStateWriter glyphs,
            in TextStudioRenderDirectiveWriter renderProperties);
    }

    public enum TextStudioGlyphGeometryDetail : byte
    {
        Fast = 0,
        Accurate = 1
    }

    [Flags]
    public enum TextStudioGlyphGeometryCapabilities : byte
    {
        None = 0,
        Bounds = 1 << 0,
        PlanarConvexShapes = 1 << 1,
        SpatialConvexShapes = 1 << 2
    }

    /// <summary>Allocation-free geometry request for a renderer-neutral glyph identity.</summary>
    public readonly struct TextStudioGlyphGeometryRequest
    {
        public TextStudioFrameTarget Target { get; }
        public TextStudioGlyphIdentity Glyph { get; }
        public TextStudioGlyphGeometryDetail Detail { get; }

        /// <summary>Creates a geometry request for one glyph and requested detail level.</summary>
        public TextStudioGlyphGeometryRequest(
            in TextStudioFrameTarget target,
            in TextStudioGlyphIdentity glyph,
            TextStudioGlyphGeometryDetail detail)
        {
            Target = target;
            Glyph = glyph;
            Detail = detail;
        }
    }

    /// <summary>Reusable destination for provider-supplied convex glyph geometry.</summary>
    public sealed class TextStudioGlyphGeometryBuffer
    {
        Vector3[] _vertices = Array.Empty<Vector3>();
        int[] _shapeStarts = Array.Empty<int>();
        int[] _shapeCounts = Array.Empty<int>();
        int _vertexCount;
        int _shapeCount;
        int _openShapeStart = -1;
        Bounds _bounds;
        bool _hasBounds;

        public TextStudioReadOnlyBuffer<Vector3> Vertices =>
            new TextStudioReadOnlyBuffer<Vector3>(_vertices, _vertexCount);
        public TextStudioReadOnlyBuffer<int> ShapeStarts =>
            new TextStudioReadOnlyBuffer<int>(_shapeStarts, _shapeCount);
        public TextStudioReadOnlyBuffer<int> ShapeCounts =>
            new TextStudioReadOnlyBuffer<int>(_shapeCounts, _shapeCount);
        public int VertexCount => _vertexCount;
        public int ShapeCount => _shapeCount;
        public Bounds Bounds => _bounds;
        public bool HasBounds => _hasBounds;
        public bool IsEmpty => _shapeCount == 0 && !_hasBounds;

        /// <summary>Removes all geometry and bounds from the reusable buffer.</summary>
        public void Clear()
        {
            _vertexCount = 0;
            _shapeCount = 0;
            _openShapeStart = -1;
            _bounds = default;
            _hasBounds = false;
        }

        /// <summary>Sets explicit bounds for the supplied glyph geometry.</summary>
        public void SetBounds(Bounds bounds)
        {
            _bounds = bounds;
            _hasBounds = true;
        }

        /// <summary>Begins a convex shape and optionally reserves space for its vertices.</summary>
        public bool BeginConvexShape(int expectedVertexCount = 0)
        {
            if (_openShapeStart >= 0)
                return false;
            EnsureShapeCapacity(_shapeCount + 1);
            EnsureVertexCapacity(_vertexCount + Mathf.Max(0, expectedVertexCount));
            _openShapeStart = _vertexCount;
            return true;
        }

        /// <summary>Adds a vertex to the convex shape currently being written.</summary>
        public bool AddVertex(Vector3 vertex)
        {
            if (_openShapeStart < 0)
                return false;
            EnsureVertexCapacity(_vertexCount + 1);
            _vertices[_vertexCount++] = vertex;
            if (!_hasBounds)
            {
                _bounds = new Bounds(vertex, Vector3.zero);
                _hasBounds = true;
            }
            else
            {
                _bounds.Encapsulate(vertex);
            }
            return true;
        }

        /// <summary>Completes the current convex shape when it contains at least three vertices.</summary>
        public bool EndConvexShape()
        {
            if (_openShapeStart < 0)
                return false;
            int count = _vertexCount - _openShapeStart;
            if (count < 3)
            {
                _vertexCount = _openShapeStart;
                _openShapeStart = -1;
                return false;
            }
            _shapeStarts[_shapeCount] = _openShapeStart;
            _shapeCounts[_shapeCount] = count;
            _shapeCount++;
            _openShapeStart = -1;
            return true;
        }

        void EnsureVertexCapacity(int count)
        {
            if (_vertices.Length >= count)
                return;
            Array.Resize(ref _vertices, TextStudioBufferCapacity.ForCount(count));
        }

        void EnsureShapeCapacity(int count)
        {
            if (_shapeStarts.Length >= count)
                return;
            int capacity = TextStudioBufferCapacity.ForCount(count);
            Array.Resize(ref _shapeStarts, capacity);
            Array.Resize(ref _shapeCounts, capacity);
        }
    }

    /// <summary>Optional exact-outline or compound-convex geometry provider.</summary>
    public interface ITextStudioGlyphGeometryProvider
    {
        bool IsAvailable { get; }
        int GeometryVersion { get; }
        TextStudioGlyphGeometryCapabilities Capabilities { get; }
        /// <summary>Writes glyph geometry into a reusable destination when the request is supported.</summary>
        bool TryWriteGeometry(
            in TextStudioGlyphGeometryRequest request,
            TextStudioGlyphGeometryBuffer destination);
    }

    /// <summary>Bounded per-glyph physical signal values for visual response mapping.</summary>
    public readonly struct TextStudioGlyphSignalState
    {
        public float LinearSpeed { get; }
        public float AngularSpeed { get; }
        public float Impact { get; }
        public float Contact { get; }
        public float Stretch { get; }
        public float Tension { get; }
        public float DistanceFromRest { get; }
        public bool IsSleeping { get; }

        /// <summary>Creates a bounded set of physical signal values for one glyph.</summary>
        public TextStudioGlyphSignalState(
            float linearSpeed,
            float angularSpeed,
            float impact,
            float contact,
            float stretch,
            float tension,
            float distanceFromRest,
            bool isSleeping)
        {
            LinearSpeed = Mathf.Max(0f, linearSpeed);
            AngularSpeed = Mathf.Max(0f, angularSpeed);
            Impact = Mathf.Max(0f, impact);
            Contact = Mathf.Max(0f, contact);
            Stretch = stretch;
            Tension = Mathf.Max(0f, tension);
            DistanceFromRest = Mathf.Max(0f, distanceFromRest);
            IsSleeping = isSleeping;
        }
    }

    /// <summary>Provides target-scoped physical signal values for individual glyphs.</summary>
    public interface ITextStudioGlyphSignalProvider
    {
        bool IsAvailable { get; }
        int SignalVersion { get; }
        /// <summary>Returns the current physical signal values for the requested glyph.</summary>
        bool TryGetSignals(int glyphIndex, out TextStudioGlyphSignalState signals);
    }

    public enum TextStudioStatefulPreviewFidelity : byte
    {
        Exact = 0,
        ResetAndReplay = 1,
        Checkpointed = 2,
        Approximate = 3
    }

    public enum TextStudioStatefulPreviewSeekMode : byte
    {
        PreferFast = 0,
        PreferExact = 1
    }

    /// <summary>Stateful preview lifecycle for simulations and other history-dependent extensions.</summary>
    public interface ITextStudioStatefulPreviewProvider
    {
        bool IsAvailable { get; }
        bool SupportsRandomAccess { get; }
        TextStudioStatefulPreviewFidelity Fidelity { get; }
        double CurrentTime { get; }
        /// <summary>Resets preview state to its initial time and values.</summary>
        void ResetPreview();
        /// <summary>Advances preview state by the supplied time interval.</summary>
        void StepPreview(float deltaTime);
        /// <summary>Requests a preview time. True means accepted; incremental providers can finish over later updates.</summary>
        bool SeekPreview(double time, TextStudioStatefulPreviewSeekMode mode);
    }

    /// <summary>Progress and cancellation for stateful preview work spread over multiple updates.</summary>
    public interface ITextStudioIncrementalPreviewProvider : ITextStudioStatefulPreviewProvider
    {
        bool IsSeeking { get; }
        float SeekProgress { get; }
        /// <summary>Cancels pending incremental preview seeking without requesting another simulation step.</summary>
        void CancelSeek();
        /// <summary>Runs bounded work, checking the time budget between indivisible simulation steps.</summary>
        void AdvanceSeek(float budgetMilliseconds = 4f);
    }

    public enum TextStudioExtensionProviderKind : byte
    {
        Geometry = 0,
        Signals = 1,
        StatefulPreview = 2
    }

    /// <summary>Optional target-scoped owner of physical semantic-unit reactions.</summary>
    public interface ITextStudioReactionBodyProvider
    {
        bool IsAvailable { get; }
        /// <summary>Reports whether this physical owner can release the specified semantic unit at its current pose.</summary>
        bool CanReleaseSemanticUnit(int unitIndex);
        /// <summary>Release at the current pose, adding world-space launch velocities to existing motion.</summary>
        bool TryReleaseSemanticUnit(int unitIndex, Vector3 velocity,
            Vector3 angularVelocity, out TextStudioReactionBodyEventSession session);
    }

    /// <summary>Generation-safe token for a registered neutral extension provider.</summary>
    public readonly struct TextStudioExtensionProviderRegistration
    {
        internal TextStudioExtensionProviderRegistration(
            TextStudioExtensionProviderKind kind,
            TextStudioGenerationHandle handle)
        {
            Kind = kind;
            Handle = handle;
        }

        public TextStudioExtensionProviderKind Kind { get; }
        public TextStudioGenerationHandle Handle { get; }
        public bool IsValid => Handle.IsValid;
    }

    /// <summary>
    /// Target-scoped registry for pose composition, exclusive transform authority, exact geometry,
    /// physical signals, and stateful preview. Registration may allocate; warmed frame composition does not.
    /// </summary>
    public sealed class TextStudioExtensionRegistry
    {
        sealed class SlotTable<T> where T : class
        {
            sealed class Slot
            {
                public uint Generation = 1u;
                public T Value;
            }

            readonly List<Slot> _slots = new List<Slot>();
            readonly Stack<int> _freeSlots = new Stack<int>();

            public int SlotCount => _slots.Count;

            public TextStudioGenerationHandle Add(T value)
            {
                int index;
                Slot slot;
                if (_freeSlots.Count > 0)
                {
                    index = _freeSlots.Pop();
                    slot = _slots[index];
                }
                else
                {
                    index = _slots.Count;
                    slot = new Slot();
                    _slots.Add(slot);
                }
                slot.Value = value;
                return new TextStudioGenerationHandle(index, slot.Generation);
            }

            public bool TryGet(TextStudioGenerationHandle handle, out T value)
            {
                if (!handle.IsValid ||
                    handle.Slot >= _slots.Count)
                {
                    value = null;
                    return false;
                }

                Slot slot = _slots[handle.Slot];
                if (slot.Generation != handle.Generation || slot.Value == null)
                {
                    value = null;
                    return false;
                }

                value = slot.Value;
                return true;
            }

            public bool TryGetAt(int slotIndex, out T value)
            {
                if (slotIndex < 0 || slotIndex >= _slots.Count)
                {
                    value = null;
                    return false;
                }
                value = _slots[slotIndex].Value;
                return value != null;
            }

            public bool Remove(TextStudioGenerationHandle handle)
            {
                if (!TryGet(handle, out _))
                    return false;
                Slot slot = _slots[handle.Slot];
                slot.Value = null;
                unchecked
                {
                    slot.Generation++;
                    if (slot.Generation == 0u)
                        slot.Generation = 1u;
                }
                _freeSlots.Push(handle.Slot);
                return true;
            }
        }

        sealed class AuthorityEntry
        {
            public TextStudioFrameTarget Target;
            public object Owner;
            public TextStudioGlyphAuthorityRequest Request;
        }

        sealed class ComposerEntry
        {
            public TextStudioFrameTarget Target;
            public ITextStudioFrameComposer Composer;
            public TextStudioGlyphAuthorityLease Lease;
            public TextStudioGlyphAuthorityRequest Request;
        }

        sealed class ProviderEntry<T> where T : class
        {
            public TextStudioFrameTarget Target;
            public T Provider;
            public int Priority;
        }

        readonly object _owner;
        readonly SlotTable<AuthorityEntry> _authorities = new SlotTable<AuthorityEntry>();
        readonly SlotTable<ComposerEntry> _composers = new SlotTable<ComposerEntry>();
        readonly SlotTable<ProviderEntry<ITextStudioGlyphGeometryProvider>> _geometryProviders =
            new SlotTable<ProviderEntry<ITextStudioGlyphGeometryProvider>>();
        readonly SlotTable<ProviderEntry<ITextStudioGlyphSignalProvider>> _signalProviders =
            new SlotTable<ProviderEntry<ITextStudioGlyphSignalProvider>>();
        readonly SlotTable<ProviderEntry<ITextStudioStatefulPreviewProvider>> _previewProviders =
            new SlotTable<ProviderEntry<ITextStudioStatefulPreviewProvider>>();
        readonly List<TextStudioGenerationHandle> _composerOrder =
            new List<TextStudioGenerationHandle>();
        readonly TextStudioGlyphInstanceBuffer _compositionBuffer =
            new TextStudioGlyphInstanceBuffer();
        readonly GlyphDirtyBits _compositionDirty = new GlyphDirtyBits();
        readonly TextStudioRenderPropertyBuffer _compositionRenderProperties =
            new TextStudioRenderPropertyBuffer();

        internal TextStudioExtensionRegistry(object owner)
        {
            _owner = owner;
        }

        public bool HasActiveFrameComposers
        {
            get
            {
                for (int i = _composerOrder.Count - 1; i >= 0; i--)
                {
                    TextStudioGenerationHandle handle = _composerOrder[i];
                    if (!_composers.TryGet(handle, out ComposerEntry entry))
                    {
                        _composerOrder.RemoveAt(i);
                        continue;
                    }
                    if (!IsProviderUsable(entry.Composer))
                    {
                        RemoveComposerAt(i, handle, entry);
                        continue;
                    }
                    if (entry.Composer.IsActive && IsAuthorityLeaseCurrent(entry.Lease))
                        return true;
                }
                return false;
            }
        }

        /// <summary>Registers a frame composer and grants its requested glyph authority when available.</summary>
        public bool TryRegisterFrameComposer(
            in TextStudioFrameTarget target,
            ITextStudioFrameComposer composer,
            in TextStudioGlyphAuthorityRequest authority,
            out TextStudioFrameComposerRegistration registration,
            out TextStudioExtensionRegistrationFailure failure)
        {
            registration = default;
            if (!OwnsTarget(in target))
            {
                failure = TextStudioExtensionRegistrationFailure.InvalidTarget;
                return false;
            }
            if (composer == null)
            {
                failure = TextStudioExtensionRegistrationFailure.InvalidProvider;
                return false;
            }
            if (!authority.IsValid)
            {
                failure = TextStudioExtensionRegistrationFailure.InvalidAuthority;
                return false;
            }

            for (int slot = 0; slot < _authorities.SlotCount; slot++)
            {
                if (!_authorities.TryGetAt(slot, out AuthorityEntry existing) ||
                    existing.Target != target)
                {
                    continue;
                }
                if (existing.Request.Overlaps(in authority))
                {
                    failure = TextStudioExtensionRegistrationFailure.AuthorityConflict;
                    return false;
                }
            }

            AuthorityEntry authorityEntry = new AuthorityEntry
            {
                Target = target,
                Owner = composer,
                Request = authority
            };
            TextStudioGenerationHandle authorityHandle = _authorities.Add(authorityEntry);
            TextStudioGlyphAuthorityLease lease =
                new TextStudioGlyphAuthorityLease(this, authorityHandle);
            ComposerEntry composerEntry = new ComposerEntry
            {
                Target = target,
                Composer = composer,
                Lease = lease,
                Request = authority
            };
            TextStudioGenerationHandle composerHandle = _composers.Add(composerEntry);
            InsertComposerOrdered(composerHandle, composer.Order);
            registration = new TextStudioFrameComposerRegistration(composerHandle, lease);
            failure = TextStudioExtensionRegistrationFailure.None;
            return true;
        }

        /// <summary>Removes a frame composer registration and releases its authority lease.</summary>
        public bool UnregisterFrameComposer(TextStudioFrameComposerRegistration registration)
        {
            bool removedComposer = _composers.Remove(registration.ComposerHandle);
            if (removedComposer)
                _composerOrder.Remove(registration.ComposerHandle);
            bool removedAuthority = ReleaseAuthority(registration.AuthorityLease);
            return removedComposer || removedAuthority;
        }

        /// <summary>Reports whether a frame composer registration and its authority lease are still current.</summary>
        public bool IsFrameComposerRegistrationCurrent(
            TextStudioFrameComposerRegistration registration)
        {
            return _composers.TryGet(registration.ComposerHandle, out _) &&
                   IsAuthorityLeaseCurrent(registration.AuthorityLease);
        }

        internal bool IsAuthorityLeaseCurrent(TextStudioGlyphAuthorityLease lease)
        {
            return ReferenceEquals(lease.Registry, this) &&
                   _authorities.TryGet(lease.Handle, out _);
        }

        bool ReleaseAuthority(TextStudioGlyphAuthorityLease lease)
        {
            return ReferenceEquals(lease.Registry, this) &&
                   _authorities.Remove(lease.Handle);
        }

        /// <summary>Registers a prioritized glyph geometry provider for a target.</summary>
        public TextStudioExtensionProviderRegistration RegisterGeometryProvider(
            in TextStudioFrameTarget target,
            ITextStudioGlyphGeometryProvider provider,
            int priority = 0)
        {
            return RegisterProvider(
                in target,
                provider,
                priority,
                TextStudioExtensionProviderKind.Geometry,
                _geometryProviders);
        }

        /// <summary>Registers a prioritized glyph signal provider for a target.</summary>
        public TextStudioExtensionProviderRegistration RegisterSignalProvider(
            in TextStudioFrameTarget target,
            ITextStudioGlyphSignalProvider provider,
            int priority = 0)
        {
            return RegisterProvider(
                in target,
                provider,
                priority,
                TextStudioExtensionProviderKind.Signals,
                _signalProviders);
        }

        /// <summary>Registers a prioritized stateful preview provider for a target.</summary>
        public TextStudioExtensionProviderRegistration RegisterStatefulPreviewProvider(
            in TextStudioFrameTarget target,
            ITextStudioStatefulPreviewProvider provider,
            int priority = 0)
        {
            return RegisterProvider(
                in target,
                provider,
                priority,
                TextStudioExtensionProviderKind.StatefulPreview,
                _previewProviders);
        }

        /// <summary>Removes a geometry, signal, or stateful preview provider registration.</summary>
        public bool UnregisterProvider(TextStudioExtensionProviderRegistration registration)
        {
            switch (registration.Kind)
            {
                case TextStudioExtensionProviderKind.Geometry:
                    return _geometryProviders.Remove(registration.Handle);
                case TextStudioExtensionProviderKind.Signals:
                    return _signalProviders.Remove(registration.Handle);
                case TextStudioExtensionProviderKind.StatefulPreview:
                    return _previewProviders.Remove(registration.Handle);
                default:
                    return false;
            }
        }

        /// <summary>Writes glyph geometry using the highest-priority available provider for the target.</summary>
        public bool TryWriteGlyphGeometry(
            in TextStudioGlyphGeometryRequest request,
            TextStudioGlyphGeometryBuffer destination)
        {
            TextStudioFrameTarget target = request.Target;
            if (destination == null || !OwnsTarget(in target))
                return false;
            destination.Clear();
            int selectedPriority = int.MinValue;
            ITextStudioGlyphGeometryProvider selected = null;
            for (int slot = 0; slot < _geometryProviders.SlotCount; slot++)
            {
                if (!_geometryProviders.TryGetAt(
                        slot,
                        out ProviderEntry<ITextStudioGlyphGeometryProvider> entry) ||
                    entry.Target != request.Target ||
                    !IsProviderUsable(entry.Provider) ||
                    !entry.Provider.IsAvailable ||
                    entry.Priority < selectedPriority)
                {
                    continue;
                }
                selected = entry.Provider;
                selectedPriority = entry.Priority;
            }
            return selected != null && selected.TryWriteGeometry(in request, destination);
        }

        /// <summary>Returns glyph signals from the highest-priority available provider for the target.</summary>
        public bool TryGetGlyphSignals(
            in TextStudioFrameTarget target,
            int glyphIndex,
            out TextStudioGlyphSignalState signals)
        {
            signals = default;
            if (!OwnsTarget(in target) || glyphIndex < 0)
                return false;
            int selectedPriority = int.MinValue;
            ITextStudioGlyphSignalProvider selected = null;
            for (int slot = 0; slot < _signalProviders.SlotCount; slot++)
            {
                if (!_signalProviders.TryGetAt(
                        slot,
                        out ProviderEntry<ITextStudioGlyphSignalProvider> entry) ||
                    entry.Target != target ||
                    !IsProviderUsable(entry.Provider) ||
                    !entry.Provider.IsAvailable ||
                    entry.Priority < selectedPriority)
                {
                    continue;
                }
                selected = entry.Provider;
                selectedPriority = entry.Priority;
            }
            return selected != null && selected.TryGetSignals(glyphIndex, out signals);
        }

        /// <summary>Returns the highest-priority available stateful preview provider for the target.</summary>
        public bool TryGetStatefulPreviewProvider(
            in TextStudioFrameTarget target,
            out ITextStudioStatefulPreviewProvider provider)
        {
            provider = null;
            if (!OwnsTarget(in target))
                return false;
            int selectedPriority = int.MinValue;
            for (int slot = 0; slot < _previewProviders.SlotCount; slot++)
            {
                if (!_previewProviders.TryGetAt(
                        slot,
                        out ProviderEntry<ITextStudioStatefulPreviewProvider> entry) ||
                    entry.Target != target ||
                    !IsProviderUsable(entry.Provider) ||
                    !entry.Provider.IsAvailable ||
                    entry.Priority < selectedPriority)
                {
                    continue;
                }
                provider = entry.Provider;
                selectedPriority = entry.Priority;
            }
            return provider != null;
        }

        /// <summary>Whether a current target has a ready physical reaction owner.</summary>
        public bool HasReactionBodyProvider
        {
            get
            {
                if (!Application.isPlaying)
                    return false;
                for (int slot = 0; slot < _signalProviders.SlotCount; slot++)
                    if (_signalProviders.TryGetAt(slot,
                            out ProviderEntry<ITextStudioGlyphSignalProvider> entry) &&
                        OwnsTarget(in entry.Target) &&
                        IsProviderUsable(entry.Provider) &&
                        entry.Provider.IsAvailable &&
                        entry.Provider is ITextStudioReactionBodyProvider bodyProvider &&
                        bodyProvider.IsAvailable)
                        return true;
                return false;
            }
        }

        /// <summary>Reports whether an available physical provider can release the semantic unit in Play Mode.</summary>
        public bool CanReleaseReactionBody(int unitIndex) =>
            FindReactionBodyProvider(unitIndex) != null;

        /// <summary>Routes one semantic unit to the highest-priority ready physical owner.</summary>
        public bool TryReleaseReactionBody(int unitIndex, Vector3 velocity,
            Vector3 angularVelocity, out TextStudioReactionBodyEventSession session)
        {
            ITextStudioReactionBodyProvider selected =
                FindReactionBodyProvider(unitIndex);
            if (selected != null)
                return selected.TryReleaseSemanticUnit(unitIndex, velocity,
                    angularVelocity, out session);
            session = default;
            return false;
        }

        ITextStudioReactionBodyProvider FindReactionBodyProvider(int unitIndex)
        {
            if (!Application.isPlaying || unitIndex < 0) return null;
            ITextStudioReactionBodyProvider selected = null;
            int selectedPriority = int.MinValue;
            for (int slot = 0; slot < _signalProviders.SlotCount; slot++)
            {
                if (!_signalProviders.TryGetAt(slot,
                        out ProviderEntry<ITextStudioGlyphSignalProvider> entry) ||
                    !OwnsTarget(in entry.Target) ||
                    !IsProviderUsable(entry.Provider) ||
                    !entry.Provider.IsAvailable ||
                    !(entry.Provider is ITextStudioReactionBodyProvider bodyProvider) ||
                    !bodyProvider.IsAvailable ||
                    !bodyProvider.CanReleaseSemanticUnit(unitIndex) ||
                    entry.Priority < selectedPriority)
                    continue;
                selected = bodyProvider;
                selectedPriority = entry.Priority;
            }
            return selected;
        }

        internal bool TryComposeFrame(
            in TextStudioFrame canonicalFrame,
            in TextStudioFrameTarget target,
            in TextStudioGlyphChangeSet textChanges,
            out TextStudioFrame composedFrame)
        {
            composedFrame = canonicalFrame;
            if (!OwnsTarget(in target) ||
                !canonicalFrame.GlyphStates.IsCreated ||
                canonicalFrame.GlyphStateCount <= 0)
            {
                return false;
            }

            bool hasActiveComposer = false;
            for (int i = 0; i < _composerOrder.Count; i++)
            {
                if (!_composers.TryGet(_composerOrder[i], out ComposerEntry entry) ||
                    entry.Target != target ||
                    !IsProviderUsable(entry.Composer) ||
                    !entry.Composer.IsActive ||
                    !IsAuthorityLeaseCurrent(entry.Lease))
                {
                    continue;
                }
                hasActiveComposer = true;
                break;
            }
            if (!hasActiveComposer)
                return false;

            int glyphCount = canonicalFrame.GlyphStateCount;
            _compositionBuffer.CopyFrom(canonicalFrame.GlyphStates, glyphCount);
            _compositionRenderProperties.CopyFrom(
                canonicalFrame.RenderProperties,
                glyphCount);
            _compositionDirty.ClearAll();
            _compositionDirty.SetAllDirty(glyphCount);
            TextStudioFrameCompositionContext context =
                new TextStudioFrameCompositionContext(
                    in canonicalFrame,
                    in target,
                    in textChanges);

            for (int i = 0; i < _composerOrder.Count; i++)
            {
                TextStudioGenerationHandle handle = _composerOrder[i];
                if (!_composers.TryGet(handle, out ComposerEntry entry) ||
                    entry.Target != target ||
                    !IsProviderUsable(entry.Composer) ||
                    !entry.Composer.IsActive ||
                    !IsAuthorityLeaseCurrent(entry.Lease))
                {
                    continue;
                }

                TextStudioGlyphStateWriter writer = new TextStudioGlyphStateWriter(
                    _compositionBuffer.States,
                    glyphCount,
                    _compositionDirty,
                    entry.Request.Channels,
                    in entry.Request,
                    target.Target as ITextStudioGlyphColorEncoder);
                TextStudioRenderDirectiveWriter renderProperties =
                    new TextStudioRenderDirectiveWriter(
                        _compositionRenderProperties,
                        glyphCount,
                        _compositionDirty,
                        entry.Request.Channels,
                        in entry.Request);
                entry.Composer.Compose(
                    in context,
                    in writer,
                    in renderProperties);
            }

            composedFrame = new TextStudioFrame(
                in canonicalFrame,
                _compositionBuffer.States,
                glyphCount,
                _compositionDirty,
                canonicalFrame.GlobalDirty | DirtyFlags.Simulation | DirtyFlags.Visual,
                _compositionRenderProperties.View);
            return true;
        }

        TextStudioExtensionProviderRegistration RegisterProvider<T>(
            in TextStudioFrameTarget target,
            T provider,
            int priority,
            TextStudioExtensionProviderKind kind,
            SlotTable<ProviderEntry<T>> table)
            where T : class
        {
            if (!OwnsTarget(in target) || provider == null)
                return default;
            ProviderEntry<T> entry = new ProviderEntry<T>
            {
                Target = target,
                Provider = provider,
                Priority = priority
            };
            return new TextStudioExtensionProviderRegistration(kind, table.Add(entry));
        }

        void InsertComposerOrdered(TextStudioGenerationHandle handle, int order)
        {
            int insertIndex = _composerOrder.Count;
            for (int i = 0; i < _composerOrder.Count; i++)
            {
                if (!_composers.TryGet(_composerOrder[i], out ComposerEntry existing) ||
                    existing.Composer.Order > order)
                {
                    insertIndex = i;
                    break;
                }
            }
            _composerOrder.Insert(insertIndex, handle);
        }

        void RemoveComposerAt(
            int orderIndex,
            TextStudioGenerationHandle handle,
            ComposerEntry entry)
        {
            _composerOrder.RemoveAt(orderIndex);
            _composers.Remove(handle);
            if (entry != null)
                ReleaseAuthority(entry.Lease);
        }

        bool OwnsTarget(in TextStudioFrameTarget target)
        {
            return target.IsValid && ReferenceEquals(target.Owner, _owner);
        }

        static bool IsProviderUsable(object provider)
        {
            if (provider == null)
                return false;
            return !(provider is UnityEngine.Object unityObject) || unityObject != null;
        }
    }
}
