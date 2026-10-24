using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Identifies a selector condition or composition node, including text ranges, tags, source colors, semantic state, coverage, properties, and Boolean or scoped combinations.</summary>
    public enum TextStudioSelectorNodeKind : byte
    {
        All = 0,
        None = 1,
        Range = 2,
        Tag = 3,
        SourceColor = 4,
        SemanticState = 5,
        Coverage = 6,
        RenderProperty = 7,
        And = 8,
        Or = 9,
        Not = 10,
        Within = 11,
        AppearanceTag = 12
    }

    /// <summary>Selects the equality or ordered comparison used by a numeric or render-property selector condition.</summary>
    public enum TextStudioSelectorComparison : byte
    {
        Equal = 0,
        NotEqual = 1,
        Less = 2,
        LessOrEqual = 3,
        Greater = 4,
        GreaterOrEqual = 5
    }

    /// <summary>Flags the visibility, reveal phase, and word or line boundary states that a selector can match on text units.</summary>
    [Flags]
    public enum TextStudioSelectorSemanticState : ushort
    {
        None = 0,
        Visible = 1 << 0,
        Hidden = 1 << 1,
        Revealing = 1 << 2,
        Active = 1 << 3,
        Hiding = 1 << 4,
        FirstInWord = 1 << 5,
        LastInWord = 1 << 6,
        FirstInLine = 1 << 7,
        LastInLine = 1 << 8
    }

    /// <summary>One node in a compact indexed application-selector tree.</summary>
    [Serializable]
    public struct TextStudioApplicationSelectorNode
    {
        [SerializeField] TextStudioSelectorNodeKind _kind;
        [SerializeField] int _left;
        [SerializeField] int _right;
        [SerializeField, Min(0)] int _start;
        [SerializeField, Min(0)] int _count;
        [SerializeField] TextStudioTextRange _textRange;
        [SerializeField] bool _useTextRange;
        [SerializeField] string _key;
        [SerializeField] Color _color;
        [SerializeField, TextStudio2DParameter(0f), Tooltip("Maximum source-color difference. Must be non-negative; HDR source colors may require values above 1.")] float _tolerance;
        [SerializeField] float _value;
        [SerializeField] TextStudioSelectorComparison _comparison;
        [SerializeField] TextStudioSelectorSemanticState _semanticState;
        [SerializeField] bool _invert;

        public TextStudioSelectorNodeKind Kind => _kind;
        public int Left => _left;
        public int Right => _right;
        public int Start => _start;
        public int Count => _count;
        public TextStudioTextRange TextRange => _textRange;
        public bool UsesTextRange => _useTextRange;
        public string Key => _key ?? string.Empty;
        public Color Color => _color;
        public float Tolerance => _tolerance;
        public float Value => _value;
        public TextStudioSelectorComparison Comparison => _comparison;
        public TextStudioSelectorSemanticState SemanticState => _semanticState;
        public bool Invert => _invert;

        internal TextStudioApplicationSelectorNode WithChildren(int left, int right)
        {
            TextStudioApplicationSelectorNode value = this;
            value._left = left;
            value._right = right;
            return value;
        }

        internal TextStudioApplicationSelectorNode WithNodeOffset(int offset)
        {
            TextStudioApplicationSelectorNode value = this;
            if (value._left >= 0) value._left += offset;
            if (value._right >= 0) value._right += offset;
            return value;
        }

        /// <summary>Creates a predicate node that selects every logical text unit.</summary>
        public static TextStudioApplicationSelectorNode All()
        {
            return Create(TextStudioSelectorNodeKind.All);
        }

        /// <summary>Creates a predicate node that selects no logical text units.</summary>
        public static TextStudioApplicationSelectorNode None()
        {
            return Create(TextStudioSelectorNodeKind.None);
        }

        /// <summary>Creates an optionally inverted predicate for a start-and-count range of logical units within the current selector scope.</summary>
        public static TextStudioApplicationSelectorNode Range(
            int start,
            int count,
            bool invert = false)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.Range);
            node._start = start;
            node._count = count;
            node._invert = invert;
            return node;
        }

        /// <summary>Creates an optionally inverted range predicate resolved from a half-open text range within the current selector scope.</summary>
        public static TextStudioApplicationSelectorNode Range(TextStudioTextRange range, bool invert = false)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.Range);
            node._textRange = range;
            node._useTextRange = true;
            node._invert = invert;
            return node;
        }

        /// <summary>Creates an optionally inverted predicate for units inside the named text region.</summary>
        public static TextStudioApplicationSelectorNode Tag(string tag, bool invert = false)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.Tag);
            node._key = tag ?? string.Empty;
            node._invert = invert;
            return node;
        }

        /// <summary>Creates an optionally inverted predicate for units covered by an invocation of the named appearance.</summary>
        public static TextStudioApplicationSelectorNode AppearanceTag(string id, bool invert = false)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.AppearanceTag);
            node._key = id ?? string.Empty;
            node._invert = invert;
            return node;
        }

        /// <summary>Creates an optionally inverted predicate matching source color within the supplied tolerance.</summary>
        public static TextStudioApplicationSelectorNode SourceColor(
            Color color,
            float tolerance,
            bool invert = false)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.SourceColor);
            node._color = color;
            node._tolerance = tolerance;
            node._invert = invert;
            return node;
        }

        /// <summary>Creates an optionally inverted predicate matching any requested semantic-state flag.</summary>
        public static TextStudioApplicationSelectorNode Semantic(
            TextStudioSelectorSemanticState state,
            bool invert = false)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.SemanticState);
            node._semanticState = state;
            node._invert = invert;
            return node;
        }

        /// <summary>Creates a predicate comparing a text unit's current coverage with the supplied scalar value.</summary>
        public static TextStudioApplicationSelectorNode Coverage(
            TextStudioSelectorComparison comparison,
            float value)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.Coverage);
            node._comparison = comparison;
            node._value = value;
            return node;
        }

        /// <summary>Creates a predicate comparing a render property's unsigned value or first vector component with the supplied scalar.</summary>
        public static TextStudioApplicationSelectorNode RenderProperty(
            string propertyId,
            TextStudioSelectorComparison comparison,
            float value)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.RenderProperty);
            node._key = propertyId ?? string.Empty;
            node._comparison = comparison;
            node._value = value;
            return node;
        }

        /// <summary>Creates a conjunction of the predicates at the supplied node indices.</summary>
        public static TextStudioApplicationSelectorNode And(int left, int right)
        {
            return Binary(TextStudioSelectorNodeKind.And, left, right);
        }

        /// <summary>Creates a disjunction of the predicates at the supplied node indices.</summary>
        public static TextStudioApplicationSelectorNode Or(int left, int right)
        {
            return Binary(TextStudioSelectorNodeKind.Or, left, right);
        }

        /// <summary>Evaluates the child separately in each logical occurrence of the parent.</summary>
        public static TextStudioApplicationSelectorNode Within(int parent, int child)
        {
            return Binary(TextStudioSelectorNodeKind.Within, parent, child);
        }

        /// <summary>Creates a predicate that negates the child at the supplied node index.</summary>
        public static TextStudioApplicationSelectorNode Not(int child)
        {
            TextStudioApplicationSelectorNode node = Create(TextStudioSelectorNodeKind.Not);
            node._left = child;
            return node;
        }

        static TextStudioApplicationSelectorNode Binary(
            TextStudioSelectorNodeKind kind,
            int left,
            int right)
        {
            TextStudioApplicationSelectorNode node = Create(kind);
            node._left = left;
            node._right = right;
            return node;
        }

        static TextStudioApplicationSelectorNode Create(TextStudioSelectorNodeKind kind)
        {
            return new TextStudioApplicationSelectorNode
            {
                _kind = kind,
                _left = -1,
                _right = -1,
                _start = 0,
                _count = 0,
                _key = string.Empty,
                _color = Color.white,
                _tolerance = 0.05f,
                _value = 0f,
                _comparison = TextStudioSelectorComparison.GreaterOrEqual,
                _semanticState = TextStudioSelectorSemanticState.None,
                _invert = false
            };
        }
    }

    /// <summary>
    /// Compact typed predicate tree used by persistent style layers and reusable by animation filters.
    /// Nodes are indexed rather than recursively allocated so evaluation can remain allocation free.
    /// </summary>
    [Serializable]
    public struct TextStudioApplicationSelector
    {
        [SerializeField] int _root;
        [SerializeField] TextStudioApplicationSelectorNode[] _nodes;

        public int Root => _root;
        public int NodeCount => _nodes?.Length ?? 0;
        public bool IsTrivialAll => NodeCount == 0 ||
                                    _root == 0 && _nodes[0].Kind == TextStudioSelectorNodeKind.All;

        public static TextStudioApplicationSelector All => new TextStudioApplicationSelector
        {
            _root = 0,
            _nodes = new[] { TextStudioApplicationSelectorNode.All() }
        };

        public static TextStudioApplicationSelector None => new TextStudioApplicationSelector
        {
            _root = 0,
            _nodes = new[] { TextStudioApplicationSelectorNode.None() }
        };

        /// <summary>Creates a selector for text units covered by an invocation of the named appearance.</summary>
        public static TextStudioApplicationSelector AppearanceTag(string id)
            => Create(new[] { TextStudioApplicationSelectorNode.AppearanceTag(id) }, 0);

        /// <summary>Creates a selector by taking a defensive copy of the indexed node program.</summary>
        public static TextStudioApplicationSelector Create(
            TextStudioApplicationSelectorNode[] nodes,
            int root)
        {
            return new TextStudioApplicationSelector
            {
                _root = root,
                _nodes = nodes != null
                    ? (TextStudioApplicationSelectorNode[])nodes.Clone()
                    : Array.Empty<TextStudioApplicationSelectorNode>()
            };
        }

        /// <summary>Attempts to retrieve an indexed selector node, returning a default node when the index is invalid.</summary>
        public bool TryGetNode(int index, out TextStudioApplicationSelectorNode node)
        {
            if (_nodes != null && (uint)index < (uint)_nodes.Length)
            {
                node = _nodes[index];
                return true;
            }
            node = default;
            return false;
        }

        internal TextStudioApplicationSelectorNode[] GetNodesUnsafe()
        {
            return _nodes;
        }
    }
}
