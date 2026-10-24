using UnityEngine;

namespace TextStudio
{
    internal static partial class TextStudio2DShaderTiers
    {
        static bool s_primitiveProgramsEnabled = true;

        /// <summary>Lab A/B runs and parity tests turn primitive programs off, so every light row draws with its text's program.</summary>
        internal static bool PrimitiveProgramsEnabled
        {
            get => s_primitiveProgramsEnabled;
            set => s_primitiveProgramsEnabled = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetPrimitivePrograms()
        {
            s_primitiveProgramsEnabled = true;
        }

        /// <summary>Whether the paint an operation evaluates in the Field program is exactly its packed colour.</summary>
        internal static bool EvaluatesSolidPaint(CompiledTextStudio2DStyle style, in TextStudio2DCompiledOperation operation)
        {
            // Operations without paint pack no gradient stops, so the general evaluator also returns their packed colour.
            if (!TextStudio2DGpuStylePacking.OperationUsesPaint(operation.Kind))
                return true;
            return operation.PaintIndex >= 0 && operation.PaintIndex < style.PaintCount &&
                   style.GetPaint(operation.PaintIndex).Kind == TextStudio2DPaintKind.Solid;
        }

        internal static bool EvaluatesBasicPaint(CompiledTextStudio2DStyle style, in TextStudio2DCompiledOperation operation) =>
            (operation.Kind == TextStudio2DLayerKind.Fill || operation.Kind == TextStudio2DLayerKind.Stroke) &&
            operation.PaintIndex >= 0 && operation.PaintIndex < style.PaintCount && IsBasicPaint(style.GetPaint(operation.PaintIndex).Kind);
    }

    /// <summary>
    /// Field programs of a style's ordered primitives (AppearanceGpuCost.md). Register allocation follows every path a program
    /// compiles, so each primitive takes the leanest existing variant whose evaluated path equals the text program's: an isolated
    /// operation its family (faux depth with solid paint when its paint is solid), and a light operation the program of its light
    /// run, the operations between two isolated or retained ones: Basic when every one is a fill or stroke with a solid or gradient
    /// paint, solid paint when every paint is solid. A run shares one program, so layer-major output keeps one draw per run, and
    /// glyph-major spans use the union of all light operations so consecutive glyphs keep sharing their run.
    /// </summary>
    internal sealed class TextStudio2DPrimitivePrograms
    {
        CompiledTextStudio2DStyle _style;
        bool _enabled;
        TextStudio2DShaderTier[] _tiers = System.Array.Empty<TextStudio2DShaderTier>();
        bool[] _solid = System.Array.Empty<bool>();
        TextStudio2DShaderTier _spanTier;
        bool _spanSolid;

        internal long ManagedPayloadBytes =>
            TextStudioAppearanceManagedMemory.ArrayBytes(_tiers) + TextStudioAppearanceManagedMemory.ArrayBytes(_solid);

        /// <summary>
        /// The program of primitive <paramref name="operationIndex"/>: one operation, -1 for the native face alone (the text's
        /// program) or -2 for a glyph-major fused span.
        /// </summary>
        internal TextStudio2DShaderTier Select(CompiledTextStudio2DStyle style, int operationIndex, out bool solidPaint)
        {
            solidPaint = false;
            if (style == null || operationIndex == -1 || operationIndex >= style.OperationCount)
                return TextStudio2DShaderTier.Full;
            if (!ReferenceEquals(style, _style) || _enabled != TextStudio2DShaderTiers.PrimitiveProgramsEnabled)
                Resolve(style);
            if (operationIndex < 0)
            {
                solidPaint = _spanSolid;
                return _spanTier;
            }
            solidPaint = _solid[operationIndex];
            return _tiers[operationIndex];
        }

        void Resolve(CompiledTextStudio2DStyle style)
        {
            _style = style;
            _enabled = TextStudio2DShaderTiers.PrimitiveProgramsEnabled;
            int count = style.OperationCount;
            if (_tiers.Length < count)
            {
                _tiers = new TextStudio2DShaderTier[count];
                _solid = new bool[count];
            }
            for (int index = 0; index < count; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                bool isolated = TextStudio2DPrimitiveSupport.IsIsolatedOperation(in operation);
                _tiers[index] = isolated ? TextStudio2DShaderTiers.IsolatedTierFor(operation.Kind) : TextStudio2DShaderTier.Full;
                // Family programs compile no light rows, so faux depth is the only isolated row that evaluates paint.
                _solid[index] = _enabled && isolated && operation.Kind == TextStudio2DLayerKind.FauxDepth &&
                    TextStudio2DShaderTiers.EvaluatesSolidPaint(style, in operation);
            }
            _spanTier = TextStudio2DShaderTier.Full;
            _spanSolid = false;
            if (!_enabled)
                return;
            ResolveRun(style, 0, count, out _spanTier, out _spanSolid);
            for (int start = 0; start < count;)
            {
                int end = start;
                while (end < count && !BreaksRun(in style.OperationAt(end))) end++;
                if (end > start)
                {
                    ResolveRun(style, start, end, out TextStudio2DShaderTier tier, out bool solid);
                    for (int index = start; index < end; index++)
                    {
                        _tiers[index] = tier;
                        _solid[index] = solid;
                    }
                }
                start = end + 1;
            }
        }

        // Isolated operations draw with their family and retained ones with their own materials, so both end a light run.
        static bool BreaksRun(in TextStudio2DCompiledOperation operation) =>
            operation.ExecutionPath != TextStudio2DExecutionPath.FusedField || TextStudio2DPrimitiveSupport.IsIsolatedOperation(in operation);

        static void ResolveRun(CompiledTextStudio2DStyle style, int start, int end, out TextStudio2DShaderTier tier, out bool solid)
        {
            bool basic = true, drawn = false;
            solid = true;
            for (int index = start; index < end; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                // Adjustments and surface treatments draw no primitive of their own; their rows add no paint to this run.
                if (BreaksRun(in operation) || operation.SurfaceOwnerOperationIndex >= 0 ||
                    TextStudio2DColorAdjustmentUtility.IsAdjustment(operation.Kind))
                    continue;
                drawn = true;
                basic &= TextStudio2DShaderTiers.EvaluatesBasicPaint(style, in operation);
                solid &= TextStudio2DShaderTiers.EvaluatesSolidPaint(style, in operation);
            }
            tier = drawn && basic ? TextStudio2DShaderTier.Basic : TextStudio2DShaderTier.Full;
            solid &= drawn;
        }
    }
}
