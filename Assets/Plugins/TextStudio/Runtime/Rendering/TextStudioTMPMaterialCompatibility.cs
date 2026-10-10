using System;
using UnityEngine;

namespace TextStudio
{
    internal enum TextStudioTMPNativeMaterialModel
    {
        Unknown = 0,
        Mobile = 1,
        Full = 2
    }

    internal readonly struct TextStudioTMPMaterialCompatibility
    {
        internal const string NativeMaterialModelProperty = "_TextStudioNativeMaterialModel";

        internal TextStudioTMPNativeMaterialModel NativeMaterialModel { get; }
        internal bool IsSupported { get; }
        internal bool IsSprite { get; }
        internal bool IsOverlay { get; }
        internal string Reason { get; }

        TextStudioTMPMaterialCompatibility(bool isSupported, bool isSprite, bool isOverlay, string reason,
            TextStudioTMPNativeMaterialModel nativeMaterialModel = TextStudioTMPNativeMaterialModel.Unknown)
        {
            NativeMaterialModel = nativeMaterialModel;
            IsSupported = isSupported;
            IsSprite = isSprite;
            IsOverlay = isOverlay;
            Reason = reason ?? string.Empty;
        }

        internal static TextStudioTMPMaterialCompatibility Analyze(Material material)
        {
            if (material == null)
                return Unsupported("missing material");

            Shader shader = material.shader;
            if (shader == null)
                return Unsupported("missing shader");

            string shaderName = shader.name ?? string.Empty;
            if (string.Equals(shaderName, "TextMeshPro/Sprite", StringComparison.Ordinal))
                return new TextStudioTMPMaterialCompatibility(false, true, false, string.Empty);

            bool overlay = string.Equals(shaderName, "TextMeshPro/Distance Field Overlay", StringComparison.Ordinal) ||
                           string.Equals(shaderName, "TextMeshPro/Mobile/Distance Field Overlay", StringComparison.Ordinal) ||
                           string.Equals(shaderName, "Text Studio/2D Production SDF Overlay", StringComparison.Ordinal);
            TextStudioTMPNativeMaterialModel nativeMaterialModel = ResolveNativeMaterialModel(material, shaderName);
            bool supportedShader = nativeMaterialModel != TextStudioTMPNativeMaterialModel.Unknown;
            if (!supportedShader)
                return Unsupported("shader '" + shaderName + "' is not a supported unlit Distance Field shader");

            if (shader.passCount != 1)
                return Unsupported("multi-pass TMP materials are not supported");
            if (!material.HasProperty("_MainTex") ||
                !material.HasProperty("_GradientScale") ||
                !material.HasProperty("_TextureWidth") ||
                !material.HasProperty("_TextureHeight"))
            {
                return Unsupported("required SDF atlas properties are missing");
            }

            if (material.IsKeywordEnabled("BEVEL_ON"))
                return Unsupported("bevel rendering is enabled");
            if (material.IsKeywordEnabled("GLOW_ON"))
                return Unsupported("TMP glow rendering is enabled; use Text Studio Outline & Glow instead");
            if (material.IsKeywordEnabled("MASK_HARD") || material.IsKeywordEnabled("MASK_SOFT"))
                return Unsupported("TMP texture masking is enabled");

            return new TextStudioTMPMaterialCompatibility(true, false, overlay, string.Empty, nativeMaterialModel);
        }

        static TextStudioTMPNativeMaterialModel ResolveNativeMaterialModel(Material material, string shaderName)
        {
            if (string.Equals(shaderName, "TextMeshPro/Distance Field", StringComparison.Ordinal) ||
                string.Equals(shaderName, "TextMeshPro/Distance Field Overlay", StringComparison.Ordinal))
                return TextStudioTMPNativeMaterialModel.Full;
            if (string.Equals(shaderName, "TextMeshPro/Mobile/Distance Field", StringComparison.Ordinal) ||
                string.Equals(shaderName, "TextMeshPro/Mobile/Distance Field Overlay", StringComparison.Ordinal))
                return TextStudioTMPNativeMaterialModel.Mobile;
            if (string.Equals(shaderName, "Text Studio/2D Production SDF", StringComparison.Ordinal) ||
                string.Equals(shaderName, "Text Studio/2D Production SDF Overlay", StringComparison.Ordinal))
            {
                // Prepared materials retain their source model. Existing authored production materials use Mobile math.
                return material.HasProperty(NativeMaterialModelProperty) &&
                       material.GetFloat(NativeMaterialModelProperty) == (float)TextStudioTMPNativeMaterialModel.Full
                    ? TextStudioTMPNativeMaterialModel.Full
                    : TextStudioTMPNativeMaterialModel.Mobile;
            }
            return TextStudioTMPNativeMaterialModel.Unknown;
        }

        static TextStudioTMPMaterialCompatibility Unsupported(string reason)
        {
            return new TextStudioTMPMaterialCompatibility(false, false, false, reason);
        }
    }
}
