//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

namespace DistantLands.Cozy
{
    public enum SunDisplayStyle
    {
        Disk,
        Texture,
        None
    }
    public enum MoonDisplayStyle
    {
        Texture,
        None
    }
    public enum StarDomePlacementMode
    {
        RotateDailyAndAnnually,
        RotateDaily,
        RotateAnnually,
        DontRotate
    }
    public enum MoonPlacementMode
    {
        Realistic,
        AlwaysOpposeSun,
    }
    public enum MoonPhaseMode
    {
        Realistic,
        Linear,
        None,
    }
    public enum AmbientLightingType
    {
        Gradient,
        Color
    }
}