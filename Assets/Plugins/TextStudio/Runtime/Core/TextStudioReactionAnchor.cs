using UnityEngine;

namespace TextStudio
{
    /// <summary>Coordinate system used by a resolved reaction anchor.</summary>
    public enum TextStudioReactionAnchorSpace
    {
        /// <summary>Position and rotation are in Unity world space.</summary>
        World,
        /// <summary>Position and rotation are in the UI Toolkit panel coordinate system.</summary>
        Panel
    }

    /// <summary>A semantic text unit pose resolved by its current renderer host.</summary>
    public readonly struct TextStudioReactionAnchor
    {
        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public TextStudioReactionAnchorSpace Space { get; }

        internal TextStudioReactionAnchor(
            Vector3 position,
            Quaternion rotation,
            TextStudioReactionAnchorSpace space)
        {
            Position = position;
            Rotation = rotation;
            Space = space;
        }
    }

    /// <summary>Optional native renderer pose for semantic reaction units.</summary>
    public interface ITextStudioReactionAnchorProvider
    {
        /// <summary>Attempts to resolve a visual anchor for the specified semantic unit index.</summary>
        bool TryGetReactionAnchor(int unitIndex, out TextStudioReactionAnchor anchor);
    }

    /// <summary>Optional renderer-defined anchor such as a 3D glyph socket.</summary>
    public interface ITextStudioReactionNamedAnchorProvider
    {
        /// <summary>Attempts to resolve the named visual anchor for a semantic unit index.</summary>
        bool TryGetReactionAnchor(int unitIndex, string anchorKey,
            out TextStudioReactionAnchor anchor);
    }

    /// <summary>Optional host route for actions that select a native renderer anchor.</summary>
    public interface ITextStudioReactionNamedAnchorHost
    {
        /// <summary>Attempts to resolve a named anchor for the semantic unit in the supplied reaction context.</summary>
        bool TryGetReactionAnchor(in TextStudioReactionContext context, string anchorKey,
            out TextStudioReactionAnchor anchor);
    }

    internal static class TextStudioReactionAnchorUtility
    {
        internal static bool TryResolveUnitPose(
            int unitIndex,
            Vector3[] basePositions,
            int unitCount,
            GlyphPlacement[] placements,
            int placementCount,
            EvaluatedGlyphState[] states,
            int stateCount,
            float[] effectUnitScales,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            int count = basePositions != null
                ? Mathf.Min(Mathf.Max(0, unitCount), basePositions.Length)
                : 0;
            if ((uint)unitIndex >= (uint)count)
                return false;

            position = basePositions[unitIndex];
            Vector3 placementScale = Vector3.one;
            Quaternion placementRotation = Quaternion.identity;
            int safePlacementCount = placements != null
                ? Mathf.Min(Mathf.Max(0, placementCount), placements.Length)
                : 0;
            if ((uint)unitIndex < (uint)safePlacementCount)
            {
                ref GlyphPlacement placement = ref placements[unitIndex];
                position = placement.position;
                placementRotation = placement.rotation;
                placementScale = placement.scale;
            }

            int safeStateCount = states != null
                ? Mathf.Min(Mathf.Max(0, stateCount), states.Length)
                : 0;
            EvaluatedGlyphState state = (uint)unitIndex < (uint)safeStateCount
                ? states[unitIndex]
                : EvaluatedGlyphState.Identity;
            float unitScale = effectUnitScales != null && unitIndex < effectUnitScales.Length
                ? Mathf.Max(0.0001f, effectUnitScales[unitIndex])
                : 1f;
            if (state.positionOffset != Vector3.zero)
                position += placementRotation * Vector3.Scale(
                    placementScale,
                    state.positionOffset * unitScale);
            rotation = state.rotation == Quaternion.identity
                ? placementRotation
                : placementRotation * state.rotation;
            return true;
        }

        internal static bool TryResolveTextCenter(
            Vector3[] basePositions,
            int count,
            GlyphPlacement[] placements,
            int placementCount,
            EvaluatedGlyphState[] states,
            int stateCount,
            float[] effectUnitScales,
            out Vector3 center)
        {
            center = Vector3.zero;
            count = basePositions != null ? Mathf.Min(Mathf.Max(0, count), basePositions.Length) : 0;
            if (count <= 0)
                return false;

            TryResolveUnitPose(
                0,
                basePositions,
                count,
                placements,
                placementCount,
                states,
                stateCount,
                effectUnitScales,
                out Vector3 first,
                out _);
            Vector3 minimum = first;
            Vector3 maximum = minimum;
            for (int i = 1; i < count; i++)
            {
                TryResolveUnitPose(
                    i,
                    basePositions,
                    count,
                    placements,
                    placementCount,
                    states,
                    stateCount,
                    effectUnitScales,
                    out Vector3 position,
                    out _);
                minimum = Vector3.Min(minimum, position);
                maximum = Vector3.Max(maximum, position);
            }
            center = (minimum + maximum) * 0.5f;
            return true;
        }
    }
}
