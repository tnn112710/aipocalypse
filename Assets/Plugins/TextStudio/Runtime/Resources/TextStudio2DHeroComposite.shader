Shader "Hidden/Text Studio/2D Hero Composite"
{
    Properties
    {
        _MainTex ("Backdrop", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "COMPOSITE"
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroCompositeFragment
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _TextStudioHeroSourceTex;
            sampler2D _TextStudioHeroMaskTex;
            int _TextStudioHeroBlendMode;
            half _TextStudioHeroOpacity;
            half4 _TextStudioHeroInvocationTint;
            half _TextStudioHeroUseMask;
            half _TextStudioHeroInvertMask;
            half _TextStudioHeroMaskStrength;
            half _TextStudioHeroSourceIsBackdropFilter;

            #include "TextStudio2DBlendCommon.cginc"

            fixed4 TextStudioHeroCompositeFragment(v2f_img input) : SV_Target
            {
                half4 backdrop = tex2D(_MainTex, input.uv);
                half4 source = tex2D(_TextStudioHeroSourceTex, input.uv);
                source.rgb *= _TextStudioHeroInvocationTint.rgb * _TextStudioHeroInvocationTint.a;
                source.a *= _TextStudioHeroInvocationTint.a;
                half contribution = saturate(_TextStudioHeroOpacity);
                if (_TextStudioHeroUseMask > .5)
                {
                    half mask = tex2D(_TextStudioHeroMaskTex, input.uv).a;
                    mask = lerp(mask, 1 - mask, saturate(_TextStudioHeroInvertMask));
                    mask = lerp(1, mask, saturate(_TextStudioHeroMaskStrength));
                    contribution *= saturate(mask);
                }
                if (_TextStudioHeroSourceIsBackdropFilter > .5)
                {
                    if (_TextStudioHeroBlendMode == 0)
                        return lerp(backdrop, source, contribution);

                    half backdropAlpha = saturate(backdrop.a);
                    half sourceAlpha = saturate(source.a);
                    half3 backdropColor = backdropAlpha > .000001
                        ? backdrop.rgb / backdropAlpha
                        : 0;
                    half3 sourceColor = sourceAlpha > .000001
                        ? source.rgb / sourceAlpha
                        : 0;
                    half3 filteredColor = TextStudioBlend(
                        backdropColor,
                        sourceColor,
                        _TextStudioHeroBlendMode);
                    half filteredAlpha = max(backdropAlpha, sourceAlpha);
                    half4 filtered = half4(
                        filteredColor * filteredAlpha,
                        filteredAlpha);
                    return lerp(backdrop, filtered, contribution);
                }
                source *= contribution;

                return TextStudioBlendPremultiplied(backdrop, source, _TextStudioHeroBlendMode);
            }
            ENDCG
        }

        Pass
        {
            Name "MASK_BLUR_HORIZONTAL"
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroMaskBlurHorizontal
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half _TextStudioHeroBlurRadius;

            half4 TextStudioHeroMaskBlurHorizontal(v2f_img input) : SV_Target
            {
                float2 stepUv = float2(
                    _MainTex_TexelSize.x * max(0, _TextStudioHeroBlurRadius) * .25,
                    0);
                half alpha = tex2D(_MainTex, input.uv).a * .227027;
                alpha += tex2D(_MainTex, input.uv + stepUv * 1.384615).a * .316216;
                alpha += tex2D(_MainTex, input.uv - stepUv * 1.384615).a * .316216;
                alpha += tex2D(_MainTex, input.uv + stepUv * 3.230769).a * .070270;
                alpha += tex2D(_MainTex, input.uv - stepUv * 3.230769).a * .070270;
                return alpha.xxxx;
            }
            ENDCG
        }

        Pass
        {
            Name "MASK_BLUR_VERTICAL"
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroMaskBlurVertical
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            half _TextStudioHeroBlurRadius;

            half4 TextStudioHeroMaskBlurVertical(v2f_img input) : SV_Target
            {
                float2 stepUv = float2(
                    0,
                    _MainTex_TexelSize.y * max(0, _TextStudioHeroBlurRadius) * .25);
                half alpha = tex2D(_MainTex, input.uv).a * .227027;
                alpha += tex2D(_MainTex, input.uv + stepUv * 1.384615).a * .316216;
                alpha += tex2D(_MainTex, input.uv - stepUv * 1.384615).a * .316216;
                alpha += tex2D(_MainTex, input.uv + stepUv * 3.230769).a * .070270;
                alpha += tex2D(_MainTex, input.uv - stepUv * 3.230769).a * .070270;
                return alpha.xxxx;
            }
            ENDCG
        }

        Pass
        {
            Name "APPLY_MASK"
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroApplyMask
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _TextStudioHeroMaskTex;
            half _TextStudioHeroInvertMask;
            half _TextStudioHeroMaskStrength;

            fixed4 TextStudioHeroApplyMask(v2f_img input) : SV_Target
            {
                half4 source = tex2D(_MainTex, input.uv);
                half mask = tex2D(_TextStudioHeroMaskTex, input.uv).a;
                mask = lerp(mask, 1 - mask, saturate(_TextStudioHeroInvertMask));
                mask = lerp(1, mask, saturate(_TextStudioHeroMaskStrength));
                return source * saturate(mask);
            }
            ENDCG
        }

        // Writes a texture into a render target array slice. WebGL only honours the destination slice of a blit that
        // uses a material, so Text Studio's drawn slices go through this pass instead of the default blit.
        Pass
        {
            Name "SLICE_COPY"
            Blend One Zero

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment TextStudioSliceCopy
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _TextStudioSliceSourceScale;

            float4 TextStudioSliceCopy(v2f_img input) : SV_Target
            {
                return tex2D(_MainTex, input.uv * _TextStudioSliceSourceScale.xy);
            }
            ENDCG
        }

        // Writes one row mask into the stencil: the quads of a row mask class, transformed like the Hero layer's vertices
        // (including the TMP vertex offset), so a masked row's full-size quads shade only inside its own support.
        Pass
        {
            Name "ROW_MASK"
            ColorMask 0
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex TextStudioHeroRowMaskVertex
            #pragma fragment TextStudioHeroRowMaskFragment
            #include "UnityCG.cginc"

            float4 _TextStudioHeroRowMaskOffset;

            float4 TextStudioHeroRowMaskVertex(float4 vertex : POSITION) : SV_POSITION
            {
                vertex.xy += _TextStudioHeroRowMaskOffset.xy;
                return UnityObjectToClipPos(vertex);
            }

            fixed4 TextStudioHeroRowMaskFragment() : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        // Composites cached static rows over the target with the rows' own premultiplied source-over blend. Both surfaces share
        // one descriptor, so each pixel loads exactly the texel it covers, unfiltered. Above composed rows of an RGBA8 target the
        // global comparison is NotEqual, so the cache lands only where BACKDROP_MASK left the stencil empty.
        Pass
        {
            Name "CACHED_ROWS"
            Blend One OneMinusSrcAlpha
            Stencil
            {
                Ref 1
                ReadMask 1
                WriteMask 0
                Comp [_TextStudioHeroCachedRowsStencil]
            }

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex TextStudioHeroCachedRowsVertex
            #pragma fragment TextStudioHeroCachedRowsFragment
            #include "UnityCG.cginc"

            Texture2D _TextStudioHeroCachedRows;

            float4 TextStudioHeroCachedRowsVertex(uint vertexId : SV_VertexID) : SV_POSITION
            {
                // One triangle covering the viewport.
                float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
                return float4(corner * 2 - 1, 0, 1);
            }

            float4 TextStudioHeroCachedRowsFragment(float4 position : SV_POSITION) : SV_Target
            {
                return _TextStudioHeroCachedRows.Load(int3(position.xy, 0));
            }
            ENDCG
        }

        // Marks with stencil 1 every pixel where the composition below a cached run is not empty. There the run's rows draw
        // over it with their own rounding; elsewhere the cached run equals the drawn rows exactly.
        Pass
        {
            Name "BACKDROP_MASK"
            ColorMask 0
            Stencil
            {
                Ref 1
                Comp Always
                Pass Replace
            }

            CGPROGRAM
            #pragma target 3.5
            #pragma vertex TextStudioHeroBackdropMaskVertex
            #pragma fragment TextStudioHeroBackdropMaskFragment
            #include "UnityCG.cginc"

            Texture2D _TextStudioHeroBackdrop;

            float4 TextStudioHeroBackdropMaskVertex(uint vertexId : SV_VertexID) : SV_POSITION
            {
                float2 corner = float2((vertexId << 1) & 2, vertexId & 2);
                return float4(corner * 2 - 1, 0, 1);
            }

            fixed4 TextStudioHeroBackdropMaskFragment(float4 position : SV_POSITION) : SV_Target
            {
                if (!any(_TextStudioHeroBackdrop.Load(int3(position.xy, 0))))
                    discard;
                return 0;
            }
            ENDCG
        }
    }
}
