using System;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace TextStudio.Editor
{
    internal enum TextStudioPreviewExportFormat
    {
        Mp4,
        Gif
    }

    internal readonly struct TextStudioPreviewExportPreset
    {
        internal readonly int Width;
        internal readonly int Height;
        internal readonly int FramesPerSecond;

        internal TextStudioPreviewExportPreset(int width, int height, int framesPerSecond)
        {
            Width = width;
            Height = height;
            FramesPerSecond = framesPerSecond;
        }

        internal static TextStudioPreviewExportPreset For(TextStudioPreviewExportFormat format)
        {
            return format == TextStudioPreviewExportFormat.Gif
                ? new TextStudioPreviewExportPreset(960, 540, 50)
                : new TextStudioPreviewExportPreset(960, 540, 60);
        }

        internal int GetFrameCount(float displayDuration)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.0001f, displayDuration) * FramesPerSecond));
        }

        internal static float GetSourceTime(int frameIndex, int frameCount, float sourceDuration)
        {
            int safeFrameCount = Mathf.Max(1, frameCount);
            int safeFrameIndex = Mathf.Clamp(frameIndex, 0, safeFrameCount - 1);
            return safeFrameIndex / (float)safeFrameCount * Mathf.Max(0f, sourceDuration);
        }
    }

    [InitializeOnLoad]
#if UNITY_6000_7_OR_NEWER
    // Static-constructor state is code-load scoped; cleanup is handled explicitly where the type crosses Play Mode.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudioPreviewExportJob
    {
        static ExportOperation s_operation;

        static TextStudioPreviewExportJob()
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            EditorApplication.update += Update;
            EditorApplication.quitting += CancelForShutdown;
            AssemblyReloadEvents.beforeAssemblyReload += CancelForShutdown;
        }

        static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode ||
                state == PlayModeStateChange.ExitingPlayMode)
                CancelForShutdown();
        }

        internal static bool IsExporting => s_operation != null;

        internal static TextStudioPreviewExportFormat DefaultFormat
        {
            get
            {
#if UNITY_EDITOR_LINUX
                return TextStudioPreviewExportFormat.Gif;
#else
                return TextStudioPreviewExportFormat.Mp4;
#endif
            }
        }

        internal static bool IsFormatAvailable(TextStudioPreviewExportFormat format)
        {
            if (format == TextStudioPreviewExportFormat.Gif)
                return true;
#if UNITY_EDITOR_LINUX
            return false;
#else
            return true;
#endif
        }

        internal static string GetFormatUnavailableReason(TextStudioPreviewExportFormat format)
        {
            if (IsFormatAvailable(format))
                return string.Empty;
            return "MP4 export is unavailable in the Linux Editor because Unity's H.264 media encoder is supported only on Windows and macOS. Export GIF instead.";
        }

        internal static bool TryStart(
            TextStudioPreviewExportSnapshot snapshot,
            TextStudioPreviewExportFormat format,
            string destination,
            out string error)
        {
            return TryStart(
                snapshot,
                format,
                TextStudioPreviewExportRendererRegistry.FlatRendererId,
                destination,
                out error);
        }

        internal static bool TryStart(
            TextStudioPreviewExportSnapshot snapshot,
            TextStudioPreviewExportFormat format,
            string rendererId,
            string destination,
            out string error)
        {
            error = string.Empty;
            if (snapshot == null)
            {
                error = "The export preview is no longer available.";
                return false;
            }
            if (!IsFormatAvailable(format))
            {
                error = GetFormatUnavailableReason(format);
                return false;
            }
            if (s_operation != null || TextStudioPreviewExportCoordinator.IsExternalExporting)
            {
                error = "Another Text Studio preview export is already running.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(destination))
            {
                error = "Choose a destination file before exporting.";
                return false;
            }

            try
            {
                s_operation = new ExportOperation(snapshot, format, rendererId, destination);
                return true;
            }
            catch (Exception exception)
            {
                error = "Text Studio could not start the export: " + exception.Message;
                return false;
            }
        }

        static void Update()
        {
            ExportOperation operation = s_operation;
            if (operation == null)
                return;

            try
            {
                if (operation.Update())
                    s_operation = null;
            }
            catch (Exception exception)
            {
                s_operation = null;
                operation.Fail(exception);
            }
        }

        static void CancelForShutdown()
        {
            ExportOperation operation = s_operation;
            s_operation = null;
            operation?.Cancel(false);
        }

        sealed class ExportOperation
        {
            readonly TextStudioPreviewExportSnapshot _snapshot;
            readonly TextStudioPreviewExportFormat _format;
            readonly TextStudioPreviewExportPreset _preset;
            readonly string _destination;
            readonly string _temporaryPath;
            readonly int _frameCount;
            readonly ITextStudioPreviewExportFrameRenderer _renderer;
            readonly TextStudioGifPaletteBuilder _paletteBuilder;
            TextStudioMp4FrameEncoder _mediaEncoder;
            TextStudioGifPalette _gifPalette;
            TextStudioGifEncoder _gifEncoder;
            byte[] _gifIndices;
            int _frameIndex;
            bool _buildingGifPalette;
            bool _finished;
            bool _resourcesCleaned;

            internal ExportOperation(
                TextStudioPreviewExportSnapshot snapshot,
                TextStudioPreviewExportFormat format,
                string rendererId,
                string destination)
            {
                _snapshot = snapshot;
                _format = format;
                _preset = TextStudioPreviewExportPreset.For(format);
                try
                {
                    _destination = Path.GetFullPath(destination);
                    string directory = Path.GetDirectoryName(_destination);
                    if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                        throw new DirectoryNotFoundException("The selected export folder does not exist.");
                    string extension = format == TextStudioPreviewExportFormat.Gif ? ".gif" : ".mp4";
                    _temporaryPath = Path.Combine(
                        directory,
                        "." + Path.GetFileNameWithoutExtension(_destination) + ".textstudio-exporting" + extension);
                    DeleteTemporaryFile();
                    _frameCount = _preset.GetFrameCount(snapshot.DisplayDuration);
                    _renderer = TextStudioPreviewExportRendererRegistry.Create(
                        rendererId,
                        snapshot,
                        _preset.Width,
                        _preset.Height,
                        format == TextStudioPreviewExportFormat.Gif);

                    if (format == TextStudioPreviewExportFormat.Gif)
                    {
                        _paletteBuilder = new TextStudioGifPaletteBuilder();
                        _buildingGifPalette = true;
                    }
                    else
                    {
                        _mediaEncoder = new TextStudioMp4FrameEncoder(
                            _temporaryPath,
                            _preset.Width,
                            _preset.Height,
                            _preset.FramesPerSecond);
                    }
                }
                catch
                {
                    CleanupResources(false);
                    DeleteTemporaryFile();
                    throw;
                }
            }

            internal bool Update()
            {
                if (_finished)
                    return true;

                float progress = GetProgress();
                string phase = GetProgressLabel();
                if (EditorUtility.DisplayCancelableProgressBar(
                        "Exporting Text Studio Loop",
                        phase,
                        progress))
                {
                    Cancel(true);
                    return true;
                }

                float sourceTime = TextStudioPreviewExportPreset.GetSourceTime(
                    _frameIndex,
                    _frameCount,
                    _snapshot.SourceDuration);
                Texture2D texture = _renderer.Render(sourceTime);
                if (_format == TextStudioPreviewExportFormat.Gif)
                    ProcessGifFrame(texture);
                else
                    ProcessMp4Frame(texture);

                _frameIndex++;
                if (_frameIndex < _frameCount)
                    return false;

                if (_format == TextStudioPreviewExportFormat.Gif && _buildingGifPalette)
                {
                    _gifPalette = _paletteBuilder.Build();
                    _renderer.ResetEvaluation();
                    _gifEncoder = new TextStudioGifEncoder(
                        _temporaryPath,
                        _preset.Width,
                        _preset.Height,
                        _preset.FramesPerSecond,
                        _gifPalette.Colors);
                    _buildingGifPalette = false;
                    _frameIndex = 0;
                    return false;
                }

                Complete();
                return true;
            }

            internal void Cancel(bool notify)
            {
                if (_finished)
                    return;
                _finished = true;
                CleanupResources();
                DeleteTemporaryFile();
                EditorUtility.ClearProgressBar();
                if (notify)
                    Debug.Log("[Text Studio] Preview export cancelled.");
            }

            internal void Fail(Exception exception)
            {
                if (!_finished)
                {
                    _finished = true;
                    CleanupResources();
                    DeleteTemporaryFile();
                }
                EditorUtility.ClearProgressBar();
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Text Studio Export Failed",
                    "The preview loop could not be exported.\n\n" + exception.Message,
                    "OK");
            }

            void ProcessGifFrame(Texture2D texture)
            {
                NativeArray<Color32> pixels = texture.GetRawTextureData<Color32>();
                if (_buildingGifPalette)
                {
                    _paletteBuilder.AddFrame(pixels);
                    return;
                }

                _gifIndices ??= new byte[_preset.Width * _preset.Height];
                _gifPalette.Quantize(
                    pixels,
                    _preset.Width,
                    _preset.Height,
                    _gifIndices);
                _gifEncoder.AddFrame(_gifIndices);
            }

            void ProcessMp4Frame(Texture2D texture)
            {
                _mediaEncoder.AddFrame(texture, _preset.Width, _preset.Height);
            }

            void Complete()
            {
                CleanupResources();
                if (File.Exists(_destination))
                    File.Replace(_temporaryPath, _destination, null);
                else
                    File.Move(_temporaryPath, _destination);
                _finished = true;
                EditorUtility.ClearProgressBar();
                Debug.Log("[Text Studio] Exported preview loop to " + _destination);
                EditorUtility.RevealInFinder(_destination);
            }

            void CleanupResources(bool disposeSnapshot = true)
            {
                if (_resourcesCleaned)
                    return;
                _resourcesCleaned = true;
                _gifEncoder?.Dispose();
                _gifEncoder = null;
                _mediaEncoder?.Dispose();
                _mediaEncoder = null;
                _renderer?.Dispose();
                if (disposeSnapshot)
                    _snapshot?.Dispose();
            }

            float GetProgress()
            {
                if (_format != TextStudioPreviewExportFormat.Gif)
                    return _frameIndex / (float)_frameCount;
                float passProgress = _frameIndex / (float)_frameCount * 0.5f;
                return _buildingGifPalette ? passProgress : 0.5f + passProgress;
            }

            string GetProgressLabel()
            {
                if (_format == TextStudioPreviewExportFormat.Gif)
                {
                    return _buildingGifPalette
                        ? "Analyzing GIF colors - frame " + (_frameIndex + 1) + " of " + _frameCount
                        : "Encoding GIF - frame " + (_frameIndex + 1) + " of " + _frameCount;
                }

                return "Encoding MP4 - frame " + (_frameIndex + 1) + " of " + _frameCount;
            }

            void DeleteTemporaryFile()
            {
                if (File.Exists(_temporaryPath))
                    File.Delete(_temporaryPath);
            }
        }
    }
}
