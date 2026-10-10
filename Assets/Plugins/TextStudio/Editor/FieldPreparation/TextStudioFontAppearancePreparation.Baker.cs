using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using TextStudio.Geometry;
using UnityEngine;

namespace TextStudio.Editor
{
    /// <summary>The native TSFIELD1 exchange: field placement, baker outlines, process execution, and verified output.</summary>
    public static partial class TextStudioFontAppearancePreparation
    {
        /// <summary>Places one glyph field on the tier's pixel grid and selects the outline the baker receives.</summary>
        internal static FieldInput CreateFieldInput(int tier, uint id, CurveGlyph glyph, float density, float range, TextStudioFieldContourResolver contours)
        {
            // Canonical curves are already normalized to em. Point size and source units must not be applied again.
            Vector2 minimum = new Vector2(Mathf.Floor((glyph.Bounds.min.x - range * .5f) * density), Mathf.Floor((glyph.Bounds.min.y - range * .5f) * density)) / density;
            int width = Mathf.CeilToInt((glyph.Bounds.max.x + range * .5f - minimum.x) * density);
            int height = Mathf.CeilToInt((glyph.Bounds.max.y + range * .5f - minimum.y) * density);
            if (width <= 0 || height <= 0 || width > PageSize - 2 || height > PageSize - 2)
                throw new InvalidDataException("A glyph exceeds the prepared page dimensions. Reduce density or distance reach, or choose a supported static font instance.");
            CurveGlyph outline = contours.Resolve(id, glyph, density, out string preprocessing);
            return new FieldInput(tier, id, outline, density, range, minimum, width, height, preprocessing);
        }

        /// <summary>Writes one TSFIELD1 job to the folder, runs the pinned baker, and returns verified fields in batch order.</summary>
        internal static List<FieldOutput> BakeFields(string baker, string folder, List<FieldInput> batch, CancellationToken cancellation, Action progressed = null)
        {
            string inputPath = Path.Combine(folder, "source.txt"), outputPath = Path.Combine(folder, "fields.bin");
            using (StreamWriter writer = new StreamWriter(inputPath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine("TSFIELD1"); writer.WriteLine(batch.Count);
                foreach (FieldInput input in batch)
                {
                    cancellation.ThrowIfCancellationRequested();
                    writer.WriteLine(FormattableString.Invariant($"{input.GlyphId} {input.Density:R} {input.Range:R} {input.Minimum.x:R} {input.Minimum.y:R} {input.Width} {input.Height} {input.Glyph.ContourCount}"));
                    for (int contourIndex = 0; contourIndex < input.Glyph.ContourCount; contourIndex++)
                    {
                        CurveContour contour = input.Glyph.GetContour(contourIndex); writer.WriteLine(contour.Count);
                        for (int edge = 0; edge < contour.Count; edge++)
                        {
                            CurveSegment segment = contour[edge];
                            writer.Write((int)segment.Kind);
                            WritePoint(writer, segment.Start); WritePoint(writer, segment.Control1); WritePoint(writer, segment.Control2); WritePoint(writer, segment.End);
                            writer.WriteLine();
                        }
                    }
                }
            }
            ProcessStartInfo start = new ProcessStartInfo(baker, "\"" + inputPath + "\" \"" + outputPath + "\"")
            { CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, UseShellExecute = false, RedirectStandardError = true };
            using (Process process = Process.Start(start))
            using (cancellation.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } }))
            {
                string lastLine = string.Empty;
                while (!process.StandardError.EndOfStream)
                {
                    string line = process.StandardError.ReadLine();
                    if (line != null && line.Length > 0) lastLine = line;
                    progressed?.Invoke();
                }
                process.WaitForExit();
                cancellation.ThrowIfCancellationRequested();
                if (process.ExitCode != 0) throw new InvalidOperationException("Font preparation failed: " + lastLine);
            }
            List<FieldOutput> fields = new List<FieldOutput>(batch.Count);
            using (BinaryReader reader = new BinaryReader(File.OpenRead(outputPath)))
            {
                if (reader.ReadUInt32() != 0x46445354 || reader.ReadUInt32() != 1 || reader.ReadUInt32() != batch.Count)
                    throw new InvalidDataException("The baker returned an incompatible field file.");
                foreach (FieldInput input in batch)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (reader.ReadUInt32() != input.GlyphId || reader.ReadUInt32() != input.Width || reader.ReadUInt32() != input.Height ||
                        reader.ReadSingle() != input.Minimum.x || reader.ReadSingle() != input.Minimum.y ||
                        reader.ReadSingle() != input.Density || reader.ReadSingle() != input.Range)
                        throw new InvalidDataException("The baker returned inconsistent glyph metrics.");
                    byte[] data = reader.ReadBytes(input.Width * input.Height * 4);
                    if (data.Length != input.Width * input.Height * 4) throw new InvalidDataException("The baker returned truncated field pixels.");
                    fields.Add(new FieldOutput(input, data));
                }
                if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("The baker returned an unexpected field payload.");
            }
            return fields;
        }

        static void WritePoint(TextWriter writer, Vector2 point)
        {
            writer.Write(' '); writer.Write(point.x.ToString("R", CultureInfo.InvariantCulture));
            writer.Write(' '); writer.Write(point.y.ToString("R", CultureInfo.InvariantCulture));
        }

        internal readonly struct FieldInput
        {
            internal readonly int Tier, Width, Height;
            internal readonly uint GlyphId;
            internal readonly CurveGlyph Glyph;
            internal readonly float Density, Range;
            internal readonly Vector2 Minimum;
            /// <summary>Null for exact source curves; otherwise the outline preprocessing that keys cached pixels.</summary>
            internal readonly string Preprocessing;
            internal FieldInput(int tier, uint id, CurveGlyph glyph, float density, float range, Vector2 minimum, int width, int height, string preprocessing)
            { Tier = tier; GlyphId = id; Glyph = glyph; Density = density; Range = range; Minimum = minimum; Width = width; Height = height; Preprocessing = preprocessing; }
        }
        internal readonly struct FieldOutput
        {
            internal readonly FieldInput Input;
            internal readonly byte[] Pixels;
            internal FieldOutput(FieldInput input, byte[] pixels) { Input = input; Pixels = pixels; }
        }
    }
}
