using System;

namespace TextStudio
{
    /// <summary>Logical glyph surfaces that a renderer property may target.</summary>
    [Flags]
    public enum TextStudioSurfaceMask : ushort
    {
        None = 0,
        Front = 1 << 0,
        Back = 1 << 1,
        Sides = 1 << 2,
        Edges = 1 << 3,
        InnerSides = 1 << 4,
        Decorations = 1 << 5,
        FrontRelief = 1 << 6,
        FrontBevel = 1 << 7,
        BackBevel = 1 << 8,
        All = Front | Back | Sides | Edges | InnerSides | Decorations |
              FrontRelief | FrontBevel | BackBevel
    }
}
