using System;
using UnityEngine;

namespace TextStudio
{
    [Flags]
    internal enum TextStudioSelectorCapabilities : byte
    {
        LayoutStable = 0,
        SourceColor = 1,
        SemanticState = 2,
        Coverage = 4,
        RenderProperty = 8,
        Appearance = SourceColor | SemanticState | Coverage | RenderProperty
    }

    /// <summary>Validated immutable selector program, independent of any renderer or consumer.</summary>
    internal readonly struct TextStudioCompiledSelector
    {
        readonly TextStudioApplicationSelectorNode[] _nodes;

        internal TextStudioCompiledSelector(TextStudioApplicationSelectorNode[] nodes, int root)
        {
            _nodes = nodes;
            Root = root;
        }

        internal int Root { get; }
        internal int NodeCount => _nodes?.Length ?? 0;
        internal bool IsTrivialAll => NodeCount == 1 && Root == 0 &&
                                      _nodes[0].Kind == TextStudioSelectorNodeKind.All && !_nodes[0].Invert;
        internal TextStudioApplicationSelectorNode GetNode(int index) => _nodes[index];
        internal static TextStudioCompiledSelector None => new TextStudioCompiledSelector(
            new[] { TextStudioApplicationSelectorNode.None() }, 0);
    }

    internal static class TextStudioSelectorCompiler
    {
        const int MaximumNodes = 256;
        const int MaximumDepth = 64;

        internal static bool TryCompile(in TextStudioApplicationSelector selector,
            TextStudioSelectorCapabilities capabilities, out TextStudioCompiledSelector compiled,
            out string diagnostic)
        {
            return TryCompile(in selector, capabilities, out compiled, out diagnostic, out _);
        }

        internal static bool TryCompile(in TextStudioApplicationSelector selector,
            TextStudioSelectorCapabilities capabilities, out TextStudioCompiledSelector compiled,
            out string diagnostic, out string diagnosticCode)
        {
            TextStudioApplicationSelectorNode[] authored = selector.GetNodesUnsafe();
            if (authored == null || authored.Length == 0)
            {
                compiled = new TextStudioCompiledSelector(new[] { TextStudioApplicationSelectorNode.All() }, 0);
                diagnostic = string.Empty;
                diagnosticCode = string.Empty;
                return true;
            }
            if (authored.Length > MaximumNodes)
                return Invalid("A selector cannot exceed 256 nodes.", "selector-capacity", out compiled, out diagnostic, out diagnosticCode);
            if (selector.Root < 0 || selector.Root >= authored.Length)
                return Invalid("The selector root is missing.", "selector-root", out compiled, out diagnostic, out diagnosticCode);
            byte[] states = new byte[authored.Length];
            if (!Validate(authored, selector.Root, states, capabilities, 0, out diagnostic, out diagnosticCode))
            {
                compiled = TextStudioCompiledSelector.None;
                return false;
            }
            compiled = new TextStudioCompiledSelector((TextStudioApplicationSelectorNode[])authored.Clone(), selector.Root);
            diagnosticCode = string.Empty;
            return true;
        }

        static bool Validate(TextStudioApplicationSelectorNode[] nodes, int index, byte[] states,
            TextStudioSelectorCapabilities capabilities, int depth, out string diagnostic, out string code)
        {
            if (index < 0 || index >= nodes.Length)
                return InvalidNode("A selector operator references a missing child.", "selector-child", out diagnostic, out code);
            if (depth > MaximumDepth)
                return InvalidNode("A selector cannot exceed 64 nested operators.", "selector-depth", out diagnostic, out code);
            if (states[index] == 1)
                return InvalidNode("The selector contains a cycle.", "selector-cycle", out diagnostic, out code);
            if (states[index] == 2)
            {
                diagnostic = string.Empty;
                code = string.Empty;
                return true;
            }
            states[index] = 1;
            TextStudioApplicationSelectorNode node = nodes[index];
            const TextStudioSelectorSemanticState supportedStates =
                TextStudioSelectorSemanticState.Visible | TextStudioSelectorSemanticState.Hidden |
                TextStudioSelectorSemanticState.Revealing | TextStudioSelectorSemanticState.Active |
                TextStudioSelectorSemanticState.Hiding | TextStudioSelectorSemanticState.FirstInWord |
                TextStudioSelectorSemanticState.LastInWord | TextStudioSelectorSemanticState.FirstInLine |
                TextStudioSelectorSemanticState.LastInLine;
            if ((uint)node.Kind > (uint)TextStudioSelectorNodeKind.AppearanceTag ||
                (uint)node.Comparison > (uint)TextStudioSelectorComparison.GreaterOrEqual ||
                (node.SemanticState & ~supportedStates) != 0)
                return InvalidNode("Selector node, comparison, and semantic-state values must be supported.",
                    "selector-enum", out diagnostic, out code);
            switch (node.Kind)
            {
                case TextStudioSelectorNodeKind.All:
                case TextStudioSelectorNodeKind.None:
                    break;
                case TextStudioSelectorNodeKind.Range:
                    if (node.UsesTextRange ? !node.TextRange.IsValid :
                        node.Start < 0 || node.Count < 0 || node.Start > int.MaxValue - node.Count)
                        return InvalidNode("The range contains an invalid endpoint.", "selector-range", out diagnostic, out code);
                    break;
                case TextStudioSelectorNodeKind.Tag:
                case TextStudioSelectorNodeKind.AppearanceTag:
                    if (string.IsNullOrWhiteSpace(node.Key))
                        return InvalidNode("A tag selector needs a tag name.", "selector-key", out diagnostic, out code);
                    break;
                case TextStudioSelectorNodeKind.And:
                case TextStudioSelectorNodeKind.Or:
                case TextStudioSelectorNodeKind.Within:
                    if (!Validate(nodes, node.Left, states, capabilities, depth + 1, out diagnostic, out code) ||
                        !Validate(nodes, node.Right, states, capabilities, depth + 1, out diagnostic, out code))
                        return false;
                    break;
                case TextStudioSelectorNodeKind.Not:
                    if (!Validate(nodes, node.Left, states, capabilities, depth + 1, out diagnostic, out code))
                        return false;
                    break;
                case TextStudioSelectorNodeKind.SourceColor:
                    if ((capabilities & TextStudioSelectorCapabilities.SourceColor) == 0)
                        return InvalidNode("Source color is unavailable for this selector.", "selector-capability", out diagnostic, out code);
                    Color color = node.Color;
                    if (!float.IsFinite(color.r) || !float.IsFinite(color.g) ||
                        !float.IsFinite(color.b) || !float.IsFinite(color.a) ||
                        !float.IsFinite(node.Tolerance) || node.Tolerance < 0f)
                        return InvalidNode("Source color and tolerance must be finite and tolerance non-negative.",
                            "selector-color-tolerance", out diagnostic, out code);
                    break;
                case TextStudioSelectorNodeKind.SemanticState:
                    if ((capabilities & TextStudioSelectorCapabilities.SemanticState) == 0)
                        return InvalidNode("Reveal state is unavailable for this selector.", "selector-capability", out diagnostic, out code);
                    break;
                case TextStudioSelectorNodeKind.Coverage:
                    if ((capabilities & TextStudioSelectorCapabilities.Coverage) == 0)
                        return InvalidNode("Coverage is unavailable for this selector.", "selector-capability", out diagnostic, out code);
                    if (!float.IsFinite(node.Value))
                        return InvalidNode("Selector comparison values must be finite.", "selector-value", out diagnostic, out code);
                    break;
                case TextStudioSelectorNodeKind.RenderProperty:
                    if ((capabilities & TextStudioSelectorCapabilities.RenderProperty) == 0)
                        return InvalidNode("Render properties are unavailable for this selector.", "selector-capability", out diagnostic, out code);
                    if (string.IsNullOrWhiteSpace(node.Key))
                        return InvalidNode("A renderer-property selector needs a key.", "selector-key", out diagnostic, out code);
                    if (!float.IsFinite(node.Value))
                        return InvalidNode("Selector comparison values must be finite.", "selector-value", out diagnostic, out code);
                    break;
                default:
                    return InvalidNode("The selector contains an unsupported node.", "selector-enum", out diagnostic, out code);
            }
            states[index] = 2;
            diagnostic = string.Empty;
            code = string.Empty;
            return true;
        }

        static bool Invalid(string message, string code, out TextStudioCompiledSelector compiled,
            out string diagnostic, out string diagnosticCode)
        {
            compiled = TextStudioCompiledSelector.None;
            diagnostic = message;
            diagnosticCode = code;
            return false;
        }

        static bool InvalidNode(string message, string diagnosticCode, out string diagnostic, out string code)
        {
            diagnostic = message;
            code = diagnosticCode;
            return false;
        }
    }
}
