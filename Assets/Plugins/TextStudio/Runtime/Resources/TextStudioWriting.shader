Shader "Text Studio/Writing"
{
    Properties
    {
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use UI Alpha Clip", Float) = 0
        [HideInInspector] _UIVertexColorAlwaysGammaSpace ("UI Vertex Color Always Gamma Space", Float) = 0
    }

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
            Name "TEXTSTUDIO_WRITING"
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex TextStudioWritingVertex
            #pragma fragment TextStudioWritingFragment
            #pragma dynamic_branch UNITY_UI_CLIP_RECT
            #pragma dynamic_branch UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct TextStudioGpuWritingTransform
            {
                float4 matrixRow0;
                float4 matrixRow1;
                float4 matrixRow2;
                float4 cornerBottomLeftTopLeft;
                float4 cornerTopRightBottomRight;
                float4 colorMultiplierAndAlpha;
                float4 colorAdditiveAndEffectScale;
            };

            #if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
            // Each packed sample travels as four exact 16-bit halves: low and high of x, then of y.
            Texture2D<float4> _TextStudioWritingSamplesData;
            uint2 TextStudioLoadWritingSample(uint texel)
            {
                uint4 halves = (uint4)round(_TextStudioWritingSamplesData.Load(int3(texel & 1023u, texel >> 10, 0)));
                return uint2(halves.x | halves.y << 16, halves.z | halves.w << 16);
            }
            #else
            StructuredBuffer<uint2> _TextStudioWritingSamples;
            uint2 TextStudioLoadWritingSample(uint index) { return _TextStudioWritingSamples[index]; }
            #endif
            #if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
            Texture2D<float4> _TextStudioWritingTransformsData;
            TextStudioGpuWritingTransform TextStudioLoadWritingTransform(uint index)
            {
                int texel = (int)index * 7;
                TextStudioGpuWritingTransform transform;
                transform.matrixRow0 = _TextStudioWritingTransformsData.Load(int3(texel & 1023, texel >> 10, 0)); texel++;
                transform.matrixRow1 = _TextStudioWritingTransformsData.Load(int3(texel & 1023, texel >> 10, 0)); texel++;
                transform.matrixRow2 = _TextStudioWritingTransformsData.Load(int3(texel & 1023, texel >> 10, 0)); texel++;
                transform.cornerBottomLeftTopLeft = _TextStudioWritingTransformsData.Load(int3(texel & 1023, texel >> 10, 0)); texel++;
                transform.cornerTopRightBottomRight = _TextStudioWritingTransformsData.Load(int3(texel & 1023, texel >> 10, 0)); texel++;
                transform.colorMultiplierAndAlpha = _TextStudioWritingTransformsData.Load(int3(texel & 1023, texel >> 10, 0)); texel++;
                transform.colorAdditiveAndEffectScale = _TextStudioWritingTransformsData.Load(int3(texel & 1023, texel >> 10, 0));
                return transform;
            }
            #else
            StructuredBuffer<TextStudioGpuWritingTransform>
                _TextStudioWritingTransforms;
            TextStudioGpuWritingTransform TextStudioLoadWritingTransform(uint index) { return _TextStudioWritingTransforms[index]; }
            #endif
            float _TextStudioWritingEnabled;
            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;

            struct WritingVertexInput
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };

            struct WritingPixelInput
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float3 path : TEXCOORD0;
                nointerpolation uint sampleIndex : TEXCOORD1;
                float2 localPosition : TEXCOORD2;
            };

            float2 TextStudioWritingCornerOffset(
                TextStudioGpuWritingTransform transform,
                float2 local)
            {
                float2 bottomLeft = transform.cornerBottomLeftTopLeft.xy;
                float2 topLeft = transform.cornerBottomLeftTopLeft.zw;
                float2 topRight = transform.cornerTopRightBottomRight.xy;
                float2 bottomRight = transform.cornerTopRightBottomRight.zw;
                float2 bottom = lerp(bottomLeft, bottomRight, local.x);
                float2 top = lerp(topLeft, topRight, local.x);
                return lerp(bottom, top, local.y) *
                    transform.colorAdditiveAndEffectScale.w;
            }

            WritingPixelInput TextStudioWritingVertex(WritingVertexInput input)
            {
                WritingPixelInput output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                uint sampleIndex = (uint)max(0, round(input.uv1.x));
                uint transformIndex = (uint)max(0, round(input.uv1.y));
                TextStudioGpuWritingTransform transform =
                    TextStudioLoadWritingTransform(transformIndex);
                float3 source = input.vertex.xyz;
                source.xy += TextStudioWritingCornerOffset(
                    transform,
                    saturate(input.uv2.xy));
                float3 transformed = float3(
                    dot(transform.matrixRow0.xyz, source) +
                        transform.matrixRow0.w,
                    dot(transform.matrixRow1.xyz, source) +
                        transform.matrixRow1.w,
                    dot(transform.matrixRow2.xyz, source) +
                        transform.matrixRow2.w);
                output.vertex = UnityObjectToClipPos(float4(transformed, 1));
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    input.color.rgb = UIGammaToLinear(input.color.rgb);
                fixed3 animated = saturate(
                    input.color.rgb *
                        transform.colorMultiplierAndAlpha.rgb +
                    transform.colorAdditiveAndEffectScale.rgb);
                output.color = fixed4(
                    animated,
                    input.color.a * transform.colorMultiplierAndAlpha.a);
                output.path = input.uv0.xyz;
                output.sampleIndex = sampleIndex;
                output.localPosition = transformed.xy;
                return output;
            }

            fixed4 TextStudioWritingFragment(WritingPixelInput input) : SV_Target
            {
                if (_TextStudioWritingEnabled < .5)
                    return 0;
                uint2 packed = TextStudioLoadWritingSample(input.sampleIndex);
                half progress = (packed.x & 0xffffu) / 65535.0;
                half opacity = (packed.x >> 16) / 65535.0;
                opacity *= 1 - input.path.z * ((packed.y & 0x0fffu) / 4095.0);
                half active = (packed.y >> 20) & 1u;
                uint brush = (packed.y >> 29) & 7u;
                if (active < .5 || opacity <= 0 || progress <= 0)
                    return 0;
                half softness = ((packed.y >> 21) & 0xffu) / 255.0;
                half revealWidth = max(
                    fwidth(input.path.x) * 1.5,
                    softness * .025);
                half revealed = 1 - smoothstep(
                    progress - revealWidth,
                    progress + revealWidth,
                    input.path.x);
                half edgeWidth = max(fwidth(input.path.y) * 1.5, .002);
                half crossDistance = abs(input.path.y);
                half edgeStart = 1;
                half edgeEnd = 1 + edgeWidth;
                half brushOpacity = 1;
                float2 noiseCell = 0;
                half noise = 0;
                if (brush == 1u || brush == 3u || brush == 4u)
                {
                    float sampleSeed =
                        (float)(input.sampleIndex & 1023u);
                    noiseCell = floor(float2(
                        input.path.x * 311.0 + sampleSeed * 19.0,
                        input.path.y * 97.0 + sampleSeed * 7.0));
                    noise = frac(52.9829189 * frac(dot(
                        noiseCell,
                        float2(.06711056, .00583715))));
                }
                if (brush == 1u)
                {
                    brushOpacity = lerp(.9, 1, noise);
                    edgeStart -= (noise - .5) * .025;
                }
                else if (brush == 2u)
                {
                    edgeStart = .965;
                    edgeEnd = 1 + edgeWidth * .55;
                }
                else if (brush == 3u)
                {
                    half markerBand = 1 - abs(
                        frac(input.path.x * 23.0 + noise * .18) * 2 - 1);
                    markerBand = smoothstep(.08, .92, markerBand);
                    brushOpacity = lerp(.9, 1, markerBand);
                    edgeStart = .985;
                    edgeEnd = 1 + edgeWidth * .7;
                }
                else if (brush == 4u)
                {
                    half coarse = frac(52.9829189 * frac(dot(
                        floor(noiseCell * .23),
                        float2(.06711056, .00583715))));
                    half pore = smoothstep(.12, .72, noise * .72 + coarse * .28);
                    brushOpacity = lerp(.34, 1, pore);
                    half roughness = (noise - .5) * .18;
                    edgeStart = .88 + roughness;
                    edgeEnd = 1.08 + roughness + edgeWidth;
                }
                half edge = 1 - smoothstep(
                    edgeStart,
                    edgeEnd,
                    crossDistance);
                half alpha = saturate(
                    input.color.a * opacity * revealed * edge * brushOpacity);
                fixed4 result = fixed4(input.color.rgb * alpha, alpha);
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
    }
}
