Shader "Hidden/Text Studio/2D Hero Effects"
{
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        #include "TextStudio2DVisualPrimitiveCommon.cginc"
        #include "TextStudio2DStylePaintCommon.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _TextStudioHeroBackdropTex;
        sampler2D _TextStudioHeroEffectMaskTex;
        sampler2D _TextStudioHeroBlurCoverage;
        sampler2D _TextStudioHeroHistory0;
        sampler2D _TextStudioHeroHistory1;
        sampler2D _TextStudioHeroHistory2;
        sampler2D _TextStudioHeroHistory3;
        int _TextStudioHeroEffectKind;
        float4 _TextStudioHeroEffectParameters0;
        float4 _TextStudioHeroEffectParameters1;
        float4 _TextStudioHeroEffectDirection;
        float4 _TextStudioHeroEffectCoordinateTransform;
        fixed4 _TextStudioHeroEffectPrimary;
        fixed4 _TextStudioHeroEffectSecondary;
        float _TextStudio2DHeroTime;
        int _TextStudio2DHeroMotionPolicy;
        int _TextStudioHeroEffectReducedBehavior;
        int _TextStudioHeroHistoryCount;
        float2 _TextStudioHeroFocusBlurRadius;
        float4 _TextStudioHeroFocusBlurTexelSize;
        float4 _TextStudioHeroLensCenterRadius;
        float4 _TextStudioHeroLensProfile;
        float4 _TextStudioHeroColorAdjustmentRed;
        float4 _TextStudioHeroColorAdjustmentGreen;
        float4 _TextStudioHeroColorAdjustmentBlue;
        float4 _TextStudioHeroColorAdjustmentAlpha;
        float4 _TextStudioHeroColorAdjustmentOffset;
        float4 _TextStudioHeroColorAdjustmentSettings;
        float4 _TextStudioHeroAdjustmentSelection;
        sampler2D _TextStudioHeroCompositeCoverage;
        sampler2D _TextStudioHeroGeneratedAlpha;
        float _TextStudioHeroGeneratedPaintAlpha;
        int _TextStudio2DHeroOperationIndex;
        int _TextStudio2DHeroStyleSlot;

        float TextStudioHeroHash(float2 value)
        {
            float3 state = frac(float3(value.xyx) * .1031);
            state += dot(state, state.yzx + 33.33);
            return frac((state.x + state.y) * state.z);
        }

        half TextStudioHeroMotionRate(out half enabled)
        {
            return TextStudioVisualMotionRate(
                _TextStudio2DHeroMotionPolicy,
                _TextStudioHeroEffectReducedBehavior,
                enabled);
        }

        float2 TextStudioHeroDirection()
        {
            return TextStudioVisualNormalizeDirection(
                _TextStudioHeroEffectDirection.xy);
        }

        float2 TextStudioHeroCoordinate(float2 uv)
        {
            return uv * _TextStudioHeroEffectCoordinateTransform.xy +
                _TextStudioHeroEffectCoordinateTransform.zw;
        }

        float2 TextStudioHeroMappingOffsetToUv(float2 offset)
        {
            float2 uvOffset = offset / max(
                _TextStudioHeroEffectCoordinateTransform.xy,
                float2(.0001, .0001));
            #if UNITY_UV_STARTS_AT_TOP
                uvOffset.y = -uvOffset.y;
            #endif
            return uvOffset;
        }

        half4 TextStudioHeroSourceOver(half4 backdrop, half4 source)
        {
            return source + backdrop * (1 - saturate(source.a));
        }

        half4 TextStudioHeroStyledMain(float2 uv)
        {
            half4 backdrop = tex2D(_MainTex, uv);
            half4 fallback = tex2D(_TextStudioHeroEffectMaskTex, uv);
            return TextStudioHeroSourceOver(fallback, backdrop);
        }

        half4 TextStudioHeroSelectedMain(float2 uv, out half selection)
        {
            half4 backdrop = tex2D(_MainTex, uv);
            half4 fallback = tex2D(_TextStudioHeroEffectMaskTex, uv);
            selection = fallback.a;
            return TextStudioHeroSourceOver(fallback, backdrop);
        }

        half4 TextStudioHeroStyledBackdrop(float2 uv)
        {
            half4 backdrop = tex2D(_TextStudioHeroBackdropTex, uv);
            half4 fallback = tex2D(_TextStudioHeroEffectMaskTex, uv);
            return TextStudioHeroSourceOver(fallback, backdrop);
        }

        half4 TextStudioHeroSelectedBackdrop(float2 uv, out half selection)
        {
            half4 backdrop = tex2D(_TextStudioHeroBackdropTex, uv);
            half4 fallback = tex2D(_TextStudioHeroEffectMaskTex, uv);
            selection = fallback.a;
            return TextStudioHeroSourceOver(fallback, backdrop);
        }

        fixed4 TextStudioHeroWarp(v2f_img input) : SV_Target
        {
            half motionEnabled;
            half motionRate = TextStudioHeroMotionRate(motionEnabled);
            if (motionEnabled < .5)
                return TextStudioHeroStyledMain(input.uv);
            half amount = max(0, _TextStudioHeroEffectParameters0.x);
            half phase = _TextStudioHeroEffectParameters0.z +
                _TextStudio2DHeroTime *
                _TextStudioHeroEffectParameters0.y * motionRate;
            half scale = max(.01, _TextStudioHeroEffectParameters0.w);
            half softness = max(.0005, _TextStudioHeroEffectParameters1.y);
            half amplitude = max(0, _TextStudioHeroEffectParameters1.z);
            half frequency = max(.01, _TextStudioHeroEffectParameters1.w);
            float2 direction = TextStudioHeroDirection();
            float2 perpendicular = float2(-direction.y, direction.x);
            float2 coordinate = TextStudioHeroCoordinate(input.uv);
            float2 displacedUv = input.uv;

            if (_TextStudioHeroEffectKind == 5)
            {
                float row = floor(coordinate.y * scale);
                half gate = step(
                    .68 + softness * .2,
                    TextStudioHeroHash(float2(row, floor(phase * frequency))));
                half burst = sin((row * 1.73 + phase * frequency) * 6.28318530718);
                float2 offset = TextStudioHeroMappingOffsetToUv(
                    direction * amplitude * gate * burst * amount);
                half baseSelection;
                half centerSelection;
                half redSelection;
                half blueSelection;
                half4 base = TextStudioHeroSelectedMain(
                    input.uv,
                    baseSelection);
                half4 center = TextStudioHeroSelectedMain(
                    input.uv + offset,
                    centerSelection);
                half4 red = TextStudioHeroSelectedMain(
                    input.uv + offset * 1.7,
                    redSelection);
                half4 blue = TextStudioHeroSelectedMain(
                    input.uv - offset * 1.35,
                    blueSelection);
                half alpha = max(center.a, max(red.a, blue.a));
                half3 separated = half3(red.r, center.g, blue.b);
                separated = lerp(
                    separated,
                    separated * lerp(
                        _TextStudioHeroEffectSecondary.rgb,
                        _TextStudioHeroEffectPrimary.rgb,
                        .5 + .5 * burst),
                    saturate(gate * amount * .35));
                half coverage = max(
                    max(baseSelection, centerSelection),
                    max(redSelection, blueSelection));
                return lerp(
                    base,
                    half4(max(0, separated), alpha),
                    saturate(coverage * amount));
            }

            if (_TextStudioHeroEffectKind == 6)
            {
                half broadWave = sin(
                    (dot(coordinate, perpendicular) * scale +
                     phase * frequency) * 6.28318530718);
                half detailWave = sin(
                    (dot(coordinate, direction) * scale * .47 -
                     phase * frequency * .63 + broadWave * .18) *
                    6.28318530718);
                displacedUv += TextStudioHeroMappingOffsetToUv(
                    direction * broadWave * amplitude * amount +
                    perpendicular * detailWave * amplitude * amount * .28);
                half baseSelection;
                half refractedSelection;
                half4 base = TextStudioHeroSelectedMain(
                    input.uv,
                    baseSelection);
                half4 refracted = TextStudioHeroSelectedMain(
                    displacedUv,
                    refractedSelection);
                half coverage = max(baseSelection, refractedSelection);
                half thermalLift = saturate(.5 + .5 * broadWave);
                refracted.rgb *= lerp(
                    1,
                    lerp(
                        _TextStudioHeroEffectSecondary.rgb,
                        _TextStudioHeroEffectPrimary.rgb,
                        thermalLift),
                    saturate(amount * .12));
                return lerp(
                    base,
                    refracted,
                    saturate(coverage * amount * .72));
            }

            half curlA = sin(
                (dot(coordinate, direction) * scale + phase * frequency) *
                6.28318530718);
            half curlB = sin(
                (dot(coordinate, perpendicular) * scale * .61 -
                 phase * frequency * .71 + curlA * .22) *
                6.28318530718);
            half curlC = sin(
                ((coordinate.x + coordinate.y) * scale * .29 +
                 phase * frequency * .37) * 6.28318530718);
            displacedUv += TextStudioHeroMappingOffsetToUv(
                direction * (curlB + curlC * .22) * amplitude * amount +
                perpendicular * curlA * amplitude * amount * .48);
            half baseSelection;
            half warpedSelection;
            half4 base = TextStudioHeroSelectedMain(
                input.uv,
                baseSelection);
            half4 warped = TextStudioHeroSelectedMain(
                displacedUv,
                warpedSelection);
            half coverage = max(baseSelection, warpedSelection);
            half sheen = saturate(.5 + .5 * curlA * curlB);
            warped.rgb *= lerp(
                1,
                lerp(
                    _TextStudioHeroEffectSecondary.rgb,
                    _TextStudioHeroEffectPrimary.rgb,
                    sheen),
                saturate(amount * .16));
            return lerp(
                base,
                warped,
                saturate(coverage * (.58 + amount * .34)));
        }

        half4 TextStudioHeroHistory(sampler2D history, float2 uv)
        {
            half4 value = tex2D(history, uv);
            return _TextStudio2DGeometryCoveragePass > .5 ? value.rrrr : value;
        }

        fixed4 TextStudioHeroSpatial(v2f_img input) : SV_Target
        {
            half motionEnabled;
            half motionRate = TextStudioHeroMotionRate(motionEnabled);
            if (motionEnabled < .5)
                return TextStudioHeroStyledMain(input.uv);
            half amount = max(0, _TextStudioHeroEffectParameters0.x);
            half phase = _TextStudioHeroEffectParameters0.z +
                _TextStudio2DHeroTime *
                _TextStudioHeroEffectParameters0.y * motionRate;
            half amplitude = max(0, _TextStudioHeroEffectParameters1.z);
            float2 direction = TextStudioHeroDirection();

            if (_TextStudioHeroEffectKind == 9)
            {
                half sourceAlpha;
                half4 result = TextStudioHeroSelectedMain(
                    input.uv,
                    sourceAlpha);
                float2 stepUv = TextStudioHeroMappingOffsetToUv(
                    direction * amplitude *
                    (.65 + .55 * abs(sin(phase * 6.28318530718))));
                float2 primaryUv = input.uv - stepUv;
                half primarySelection;
                half4 primaryGhost = TextStudioHeroSelectedMain(
                    primaryUv,
                    primarySelection);
                primaryGhost.rgb *= lerp(
                    1,
                    _TextStudioHeroEffectPrimary.rgb,
                    .72);
                primaryGhost *= primarySelection *
                    saturate(amount * .62) * (1 - sourceAlpha);
                result = TextStudioHeroSourceOver(primaryGhost, result);
                float2 secondaryUv = input.uv - stepUv * 2;
                half secondarySelection;
                half4 secondaryGhost = TextStudioHeroSelectedMain(
                    secondaryUv,
                    secondarySelection);
                secondaryGhost.rgb *= lerp(
                    1,
                    _TextStudioHeroEffectSecondary.rgb,
                    .82);
                secondaryGhost *= secondarySelection *
                    saturate(amount * .48) * (1 - sourceAlpha);
                result = TextStudioHeroSourceOver(secondaryGhost, result);
                if (_TextStudioHeroHistoryCount > 0)
                {
                    half4 trail = TextStudioHeroHistory(
                        _TextStudioHeroHistory0,
                        input.uv - stepUv * 1.45);
                    trail.rgb *= lerp(1, _TextStudioHeroEffectPrimary.rgb, .36);
                    trail *= saturate(amount * .4) * (1 - sourceAlpha);
                    result = TextStudioHeroSourceOver(trail, result);
                }
                if (_TextStudioHeroHistoryCount > 1)
                {
                    half4 trail = TextStudioHeroHistory(
                        _TextStudioHeroHistory1,
                        input.uv - stepUv * 2.45);
                    trail.rgb *= lerp(1, _TextStudioHeroEffectSecondary.rgb, .44);
                    trail *= saturate(amount * .3) * (1 - sourceAlpha);
                    result = TextStudioHeroSourceOver(trail, result);
                }
                if (_TextStudioHeroHistoryCount > 2)
                {
                    half4 trail = TextStudioHeroHistory(
                        _TextStudioHeroHistory2,
                        input.uv - stepUv * 3.45);
                    trail.rgb *= lerp(1, _TextStudioHeroEffectSecondary.rgb, .62);
                    trail *= saturate(amount * .21) * (1 - sourceAlpha);
                    result = TextStudioHeroSourceOver(trail, result);
                }
                if (_TextStudioHeroHistoryCount > 3)
                {
                    half4 trail = TextStudioHeroHistory(
                        _TextStudioHeroHistory3,
                        input.uv - stepUv * 4.45);
                    trail.rgb *= lerp(1, _TextStudioHeroEffectSecondary.rgb, .76);
                    trail *= saturate(amount * .14) * (1 - sourceAlpha);
                    result = TextStudioHeroSourceOver(trail, result);
                }
                return result;
            }

            half frequency = max(.01, _TextStudioHeroEffectParameters1.w);
            half softness = saturate(
                _TextStudioHeroEffectParameters1.y * 3);
            half velocityCycle = .5 + .5 * sin(
                phase * frequency * 6.28318530718);
            half shutter = smoothstep(
                softness,
                max(softness + .001, 1 - softness),
                velocityCycle);
            half restingSpan = saturate(
                _TextStudioHeroEffectParameters1.x * 4);
            half span = lerp(restingSpan, 1, shutter);
            float2 stepUv = TextStudioHeroMappingOffsetToUv(
                direction * amplitude * amount * span / 4);
            half4 source = TextStudioHeroStyledMain(input.uv);
            half4 trail = 0;
            float2 trailUv = input.uv - stepUv * .72;
            half trailSelection;
            half4 trailSample = TextStudioHeroSelectedMain(
                trailUv,
                trailSelection);
            trail += trailSample * trailSelection * .36;
            trailUv = input.uv - stepUv * 1.65;
            trailSample = TextStudioHeroSelectedMain(
                trailUv,
                trailSelection);
            trail += trailSample * trailSelection * .27;
            trailUv = input.uv - stepUv * 2.75;
            trailSample = TextStudioHeroSelectedMain(
                trailUv,
                trailSelection);
            trail += trailSample * trailSelection * .19;
            trailUv = input.uv - stepUv * 4;
            trailSample = TextStudioHeroSelectedMain(
                trailUv,
                trailSelection);
            trail += trailSample * trailSelection * .12;
            trail.rgb *= lerp(
                1,
                _TextStudioHeroEffectSecondary.rgb,
                saturate(amount * lerp(.08, .22, shutter)));
            trail *= saturate(amount * lerp(.28, .72, shutter));
            return TextStudioHeroSourceOver(trail, source);
        }

        fixed4 TextStudioHeroBlurHorizontal(v2f_img input) : SV_Target
        {
            float2 stepUv = float2(
                _TextStudioHeroFocusBlurTexelSize.x *
                max(0, _TextStudioHeroFocusBlurRadius.x) / 12,
                0);
            half4 value = 0;
            float weightSum = 0;
            [unroll] for (int tap = -12; tap <= 12; tap++)
            {
                float weight = exp(-.03125 * tap * tap);
                value += TextStudioHeroStyledBackdrop(input.uv + stepUv * tap) * weight;
                weightSum += weight;
            }
            return value / weightSum;
        }

        fixed4 TextStudioHeroBlurVertical(v2f_img input) : SV_Target
        {
            float2 stepUv = float2(
                0,
                _TextStudioHeroFocusBlurTexelSize.y *
                max(0, _TextStudioHeroFocusBlurRadius.y) / 12);
            half4 value = 0;
            float weightSum = 0;
            [unroll] for (int tap = -12; tap <= 12; tap++)
            {
                float weight = exp(-.03125 * tap * tap);
                value += tex2D(_MainTex, input.uv + stepUv * tap) * weight;
                weightSum += weight;
            }
            value /= weightSum;
            half mask;
            half4 sharp = TextStudioHeroSelectedBackdrop(
                input.uv,
                mask);
            half motionEnabled;
            half motionRate = TextStudioHeroMotionRate(motionEnabled);
            if (motionEnabled < .5)
                return sharp;
            half amount = saturate(_TextStudioHeroEffectParameters0.x);
            half phase = _TextStudioHeroEffectParameters0.z +
                _TextStudio2DHeroTime *
                _TextStudioHeroEffectParameters0.y * motionRate;
            half width = max(.001, _TextStudioHeroEffectParameters1.x);
            float2 direction = TextStudioHeroDirection();
            float2 coordinate = TextStudioHeroCoordinate(input.uv);
            half projected = dot(coordinate - .5, direction) + .5;
            half focus = .5 + .58 * sin(phase * 6.28318530718);
            half blurMix = smoothstep(focus - width, focus + width, projected);
            half halo = saturate(max(0, value.a - sharp.a) * 3);
            half coverage = saturate(mask + halo);
            half boundary = 1 - saturate(
                abs(projected - focus) / max(.001, width * .42));
            half4 result = lerp(
                sharp,
                value,
                saturate(blurMix * amount * coverage));
            result.rgb += _TextStudioHeroEffectSecondary.rgb *
                boundary * mask * amount * .06;
            return result;
        }

        fixed4 TextStudioHeroLensMagnifier(v2f_img input) : SV_Target
        {
            float2 center = _TextStudioHeroLensCenterRadius.xy;
            float2 radiusUv = max(
                _TextStudioHeroLensCenterRadius.zw,
                float2(.000001, .000001));
            float2 radial = (input.uv - center) / radiusUv;
            half normalizedRadius = length(radial);
            half4 source = tex2D(_MainTex, input.uv);
            if (normalizedRadius > 1)
                return source;

            half t = saturate(normalizedRadius);
            t = t * t * (3 - 2 * t);
            half exponent = lerp(
                .5,
                3,
                saturate(_TextStudioHeroLensProfile.z));
            half magnification = lerp(
                max(1, _TextStudioHeroLensProfile.x),
                max(1, _TextStudioHeroLensProfile.y),
                pow(t, exponent));
            float2 sampledUv = center + (input.uv - center) / magnification;
            half4 magnified = tex2D(_MainTex, sampledUv);
            half feather = saturate(_TextStudioHeroLensProfile.w);
            half aperture = feather <= .000001
                ? 1
                : 1 - smoothstep(1 - feather, 1, normalizedRadius);
            return lerp(source, magnified, aperture);
        }

        half4 TextStudioHeroTextWideBlur(float2 uv, float2 stepUv)
        {
            half4 value = 0;
            half weightSum = 0;
            [unroll]
            for (int tap = -16; tap <= 16; tap++)
            {
                half weight = exp(-.03125 * tap * tap);
                value += tex2D(_MainTex, uv + stepUv * tap) * weight;
                weightSum += weight;
            }
            return value / weightSum;
        }

        fixed4 TextStudioHeroTextWideBlurHorizontal(v2f_img input) : SV_Target
        {
            float2 offsetUv = _TextStudioHeroEffectDirection.xy;
            #if UNITY_UV_STARTS_AT_TOP
                offsetUv.y = -offsetUv.y;
            #endif
            float2 stepUv = float2(
                _TextStudioHeroFocusBlurTexelSize.x *
                max(.5, _TextStudioHeroFocusBlurRadius.x) / 16,
                0);
            return TextStudioHeroTextWideBlur(input.uv - offsetUv, stepUv);
        }

        fixed4 TextStudioHeroTextWideBlurCoverageVertical(v2f_img input) : SV_Target
        {
            float2 stepUv = float2(
                0,
                _TextStudioHeroFocusBlurTexelSize.y *
                max(.5, _TextStudioHeroFocusBlurRadius.y) / 16);
            return TextStudioHeroTextWideBlur(input.uv, stepUv);
        }

        fixed4 TextStudioHeroTextWideBlurVertical(v2f_img input) : SV_Target
        {
            half4 blurred = TextStudioHeroTextWideBlurCoverageVertical(input);
            half blurredCoverage = tex2D(_TextStudioHeroBlurCoverage, input.uv).a;
            half sharpAlpha = tex2D(
                _TextStudioHeroEffectMaskTex,
                input.uv).a;
            half spreadRatio = saturate(
                _TextStudioHeroEffectParameters0.x /
                max(.0001, _TextStudioHeroEffectParameters0.y));
            half strength = lerp(2.15, 4.5, spreadRatio);
            half outerAlpha = saturate(
                max(0, blurredCoverage - sharpAlpha) * strength);
            return blurred * (outerAlpha / max(.00001, blurredCoverage));
        }

        fixed4 TextStudioHeroColorAdjustment(v2f_img input) : SV_Target
        {
            half4 source = tex2D(_MainTex, input.uv);
            half selection = tex2D(_TextStudioHeroEffectMaskTex, input.uv).a;
            half sourceAlpha = saturate(source.a);
            half coverage = _TextStudioHeroColorAdjustmentSettings.z > .5
                ? saturate(tex2D(_TextStudioHeroCompositeCoverage, input.uv).a) : 1;
            coverage = max(coverage, sourceAlpha);
            if (coverage <= .000001) return 0;
            half3 straightColor = sourceAlpha > .000001
                ? source.rgb / sourceAlpha
                : 0;
            half4 straight = half4(straightColor, saturate(sourceAlpha / coverage));
            #ifdef UNITY_COLORSPACE_GAMMA
            straight.rgb = GammaToLinearSpace(straight.rgb);
            #endif
            half4 transformed = half4(
                dot(_TextStudioHeroColorAdjustmentRed, straight),
                dot(_TextStudioHeroColorAdjustmentGreen, straight),
                dot(_TextStudioHeroColorAdjustmentBlue, straight),
                dot(_TextStudioHeroColorAdjustmentAlpha, straight));
            transformed += _TextStudioHeroColorAdjustmentOffset;
            if (_TextStudioHeroColorAdjustmentSettings.y > .5)
                transformed = saturate(transformed);
            transformed = lerp(straight, transformed, saturate(_TextStudioHeroColorAdjustmentSettings.x));
            #ifdef UNITY_COLORSPACE_GAMMA
            transformed.rgb = LinearToGammaSpace(transformed.rgb);
            #endif
            half transformedAlpha = saturate(transformed.a) * coverage;
            half4 premultiplied = half4(
                transformed.rgb * transformedAlpha,
                transformedAlpha);
            return lerp(source, premultiplied, saturate(selection));
        }

        half TextStudioHeroDepthSample(float2 uv)
        {
            return tex2D(_MainTex, uv).a;
        }

        fixed4 TextStudioHeroFauxDepth(v2f_img input) : SV_Target
        {
            if (TEXT_STUDIO_STYLE_TABLE_ENABLED < .5 ||
                _TextStudio2DHeroStyleSlot <= 0 ||
                _TextStudio2DHeroOperationIndex < 0 ||
                _TextStudio2DHeroOperationIndex >= (int)round(TextStudioLoadStyleHeader(_TextStudio2DHeroStyleSlot).metadata.y))
            {
                return 0;
            }

            TextStudio2DGpuOperation operation =
                TextStudioStyleOperation((uint)_TextStudio2DHeroStyleSlot, _TextStudio2DHeroOperationIndex);
            if (all(operation.parameters0.xy == 0))
                return 0;
            float2 offsetUv = _TextStudioHeroEffectDirection.xy;
            float2 pivotUv = _TextStudioHeroEffectDirection.zw;
            float taper = _TextStudioHeroEffectParameters0.x;
            #if UNITY_UV_STARTS_AT_TOP
                offsetUv.y = -offsetUv.y;
                pivotUv.y = 1.0 - pivotUv.y;
            #endif
            // Taper moves the contour too. Budget the complete sweep instead
            // of dropping to eight visibly separated slices as offset shrinks.
            float2 sourceRadiusPixels = max(pivotUv, 1.0 - pivotUv) * _MainTex_TexelSize.zw;
            float rayPixels = length(offsetUv * _MainTex_TexelSize.zw) +
                abs(taper) * length(sourceRadiusPixels);
            int qualityLimit = _TextStudioHeroEffectParameters0.z < .5
                ? 48
                : _TextStudioHeroEffectParameters0.z < 1.5
                    ? 72
                    : 96;
            int iterationCount = clamp(
                (int)ceil(rayPixels * 1.15),
                8,
                qualityLimit);
            half4 front = tex2D(_MainTex, input.uv);
            // Preserve the face-adjacent source slice. The style stack draws
            // Fill and Stroke above the faux depth, which produces a sealed
            // join without a subtractive antialiasing moat.
            half bestAlpha = front.a;
            half3 accumulatedColor = front.rgb;
            bool hasSurfaceAlpha = _TextStudioHeroEffectParameters0.w > .5;
            bool sampleSurfaceAlpha = _TextStudioHeroEffectParameters0.w > 1.5;
            half paintAlphaMoment = sampleSurfaceAlpha ? tex2D(_TextStudioHeroGeneratedAlpha, input.uv).r : 0;
            float2 surfaceMoment = input.uv * front.a;
            float depthMoment = 0;
            half endFade = saturate(_TextStudioHeroEffectParameters0.y);

            [loop]
            for (int iteration = 0; iteration < 96; iteration++)
            {
                if (iteration >= iterationCount || bestAlpha >= 1)
                    break;
                float t = (iteration + 1.0) / iterationCount;
                float scale = max(.05, 1.0 - taper * t);
                float2 sourceUv = pivotUv +
                    (input.uv - pivotUv - offsetUv * t) / scale;
                // The isolated target has no mip chain. Explicit LOD keeps
                // derivatives defined when an opaque ray terminates early.
                half4 sampleValue = tex2Dlod(_MainTex, float4(sourceUv, 0, 0));
                half attenuation = saturate(1.0 - operation.parameters0.w * t);
                if (endFade > .0001)
                {
                    attenuation *= 1 - smoothstep(
                        max(0, 1.0 - endFade),
                        1.0,
                        t);
                }
                // Each farther slice contributes only coverage not supplied by
                // nearer slices. Weighting progress by that increment avoids
                // threshold jumps at concave or merely grazing intersections.
                half contribution = max(0, sampleValue.a * attenuation - bestAlpha);
                bestAlpha += contribution;
                depthMoment += contribution * t;
                surfaceMoment += contribution * sourceUv;
                accumulatedColor += sampleValue.rgb *
                    (contribution / max(sampleValue.a, .00001));
                if (sampleSurfaceAlpha)
                    paintAlphaMoment += tex2Dlod(_TextStudioHeroGeneratedAlpha, float4(sourceUv, 0, 0)).r *
                        (contribution / max(sampleValue.a, .00001));
            }

            half coverage = bestAlpha;
            if (coverage <= .00001)
                return 0;
            float inverseCoverage = rcp(coverage);
            float bestT = saturate(depthMoment * inverseCoverage);
            float2 bestUv = surfaceMoment * inverseCoverage;
            half3 sourceColor = accumulatedColor * inverseCoverage;

            half3 straightColor;
            half paintAlpha = 1;
            if (operation.parameters1.z > .5)
            {
                float2 paintLocal = float2(.5, 1.0 - bestT);
                fixed4 paint = TextStudioEvaluateStylePaint(
                    _TextStudio2DHeroStyleSlot,
                    _TextStudio2DHeroOperationIndex,
                    0,
                    operation,
                    paintLocal,
                    paintLocal,
                    paintLocal,
                    paintLocal,
                    paintLocal,
                    bestT);
                straightColor = paint.rgb * sourceColor;
                paintAlpha = paint.a;
            }
            else
            {
                straightColor = sourceColor;
                if (hasSurfaceAlpha) paintAlpha = sampleSurfaceAlpha ? paintAlphaMoment * inverseCoverage : _TextStudioHeroGeneratedPaintAlpha;
            }

            float2 texel = _MainTex_TexelSize.xy;
            half2 alphaGradient = half2(
                TextStudioHeroDepthSample(bestUv + float2(texel.x, 0)) -
                TextStudioHeroDepthSample(bestUv - float2(texel.x, 0)),
                TextStudioHeroDepthSample(bestUv + float2(0, texel.y)) -
                TextStudioHeroDepthSample(bestUv - float2(0, texel.y)));
            half gradientLength = length(alphaGradient);
            half2 normal = gradientLength > .0001
                ? -alphaGradient / gradientLength
                : half2(0, 1);
            half radians = operation.parameters3.x * .0174532925199433;
            half2 lightDirection = half2(cos(radians), sin(radians));
            half lambert = saturate(dot(normal, lightDirection) * .5 + .5);
            half lighting = lerp(
                1,
                lerp(.62, 1.28, lambert),
                saturate(operation.parameters3.y));
            half nearFace = 1 - smoothstep(
                0,
                max(.0001, operation.parameters3.z),
                bestT);
            lighting += nearFace *
                saturate(operation.parameters3.w) * .42;
            lighting *= 1 - smoothstep(.55, 1, bestT) *
                saturate(operation.parameters2.w) * .62;
            coverage *= saturate(rayPixels);
            if (_TextStudio2DGeometryCoveragePass > .5) return coverage;
            half4 adjusted = half4(straightColor * max(0, lighting), paintAlpha);
            if (operation.parameters1.z > .5 || hasSurfaceAlpha)
                adjusted = TextStudioApplyColorAdjustmentsForSelection(_TextStudio2DHeroStyleSlot,
                    _TextStudio2DHeroOperationIndex, _TextStudioHeroAdjustmentSelection, adjusted);
            coverage *= saturate(adjusted.a);
            return half4(
                adjusted.rgb * coverage,
                coverage);
        }
        ENDCG

        Pass
        {
            Name "WARP"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroWarp
            ENDCG
        }

        Pass
        {
            Name "SPATIAL_OR_HISTORY"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroSpatial
            ENDCG
        }

        Pass
        {
            Name "BLUR_HORIZONTAL"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroBlurHorizontal
            ENDCG
        }

        Pass
        {
            Name "BLUR_VERTICAL"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroBlurVertical
            ENDCG
        }

        Pass
        {
            Name "LENS_MAGNIFIER"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroLensMagnifier
            ENDCG
        }

        Pass
        {
            Name "TEXT_WIDE_BLUR_HORIZONTAL"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroTextWideBlurHorizontal
            ENDCG
        }

        Pass
        {
            Name "TEXT_WIDE_BLUR_VERTICAL"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroTextWideBlurVertical
            ENDCG
        }

        Pass
        {
            Name "COLOR_FILTER"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroColorAdjustment
            ENDCG
        }

        Pass
        {
            Name "FAUX_DEPTH"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroFauxDepth
            ENDCG
        }

        Pass
        {
            Name "TEXT_WIDE_BLUR_COVERAGE_VERTICAL"
            Blend One Zero
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex vert_img
            #pragma fragment TextStudioHeroTextWideBlurCoverageVertical
            ENDCG
        }
    }
}
