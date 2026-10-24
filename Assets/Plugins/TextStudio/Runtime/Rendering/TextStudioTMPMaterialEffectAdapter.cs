using System.Text;
using TMPro;
using UnityEngine;

namespace TextStudio
{
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal sealed partial class TextStudioTMPMaterialEffectAdapter
    {
        const string ProductionShaderName = "Text Studio/2D Production SDF";
        const string OverlayProductionShaderName = "Text Studio/2D Production SDF Overlay";
        const string FieldShaderName = "Text Studio/2D Field SDF";
        const string CanvasFieldShaderName = "Text Studio/2D Field SDF Canvas";
        const string OverlayFieldShaderName = "Text Studio/2D Field SDF Overlay";
        const string UniformFieldShaderName = "Text Studio/2D Field Uniform";
        const string CanvasUniformFieldShaderName = "Text Studio/2D Field Uniform Canvas";
        const AdditionalCanvasShaderChannels CanvasPayloadChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2 |
            AdditionalCanvasShaderChannels.TexCoord3 |
            AdditionalCanvasShaderChannels.Tangent;

        static bool s_shaderWarningLogged;
        static bool s_overlayShaderWarningLogged;
        static bool s_fieldShaderWarningLogged;
        static bool s_fieldOverlayShaderWarningLogged;

        Material[] _sourceMaterials;
        Material[] _productionMaterials;
        TextStudioTMPProductionMaterialLease[] _productionMaterialLeases;
        // Program variants of the production leases (tier and solid paint), acquired on first use by an ordered primitive.
        TextStudioTMPProductionMaterialLease[] _primitiveMaterialLeases;
        // Slots the current ordered build resolved; EndPrimitiveMaterials releases the others, so a row that changes its program
        // does not keep its previous program's material alive while the production state stays current.
        bool[] _primitiveMaterialUsed;
        // Every shader tier with and without solid paint.
        const int PrimitiveSlots = 12;
        Material _sourcePrimaryMaterial;
        TMP_Text _target;
        TextStudioCanvasShaderChannelLease _canvasPayloadLease;
        bool _overlayIntent;
        bool _writingMaterialRequired;
        bool _appearanceMaterialRequired;
        TextStudio2DFieldPaddingTier _appearancePaddingTier;
        TextStudio2DShaderTier _appearanceShaderTier;
        TextStudio2DShaderOptions _shaderOptions = TextStudio2DShaderOptions.MaterialEffects;
        // Rows of the uniform field program the eligible submeshes draw with; default when the text keeps the Field program.
        TextStudio2DUniformFieldProgram _uniformProgram;
        // TextMeshPro route: every submesh draws a shared copy of its own TMP material with these rows instead of a production
        // material, and the text needs no Text Studio payload. Default on every other route.
        TextStudio2DTextMeshProStyle _textMeshProStyle;
        TextStudioTMPRouteMaterialLease[] _routeMaterialLeases;
        // The last source check of SupportsTextMeshProRoute, reused while the style, sources and atlas densities are unchanged.
        TextStudio2DTextMeshProStyle _routeCheckStyle;
        Material[] _routeCheckSources = System.Array.Empty<Material>();
        float[] _routeCheckPixelsPerEm = System.Array.Empty<float>();
        int _routeCheckCount = -1;
        bool _routeCheckResult;
        // Set when a source could not take or keep its route material since the bridge last asked.
        bool _textMeshProRouteFailed;
        // Whether the effect plan uses a material-tier effect, known even when the text needs no appearance production
        // material. Restore resets it to true, so the Hero compositor reads the plan itself for its rows.
        bool _materialEffectsRequired = true;
        bool _canvasPayloadRequired;
        // False while every field Restore resets holds its reset value and no lease is held, so Restore has nothing to do;
        // Prepare sets it before it writes any of them.
        bool _hasRestorableState;

        internal string CompatibilityWarning { get; private set; } = string.Empty;
        internal bool HasPreparedState => _target != null;
        /// <summary>
        /// The text draws with shared copies of its TMP materials on the TextMeshPro route; its meshes keep TMP's own streams,
        /// without Text Studio payload, source coordinates or Canvas payload channels.
        /// </summary>
        internal bool UsesTextMeshProRoute => _target != null && _textMeshProStyle.IsValid;
        internal bool HasRestorableState => _hasRestorableState;
        internal Material SourcePrimaryMaterial => _sourcePrimaryMaterial;

        internal Material ResolveRenderingMaterial(TMP_Text target, int materialIndex)
        {
            return _productionMaterials != null && materialIndex >= 0 && materialIndex < _productionMaterials.Length
                ? _productionMaterials[materialIndex] : ResolveSourceMaterial(target, materialIndex);
        }

        /// <summary>
        /// Resolves the production variant of one ordered primitive's program (<see cref="TextStudio2DPrimitivePrograms"/>): an
        /// isolated operation family, the Basic tier, or solid paint for a primitive whose own operations paint solid. Every variant
        /// shares the source, padding, payload identity and material effects of the production lease, which a primitive asking for
        /// the text's own program receives.
        /// </summary>
        internal Material ResolvePrimitiveMaterial(TMP_Text target, int materialIndex, TextStudio2DShaderTier tier, bool solidPaint)
        {
            if (_productionMaterialLeases == null || materialIndex < 0 || materialIndex >= _productionMaterialLeases.Length)
                return ResolveRenderingMaterial(target, materialIndex);
            TextStudioTMPProductionMaterialLease production = _productionMaterialLeases[materialIndex];
            // The uniform program carries its own rows and has no tiers.
            if (!production.IsValid || production.UniformProgram.IsValid)
                return ResolveRenderingMaterial(target, materialIndex);
            TextStudio2DShaderOptions options = solidPaint ? production.Options | TextStudio2DShaderOptions.SolidPaint : production.Options;
            if (tier == production.ShaderTier && options == production.Options)
                return production.Replacement;
            if (_primitiveMaterialLeases == null || _primitiveMaterialLeases.Length != _productionMaterialLeases.Length * PrimitiveSlots)
            {
                ReleaseProductionMaterialLeases(_primitiveMaterialLeases);
                _primitiveMaterialLeases = new TextStudioTMPProductionMaterialLease[_productionMaterialLeases.Length * PrimitiveSlots];
                _primitiveMaterialUsed = new bool[_primitiveMaterialLeases.Length];
            }
            int slot = materialIndex * PrimitiveSlots + (int)tier * 2 + (solidPaint ? 1 : 0);
            _primitiveMaterialUsed[slot] = true;
            ref TextStudioTMPProductionMaterialLease primitive = ref _primitiveMaterialLeases[slot];
            if (!primitive.IsValid || !ReferenceEquals(primitive.Source, production.Source) || !ReferenceEquals(primitive.Shader, production.Shader) ||
                primitive.PayloadFromTangent != production.PayloadFromTangent || primitive.PaddingTier != production.PaddingTier ||
                primitive.WritingSource != production.WritingSource || !Mathf.Approximately(primitive.PixelsPerEm, production.PixelsPerEm) ||
                primitive.Options != options || primitive.ShaderTier != TextStudio2DShaderTiers.SupportedTier(production.Shader, tier))
            {
                TextStudioTMPProductionMaterialCache.Release(ref primitive);
                primitive = TextStudioTMPProductionMaterialCache.Acquire(production.Source, production.Shader, production.PayloadFromTangent,
                    production.PaddingTier, production.WritingSource, production.PixelsPerEm, tier, options);
            }

            return primitive.IsValid ? primitive.Replacement : production.Replacement;
        }

        /// <summary>Starts an ordered build: primitive leases it does not resolve again are released by <see cref="EndPrimitiveMaterials"/>.</summary>
        internal void BeginPrimitiveMaterials()
        {
            if (_primitiveMaterialUsed != null)
                System.Array.Clear(_primitiveMaterialUsed, 0, _primitiveMaterialUsed.Length);
        }

        /// <summary>Releases the primitive leases the finished ordered build did not use.</summary>
        internal void EndPrimitiveMaterials()
        {
            if (_primitiveMaterialLeases == null || _primitiveMaterialUsed == null)
                return;
            for (int slot = 0; slot < _primitiveMaterialLeases.Length; slot++)
                if (!_primitiveMaterialUsed[slot] && _primitiveMaterialLeases[slot].HasIdentity)
                    TextStudioTMPProductionMaterialCache.Release(ref _primitiveMaterialLeases[slot]);
        }

        internal TextStudio2DShaderOptions ShaderOptions => _shaderOptions;
        internal bool MaterialEffectsRequired => _materialEffectsRequired;

        internal Material ResolveSourceMaterial(TMP_Text target, int materialIndex)
        {
            if (_sourceMaterials != null &&
                materialIndex >= 0 &&
                materialIndex < _sourceMaterials.Length &&
                _sourceMaterials[materialIndex] != null)
            {
                return _sourceMaterials[materialIndex];
            }
            if (target != null && materialIndex == 0)
            {
                Material primary = target.fontSharedMaterial;
                if (primary != null && !IsTransientProductionMaterial(primary))
                    return primary;
            }
            TMP_TextInfo textInfo = target != null ? target.textInfo : null;
            if (textInfo?.meshInfo != null &&
                materialIndex >= 0 &&
                materialIndex < textInfo.meshInfo.Length)
            {
                Material meshMaterial = textInfo.meshInfo[materialIndex].material;
                if (meshMaterial != null &&
                    !IsTransientProductionMaterial(meshMaterial))
                {
                    return meshMaterial;
                }
            }
            return ResolveFontSourceMaterial(target, materialIndex);
        }

        internal static bool RestoreOrphanedProductionMaterials(TMP_Text target)
        {
            // TMP invokes component OnValidate while a scene is still being
            // deserialized. Its shared-material getter requires textInfo, which
            // is initialized later by Awake. Initialization and the first layout
            // pass retry this recovery once TMP is ready.
            if (target == null || target.textInfo == null)
                return false;

            Material[] current = target.fontSharedMaterials;
            if (current == null || current.Length == 0)
                return false;

            Material[] restored = null;
            for (int i = 0; i < current.Length; i++)
            {
                // Route materials keep TMP's shader, so a reload's orphans are recognized by their hidden name.
                if (!IsTransientProductionMaterial(current[i]) && !TextStudioTMPRouteMaterialCache.IsOrphan(current[i]))
                    continue;

                Material source = ResolveFontSourceMaterial(target, i);
                if (source == null || IsTransientProductionMaterial(source))
                    continue;

                restored ??= (Material[])current.Clone();
                restored[i] = source;
            }

            if (restored == null)
                return false;

            target.fontSharedMaterials = restored;
            if (restored[0] != null)
                target.fontSharedMaterial = restored[0];
            return true;
        }

        internal static Material ResolveFontSourceMaterial(TMP_Text target, int materialIndex)
        {
            if (materialIndex == 0)
                return target.font != null ? target.font.material : null;

            TMP_TextInfo textInfo = target.textInfo;
            if (textInfo?.characterInfo == null)
                return null;

            int characterCount = Mathf.Min(textInfo.characterCount, textInfo.characterInfo.Length);
            for (int i = 0; i < characterCount; i++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[i];
                if (character.materialReferenceIndex == materialIndex && character.fontAsset != null)
                    return character.fontAsset.material;
            }
            return null;
        }

        internal static bool IsTransientProductionMaterial(Material material)
        {
            if (material == null)
                return false;
            // TextMeshPro route materials keep TMP's shader and are registered while they live; restore paths recognize a
            // reload's orphans by name (TextStudioTMPRouteMaterialCache.IsOrphan).
            if (TextStudioTMPRouteMaterialCache.IsRegistered(material))
                return true;
            // Every other replacement declares this property. Native TMP materials are queried
            // during continuous Hero output and need no allocating shader-name inspection.
            if (!material.HasProperty("_TextStudioPayloadEnabled") || material.shader == null)
                return false;
            string shaderName = material.shader.name;
            if (shaderName != ProductionShaderName &&
                shaderName != OverlayProductionShaderName &&
                shaderName != FieldShaderName &&
                shaderName != CanvasFieldShaderName &&
                shaderName != OverlayFieldShaderName &&
                shaderName != UniformFieldShaderName &&
                shaderName != CanvasUniformFieldShaderName)
                return false;

            bool hiddenAndUnsaved =
                (material.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave;
            return hiddenAndUnsaved ||
                   material.name.IndexOf(
                       " (Text Studio 2D Production)",
                       System.StringComparison.Ordinal) >= 0;
        }

        public void Prepare(
            TMP_Text target,
            TextStudioCompiledPlan plan,
            bool requiresExternalMaterial = false,
            bool requiresWritingMaterial = false,
            bool requiresAppearanceMaterial = false,
            TextStudio2DFieldPaddingTier appearancePaddingTier =
                TextStudio2DFieldPaddingTier.None,
            CompiledTextStudio2DStyle appearanceStyle = null,
            CompiledTextStudioAppearancePlan appearancePlan = null,
            in TextStudio2DTextMeshProStyle textMeshProStyle = default)
        {
            TextStudio2DShaderTier shaderTier = requiresAppearanceMaterial
                ? TextStudio2DShaderTiers.Select(appearanceStyle, appearancePlan)
                : TextStudio2DShaderTier.Full;
            // Appearance materials compile material-tier effects only when the plan uses one, and paint evaluation only when
            // a paint is not solid; see TEXT_STUDIO_MATERIAL_EFFECTS and TEXT_STUDIO_SOLID_PAINT.
            bool materialEffects = RequiresProductionMaterial(plan);
            _materialEffectsRequired = materialEffects;
            _hasRestorableState = true;
            TextStudio2DShaderOptions shaderOptions = requiresAppearanceMaterial
                ? TextStudio2DShaderTiers.SelectOptions(appearanceStyle, appearancePlan, materialEffects)
                : TextStudio2DShaderOptions.MaterialEffects;
            // TMP-expressible styles read their rows from material constants instead of the style table; see
            // TextStudio2DShaderTiers.TrySelectUniformProgram. Overlay and other ineligible submeshes keep the Field program.
            TextStudio2DUniformFieldProgram uniformProgram = default;
            Shader uniformShader = null;
            if (requiresAppearanceMaterial && !requiresExternalMaterial && target != null &&
                TextStudio2DShaderTiers.TrySelectUniformProgram(appearanceStyle, appearancePlan, materialEffects, requiresWritingMaterial,
                    out uniformProgram))
            {
                uniformShader = TextStudio2DShaderTiers.LoadUniformShader(target is TextMeshProUGUI);
                if (uniformShader == null)
                    uniformProgram = default;
            }
            if (!requiresAppearanceMaterial)
                appearancePaddingTier = TextStudio2DFieldPaddingTier.None;
            // The TextMeshPro route replaces every production requirement; any of them keeps the production path.
            TextStudio2DTextMeshProStyle routeStyle = !requiresExternalMaterial && !requiresWritingMaterial &&
                                                      !requiresAppearanceMaterial && !materialEffects
                ? textMeshProStyle
                : default;
            if (!requiresExternalMaterial &&
                !requiresWritingMaterial &&
                !requiresAppearanceMaterial &&
                !materialEffects &&
                !routeStyle.IsValid)
            {
                Restore();
                return;
            }

            if (target == null)
            {
                Restore();
                return;
            }

            if (IsPreparedStateCurrent(
                    target,
                    requiresWritingMaterial,
                    requiresAppearanceMaterial,
                    appearancePaddingTier, shaderTier, shaderOptions, in uniformProgram, in routeStyle))
            {
                SynchronizeProductionMaterials();
                return;
            }

            // TMP rebuilds fallback slots during text edits; retain existing leases until their replacements have been acquired.
            TextStudioTMPProductionMaterialLease[] previousLeases = _productionMaterialLeases;
            TextStudioTMPRouteMaterialLease[] previousRouteLeases = _routeMaterialLeases;
            _productionMaterialLeases = null;
            _routeMaterialLeases = null;
            try
            {
                if (routeStyle.IsValid)
                    PrepareTextMeshProMaterials(target, in routeStyle);
                else
                    PrepareMaterials(target, requiresWritingMaterial, requiresAppearanceMaterial, appearancePaddingTier, shaderTier, shaderOptions,
                        in uniformProgram, uniformShader);
            }
            finally
            {
                ReleaseProductionMaterialLeases(previousLeases);
                ReleaseRouteMaterialLeases(previousRouteLeases);
                ReleaseProductionMaterialLeases(_primitiveMaterialLeases);
                _primitiveMaterialLeases = null;
            }
        }

        /// <summary>
        /// Whether every submesh's live source material can draw the TextMeshPro rows (<see cref="TextStudio2DTextMeshProRoute.TryResolveMaterial"/>).
        /// Overlay text keeps the exact path. The result is reused while the rows, sources and atlas densities are unchanged.
        /// </summary>
        internal bool SupportsTextMeshProRoute(TMP_Text target, in TextStudio2DTextMeshProStyle style)
        {
            if (target == null || target.isOverlay || !style.IsValid)
                return false;
            TMP_TextInfo info = target.textInfo;
            int count = info != null ? Mathf.Max(1, info.materialCount) : 1;
            bool current = _routeCheckCount == count && _routeCheckStyle.Equals(style);
            if (_routeCheckSources.Length < count)
            {
                System.Array.Resize(ref _routeCheckSources, count);
                System.Array.Resize(ref _routeCheckPixelsPerEm, count);
                current = false;
            }
            for (int index = 0; index < count; index++)
            {
                Material source = ResolveSourceMaterial(target, index);
                float pixelsPerEm = TextStudioTMPFontMetrics.PixelsPerEm(target, index, source);
                current &= ReferenceEquals(_routeCheckSources[index], source) && _routeCheckPixelsPerEm[index].Equals(pixelsPerEm);
                _routeCheckSources[index] = source;
                _routeCheckPixelsPerEm[index] = pixelsPerEm;
            }
            if (current)
                return _routeCheckResult;
            _routeCheckStyle = style;
            _routeCheckCount = count;
            _routeCheckResult = true;
            for (int index = 0; index < count && _routeCheckResult; index++)
                _routeCheckResult = TextStudio2DTextMeshProRoute.TryResolveMaterial(in style, _routeCheckSources[index],
                    _routeCheckPixelsPerEm[index], out _);
            return _routeCheckResult;
        }

        /// <summary>Forgets the last TextMeshPro source check, for example after a source material's properties changed.</summary>
        internal void InvalidateTextMeshProSupport() => _routeCheckCount = -1;

        /// <summary>
        /// Whether a source could not take or keep its route material since the last call, for example a fallback font the
        /// route check did not see or a source edited at runtime; the text then needs a new route decision. Clears the report.
        /// </summary>
        internal bool ConsumeTextMeshProRouteFailure()
        {
            bool failed = _textMeshProRouteFailed;
            _textMeshProRouteFailed = false;
            return failed;
        }

        void MarkTextMeshProRouteFailed()
        {
            _textMeshProRouteFailed = true;
            _routeCheckCount = -1;
        }

        // Every submesh draws a shared copy of its live source material with the style's face, outline and underlay. When any
        // source cannot take one, the text keeps its sources and reports the failure, so the bridge decides the exact path.
        void PrepareTextMeshProMaterials(TMP_Text target, in TextStudio2DTextMeshProStyle style)
        {
            Restore();
            _hasRestorableState = true;
            Material sourcePrimaryMaterial = ResolveLiveSourceMaterial(target, target.fontSharedMaterial, 0, null);
            Material[] currentMaterials = target.fontSharedMaterials;
            if (sourcePrimaryMaterial == null || currentMaterials == null || currentMaterials.Length == 0)
            {
                MarkTextMeshProRouteFailed();
                return;
            }
            Material[] source = (Material[])currentMaterials.Clone();
            source[0] = sourcePrimaryMaterial;
            for (int i = 1; i < source.Length; i++)
                source[i] = ResolveLiveSourceMaterial(target, source[i], i, sourcePrimaryMaterial);

            Material[] replacements = new Material[source.Length];
            TextStudioTMPRouteMaterialLease[] leases = new TextStudioTMPRouteMaterialLease[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                leases[i] = TextStudioTMPRouteMaterialCache.Acquire(source[i], TextStudioTMPFontMetrics.PixelsPerEm(target, i, source[i]), in style);
                if (!leases[i].IsValid)
                {
                    ReleaseRouteMaterialLeases(leases);
                    MarkTextMeshProRouteFailed();
                    return;
                }
                replacements[i] = leases[i].Replacement;
            }

            CompatibilityWarning = string.Empty;
            _target = target;
            _sourceMaterials = source;
            _sourcePrimaryMaterial = sourcePrimaryMaterial;
            _productionMaterials = replacements;
            _productionMaterialLeases = null;
            _routeMaterialLeases = leases;
            _overlayIntent = target.isOverlay;
            _writingMaterialRequired = false;
            _appearanceMaterialRequired = false;
            _appearancePaddingTier = TextStudio2DFieldPaddingTier.None;
            _appearanceShaderTier = TextStudio2DShaderTier.Full;
            _shaderOptions = TextStudio2DShaderOptions.MaterialEffects;
            _uniformProgram = default;
            _textMeshProStyle = style;
            // As in PrepareMaterials, a target whose primary material no longer exists receives its live source first.
            if (target.fontSharedMaterial == null)
                PrimeTargetSharedMaterials(target, source);
            target.fontSharedMaterials = replacements;
            target.fontSharedMaterial = replacements[0];
        }

        void PrepareMaterials(
            TMP_Text target,
            bool requiresWritingMaterial,
            bool requiresAppearanceMaterial,
            TextStudio2DFieldPaddingTier appearancePaddingTier,
            TextStudio2DShaderTier shaderTier,
            TextStudio2DShaderOptions shaderOptions,
            in TextStudio2DUniformFieldProgram uniformProgram,
            Shader uniformShader)
        {
            Restore();
            _hasRestorableState = true;
            TextStudio2DShaderVariants.WarmUp();
            string shaderResource = requiresAppearanceMaterial
                ? target is TextMeshProUGUI
                    ? TextStudio2DResourceKeys.FieldCanvasShader
                    : TextStudio2DResourceKeys.FieldShader
                : TextStudio2DResourceKeys.ProductionShader;
            string overlayShaderResource = requiresAppearanceMaterial
                ? TextStudio2DResourceKeys.FieldOverlayShader
                : TextStudio2DResourceKeys.OverlayShader;
            Shader shader = LoadSupportedShader(
                shaderResource,
                requiresAppearanceMaterial,
                false,
                out string shaderFailure);
            if (shader == null)
            {
                CompatibilityWarning = shaderFailure +
                    " Text Studio is preserving the source TMP material.";
                return;
            }
            Shader overlayShader = LoadSupportedShader(
                overlayShaderResource,
                requiresAppearanceMaterial,
                true,
                out string overlayShaderFailure);

            Material sourcePrimaryMaterial = ResolveLiveSourceMaterial(
                target,
                target.fontSharedMaterial,
                0,
                null);
            if (sourcePrimaryMaterial == null)
                return;
            Material[] currentMaterials = target.fontSharedMaterials;
            if (currentMaterials == null || currentMaterials.Length == 0)
                return;
            Material[] source = (Material[])currentMaterials.Clone();
            source[0] = sourcePrimaryMaterial;
            for (int i = 1; i < source.Length; i++)
            {
                source[i] = ResolveLiveSourceMaterial(
                    target,
                    source[i],
                    i,
                    sourcePrimaryMaterial);
            }

            Material[] replacements = new Material[source.Length];
            TextStudioTMPProductionMaterialLease[] leases =
                new TextStudioTMPProductionMaterialLease[source.Length];
            bool replacedAny = false;
            StringBuilder warning = null;
            for (int i = 0; i < source.Length; i++)
            {
                Material sourceMaterial = source[i];
                TextStudioTMPMaterialCompatibility compatibility =
                    TextStudioTMPMaterialCompatibility.Analyze(sourceMaterial);
                if (!compatibility.IsSupported)
                {
                    replacements[i] = sourceMaterial;
                    if (!compatibility.IsSprite)
                        AppendCompatibilityWarning(ref warning, i, sourceMaterial, compatibility.Reason);
                    continue;
                }

                bool useOverlay = target.isOverlay || compatibility.IsOverlay;
                bool useUniform = UsesUniformProgram(in uniformProgram, useOverlay, sourceMaterial, compatibility);
                Shader replacementShader = useUniform ? uniformShader : useOverlay ? overlayShader : shader;
                if (replacementShader == null)
                {
                    replacements[i] = sourceMaterial;
                    AppendCompatibilityWarning(
                        ref warning,
                        i,
                        sourceMaterial,
                        overlayShaderFailure);
                    continue;
                }

                TextStudioTMPProductionMaterialLease lease =
                    TextStudioTMPProductionMaterialCache.Acquire(
                        sourceMaterial,
                        replacementShader,
                        target is TextMeshProUGUI,
                        appearancePaddingTier,
                        requiresWritingMaterial, TextStudioTMPFontMetrics.PixelsPerEm(target, i, sourceMaterial), shaderTier, shaderOptions,
                        useUniform ? uniformProgram : default);
                leases[i] = lease;
                replacements[i] = lease.IsValid
                    ? lease.Replacement
                    : sourceMaterial;
                replacedAny |= lease.IsValid;
            }

            CompatibilityWarning = warning != null
                ? "Text Studio TMP material effects left incompatible submeshes unchanged: " + warning + "."
                : string.Empty;

            if (!replacedAny)
            {
                _target = target;
                _sourceMaterials = source;
                _sourcePrimaryMaterial = sourcePrimaryMaterial;
                _productionMaterials = replacements;
                _productionMaterialLeases = leases;
                _overlayIntent = target.isOverlay;
                _writingMaterialRequired = requiresWritingMaterial;
                _appearanceMaterialRequired = requiresAppearanceMaterial;
                _appearancePaddingTier = appearancePaddingTier;
                _appearanceShaderTier = shaderTier;
                _shaderOptions = shaderOptions;
                _uniformProgram = uniformProgram;
                return;
            }

            _target = target;
            _sourceMaterials = source;
            _sourcePrimaryMaterial = sourcePrimaryMaterial;
            _productionMaterials = replacements;
            _productionMaterialLeases = leases;
            _overlayIntent = target.isOverlay;
            _writingMaterialRequired = requiresWritingMaterial;
            _appearanceMaterialRequired = requiresAppearanceMaterial;
            _appearancePaddingTier = appearancePaddingTier;
            _appearanceShaderTier = shaderTier;
            _shaderOptions = shaderOptions;
            _uniformProgram = uniformProgram;
            AcquireCanvasPayloadChannels(target);
            // TMP reads its current primary material while it accepts a material array. A target can still hold a
            // production material that no longer exists, for example an instance of a prefab saved from a styled scene
            // object after that object is gone, so the live source goes in first.
            if (target.fontSharedMaterial == null)
                PrimeTargetSharedMaterials(target, source);
            target.fontSharedMaterials = replacements;
            if (replacements[0] != null)
                target.fontSharedMaterial = replacements[0];
        }

        static Shader LoadSupportedShader(
            string resourceName,
            bool fieldShader,
            bool overlayShader,
            out string failureReason)
        {
            if (fieldShader)
            {
                if (overlayShader)
                {
                    return TextStudio2DShaderResources.LoadSupported(
                        resourceName,
                        "2D appearance styling",
                        ref s_fieldOverlayShaderWarningLogged,
                        out failureReason);
                }
                return TextStudio2DShaderResources.LoadSupported(
                    resourceName,
                    "2D appearance styling",
                    ref s_fieldShaderWarningLogged,
                    out failureReason);
            }

            if (overlayShader)
            {
                return TextStudio2DShaderResources.LoadSupported(
                    resourceName,
                    "material effects",
                    ref s_overlayShaderWarningLogged,
                    out failureReason);
            }
            return TextStudio2DShaderResources.LoadSupported(
                resourceName,
                "material effects",
                ref s_shaderWarningLogged,
                out failureReason);
        }

        public void Restore()
        {
            // Plain text releases its unused material effects every frame; after the first release nothing is left to reset.
            if (!_hasRestorableState)
                return;
            ReleaseCanvasPayloadChannels();
            if (_target != null && _sourceMaterials != null)
            {
                Material sourcePrimaryMaterial = ResolveLiveSourceMaterial(
                    _target,
                    _sourcePrimaryMaterial,
                    0,
                    null);
                Material[] current = _target.fontSharedMaterials;
                if (current != null && sourcePrimaryMaterial != null)
                {
                    Material[] restored = (Material[])current.Clone();
                    int count = Mathf.Min(restored.Length, _sourceMaterials.Length);
                    for (int i = 0; i < restored.Length; i++)
                    {
                        Material productionMaterial =
                            _productionMaterials != null && i < _productionMaterials.Length
                                ? _productionMaterials[i]
                                : null;
                        bool shouldRestore =
                            i < count &&
                            (ReferenceEquals(restored[i], productionMaterial) ||
                             restored[i] == null ||
                             IsTransientProductionMaterial(restored[i]));
                        if (shouldRestore)
                        {
                            restored[i] = ResolveLiveSourceMaterial(
                                _target,
                                _sourceMaterials[i],
                                i,
                                sourcePrimaryMaterial);
                        }
                        else if (restored[i] == null)
                        {
                            restored[i] = ResolveLiveSourceMaterial(
                                _target,
                                null,
                                i,
                                sourcePrimaryMaterial);
                        }
                    }
                    PrimeTargetSharedMaterials(_target, restored);
                    _target.fontSharedMaterials = restored;
                }
                if (sourcePrimaryMaterial != null &&
                    _productionMaterials != null &&
                    _productionMaterials.Length > 0 &&
                    (ReferenceEquals(_target.fontSharedMaterial, _productionMaterials[0]) ||
                     _target.fontSharedMaterial == null ||
                     IsTransientProductionMaterial(_target.fontSharedMaterial)))
                {
                    _target.fontSharedMaterial = sourcePrimaryMaterial;
                }
            }

            ReleaseProductionMaterialLeases(_productionMaterialLeases);
            ReleaseRouteMaterialLeases(_routeMaterialLeases);
            ReleaseProductionMaterialLeases(_primitiveMaterialLeases);
            _primitiveMaterialLeases = null;
            _target = null;
            _sourceMaterials = null;
            _productionMaterials = null;
            _productionMaterialLeases = null;
            _routeMaterialLeases = null;
            _textMeshProStyle = default;
            _sourcePrimaryMaterial = null;
            _overlayIntent = false;
            _writingMaterialRequired = false;
            _appearanceMaterialRequired = false;
            _appearancePaddingTier = TextStudio2DFieldPaddingTier.None;
            _appearanceShaderTier = TextStudio2DShaderTier.Full;
            _shaderOptions = TextStudio2DShaderOptions.MaterialEffects;
            _uniformProgram = default;
            _materialEffectsRequired = true;
            _canvasPayloadRequired = false;
            CompatibilityWarning = string.Empty;
            _hasRestorableState = false;
        }

        static Material ResolveLiveSourceMaterial(
            TMP_Text target,
            Material candidate,
            int materialIndex,
            Material fallback)
        {
            // TMP's instance material copies a route material without its registration or hidden flags, so its name identifies
            // it; the font's own material needs no name check.
            if (candidate != null && !IsTransientProductionMaterial(candidate) &&
                (materialIndex == 0 && target.font != null && ReferenceEquals(candidate, target.font.material) ||
                 !TextStudioTMPRouteMaterialCache.HasRouteName(candidate)))
                return candidate;

            Material fontSource = ResolveFontSourceMaterial(target, materialIndex);
            if (fontSource != null && !IsTransientProductionMaterial(fontSource))
                return fontSource;
            if (fallback != null && !IsTransientProductionMaterial(fallback))
                return fallback;
            return null;
        }

        static void PrimeTargetSharedMaterials(
            TMP_Text target,
            Material[] restored)
        {
            if (target == null || restored == null || restored.Length == 0)
                return;

            target.fontSharedMaterial = restored[0];
            if (target is TextMeshPro)
            {
                TMP_SubMesh[] subMeshes =
                    target.GetComponentsInChildren<TMP_SubMesh>(true);
                int count = Mathf.Min(subMeshes.Length, restored.Length - 1);
                for (int i = 0; i < count; i++)
                {
                    if (subMeshes[i] != null && restored[i + 1] != null)
                        subMeshes[i].sharedMaterial = restored[i + 1];
                }
            }
            else if (target is TextMeshProUGUI)
            {
                TMP_SubMeshUI[] subMeshes =
                    target.GetComponentsInChildren<TMP_SubMeshUI>(true);
                int count = Mathf.Min(subMeshes.Length, restored.Length - 1);
                for (int i = 0; i < count; i++)
                {
                    if (subMeshes[i] != null && restored[i + 1] != null)
                        subMeshes[i].sharedMaterial = restored[i + 1];
                }
            }
        }

        internal bool RequiresRefresh(TMP_Text target)
        {
            return _target != null &&
                   target != null &&
                   !IsPreparedStateCurrent(
                       target,
                       _writingMaterialRequired,
                       _appearanceMaterialRequired,
                       _appearancePaddingTier,
                       _appearanceShaderTier,
                       _shaderOptions,
                       in _uniformProgram,
                       in _textMeshProStyle);
        }

        internal bool ReferencesSourceMaterial(Material material)
        {
            return material != null && Contains(_sourceMaterials, material);
        }

        bool IsPreparedStateCurrent(
            TMP_Text target,
            bool requiresWritingMaterial,
            bool requiresAppearanceMaterial,
            TextStudio2DFieldPaddingTier appearancePaddingTier,
            TextStudio2DShaderTier shaderTier,
            TextStudio2DShaderOptions shaderOptions,
            in TextStudio2DUniformFieldProgram uniformProgram,
            in TextStudio2DTextMeshProStyle textMeshProStyle)
        {
            if (!ReferenceEquals(_target, target) ||
                _sourceMaterials == null ||
                _productionMaterials == null ||
                _overlayIntent != target.isOverlay ||
                _writingMaterialRequired != requiresWritingMaterial ||
                _appearanceMaterialRequired != requiresAppearanceMaterial ||
                _appearancePaddingTier != appearancePaddingTier ||
                _appearanceShaderTier != shaderTier ||
                _shaderOptions != shaderOptions ||
                !_uniformProgram.Equals(uniformProgram) ||
                !_textMeshProStyle.Equals(textMeshProStyle))
            {
                return false;
            }

            if (_canvasPayloadRequired)
            {
                Canvas currentCanvas = ResolveCanvas(target);
                if (!TextStudioCanvasShaderChannelLeases.IsCurrent(
                        in _canvasPayloadLease,
                        currentCanvas))
                {
                    return false;
                }
            }

            Material[] current = target.fontSharedMaterials;
            if (current == null || current.Length != _productionMaterials.Length)
                return false;
            if (_textMeshProStyle.IsValid)
                return IsTextMeshProStateCurrent(target, current);
            for (int i = 0; i < current.Length; i++)
            {
                if (_productionMaterialLeases != null && i < _productionMaterialLeases.Length &&
                    _productionMaterialLeases[i].IsValid &&
                    !_productionMaterialLeases[i].PixelsPerEm.Equals(TextStudioTMPFontMetrics.PixelsPerEm(target, i, _sourceMaterials[i])))
                    return false;
                if (!ReferenceEquals(current[i], _productionMaterials[i]))
                    return false;
                TextStudioTMPMaterialCompatibility compatibility =
                    TextStudioTMPMaterialCompatibility.Analyze(_sourceMaterials[i]);
                if (!compatibility.IsSupported && !ReferenceEquals(_productionMaterials[i], _sourceMaterials[i]))
                    return false;
                bool useOverlay = target.isOverlay || compatibility.IsOverlay;
                if (compatibility.IsSupported &&
                    !UsesExpectedProductionShader(
                        _productionMaterials[i],
                        useOverlay,
                        requiresAppearanceMaterial,
                        target is TextMeshProUGUI,
                        UsesUniformProgram(in uniformProgram, useOverlay, _sourceMaterials[i], compatibility)))
                {
                    return false;
                }
            }
            return true;
        }

        // Every submesh holds the live, registered route material of its source at its current atlas density. A destroyed
        // material still equals its own reference, so the lease checks that both materials exist.
        bool IsTextMeshProStateCurrent(TMP_Text target, Material[] current)
        {
            if (_routeMaterialLeases == null || _routeMaterialLeases.Length != current.Length)
                return false;
            for (int i = 0; i < current.Length; i++)
            {
                TextStudioTMPRouteMaterialLease lease = _routeMaterialLeases[i];
                if (!lease.IsValid || !ReferenceEquals(current[i], lease.Replacement) ||
                    !TextStudioTMPRouteMaterialCache.IsRegistered(lease.Replacement) ||
                    lease.Replacement.shader != lease.Source.shader ||
                    !lease.PixelsPerEm.Equals(TextStudioTMPFontMetrics.PixelsPerEm(target, i, lease.Source)))
                    return false;
            }
            return true;
        }

        static bool UsesExpectedProductionShader(
            Material material,
            bool useOverlay,
            bool useFieldShader,
            bool useCanvasFieldShader,
            bool useUniformShader)
        {
            if (material == null || material.shader == null)
                return false;

            if (!TextStudio2DShaderResources.IsUsable(material.shader))
                return false;

            string expectedName = useUniformShader
                ? useCanvasFieldShader ? CanvasUniformFieldShaderName : UniformFieldShaderName
                : useFieldShader
                ? useOverlay
                    ? OverlayFieldShaderName
                    : useCanvasFieldShader
                        ? CanvasFieldShaderName
                        : FieldShaderName
                : useOverlay ? OverlayProductionShaderName : ProductionShaderName;
            return material.shader.name == expectedName;
        }

        /// <summary>A submesh draws the uniform program when its text selected one and its source material adds nothing the program omits.</summary>
        static bool UsesUniformProgram(in TextStudio2DUniformFieldProgram program, bool useOverlay, Material source,
            TextStudioTMPMaterialCompatibility compatibility)
        {
            return program.IsValid && !useOverlay && compatibility.IsSupported &&
                   TextStudio2DShaderTiers.SupportsUniformSource(source, compatibility.NativeMaterialModel);
        }

        void SynchronizeProductionMaterials()
        {
            for (int i = 0; i < _sourceMaterials.Length; i++)
            {
                Material source = _sourceMaterials[i];
                Material production = _productionMaterials[i];
                if (source == null || production == null || ReferenceEquals(source, production))
                    continue;

                // A source edited beyond what the route draws keeps its last route values until the next route decision.
                if (_routeMaterialLeases != null && i < _routeMaterialLeases.Length &&
                    !TextStudioTMPRouteMaterialCache.Synchronize(in _routeMaterialLeases[i]))
                    MarkTextMeshProRouteFailed();
                if (_productionMaterialLeases != null &&
                    i < _productionMaterialLeases.Length)
                {
                    TextStudioTMPProductionMaterialLease lease =
                        _productionMaterialLeases[i];
                    TextStudioTMPProductionMaterialCache.Synchronize(in lease);
                }
                for (int slot = 0; _primitiveMaterialLeases != null && slot < PrimitiveSlots; slot++)
                {
                    int primitiveIndex = i * PrimitiveSlots + slot;
                    if (primitiveIndex < _primitiveMaterialLeases.Length && _primitiveMaterialLeases[primitiveIndex].IsValid)
                        TextStudioTMPProductionMaterialCache.Synchronize(in _primitiveMaterialLeases[primitiveIndex]);
                }
            }
        }

        internal bool ReleaseIfUnused(
            TextStudioCompiledPlan plan,
            bool requiresExternalMaterial)
        {
            // The TextMeshPro route is an appearance the bridge prepares; only its next route decision releases it.
            if (UsesTextMeshProRoute ||
                requiresExternalMaterial ||
                _appearanceMaterialRequired ||
                RequiresProductionMaterial(plan))
                return false;

            Restore();
            return true;
        }

        internal void SetPayloadEnabled(bool enabled)
        {
            // TMP's own material reads no Text Studio payload.
            if (!enabled && _target != null && !UsesTextMeshProRoute)
                Restore();
        }

        void AcquireCanvasPayloadChannels(TMP_Text target)
        {
            _canvasPayloadRequired = target is TextMeshProUGUI;
            if (!_canvasPayloadRequired)
                return;

            TextStudioCanvasShaderChannelLeases.Acquire(
                ResolveCanvas(target),
                CanvasPayloadChannels,
                ref _canvasPayloadLease);
        }

        void ReleaseCanvasPayloadChannels()
        {
            TextStudioCanvasShaderChannelLeases.Release(
                ref _canvasPayloadLease);
        }

        static Canvas ResolveCanvas(TMP_Text target)
        {
            return target is TextMeshProUGUI uiText ? uiText.canvas : null;
        }

        static void AppendCompatibilityWarning(
            ref StringBuilder warning,
            int materialIndex,
            Material material,
            string reason)
        {
            warning ??= new StringBuilder(128);
            if (warning.Length > 0)
                warning.Append("; ");
            warning.Append("submesh ");
            warning.Append(materialIndex);
            warning.Append(" (");
            warning.Append(material != null ? material.name : "missing material");
            warning.Append("): ");
            warning.Append(reason);
        }

        /// <summary>Whether the effect plan uses a material-tier effect, which production materials and Hero rows compile.</summary>
        internal static bool RequiresProductionMaterial(TextStudioCompiledPlan plan)
        {
            if (plan?.EffectBindings == null)
                return false;

            int count = Mathf.Min(plan.EffectBindingCount, plan.EffectBindings.Length);
            for (int i = 0; i < count; i++)
            {
                ITextStudioEffect effect = plan.EffectBindings[i].effect;
                if (effect is ITextStudioEffectCapabilitiesProvider provider &&
                    provider.Capabilities.PerformanceTier == TextStudioEffectPerformanceTier.Material)
                {
                    return true;
                }

                string id = plan.EffectBindings[i].effectId;
                if (id == TextStudioBuiltInEffectIds.Focus ||
                    id == TextStudioBuiltInEffectIds.Dissolve ||
                    id == TextStudioBuiltInEffectIds.Burn ||
                    id == TextStudioBuiltInEffectIds.OutlineGlow ||
                    id == TextStudioBuiltInEffectIds.SurfaceSweep ||
                    id == TextStudioBuiltInEffectIds.ChromaticGlitch ||
                    id == TextStudioBuiltInEffectIds.PixelGrid ||
                    id == TextStudioBuiltInEffectIds.Squiggle)
                {
                    return true;
                }
            }
            return false;
        }

        static void ReleaseProductionMaterialLeases(TextStudioTMPProductionMaterialLease[] leases)
        {
            if (leases == null)
                return;
            for (int i = 0; i < leases.Length; i++)
                TextStudioTMPProductionMaterialCache.Release(ref leases[i]);
        }

        static void ReleaseRouteMaterialLeases(TextStudioTMPRouteMaterialLease[] leases)
        {
            if (leases == null)
                return;
            for (int i = 0; i < leases.Length; i++)
                TextStudioTMPRouteMaterialCache.Release(ref leases[i]);
        }

        static bool Contains(Material[] materials, Material candidate)
        {
            if (materials == null)
                return false;
            for (int i = 0; i < materials.Length; i++)
            {
                if (ReferenceEquals(materials[i], candidate))
                    return true;
            }
            return false;
        }

    }
}
