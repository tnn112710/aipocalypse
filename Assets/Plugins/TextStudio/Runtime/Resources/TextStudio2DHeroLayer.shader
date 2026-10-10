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
            #define TEXT_STUDIO_STYLE_TABLE 1
            #define TEXT_STUDIO_SINGLE_STYLE_OPERATION 1
            #define TEXT_STUDIO_FULL_PRECISION_PAYLOAD 1
            #define TEXT_STUDIO_SEPARATE_SELECTION_PAYLOAD 1
            #define UNITY_UI_CLIP_RECT 0
            #define UNITY_UI_ALPHACLIP 0
            int _TextStudio2DHeroOperationIndex;
            int _TextStudio2DHeroSelectorIndex;
            int _TextStudio2DHeroStyleSlot;
            int _TextStudio2DHeroSourceMode;
            int _TextStudio2DHeroGlyphFilter;
            float4 _TextStudio2DHeroSourceRange;
            float4 _TextStudio2DHeroScopeSelectors0;
            float4 _TextStudio2DHeroScopeSelectors1;
            #include "TextStudio2DProductionSDFCommon.cginc"
            ENDCG
        }
    }
}
