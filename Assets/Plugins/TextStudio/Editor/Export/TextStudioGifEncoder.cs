using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Collections;
using UnityEngine;

namespace TextStudio.Editor
{
    internal static class TextStudioGifTransparency
    {
        internal const int CoverageTileSize = 16;
        internal const byte PaletteIndex = 0;

        static readonly byte[] s_coverageRanks = BuildCoverageRanks();

        internal static bool IsTransparent(byte alpha, int x, int y)
        {
            if (alpha == 0)
                return true;
            if (alpha == byte.MaxValue)
                return false;

            int tileX = x & (CoverageTileSize - 1);
            int tileY = y & (CoverageTileSize - 1);
            int rank = s_coverageRanks[tileY * CoverageTileSize + tileX];
            return rank >= alpha;
        }

        internal static byte Unpremultiply(byte component, byte alpha)
        {
            if (alpha == 0)
                return 0;
            return (byte)Mathf.Clamp(Mathf.RoundToInt(component * 255f / alpha), 0, 255);
        }

        static byte[] BuildCoverageRanks()
        {
            int pixelCount = CoverageTileSize * CoverageTileSize;
            byte[] ranks = new byte[pixelCount];
            bool[] selected = new bool[pixelCount];
            for (int rank = 0; rank < pixelCount; rank++)
            {
                int bestIndex = -1;
                int bestDistance = -1;
                int bestTieBreaker = -1;
                for (int candidate = 0; candidate < pixelCount; candidate++)
                {
                    if (selected[candidate])
                        continue;

                    int minimumDistance = int.MaxValue;
                    int candidateX = candidate % CoverageTileSize;
                    int candidateY = candidate / CoverageTileSize;
                    for (int other = 0; other < pixelCount; other++)
                    {
                        if (!selected[other])
                            continue;

                        int deltaX = Mathf.Abs(candidateX - other % CoverageTileSize);
                        int deltaY = Mathf.Abs(candidateY - other / CoverageTileSize);
                        deltaX = Mathf.Min(deltaX, CoverageTileSize - deltaX);
                        deltaY = Mathf.Min(deltaY, CoverageTileSize - deltaY);
                        minimumDistance = Mathf.Min(minimumDistance, deltaX * deltaX + deltaY * deltaY);
                    }

                    int tieBreaker = (candidate * 73 + 19) & (pixelCount - 1);
                    if (minimumDistance < bestDistance ||
                        (minimumDistance == bestDistance && tieBreaker <= bestTieBreaker))
                        continue;
                    bestIndex = candidate;
                    bestDistance = minimumDistance;
                    bestTieBreaker = tieBreaker;
                }

                selected[bestIndex] = true;
                ranks[bestIndex] = (byte)rank;
            }

            return ranks;
        }
    }

    internal sealed class TextStudioGifPaletteBuilder
    {
        const int PaletteRefinementPasses = 4;
        const int HistogramSize = 32 * 32 * 32;
        readonly long[] _histogram = new long[HistogramSize];

        internal void AddFrame(Color32[] pixels)
        {
            if (pixels == null)
                return;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 color = pixels[i];
                if (color.a == 0)
                    continue;
                _histogram[GetBin(
                    TextStudioGifTransparency.Unpremultiply(color.r, color.a),
                    TextStudioGifTransparency.Unpremultiply(color.g, color.a),
                    TextStudioGifTransparency.Unpremultiply(color.b, color.a))] += color.a;
            }
        }

        internal void AddFrame(NativeArray<Color32> pixels)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 color = pixels[i];
                if (color.a == 0)
                    continue;
                _histogram[GetBin(
                    TextStudioGifTransparency.Unpremultiply(color.r, color.a),
                    TextStudioGifTransparency.Unpremultiply(color.g, color.a),
                    TextStudioGifTransparency.Unpremultiply(color.b, color.a))] += color.a;
            }
        }

        internal TextStudioGifPalette Build()
        {
            List<ColorSample> samples = new List<ColorSample>(HistogramSize);
            for (int bin = 0; bin < _histogram.Length; bin++)
            {
                long count = _histogram[bin];
                if (count <= 0)
                    continue;

                int red = (bin >> 10) & 31;
                int green = (bin >> 5) & 31;
                int blue = bin & 31;
                samples.Add(new ColorSample(
                    (byte)((red << 3) | 4),
                    (byte)((green << 3) | 4),
                    (byte)((blue << 3) | 4),
                    count));
            }

            if (samples.Count == 0)
                samples.Add(new ColorSample(0, 0, 0, 1));

            List<ColorBox> boxes = new List<ColorBox>(255)
            {
                new ColorBox(samples)
            };
            while (boxes.Count < 255)
            {
                int splitIndex = FindBoxToSplit(boxes);
                if (splitIndex < 0)
                    break;

                ColorBox source = boxes[splitIndex];
                if (!source.TrySplit(out ColorBox first, out ColorBox second))
                    break;
                boxes[splitIndex] = first;
                boxes.Add(second);
            }

            Color32[] colors = new Color32[256];
            colors[TextStudioGifTransparency.PaletteIndex] = new Color32(0, 0, 0, 0);
            int colorCount = Mathf.Max(1, boxes.Count);
            for (int i = 0; i < boxes.Count; i++)
                colors[i + 1] = boxes[i].GetAverageColor();
            RefinePalette(samples, colors, colorCount);
            for (int i = boxes.Count + 1; i < colors.Length; i++)
                colors[i] = colors[Mathf.Max(1, boxes.Count)];

            int[] lookup = new int[HistogramSize];
            for (int bin = 0; bin < lookup.Length; bin++)
            {
                int red = ((bin >> 10) & 31) * 8 + 4;
                int green = ((bin >> 5) & 31) * 8 + 4;
                int blue = (bin & 31) * 8 + 4;
                lookup[bin] = FindNearestColorIndex(red, green, blue, colors, colorCount);
            }

            return new TextStudioGifPalette(colors, lookup);
        }

        static void RefinePalette(
            List<ColorSample> samples,
            Color32[] colors,
            int colorCount)
        {
            long[] redSums = new long[colorCount + 1];
            long[] greenSums = new long[colorCount + 1];
            long[] blueSums = new long[colorCount + 1];
            long[] weights = new long[colorCount + 1];
            for (int pass = 0; pass < PaletteRefinementPasses; pass++)
            {
                Array.Clear(redSums, 0, redSums.Length);
                Array.Clear(greenSums, 0, greenSums.Length);
                Array.Clear(blueSums, 0, blueSums.Length);
                Array.Clear(weights, 0, weights.Length);
                for (int i = 0; i < samples.Count; i++)
                {
                    ColorSample sample = samples[i];
                    int paletteIndex = FindNearestColorIndex(
                        sample.Red,
                        sample.Green,
                        sample.Blue,
                        colors,
                        colorCount);
                    redSums[paletteIndex] += sample.Red * sample.Count;
                    greenSums[paletteIndex] += sample.Green * sample.Count;
                    blueSums[paletteIndex] += sample.Blue * sample.Count;
                    weights[paletteIndex] += sample.Count;
                }

                bool changed = false;
                for (int i = 1; i <= colorCount; i++)
                {
                    if (weights[i] <= 0)
                        continue;
                    Color32 refined = new Color32(
                        (byte)(redSums[i] / weights[i]),
                        (byte)(greenSums[i] / weights[i]),
                        (byte)(blueSums[i] / weights[i]),
                        byte.MaxValue);
                    if (!refined.Equals(colors[i]))
                        changed = true;
                    colors[i] = refined;
                }

                if (!changed)
                    break;
            }
        }

        static int FindNearestColorIndex(
            int red,
            int green,
            int blue,
            Color32[] colors,
            int colorCount)
        {
            int bestIndex = 1;
            long bestDistance = long.MaxValue;
            for (int i = 1; i <= colorCount; i++)
            {
                long distance = GetColorDistance(red, green, blue, colors[i]);
                if (distance >= bestDistance)
                    continue;
                bestDistance = distance;
                bestIndex = i;
            }

            return bestIndex;
        }

        static long GetColorDistance(int red, int green, int blue, Color32 candidate)
        {
            long deltaRed = red - candidate.r;
            long deltaGreen = green - candidate.g;
            long deltaBlue = blue - candidate.b;
            return 3L * deltaRed * deltaRed +
                   6L * deltaGreen * deltaGreen +
                   2L * deltaBlue * deltaBlue;
        }

        static int FindBoxToSplit(List<ColorBox> boxes)
        {
            int bestIndex = -1;
            long bestScore = -1;
            for (int i = 0; i < boxes.Count; i++)
            {
                ColorBox box = boxes[i];
                if (box.SampleCount < 2)
                    continue;
                long score = (long)box.MaximumRange * box.TotalWeight;
                if (score <= bestScore)
                    continue;
                bestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }

        internal static int GetBin(int red, int green, int blue)
        {
            return ((Mathf.Clamp(red, 0, 255) >> 3) << 10) |
                   ((Mathf.Clamp(green, 0, 255) >> 3) << 5) |
                   (Mathf.Clamp(blue, 0, 255) >> 3);
        }

        readonly struct ColorSample
        {
            internal readonly byte Red;
            internal readonly byte Green;
            internal readonly byte Blue;
            internal readonly long Count;

            internal ColorSample(byte red, byte green, byte blue, long count)
            {
                Red = red;
                Green = green;
                Blue = blue;
                Count = count;
            }
        }

        sealed class ColorBox
        {
            readonly List<ColorSample> _samples;
            int _redRange;
            int _greenRange;
            int _blueRange;

            internal ColorBox(List<ColorSample> samples)
            {
                _samples = samples;
                Recalculate();
            }

            internal int SampleCount => _samples.Count;

            internal long TotalWeight { get; private set; }

            internal int MaximumRange => Mathf.Max(_redRange, _greenRange, _blueRange);

            internal bool TrySplit(out ColorBox first, out ColorBox second)
            {
                first = null;
                second = null;
                if (_samples.Count < 2)
                    return false;

                int channel = _redRange >= _greenRange && _redRange >= _blueRange
                    ? 0
                    : (_greenRange >= _blueRange ? 1 : 2);
                _samples.Sort((left, right) => GetChannel(left, channel).CompareTo(GetChannel(right, channel)));
                long halfWeight = Math.Max(1L, TotalWeight / 2L);
                long runningWeight = 0;
                int split = 1;
                for (int i = 0; i < _samples.Count - 1; i++)
                {
                    runningWeight += _samples[i].Count;
                    if (runningWeight < halfWeight)
                        continue;
                    split = i + 1;
                    break;
                }

                List<ColorSample> firstSamples = _samples.GetRange(0, split);
                List<ColorSample> secondSamples = _samples.GetRange(split, _samples.Count - split);
                if (firstSamples.Count == 0 || secondSamples.Count == 0)
                    return false;
                first = new ColorBox(firstSamples);
                second = new ColorBox(secondSamples);
                return true;
            }

            internal Color32 GetAverageColor()
            {
                long red = 0;
                long green = 0;
                long blue = 0;
                long weight = 0;
                for (int i = 0; i < _samples.Count; i++)
                {
                    ColorSample sample = _samples[i];
                    red += sample.Red * (long)sample.Count;
                    green += sample.Green * (long)sample.Count;
                    blue += sample.Blue * (long)sample.Count;
                    weight += sample.Count;
                }

                if (weight <= 0)
                    return new Color32(0, 0, 0, 255);
                return new Color32(
                    (byte)(red / weight),
                    (byte)(green / weight),
                    (byte)(blue / weight),
                    255);
            }

            void Recalculate()
            {
                int minRed = 255;
                int maxRed = 0;
                int minGreen = 255;
                int maxGreen = 0;
                int minBlue = 255;
                int maxBlue = 0;
                TotalWeight = 0;
                for (int i = 0; i < _samples.Count; i++)
                {
                    ColorSample sample = _samples[i];
                    minRed = Mathf.Min(minRed, sample.Red);
                    maxRed = Mathf.Max(maxRed, sample.Red);
                    minGreen = Mathf.Min(minGreen, sample.Green);
                    maxGreen = Mathf.Max(maxGreen, sample.Green);
                    minBlue = Mathf.Min(minBlue, sample.Blue);
                    maxBlue = Mathf.Max(maxBlue, sample.Blue);
                    TotalWeight += sample.Count;
                }

                _redRange = maxRed - minRed;
                _greenRange = maxGreen - minGreen;
                _blueRange = maxBlue - minBlue;
            }

            static int GetChannel(ColorSample sample, int channel)
            {
                return channel switch
                {
                    0 => sample.Red,
                    1 => sample.Green,
                    _ => sample.Blue
                };
            }
        }
    }

    internal sealed class TextStudioGifPalette
    {
        const float ErrorDiffusionStrength = 0.75f;

        readonly int[] _lookup;
        float[] _currentRed;
        float[] _currentGreen;
        float[] _currentBlue;
        float[] _nextRed;
        float[] _nextGreen;
        float[] _nextBlue;

        internal TextStudioGifPalette(Color32[] colors, int[] lookup)
        {
            Colors = colors;
            _lookup = lookup;
        }

        internal Color32[] Colors { get; }

        internal byte[] Quantize(Color32[] bottomUpPixels, int width, int height)
        {
            int pixelCount = Mathf.Max(0, width * height);
            byte[] indices = new byte[pixelCount];
            Quantize(bottomUpPixels, width, height, indices);
            return indices;
        }

        internal void Quantize(
            NativeArray<Color32> bottomUpPixels,
            int width,
            int height,
            byte[] indices)
        {
            ValidateQuantizationBuffers(bottomUpPixels.Length, width, height, indices);
            EnsureErrorBuffers(width + 2);
            ClearErrors();
            for (int y = 0; y < height; y++)
            {
                Array.Clear(_nextRed, 0, _nextRed.Length);
                Array.Clear(_nextGreen, 0, _nextGreen.Length);
                Array.Clear(_nextBlue, 0, _nextBlue.Length);
                int sourceRow = (height - 1 - y) * width;
                int destinationRow = y * width;
                int direction = (y & 1) == 0 ? 1 : -1;
                int startX = direction > 0 ? 0 : width - 1;
                int endX = direction > 0 ? width : -1;
                for (int x = startX; x != endX; x += direction)
                {
                    Color32 source = bottomUpPixels[sourceRow + x];
                    QuantizePixel(source, x, y, destinationRow, direction, indices);
                }

                Swap(ref _currentRed, ref _nextRed);
                Swap(ref _currentGreen, ref _nextGreen);
                Swap(ref _currentBlue, ref _nextBlue);
            }
        }

        void Quantize(Color32[] bottomUpPixels, int width, int height, byte[] indices)
        {
            int sourceLength = bottomUpPixels?.Length ?? 0;
            ValidateQuantizationBuffers(sourceLength, width, height, indices);
            EnsureErrorBuffers(width + 2);
            ClearErrors();
            for (int y = 0; y < height; y++)
            {
                Array.Clear(_nextRed, 0, _nextRed.Length);
                Array.Clear(_nextGreen, 0, _nextGreen.Length);
                Array.Clear(_nextBlue, 0, _nextBlue.Length);
                int sourceRow = (height - 1 - y) * width;
                int destinationRow = y * width;
                int direction = (y & 1) == 0 ? 1 : -1;
                int startX = direction > 0 ? 0 : width - 1;
                int endX = direction > 0 ? width : -1;
                for (int x = startX; x != endX; x += direction)
                {
                    Color32 source = bottomUpPixels[sourceRow + x];
                    QuantizePixel(source, x, y, destinationRow, direction, indices);
                }

                Swap(ref _currentRed, ref _nextRed);
                Swap(ref _currentGreen, ref _nextGreen);
                Swap(ref _currentBlue, ref _nextBlue);
            }
        }

        void QuantizePixel(
            Color32 source,
            int x,
            int y,
            int destinationRow,
            int direction,
            byte[] indices)
        {
            int errorIndex = x + 1;
            if (TextStudioGifTransparency.IsTransparent(source.a, x, y))
            {
                indices[destinationRow + x] = TextStudioGifTransparency.PaletteIndex;
                _currentRed[errorIndex] = 0f;
                _currentGreen[errorIndex] = 0f;
                _currentBlue[errorIndex] = 0f;
                return;
            }

            int red = Mathf.Clamp(Mathf.RoundToInt(
                TextStudioGifTransparency.Unpremultiply(source.r, source.a) + _currentRed[errorIndex]), 0, 255);
            int green = Mathf.Clamp(Mathf.RoundToInt(
                TextStudioGifTransparency.Unpremultiply(source.g, source.a) + _currentGreen[errorIndex]), 0, 255);
            int blue = Mathf.Clamp(Mathf.RoundToInt(
                TextStudioGifTransparency.Unpremultiply(source.b, source.a) + _currentBlue[errorIndex]), 0, 255);
            int paletteIndex = _lookup[TextStudioGifPaletteBuilder.GetBin(red, green, blue)];
            indices[destinationRow + x] = (byte)paletteIndex;

            Color32 resolved = Colors[paletteIndex];
            float redError = red - resolved.r;
            float greenError = green - resolved.g;
            float blueError = blue - resolved.b;
            AddError(
                _currentRed,
                _currentGreen,
                _currentBlue,
                errorIndex + direction,
                redError,
                greenError,
                blueError,
                ErrorDiffusionStrength * 7f / 16f);
            AddError(
                _nextRed,
                _nextGreen,
                _nextBlue,
                errorIndex - direction,
                redError,
                greenError,
                blueError,
                ErrorDiffusionStrength * 3f / 16f);
            AddError(
                _nextRed,
                _nextGreen,
                _nextBlue,
                errorIndex,
                redError,
                greenError,
                blueError,
                ErrorDiffusionStrength * 5f / 16f);
            AddError(
                _nextRed,
                _nextGreen,
                _nextBlue,
                errorIndex + direction,
                redError,
                greenError,
                blueError,
                ErrorDiffusionStrength * 1f / 16f);
        }

        static void ValidateQuantizationBuffers(
            int sourceLength,
            int width,
            int height,
            byte[] indices)
        {
            int pixelCount = Mathf.Max(0, width * height);
            if (sourceLength < pixelCount)
                throw new ArgumentException("The source pixel buffer is smaller than the GIF frame.");
            if (indices == null || indices.Length < pixelCount)
                throw new ArgumentException("The GIF index buffer is smaller than the GIF frame.", nameof(indices));
        }

        void EnsureErrorBuffers(int length)
        {
            if (_currentRed != null && _currentRed.Length >= length)
                return;
            _currentRed = new float[length];
            _currentGreen = new float[length];
            _currentBlue = new float[length];
            _nextRed = new float[length];
            _nextGreen = new float[length];
            _nextBlue = new float[length];
        }

        void ClearErrors()
        {
            Array.Clear(_currentRed, 0, _currentRed.Length);
            Array.Clear(_currentGreen, 0, _currentGreen.Length);
            Array.Clear(_currentBlue, 0, _currentBlue.Length);
            Array.Clear(_nextRed, 0, _nextRed.Length);
            Array.Clear(_nextGreen, 0, _nextGreen.Length);
            Array.Clear(_nextBlue, 0, _nextBlue.Length);
        }

        static void AddError(
            float[] red,
            float[] green,
            float[] blue,
            int index,
            float redError,
            float greenError,
            float blueError,
            float weight)
        {
            if (index < 0 || index >= red.Length)
                return;
            red[index] += redError * weight;
            green[index] += greenError * weight;
            blue[index] += blueError * weight;
        }

        static void Swap(ref float[] first, ref float[] second)
        {
            float[] temporary = first;
            first = second;
            second = temporary;
        }
    }

    internal sealed class TextStudioGifEncoder : IDisposable
    {
        readonly BinaryWriter _writer;
        readonly int _width;
        readonly int _height;
        readonly int _framesPerSecond;
        int _delayRemainder;
        bool _closed;

        internal TextStudioGifEncoder(
            string path,
            int width,
            int height,
            int framesPerSecond,
            Color32[] palette)
        {
            _width = Mathf.Clamp(width, 1, ushort.MaxValue);
            _height = Mathf.Clamp(height, 1, ushort.MaxValue);
            _framesPerSecond = Mathf.Clamp(framesPerSecond, 1, 50);
            _delayRemainder = _framesPerSecond / 2;
            FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            _writer = new BinaryWriter(stream, Encoding.ASCII, false);
            WriteHeader(palette);
        }

        internal void AddFrame(byte[] indices)
        {
            if (_closed)
                throw new ObjectDisposedException(nameof(TextStudioGifEncoder));
            if (indices == null || indices.Length != _width * _height)
                throw new ArgumentException("The GIF frame size does not match the export dimensions.", nameof(indices));

            _writer.Write((byte)0x21);
            _writer.Write((byte)0xF9);
            _writer.Write((byte)0x04);
            _writer.Write((byte)0x09);
            // GIF stores centiseconds. Distribute rounding error so fractional frame delays do not accumulate drift.
            _delayRemainder += 100;
            ushort frameDelay = (ushort)(_delayRemainder / _framesPerSecond);
            _delayRemainder %= _framesPerSecond;
            _writer.Write(frameDelay);
            _writer.Write(TextStudioGifTransparency.PaletteIndex);
            _writer.Write((byte)0x00);

            _writer.Write((byte)0x2C);
            _writer.Write((ushort)0);
            _writer.Write((ushort)0);
            _writer.Write((ushort)_width);
            _writer.Write((ushort)_height);
            _writer.Write((byte)0x00);
            WriteLzw(indices);
        }

        public void Dispose()
        {
            if (_closed)
                return;
            _closed = true;
            _writer.Write((byte)0x3B);
            _writer.Flush();
            _writer.Dispose();
        }

        void WriteHeader(Color32[] palette)
        {
            WriteAscii("GIF89a");
            _writer.Write((ushort)_width);
            _writer.Write((ushort)_height);
            _writer.Write((byte)0xF7);
            _writer.Write((byte)0x00);
            _writer.Write((byte)0x00);
            for (int i = 0; i < 256; i++)
            {
                Color32 color = palette != null && i < palette.Length
                    ? palette[i]
                    : new Color32(0, 0, 0, 255);
                _writer.Write(color.r);
                _writer.Write(color.g);
                _writer.Write(color.b);
            }

            _writer.Write((byte)0x21);
            _writer.Write((byte)0xFF);
            _writer.Write((byte)0x0B);
            WriteAscii("NETSCAPE2.0");
            _writer.Write((byte)0x03);
            _writer.Write((byte)0x01);
            _writer.Write((ushort)0);
            _writer.Write((byte)0x00);
        }

        void WriteLzw(byte[] indices)
        {
            const int minimumCodeSize = 8;
            const int clearCode = 1 << minimumCodeSize;
            const int endCode = clearCode + 1;
            int nextCode = endCode + 1;
            int codeSize = minimumCodeSize + 1;
            Dictionary<int, int> dictionary = new Dictionary<int, int>(4096);
            GifSubBlockBitWriter bits = new GifSubBlockBitWriter(_writer);
            _writer.Write((byte)minimumCodeSize);
            bits.Write(clearCode, codeSize);
            int prefix = indices[0];
            for (int i = 1; i < indices.Length; i++)
            {
                int suffix = indices[i];
                int key = (prefix << 8) | suffix;
                if (dictionary.TryGetValue(key, out int combined))
                {
                    prefix = combined;
                    continue;
                }

                bits.Write(prefix, codeSize);
                if (nextCode < 4096)
                {
                    dictionary.Add(key, nextCode++);
                    if (nextCode > (1 << codeSize) && codeSize < 12)
                        codeSize++;
                }
                else
                {
                    bits.Write(clearCode, codeSize);
                    dictionary.Clear();
                    nextCode = endCode + 1;
                    codeSize = minimumCodeSize + 1;
                }

                prefix = suffix;
            }

            bits.Write(prefix, codeSize);
            bits.Write(endCode, codeSize);
            bits.Finish();
        }

        void WriteAscii(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            _writer.Write(bytes);
        }

        sealed class GifSubBlockBitWriter
        {
            readonly BinaryWriter _writer;
            readonly byte[] _block = new byte[255];
            int _blockLength;
            uint _bits;
            int _bitCount;

            internal GifSubBlockBitWriter(BinaryWriter writer)
            {
                _writer = writer;
            }

            internal void Write(int code, int codeSize)
            {
                _bits |= (uint)code << _bitCount;
                _bitCount += codeSize;
                while (_bitCount >= 8)
                {
                    WriteByte((byte)(_bits & 0xFF));
                    _bits >>= 8;
                    _bitCount -= 8;
                }
            }

            internal void Finish()
            {
                if (_bitCount > 0)
                    WriteByte((byte)(_bits & 0xFF));
                FlushBlock();
                _writer.Write((byte)0x00);
            }

            void WriteByte(byte value)
            {
                _block[_blockLength++] = value;
                if (_blockLength == _block.Length)
                    FlushBlock();
            }

            void FlushBlock()
            {
                if (_blockLength <= 0)
                    return;
                _writer.Write((byte)_blockLength);
                _writer.Write(_block, 0, _blockLength);
                _blockLength = 0;
            }
        }
    }
}
