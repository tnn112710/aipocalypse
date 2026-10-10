using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace TextStudio.Editor
{
    internal partial class TextStudioTextEditor
    {
        // Route, reason and scheduled work of the latest presentation update, plus the shared renderer state behind it.
        // Values come from the executed renderer, never from re-evaluating route heuristics in the inspector.
        static Action AddRenderRouteRows(VisualElement section, TextStudioText hub)
        {
            Label route = AddAppearanceSummaryRow(section, "Route");
            Label work = AddAppearanceSummaryRow(section, "Scheduled Work");
            Label shared = AddAppearanceSummaryRow(section, "Shared Renderer");
            return () =>
            {
                if (hub == null) return;
                TextStudio2DRenderStatistics statistics = hub.RenderStatistics;
                string routeText = ObjectNames.NicifyVariableName(statistics.Route.ToString());
                if (statistics.Reason != TextStudio2DRouteReason.None)
                    routeText += " · " + ObjectNames.NicifyVariableName(statistics.Reason.ToString());
                if (statistics.WorldBatched && hub.WorldBatchGroup != null)
                    routeText += $" · drawn by '{hub.WorldBatchGroup.name}'";
                SetText(route, routeText);

                string workText = statistics.WorldBatched
                    ? "Counted on the World Batch Group"
                    : Plural(statistics.DirectDraws, "direct draw");
                if (statistics.OffscreenWork > 0)
                    workText += $" · offscreen {Plural(statistics.OffscreenLayerDraws, "layer draw")}, {Plural(statistics.CompositePasses, "composite")}, " +
                                $"{Plural(statistics.FilterPasses, "filter")}, {Plural(statistics.PublishPasses, "publish", "es")} at {statistics.CompositionWidth}×{statistics.CompositionHeight}";
                if (statistics.ShadowDraws > 0)
                    workText += " · " + Plural(statistics.ShadowDraws, "shadow draw");
                SetText(work, workText);

                TextStudio2DStyleTableStatistics programs = TextStudio2DStyleTable.Shared.Statistics;
                TextStudio2DHeroOutputPages pages = TextStudio2DHeroOutputPages.Shared;
                SetText(shared, $"{ObjectNames.NicifyVariableName(TextStudio2DTransport.Active.ToString())} · {Plural(programs.ProgramCount, "program")} · " +
                                $"{Plural(pages.PageCount, "output page")} ({Plural(pages.SliceCount, "slice")}, {pages.ReservedBytes / (1024f * 1024f):0.#} MiB) · " +
                                $"{Plural(TextStudio2DHeroSubmission.SubmissionCount, "Hero submission")}");
            };

            static string Plural(int count, string noun, string suffix = "s") => count + " " + noun + (count == 1 ? string.Empty : suffix);

            static void SetText(Label label, string text)
            {
                if (label.text != text) label.text = text;
            }
        }
    }
}
