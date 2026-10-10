using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Text outputs a built-in reaction recipe is designed for. Renderer Independent recipes suit every host;
    /// otherwise a recipe is offered when a host has at least one matching configured output.
    /// </summary>
    [Flags]
    public enum TextStudioReactionOutputKinds
    {
        RendererIndependent = 0,
        /// <summary>Canvas and UI Toolkit text.</summary>
        UI = 1 << 0,
        /// <summary>Flat world-space text such as a TextMeshPro component outside a Canvas.</summary>
        World = 1 << 1,
        /// <summary>Extruded 3D glyph output.</summary>
        Extruded3D = 1 << 2
    }

    /// <summary>Package-owned reaction assets exposed through the shared Built-In picker.</summary>
    public sealed class TextStudioReactionBuiltInLibrary : ScriptableObject
    {
        /// <summary>Authored catalog entry linking a built-in reaction recipe to its display information, resource path, and supported outputs.</summary>
        [Serializable]
        public struct Entry
        {
            [SerializeField] string _id;
            [SerializeField] string _displayName;
            [SerializeField, TextArea(1, 3)] string _description;
            [SerializeField] string _resourcePath;
            [SerializeField, Tooltip("Text outputs this recipe is designed for. The Built-In picker offers it only " +
                                     "to hosts with a matching output. Renderer Independent recipes suit every host.")]
            TextStudioReactionOutputKinds _outputs;

            public string Id => _id;
            public string DisplayName => _displayName;
            public string Description => _description;
            public string ResourcePath => _resourcePath;
            public TextStudioReactionOutputKinds Outputs => _outputs;
        }

        [SerializeField] Entry[] _entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;

        void OnValidate() => BuiltIns.BuiltInReactionPresets.Invalidate();
    }
}
