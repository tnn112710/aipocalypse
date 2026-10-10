using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    public enum TextStudioPreviewStageMode
    {
        Automatic,
        Flat2D,
        Renderer,
        Compare
    }

    public enum TextStudioPreviewView
    {
        Front,
        Perspective,
        Side,
        Orbit
    }

    public enum TextStudioPreviewQuality
    {
        Draft,
        Full
    }

    [Flags]
    public enum TextStudioPreviewStageCapabilities
    {
        None = 0,
        Perspective = 1 << 0,
        Side = 1 << 1,
        Orbit = 1 << 2,
        Compare = 1 << 3,
        DraftQuality = 1 << 4,
        MaterialDiagnostics = 1 << 5,
        LightingDiagnostics = 1 << 6
    }

    public enum TextStudioPreviewDiagnosticSeverity
    {
        Information,
        Success,
        Warning,
        Error
    }

    /// <summary>Immutable renderer-preview mode, viewpoint, quality, and comparison layout.</summary>
    public readonly struct TextStudioPreviewStageSettings
    {
        public readonly TextStudioPreviewStageMode Mode;
        public readonly TextStudioPreviewView View;
        public readonly TextStudioPreviewQuality Quality;
        public readonly Vector2 OrbitAngles;
        public readonly float CompareSpacing;

        /// <summary>Creates a preview request, clamping comparison spacing to a non-negative value.</summary>
        public TextStudioPreviewStageSettings(
            TextStudioPreviewStageMode mode,
            TextStudioPreviewView view,
            TextStudioPreviewQuality quality,
            Vector2 orbitAngles,
            float compareSpacing)
        {
            Mode = mode;
            View = view;
            Quality = quality;
            OrbitAngles = orbitAngles;
            CompareSpacing = Mathf.Max(0f, compareSpacing);
        }
    }

    /// <summary>Renderer-supplied camera framing for a preview stage.</summary>
    public readonly struct TextStudioPreviewCameraPose
    {
        public readonly Vector3 Pivot;
        public readonly Quaternion Rotation;
        public readonly float Size;

        /// <summary>Creates a camera pose with a positive orthographic or framing size.</summary>
        public TextStudioPreviewCameraPose(Vector3 pivot, Quaternion rotation, float size)
        {
            Pivot = pivot;
            Rotation = rotation;
            Size = Mathf.Max(0.01f, size);
        }
    }

    /// <summary>Renderer-specific preview status with an optional suggested action.</summary>
    public readonly struct TextStudioPreviewDiagnostic
    {
        public readonly TextStudioPreviewDiagnosticSeverity Severity;
        public readonly string Message;
        public readonly string Action;

        /// <summary>Creates a diagnostic with normalized non-null text.</summary>
        public TextStudioPreviewDiagnostic(
            TextStudioPreviewDiagnosticSeverity severity,
            string message,
            string action = "")
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Action = action ?? string.Empty;
        }
    }

    /// <summary>
    /// Optional renderer extension for the shared Text Studio preview lifecycle.
    /// The base owns mode/view/quality selection and playback; renderers configure
    /// only their representative stage and report renderer-specific diagnostics.
    /// </summary>
    public interface ITextStudioPreviewStageProvider
    {
        /// <summary>Gets the stable provider identifier persisted by preview selection.</summary>
        string PreviewStageId { get; }
        /// <summary>Gets the user-facing renderer preview name.</summary>
        string PreviewStageName { get; }
        /// <summary>Gets whether the renderer can currently construct its preview stage.</summary>
        bool IsPreviewStageAvailable { get; }
        /// <summary>Gets the views, quality modes, and diagnostics supported by the provider.</summary>
        TextStudioPreviewStageCapabilities PreviewStageCapabilities { get; }

        /// <summary>Applies renderer-specific stage configuration without taking ownership of playback.</summary>
        void ConfigurePreviewStage(in TextStudioPreviewStageSettings settings);
        /// <summary>Restores stage state changed by preview configuration.</summary>
        void ResetPreviewStage();
        /// <summary>Tries to supply renderer-appropriate camera framing for the requested preview.</summary>
        bool TryGetPreviewCameraPose(
            in TextStudioPreviewStageSettings settings,
            out TextStudioPreviewCameraPose pose);
        /// <summary>Appends current renderer warnings and readiness information to the supplied list.</summary>
        void CollectPreviewDiagnostics(List<TextStudioPreviewDiagnostic> diagnostics);
    }
}
