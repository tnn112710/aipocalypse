Shader "Text Studio/2D Hero Layer"
{
    Properties
    {
        [HideInInspector] _TextStudioNativeMaterialModel ("Text Studio Native Material Model", Float) = 1
        _FaceColor ("Face Color", Color) = (1,1,1,1)
        _FaceTex ("Face Texture", 2D) = "white" {}
        _FaceUVSpeedX ("Face UV Speed X", Range(-5,5)) = 0
        _FaceUVSpeedY ("Face UV Speed Y", Range(-5,5)) = 0
        _FaceDilate ("Face Dilate", Range(-1,1)) = 0
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineTex ("Outline Texture", 2D) = "white" {}
        _OutlineUVSpeedX ("Outline UV Speed X", Range(-5,5)) = 0
        _OutlineUVSpeedY ("Outline UV Speed Y", Range(-5,5)) = 0
        _OutlineWidth ("Outline Thickness", Range(0,1)) = 0
        _OutlineSoftness ("Outline Softness", Range(0,1)) = 0
        _UnderlayColor ("Border Color", Color) = (0,0,0,.5)
        _UnderlayOffsetX ("Border Offset X", Range(-1,1)) = 0
        _UnderlayOffsetY ("Border Offset Y", Range(-1,1)) = 0
        _UnderlayDilate ("Border Dilate", Range(-1,1)) = 0
        _UnderlaySoftness ("Border Softness", Range(0,1)) = 0
        _WeightNormal ("Weight Normal", Float) = 0
        _WeightBold ("Weight Bold", Float) = .5
        _ShaderFlags ("Flags", Float) = 0
        _ScaleRatioA ("Scale Ratio A", Float) = 1
        _ScaleRatioB ("Scale Ratio B", Float) = 1
        _ScaleRatioC ("Scale Ratio C", Float) = 1
        _MainTex ("Font Atlas", 2D) = "white" {}
        [HideInInspector] _TextStudioColorSourceMode ("Color Source Mode", Float) = 0
        _TextureWidth ("Texture Width", Float) = 512
        _TextureHeight ("Texture Height", Float) = 512
        _GradientScale ("Gradient Scale", Float) = 5
        [HideInInspector] _TextStudioFieldFormat ("Text Studio Field Format", Float) = 0
        [HideInInspector] _TextStudioFieldPixelsPerEm ("Text Studio Field Pixels Per Em", Float) = 0
        [HideInInspector] _TextStudioFieldSafeDistance ("Text Studio Field Safe Distance", Float) = 0
        _ScaleX ("Scale X", Float) = 1
        _ScaleY ("Scale Y", Float) = 1
        _PerspectiveFilter ("Perspective Correction", Range(0,1)) = .875
        _Sharpness ("Sharpness", Range(-1,1)) = 0
        _VertexOffsetX ("Vertex Offset X", Float) = 0
        _VertexOffsetY ("Vertex Offset Y", Float) = 0
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _MaskSoftnessX ("Mask Softness X", Float) = 0
        _MaskSoftnessY ("Mask Softness Y", Float) = 0
        _TextStudioGlowColor ("Text Studio Glow", Color) = (.2,.65,1,1)
        _TextStudioSweepColor ("Text Studio Sweep", Color) = (1,1,1,1)
        _TextStudioSweepIntensity ("Text Studio Sweep Intensity", Range(0,3)) = 1
        _TextStudioChromaticIntensity ("Text Studio Chromatic Intensity", Range(0,3)) = 1
        [HideInInspector] _TextStudioPayloadEnabled ("Text Studio Payload Enabled", Float) = 1
        [HideInInspector] _TextStudioWritingSourceEnabled ("Text Studio Writing Source Enabled", Float) = 0
        [HideInInspector] _TextStudioPayloadFromTangent ("Text Studio Payload From Tangent", Float) = 0
        [HideInInspector] _TextStudioSourceBounds ("Text Studio Source Bounds", Vector) = (0,0,0,0)
        // Record and gradient stops of a uniform row (TextStudio2DHeroUniformRow). Vectors, not colors: packed colors are already linear.
        [HideInInspector] _TextStudioHeroRowLayer ("Text Studio Hero Row Layer", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowExecution ("Text Studio Hero Row Execution", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowPaint ("Text Studio Hero Row Paint", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowColor ("Text Studio Hero Row Color", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowPaintTransform0 ("Text Studio Hero Row Paint Transform 0", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowPaintTransform1 ("Text Studio Hero Row Paint Transform 1", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowParameters0 ("Text Studio Hero Row Parameters 0", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowParameters1 ("Text Studio Hero Row Parameters 1", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowParameters2 ("Text Studio Hero Row Parameters 2", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopPositions0 ("Text Studio Hero Row Stop Positions 0", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopPositions1 ("Text Studio Hero Row Stop Positions 1", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor0 ("Text Studio Hero Row Stop Color 0", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor1 ("Text Studio Hero Row Stop Color 1", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor2 ("Text Studio Hero Row Stop Color 2", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor3 ("Text Studio Hero Row Stop Color 3", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor4 ("Text Studio Hero Row Stop Color 4", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor5 ("Text Studio Hero Row Stop Color 5", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor6 ("Text Studio Hero Row Stop Color 6", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioHeroRowStopColor7 ("Text Studio Hero Row Stop Color 7", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off
        Fog { Mode Off }
        Blend One OneMinusSrcAlpha
        // A masked row draws its full-size quads where its row mask pre-pass wrote 1 (TextStudio2DHeroCompositor.RowMasks.cs).
        // The global comparison is 0 (disabled) for every other draw; it is a global so the shared row materials stay unchanged.
        Stencil
        {
            Ref 1
            ReadMask 1
            WriteMask 0
            Comp [_TextStudio2DHeroRowMask]
        }

        Pass
        {
            Name "TEXTSTUDIO_HERO_LAYER"
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex VertShader
            #pragma fragment PixShader
            #pragma dynamic_branch OUTLINE_ON
            #pragma dynamic_branch UNDERLAY_ON UNDERLAY_INNER
            // Each row compiles only what it draws: its heavy family, or solid paint for a light row with solid paint, and
            // material-tier effects only when the text uses one. See TextStudio2DHeroMaterials.ResolveRowMaterial.
            // TEXT_STUDIO_UNIFORM_ROW draws a fill, stroke, shadow or glow row from its record in material constants
            // instead of the style table (TextStudio2DHeroMaterials.SupportsUniformRow).
            // TEXT_STUDIO_PLAIN_* compile a material, graphic or animated row for the plain draw (Operation source mode, full source
            // range, no glyph filter, no coverage replay, no colour source): those per-draw values become the constants below.
            #pragma multi_compile_local __ TEXT_STUDIO_SOLID_PAINT TEXT_STUDIO_FAMILY_MATERIAL TEXT_STUDIO_FAMILY_GRAPHIC TEXT_STUDIO_FAMILY_ANIMATED TEXT_STUDIO_FAMILY_DEPTH TEXT_STUDIO_UNIFORM_ROW TEXT_STUDIO_PLAIN_MATERIAL TEXT_STUDIO_PLAIN_GRAPHIC TEXT_STUDIO_PLAIN_ANIMATED
            #pragma multi_compile_local __ TEXT_STUDIO_MATERIAL_EFFECTS
            #if defined(TEXT_STUDIO_PLAIN_MATERIAL)
            #define TEXT_STUDIO_FAMILY_MATERIAL 1
            #define TEXT_STUDIO_HERO_PLAIN_ROW 1
            #elif defined(TEXT_STUDIO_PLAIN_GRAPHIC)
            #define TEXT_STUDIO_FAMILY_GRAPHIC 1
            #define TEXT_STUDIO_HERO_PLAIN_ROW 1
            #elif defined(TEXT_STUDIO_PLAIN_ANIMATED)
            #define TEXT_STUDIO_FAMILY_ANIMATED 1
            #define TEXT_STUDIO_HERO_PLAIN_ROW 1
            #endif
            #define TEXT_STUDIO_STYLE_TABLE 1
            #define TEXT_STUDIO_SINGLE_STYLE_OPERATION 1
            #define TEXT_STUDIO_FAMILY_VARIANTS 1
            #define TEXT_STUDIO_MATERIAL_EFFECT_VARIANTS 1
            #define TEXT_STUDIO_FULL_PRECISION_PAYLOAD 1
            #define TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD 1
            #define UNITY_UI_CLIP_RECT 0
            #define UNITY_UI_ALPHACLIP 0
            int _TextStudio2DHeroOperationIndex;
            int _TextStudio2DHeroSelectorIndex;
            int _TextStudio2DHeroStyleSlot;
            #ifdef TEXT_STUDIO_HERO_PLAIN_ROW
            static const int _TextStudio2DHeroSourceMode = 0;
            static const int _TextStudio2DHeroGlyphFilter = -1;
            static const float4 _TextStudio2DHeroSourceRange = float4(0, 2147483648.0, 0, 0);
            #else
            int _TextStudio2DHeroSourceMode;
            int _TextStudio2DHeroGlyphFilter;
            float4 _TextStudio2DHeroSourceRange;
            #endif
            float4 _TextStudio2DHeroScopeSelectors0;
            float4 _TextStudio2DHeroScopeSelectors1;
            #include "TextStudio2DProductionSDFCommon.cginc"
            ENDCG
        }
    }
}
