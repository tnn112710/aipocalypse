using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace TextStudio.Editor
{
    public static partial class TextStudioColorAppearancePreparation
    {
        internal sealed class CpuResult
        {
            internal float[] Pixels;
            internal int Width, Height;
            internal Rect Bounds;
            internal float Density, Range;
        }

        static void UpdateCpu(Job job)
        {
            if (job.CpuTask == null)
            {
                TextStudioColorSource source = job.Sources[job.Index];
                Texture2D texture = (Texture2D)source.Texture;
                long workingBytes = (long)texture.width * texture.height * 5 + 14L * 1024 * 1024;
                if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Preparation, "Static image preparation", 0, workingBytes, out job.CpuMemory))
                    throw new InvalidOperationException(TextStudioAppearanceMemory.LastRejection.ToString());
                byte[] alpha = TextStudioPreparationImagePixels.Read(texture, out int sourceWidth, out int sourceHeight);
                job.AcceptCpuImport();
                FilterMode filter = texture.filterMode;
                TextureWrapMode wrapU = texture.wrapModeU, wrapV = texture.wrapModeV;
                float density = Mathf.NextPowerOfTwo(Mathf.CeilToInt(job.Density));
                float padding = Mathf.Max(Mathf.Ceil(job.Padding * 32) / 32, 2 / density);
                Rect bounds = source.Bounds;
                bounds = Rect.MinMaxRect(bounds.xMin - padding, bounds.yMin - padding, bounds.xMax + padding, bounds.yMax + padding);
                float scale = Mathf.Min(density, 1024 / Mathf.Max(bounds.width, bounds.height));
                int width = Mathf.Clamp(Mathf.CeilToInt(bounds.width * scale), 4, 1024);
                int height = Mathf.Clamp(Mathf.CeilToInt(bounds.height * scale), 4, 1024);
                if (job.Bytes + (long)width * height * 4 > TextStudioColorFieldCache.MaximumTextureBytes)
                    throw new InvalidOperationException("Prepared image fields exceed 64 MiB. Reduce density, padding, or coverage.");
                float range = Mathf.Max(padding, Mathf.Max(source.Bounds.width, source.Bounds.height), .01f);
                CancellationToken cancellation = job.CpuCancellation.Token;
                job.CpuTask = Task.Run(() =>
                {
                    byte[] raster = RasterizeAlpha(source, alpha, sourceWidth, sourceHeight, bounds, width, height, filter, wrapU, wrapV, cancellation);
                    cancellation.ThrowIfCancellationRequested();
                    return new CpuResult { Width = width, Height = height, Bounds = bounds, Density = density, Range = range,
                        Pixels = TextStudioSilhouetteDistanceTransform.Generate(raster, width, height, bounds.width / width, bounds.height / height, source.AlphaThreshold, range) };
                }, cancellation);
                return;
            }
            if (!job.CpuTask.IsCompleted) return;
            CpuResult result = job.CpuTask.GetAwaiter().GetResult();
            if (!job.IsCurrent(job.Index)) throw new InvalidOperationException("An image changed during preparation. Its previous data was retained.");
            Texture2D fieldTexture = new Texture2D(result.Width, result.Height, TextureFormat.RFloat, false, true)
            { name = "Image Silhouette " + job.Index, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            try
            {
                fieldTexture.SetPixelData(result.Pixels, 0); fieldTexture.Apply(false, true);
                TextStudioPreparedColorField field = new TextStudioPreparedColorField(job.Sources[job.Index], fieldTexture, result.Bounds,
                    Mathf.Min(result.Width / result.Bounds.width, result.Height / result.Bounds.height), result.Density, job.Padding, result.Range);
                job.AddCpuField(field); fieldTexture = null;
                job.CpuTask = null;
                job.CpuMemory?.Dispose(); job.CpuMemory = null;
            }
            finally { UnityEngine.Object.DestroyImmediate(fieldTexture); }
        }

        internal static byte[] RasterizeAlpha(TextStudioColorSource source, byte[] pixels, int sourceWidth, int sourceHeight,
            Rect bounds, int width, int height, FilterMode filter, TextureWrapMode wrapU, TextureWrapMode wrapV, CancellationToken cancellation)
        {
            byte[] raster = new byte[width * height];
            for (int triangle = 0; triangle < source.Triangles.Length; triangle += 3)
            {
                int ia = source.Triangles[triangle], ib = source.Triangles[triangle + 1], ic = source.Triangles[triangle + 2];
                Vector2 a = source.Positions[ia], b = source.Positions[ib], c = source.Positions[ic];
                float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                if (Mathf.Abs(denominator) < 1e-12f) continue;
                int xMin = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.x, b.x, c.x) - bounds.xMin) * width / bounds.width), 0, width - 1);
                int xMax = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.x, b.x, c.x) - bounds.xMin) * width / bounds.width), 0, width - 1);
                int yMin = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(a.y, b.y, c.y) - bounds.yMin) * height / bounds.height), 0, height - 1);
                int yMax = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(a.y, b.y, c.y) - bounds.yMin) * height / bounds.height), 0, height - 1);
                for (int y = yMin; y <= yMax; y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    float py = bounds.yMin + (y + .5f) * bounds.height / height;
                    for (int x = xMin; x <= xMax; x++)
                    {
                        float px = bounds.xMin + (x + .5f) * bounds.width / width;
                        float wa = ((b.y - c.y) * (px - c.x) + (c.x - b.x) * (py - c.y)) / denominator;
                        float wb = ((c.y - a.y) * (px - c.x) + (a.x - c.x) * (py - c.y)) / denominator;
                        float wc = 1 - wa - wb;
                        if (!Covered(wa, b, c, denominator) || !Covered(wb, c, a, denominator) || !Covered(wc, a, b, denominator)) continue;
                        Vector2 uv = source.Uv[ia] * wa + source.Uv[ib] * wb + source.Uv[ic] * wc;
                        // Match the GPU capture shader's explicit texel-center sampling. Bilinear
                        // alpha reconstruction would pull adjacent atlas art into a silhouette.
                        int sx = Wrap(Mathf.FloorToInt(uv.x * sourceWidth), sourceWidth, wrapU);
                        int sy = Wrap(Mathf.FloorToInt(uv.y * sourceHeight), sourceHeight, wrapV);
                        float alpha = pixels[sy * sourceWidth + sx];
                        raster[y * width + x] = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha), 0, 255);
                    }
                }
            }
            return raster;
        }

        static bool Covered(float weight, Vector2 start, Vector2 end, float winding)
        {
            if (weight < -1e-6f) return false;
            if (weight > 1e-6f) return true;
            // Apply the GPU top-left rule at a triangle edge. Inclusive barycentric tests
            // add a row of pixels to tight meshes and shift the resulting distance field.
            Vector2 edge = winding < 0 ? end - start : start - end;
            return edge.y > 0 || edge.y == 0 && edge.x > 0;
        }

        static int Wrap(int value, int size, TextureWrapMode mode)
        {
            if (mode == TextureWrapMode.Clamp) return Mathf.Clamp(value, 0, size - 1);
            if (mode == TextureWrapMode.MirrorOnce) return Mathf.Clamp(value < 0 ? -value - 1 : value, 0, size - 1);
            int period = mode == TextureWrapMode.Mirror ? size * 2 : size;
            int wrapped = (value % period + period) % period;
            return wrapped < size ? wrapped : period - wrapped - 1;
        }
    }

    // Capture import-derived alpha before Unity discards CPU pixels. No importer setting or source image is changed.
    internal sealed class TextStudioPreparationImagePixels : AssetPostprocessor
    {
        static string CachePath(string asset) => Path.GetFullPath("Library/TextStudioFieldPreparation/ImagePixels/" + AssetDatabase.AssetPathToGUID(asset) + ".alpha");
        static string Identity(string asset)
        {
            string metadata = File.Exists(asset + ".meta") ? File.ReadAllText(asset + ".meta") : string.Empty;
            return TextStudioFontAppearancePreparation.Hash(File.ReadAllBytes(asset)) + Hash128.Compute(metadata) + EditorUserBuildSettings.activeBuildTarget;
        }
        void OnPostprocessTexture(Texture2D texture)
        {
            // Opt in only the texture requested by preparation, so ordinary imports have no extra memory cost.
            string marker = CachePath(assetPath) + ".requested";
            if (!File.Exists(marker) || (long)texture.width * texture.height > 8 * 1024 * 1024) return;
            Write(assetPath, texture);
        }
        static void Write(string path, Texture2D texture)
        {
            string cache = CachePath(path); Directory.CreateDirectory(Path.GetDirectoryName(cache));
            Color32[] pixels = texture.GetPixels32();
            using BinaryWriter writer = new BinaryWriter(File.Create(cache));
            writer.Write(Identity(path)); writer.Write(texture.width); writer.Write(texture.height);
            foreach (Color32 pixel in pixels) writer.Write(pixel.a);
        }
        internal static byte[] Read(Texture2D texture, out int width, out int height)
        {
            string path = AssetDatabase.GetAssetPath(texture), cache = CachePath(path), identity = Identity(path);
            width = texture.width; height = texture.height;
            if ((long)width * height > 8 * 1024 * 1024) throw new InvalidOperationException("Image source pixels exceed the 64 MiB preparation working budget.");
            if (texture.isReadable)
            {
                width = texture.width; height = texture.height;
                if ((long)width * height > 8 * 1024 * 1024) throw new InvalidOperationException("Image source pixels exceed the 64 MiB preparation working budget.");
                Color32[] pixels = texture.GetPixels32(); byte[] alpha = new byte[pixels.Length];
                for (int i = 0; i < alpha.Length; i++) alpha[i] = pixels[i].a;
                return alpha;
            }
            // Import callbacks precede lossy compression. Use the actual imported pixels for
            // compressed or generated assets, otherwise the silhouette could disagree with its visible image.
            if (GraphicsFormatUtility.IsCompressedFormat(texture.graphicsFormat) || AssetImporter.GetAtPath(path) is not TextureImporter)
            {
                if (!SystemInfo.supportsAsyncGPUReadback || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new InvalidOperationException("This unreadable image needs GPU pixel access. Prepare it on a graphics-enabled Editor first, or use a readable or uncompressed imported image for GPU-less preparation.");
                if (!TextStudioAppearanceMemory.TryReserve(TextStudioAppearanceResourceKind.Preparation,
                        "Imported image pixel readback", (long)width * height * 4, 0, out TextStudioAppearanceMemoryLease memory))
                    throw new InvalidOperationException(TextStudioAppearanceMemory.LastRejection.ToString());
                RenderTexture previous = RenderTexture.active;
                RenderTexture target = null;
                try
                {
                    // Readback cannot decompress block-compressed sources. Sampling into an
                    // uncompressed target preserves the imported alpha that the renderer uses.
                    target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                    if (!target.Create()) throw new InvalidOperationException("The image pixel readback target could not be created.");
                    Graphics.Blit(texture, target);
                    AsyncGPUReadbackRequest request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32);
                    request.WaitForCompletion();
                    if (request.hasError) throw new InvalidOperationException("The imported image pixels could not be read for preparation.");
                    Unity.Collections.NativeArray<Color32> pixels = request.GetData<Color32>();
                    byte[] alpha = new byte[checked(width * height)];
                    for (int i = 0; i < alpha.Length; i++) alpha[i] = pixels[i].a;
                    return alpha;
                }
                finally
                {
                    RenderTexture.active = previous;
                    if (target != null) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
                    memory.Dispose();
                }
            }
            bool Current()
            {
                if (!File.Exists(cache)) return false;
                try { using BinaryReader reader = new BinaryReader(File.OpenRead(cache)); return reader.ReadString() == identity; }
                catch (IOException) { return false; }
            }
            if (!Current())
            {
                Directory.CreateDirectory(Path.GetDirectoryName(cache));
                File.WriteAllText(cache + ".requested", string.Empty);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            if (!Current()) throw new InvalidOperationException("Image alpha could not be captured from its importer. Sources must fit within the 64 MiB preparation working budget.");
            using (BinaryReader reader = new BinaryReader(File.OpenRead(cache)))
            {
                reader.ReadString(); width = reader.ReadInt32(); height = reader.ReadInt32();
                long count = (long)width * height;
                if (width <= 0 || height <= 0 || count > 8 * 1024 * 1024) throw new InvalidDataException("Invalid cached image dimensions.");
                byte[] alpha = reader.ReadBytes((int)count);
                if (alpha.Length != count || reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Incomplete cached image alpha.");
                return alpha;
            }
        }
    }
}
