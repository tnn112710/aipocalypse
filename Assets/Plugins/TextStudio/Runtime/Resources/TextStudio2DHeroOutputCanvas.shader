Shader "Text Studio/2D Hero Output Canvas"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _TextStudioHeroOutputPages ("Composed Text Pages", 2DArray) = "" {}
        [HideInInspector] _TextStudioHeroCameraOutput ("Camera Output", Float) = 0
        [HideInInspector] _TextStudioHeroCameraSlice ("Camera Output Slice", Vector) = (1,1,0,0)
        _Color ("Tint", Color) = (1,1,1,1)
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use UI Alpha Clip", Float) = 0
    }

    // CanvasRenderer submits every pass of a material, so Canvas output reuses only the display pass; the world shader
    // alone owns the shadow caster, scene selection and picking passes.
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        UsePass "Text Studio/2D Hero Output/TEXTSTUDIO_HERO_OUTPUT"
    }
}
