using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal static class TextStudioAppearanceConversion
    {
        internal static bool IsAdHoc(string source, int tagStart, TextStudioTagAnalysisContext context)
        {
            foreach (TextStudioTagDiagnostic tag in TextStudioAppearanceAuthoringMetadata.GetAnalysis(context, source).Tags)
                if (tag.SourceStart == tagStart && !tag.IsClosing && tag.Kind == TextStudioTagKind.Appearance)
                    return TextStudioAppearanceTagSyntax.TryClassify(tag.Name, false, TextStudioTagParameterParser.Parse(tag.RawParameter, tag.HasParameter), out _, out bool adHoc) && adHoc;
            return false;
        }

        internal static bool TryConvert(TextStudio2DStyleSettings sourceSettings, string source, int tagStart, TextStudioTagAnalysisContext context, out TextStudio2DStyleSettings settings, out TextStudioTagSourceEditor.EditResult edit, out string error)
        {
            settings = sourceSettings.Clone(); edit = default; error = string.Empty;
            TextStudioDiagnosticReport report = TextStudioAppearanceAuthoringMetadata.GetAnalysis(context, source);
            int openingIndex = -1;
            for (int i = 0; i < report.Tags.Length; i++) if (report.Tags[i].SourceStart == tagStart && !report.Tags[i].IsClosing) { openingIndex = i; break; }
            if (openingIndex < 0 || !IsAdHoc(source, tagStart, context)) return false;
            TextStudioTagDiagnostic opening = report.Tags[openingIndex];
            TextStudioTagParameters parameters = TextStudioTagParameterParser.Parse(opening.RawParameter, opening.HasParameter);
            TextStudioAppearanceApplyMode mode = parameters.TryGetString("mode", out string authoredMode) && string.Equals(authoredMode, "overlay", StringComparison.OrdinalIgnoreCase) ? TextStudioAppearanceApplyMode.Overlay : TextStudioAppearanceApplyMode.Replace;
            List<string> diagnostics = new List<string>();
            TextStudio2DStyleSettings content = TextStudioAppearancePlanCompiler.CreateAdHoc(parameters, mode, diagnostics);
            if (diagnostics.Count > 0) { error = string.Join("\n", diagnostics); return false; }
            TextStudioAppearanceDefinitionTable definitions = TextStudioAppearanceDefinitions.Resolve(settings);
            string id = "style";
            for (int suffix = 2; definitions.TryGet(id, out _); suffix++) id = "style-" + suffix;
            string preferred = TextStudioAppearanceTagSyntax.GetPreferredOpeningTag(id);
            if (preferred == null) { error = "Both appearance spellings are occupied by existing tag registrations."; return false; }
            TextStudio2DLayer group = TextStudio2DLayer.CreateGroup("Inline Style").WithAppearanceActivation(TextStudioAppearanceActivation.Inline(id, mode));
            List<TextStudio2DLayer> layers = new List<TextStudio2DLayer>(settings.CloneAuthoredLayers()) { group };
            for (int i = 0; i < content.ItemCount; i++) layers.Add(content.GetLayer(i).WithRelationships(group.ItemId));
            settings.SetLayers(layers.ToArray());
            StringBuilder namedOpening = new StringBuilder(preferred.Substring(0, preferred.Length - 1));
            foreach (TextStudioNamedTagParameter parameter in parameters.Named)
            {
                if (!string.Equals(parameter.Name, "tint", StringComparison.OrdinalIgnoreCase) && !string.Equals(parameter.Name, "opacity", StringComparison.OrdinalIgnoreCase) && !string.Equals(parameter.Name, "mode", StringComparison.OrdinalIgnoreCase)) continue;
                namedOpening.Append(' ').Append(parameter.Name).Append("=\"").Append(parameter.Value).Append('"');
            }
            namedOpening.Append('>');
            string next = source;
            int depth = 1;
            for (int i = openingIndex + 1; i < report.Tags.Length; i++)
            {
                TextStudioTagDiagnostic tag = report.Tags[i];
                if (tag.Kind != TextStudioTagKind.Appearance || !string.Equals(tag.Name, "style", StringComparison.OrdinalIgnoreCase)) continue;
                depth += tag.IsClosing ? -1 : 1;
                if (depth != 0) continue;
                string closing = preferred.StartsWith("<$", StringComparison.Ordinal) ? "</$" + id + ">" : "</style>";
                next = TextStudioTagSourceEditor.ReplaceRange(next, tag.SourceStart, tag.SourceEnd, closing);
                break;
            }
            next = TextStudioTagSourceEditor.ReplaceRange(next, opening.SourceStart, opening.SourceEnd, namedOpening.ToString());
            int caret = opening.SourceStart + namedOpening.Length;
            edit = new TextStudioTagSourceEditor.EditResult(next, caret, caret);
            return true;
        }
    }

    internal partial class TextStudioTextEditor
    {
        void ConvertAdHocAppearance(TextStudioText hub, TextStudioInlineTagHelpState state, TextField source)
        {
            TextStudioTagAnalysisContext context = TextStudioTagInspectorContext.Build(hub, state.SourceText);
            if (!TextStudioAppearanceConversion.TryConvert(hub.ActiveAppearanceSettings, state.SourceText, state.TagStart, context, out TextStudio2DStyleSettings settings, out TextStudioTagSourceEditor.EditResult edit, out string error))
            {
                if (!string.IsNullOrEmpty(error)) EditorUtility.DisplayDialog("Convert Appearance", error, "OK");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.RecordObject(hub, "Convert to Inline Appearance Style");
            hub.ApplyAppearanceSettings(settings);
            ApplyMarkupEdit(hub, edit, source);
            Undo.SetCurrentGroupName("Convert to Inline Appearance Style");
            Undo.CollapseUndoOperations(group);
            TextStudioAppearanceAuthoringMetadata.Invalidate();
        }
    }
}
