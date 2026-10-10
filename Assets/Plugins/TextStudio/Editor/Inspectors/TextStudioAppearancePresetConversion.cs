using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TextStudio.Editor
{
    internal static class TextStudioAppearancePresetConversion
    {
        internal static string GetUnavailableReason(Object[] owners)
        {
            if (owners == null || owners.Length == 0)
                return "Select a Text Studio object to convert its appearance.";
            foreach (Object owner in owners)
            {
                if (owner is not TextStudioText text ||
                    text.AppearanceSource != TextStudioAppearanceSource.Style)
                    return "Select Catalog or Custom on every selected object to make their appearances inline.";
                if (text.AppearanceStyle == null)
                    return "Assign an appearance preset on every selected object before converting to Inline.";
                CompiledTextStudio2DStyle compiled = text.CompiledAppearanceStyle;
                if (compiled == null || compiled.IsValid)
                    continue;
                for (int i = 0; i < compiled.DiagnosticCount; i++)
                {
                    TextStudioStackDiagnostic diagnostic = compiled.GetDiagnostic(i);
                    if (diagnostic.Severity == TextStudioStackDiagnosticSeverity.Error)
                        return diagnostic.Message + " Fix the preset before converting it to Inline.";
                }
            }
            return string.Empty;
        }

        internal static bool ConvertToInline(Object[] owners, out string error)
        {
            error = GetUnavailableReason(owners);
            if (!string.IsNullOrEmpty(error))
                return false;

            List<TextStudio2DStyleSettings> snapshots = new List<TextStudio2DStyleSettings>(owners.Length);
            foreach (Object owner in owners)
            {
                TextStudioText text = (TextStudioText)owner;
                if (!TextStudio2DStyleCompiler.TryCreateInlineSettings(
                        text.AppearanceStyle, out TextStudio2DStyleSettings settings, out error))
                    return false;
                snapshots.Add(settings);
            }

            Undo.RecordObjects(owners, "Convert Appearance Preset to Inline");
            for (int i = 0; i < owners.Length; i++)
            {
                TextStudioText text = (TextStudioText)owners[i];
                TextStudio2DStyleSelection.State(text).Remember();
                text.ApplyAppearanceSettings(snapshots[i]);
                TextStudioStyleCatalogProvider.MarkChanged(text);
            }
            return true;
        }
    }
}
