using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace TextStudio
{
    /// <summary>Encodes one record into exact float4 texels for the data-texture transport.</summary>
    internal delegate void TextStudioTexelEncoder<T>(in T record, NativeArray<Vector4> texels, int texel) where T : struct;

    /// <summary>
    /// One logical GPU payload table with a CPU mirror. It reserves the CPU mirror and its next GPU generation before
    /// growing, and publishes through the active <see cref="TextStudio2DTransport"/>: a structured buffer with partial
    /// uploads, or a point-sampled full-float data texture addressed by integer texel. A data texture keeps its own
    /// readable copy, which records encode into directly and which its generation accounts as CPU memory.
    /// </summary>
    internal sealed class TextStudioAppearanceBuffer<T> : IDisposable where T : struct
    {
        readonly TextStudioAppearanceResourceKind _kind;
        readonly string _resource;
        readonly int _stride = Marshal.SizeOf<T>();
        readonly int _texelsPerRecord;
        readonly TextStudioTexelEncoder<T> _encoder;
        TextStudioAppearanceMemoryLease _cpuMemory;
        TextStudioAppearanceMemoryLease _gpuMemory;
        TextStudioAppearanceMemoryLease _pendingGpuMemory;
        bool _textureDirty;
        bool _idle;

        internal T[] Data { get; private set; } = Array.Empty<T>();
        internal GraphicsBuffer Buffer { get; private set; }
        internal Texture2D Texture { get; private set; }
        internal bool HasGpuStorage => Buffer != null || Texture != null;
        internal long CpuBytes => (_cpuMemory?.Cpu ?? 0) + (_gpuMemory?.Cpu ?? 0);
        internal long GpuBytes => (_gpuMemory?.Gpu ?? 0) + (_pendingGpuMemory?.Gpu ?? 0);

        /// <param name="encoder">Required only for records that are not a sequence of float4 values.</param>
        internal TextStudioAppearanceBuffer(TextStudioAppearanceResourceKind kind, string resource,
            int texelsPerRecord = 0, TextStudioTexelEncoder<T> encoder = null)
        {
            _kind = kind;
            _resource = resource;
            _encoder = encoder;
            _texelsPerRecord = texelsPerRecord > 0 ? texelsPerRecord : _stride / 16;
            if (_texelsPerRecord <= 0 || encoder == null && _stride % 16 != 0)
                throw new ArgumentException("Texture transport needs float4 records or an explicit texel encoder.", nameof(encoder));
        }

        internal bool TryEnsureCapacity(int count)
        {
            if (count <= 0) return false;
            count = Math.Max(count, Data.Length);
            long gpuBytes = GpuBytesFor(count);
            bool grow = Data.Length < count;
            bool reserveGpu = !MatchesCapacity(count) && _pendingGpuMemory?.Gpu != gpuBytes;
            if (!grow && !reserveGpu) return true;
            if (TextStudio2DTransport.UsesTextures && (long)count * _texelsPerRecord > TextStudio2DTransport.MaximumTexels) return false;

            TextStudioAppearanceMemoryLease cpu = null;
            TextStudioAppearanceMemoryLease gpu = null;
            try
            {
                if (grow && !TextStudioAppearanceMemory.TryReserve(_kind, _resource, 0, (long)count * _stride, out cpu)) return false;
                if (reserveGpu && !TryReserveGeneration(count, out gpu)) return false;
                if (grow)
                {
                    T[] replacement = new T[count];
                    Array.Copy(Data, replacement, Data.Length);
                    Data = replacement;
                    _cpuMemory?.Dispose();
                    _cpuMemory = cpu;
                    cpu = null;
                    _cpuMemory.Commit();
                    _cpuMemory.SetIdle(_idle);
                }
                if (reserveGpu)
                {
                    _pendingGpuMemory?.Dispose();
                    _pendingGpuMemory = gpu;
                    gpu = null;
                }
                return true;
            }
            finally
            {
                cpu?.Dispose();
                gpu?.Dispose();
            }
        }

        /// <summary>Creates GPU storage for the whole mirror on the active transport. <paramref name="uploaded"/> reports a full upload.</summary>
        internal bool TryEnsureGpuBuffer(out bool uploaded)
        {
            uploaded = false;
            if (Data.Length == 0 || !TextStudio2DTransport.IsSupported) return false;
            if (MatchesCapacity(Data.Length) && (Buffer == null || Buffer.IsValid())) return true;
            if (!TryEnsureCapacity(Data.Length)) return false;
            if (_pendingGpuMemory == null && !TryReserveGeneration(Data.Length, out _pendingGpuMemory)) return false;

            if (TextStudio2DTransport.UsesTextures) CreateTexture();
            else CreateBuffer();
            _gpuMemory?.Dispose();
            _gpuMemory = _pendingGpuMemory;
            _pendingGpuMemory = null;
            _gpuMemory.Commit();
            _gpuMemory.SetIdle(_idle);
            TextStudioAppearanceMemory.RecordUpload(_kind, GpuBytesFor(Data.Length));
            uploaded = true;
            return true;
        }

        /// <summary>
        /// Publishes records <paramref name="offset"/> through <paramref name="offset"/> + <paramref name="count"/> - 1.
        /// Buffers upload the range now; data textures encode it and upload once when bound.
        /// </summary>
        internal void Upload(int offset, int count)
        {
            if (count <= 0) return;
            if (Texture != null)
            {
                Encode(offset, count);
                _textureDirty = true;
                return;
            }
            Buffer.SetData(Data, offset, offset, count);
            TextStudioAppearanceMemory.RecordUpload(_kind, (long)count * _stride);
        }

        /// <summary>Binds the published storage to the transport's global property and applies pending texel edits once.</summary>
        internal void Bind(int bufferProperty, int textureProperty)
        {
            if (Texture != null)
            {
                if (_textureDirty)
                {
                    Texture.Apply(false, false);
                    TextStudioAppearanceMemory.RecordUpload(_kind, GpuBytesFor(Data.Length));
                    _textureDirty = false;
                }
                Shader.SetGlobalTexture(textureProperty, Texture);
            }
            else if (Buffer != null) Shader.SetGlobalBuffer(bufferProperty, Buffer);
        }

        internal void SetIdle(bool idle)
        {
            if (_idle != idle)
            {
                _idle = idle;
                _cpuMemory?.SetIdle(idle);
                _gpuMemory?.SetIdle(idle);
            }
            if (idle)
            {
                _pendingGpuMemory?.Dispose();
                _pendingGpuMemory = null;
            }
        }

        internal void ReleaseGpu()
        {
            ReleaseGpuObjects();
            _gpuMemory?.Dispose();
            _gpuMemory = null;
            _pendingGpuMemory?.Dispose();
            _pendingGpuMemory = null;
        }

        public void Dispose()
        {
            ReleaseGpu();
            Data = Array.Empty<T>();
            _cpuMemory?.Dispose();
            _cpuMemory = null;
        }

        // A data texture generation also owns the readable copy Unity keeps for later partial edits.
        bool TryReserveGeneration(int count, out TextStudioAppearanceMemoryLease lease)
        {
            long gpuBytes = GpuBytesFor(count);
            return TextStudioAppearanceMemory.TryReserve(_kind, _resource, gpuBytes,
                TextStudio2DTransport.UsesTextures ? gpuBytes : 0, out lease);
        }

        bool MatchesCapacity(int count) => TextStudio2DTransport.UsesTextures
            ? Texture != null && Buffer == null && TextureRows(count) == Texture.height
            : Buffer != null && Buffer.count == count && Texture == null;

        long GpuBytesFor(int count) => TextStudio2DTransport.UsesTextures
            ? (long)TextStudio2DTransport.TextureWidth * TextureRows(count) * 16
            : (long)count * _stride;

        int TextureRows(int count) => Math.Max(1, (int)(((long)count * _texelsPerRecord + TextStudio2DTransport.TextureWidth - 1) / TextStudio2DTransport.TextureWidth));

        void CreateBuffer()
        {
            GraphicsBuffer replacement = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Data.Length, _stride);
            try { replacement.SetData(Data); }
            catch { replacement.Dispose(); throw; }
            ReleaseGpuObjects();
            Buffer = replacement;
        }

        void CreateTexture()
        {
            Texture2D replacement = new Texture2D(TextStudio2DTransport.TextureWidth, TextureRows(Data.Length), TextureFormat.RGBAFloat, false, true)
            {
                name = _resource,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            try
            {
                ReleaseGpuObjects();
                Texture = replacement;
                Encode(0, Data.Length);
                replacement.Apply(false, false);
            }
            catch
            {
                Texture = null;
                UnityEngine.Object.DestroyImmediate(replacement);
                throw;
            }
            _textureDirty = false;
        }

        void ReleaseGpuObjects()
        {
            Buffer?.Dispose();
            Buffer = null;
            _textureDirty = false;
            if (Texture == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(Texture);
            else UnityEngine.Object.DestroyImmediate(Texture);
            Texture = null;
        }

        void Encode(int offset, int count)
        {
            NativeArray<Vector4> texels = Texture.GetPixelData<Vector4>(0);
            if (_encoder != null)
            {
                for (int index = offset; index < offset + count; index++)
                    _encoder(in Data[index], texels, index * _texelsPerRecord);
                return;
            }
            ReadOnlySpan<Vector4> source = MemoryMarshal.Cast<T, Vector4>(new ReadOnlySpan<T>(Data, offset, count));
            int first = offset * _texelsPerRecord;
            for (int index = 0; index < source.Length; index++)
                texels[first + index] = source[index];
        }
    }
}
