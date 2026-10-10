namespace TextStudio
{
    /// <summary>
    /// Exact per-vertex style identity carried in UV0.z, separate from continuous glyph-local coordinates.
    /// Expanded primitives can place local coordinates outside the source quad without corrupting it. A primitive
    /// selects up to eight operations of one fused span; programs beyond eight operations draw one primitive per span.
    /// </summary>
    internal static class TextStudio2DStyleIdentity
    {
        internal const int SlotBits = 10;
        internal const int SlotCapacity = 1 << SlotBits;
        internal const int OperationMaskBits = TextStudio2DStyleTable.MaximumFusedOperations;
        internal const int SpanBits = 6;
        /// <summary>Spans addressable by one identity: 64 spans of eight cover the 512-operation program limit.</summary>
        internal const int SpanCapacity = 1 << SpanBits;
        const int SpanShift = SlotBits + OperationMaskBits;

        /// <summary>
        /// Returns zero for pass-through output. Bit n of <paramref name="operationMask"/> selects operation
        /// <paramref name="span"/> * 8 + n. Every packed value stays below 2^24, so float transport is exact.
        /// </summary>
        internal static float Pack(int styleSlot, int operationMask, int span = 0)
        {
            if (styleSlot <= 0 || styleSlot >= SlotCapacity || span < 0 || span >= SpanCapacity)
                return 0f;
            int mask = operationMask & ((1 << OperationMaskBits) - 1);
            return styleSlot | mask << SlotBits | span << SpanShift;
        }

        /// <summary>Identity selecting exactly one operation.</summary>
        internal static float PackOperation(int styleSlot, int operationIndex) =>
            operationIndex < 0 ? Pack(styleSlot, 0) : Pack(styleSlot, 1 << (operationIndex & (OperationMaskBits - 1)), operationIndex / OperationMaskBits);

        internal static int Slot(float identity) => (int)System.Math.Round(identity) & (SlotCapacity - 1);

        internal static int OperationMask(float identity) => ((int)System.Math.Round(identity) >> SlotBits) & ((1 << OperationMaskBits) - 1);

        internal static int Span(float identity) => (int)System.Math.Round(identity) >> SpanShift;
    }
}
