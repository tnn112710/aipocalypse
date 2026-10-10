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
    }
}
