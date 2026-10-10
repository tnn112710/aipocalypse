Shader "Text Studio/2D Hero Output"
{
    Properties
    {
        // Canvas requires a 2D main texture, so composed pages travel in their own array properties.
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        [HideInInspector] _TextStudioHeroOutputPages ("Composed Text Pages", 2DArray) = "" {}
        [HideInInspector] _TextStudioHeroShadowPages ("Shadow Coverage Pages", 2DArray) = "" {}
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

    CGINCLUDE
    #include "UnityCG.cginc"
    // Outputs are slices of shared pages. uv0.xy samples the owner's valid rectangle, z selects its color slice and w its
    // shadow slice, so compatible outputs share one texture and material without property blocks.
    UNITY_DECLARE_TEX2DARRAY(_TextStudioHeroOutputPages);
    UNITY_DECLARE_TEX2DARRAY(_TextStudioHeroShadowPages);
    // One UnityPerMaterial layout in every pass keeps generated world outputs SRP Batcher compatible.
    CBUFFER_START(UnityPerMaterial)
    fixed4 _Color;
    float4 _ClipRect;
    float _TextStudioHeroCameraOutput;
    // Camera-projected Canvas output re-renders inside the camera callback, after Canvas geometry was built, so its
    // valid extent (xy) and slice (z) travel on the owner's camera material instead of the vertex UVs.
    float4 _TextStudioHeroCameraSlice;
    float4 _TextStudioHeroCameraBounds;
    float4x4 _TextStudioHeroOutputToWorld;
    CBUFFER_END

    struct TextStudioHeroOutputVertexInput
    {
        float4 vertex : POSITION;
        fixed4 color : COLOR;
        float4 uv : TEXCOORD0;
    };

    // Camera projection rebuilds the quad from its corner. Corner UVs are exactly zero or the valid-rectangle extent.
    float4 TextStudioHeroOutputObjectPosition(TextStudioHeroOutputVertexInput input)
    {
        if (_TextStudioHeroCameraOutput < .5)
            return input.vertex;
        float2 positionUv = step(1e-6, input.uv.xy);
        #if UNITY_UV_STARTS_AT_TOP
        positionUv.y = 1 - positionUv.y;
        #endif
        float2 local = _TextStudioHeroCameraBounds.xy + positionUv * _TextStudioHeroCameraBounds.zw;
        float4 world = mul(_TextStudioHeroOutputToWorld, float4(local, 0, 1));
        return mul(unity_WorldToObject, world);
    }

    float4 TextStudioHeroOutputUv(float4 uv)
    {
        if (_TextStudioHeroCameraOutput < .5)
            return uv;
        return float4(step(1e-6, uv.xy) * _TextStudioHeroCameraSlice.xy, _TextStudioHeroCameraSlice.zz);
    }

    fixed4 TextStudioHeroOutputSample(float4 uv)
    {
        return UNITY_SAMPLE_TEX2DARRAY(_TextStudioHeroOutputPages, uv.xyz);
    }

    struct TextStudioHeroCoverageInput
    {
        float4 vertex : SV_POSITION;
        float4 uv : TEXCOORD0;
    };

    TextStudioHeroCoverageInput TextStudioHeroCoverageVertex(TextStudioHeroOutputVertexInput input)
    {
        TextStudioHeroCoverageInput output;
        output.vertex = UnityObjectToClipPos(TextStudioHeroOutputObjectPosition(input));
        output.uv = TextStudioHeroOutputUv(input.uv);
        return output;
    }
    ENDCG

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

        Pass
        {
            Name "TEXTSTUDIO_HERO_OUTPUT"
            CGPROGRAM
            #pragma target 3.5
            #pragma vertex TextStudioHeroOutputVertex
            #pragma fragment TextStudioHeroOutputFragment
            #pragma dynamic_branch UNITY_UI_CLIP_RECT
            #pragma dynamic_branch UNITY_UI_ALPHACLIP
            #include "UnityUI.cginc"

            struct TextStudioHeroOutputPixelInput
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 uv : TEXCOORD0;
                float2 localPosition : TEXCOORD1;
            };

            TextStudioHeroOutputPixelInput TextStudioHeroOutputVertex(
                TextStudioHeroOutputVertexInput input)
            {
                TextStudioHeroOutputPixelInput output;
                float4 position = TextStudioHeroOutputObjectPosition(input);
                output.vertex = UnityObjectToClipPos(position);
                output.color = input.color * _Color;
                output.uv = TextStudioHeroOutputUv(input.uv);
                output.localPosition = position.xy;
                return output;
            }

            fixed4 TextStudioHeroOutputFragment(
                TextStudioHeroOutputPixelInput input) : SV_Target
            {
                fixed4 textureColor = TextStudioHeroOutputSample(input.uv);
                fixed4 result;
                result.rgb = textureColor.rgb * input.color.rgb * input.color.a;
                result.a = textureColor.a * input.color.a;
                UNITY_BRANCH
                if (UNITY_UI_CLIP_RECT)
                    result *= UnityGet2DClipping(input.localPosition, _ClipRect);
                UNITY_BRANCH
                if (UNITY_UI_ALPHACLIP)
                    clip(result.a - .001);
                return result;
            }
            ENDCG
        }

        Pass
        {
            Name "TEXTSTUDIO_HERO_SHADOW_CASTER"
            Tags { "LightMode"="ShadowCaster" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            ColorMask 0
            Offset 1, 1

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex TextStudioHeroCoverageVertex
            #pragma fragment TextStudioHeroShadowFragment
            #pragma multi_compile_shadowcaster

            fixed4 TextStudioHeroShadowFragment(TextStudioHeroCoverageInput input) : SV_Target
            {
                clip(UNITY_SAMPLE_TEX2DARRAY(_TextStudioHeroShadowPages, float3(input.uv.xy, input.uv.w)).a * _Color.a - .001);
                return 0;
            }
            ENDCG
        }

        // Editor selection outlines and picking read the page slice, so they match the displayed silhouette.
        Pass
        {
            Name "SceneSelectionPass"
            Tags { "LightMode"="SceneSelectionPass" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            ColorMask RGBA

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex TextStudioHeroCoverageVertex
            #pragma fragment TextStudioHeroSelectionFragment
            int _ObjectId;
            int _PassValue;

            float4 TextStudioHeroSelectionFragment(TextStudioHeroCoverageInput input) : SV_Target
            {
                clip(TextStudioHeroOutputSample(input.uv).a - .001);
                return float4(_ObjectId, _PassValue, 1, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Picking"
            Tags { "LightMode"="Picking" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off
            ColorMask RGBA

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex TextStudioHeroCoverageVertex
            #pragma fragment TextStudioHeroPickingFragment
            float4 _SelectionID;

            float4 TextStudioHeroPickingFragment(TextStudioHeroCoverageInput input) : SV_Target
            {
                clip(TextStudioHeroOutputSample(input.uv).a - .001);
                return _SelectionID;
            }
            ENDCG
        }
    }
}
