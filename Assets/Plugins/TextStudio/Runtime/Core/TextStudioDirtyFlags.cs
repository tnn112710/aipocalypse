using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Domain-specific dirty flag tracker with addon extension bits.
    /// Supports per-glyph and per-region dirty tracking.
    /// </summary>
    internal struct DirtyTracker
    {
        DirtyFlags _globalFlags;
        uint _version;

        public DirtyFlags GlobalFlags => _globalFlags;
        public uint Version => _version;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDirty(DirtyFlags flags)
        {
            _globalFlags |= flags;
            _version++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDirty(DirtyFlags flags)
        {
            return (_globalFlags & flags) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(DirtyFlags flags)
        {
            _globalFlags &= ~flags;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearAll()
        {
            _globalFlags = DirtyFlags.None;
        }

        /// <summary>
        /// Consume a set of flags: returns true if any were set, then clears them.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Consume(DirtyFlags flags)
        {
            if ((_globalFlags & flags) == 0) return false;
            _globalFlags &= ~flags;
            return true;
        }
    }

    /// <summary>
    /// Per-glyph dirty bit array. Backed by a uint[] for 32-glyph-per-element packing.
    /// </summary>
    internal sealed class GlyphDirtyBits
    {
        uint[] _bits;
        int _capacity;
        int _activeWordCount;
        int _allDirtyGlyphCount;

        public GlyphDirtyBits(int glyphCapacity = 256)
        {
            _capacity = glyphCapacity;
            _bits = new uint[(glyphCapacity + 31) >> 5];
        }

        public void EnsureCapacity(int glyphCount)
        {
            int needed = (glyphCount + 31) >> 5;
            if (needed > _bits.Length)
            {
                _capacity = glyphCount;
                Array.Resize(ref _bits, needed);
            }
            else if (glyphCount > _capacity)
            {
                _capacity = glyphCount;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetDirty(int glyphIndex)
        {
            if (glyphIndex >= _capacity)
                EnsureCapacity(glyphIndex + 1);
            int word = glyphIndex >> 5;
            _bits[word] |= 1u << (glyphIndex & 31);
            if (word + 1 > _activeWordCount)
                _activeWordCount = word + 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDirty(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex >= _capacity) return false;
            return (_bits[glyphIndex >> 5] & (1u << (glyphIndex & 31))) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear(int glyphIndex)
        {
            if (glyphIndex < 0 || glyphIndex >= _capacity) return;
            _bits[glyphIndex >> 5] &= ~(1u << (glyphIndex & 31));
            if (glyphIndex < _allDirtyGlyphCount)
                _allDirtyGlyphCount = 0;
        }

        public void ClearAll()
        {
            if (_activeWordCount > 0)
                Array.Clear(_bits, 0, _activeWordCount);
            _activeWordCount = 0;
            _allDirtyGlyphCount = 0;
        }

        public void SetAllDirty(int glyphCount)
        {
            EnsureCapacity(glyphCount);

            int fullWords = glyphCount >> 5;
            for (int i = 0; i < fullWords; i++)
                _bits[i] = 0xFFFFFFFF;

            int remainder = glyphCount & 31;
            if (remainder > 0 && fullWords < _bits.Length)
                _bits[fullWords] = (1u << remainder) - 1;

            int wordCount = (glyphCount + 31) >> 5;
            if (wordCount < _activeWordCount)
                Array.Clear(_bits, wordCount, _activeWordCount - wordCount);
            _activeWordCount = wordCount;
            _allDirtyGlyphCount = glyphCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool AreAllDirty(int glyphCount)
        {
            return glyphCount > 0 && _allDirtyGlyphCount >= glyphCount;
        }

        /// <summary>
        /// Marks each glyph below glyphCount that is dirty in any source, one 32-glyph word at a time, and returns
        /// whether any was. Sources never hold bits past their capacity, so absent words read as clean.
        /// </summary>
        public bool MarkDirtyFromAny(
            int glyphCount,
            GlyphDirtyBits first,
            GlyphDirtyBits second,
            GlyphDirtyBits third)
        {
            if (glyphCount <= 0)
                return false;

            EnsureCapacity(glyphCount);
            int wordCount = (glyphCount + 31) >> 5;
            int remainder = glyphCount & 31;
            bool marked = false;
            for (int word = 0; word < wordCount; word++)
            {
                uint bits = first.WordAt(word) | second.WordAt(word) | third.WordAt(word);
                if (remainder != 0 && word == wordCount - 1)
                    bits &= (1u << remainder) - 1u;
                if (bits == 0u)
                    continue;

                _bits[word] |= bits;
                if (word + 1 > _activeWordCount)
                    _activeWordCount = word + 1;
                marked = true;
            }

            return marked;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        uint WordAt(int word)
        {
            return word < _bits.Length ? _bits[word] : 0u;
        }

        /// <summary>
        /// Returns true if any glyph in the range [start, start+count) is dirty.
        /// </summary>
        public bool AnyDirtyInRange(int start, int count)
        {
            if (count <= 0 || _bits == null || _capacity <= 0) return false;

            int begin = Mathf.Max(0, start);
            int end = Mathf.Min(start + count, _capacity);
            if (end <= begin) return false;

            int startWord = begin >> 5;
            int endWord = (end - 1) >> 5;
            int firstBit = begin & 31;
            int lastBit = (end - 1) & 31;

            for (int word = startWord; word <= endWord; word++)
            {
                uint mask = uint.MaxValue;
                if (word == startWord)
                    mask &= uint.MaxValue << firstBit;
                if (word == endWord)
                    mask &= uint.MaxValue >> (31 - lastBit);

                if ((_bits[word] & mask) != 0)
                    return true;
            }

            return false;
        }
    }
}
