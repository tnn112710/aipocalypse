namespace TextStudio
{
    /// <summary>
    /// Read-only snapshot of how one text rendered its 2D Appearance in the latest presentation update. Categories are
    /// disjoint, so their sum is the text's scheduled work: direct draws present the visible output, offscreen draws and
    /// passes build an isolated composition, and shadow draws render only into shadow maps. Counts are scheduled
    /// submissions before engine batching; the Frame Debugger or a GPU profiler measures merged hardware draws.
    /// </summary>
    public readonly struct TextStudio2DRenderStatistics
    {
        /// <summary>How the text's Appearance executes.</summary>
        public TextStudio2DRoute Route { get; }
        /// <summary>The first condition that selected <see cref="Route"/>.</summary>
        public TextStudio2DRouteReason Reason { get; }
        /// <summary>Draws presenting the visible output: native field submeshes, ordered primitive runs or the composed quad.</summary>
        public int DirectDraws { get; }
        /// <summary>True while a <see cref="TextStudioWorldBatchGroup"/> draws this text; its draws count on the group.</summary>
        public bool WorldBatched { get; }
        /// <summary>Glyph and surface draws into offscreen composition targets.</summary>
        public int OffscreenLayerDraws { get; }
        /// <summary>Full-target blend passes that combine offscreen surfaces, excluding filter passes.</summary>
        public int CompositePasses { get; }
        /// <summary>Blur, warp, history, lens and other image-filter passes.</summary>
        public int FilterPasses { get; }
        /// <summary>Copies of a finished composition into its shared output page slice.</summary>
        public int PublishPasses { get; }
        /// <summary>Draws into shadow maps for alpha-clipped world shadows.</summary>
        public int ShadowDraws { get; }
        /// <summary>Offscreen composition size in pixels, or zero without a composition.</summary>
        public int CompositionWidth { get; }
        /// <summary>Offscreen composition size in pixels, or zero without a composition.</summary>
        public int CompositionHeight { get; }

        internal TextStudio2DRenderStatistics(TextStudio2DRoute route, TextStudio2DRouteReason reason, int directDraws, bool worldBatched,
            int offscreenLayerDraws, int compositePasses, int filterPasses, int publishPasses, int shadowDraws, int width, int height)
        {
            Route = route;
            Reason = reason;
            DirectDraws = directDraws;
            WorldBatched = worldBatched;
            OffscreenLayerDraws = offscreenLayerDraws;
            CompositePasses = compositePasses;
            FilterPasses = filterPasses;
            PublishPasses = publishPasses;
            ShadowDraws = shadowDraws;
            CompositionWidth = width;
            CompositionHeight = height;
        }

        /// <summary>Scheduled offscreen work: layer draws plus composite, filter and publish passes.</summary>
        public int OffscreenWork => OffscreenLayerDraws + CompositePasses + FilterPasses + PublishPasses;
    }
}
