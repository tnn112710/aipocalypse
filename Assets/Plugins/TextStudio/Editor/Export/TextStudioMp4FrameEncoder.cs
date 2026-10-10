using System;
using Unity.Collections;
#if !UNITY_EDITOR_LINUX
using UnityEditor.Media;
#endif
using UnityEngine;

namespace TextStudio.Editor
{
    internal sealed class TextStudioMp4FrameEncoder : IDisposable
    {
#if !UNITY_EDITOR_LINUX
        MediaEncoder _encoder;
#endif

        internal TextStudioMp4FrameEncoder(
            string destination,
            int width,
            int height,
            int framesPerSecond)
        {
#if UNITY_EDITOR_LINUX
            throw new PlatformNotSupportedException(
                TextStudioPreviewExportJob.GetFormatUnavailableReason(
                    TextStudioPreviewExportFormat.Mp4));
#else
            VideoTrackAttributes attributes = new VideoTrackAttributes
            {
                frameRate = new MediaRational(framesPerSecond),
                width = (uint)width,
                height = (uint)height,
                includeAlpha = false,
                bitRateMode = UnityEditor.VideoBitrateMode.High
            };
            _encoder = new MediaEncoder(destination, attributes);
#endif
        }

        internal void AddFrame(Texture2D texture, int width, int height)
        {
            if (texture == null)
                throw new ArgumentNullException(nameof(texture));

#if UNITY_EDITOR_LINUX
            throw new PlatformNotSupportedException(
                TextStudioPreviewExportJob.GetFormatUnavailableReason(
                    TextStudioPreviewExportFormat.Mp4));
#else
            if (_encoder == null)
                throw new ObjectDisposedException(nameof(TextStudioMp4FrameEncoder));

            NativeArray<byte> pixels = texture.GetRawTextureData<byte>();
            MediaTime time = new MediaTime
            {
                count = 0,
                rate = MediaRational.Invalid
            };
            bool accepted = _encoder.AddFrame(
                width,
                height,
                0,
                TextureFormat.RGBA32,
                pixels,
                time);
            if (!accepted)
                throw new InvalidOperationException("Unity's media encoder rejected an MP4 frame.");
#endif
        }

        public void Dispose()
        {
#if !UNITY_EDITOR_LINUX
            _encoder?.Dispose();
            _encoder = null;
#endif
        }
    }
}
