using System;
using UnityEngine;

namespace TextStudio
{
    /// <summary>How appearance data tables reach the 2D shaders on the active device.</summary>
    internal enum TextStudio2DDataTransport : byte
    {
        /// <summary>No supported route. Appearance keeps the source rendering and reports a compatibility diagnostic.</summary>
        Unsupported = 0,
        /// <summary>Structured buffers with coalesced partial uploads, used where every shader stage admits enough buffers.</summary>
        StructuredBuffers = 1,
        /// <summary>
        /// Point-sampled full-float data textures addressed by integer texel, used on WebGL 2, OpenGL ES 3.0 and devices
        /// whose vertex or fragment stage admits too few storage buffers.
        /// </summary>
        DataTextures = 2
    }

    /// <summary>Project preference for <see cref="TextStudio2DDataTransport"/>. Automatic follows device capability.</summary>
    internal enum TextStudio2DDataTransportPreference : byte
    {
        Automatic = 0,
        /// <summary>Use data textures wherever they are supported, for example to preview the WebGL route on desktop.</summary>
        DataTextures = 1
    }

    /// <summary>
    /// Single capability owner for the shared 2D data transport. Buffers are admitted per shader stage actually used,
    /// because some OpenGL ES 3.1 drivers report compute support while exposing no vertex-stage storage buffers.
    /// A WebGPU player that exposes storage buffers therefore takes the buffer route; Web is not assumed texture-only.
    /// </summary>
#if UNITY_6000_7_OR_NEWER
    // The transport preference persists; explicit reset also restores the global shader keyword.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextStudio2DTransport
    {
        /// <summary>Global shader keyword that selects the texture accessors in every 2D program.</summary>
        internal const string TextureKeyword = "TEXT_STUDIO_TEXTURE_TRANSPORT";
        /// <summary>Default keyword of the same multi_compile set, active whenever the texture keyword is disabled.</summary>
        internal const string BufferKeyword = "TEXT_STUDIO_BUFFER_TRANSPORT";
        /// <summary>Texels per data-texture row; shaders address texel (index &amp; 1023, index &gt;&gt; 10).</summary>
        internal const int TextureWidth = 1024;
        internal const int TextureWidthBits = 10;
        // The largest program binds style (4), mapping (3), surface (3) and Writing (2) tables in the fragment stage;
        // vertex stages read style, mapping and Writing records for placement and source recovery.
        const int FragmentBuffers = 12;
        const int VertexBuffers = 7;

        static TextStudio2DDataTransportPreference s_preference;
        static TextStudio2DDataTransport s_active;
        static bool s_resolved;
        static bool s_keywordEnabled;

        internal static TextStudio2DDataTransport Active
        {
            get
            {
                if (!s_resolved) Resolve();
                return s_active;
            }
        }

        internal static bool IsSupported => Active != TextStudio2DDataTransport.Unsupported;
        internal static bool UsesTextures => Active == TextStudio2DDataTransport.DataTextures;
        internal static string ActiveKeyword => UsesTextures ? TextureKeyword : BufferKeyword;
        internal static TextStudio2DDataTransportPreference Preference => s_preference;

        internal static bool BuffersSupported =>
            SystemInfo.graphicsShaderLevel >= 45 &&
            SystemInfo.supportsComputeShaders &&
            SystemInfo.maxComputeBufferInputsVertex >= VertexBuffers &&
            SystemInfo.maxComputeBufferInputsFragment >= FragmentBuffers;

        internal static bool TexturesSupported =>
            SystemInfo.graphicsShaderLevel >= 35 &&
            SystemInfo.supports2DArrayTextures &&
            SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat);

        /// <summary>Largest record count a data texture can address on this device.</summary>
        internal static int MaximumTexels => TextureWidth * Math.Max(1, SystemInfo.maxTextureSize);

        /// <summary>
        /// Selects the transport preference. Tables keep their current storage until recreated, so callers release the
        /// shared appearance resources afterwards and every table rebuilds its storage on the new transport.
        /// </summary>
        internal static void SetPreference(TextStudio2DDataTransportPreference preference)
        {
            if (s_preference == preference && s_resolved) return;
            s_preference = preference;
            Resolve();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#if UNITY_6000_7_OR_NEWER
        [UnityEngine.OnExitingPlayMode]
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void ResetRuntimeState()
        {
            s_resolved = false;
            s_keywordEnabled = false;
            Shader.DisableKeyword(TextureKeyword);
        }

        static void Resolve()
        {
            bool buffers = BuffersSupported;
            bool textures = TexturesSupported;
            s_active = s_preference == TextStudio2DDataTransportPreference.DataTextures && textures ? TextStudio2DDataTransport.DataTextures :
                buffers ? TextStudio2DDataTransport.StructuredBuffers :
                textures ? TextStudio2DDataTransport.DataTextures :
                TextStudio2DDataTransport.Unsupported;
            s_resolved = true;
            bool keyword = s_active == TextStudio2DDataTransport.DataTextures;
            if (keyword == s_keywordEnabled && Shader.IsKeywordEnabled(TextureKeyword) == keyword) return;
            s_keywordEnabled = keyword;
            if (keyword) Shader.EnableKeyword(TextureKeyword);
            else Shader.DisableKeyword(TextureKeyword);
        }
    }
}
