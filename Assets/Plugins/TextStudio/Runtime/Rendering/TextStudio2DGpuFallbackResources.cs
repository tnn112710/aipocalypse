using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>
    /// Keeps every table declared by a packaged 2D shader safely bound while the corresponding feature table is
    /// disabled: one-record structured buffers on the buffer transport (Metal requires declared buffers to be bound even
    /// when a runtime branch avoids reading them) or one zeroed data-texture row on the texture transport. Compiled
    /// programs still read a disabled table (fxc flattens the guarding branches), so the style and mapping fallbacks
    /// publish zero last records and every accessor then reads record 0.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DGpuFallbackResources
    {
        const int BufferCount = 6;
        // The data-texture fallback holds record 0 of every table, up to the widest one: an operation.
        static readonly int s_dataTextureTexels = System.Runtime.InteropServices.Marshal.SizeOf<TextStudio2DGpuOperation>() / 16;

        static readonly int s_styleHeadersProperty =
            Shader.PropertyToID("_TextStudio2DStyleHeaders");
        static readonly int s_styleOperationsProperty =
            Shader.PropertyToID("_TextStudio2DStyleOperations");
        static readonly int s_styleGradientsProperty =
            Shader.PropertyToID("_TextStudio2DGradientStops");
        static readonly int s_textUniformsProperty =
            Shader.PropertyToID("_TextStudio2DTextUniforms");
        static readonly int s_mappingHeadersProperty =
            Shader.PropertyToID("_TextStudio2DMappingHeaders");
        static readonly int s_mappingGlyphsProperty =
            Shader.PropertyToID("_TextStudio2DGlyphMappings");
        static readonly int s_mappingRangesProperty =
            Shader.PropertyToID("_TextStudio2DRangeMappings");
        static readonly int s_writingSamplesProperty =
            Shader.PropertyToID("_TextStudioWritingSamples");
        static readonly int s_writingTransformsProperty =
            Shader.PropertyToID("_TextStudioWritingTransforms");
        static readonly int s_styleEnabledProperty =
            Shader.PropertyToID("_TextStudio2DStyleTableEnabled");
        static readonly int s_visualModifiersEnabledProperty =
            Shader.PropertyToID("_TextStudio2DVisualModifiersEnabled");
        static readonly int s_mappingEnabledProperty =
            Shader.PropertyToID("_TextStudio2DMappingTableEnabled");
        static readonly int s_writingEnabledProperty =
            Shader.PropertyToID("_TextStudioWritingEnabled");
        static readonly int s_writingSourceEnabledProperty =
            Shader.PropertyToID("_TextStudioWritingSourceEnabled");
        static readonly int s_styleLastRecordsProperty =
            Shader.PropertyToID("_TextStudio2DStyleTableLastRecords");
        static readonly int s_mappingLastRecordsProperty =
            Shader.PropertyToID("_TextStudio2DMappingTableLastRecords");

        static readonly TextStudio2DGpuStyleHeader[] s_vectorRecords =
            new TextStudio2DGpuStyleHeader[1];
        static readonly TextStudio2DGpuOperation[] s_operationRecords =
            new TextStudio2DGpuOperation[1];
        static readonly TextStudioGpuWritingSample[] s_writingSampleRecords =
            new TextStudioGpuWritingSample[1];
        static readonly TextStudioGpuWritingTransform[] s_writingTransformRecords =
            new TextStudioGpuWritingTransform[1];
        static readonly Vector4[] s_vector4Records = new Vector4[1];
        static readonly int[] s_dataTextureProperties =
        {
            Shader.PropertyToID("_TextStudio2DStyleHeadersData"),
            Shader.PropertyToID("_TextStudio2DStyleOperationsData"),
            Shader.PropertyToID("_TextStudio2DGradientStopsData"),
            Shader.PropertyToID("_TextStudio2DTextUniformsData"),
            Shader.PropertyToID("_TextStudio2DMappingHeadersData"),
            Shader.PropertyToID("_TextStudio2DGlyphMappingsData"),
            Shader.PropertyToID("_TextStudio2DRangeMappingsData"),
            Shader.PropertyToID("_TextStudio2DSurfaceHeadersData"),
            Shader.PropertyToID("_TextStudio2DSurfaceEdgesData"),
            Shader.PropertyToID("_TextStudio2DSurfaceInkSourcesData"),
            Shader.PropertyToID("_TextStudioWritingSamplesData"),
            Shader.PropertyToID("_TextStudioWritingTransformsData")
        };
        static Texture2D s_dataTexture;
        static readonly TextStudio2DGpuGlyphMapping[] s_glyphMappingRecords = new TextStudio2DGpuGlyphMapping[1];

        static GraphicsBuffer s_vectorBuffer;
        static GraphicsBuffer s_operationBuffer;
        static GraphicsBuffer s_writingSampleBuffer;
        static GraphicsBuffer s_writingTransformBuffer;
        static GraphicsBuffer s_vector4Buffer;
        static GraphicsBuffer s_glyphMappingBuffer;
        static Texture2DArray s_preparedTexture;
        static bool s_warningLogged;
        static bool s_shuttingDown;

        internal static bool IsSupported => TextStudio2DTransport.IsSupported;

        internal static int BufferCountForTests =>
            HasEveryBuffer ? BufferCount : 0;

        internal static int AllocatedBytesForTests =>
            HasEveryBuffer
                ? s_vectorBuffer.stride +
                  s_operationBuffer.stride +
                  s_writingSampleBuffer.stride +
                  s_writingTransformBuffer.stride +
                  s_vector4Buffer.stride + s_glyphMappingBuffer.stride
                : 0;

        static bool HasEveryBuffer =>
            TextStudio2DTransport.UsesTextures ? s_dataTexture != null :
            s_vectorBuffer != null &&
            s_operationBuffer != null &&
            s_writingSampleBuffer != null &&
            s_writingTransformBuffer != null &&
            s_vector4Buffer != null && s_glyphMappingBuffer != null;

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnEnteringPlayMode]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetRuntimeState()
        {
            s_shuttingDown = false;
            Release();
        }

#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        static void ReleasePlayResources() => Release();

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void Shutdown()
        {
            // Renderer OnDisable callbacks can restore bindings after the editor's reload cleanup.
            s_shuttingDown = true;
            Release();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeBeforeSceneLoad()
        {
            EnsureInitialized();
        }

        /// <summary>True while Play Mode ends or code reloads: the data tables are gone and are not created again.</summary>
        internal static bool IsShuttingDown => s_shuttingDown || TextStudioAppearanceResources.IsShuttingDown;

        internal static bool EnsureInitialized()
        {
            if (IsShuttingDown) return false;
            if (HasEveryBuffer)
                return true;
            if (!IsSupported)
                return false;

            ReleaseBuffers();
            try
            {
                if (TextStudio2DTransport.UsesTextures)
                {
                    s_dataTexture = new Texture2D(s_dataTextureTexels, 1, TextureFormat.RGBAFloat, false, true)
                    {
                        name = "Text Studio Empty Data Table",
                        hideFlags = HideFlags.HideAndDontSave,
                        filterMode = FilterMode.Point
                    };
                    s_dataTexture.SetPixelData(new Vector4[s_dataTextureTexels], 0);
                    s_dataTexture.Apply(false, true);
                    BindAllFallbacks();
                    DisableAllConsumers();
                    return true;
                }
                s_vectorBuffer = CreateBuffer(s_vectorRecords);
                s_operationBuffer = CreateBuffer(s_operationRecords);
                s_writingSampleBuffer = CreateBuffer(s_writingSampleRecords);
                s_writingTransformBuffer = CreateBuffer(s_writingTransformRecords);
                s_vector4Buffer = CreateBuffer(s_vector4Records);
                s_glyphMappingBuffer = CreateBuffer(s_glyphMappingRecords);
                BindAllFallbacks();
                DisableAllConsumers();
                return true;
            }
            catch (Exception exception)
            {
                ReleaseBuffers();
                if (!s_warningLogged)
                {
                    s_warningLogged = true;
                    Debug.LogWarning(
                        "[TextStudio] Safe 2D shader fallback buffers could not be created. " +
                        "Data-table-based enhancements will preserve source text. " +
                        exception.Message);
                }
                return false;
            }
        }

        internal static bool RequiresDataTables(Shader shader)
        {
            if (shader == null)
                return false;

            string shaderName = shader.name;
            return shaderName == "Text Studio/2D Production SDF" ||
                   shaderName == "Text Studio/2D Production SDF Overlay" ||
                   shaderName == "Text Studio/2D Field SDF" ||
                   shaderName == "Text Studio/2D Field SDF Overlay" ||
                   shaderName == "Text Studio/2D Hero Layer" ||
                   shaderName == "Text Studio/2D Style Decoration" ||
                   shaderName == "Text Studio/Writing";
        }

        internal static void RestoreStyleBindings()
        {
            if (!EnsureInitialized())
                return;
            Shader.SetGlobalVector(s_styleLastRecordsProperty, Vector4.zero);
            if (BindDataTextures(0, 4)) return;
            Shader.SetGlobalBuffer(s_styleHeadersProperty, s_vectorBuffer);
            Shader.SetGlobalBuffer(s_styleOperationsProperty, s_operationBuffer);
            Shader.SetGlobalBuffer(s_styleGradientsProperty, s_vectorBuffer);
            Shader.SetGlobalBuffer(s_textUniformsProperty, s_vectorBuffer);
        }

        internal static void RestoreMappingBindings()
        {
            if (!EnsureInitialized())
                return;
            RestoreMappingBindings(s_mappingHeadersProperty, s_mappingGlyphsProperty, s_mappingRangesProperty,
                s_mappingLastRecordsProperty);
        }

        internal static void RestoreMappingBindings(int headersProperty, int glyphsProperty, int rangesProperty, int lastRecordsProperty)
        {
            if (!EnsureInitialized()) return;
            Shader.SetGlobalVector(lastRecordsProperty, Vector4.zero);
            if (BindDataTextures(4, 3)) return;
            Shader.SetGlobalBuffer(headersProperty, s_vectorBuffer);
            Shader.SetGlobalBuffer(glyphsProperty, s_glyphMappingBuffer);
            Shader.SetGlobalBuffer(rangesProperty, s_vector4Buffer);
        }

        internal static void RestoreWritingBindings()
        {
            if (!EnsureInitialized())
                return;
            if (BindDataTextures(10, 2)) return;
            Shader.SetGlobalBuffer(s_writingSamplesProperty, s_writingSampleBuffer);
            Shader.SetGlobalBuffer(s_writingTransformsProperty, s_writingTransformBuffer);
        }

        internal static void RestorePreparedFieldTexture()
        {
            if (s_shuttingDown || !SystemInfo.supports2DArrayTextures) return;
            if (s_preparedTexture == null)
            {
                s_preparedTexture = new Texture2DArray(1, 1, 1, TextureFormat.RGBA32, false, true)
                { name = "Text Studio Empty Prepared Field", hideFlags = HideFlags.HideAndDontSave };
                s_preparedTexture.SetPixels(new[] { Color.clear }, 0);
                s_preparedTexture.Apply(false, true);
            }
            Shader.SetGlobalTexture("_TextStudioPreparedFields", s_preparedTexture);
        }

        internal static void RestoreSurfaceInkBindings()
        {
            if (!EnsureInitialized()) return;
            if (BindDataTextures(9, 1)) return;
            Shader.SetGlobalBuffer("_TextStudio2DSurfaceInkSources", s_vector4Buffer);
        }

        internal static void RestoreSurfaceBindings()
        {
            if (!EnsureInitialized()) return;
            if (BindDataTextures(7, 3))
            {
                Shader.SetGlobalFloat("_TextStudio2DSurfacesEnabled", 0);
                return;
            }
            Shader.SetGlobalBuffer("_TextStudio2DSurfaceHeaders", s_vector4Buffer);
            Shader.SetGlobalBuffer("_TextStudio2DSurfaceEdges", s_vector4Buffer);
            Shader.SetGlobalBuffer("_TextStudio2DSurfaceInkSources", s_vector4Buffer);
            Shader.SetGlobalFloat("_TextStudio2DSurfacesEnabled", 0);
        }

        internal static void RestoreAllBindings()
        {
            if (!EnsureInitialized())
                return;
            BindAllFallbacks();
            DisableAllConsumers();
        }

        internal static void Release()
        {
            ReleaseBuffers();
            if (s_preparedTexture != null)
            {
                if (Application.isPlaying && !s_shuttingDown) UnityEngine.Object.Destroy(s_preparedTexture);
                else UnityEngine.Object.DestroyImmediate(s_preparedTexture);
                s_preparedTexture = null;
            }
            s_warningLogged = false;
        }

        static GraphicsBuffer CreateBuffer<T>(T[] records) where T : struct
        {
            GraphicsBuffer buffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                1,
                System.Runtime.InteropServices.Marshal.SizeOf<T>());
            try
            {
                buffer.SetData(records);
                return buffer;
            }
            catch
            {
                buffer.Dispose();
                throw;
            }
        }

        /// <summary>Binds the empty data texture to <paramref name="count"/> table properties on the texture transport.</summary>
        static bool BindDataTextures(int first, int count)
        {
            if (!TextStudio2DTransport.UsesTextures || s_dataTexture == null) return false;
            for (int index = first; index < first + count; index++)
                Shader.SetGlobalTexture(s_dataTextureProperties[index], s_dataTexture);
            return true;
        }

        static void BindAllFallbacks()
        {
            Shader.SetGlobalVector(s_styleLastRecordsProperty, Vector4.zero);
            Shader.SetGlobalVector(s_mappingLastRecordsProperty, Vector4.zero);
            if (BindDataTextures(0, s_dataTextureProperties.Length))
            {
                RestorePreparedFieldTexture();
                return;
            }
            Shader.SetGlobalBuffer(s_styleHeadersProperty, s_vectorBuffer);
            Shader.SetGlobalBuffer(s_styleOperationsProperty, s_operationBuffer);
            Shader.SetGlobalBuffer(s_styleGradientsProperty, s_vectorBuffer);
            Shader.SetGlobalBuffer(s_textUniformsProperty, s_vectorBuffer);
            Shader.SetGlobalBuffer(s_mappingHeadersProperty, s_vectorBuffer);
            Shader.SetGlobalBuffer(s_mappingGlyphsProperty, s_glyphMappingBuffer);
            Shader.SetGlobalBuffer(s_mappingRangesProperty, s_vector4Buffer);
            Shader.SetGlobalBuffer(s_writingSamplesProperty, s_writingSampleBuffer);
            Shader.SetGlobalBuffer(s_writingTransformsProperty, s_writingTransformBuffer);
            Shader.SetGlobalBuffer("_TextStudio2DSurfaceHeaders", s_vector4Buffer);
            Shader.SetGlobalBuffer("_TextStudio2DSurfaceEdges", s_vector4Buffer);
            Shader.SetGlobalBuffer("_TextStudio2DSurfaceInkSources", s_vector4Buffer);
            RestorePreparedFieldTexture();
        }

        static void DisableAllConsumers()
        {
            Shader.SetGlobalFloat(s_styleEnabledProperty, 0f);
            Shader.SetGlobalFloat(s_visualModifiersEnabledProperty, 0f);
            Shader.SetGlobalFloat(s_mappingEnabledProperty, 0f);
            Shader.SetGlobalFloat(s_writingEnabledProperty, 0f);
            Shader.SetGlobalFloat(s_writingSourceEnabledProperty, 0f);
            Shader.SetGlobalFloat("_TextStudio2DSurfacesEnabled", 0);
        }

        static void ReleaseBuffers()
        {
            if (s_dataTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(s_dataTexture);
                s_dataTexture = null;
            }
            s_vectorBuffer?.Dispose();
            s_operationBuffer?.Dispose();
            s_writingSampleBuffer?.Dispose();
            s_writingTransformBuffer?.Dispose();
            s_vector4Buffer?.Dispose();
            s_glyphMappingBuffer?.Dispose();
            s_vectorBuffer = null;
            s_operationBuffer = null;
            s_writingSampleBuffer = null;
            s_writingTransformBuffer = null;
            s_vector4Buffer = null;
            s_glyphMappingBuffer = null;
        }
    }
}
