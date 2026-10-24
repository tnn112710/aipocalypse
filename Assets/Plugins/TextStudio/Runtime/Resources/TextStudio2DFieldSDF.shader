Shader "Text Studio/2D Field SDF"
{
    Properties
    {
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
        _TextureWidth ("Texture Width", Float) = 512
        _TextureHeight ("Texture Height", Float) = 512
        _GradientScale ("Gradient Scale", Float) = 5
        [HideInInspector] _TextStudioNativeMaterialModel ("Text Studio Native Material Model", Float) = 0
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
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _CullMode ("Cull Mode", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
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
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull [_CullMode]
        ZWrite Off
        Lighting Off
        Fog { Mode Off }
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "TEXTSTUDIO_FIELD_SDF"
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            // Program tier: Basic, or one isolated operation family for ordered primitives. They never combine, and each
            // compiles only its own code; see TextStudio2DShaderTiers.
            #pragma multi_compile_local __ TEXT_STUDIO_APPEARANCE_BASIC TEXT_STUDIO_FAMILY_MATERIAL TEXT_STUDIO_FAMILY_GRAPHIC TEXT_STUDIO_FAMILY_ANIMATED TEXT_STUDIO_FAMILY_DEPTH
            #pragma multi_compile_local __ TEXT_STUDIO_MATERIAL_EFFECTS
            #pragma multi_compile_local __ TEXT_STUDIO_SOLID_PAINT
            #pragma vertex VertShader
            #pragma fragment PixShader
            #pragma dynamic_branch OUTLINE_ON
            #pragma dynamic_branch UNDERLAY_ON UNDERLAY_INNER
            #pragma dynamic_branch UNITY_UI_CLIP_RECT
            #pragma dynamic_branch UNITY_UI_ALPHACLIP
            #define TEXT_STUDIO_STYLE_TABLE 1
            #define TEXT_STUDIO_MATERIAL_EFFECT_VARIANTS 1
            #define TEXT_STUDIO_FAMILY_VARIANTS 1
            #include "TextStudio2DProductionSDFCommon.cginc"
            ENDCG
        }

        Pass
        {
            Name "TEXTSTUDIO_FIELD_SHADOW_CASTER"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            ColorMask 0
            Offset 1, 1

            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            // Shadows keep one program with every family, material effect and paint (eight variants): specializing them
            // tripled the build's shader compile time for a rarely used pass.
            #pragma multi_compile_local __ TEXT_STUDIO_APPEARANCE_BASIC
            #pragma vertex VertShader
            #pragma fragment TextStudioWorldShadowFragment
            #pragma multi_compile_shadowcaster
            #define TEXT_STUDIO_STYLE_TABLE 1
            #define OUTLINE_ON 0
            #define UNDERLAY_ON 0
            #define UNDERLAY_INNER 0
            #define UNITY_UI_CLIP_RECT 0
            #define UNITY_UI_ALPHACLIP 0
            #include "TextStudio2DProductionSDFCommon.cginc"
            ENDCG
        }
    }
    CustomEditor "TMPro.EditorUtilities.TMP_SDFShaderGUI"
}
