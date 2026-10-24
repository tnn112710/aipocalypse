using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Material constants of the uniform field program (TextStudio2DFieldUniform.shader): the fill, stroke and outer shadow
    /// rows of a TMP-expressible style, packed by <see cref="TextStudio2DGpuStylePacking.PackOperation"/> exactly like their
    /// style-table rows. x, y and z of every vector belong to the fill, stroke and shadow row.
    /// </summary>
    internal readonly struct TextStudio2DUniformFieldProgram : IEquatable<TextStudio2DUniformFieldProgram>
    {
        /// <summary>Operation-mask bit of each row, all in span zero, or zero when the style has no such row.</summary>
        internal readonly Vector4 Bits;
        /// <summary>Stack rank of each row, zero topmost; absent rows take the remaining ranks.</summary>
        internal readonly Vector4 Ranks;
        /// <summary>Row opacity, <c>LayerMetadata.z</c>.</summary>
        internal readonly Vector4 Opacity;
        /// <summary>Animated tint participation, <c>LayerMetadata.w</c>.</summary>
        internal readonly Vector4 Tint;
        internal readonly Vector4 FillColor;
        internal readonly Vector4 FillParameters;
        internal readonly Vector4 StrokeColor;
        internal readonly Vector4 StrokeParameters0;
        internal readonly Vector4 StrokeParameters1;
        internal readonly Vector4 ShadowColor;
        internal readonly Vector4 ShadowParameters0;
        internal readonly Vector4 ShadowParameters1;

        /// <param name="fill">Operation index of the fill row, or -1; likewise <paramref name="stroke"/> and <paramref name="shadow"/>.</param>
        internal TextStudio2DUniformFieldProgram(CompiledTextStudio2DStyle style, int fill, int stroke, int shadow)
        {
            Pack(style, fill, out float fillBit, out float fillOpacity, out float fillTint, out FillColor, out FillParameters, out _);
            Pack(style, stroke, out float strokeBit, out float strokeOpacity, out float strokeTint, out StrokeColor, out StrokeParameters0,
                out StrokeParameters1);
            Pack(style, shadow, out float shadowBit, out float shadowOpacity, out float shadowTint, out ShadowColor, out ShadowParameters0,
                out ShadowParameters1);
            Bits = new Vector4(fillBit, strokeBit, shadowBit, 0f);
            Ranks = new Vector4(Rank(fill, stroke, shadow, 0), Rank(fill, stroke, shadow, 1), Rank(fill, stroke, shadow, 2), 0f);
            Opacity = new Vector4(fillOpacity, strokeOpacity, shadowOpacity, 0f);
            Tint = new Vector4(fillTint, strokeTint, shadowTint, 0f);
        }

        internal bool IsValid => Bits.x > 0f || Bits.y > 0f || Bits.z > 0f;

        static void Pack(CompiledTextStudio2DStyle style, int operationIndex, out float bit, out float opacity, out float tint,
            out Vector4 color, out Vector4 parameters0, out Vector4 parameters1)
        {
            if (operationIndex < 0)
            {
                bit = opacity = tint = 0f;
                color = parameters0 = parameters1 = Vector4.zero;
                return;
            }
            TextStudio2DGpuOperation row = TextStudio2DGpuStylePacking.PackOperation(style, operationIndex, 0);
            bit = 1 << operationIndex;
            opacity = row.LayerMetadata.z;
            tint = row.LayerMetadata.w;
            color = row.Color;
            parameters0 = row.Parameters0;
            parameters1 = row.Parameters1;
        }

        // The Field program composes selected rows in operation order, topmost first. Present rows keep that order;
        // absent rows, which never draw, fill the remaining ranks so the three ranks stay a permutation.
        static float Rank(int fill, int stroke, int shadow, int kind)
        {
            int index = kind == 0 ? fill : kind == 1 ? stroke : shadow;
            int rank = 0;
            for (int other = 0; other < 3; other++)
            {
                int otherIndex = other == 0 ? fill : other == 1 ? stroke : shadow;
                if (other == kind) continue;
                if (index >= 0 ? otherIndex >= 0 && otherIndex < index : otherIndex >= 0 || other < kind) rank++;
            }
            return rank;
        }

        public bool Equals(TextStudio2DUniformFieldProgram other) =>
            Bits.Equals(other.Bits) && Ranks.Equals(other.Ranks) && Opacity.Equals(other.Opacity) && Tint.Equals(other.Tint) &&
            FillColor.Equals(other.FillColor) && FillParameters.Equals(other.FillParameters) &&
            StrokeColor.Equals(other.StrokeColor) && StrokeParameters0.Equals(other.StrokeParameters0) &&
            StrokeParameters1.Equals(other.StrokeParameters1) && ShadowColor.Equals(other.ShadowColor) &&
            ShadowParameters0.Equals(other.ShadowParameters0) && ShadowParameters1.Equals(other.ShadowParameters1);

        public override bool Equals(object obj) => obj is TextStudio2DUniformFieldProgram other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Bits.GetHashCode();
                hash = hash * 397 ^ Ranks.GetHashCode();
                hash = hash * 397 ^ Opacity.GetHashCode();
                hash = hash * 397 ^ Tint.GetHashCode();
                hash = hash * 397 ^ FillColor.GetHashCode();
                hash = hash * 397 ^ FillParameters.GetHashCode();
                hash = hash * 397 ^ StrokeColor.GetHashCode();
                hash = hash * 397 ^ StrokeParameters0.GetHashCode();
                hash = hash * 397 ^ StrokeParameters1.GetHashCode();
                hash = hash * 397 ^ ShadowColor.GetHashCode();
                hash = hash * 397 ^ ShadowParameters0.GetHashCode();
                return hash * 397 ^ ShadowParameters1.GetHashCode();
            }
        }
    }

    internal static partial class TextStudio2DShaderTiers
    {
        static readonly int s_uniformBits = Shader.PropertyToID("_TextStudioUniformBits");
        static readonly int s_uniformRanks = Shader.PropertyToID("_TextStudioUniformRanks");
        static readonly int s_uniformOpacity = Shader.PropertyToID("_TextStudioUniformOpacity");
        static readonly int s_uniformTint = Shader.PropertyToID("_TextStudioUniformTint");
        static readonly int s_uniformFillColor = Shader.PropertyToID("_TextStudioUniformFillColor");
        static readonly int s_uniformFillParameters = Shader.PropertyToID("_TextStudioUniformFillParameters");
        static readonly int s_uniformStrokeColor = Shader.PropertyToID("_TextStudioUniformStrokeColor");
        static readonly int s_uniformStrokeParameters0 = Shader.PropertyToID("_TextStudioUniformStrokeParameters0");
        static readonly int s_uniformStrokeParameters1 = Shader.PropertyToID("_TextStudioUniformStrokeParameters1");
        static readonly int s_uniformShadowColor = Shader.PropertyToID("_TextStudioUniformShadowColor");
        static readonly int s_uniformShadowParameters0 = Shader.PropertyToID("_TextStudioUniformShadowParameters0");
        static readonly int s_uniformShadowParameters1 = Shader.PropertyToID("_TextStudioUniformShadowParameters1");

        /// <summary>
        /// Local keyword of the uniform program's variant for glyphs a text engine publishes with its own single-channel atlas
        /// (<see cref="TextStudio2DExternalGlyphSource"/>). It reads each glyph's mapping row from the quad instead of the
        /// TMP atlas coordinates.
        /// </summary>
        internal const string UniformExternalFieldKeyword = "TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD";

        static bool s_uniformProgramEnabled = true;
        static bool s_uniformShaderWarningLogged;

        /// <summary>Tests force the uniform program off to compare it with the Field program on the same text.</summary>
        internal static bool UniformProgramEnabled
        {
            get => s_uniformProgramEnabled;
            set => s_uniformProgramEnabled = value;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void ResetUniformProgram()
        {
            s_uniformProgramEnabled = true;
        }

        /// <summary>
        /// The uniform field shader of Canvas or world output, or null with a one-time warning when it is unusable. Callers then
        /// keep the Field program, which draws the same styles at a higher GPU cost.
        /// </summary>
        internal static Shader LoadUniformShader(bool canvas)
        {
            string resource = canvas ? TextStudio2DResourceKeys.FieldUniformCanvasShader : TextStudio2DResourceKeys.FieldUniformShader;
            Shader shader = Resources.Load<Shader>(resource);
            if (TextStudio2DShaderResources.IsUsable(shader))
                return shader;
            // The Field program draws these styles too, so a missing uniform shader costs GPU time, not rendering.
            if (!s_uniformShaderWarningLogged && (shader == null || !TextStudio2DGpuFallbackResources.IsShuttingDown))
            {
                s_uniformShaderWarningLogged = true;
                Debug.LogWarning("[TextStudio] " + TextStudio2DShaderResources.DescribeFailure(shader, resource) +
                                 " Text Studio draws TextMeshPro-expressible 2D styles with the general field program.");
            }
            return null;
        }

        /// <summary>
        /// Selects the uniform field program for a style TextMeshPro could express: at most one fill, one round undashed
        /// stroke and one distance-field outer shadow, all solid, Normal blend and on the fused field path, without
        /// selectors, groups, masks, a preserved native face, scoped invocations, material effects or Writing, at Native
        /// source quality with world shadows off and layer-major order or a single operation. Independent retained lines
        /// may share the first operation span; they keep their own material. Every other style keeps the Field program. The source material must also pass <see cref="SupportsUniformSource"/>.
        /// Glyphs a text engine publishes with its own atlas pass the same rule without material effects or Writing, which
        /// engine-owned output never draws; each glyph then needs a single-channel field in place of the source material check.
        /// </summary>
        internal static bool TrySelectUniformProgram(CompiledTextStudio2DStyle style, CompiledTextStudioAppearancePlan plan,
            bool materialEffects, bool writing, out TextStudio2DUniformFieldProgram program)
        {
            program = default;
            if (!s_uniformProgramEnabled || !TryResolveUniformRows(style, plan, materialEffects, writing, out int fill, out int stroke, out int shadow))
                return false;
            program = new TextStudio2DUniformFieldProgram(style, fill, stroke, shadow);
            return program.IsValid;
        }

        /// <summary>
        /// The operation indices of the fill, stroke and outer shadow rows of a style <see cref="TrySelectUniformProgram"/> accepts,
        /// each -1 when absent, independent of the program switch. <see cref="TextStudio2DTextMeshProRoute.TrySelect"/> applies
        /// the same rule before its own conditions.
        /// </summary>
        internal static bool TryResolveUniformRows(CompiledTextStudio2DStyle style, CompiledTextStudioAppearancePlan plan,
            bool materialEffects, bool writing, out int fill, out int stroke, out int shadow)
        {
            fill = stroke = shadow = -1;
            if (materialEffects || writing || plan != null && plan.HasInvocations ||
                style == null || !style.IsValid || style.IsPassThrough ||
                style.OperationCount > TextStudio2DStyleTable.MaximumFusedOperations ||
                style.SourceFieldRequest.Quality != TextStudio2DSourceFieldQuality.Native ||
                style.WorldShadowMode != TextStudio2DWorldShadowMode.Off ||
                style.PainterOrder != TextStudio2DPainterOrder.LayerMajor && style.OperationCount != 1)
                return false;
            for (int index = 0; index < style.BucketCount; index++)
            {
                TextStudio2DExecutionPath path = style.GetBucket(index).BatchKey.ExecutionPath;
                if (path != TextStudio2DExecutionPath.FusedField && path != TextStudio2DExecutionPath.RetainedGeometry)
                    return false;
            }

            for (int index = 0; index < style.OperationCount; index++)
            {
                ref readonly TextStudio2DCompiledOperation operation = ref style.OperationAt(index);
                if (operation.ExecutionPath == TextStudio2DExecutionPath.RetainedGeometry)
                {
                    // Independent lines and their fill treatments draw with their own retained material. They consume
                    // operation indices, but no uniform row; the first-span limit keeps every glyph mask bit exact.
                    int owner = operation.SurfaceOwnerOperationIndex >= 0 ? operation.SurfaceOwnerOperationIndex : index;
                    if (operation.BlendMode != TextStudio2DBlendMode.Normal || operation.ParentOperationIndex >= 0 ||
                        operation.MaskOperationIndex >= 0 || !TextStudio2DStyleDecorations.SupportsLineProgram(style, owner))
                        return false;
                    continue;
                }
                if (operation.ExecutionPath != TextStudio2DExecutionPath.FusedField ||
                    operation.BlendMode != TextStudio2DBlendMode.Normal ||
                    operation.ParentOperationIndex >= 0 || operation.MaskOperationIndex >= 0 || operation.SurfaceOwnerOperationIndex >= 0 ||
                    operation.PaintIndex < 0 || operation.PaintIndex >= style.PaintCount ||
                    style.GetPaint(operation.PaintIndex).Kind != TextStudio2DPaintKind.Solid ||
                    operation.SelectorIndex < 0 || operation.SelectorIndex >= style.SelectorCount ||
                    !style.GetSelector(operation.SelectorIndex).IsTrivialAll)
                    return false;
                switch (operation.Kind)
                {
                    case TextStudio2DLayerKind.Fill:
                        // A preserved native face draws TMP's own material contribution, not a fill row.
                        if (fill >= 0 || operation.Fill.PreservesNativeAppearance) return false;
                        fill = index;
                        break;
                    case TextStudio2DLayerKind.Stroke:
                        if (stroke >= 0 || operation.Stroke.Join != TextStudio2DStrokeJoin.Round || operation.Stroke.DashCount != 0) return false;
                        stroke = index;
                        break;
                    case TextStudio2DLayerKind.OuterShadow:
                        if (shadow >= 0 || operation.Shadow.Rendering != TextStudio2DSoftEffectRendering.DistanceField) return false;
                        shadow = index;
                        break;
                    default:
                        return false;
                }
            }
            return fill >= 0 || stroke >= 0 || shadow >= 0;
        }

        /// <summary>
        /// Whether a source TMP material contributes nothing the uniform program omits: white face and outline textures, a
        /// single-distance atlas, and no outline or underlay under the production lease's own outline rule.
        /// </summary>
        internal static bool SupportsUniformSource(Material source, TextStudioTMPNativeMaterialModel nativeModel)
        {
            if (source == null || source.IsKeywordEnabled("OUTLINE_ON") || source.IsKeywordEnabled("UNDERLAY_ON") ||
                source.IsKeywordEnabled("UNDERLAY_INNER"))
                return false;
            // Full TMP has no outline keyword; a positive width enables its outline.
            if (nativeModel == TextStudioTMPNativeMaterialModel.Full && source.HasProperty("_OutlineWidth") &&
                source.GetFloat("_OutlineWidth") > 0.0001f)
                return false;
            return IsWhite(source, "_FaceTex") && IsWhite(source, "_OutlineTex") &&
                   TextStudioGlyphFieldContract.ResolveFormat(source) == TextStudioGlyphFieldFormat.SingleDistance;
        }

        static bool IsWhite(Material material, string property)
        {
            if (!material.HasProperty(property))
                return true;
            Texture texture = material.GetTexture(property);
            return texture == null || texture == Texture2D.whiteTexture;
        }

        /// <summary>Writes the rows of a uniform program into its production material.</summary>
        internal static void ConfigureUniformProgram(Material material, in TextStudio2DUniformFieldProgram program)
        {
            material.SetVector(s_uniformBits, program.Bits);
            material.SetVector(s_uniformRanks, program.Ranks);
            material.SetVector(s_uniformOpacity, program.Opacity);
            material.SetVector(s_uniformTint, program.Tint);
            material.SetVector(s_uniformFillColor, program.FillColor);
            material.SetVector(s_uniformFillParameters, program.FillParameters);
            material.SetVector(s_uniformStrokeColor, program.StrokeColor);
            material.SetVector(s_uniformStrokeParameters0, program.StrokeParameters0);
            material.SetVector(s_uniformStrokeParameters1, program.StrokeParameters1);
            material.SetVector(s_uniformShadowColor, program.ShadowColor);
            material.SetVector(s_uniformShadowParameters0, program.ShadowParameters0);
            material.SetVector(s_uniformShadowParameters1, program.ShadowParameters1);
        }
    }
}
