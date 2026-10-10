using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TextStudio.Editor
{
    public static partial class TextStudioFontAppearancePreparation
    {
        static string FieldCachePath(Job job, string sourceHash, FieldInput input)
        {
            string key = FormattableString.Invariant($"{sourceHash}/{job.FaceIndex}/{TextStudioFontAppearanceData.GeneratorVersion}/{input.GlyphId}/{input.Density:R}/{input.Range:R}/{input.Width}/{input.Height}/{input.Minimum.x:R}/{input.Minimum.y:R}");
            // Exact-curve glyphs keep their key; resolved outlines never reuse pixels baked from the unresolved source.
            if (input.Preprocessing != null) key += "/" + input.Preprocessing;
            return Path.Combine(Path.GetDirectoryName(job.Folder), "Cache", Hash(Encoding.UTF8.GetBytes(key)) + ".field");
        }

        static bool TryReadField(string path, FieldInput input, out FieldOutput field)
        {
            field = default;
            try
            {
                int length = checked(input.Width * input.Height * 4);
                if (!File.Exists(path) || new FileInfo(path).Length != length + 32) return false;
                using BinaryReader reader = new BinaryReader(File.OpenRead(path));
                byte[] digest = reader.ReadBytes(32), pixels = reader.ReadBytes(length);
                using SHA256 sha = SHA256.Create();
                if (!sha.ComputeHash(pixels).SequenceEqual(digest)) return false;
                field = new FieldOutput(input, pixels);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }

        static void WriteFieldCache(string path, byte[] pixels)
        {
            // Library data is only an acceleration cache. Failure cannot invalidate a completed asset.
            string temporary = path + "." + Guid.NewGuid().ToString("N");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (BinaryWriter writer = new BinaryWriter(File.Create(temporary)))
                using (SHA256 sha = SHA256.Create()) { writer.Write(sha.ComputeHash(pixels)); writer.Write(pixels); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
