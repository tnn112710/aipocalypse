Shader "Text Studio/2D Field Uniform"
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
        // The engine field array and glyph table of the engine atlas variant, bound per material by
        // TextStudio2DExternalGlyphSource. Declared so Material.GetTexture reports them; drawing binds them either way.
        [HideInInspector] _TextStudioPreparedFields ("Text Studio Engine Field Atlas", 2DArray) = "" {}
        [HideInInspector] _TextStudioExternalGlyphTable ("Text Studio Engine Glyph Table", 2D) = "black" {}
        // Packed rows of TextStudio2DUniformFieldProgram. Vectors, not colors: the packed colors are already linear.
        [HideInInspector] _TextStudioUniformBits ("Text Studio Uniform Operation Bits", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioUniformRanks ("Text Studio Uniform Stack Ranks", Vector) = (0,1,2,0)
        [HideInInspector] _TextStudioUniformOpacity ("Text Studio Uniform Opacity", Vector) = (1,1,1,0)
        [HideInInspector] _TextStudioUniformTint ("Text Studio Uniform Animated Tint", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioUniformFillColor ("Text Studio Uniform Fill Color", Vector) = (1,1,1,1)
        [HideInInspector] _TextStudioUniformFillParameters ("Text Studio Uniform Fill Parameters", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioUniformStrokeColor ("Text Studio Uniform Stroke Color", Vector) = (0,0,0,1)
        [HideInInspector] _TextStudioUniformStrokeParameters0 ("Text Studio Uniform Stroke Parameters 0", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioUniformStrokeParameters1 ("Text Studio Uniform Stroke Parameters 1", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioUniformShadowColor ("Text Studio Uniform Shadow Color", Vector) = (0,0,0,1)
        [HideInInspector] _TextStudioUniformShadowParameters0 ("Text Studio Uniform Shadow Parameters 0", Vector) = (0,0,0,0)
        [HideInInspector] _TextStudioUniformShadowParameters1 ("Text Studio Uniform Shadow Parameters 1", Vector) = (0,0,0,0)
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
            Name "TEXTSTUDIO_FIELD_UNIFORM"
            CGPROGRAM
            // One program without style tables or a data transport: a TMP-expressible style's fill, stroke and outer shadow
            // rows come from material constants (TextStudio2DShaderTiers.TrySelectUniformProgram). Every other style keeps
            // the Field program, whose coverage math this program shares.
            #pragma target 3.5
            #pragma vertex TextStudioUniformFieldVertex
            #pragma fragment TextStudioUniformFieldFragment
            #pragma dynamic_branch UNITY_UI_CLIP_RECT
            #pragma dynamic_branch UNITY_UI_ALPHACLIP
            // Glyphs a text engine publishes with its own single-channel atlas (TextStudio2DExternalGlyphSource) read that
            // atlas instead of the TMP atlas; TextStudio2DExternalGlyphSource.ResolveUniformMaterial enables this variant.
            #pragma multi_compile_local __ TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
            // Selected only for source materials without TMP outline or underlay.
            #define OUTLINE_ON 0
            #define UNDERLAY_ON 0
            #define UNDERLAY_INNER 0
            #define TEXT_STUDIO_UNIFORM_FIELD_PROGRAM 1
            #include "TextStudio2DProductionSDFCommon.cginc"
            #include "TextStudio2DFieldCoverageCommon.cginc"

            // Only the Field interpolants this program reads: 5 fragment inputs, 7 for engine glyphs, instead of the Field
            // program's 13.
            struct TextStudioUniformFieldPixel
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex : SV_POSITION;
                fixed4 faceColor : COLOR;
                // xy: atlas coordinates; zw: glyph-local coordinates.
                float4 atlasAndLocal : TEXCOORD0;
                half4 param : TEXCOORD1;
                // xy: Canvas clip-rect position; z: the Full TMP bias term (pixel_t.texcoord1.w).
                float4 clipAndBias : TEXCOORD2;
                // Exact style identity; its operation mask selects the rows this primitive draws.
                nointerpolation float identity : TEXCOORD3;
                #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
                // The engine glyph's resolved field row (TextStudioResolveExternalField), constant across its primitive:
                // its uniform atlas scale, offset and page plus one in fieldTransform, its atlas bounds in fieldBounds.
                nointerpolation float4 fieldTransform : TEXCOORD4;
                nointerpolation float4 fieldBounds : TEXCOORD5;
                #endif
            };

            // The Field vertex program computes every value, so both programs see identical interpolants.
            TextStudioUniformFieldPixel TextStudioUniformFieldVertex(vertex_t input)
            {
                pixel_t field = VertShader(input);
                TextStudioUniformFieldPixel output;
                UNITY_INITIALIZE_OUTPUT(TextStudioUniformFieldPixel, output);
                UNITY_TRANSFER_INSTANCE_ID(field, output);
                UNITY_TRANSFER_VERTEX_OUTPUT_STEREO(field, output);
                output.vertex = field.vertex;
                output.faceColor = field.faceColor;
                output.atlasAndLocal = float4(field.texcoord0.xy, field.textStudioLocal.xy);
                output.param = field.param;
                output.clipAndBias = float4(field.mask.zw, field.texcoord1.w, 0);
                output.identity = field.textStudioWritingAndSquiggle.w;
                #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
                // An engine glyph's quad carries its mapping row (TextStudio2DOrderedRenderer.EmitExternal): uv2 the row
                // transform and uv3 the em rectangle the engine stores field data for, the values
                // TextStudio2DExternalGlyphSource.TryMap publishes to the mapping table the Field program reads. Every
                // engine row shares one decode, and the atlas transform is read here as in the Field vertex stage.
                TextStudioPreparedField row;
                row.transform = input.textStudio0;
                row.bounds = input.textStudio1;
                row.decode = float4(-1, 1, _TextStudioFieldPixelsPerEm, 0);
                row = TextStudioResolveExternalField(row, TextStudioLoadExternalFieldTransform(row));
                // An engine row scales both axes alike, and only its page varies in the decode.
                output.fieldTransform = float4(row.transform.xzw, row.decode.x);
                output.fieldBounds = row.bounds;
                #endif
                return output;
            }

            // TextStudioStyleStrokeCoverage of one distance for a stroke without softness: the soft amount resolves to zero, so
            // both edges take TextStudioStyleSoftCoverage's hard branch. Keep the arithmetic identical to that function.
            half TextStudioUniformHardStrokeCoverage(half distance, half bias, half exteriorBand, half fieldScale, half width, half alignment,
                half units)
            {
                half amount = TextStudioStyleResolveFieldAmount(width, units, fieldScale);
                half outsideFraction = saturate((alignment + 1.0) * .5);
                half outsideAmount = amount * outsideFraction;
                half insideAmount = amount - outsideAmount;
                outsideAmount = min(outsideAmount, max(0, exteriorBand));
                half signedDistance = distance - bias;
                half outer = saturate(signedDistance + outsideAmount);
                half inner = saturate(signedDistance - insideAmount);
                return saturate(saturate(outer - inner));
            }

            // One solid row: TextStudioFinalizeCoverageOperation and the end of TextStudioEvaluateStyleOperation for a solid
            // paint, which has no paint lighting, and a program without color adjustments.
            fixed4 TextStudioUniformFieldRow(fixed4 paint, half coverage, float opacity, float tint, fixed4 animatedTint)
            {
                half sourceAlpha = saturate(animatedTint.a);
                UNITY_BRANCH
                if (tint > .5)
                {
                    half inverseAlpha = sourceAlpha > .0001
                        ? rcp(sourceAlpha)
                        : 0;
                    paint.rgb *= animatedTint.rgb * inverseAlpha;
                }
                half contributionCoverage = coverage * opacity * sourceAlpha;
                if (contributionCoverage <= 0) return 0;
                half alpha = saturate(paint.a * contributionCoverage);
                return float4(paint.rgb * alpha, alpha);
            }

            // The glyph-local displacement of an atlas displacement: atlas and local coordinates are affine across a primitive, so
            // the screen step that moves the atlas coordinate by atlasDelta moves the local coordinate by the returned amount.
            // valid is false where the atlas derivatives cannot be inverted.
            float2 TextStudioUniformLocalFromAtlasDelta(float2 atlasDelta, TextStudioStyleDerivatives derivatives, out bool valid)
            {
                float determinant = derivatives.atlasDx.x * derivatives.atlasDy.y - derivatives.atlasDy.x * derivatives.atlasDx.y;
                valid = abs(determinant) >= .0000000001;
                float2 screen = TextStudioScreenDeltaFromLocal(atlasDelta, derivatives.atlasDx, derivatives.atlasDy, valid ? determinant : 1);
                return derivatives.localDx * screen.x + derivatives.localDy * screen.y;
            }

            // The atlas offset TextStudioStyleSampleShiftedDistance applies to an outer shadow's shifted sample.
            float2 TextStudioUniformShadowAtlasOffset(TextStudioStyleDerivatives derivatives)
            {
                float2 normalizedOffset = _TextStudioUniformShadowParameters0.xy;
                return _TextStudioUniformShadowParameters1.y > .5
                    ? -(derivatives.atlasDx * normalizedOffset.x +
                        derivatives.atlasDy * normalizedOffset.y) * TextStudioStyleOutputPixelScale()
                    : float2(
                        -normalizedOffset.x * max(1, _TextStudioFieldPixelsPerEm) / max(1.0, _TextureWidth),
                        -normalizedOffset.y * max(1, _TextStudioFieldPixelsPerEm) / max(1.0, _TextureHeight));
            }

            // TextStudioSampleContinuedDistance of the TMP atlas with identical arithmetic, for the face sample and an outer shadow's
            // shifted sample. A sample point inside its glyph's local 0..1 span skips the bounds terms, which cannot change the result
            // there: native quads and the 0..1 span of extended primitives map local coordinates affinely onto the glyph's padded
            // atlas rectangle, so the bounds rebuilt from derivatives contain the point by the safe padding less the half-texel
            // inset; the outside distance is zero, and a field inside the glyph measures at most its distance to the rectangle,
            // which the rectangle distance exceeds by that margin. The caller requires a margin of two texels and the derivative rank
            // the reconstruction needs. Both cases share one fetch, so a driver that flattens the branch still samples once.
            half TextStudioUniformContinuedDistance(float2 sampleUv, float2 sampleDx, float2 sampleDy, half fieldScale, half threshold,
                float2 lower, float2 upper, bool onGlyphQuad)
            {
                float2 textureSize = max(float2(_TextureWidth, _TextureHeight), 1.0);
                float2 safeUv = clamp(sampleUv, lower, upper);
                half distance = tex2Dgrad(
                    _MainTex,
                    safeUv,
                    sampleDx,
                    sampleDy).a * fieldScale;
                half gradientScale = max(2 * _GradientScale, 1.0);
                half safeFieldScale = max(abs(fieldScale), .0001);
                half signedDistancePixels =
                    (distance - threshold) * gradientScale / safeFieldScale;
                half continuedDistancePixels = signedDistancePixels;
                UNITY_BRANCH
                if (!onGlyphQuad)
                {
                    float2 outside = max(lower - sampleUv, 0) +
                                     max(sampleUv - upper, 0);
                    float outsidePixels = length(outside * textureSize);
                    float2 insideFromLower = (sampleUv - lower) * textureSize;
                    float2 insideFromUpper = (upper - sampleUv) * textureSize;
                    float insidePixels = min(
                        min(insideFromLower.x, insideFromLower.y),
                        min(insideFromUpper.x, insideFromUpper.y));
                    half rectangleDistancePixels = outsidePixels > .00001
                        ? -outsidePixels
                        : insidePixels;
                    continuedDistancePixels = min(
                        signedDistancePixels - outsidePixels,
                        rectangleDistancePixels);
                }
                return threshold +
                       continuedDistancePixels * safeFieldScale / gradientScale;
            }

            fixed4 TextStudioUniformFieldFragment(TextStudioUniformFieldPixel uniformInput) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(uniformInput);
                // The shared coverage helpers read Field interpolants; this program carries the ones they use.
                pixel_t input = (pixel_t)0;
                input.faceColor = uniformInput.faceColor;
                input.texcoord0 = float4(uniformInput.atlasAndLocal.xy, 0, 0);
                input.param = uniformInput.param;
                input.mask = float4(0, 0, uniformInput.clipAndBias.xy);
                input.texcoord1 = float4(0, 0, 0, uniformInput.clipAndBias.z);
                #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
                // The row TextStudioResolveExternalField produces: equal axis scales, the engine's unit range, and the
                // single-channel bank flag, the only bank this variant serves.
                input.preparedTransform = uniformInput.fieldTransform.xxyz;
                input.preparedBounds = uniformInput.fieldBounds;
                input.preparedDecode = float4(uniformInput.fieldTransform.w, 1, _TextStudioFieldPixelsPerEm, -1);
                #endif
                half payloadValid = saturate(_TextStudioPayloadEnabled);
                float4 textStudioLocal = float4(uniformInput.atlasAndLocal.zw, 0, 0) * payloadValid;
                input.textStudioLocal = textStudioLocal;
                uint styleSlot;
                uint operationMask;
                int spanBase;
                TextStudioDecodeStyleIdentity(uniformInput.identity * payloadValid, styleSlot, operationMask, spanBase);
                UNITY_BRANCH
                if (_TextStudioNativeMaterialModel > 1.5)
                {
                    // Full TMP's coverage in the canonical scaled-distance form, as in PixShader. Multiplication must
                    // follow interpolation: lerp(1 / scale) * lerp(scale) is not 1 in perspective.
                    float nativeScale = input.param.w;
                    float nativeSoftness = _OutlineSoftness * _ScaleRatioA * nativeScale;
                    float nativeSoftnessRatio = 1 + nativeSoftness;
                    float nativeFieldScale = nativeScale / nativeSoftnessRatio;
                    float nativeBias = (input.texcoord1.w * nativeScale - 1 - .5 * nativeSoftness) / nativeSoftnessRatio;
                    float nativeOutline = _OutlineWidth * _ScaleRatioA * .5 * nativeFieldScale;
                    input.param = half4(nativeFieldScale, nativeBias - nativeOutline, nativeBias + nativeOutline, nativeSoftnessRatio);
                }
                half sdfBias = (input.param.y + input.param.z) * .5;
                // Direct primitives can extend beyond the padded source quad; the field continues past the glyph bounds.
                TextStudioStyleDerivatives derivatives = TextStudioBuildStyleDerivatives(input, textStudioLocal.xy);
                #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
                float2 lower;
                float2 upper;
                TextStudioContinuedAtlasBounds(input, derivatives, lower, upper);
                half distance = TextStudioSampleContinuedDistance(
                    input,
                    input.texcoord0.xy,
                    derivatives.atlasDx,
                    derivatives.atlasDy,
                    input.param.x,
                    sdfBias,
                    lower,
                    upper);
                #else
                // A pixel inside its glyph's local 0..1 span samples without glyph bounds (TextStudioUniformContinuedDistance), and so
                // does an outer shadow's shifted sample point inside that span; only samples outside it resolve the bounds. The
                // determinant test is the reconstruction's own.
                bool glyphQuadSamples = payloadValid > .5 && TextStudioSafeAtlasPaddingPixels() >= 2.5 &&
                    abs(derivatives.localDx.x * derivatives.localDy.y - derivatives.localDy.x * derivatives.localDx.y) >= .0000001;
                bool onGlyphQuad = glyphQuadSamples && all(textStudioLocal.xy >= 0) && all(textStudioLocal.xy <= 1);
                bool shadowSelected = (operationMask & (uint)_TextStudioUniformBits.z) != 0u;
                bool shadowOnGlyphQuad = false;
                UNITY_BRANCH
                if (shadowSelected)
                {
                    bool shadowInvertible;
                    float2 shadowLocal = textStudioLocal.xy + TextStudioUniformLocalFromAtlasDelta(
                        TextStudioUniformShadowAtlasOffset(derivatives), derivatives, shadowInvertible);
                    shadowOnGlyphQuad = glyphQuadSamples && shadowInvertible && all(shadowLocal >= 0) && all(shadowLocal <= 1);
                }
                // Unresolved bounds contain every atlas coordinate, so clamping a glyph-quad sample to them changes nothing.
                float2 lower = -16384;
                float2 upper = 16384;
                UNITY_BRANCH
                if (!onGlyphQuad || shadowSelected && !shadowOnGlyphQuad)
                    TextStudioContinuedAtlasBounds(input, derivatives, lower, upper);
                half distance = TextStudioUniformContinuedDistance(
                    input.texcoord0.xy,
                    derivatives.atlasDx,
                    derivatives.atlasDy,
                    input.param.x,
                    sdfBias,
                    lower,
                    upper,
                    onGlyphQuad);
                #endif
                half4 distances = distance.xxxx;
                #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
                // The Field program reads an untreated fill and the native face of a prepared row from the median of its
                // channels: TextStudioSampleSharpDistance at the coordinate TextStudioClampAtlasToGlyph leaves unclamped
                // for prepared rows. A single-channel engine row repeats one distance, but its rounding differs from the
                // alpha distance on half-precision GPUs, so the same expression is evaluated here.
                half4 sharpSample = TextStudioSamplePreparedField(TextStudioPreparedForPixel(input), input.texcoord0.xy);
                half sharpDistance = TextStudioMedianDistance(sharpSample.rgb) * input.param.x;
                #else
                half sharpDistance = distance;
                #endif
                // Native fields and engine rows have no extended prepared range, so every row measures from the face bias.
                half4 fieldParameters = half4(input.param.x, sdfBias, input.param.z, sdfBias);
                // White face and outline textures leave the vertex face color unchanged.
                fixed4 faceColor = input.faceColor;

                fixed4 fill = 0;
                fixed4 stroke = 0;
                fixed4 shadow = 0;
                UNITY_BRANCH
                if ((operationMask & (uint)_TextStudioUniformBits.x) != 0u)
                {
                    #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
                    // As TextStudioEvaluateStyleOperation selects the fill distances of a row without dilation or softness.
                    half4 fillDistances = abs(_TextStudioUniformFillParameters.x) < .0001 && _TextStudioUniformFillParameters.y < .0001
                        ? sharpDistance.xxxx
                        : distances;
                    #else
                    half4 fillDistances = distances;
                    #endif
                    // Without dilation or softness the treated coverage reduces to the hard edge; a uniform branch skips the rest.
                    half fillCoverage;
                    UNITY_BRANCH
                    if (_TextStudioUniformFillParameters.x == 0 && _TextStudioUniformFillParameters.y <= 0)
                        fillCoverage = saturate(fillDistances.x - fieldParameters.w);
                    else
                        fillCoverage = TextStudioStyleTreatedFillCoverage(fillDistances, fieldParameters, _TextStudioUniformFillParameters, 0);
                    fill = TextStudioUniformFieldRow(
                        _TextStudioUniformFillColor,
                        fillCoverage,
                        _TextStudioUniformOpacity.x,
                        _TextStudioUniformTint.x,
                        faceColor);
                }
                UNITY_BRANCH
                if ((operationMask & (uint)_TextStudioUniformBits.y) != 0u)
                {
                    half strokeCoverage;
                    UNITY_BRANCH
                    if (_TextStudioUniformStrokeParameters0.z <= 0)
                        strokeCoverage = TextStudioUniformHardStrokeCoverage(
                            distances.x,
                            fieldParameters.w,
                            fieldParameters.y,
                            fieldParameters.x,
                            _TextStudioUniformStrokeParameters0.x,
                            _TextStudioUniformStrokeParameters0.y,
                            _TextStudioUniformStrokeParameters1.w);
                    else
                        strokeCoverage = TextStudioStyleStrokeCoverage(
                            distances,
                            fieldParameters,
                            _TextStudioUniformStrokeParameters0.x,
                            _TextStudioUniformStrokeParameters0.y,
                            _TextStudioUniformStrokeParameters0.z,
                            _TextStudioUniformStrokeParameters1.w,
                            0);
                    stroke = TextStudioUniformFieldRow(
                        _TextStudioUniformStrokeColor,
                        strokeCoverage,
                        _TextStudioUniformOpacity.y,
                        _TextStudioUniformTint.y,
                        faceColor);
                }
                UNITY_BRANCH
                if ((operationMask & (uint)_TextStudioUniformBits.z) != 0u)
                {
                    // The outer shadow of TextStudioStyleEffectCoverage: a softened shifted field kept outside the face.
                    half face = TextStudioStyleFillCoverage(distances, fieldParameters.w, 0);
                    #ifdef TEXT_STUDIO_UNIFORM_EXTERNAL_FIELD
                    half shiftedDistance = TextStudioStyleSampleShiftedDistance(
                        input,
                        _TextStudioUniformShadowParameters0.xy,
                        fieldParameters.x,
                        fieldParameters.w,
                        _TextStudioUniformShadowParameters1.y,
                        derivatives,
                        lower,
                        upper);
                    #else
                    half shiftedDistance = TextStudioUniformContinuedDistance(
                        input.texcoord0.xy + TextStudioUniformShadowAtlasOffset(derivatives),
                        derivatives.atlasDx,
                        derivatives.atlasDy,
                        fieldParameters.x,
                        fieldParameters.w,
                        lower,
                        upper,
                        shadowOnGlyphQuad);
                    #endif
                    half shifted = TextStudioStyleSoftCoverage(
                        shiftedDistance.xxxx,
                        fieldParameters,
                        _TextStudioUniformShadowParameters0.w,
                        _TextStudioUniformShadowParameters0.z,
                        1.0,
                        _TextStudioUniformShadowParameters1.y,
                        0);
                    shadow = TextStudioUniformFieldRow(
                        _TextStudioUniformShadowColor,
                        saturate(shifted - face),
                        _TextStudioUniformOpacity.z,
                        _TextStudioUniformTint.z,
                        faceColor);
                }

                uint rows = (uint)_TextStudioUniformBits.x | (uint)_TextStudioUniformBits.y | (uint)_TextStudioUniformBits.z;
                uint selectedRows = operationMask & rows;
                fixed4 c;
                UNITY_BRANCH
                if ((selectedRows & (selectedRows - 1u)) != 0u)
                {
                    // Stack rank zero is the topmost row; like TextStudioEvaluateStyleProgram, later rows compose below it.
                    // Rows this primitive does not select are transparent and leave the composition unchanged.
                    fixed4 top = _TextStudioUniformRanks.x < .5 ? fill : _TextStudioUniformRanks.y < .5 ? stroke : shadow;
                    fixed4 middle = abs(_TextStudioUniformRanks.x - 1) < .5 ? fill : abs(_TextStudioUniformRanks.y - 1) < .5 ? stroke : shadow;
                    fixed4 bottom = _TextStudioUniformRanks.x > 1.5 ? fill : _TextStudioUniformRanks.y > 1.5 ? stroke : shadow;
                    c = top;
                    c = c + middle * (1 - c.a);
                    c = c + bottom * (1 - c.a);
                }
                else
                {
                    // One selected row composes with transparent rows into itself, so a single-row primitive returns it.
                    c = fill + stroke + shadow;
                    // Without a selected row the Field program keeps the native face, for example while a text has no style slot.
                    UNITY_BRANCH
                    if (selectedRows == 0u)
                        c = faceColor * TextStudioFaceAlpha(sharpDistance, sdfBias, 0);
                }

                UNITY_BRANCH
                if (UNITY_UI_CLIP_RECT)
                {
                    const half2 maskSoftness = half2(
                        max(_UIMaskSoftnessX, _MaskSoftnessX),
                        max(_UIMaskSoftnessY, _MaskSoftnessY));
                    float2 localPixelSize = max(.00001, fwidth(input.mask.zw) * .5);
                    half2 maskScale = .25 / (.25 * maskSoftness + localPixelSize);
                    half2 m = saturate(
                        (_ClipRect.zw - _ClipRect.xy - abs(input.mask.zw)) *
                        maskScale);
                    c *= m.x * m.y;
                }
                UNITY_BRANCH
                if (UNITY_UI_ALPHACLIP)
                    clip(c.a - .001);
                return c;
            }
            ENDCG
        }
    }
    CustomEditor "TMPro.EditorUtilities.TMP_SDFShaderGUI"
}
