using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TextStudio
{
    /// <summary>
    /// The record a Hero row's uniform program reads from material constants instead of the style table: the fields a fill,
    /// stroke, shadow or glow record carries, packed by <see cref="TextStudio2DGpuStylePacking.PackOperation"/> with the
    /// arguments the style table packs it with, and up to <see cref="MaximumStops"/> stops of its gradient, packed like the
    /// table's stops from the paint's first stop. Every other field of such a record is zero in the table too.
    /// </summary>
    internal readonly struct TextStudio2DHeroUniformRow : IEquatable<TextStudio2DHeroUniformRow>
    {
        /// <summary>Gradient stops the program holds; longer gradients keep the table program.</summary>
        internal const int MaximumStops = 8;

        internal readonly Vector4 Layer;
        internal readonly Vector4 Execution;
        internal readonly Vector4 Paint;
        internal readonly Vector4 Color;
        internal readonly Vector4 PaintTransform0;
        internal readonly Vector4 PaintTransform1;
        internal readonly Vector4 Parameters0;
        internal readonly Vector4 Parameters1;
        internal readonly Vector4 Parameters2;
        /// <summary>Positions of stops 0 to 3.</summary>
        internal readonly Vector4 StopPositions0;
        /// <summary>Positions of stops 4 to 7.</summary>
        internal readonly Vector4 StopPositions1;
        internal readonly Vector4 StopColor0;
        internal readonly Vector4 StopColor1;
        internal readonly Vector4 StopColor2;
        internal readonly Vector4 StopColor3;
        internal readonly Vector4 StopColor4;
        internal readonly Vector4 StopColor5;
        internal readonly Vector4 StopColor6;
        internal readonly Vector4 StopColor7;

        internal TextStudio2DHeroUniformRow(CompiledTextStudio2DStyle style, int operationIndex)
        {
            ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
            // The table passes a texture slice only for texture and pattern paints, which keep the table program.
            TextStudio2DGpuOperation record = TextStudio2DGpuStylePacking.PackOperation(style, operationIndex, -1,
                TextStudio2DVisualModifierPacking.ResolveOperationFlags(in operation));
            Layer = record.LayerMetadata;
            Execution = record.ExecutionMetadata;
            Paint = record.PaintMetadata;
            Color = record.Color;
            PaintTransform0 = record.PaintTransform0;
            PaintTransform1 = record.PaintTransform1;
            Parameters0 = record.Parameters0;
            Parameters1 = record.Parameters1;
            Parameters2 = record.Parameters2;
            Vector4 positions0 = default, positions1 = default;
            Vector4 color0 = default, color1 = default, color2 = default, color3 = default;
            Vector4 color4 = default, color5 = default, color6 = default, color7 = default;
            int stopCount = 0, stopStart = 0;
            if (TextStudio2DGpuStylePacking.OperationUsesPaint(operation.Kind) && operation.PaintIndex >= 0 &&
                operation.PaintIndex < style.PaintCount)
            {
                TextStudio2DCompiledPaint paint = style.GetPaint(operation.PaintIndex);
                stopStart = paint.GradientStopStart;
                stopCount = Mathf.Clamp(Mathf.Min(paint.GradientStopCount, style.GradientStopCount - stopStart), 0, MaximumStops);
            }
            for (int index = 0; index < stopCount; index++)
            {
                TextStudio2DGpuGradientStop stop = TextStudio2DGpuStylePacking.PackGradient(style.GetGradientStop(stopStart + index));
                float position = stop.Metadata.x;
                switch (index)
                {
                    case 0: positions0.x = position; color0 = stop.Color; break;
                    case 1: positions0.y = position; color1 = stop.Color; break;
                    case 2: positions0.z = position; color2 = stop.Color; break;
                    case 3: positions0.w = position; color3 = stop.Color; break;
                    case 4: positions1.x = position; color4 = stop.Color; break;
                    case 5: positions1.y = position; color5 = stop.Color; break;
                    case 6: positions1.z = position; color6 = stop.Color; break;
                    default: positions1.w = position; color7 = stop.Color; break;
                }
            }
            StopPositions0 = positions0;
            StopPositions1 = positions1;
            StopColor0 = color0;
            StopColor1 = color1;
            StopColor2 = color2;
            StopColor3 = color3;
            StopColor4 = color4;
            StopColor5 = color5;
            StopColor6 = color6;
            StopColor7 = color7;
        }

        // Vector4.Equals compares exactly; the == operator tolerates small differences.
        public bool Equals(TextStudio2DHeroUniformRow other) =>
            Layer.Equals(other.Layer) && Execution.Equals(other.Execution) && Paint.Equals(other.Paint) && Color.Equals(other.Color) &&
            PaintTransform0.Equals(other.PaintTransform0) && PaintTransform1.Equals(other.PaintTransform1) &&
            Parameters0.Equals(other.Parameters0) && Parameters1.Equals(other.Parameters1) && Parameters2.Equals(other.Parameters2) &&
            StopPositions0.Equals(other.StopPositions0) && StopPositions1.Equals(other.StopPositions1) &&
            StopColor0.Equals(other.StopColor0) && StopColor1.Equals(other.StopColor1) && StopColor2.Equals(other.StopColor2) &&
            StopColor3.Equals(other.StopColor3) && StopColor4.Equals(other.StopColor4) && StopColor5.Equals(other.StopColor5) &&
            StopColor6.Equals(other.StopColor6) && StopColor7.Equals(other.StopColor7);

        public override bool Equals(object obj) => obj is TextStudio2DHeroUniformRow other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Layer.GetHashCode();
                hash = hash * 397 ^ Paint.GetHashCode();
                hash = hash * 397 ^ Color.GetHashCode();
                hash = hash * 397 ^ PaintTransform0.GetHashCode();
                hash = hash * 397 ^ PaintTransform1.GetHashCode();
                hash = hash * 397 ^ Parameters0.GetHashCode();
                hash = hash * 397 ^ Parameters1.GetHashCode();
                hash = hash * 397 ^ Parameters2.GetHashCode();
                hash = hash * 397 ^ StopPositions0.GetHashCode();
                hash = hash * 397 ^ StopColor0.GetHashCode();
                return hash * 397 ^ StopColor1.GetHashCode();
            }
        }
    }

    /// <summary>A Hero batch's hold on the shared material of one uniform row.</summary>
    internal struct TextStudio2DHeroUniformRowLease
    {
        internal Material Material;
        internal TextStudio2DHeroUniformRow Row;
        internal bool MaterialEffects;

        internal bool IsHeld => !ReferenceEquals(Material, null);
    }

    internal static partial class TextStudio2DHeroMaterials
    {
        /// <summary>The Hero Layer keyword of the uniform row program, in the row family keyword set.</summary>
        internal const string UniformRowKeyword = "TEXT_STUDIO_UNIFORM_ROW";

        static readonly int s_rowLayer = Shader.PropertyToID("_TextStudioHeroRowLayer");
        static readonly int s_rowExecution = Shader.PropertyToID("_TextStudioHeroRowExecution");
        static readonly int s_rowPaint = Shader.PropertyToID("_TextStudioHeroRowPaint");
        static readonly int s_rowColor = Shader.PropertyToID("_TextStudioHeroRowColor");
        static readonly int s_rowPaintTransform0 = Shader.PropertyToID("_TextStudioHeroRowPaintTransform0");
        static readonly int s_rowPaintTransform1 = Shader.PropertyToID("_TextStudioHeroRowPaintTransform1");
        static readonly int s_rowParameters0 = Shader.PropertyToID("_TextStudioHeroRowParameters0");
        static readonly int s_rowParameters1 = Shader.PropertyToID("_TextStudioHeroRowParameters1");
        static readonly int s_rowParameters2 = Shader.PropertyToID("_TextStudioHeroRowParameters2");
        static readonly int s_rowStopPositions0 = Shader.PropertyToID("_TextStudioHeroRowStopPositions0");
        static readonly int s_rowStopPositions1 = Shader.PropertyToID("_TextStudioHeroRowStopPositions1");
        static readonly int[] s_rowStopColors =
        {
            Shader.PropertyToID("_TextStudioHeroRowStopColor0"), Shader.PropertyToID("_TextStudioHeroRowStopColor1"),
            Shader.PropertyToID("_TextStudioHeroRowStopColor2"), Shader.PropertyToID("_TextStudioHeroRowStopColor3"),
            Shader.PropertyToID("_TextStudioHeroRowStopColor4"), Shader.PropertyToID("_TextStudioHeroRowStopColor5"),
            Shader.PropertyToID("_TextStudioHeroRowStopColor6"), Shader.PropertyToID("_TextStudioHeroRowStopColor7")
        };

        static bool s_uniformRowsEnabled = true;

        /// <summary>Tests force the uniform row program off to compare it with the table program on the same title.</summary>
        internal static bool UniformRowsEnabled
        {
            get => s_uniformRowsEnabled;
            set => s_uniformRowsEnabled = value;
        }

        /// <summary>Live uniform row materials across every layer material, for lifetime tests.</summary>
        internal static int UniformRowMaterialCount
        {
            get
            {
                int count = 0;
                foreach (KeyValuePair<CacheKey, CacheEntry> pair in s_layerEntries)
                    count += pair.Value.UniformRows?.Count ?? 0;
                return count;
            }
        }

        readonly struct UniformRowKey : IEquatable<UniformRowKey>
        {
            internal readonly TextStudio2DHeroUniformRow Row;
            internal readonly bool MaterialEffects;

            internal UniformRowKey(in TextStudio2DHeroUniformRow row, bool materialEffects)
            {
                Row = row;
                MaterialEffects = materialEffects;
            }

            public bool Equals(UniformRowKey other) => MaterialEffects == other.MaterialEffects && Row.Equals(other.Row);
            public override bool Equals(object obj) => obj is UniformRowKey other && Equals(other);
            public override int GetHashCode() => Row.GetHashCode() * 2 + (MaterialEffects ? 1 : 0);
        }

        sealed class UniformRowMaterial
        {
            internal Material Material;
            internal int References;
        }

        /// <summary>
        /// Whether a row of <paramref name="style"/> may draw from its record in material constants: a fill that does not keep
        /// the native face, a stroke, an inner or outer shadow, or an inner or outer glow, whose paint is solid or a linear,
        /// radial, conic, four-corner or distance gradient of at most <see cref="TextStudio2DHeroUniformRow.MaximumStops"/>
        /// stops, in a style without colour adjustments, which read earlier rows' records. Texture, pattern and noise paints,
        /// heavy families and every other row keep the table program. The program reads everything else, such as prepared
        /// fields, selection, paint mappings and Writing, exactly like the table program; the compositor adds the per-draw
        /// conditions (no appearance occurrence, no colour-source batch).
        /// </summary>
        internal static bool SupportsUniformRow(CompiledTextStudio2DStyle style, int operationIndex)
        {
            if (style == null || !style.IsValid || (uint)operationIndex >= (uint)style.OperationCount)
                return false;
            for (int index = 0; index < style.OperationCount; index++)
                if (style.OperationAt(index).Kind == TextStudio2DLayerKind.ColorAdjustment)
                    return false;
            ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(operationIndex);
            switch (operation.Kind)
            {
                case TextStudio2DLayerKind.Fill:
                    // A preserved native face draws TMP's own material contribution, which the program compiles out.
                    if (operation.Fill.PreservesNativeAppearance) return false;
                    break;
                case TextStudio2DLayerKind.Stroke:
                case TextStudio2DLayerKind.InnerShadow:
                case TextStudio2DLayerKind.OuterShadow:
                case TextStudio2DLayerKind.InnerGlow:
                case TextStudio2DLayerKind.OuterGlow:
                    break;
                default:
                    return false;
            }
            if (operation.PaintIndex < 0 || operation.PaintIndex >= style.PaintCount)
                return false;
            TextStudio2DCompiledPaint paint = style.GetPaint(operation.PaintIndex);
            switch (paint.Kind)
            {
                case TextStudio2DPaintKind.Solid:
                    return true;
                case TextStudio2DPaintKind.LinearGradient:
                case TextStudio2DPaintKind.RadialGradient:
                case TextStudio2DPaintKind.ConicGradient:
                case TextStudio2DPaintKind.FourCornerGradient:
                case TextStudio2DPaintKind.DistanceGradient:
                    return paint.GradientStopCount <= TextStudio2DHeroUniformRow.MaximumStops && paint.GradientStopStart >= 0 &&
                           paint.GradientStopStart + paint.GradientStopCount <= style.GradientStopCount;
                default:
                    return false;
            }
        }

        /// <summary>
        /// The shared material that draws <paramref name="row"/> with the uniform row program, holding it in
        /// <paramref name="held"/>, or null when the layer material or its shader cannot provide one; callers then keep the
        /// table program. Rows with equal records share one material per layer material.
        /// </summary>
        internal static Material ResolveUniformRow(in TextStudio2DHeroLayerMaterialLease lease, ref TextStudio2DHeroUniformRowLease held,
            in TextStudio2DHeroUniformRow row, bool materialEffects)
        {
            if (held.IsHeld && held.Material != null && held.MaterialEffects == materialEffects && held.Row.Equals(row))
                return held.Material;
            ReleaseUniformRow(in lease, ref held);
            if (!lease.IsValid)
                return null;
            CacheKey cacheKey = new CacheKey(lease.Source, lease.Atlas, lease.PixelsPerEm, lease.ColorMode, lease.DistanceRange);
            if (!s_layerEntries.TryGetValue(cacheKey, out CacheEntry entry) || entry.Material == null ||
                !entry.Material.shader.keywordSpace.FindKeyword(UniformRowKeyword).isValid)
                return null;
            UniformRowKey key = new UniformRowKey(in row, materialEffects);
            entry.UniformRows ??= new Dictionary<UniformRowKey, UniformRowMaterial>();
            if (!entry.UniformRows.TryGetValue(key, out UniformRowMaterial shared) || shared.Material == null)
            {
                Material material = new Material(entry.Material)
                {
                    name = entry.Material.name + " (Uniform Row)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                ConfigureUniformRow(material, entry.Material, in key);
                shared = new UniformRowMaterial { Material = material };
                entry.UniformRows[key] = shared;
            }
            shared.References++;
            held = new TextStudio2DHeroUniformRowLease { Material = shared.Material, Row = row, MaterialEffects = materialEffects };
            return shared.Material;
        }

        /// <summary>Releases a batch's hold on a uniform row material; the last holder destroys it.</summary>
        internal static void ReleaseUniformRow(in TextStudio2DHeroLayerMaterialLease lease, ref TextStudio2DHeroUniformRowLease held)
        {
            if (!held.IsHeld)
                return;
            UniformRowKey key = new UniformRowKey(in held.Row, held.MaterialEffects);
            // A layer material destroyed meanwhile took its row materials with it.
            if (lease.HasIdentity &&
                s_layerEntries.TryGetValue(new CacheKey(lease.Source, lease.Atlas, lease.PixelsPerEm, lease.ColorMode, lease.DistanceRange),
                    out CacheEntry entry) &&
                entry.UniformRows != null && entry.UniformRows.TryGetValue(key, out UniformRowMaterial shared) &&
                ReferenceEquals(shared.Material, held.Material) && --shared.References <= 0)
            {
                entry.UniformRows.Remove(key);
                Destroy(shared.Material);
            }
            held = default;
        }

        static void ConfigureUniformRow(Material material, Material layerMaterial, in UniformRowKey key)
        {
            material.CopyPropertiesFromMaterial(layerMaterial);
            TextStudio2DShaderTiers.Configure(material, TextStudio2DShaderTier.Full,
                key.MaterialEffects ? TextStudio2DShaderOptions.MaterialEffects : TextStudio2DShaderOptions.None);
            LocalKeyword keyword = material.shader.keywordSpace.FindKeyword(UniformRowKeyword);
            if (keyword.isValid)
                material.SetKeyword(keyword, true);
            ref readonly TextStudio2DHeroUniformRow row = ref key.Row;
            material.SetVector(s_rowLayer, row.Layer);
            material.SetVector(s_rowExecution, row.Execution);
            material.SetVector(s_rowPaint, row.Paint);
            material.SetVector(s_rowColor, row.Color);
            material.SetVector(s_rowPaintTransform0, row.PaintTransform0);
            material.SetVector(s_rowPaintTransform1, row.PaintTransform1);
            material.SetVector(s_rowParameters0, row.Parameters0);
            material.SetVector(s_rowParameters1, row.Parameters1);
            material.SetVector(s_rowParameters2, row.Parameters2);
            material.SetVector(s_rowStopPositions0, row.StopPositions0);
            material.SetVector(s_rowStopPositions1, row.StopPositions1);
            material.SetVector(s_rowStopColors[0], row.StopColor0);
            material.SetVector(s_rowStopColors[1], row.StopColor1);
            material.SetVector(s_rowStopColors[2], row.StopColor2);
            material.SetVector(s_rowStopColors[3], row.StopColor3);
            material.SetVector(s_rowStopColors[4], row.StopColor4);
            material.SetVector(s_rowStopColors[5], row.StopColor5);
            material.SetVector(s_rowStopColors[6], row.StopColor6);
            material.SetVector(s_rowStopColors[7], row.StopColor7);
        }

        static void SynchronizeUniformRows(CacheEntry entry)
        {
            if (entry.UniformRows == null)
                return;
            foreach (KeyValuePair<UniformRowKey, UniformRowMaterial> pair in entry.UniformRows)
                if (pair.Value.Material != null)
                    ConfigureUniformRow(pair.Value.Material, entry.Material, pair.Key);
        }

        static void DestroyUniformRows(CacheEntry entry)
        {
            if (entry.UniformRows == null)
                return;
            foreach (KeyValuePair<UniformRowKey, UniformRowMaterial> pair in entry.UniformRows)
                Destroy(pair.Value.Material);
            entry.UniformRows = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetUniformRows()
        {
            s_uniformRowsEnabled = true;
        }
    }
}
