namespace TextStudio.Editor
{
    internal static class TextStudioPlacementInspector
    {
        const string AutoFitRadius = "autoFitRadius";
        const string AutoFitPadding = "autoFitPadding";
        const string Radius = "radius";
        const string ArcAngle = "arcAngle";
        const string StartAngle = "startAngle";
        const string PathPositionField = "pathPosition";
        const string Spacing = "spacing";
        const string Fill = "fill";

        static readonly string[] s_arcAutoFitFields =
        {
            AutoFitRadius,
            AutoFitPadding,
            ArcAngle,
            PathPositionField,
            StartAngle,
            Spacing,
            Fill
        };

        static readonly string[] s_arcManualRadiusFields =
        {
            AutoFitRadius,
            Radius,
            ArcAngle,
            PathPositionField,
            StartAngle,
            Spacing,
            Fill
        };

        static readonly string[] s_circleAutoFitFields =
        {
            AutoFitRadius,
            AutoFitPadding,
            PathPositionField,
            StartAngle
        };

        static readonly string[] s_circleManualRadiusFields =
        {
            AutoFitRadius,
            Radius,
            PathPositionField,
            StartAngle
        };

        internal static string[] GetArcPlacementVisibleFields(bool autoFitRadius)
        {
            return autoFitRadius ? s_arcAutoFitFields : s_arcManualRadiusFields;
        }

        internal static string[] GetCirclePlacementVisibleFields(bool autoFitRadius)
        {
            return autoFitRadius ? s_circleAutoFitFields : s_circleManualRadiusFields;
        }

        internal static bool IsManualRadiusVisible(
            bool autoFitRadius,
            bool hasMixedAutoFitRadius)
        {
            return hasMixedAutoFitRadius || !autoFitRadius;
        }

        internal static bool IsAutoFitPaddingVisible(
            bool autoFitRadius,
            bool hasMixedAutoFitRadius)
        {
            return hasMixedAutoFitRadius || autoFitRadius;
        }

        internal static bool IsArcFillVisible(
            PathSpacing spacing,
            bool hasMixedSpacing)
        {
            return hasMixedSpacing || spacing == PathSpacing.Proportional;
        }

        internal static bool IsGridCellSizeVisible(
            GridCellSizing cellSizing,
            bool hasMixedCellSizing)
        {
            return hasMixedCellSizing || cellSizing == GridCellSizing.Fixed;
        }

        internal static bool IsGridRespectSourceLinesVisible(
            GridFlow flow,
            bool hasMixedFlow)
        {
            return hasMixedFlow || flow == GridFlow.RowsFirst;
        }
    }
}
