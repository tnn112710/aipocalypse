using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Selects logical text order or a spatial direction for ordering selected text units.</summary>
    public enum TextStudioSequenceDirection
    {
        Logical,
        LeftToRight,
        RightToLeft,
        TopToBottom,
        BottomToTop,
        TopLeftToBottomRight,
        TopRightToBottomLeft,
        BottomLeftToTopRight,
        BottomRightToTopLeft
    }

    /// <summary>Chooses forward, backward, center-out, edges-in, simultaneous, or deterministic random ordering for a text-unit sequence.</summary>
    public enum TextStudioSequenceOrder
    {
        Forward,
        Backward,
        CenterOut,
        EdgesIn,
        AllAtOnce,
        SeededRandom
    }

    /// <summary>Serializable ordering, grouping, direction, seed, and stagger spread for reveal or hide sequencing.</summary>
    [Serializable]
    public struct TextStudioSequenceSettings : IEquatable<TextStudioSequenceSettings>
    {
        public TextStudioSequenceOrder Order;
        [Tooltip("Logical follows text order; spatial directions sort units by their original layout position.")]
        public TextStudioSequenceDirection Direction;
        [Min(0f)] public float Spread;
        public int Seed;

        public static TextStudioSequenceSettings Default => new TextStudioSequenceSettings
        {
            Order = TextStudioSequenceOrder.Forward,
            Spread = 1f,
            Seed = 1729
        };

        public static TextStudioSequenceSettings Unstaggered => new TextStudioSequenceSettings
        {
            Order = TextStudioSequenceOrder.AllAtOnce,
            Spread = 0f,
            Seed = 1729
        };

        public bool Equals(TextStudioSequenceSettings other)
        {
            return Direction == other.Direction && Order == other.Order && Spread.Equals(other.Spread) && Seed == other.Seed;
        }

        public override bool Equals(object obj)
        {
            return obj is TextStudioSequenceSettings other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Order, (int)Direction, Spread, Seed);
        }
    }

    internal static class TextStudioSequenceUtility
    {
        public static float ComputeDelay(
            int unitIndex,
            int unitCount,
            in TextStudioSequenceSettings settings)
        {
            float spread = Mathf.Max(0f, settings.Spread);
            if (spread <= 0f || unitCount <= 1 || settings.Order == TextStudioSequenceOrder.AllAtOnce)
                return 0f;

            int clampedIndex = Mathf.Clamp(unitIndex, 0, unitCount - 1);
            float normalizedOrder = ResolveNormalizedOrderPosition(
                clampedIndex,
                unitCount,
                settings.Order,
                settings.Seed);
            return normalizedOrder * spread;
        }

        public static void BuildOrder(
            int count,
            in TextStudioSequenceSettings settings,
            int[] sequence,
            int[] orderByIndex)
        {
            if (count <= 0 || sequence == null)
                return;

            for (int order = 0; order < count; order++)
                sequence[order] = ResolveIndex(order, count, settings.Order);

            if (settings.Order == TextStudioSequenceOrder.SeededRandom)
                Shuffle(sequence, count, settings.Seed);

            if (orderByIndex != null)
            {
                for (int order = 0; order < count; order++)
                    orderByIndex[sequence[order]] = order;
            }
        }

        static int ResolveIndex(int order, int count, TextStudioSequenceOrder sequenceOrder)
        {
            switch (sequenceOrder)
            {
                case TextStudioSequenceOrder.Backward:
                    return count - 1 - order;
                case TextStudioSequenceOrder.CenterOut:
                {
                    int left = (count - 1) / 2;
                    return order == 0
                        ? left
                        : (order & 1) == 1
                            ? left + (order + 1) / 2
                            : left - order / 2;
                }
                case TextStudioSequenceOrder.EdgesIn:
                    return (order & 1) == 0 ? order / 2 : count - 1 - order / 2;
                default:
                    return order;
            }
        }

        static float ResolveNormalizedOrderPosition(
            int index,
            int count,
            TextStudioSequenceOrder sequenceOrder,
            int seed)
        {
            int order;
            switch (sequenceOrder)
            {
                case TextStudioSequenceOrder.Backward:
                    order = count - 1 - index;
                    break;
                case TextStudioSequenceOrder.CenterOut:
                {
                    int left = (count - 1) / 2;
                    order = index == left
                        ? 0
                        : index > left
                            ? (index - left) * 2 - 1
                            : (left - index) * 2;
                    break;
                }
                case TextStudioSequenceOrder.EdgesIn:
                    order = index <= (count - 1) / 2
                        ? index * 2
                        : (count - 1 - index) * 2 + 1;
                    break;
                case TextStudioSequenceOrder.SeededRandom:
                    return Hash01(index, seed);
                default:
                    order = index;
                    break;
            }

            return count > 1 ? (float)order / (count - 1) : 0f;
        }

        static float Hash01(int index, int seed)
        {
            uint value = unchecked((uint)index * 747796405u + (uint)seed * 2891336453u + 277803737u);
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return (value & 0x00FFFFFFu) / 16777216f;
        }

        static void Shuffle(int[] values, int count, int seed)
        {
            uint state = unchecked((uint)seed * 747796405u + (uint)count * 2891336453u);
            for (int i = count - 1; i > 0; i--)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                int swapIndex = (int)(state % (uint)(i + 1));
                int value = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = value;
            }
        }
    }
}
