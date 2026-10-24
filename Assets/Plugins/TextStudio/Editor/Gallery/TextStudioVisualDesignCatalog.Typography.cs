using System;
using System.IO;
using ImpossibleRobert.Common;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal static partial class TextStudioVisualDesignCatalog
    {
        internal const string AppearanceFontRoot = "Runtime/Fonts/Appearance";

        internal static string ResolveAppearanceFontName(string id)
        {
            return id switch
            {
                "AppearanceCinematicGold" or "AppearanceIceCrystal" or
                "AppearanceDeepLetterpress" or "AppearanceEngravedLine" or
                "AppearanceLegendaryRelic" or "AppearanceAncientRelic" or
                "AppearanceSilverScreen" => "CinzelDecorative-Bold",
                "AppearanceScifiHud" or "AppearanceHolographicFoil" or
                "AppearanceSignalDuotone" or "AppearanceGlitchSignal" or
                "AppearanceNebulaPulse" => "Audiowide-Regular",
                "AppearanceComicImpact" or "AppearanceGlassPrism" or
                "AppearanceMoltenObsidian" or "AppearanceRetroPoster" or
                "AppearanceBubbleGel" or "AppearanceGlitterResin" or
                "AppearanceVictoryGold" or "AppearanceArcadeChampion" or
                "AppearanceToxicWarning" or "AppearanceCarbonFiber" or
                "AppearanceMotherOfPearl" or "AppearanceVelvetFlock" => "Bungee-Regular",
                _ => "BebasNeue-Regular"
            };
        }

        internal static TMP_FontAsset LoadAppearanceDisplayFont(string id)
        {
            string root = TextStudioPackagePaths.FindAssetRoot(TextStudioPackagePaths.BasePackageName);
            if (string.IsNullOrEmpty(root))
                return null;
            string name = ResolveAppearanceFontName(id);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                root.TrimEnd('/') + "/" + AppearanceFontRoot + "/" + name + "/" + name + " Gallery SDF.asset");
        }

        internal static TextStudioFontAppearanceData LoadAppearanceFontData(string id)
        {
            TMP_FontAsset font = LoadAppearanceDisplayFont(id);
            if (font == null) return null;
            string directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(font)).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<TextStudioFontAppearanceData>(directory + "/" + ResolveAppearanceFontName(id) + " Appearance.asset");
        }

        internal static void ConfigureAppearanceResources(TextStudioText hub, string id)
        {
            if (hub == null) return;
            TextStudioFontAppearanceData data = LoadAppearanceFontData(id);
            if (data != null && hub.TMPText != null && data.Font == hub.TMPText.font) hub.SetAppearanceFont(data);
        }

        internal static bool ConfigureAppearanceTypography(TMP_Text text, string id, float fontSize)
        {
            if (text == null || string.IsNullOrEmpty(id) || !id.StartsWith("Appearance", StringComparison.Ordinal))
                return false;
            TMP_FontAsset font = LoadAppearanceDisplayFont(id);
            if (font == null)
            {
                Debug.LogWarning("[Text Studio] The gallery display font is missing for " + id + ". Reimport the Text Studio fonts to restore this example.");
                return false;
            }
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            text.enableAutoSizing = false;
            UnityUtils.SetTextMeshProWordWrapping(text, false);
            return true;
        }

        internal static float ResolveAppearancePreviewProgress(string id)
        {
            return id == "AppearanceFocusPull" ? 0.08f : 0.42f;
        }
    }
}
