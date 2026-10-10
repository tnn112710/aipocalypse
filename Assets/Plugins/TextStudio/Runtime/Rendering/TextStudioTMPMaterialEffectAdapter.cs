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
        Material _sourcePrimaryMaterial;
        TMP_Text _target;
        TextStudioCanvasShaderChannelLease _canvasPayloadLease;
        bool _overlayIntent;
        bool _writingMaterialRequired;
        bool _appearanceMaterialRequired;
        TextStudio2DFieldPaddingTier _appearancePaddingTier;
        bool _canvasPayloadRequired;

        internal string CompatibilityWarning { get; private set; } = string.Empty;
        internal bool HasPreparedState => _target != null;
        internal Material SourcePrimaryMaterial => _sourcePrimaryMaterial;

        internal Material ResolveRenderingMaterial(TMP_Text target, int materialIndex)
        {
            return _productionMaterials != null && materialIndex >= 0 && materialIndex < _productionMaterials.Length
                ? _productionMaterials[materialIndex] : ResolveSourceMaterial(target, materialIndex);
        }

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
                if (!IsTransientProductionMaterial(current[i]))
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
            // Every replacement declares this property. Native TMP materials are queried
            // during continuous Hero output and need no allocating shader-name inspection.
            if (material == null || !material.HasProperty("_TextStudioPayloadEnabled") || material.shader == null)
                return false;
            string shaderName = material.shader.name;
            if (shaderName != ProductionShaderName &&
                shaderName != OverlayProductionShaderName &&
                shaderName != FieldShaderName &&
                shaderName != CanvasFieldShaderName &&
                shaderName != OverlayFieldShaderName)
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
                TextStudio2DFieldPaddingTier.None)
        {
            if (!requiresAppearanceMaterial)
                appearancePaddingTier = TextStudio2DFieldPaddingTier.None;
            if (!requiresExternalMaterial &&
                !requiresWritingMaterial &&
                !requiresAppearanceMaterial &&
                !RequiresProductionMaterial(plan))
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
                    appearancePaddingTier))
            {
                SynchronizeProductionMaterials();
                return;
            }

            // TMP rebuilds fallback slots during text edits; retain existing leases until their replacements have been acquired.
            TextStudioTMPProductionMaterialLease[] previousLeases = _productionMaterialLeases;
            _productionMaterialLeases = null;
            try
            {
                PrepareMaterials(target, requiresWritingMaterial, requiresAppearanceMaterial, appearancePaddingTier);
            }
            finally
            {
                ReleaseProductionMaterialLeases(previousLeases);
            }
        }

        void PrepareMaterials(
            TMP_Text target,
            bool requiresWritingMaterial,
            bool requiresAppearanceMaterial,
            TextStudio2DFieldPaddingTier appearancePaddingTier)
        {
            Restore();
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
                Shader replacementShader = useOverlay ? overlayShader : shader;
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
                        requiresWritingMaterial, TextStudioTMPFontMetrics.PixelsPerEm(target, i, sourceMaterial));
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
            AcquireCanvasPayloadChannels(target);
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
            _target = null;
            _sourceMaterials = null;
            _productionMaterials = null;
            _productionMaterialLeases = null;
            _sourcePrimaryMaterial = null;
            _overlayIntent = false;
            _writingMaterialRequired = false;
            _appearanceMaterialRequired = false;
            _appearancePaddingTier = TextStudio2DFieldPaddingTier.None;
            _canvasPayloadRequired = false;
            CompatibilityWarning = string.Empty;
        }

        static Material ResolveLiveSourceMaterial(
            TMP_Text target,
            Material candidate,
            int materialIndex,
            Material fallback)
        {
            if (candidate != null && !IsTransientProductionMaterial(candidate))
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
                       _appearancePaddingTier);
        }

        internal bool ReferencesSourceMaterial(Material material)
        {
            return material != null && Contains(_sourceMaterials, material);
        }

        bool IsPreparedStateCurrent(
            TMP_Text target,
            bool requiresWritingMaterial,
            bool requiresAppearanceMaterial,
            TextStudio2DFieldPaddingTier appearancePaddingTier)
        {
            if (!ReferenceEquals(_target, target) ||
                _sourceMaterials == null ||
                _productionMaterials == null ||
                _overlayIntent != target.isOverlay ||
                _writingMaterialRequired != requiresWritingMaterial ||
                _appearanceMaterialRequired != requiresAppearanceMaterial ||
                _appearancePaddingTier != appearancePaddingTier)
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
                if (compatibility.IsSupported &&
                    !UsesExpectedProductionShader(
                        _productionMaterials[i],
                        target.isOverlay || compatibility.IsOverlay,
                        requiresAppearanceMaterial,
                        target is TextMeshProUGUI))
                {
                    return false;
                }
            }
            return true;
        }

        static bool UsesExpectedProductionShader(
            Material material,
            bool useOverlay,
            bool useFieldShader,
            bool useCanvasFieldShader)
        {
            if (material == null || material.shader == null)
                return false;

            if (!TextStudio2DShaderResources.IsUsable(material.shader))
                return false;

            string expectedName = useFieldShader
                ? useOverlay
                    ? OverlayFieldShaderName
                    : useCanvasFieldShader
                        ? CanvasFieldShaderName
                        : FieldShaderName
                : useOverlay ? OverlayProductionShaderName : ProductionShaderName;
            return material.shader.name == expectedName;
        }

        void SynchronizeProductionMaterials()
        {
            for (int i = 0; i < _sourceMaterials.Length; i++)
            {
                Material source = _sourceMaterials[i];
                Material production = _productionMaterials[i];
                if (source == null || production == null || ReferenceEquals(source, production))
                    continue;

                if (_productionMaterialLeases != null &&
                    i < _productionMaterialLeases.Length)
                {
                    TextStudioTMPProductionMaterialLease lease =
                        _productionMaterialLeases[i];
                    TextStudioTMPProductionMaterialCache.Synchronize(in lease);
                }
            }
        }

        internal bool ReleaseIfUnused(
            TextStudioCompiledPlan plan,
            bool requiresExternalMaterial)
        {
            if (requiresExternalMaterial ||
                _appearanceMaterialRequired ||
                RequiresProductionMaterial(plan))
                return false;

            Restore();
            return true;
        }

        internal void SetPayloadEnabled(bool enabled)
        {
            if (!enabled && _target != null)
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

        static bool RequiresProductionMaterial(TextStudioCompiledPlan plan)
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
