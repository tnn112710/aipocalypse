using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// A single glyph in the presentation layer.
    /// Separates canonical text (what was authored) from visual text (what is displayed).
    /// Addons like Terminal FX and Mechanical Displays substitute presentation glyphs
    /// while preserving the source text model.
    /// </summary>
    [Serializable]
    public struct PresentationGlyph
    {
        /// <summary>Unicode codepoint or font-specific glyph ID to render.</summary>
        public int glyphId;

        /// <summary>Index into the canonical (source) text this glyph maps to.</summary>
        public int sourceIndex;

        /// <summary>Current substitution state for animated transitions.</summary>
        public SubstitutionState substitutionState;

        /// <summary>Progress of the substitution (0 = source, 1 = target). Used by decrypt/scramble effects.</summary>
        public float substitutionProgress;

        /// <summary>Override tint applied on top of style color. Default: white (no tint).</summary>
        public Color32 tintOverride;

        /// <summary>Alpha multiplier (0–1). Applied after all other alpha calculations.</summary>
        public float alphaMultiplier;

        /// <summary>Bit flags for presentation state.</summary>
        public PresentationFlags flags;

        /// <summary>Reports whether the glyph carries every requested presentation flag.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFlag(PresentationFlags flag) => (flags & flag) != 0;

        /// <summary>Creates a visible presentation glyph with opaque white tint and unchanged alpha.</summary>
        public static PresentationGlyph Default(int glyphId, int sourceIndex)
        {
            return new PresentationGlyph
            {
                glyphId = glyphId,
                sourceIndex = sourceIndex,
                substitutionState = SubstitutionState.Resolved,
                substitutionProgress = 1f,
                tintOverride = new Color32(255, 255, 255, 255),
                alphaMultiplier = 1f,
                flags = PresentationFlags.Visible
            };
        }
    }

    [Flags]
    public enum PresentationFlags : byte
    {
        None        = 0,
        Visible     = 1 << 0,
        Revealed    = 1 << 1,
        Animating   = 1 << 2,
        Substituted = 1 << 3,
        Dirty       = 1 << 4
    }

    public enum SubstitutionState : byte
    {
        Resolved,
        Scrambling,
        Decrypting,
        Encrypting,
        Corrupted,
        Custom
    }
}
