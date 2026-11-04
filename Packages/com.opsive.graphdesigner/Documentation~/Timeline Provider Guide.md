# Graph Timeline Provider Guide

Graph Designer owns the timeline UI. A product owns the graph meaning. Implement `IGraphTimelineWorkspaceEditor` on the product's graph editor and return an `IGraphTimelineWorkspace` from `CreateTimelineWorkspace`.

## Workspace pages

A workspace contains one or more ordered `GraphTimelineWorkspacePage` instances. Each page has a stable ID, a title, and one `IGraphTimelineProvider`. Optional header, sidebar, and footer `VisualElement` surfaces let the product compose page-specific navigation, assignment, validation, and status controls around the shared timeline without replacing its transport or interaction behavior.

Use `GraphTimelineWorkspace` for the standard implementation. It owns every page provider, retains the active page while projections refresh, and disposes providers with the graph window. A product with one timeline still creates a one-page workspace; the page tab strip is hidden automatically. Switching pages pauses the outgoing playback controller and clears its transient Scene view gizmos.

## Provider responsibilities

Return an immutable `GraphTimelineSnapshot` whose channel and element IDs remain stable between refreshes. Channels may be nested to any depth. Elements may be clips, instant events, markers, ranges, links, or diagnostic spans. Links reference source and destination element IDs and may cross channels.

The provider receives selection and interaction callbacks. Selection should select the matching graph node or connection and let the existing Element Inspector update normally. Mutation callbacks own Undo, graph or referenced-asset changes, serialization, dependent timing, and the next snapshot. Graph Designer never schedules product data.

## Restrictions and validation

Use the narrowest layer that expresses a rule:

1. Omit a `GraphTimelineElementCapabilities` flag when an action is never legal. A fixed-duration clip should not advertise resize handles.
2. Attach `GraphTimelineElementConstraints` for common minimum/maximum time, duration, or allowed-channel limits.
3. Implement `IGraphTimelineInteractionValidator` for graph-dependent rules. It runs before `HandleInteraction` on begin, update, and commit, and must not mutate data.
4. Return a rejected `GraphTimelineInteractionResult` from the mutation callback if the graph changed after validation.

Every channel declares an overlap mode. `Disallow` rejects overlapping duration edits, `Allow` permits independent overlaps, and `Blend` permits overlaps whose semantic meaning is supplied by the product. Existing overlaps receive common visual emphasis.

## Interaction lifecycle

Move and resize gestures send begin, update, then commit. A rejected begin never captures the pointer. A rejected update leaves the last accepted visual position in place. Commit is the only phase that should create an Undo record or mutate canonical data. Add, remove, and duplicate are terminal commit interactions.

Asset drops include the dropped object, destination channel ID, and proposed time. Providers may create a graph node, add an item to an existing node, or reject the object with a concise message.

## Rich content and playback

Use `GraphTimelineContent` for labels, images, waveforms, and curves. Implement `IGraphTimelineContentFactory` only when the built-in representations are insufficient. The common frame retains selection, clipping, and edit handles.

Implement `IGraphTimelinePlaybackController` for play, pause, resume, stop, stepping, speed, looping, seeking, and scrubbing. Playback may be a product preview rather than runtime execution. Implement `IGraphTimelineGizmoProvider` when timeline selection or the full composite can be visualized in the Scene view. Dispose every editor callback and transient preview resource when the provider is disposed.

Implement `IGraphTimelinePlaybackReporter` when preview can encounter unsupported, ambiguous, or failed elements. Reports have an element ID, active time range, severity, and message. The common status strip displays reports at the playhead time. Keep the original element in the snapshot: reporting is a playback diagnostic, not permission to hide data that the provider cannot evaluate.

## Precompiled package consumers

Products that consume Graph Designer as DLLs must rebuild both `Opsive.GraphDesigner.Runtime.dll` and `Opsive.GraphDesigner.Editor.dll` from the matching source revision. They must also ship `Editor/Timeline/GraphTimelineStyles.uss` with its original meta GUID so the compiled panel can load its shared stylesheet.
