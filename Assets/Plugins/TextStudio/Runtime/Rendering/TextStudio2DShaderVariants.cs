using UnityEngine;

namespace TextStudio
{
    internal static class TextStudio2DResourceKeys
    {
        internal const string ProductionShader =
            "TextStudio2DProductionSDF";
        internal const string OverlayShader =
            "TextStudio2DProductionSDFOverlay";
        internal const string FieldShader =
            "TextStudio2DFieldSDF";
        internal const string FieldCanvasShader =
            "TextStudio2DFieldSDFCanvas";
        internal const string FieldOverlayShader =
            "TextStudio2DFieldSDFOverlay";
        internal const string StyleDecorationShader =
            "TextStudio2DStyleDecoration";
        internal const string WritingShader =
            "TextStudioWriting";
        internal const string HeroLayerShader =
            "TextStudio2DHeroLayer";
        internal const string HeroCompositeShader =
            "TextStudio2DHeroComposite";
        internal const string HeroEffectsShader =
            "TextStudio2DHeroEffects";
        internal const string HeroOutputShader =
            "TextStudio2DHeroOutput";
        internal const string HeroOutputCanvasShader =
            "TextStudio2DHeroOutputCanvas";
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DShaderVariants
    {
        static bool s_warmed;

        internal static void WarmUp()
        {
            // Editor development compiles only the shader that is actually rendered. Eagerly warming every packaged SDF shader makes each domain reload pay the complete player startup cost and blocks the shader-authoring loop.
            if (Application.isEditor)
                return;

            if (s_warmed)
                return;
            s_warmed = true;
            WarmActiveTransport();
        }

        // Production and field programs compile one variant per data transport. Warming only the active transport keeps
        // player startup free of the unused route, which a device without structured buffers could not even create.
        static void WarmActiveTransport()
        {
            if (!TextStudio2DTransport.IsSupported)
                return;
            ShaderVariantCollection collection = new ShaderVariantCollection { hideFlags = HideFlags.HideAndDontSave };
            foreach (string key in new[]
                     {
                         TextStudio2DResourceKeys.ProductionShader, TextStudio2DResourceKeys.OverlayShader,
                         TextStudio2DResourceKeys.FieldShader, TextStudio2DResourceKeys.FieldCanvasShader, TextStudio2DResourceKeys.FieldOverlayShader
                     })
            {
                Shader shader = Resources.Load<Shader>(key);
                if (shader != null && shader.isSupported)
                    collection.Add(new ShaderVariantCollection.ShaderVariant(shader, UnityEngine.Rendering.PassType.Normal, TextStudio2DTransport.ActiveKeyword));
            }
            collection.WarmUp();
            Object.Destroy(collection);
        }
    }

    /// <summary>
    /// Resolves packaged 2D shaders without allowing a failed platform import to replace
    /// readable source rendering with Unity's magenta error output.
    /// </summary>
    internal static class TextStudio2DShaderResources
    {
        internal static Shader LoadSupported(
            string resourceKey,
            string featureName,
            ref bool warningLogged,
            out string failureReason)
        {
            Shader shader = Resources.Load<Shader>(resourceKey);
            if (IsUsable(shader))
            {
                failureReason = string.Empty;
                return shader;
            }

            failureReason = DescribeFailure(shader, resourceKey);
            if (!warningLogged)
            {
                warningLogged = true;
                Debug.LogWarning(
                    "[TextStudio] " + failureReason + " Text Studio is preserving " +
                    "source rendering and disabling only " + featureName + ".");
            }
            return null;
        }

        internal static bool IsUsable(Shader shader)
        {
            return IsUsable(shader, TextStudio2DGpuFallbackResources.RequiresDataTables(shader));
        }

        // Retained material owners know their shader contract. Passing it avoids allocating
        // Unity's shader-name string on every tick while keeping live device and binding checks.
        internal static bool IsUsable(Shader shader, bool requiresDataTables)
        {
            if (shader == null || !shader.isSupported || shader.passCount <= 0)
                return false;
            return !requiresDataTables || TextStudio2DGpuFallbackResources.EnsureInitialized();
        }

        internal static string DescribeFailure(Shader shader, string resourceKey)
        {
            if (shader == null)
            {
                return "Packaged shader resource '" + resourceKey +
                       "' is missing.";
            }

            if (shader.isSupported &&
                shader.passCount > 0 &&
                TextStudio2DGpuFallbackResources.RequiresDataTables(shader))
            {
                return TextStudio2DGpuFallbackResources.IsSupported
                    ? "Packaged shader resource '" + resourceKey +
                      "' could not establish its safe data-table bindings."
                    : "Packaged shader resource '" + resourceKey +
                      "' requires structured buffers or full-float data textures, and the active graphics device supports neither.";
            }

            return "Packaged shader resource '" + resourceKey +
                   "' did not compile for the active graphics device.";
        }
    }
}
