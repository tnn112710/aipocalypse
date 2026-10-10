using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Renderer-neutral local glyph bounds plus source visibility metadata.</summary>
    public readonly struct TextStudioGlyphVisualBounds
    {
        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }
        public bool IsVisible { get; }

        public bool IsValid =>
            Maximum.x >= Minimum.x &&
            Maximum.y >= Minimum.y;

        public Vector2 Center => (Minimum + Maximum) * 0.5f;

        /// <summary>Creates normalized minimum and maximum bounds.</summary>
        public TextStudioGlyphVisualBounds(
            Vector2 minimum,
            Vector2 maximum,
            bool isVisible)
        {
            Minimum = Vector2.Min(minimum, maximum);
            Maximum = Vector2.Max(minimum, maximum);
            IsVisible = isVisible;
        }
    }

    /// <summary>
    /// Renderer-neutral source colors for the four canonical corners of a glyph.
    /// The order matches TMP quads: bottom-left, top-left, top-right, bottom-right.
    /// </summary>
    public readonly struct TextStudioGlyphColorQuad
    {
        static readonly Color32 s_white = new Color32(255, 255, 255, 255);

        public static TextStudioGlyphColorQuad White => Uniform(s_white);

        public Color32 BottomLeft { get; }
        public Color32 TopLeft { get; }
        public Color32 TopRight { get; }
        public Color32 BottomRight { get; }

        public bool IsUniform =>
            ColorsEqual(BottomLeft, TopLeft) &&
            ColorsEqual(BottomLeft, TopRight) &&
            ColorsEqual(BottomLeft, BottomRight);

        public bool IsInitialized =>
            !ColorsEqual(BottomLeft, default) ||
            !ColorsEqual(TopLeft, default) ||
            !ColorsEqual(TopRight, default) ||
            !ColorsEqual(BottomRight, default);

        // Older Unity versions inherit the managed, boxing Color32.Equals(object) implementation.
        static bool ColorsEqual(Color32 first, Color32 second) =>
            first.r == second.r && first.g == second.g && first.b == second.b && first.a == second.a;

        /// <summary>Creates source colors in canonical bottom-left, top-left, top-right, bottom-right order.</summary>
        public TextStudioGlyphColorQuad(
            Color32 bottomLeft,
            Color32 topLeft,
            Color32 topRight,
            Color32 bottomRight)
        {
            BottomLeft = bottomLeft;
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
        }

        /// <summary>Creates a quad with one color at every corner.</summary>
        public static TextStudioGlyphColorQuad Uniform(Color32 color)
        {
            return new TextStudioGlyphColorQuad(color, color, color, color);
        }

        /// <summary>Interpolates corresponding corners with a clamped normalized amount.</summary>
        public static TextStudioGlyphColorQuad Lerp(
            in TextStudioGlyphColorQuad first,
            in TextStudioGlyphColorQuad second,
            float amount)
        {
            float t = Mathf.Clamp01(amount);
            return new TextStudioGlyphColorQuad(
                Color32.Lerp(first.BottomLeft, second.BottomLeft, t),
                Color32.Lerp(first.TopLeft, second.TopLeft, t),
                Color32.Lerp(first.TopRight, second.TopRight, t),
                Color32.Lerp(first.BottomRight, second.BottomRight, t));
        }

        /// <summary>Applies evaluated color composition independently to all source corners.</summary>
        public TextStudioGlyphColorQuad Resolve(in EvaluatedGlyphState state)
        {
            return new TextStudioGlyphColorQuad(
                state.ResolveColor(BottomLeft),
                state.ResolveColor(TopLeft),
                state.ResolveColor(TopRight),
                state.ResolveColor(BottomRight));
        }

        /// <summary>Bilaterally samples the quad at clamped normalized coordinates.</summary>
        public Color32 Sample(float normalizedX, float normalizedY)
        {
            float x = Mathf.Clamp01(normalizedX);
            float y = Mathf.Clamp01(normalizedY);
            Color bottom = Color.Lerp((Color)BottomLeft, (Color)BottomRight, x);
            Color top = Color.Lerp((Color)TopLeft, (Color)TopRight, x);
            return (Color32)Color.Lerp(bottom, top, y);
        }
    }

    /// <summary>
    /// Renderer-neutral local coordinates used to sample a glyph's source corner colors.
    /// TMP-backed bounds are relative to the character origin and baseline so decoration-only
    /// geometry samples the same vertical gradient as visible glyph geometry.
    /// </summary>
    public readonly struct TextStudioGlyphColorBounds
    {
        public Vector2 Minimum { get; }
        public Vector2 Maximum { get; }

        public bool IsValid =>
            Maximum.x - Minimum.x > 0.0001f &&
            Maximum.y - Minimum.y > 0.0001f;

        /// <summary>Creates normalized source-color sampling bounds.</summary>
        public TextStudioGlyphColorBounds(Vector2 minimum, Vector2 maximum)
        {
            Minimum = Vector2.Min(minimum, maximum);
            Maximum = Vector2.Max(minimum, maximum);
        }

        /// <summary>Returns bounds with a nonzero sorted horizontal sampling range.</summary>
        public TextStudioGlyphColorBounds WithHorizontalRange(float first, float second)
        {
            float minimum = Mathf.Min(first, second);
            float maximum = Mathf.Max(first, second);
            if (maximum - minimum <= 0.0001f)
                maximum = minimum + 0.0001f;
            return new TextStudioGlyphColorBounds(
                new Vector2(minimum, Minimum.y),
                new Vector2(maximum, Maximum.y));
        }
    }

    [Flags]
    public enum GlyphInstanceFlags : byte
    {
        None = 0,
        Visible = 1 << 0,
        Dirty = 1 << 1,
        Simulated = 1 << 2
    }

    /// <summary>Complete renderer-neutral source, placement, animation, color, and simulation state for one glyph.</summary>
    public struct TextStudioGlyphInstanceState
    {
        public int glyphIndex;
        public int resolvedStart;
        public int resolvedLength;
        public int sourceStart;
        public int sourceLength;
        public Vector3 basePosition;
        public Vector3 placedPosition;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public Vector2 skew;
        public Vector2 pivotOffset;
        public TextStudioCornerOffsets cornerOffsets;
        public Vector2 uvOffset;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public Color32 color;
        public float alpha;
        public AnimationPhase phase;
        public float phaseProgress;
        public TextStudioRenderChannels renderChannels;
        public TextStudioGlyphColorQuad vertexColors;
        public GlyphInstanceFlags flags;

        public bool IsDirty => (flags & GlyphInstanceFlags.Dirty) != 0;
        public bool IsVisible => (flags & GlyphInstanceFlags.Visible) != 0;

        public Vector2 Pivot
        {
            get => pivotOffset + new Vector2(0.5f, 0.5f);
            set => pivotOffset = value - new Vector2(0.5f, 0.5f);
        }
    }

    internal sealed class TextStudioGlyphInstanceBuffer
    {
        TextStudioGlyphInstanceState[] _states = Array.Empty<TextStudioGlyphInstanceState>();
        int _count;

        public TextStudioGlyphInstanceState[] States => _states;
        public int Count => _count;

        public void EnsureCapacity(int count)
        {
            if (_states.Length >= count) return;
            Array.Resize(ref _states, TextStudioBufferCapacity.ForCount(count));
        }

        public void Clear()
        {
            _count = 0;
        }

        public void CopyFrom(
            TextStudioReadOnlyBuffer<TextStudioGlyphInstanceState> source,
            int count)
        {
            int safeCount = source.IsCreated
                ? Mathf.Min(Mathf.Max(0, count), source.Length)
                : 0;
            EnsureCapacity(safeCount);
            _count = safeCount;
            for (int i = 0; i < safeCount; i++)
                _states[i] = source[i];
        }

        public void Build(
            TextStudioCompiledPlan plan,
            Vector3[] basePositions,
            GlyphPlacement[] placements,
            int placementCount,
            PlacementMode placementMode,
            EvaluatedGlyphState[] animated,
            int animatedCount,
            GlyphDirtyBits dirty)
        {
            Build(
                plan,
                basePositions,
                placements,
                placementCount,
                placementMode,
                animated,
                animatedCount,
                null,
                null,
                dirty);
        }

        public void Build(
            TextStudioCompiledPlan plan,
            Vector3[] basePositions,
            GlyphPlacement[] placements,
            int placementCount,
            PlacementMode placementMode,
            EvaluatedGlyphState[] animated,
            int animatedCount,
            TextStudioGlyphColorQuad[] sourceColors,
            float[] effectUnitScales,
            GlyphDirtyBits dirty)
        {
            int count = animatedCount;
            EnsureCapacity(count);
            _count = count;

            bool usePlacement = placements != null &&
                placementCount > 0;
            bool allDirty = dirty == null || dirty.AreAllDirty(count);

            for (int i = 0; i < count; i++)
            {
                Vector3 basePosition = basePositions != null && i < basePositions.Length
                    ? basePositions[i]
                    : Vector3.zero;

                Vector3 placedPosition = basePosition;
                Quaternion placementRotation = Quaternion.identity;
                Vector3 placementScale = Vector3.one;
                if (usePlacement && i < placementCount)
                {
                    ref GlyphPlacement placement = ref placements[i];
                    placedPosition = placement.position;
                    placementRotation = placement.rotation;
                    placementScale = placement.scale;
                }

                EvaluatedGlyphState state = animated != null && i < animated.Length
                    ? animated[i]
                    : EvaluatedGlyphState.Identity;
                float effectUnitScale = effectUnitScales != null && i < effectUnitScales.Length
                    ? Mathf.Max(0.0001f, effectUnitScales[i])
                    : 1f;
                TextStudioTextUnit textUnit = plan != null
                    ? plan.GetTextUnit(i)
                    : default;
                Vector3 position = state.positionOffset == Vector3.zero
                    ? placedPosition
                    : placedPosition + placementRotation * Vector3.Scale(
                        placementScale,
                        state.positionOffset * effectUnitScale);
                Quaternion rotation = state.rotation == Quaternion.identity
                    ? placementRotation
                    : placementRotation * state.rotation;
                Vector3 scale = state.scale == Vector3.one
                    ? placementScale
                    : Vector3.Scale(placementScale, state.scale);
                TextStudioGlyphColorQuad sourceColor = sourceColors != null && i < sourceColors.Length
                    ? sourceColors[i]
                    : TextStudioGlyphColorQuad.White;
                TextStudioGlyphColorQuad vertexColors = sourceColor.Resolve(in state);
                Color32 color = vertexColors.Sample(0.5f, 0.5f);

                GlyphInstanceFlags flags = GlyphInstanceFlags.Visible;
                if (allDirty || dirty.IsDirty(i)) flags |= GlyphInstanceFlags.Dirty;
                if (state.alpha <= 0.0001f) flags &= ~GlyphInstanceFlags.Visible;

                _states[i] = new TextStudioGlyphInstanceState
                {
                    glyphIndex = i,
                    resolvedStart = plan != null ? textUnit.resolvedStart : i,
                    resolvedLength = plan != null ? textUnit.resolvedLength : 1,
                    sourceStart = plan != null ? textUnit.sourceStart : i,
                    sourceLength = plan != null ? textUnit.sourceLength : 1,
                    basePosition = basePosition,
                    placedPosition = placedPosition,
                    position = position,
                    rotation = rotation,
                    scale = scale,
                    skew = state.skew,
                    pivotOffset = state.pivotOffset,
                    cornerOffsets = TextStudioCornerOffsets.Scale(in state.cornerOffsets, effectUnitScale),
                    uvOffset = state.uvOffset,
                    velocity = Vector3.zero,
                    angularVelocity = Vector3.zero,
                    color = color,
                    alpha = Mathf.Clamp01(state.alpha),
                    phase = state.currentPhase,
                    phaseProgress = state.phaseProgress,
                    renderChannels = state.renderChannels,
                    vertexColors = vertexColors,
                    flags = flags
                };
            }
        }
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioGlyphInstanceBufferPool
    {
        [ThreadStatic] static TextStudioGlyphInstanceBuffer s_primary;
        [ThreadStatic] static bool s_primaryInUse;

        internal static TextStudioGlyphInstanceBuffer Acquire()
        {
            if (!s_primaryInUse)
            {
                s_primary ??= new TextStudioGlyphInstanceBuffer();
                s_primaryInUse = true;
                return s_primary;
            }

            return new TextStudioGlyphInstanceBuffer();
        }

        internal static void Release(TextStudioGlyphInstanceBuffer buffer)
        {
            if (ReferenceEquals(buffer, s_primary))
                s_primaryInUse = false;
        }
    }
}
