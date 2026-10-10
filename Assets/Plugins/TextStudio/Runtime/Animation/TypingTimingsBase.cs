using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Allocation-free semantic context supplied to custom reveal timing assets.
    /// </summary>
    public readonly struct TextStudioTimingContext
    {
        public TextStudioPlanView Plan { get; }
        public int UnitIndex { get; }
        public int WordIndex { get; }
        public int LineIndex { get; }
        public int ParagraphIndex { get; }
        public int ResolvedStart { get; }
        public int ResolvedLength { get; }
        public int PreviousUnitIndex { get; }
        public int NextUnitIndex { get; }
        public bool IsFirstUnit { get; }
        public bool IsLastUnit { get; }
        public bool IsLastVisibleUnit { get; }
        public bool IsWhiteSpace { get; }
        public bool IsLineBreak { get; }
        public bool IsPunctuation { get; }
        public bool IsInstant { get; }

        public string ResolvedText => Plan.ResolvedText;
        public char PrimaryCharacter => Plan.GetPrimaryChar(UnitIndex);

        internal TextStudioTimingContext(
            TextStudioCompiledPlan plan,
            int unitIndex,
            bool isInstant,
            int lastVisibleUnitIndex)
        {
            Plan = new TextStudioPlanView(plan);
            UnitIndex = unitIndex;
            WordIndex = TextSegmenter.GetWordIndexFast(plan?.GlyphToWordIndex, unitIndex);
            LineIndex = TextSegmenter.GetLineIndexFast(plan?.GlyphToLineIndex, unitIndex);
            ParagraphIndex = TextSegmenter.GetLineIndexFast(plan?.GlyphToParagraphIndex, unitIndex);
            ResolvedStart = plan != null ? plan.GetResolvedStart(unitIndex) : 0;
            ResolvedLength = plan != null ? plan.GetResolvedLength(unitIndex) : 0;
            PreviousUnitIndex = unitIndex > 0 ? unitIndex - 1 : -1;
            int unitCount = plan?.VisibleGlyphCount ?? 0;
            NextUnitIndex = unitIndex + 1 < unitCount ? unitIndex + 1 : -1;
            IsFirstUnit = unitIndex == 0;
            IsLastUnit = unitIndex + 1 >= unitCount;
            IsLastVisibleUnit =
                lastVisibleUnitIndex < 0 ||
                unitIndex >= lastVisibleUnitIndex;
            IsWhiteSpace = plan != null && plan.IsWhiteSpaceUnit(unitIndex);
            IsLineBreak = plan != null && plan.IsLineBreakUnit(unitIndex);
            IsPunctuation = plan != null && plan.IsPunctuationUnit(unitIndex);
            IsInstant = isInstant;
        }

        /// <summary>Gets a character relative to the current resolved unit, or a null character outside the transient plan.</summary>
        public char GetResolvedCharacter(int offset)
        {
            if (offset < 0 || offset >= ResolvedLength)
                return '\0';

            string text = ResolvedText;
            int index = ResolvedStart + offset;
            return index >= 0 && index < text.Length ? text[index] : '\0';
        }

    }

    /// <summary>Calculates the delay in seconds after one semantic text unit becomes visible.</summary>
    /// <param name="context">Transient allocation-free timing context.</param>
    /// <returns>A non-negative delay in seconds.</returns>
    public delegate float TextStudioTimingEvaluator(in TextStudioTimingContext context);

    /// <summary>
    /// Base class for semantic reveal pacing. Returned values are waits applied
    /// after the current text unit has become visible.
    /// </summary>
    public abstract class TypingTimingsBase : ScriptableObject
    {
        /// <summary>
        /// Whether the final revealed unit contributes its trailing delay before
        /// the reveal-complete event is raised. Custom timing assets can override
        /// this property when completion should coincide with final visibility.
        /// </summary>
        public virtual bool IncludesFinalRevealDelay => true;

        /// <summary>For Custom grouping, adjacent units with the same identifier begin together. Evaluated only when the schedule is built.</summary>
        public virtual int GetRevealGroup(in TextStudioTimingContext context) => context.UnitIndex;

        /// <summary>Calculates the delay after the current unit becomes visible.</summary>
        /// <param name="context">Transient semantic timing context that must not be retained.</param>
        /// <returns>Delay in seconds. Runtime playback clamps negative values to zero.</returns>
        public abstract float GetRevealDelayAfter(in TextStudioTimingContext context);
    }
}
