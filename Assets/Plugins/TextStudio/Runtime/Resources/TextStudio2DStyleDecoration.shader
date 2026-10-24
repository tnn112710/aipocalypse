// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Text Studio/2D Style Decoration"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _TextureWidth ("Texture Width", Float) = 512
        _TextureHeight ("Texture Height", Float) = 512
        _GradientScale ("Gradient Scale", Float) = 5
        _WeightNormal ("Weight Normal", Float) = 0
        _WeightBold ("Weight Bold", Float) = .5
        _FaceDilate ("Face Dilate", Range(-1,1)) = 0
        _ScaleRatioA ("Scale Ratio A", Float) = 1
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
        [HideInInspector] _UIVertexColorAlwaysGammaSpace ("UI Vertex Color Always Gamma Space", Float) = 0
        // Declared because a shader reimport or assignment rebuilds material values from this block and drops undeclared ones.
        [HideInInspector] _TextStudio2DDecorationHero ("Text Studio 2D Decoration Hero", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
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

        CGINCLUDE
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            sampler2D _MainTex;
            float _TextureWidth;
            float _TextureHeight;
            float _GradientScale;
            float _WeightNormal;
            float _WeightBold;
            float _FaceDilate;
            float _ScaleRatioA;

            // Set on the compositor's decoration material. Hero coverage replays draw one selected operation of one
            // glyph into a render target whose pixels differ from the final output.
            float _TextStudio2DDecorationHero;
            int _TextStudio2DHeroOperationIndex;
            int _TextStudio2DHeroGlyphFilter;
            float4 _TextStudio2DSurfacePixelUnit;
            // Flattened treatments one surface evaluates; the shifted-shadow samples are held per treatment.
            #define TEXT_STUDIO_MAX_SURFACE_TREATMENTS 64
            #include "TextStudio2DStylePaintCommon.cginc"
            #include "TextStudioPreparedFieldCommon.cginc"
            #include "TextStudio2DBlendCommon.cginc"
            #include "TextStudio2DMaterialLightingCommon.cginc"
            #include "TextStudio2DAdaptiveDepthCommon.cginc"
            #include "TextStudio2DSurfaceCommon.cginc"
            #include "TextStudioWritingSourceCoverage.cginc"

            float4 _ClipRect;
            int _UIVertexColorAlwaysGammaSpace;
            float4 _TextStudio2DCameraPixels;

            struct TextStudioDecorationVertexInput
            {
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };

            struct TextStudioDecorationPixelInput
            {
                UNITY_VERTEX_OUTPUT_STEREO
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float4 shapeAndGlyph : TEXCOORD0;
                float4 textAndProgram : TEXCOORD1;
                float4 geometry : TEXCOORD2;
                float4 atlasRect : TEXCOORD3;
                float2 localPosition : TEXCOORD4;
                float2 worldPosition : TEXCOORD5;
                float4 screenPosition : TEXCOORD6;
                // Dash, dot and wave coordinate of a line. It is affine within every tile, so the vertex stage reads its
                // mapping rows once instead of every pixel reading them.
                float lineCoordinate : TEXCOORD7;
            #if defined(TEXT_STUDIO_DECORATION_LINE_SOLID)
                // Constant per tile: x the operation record base (negative when the style row is unavailable), y the first
                // edge of a static skip-ink mask, z its edge count (-1 without a mask, -2 when the mask is not current) and
                // w the count of its leading band edges.
                nointerpolation float4 lineRecord : TEXCOORD8;
                // Vertical range, in the mask's local space, that every edge after the band edges stays outside.
                nointerpolation float2 lineBand : TEXCOORD9;
            #endif
            };

            float2 TextStudioLinePixelUnits(float4 localCenter)
            {
                if (_TextStudio2DDecorationHero > .5)
                    return max(_TextStudio2DSurfacePixelUnit.xy, .000001);
                float4 clipCenter = UnityObjectToClipPos(localCenter);
                // Preserve direction w=0; UnityObjectToClipPos always treats its input as a position.
                float4 clipX = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(1, 0, 0, 0)));
                float4 clipY = mul(UNITY_MATRIX_VP, mul(unity_ObjectToWorld, float4(0, 1, 0, 0)));
                float denominator = max(.000001, clipCenter.w * clipCenter.w);
                float2 outputPixels = _TextStudio2DCameraPixels.w > .5 ? _TextStudio2DCameraPixels.xy : _ScreenParams.xy;
                float2 pixelsX = (clipX.xy * clipCenter.w - clipCenter.xy * clipX.w) / denominator * outputPixels * .5;
                float2 pixelsY = (clipY.xy * clipCenter.w - clipCenter.xy * clipY.w) / denominator * outputPixels * .5;
                return 1.0 / max(float2(length(pixelsX), length(pixelsY)), .000001);
            }

            void TextStudioResolvePixelLine(inout TextStudioDecorationVertexInput input)
            {
                uint slot = (uint)round(input.uv1.z);
                int index = (int)round(input.uv1.w);
                if (slot == 0 || TEXT_STUDIO_STYLE_TABLE_ENABLED < .5) return;
                TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(slot);
                if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION || index < 0 || index >= header.metadata.y) return;
                TextStudio2DGpuOperation operation = TextStudioLoadOperation((int)round(header.program.x) + index);
                if (operation.layerMetadata.x != 8 || operation.parameters0.x > 2 || operation.parameters3.w != 1) return;
                float originalHeight = max(.000001, input.uv2.y);
                float originalCenter = input.vertex.y - (input.uv0.y - .5) * originalHeight;
                float4 center = float4(input.vertex.x, originalCenter, input.vertex.z, 1);
                float2 pixelUnit = TextStudioLinePixelUnits(center);
                int pattern = (int)round(operation.parameters3.x);
                float originalThickness = pattern == 1 ? max(0, (originalHeight - operation.parameters5.y) * .5) :
                    pattern == 4 ? max(0, originalHeight - operation.parameters4.w * 2) : originalHeight;
                float thickness = operation.parameters3.y < .5 ? operation.parameters0.y * pixelUnit.y : originalThickness;
                float height = pattern == 1 ? thickness * 2 + operation.parameters5.y * pixelUnit.y :
                    pattern == 4 ? thickness + operation.parameters4.w * pixelUnit.y * 2 : thickness;
                height = max(height, .000001);
                float centerY = originalCenter + operation.parameters0.z * (pixelUnit.y - 1);
                float boundedY = saturate(input.uv0.y);
                float outsideY = (input.uv0.y - boundedY) * originalHeight;
                input.vertex.y = centerY + (boundedY - .5) * height + outsideY;
                input.uv0.y = boundedY + outsideY / height;
                if (input.uv2.w < 0) input.uv3.w += centerY - originalCenter + (originalHeight - height) * .5;
                input.uv2.y = height;
            }

            void TextStudioResolvePixelExpansion(inout TextStudioDecorationVertexInput input)
            {
                uint slot = (uint)round(input.uv1.z);
                int index = (int)round(input.uv1.w);
                if (slot == 0 || TEXT_STUDIO_STYLE_TABLE_ENABLED < .5) return;
                TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(slot);
                if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION || index < 0 || index >= header.metadata.y) return;
                TextStudio2DGpuOperation owner = TextStudioLoadOperation((int)round(header.program.x) + index);
                if (owner.layerMetadata.x != 8) return;
                float2 size = max(abs(input.uv2.xy), .000001);
                float2 outside = (input.uv0.xy - saturate(input.uv0.xy)) * size;
                float2 amount = float2(outside.x < 0 ? owner.parameters11.x : owner.parameters11.z,
                    outside.y < 0 ? owner.parameters11.y : owner.parameters11.w);
                float2 delta = sign(outside) * min(abs(outside), amount) * (TextStudioLinePixelUnits(input.vertex) - 1);
                input.vertex.xy += delta;
                input.uv0.xy += delta / size;
            }

            // Every branch of TextStudioStyleLineCoordinate is affine in the text coordinate with coefficients that are
            // constant across a tile, so interpolating its vertex values reproduces the per-pixel evaluation.
            float TextStudioDecorationLineCoordinate(TextStudioDecorationVertexInput input)
            {
                uint slot = (uint)max(0, round(input.uv1.z));
                int index = max(0, (int)round(input.uv1.w));
                if (slot == 0 || TEXT_STUDIO_STYLE_TABLE_ENABLED < .5) return 0;
                TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(slot);
                if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION || index >= (int)round(header.metadata.y)) return 0;
                TextStudio2DGpuOperation owner = TextStudioLoadOperation((int)round(header.program.x) + index);
                if ((int)round(owner.layerMetadata.x) != 8 || owner.parameters0.x > 2 || owner.parameters3.x < 2) return 0;
                float packedGeometryKind = round(input.uv2.w);
                int glyphIndex = packedGeometryKind < 0 ? -(int)packedGeometryKind - 1 : max(0, (int)floor(packedGeometryKind * .5) - 1);
                return TextStudioStyleLineCoordinate(slot, index, glyphIndex, input.uv1.x, ((int)round(owner.parameters5.w) & 3) == 1);
            }

        #if defined(TEXT_STUDIO_DECORATION_LINE_SOLID)
            // The lean line program reads its operation rows and its static mask through these tile constants instead of
            // the dependent header lookups the general program repeats for every pixel.
            void TextStudioResolveLineRecords(TextStudioDecorationVertexInput input, out float4 record, out float2 band)
            {
                record = float4(-1, 0, -1, 0);
                band = 0;
                uint slot = (uint)max(0, round(input.uv1.z));
                int index = max(0, (int)round(input.uv1.w));
                if (slot == 0 || TEXT_STUDIO_STYLE_TABLE_ENABLED < .5) return;
                TextStudio2DGpuStyleHeader header = TextStudioLoadStyleHeader(slot);
                if ((int)round(header.metadata.x) != TEXT_STUDIO_STYLE_SCHEMA_VERSION || index >= (int)round(header.metadata.y)) return;
                record.x = round(header.program.x);
                float2 handle = input.uv3.xy;
                if (round(input.uv2.w) >= 0 || handle.x <= .5) return;
                // A present handle that is no longer current measures no ink, as TextStudioSurfaceDistanceAtPoint does.
                record.z = -2;
                if (_TextStudio2DSurfacesEnabled < .5) return;
                int surfaceHeader = (int)round(handle.x) * TEXT_STUDIO_SURFACE_HEADER_STRIDE;
                float4 metadata = TextStudioLoadSurfaceHeader(surfaceHeader);
                // Live Writing masks keep the general program (TextStudio2DStyleDecorations.SupportsLineProgram).
                if (abs(metadata.z - handle.y) > .5 || metadata.w >= 1.5) return;
                record.y = round(metadata.x);
                record.z = min(2048, round(metadata.y));
                record.w = record.z;
                band = float2(-1e30, 1e30);
                float4 bandRecord = TextStudioLoadSurfaceHeader(surfaceHeader + 3);
                if (bandRecord.w > .5)
                {
                    record.w = min(record.z, round(bandRecord.x));
                    band = bandRecord.yz;
                }
            }
        #endif

            TextStudioDecorationPixelInput TextStudioDecorationVertex(
                TextStudioDecorationVertexInput input)
            {
                TextStudioDecorationPixelInput output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                TextStudioResolvePixelLine(input);
                TextStudioResolvePixelExpansion(input);
                output.vertex = UnityObjectToClipPos(input.vertex);
                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                    input.color.rgb = UIGammaToLinear(input.color.rgb);
                output.color = input.color;
                output.shapeAndGlyph = input.uv0;
                output.textAndProgram = input.uv1;
                output.geometry = input.uv2;
                output.atlasRect = input.uv3;
                output.localPosition = input.vertex.xy;
                output.worldPosition = mul(unity_ObjectToWorld, input.vertex).xy;
                output.screenPosition = ComputeScreenPos(output.vertex);
                output.lineCoordinate = TextStudioDecorationLineCoordinate(input);
            #if defined(TEXT_STUDIO_DECORATION_LINE_SOLID)
                TextStudioResolveLineRecords(input, output.lineRecord, output.lineBand);
            #endif
                return output;
            }

            float TextStudioRoundedRectDistance(
                float2 shapeLocal,
                float2 size,
                float radius)
            {
                float2 halfSize = max(size * .5, .00001);
                radius = min(max(0, radius), min(halfSize.x, halfSize.y));
                float2 localPoint = (shapeLocal - .5) * size;
                float2 delta = abs(localPoint) - (halfSize - radius);
                return length(max(delta, 0)) + min(max(delta.x, delta.y), 0) - radius;
            }

            float TextStudioSmoothMaximum(float first, float second, float radius)
            {
                if (radius <= .00001)
                    return max(first, second);
                float smoothing = radius * 1.65;
                float blend = max(smoothing - abs(first - second), 0) /
                              smoothing;
                return max(first, second) +
                       blend * blend * smoothing * .25;
            }

            float TextStudioPlateEndDistance(
                float inward,
                float vertical,
                float halfHeight,
                float radius,
                float capSize,
                int endStyle)
            {
                float absoluteVertical = abs(vertical);
                if (endStyle == 0)
                {
                    radius = min(max(0, radius), halfHeight);
                    if (radius <= .00001 ||
                        absoluteVertical <= halfHeight - radius)
                    {
                        return -inward;
                    }
                    return length(float2(
                        inward - radius,
                        absoluteVertical - (halfHeight - radius))) - radius;
                }
                if (endStyle == 1)
                    return -inward;
                if (endStyle == 2)
                {
                    float circleDistance = length(float2(
                        inward - halfHeight,
                        vertical)) - halfHeight;
                    float bodyDistance = max(
                        halfHeight - inward,
                        absoluteVertical - halfHeight);
                    return min(circleDistance, bodyDistance);
                }

                float resolvedCap = capSize > .00001
                    ? min(capSize, halfHeight)
                    : halfHeight * .5;
                if (endStyle == 3)
                {
                    float upper =
                        (vertical - (halfHeight - resolvedCap) - inward) *
                        .70710678;
                    float lower =
                        (-vertical - (halfHeight - resolvedCap) - inward) *
                        .70710678;
                    float diagonal = TextStudioSmoothMaximum(
                        upper,
                        lower,
                        radius);
                    return TextStudioSmoothMaximum(
                        -inward,
                        diagonal,
                        radius);
                }

                float denominator = max(
                    .00001,
                    length(float2(resolvedCap, halfHeight)));
                if (endStyle == 4)
                {
                    float upper =
                        (vertical * resolvedCap - inward * halfHeight) /
                        denominator;
                    float lower =
                        (-vertical * resolvedCap - inward * halfHeight) /
                        denominator;
                    return TextStudioSmoothMaximum(
                        upper,
                        lower,
                        radius);
                }
                return (resolvedCap * (halfHeight - absoluteVertical) -
                        inward * halfHeight) / denominator;
            }

            float TextStudioPlateDistance(
                float2 shapeLocal,
                float2 size,
                float radius,
                float capSize,
                int leftEnd,
                int rightEnd)
            {
                if (leftEnd == 0 && rightEnd == 0)
                    return TextStudioRoundedRectDistance(shapeLocal, size, radius);

                float2 platePoint = shapeLocal * size;
                float halfHeight = max(size.y * .5, .00001);
                float vertical = platePoint.y - halfHeight;
                float verticalDistance = abs(vertical) - halfHeight;
                float leftDistance = TextStudioPlateEndDistance(
                    platePoint.x,
                    vertical,
                    halfHeight,
                    radius,
                    capSize,
                    leftEnd);
                float rightDistance = TextStudioPlateEndDistance(
                    size.x - platePoint.x,
                    vertical,
                    halfHeight,
                    radius,
                    capSize,
                    rightEnd);
                float leftPlateDistance = leftEnd == 0 || leftEnd == 1
                    ? max(verticalDistance, leftDistance)
                    : TextStudioSmoothMaximum(
                        verticalDistance,
                        leftDistance,
                        radius);
                float rightPlateDistance = rightEnd == 0 || rightEnd == 1
                    ? max(verticalDistance, rightDistance)
                    : TextStudioSmoothMaximum(
                        verticalDistance,
                        rightDistance,
                        radius);
                return max(leftPlateDistance, rightPlateDistance);
            }

            float TextStudioPerCornerRoundedRectDistance(
                float2 shapeLocal,
                float2 size,
                float4 radii)
            {
                float2 halfSize = max(size * .5, .00001);
                float2 localPoint = (shapeLocal - .5) * size;
                // Corner order is top-left, top-right, bottom-right, bottom-left.
                float radius = localPoint.y >= 0
                    ? (localPoint.x < 0 ? radii.x : radii.y)
                    : (localPoint.x >= 0 ? radii.z : radii.w);
                radius = clamp(radius, 0, min(halfSize.x, halfSize.y));
                float2 delta = abs(localPoint) - (halfSize - radius);
                return length(max(delta, 0)) +
                       min(max(delta.x, delta.y), 0) - radius;
            }

            float TextStudioSquircleDistance(
                float2 shapeLocal,
                float2 size,
                float power)
            {
                float2 halfSize = max(size * .5, .00001);
                float2 normalized = abs((shapeLocal - .5) * size) / halfSize;
                power = clamp(power, 2, 16);
                float contour = pow(
                    pow(normalized.x, power) + pow(normalized.y, power),
                    1.0 / power) - 1.0;
                return contour * min(halfSize.x, halfSize.y);
            }

            float TextStudioEllipseDistance(float2 shapeLocal, float2 size)
            {
                float2 halfSize = max(size * .5, .00001);
                float radial = length((shapeLocal - .5) * size / halfSize) - 1;
                return radial * min(halfSize.x, halfSize.y);
            }

            float TextStudioRegularPolygonDistance(
                float2 shapeLocal,
                float2 size,
                int sideCount)
            {
                float2 halfSize = max(size * .5, .00001);
                float2 localPoint = (shapeLocal - .5) * size / halfSize;
                sideCount = clamp(sideCount, 3, 8);
                float sector = 6.283185307179586 / sideCount;
                float angle = atan2(localPoint.y, localPoint.x) +
                              1.570796326794897;
                float radial = cos(
                    floor(.5 + angle / sector) * sector - angle) *
                    length(localPoint);
                float inset = cos(3.141592653589793 / sideCount);
                return (radial - inset) * min(halfSize.x, halfSize.y);
            }

            float2 TextStudioCustomPolygonPoint(
                TextStudio2DGpuOperation operation,
                int index)
            {
                if (index == 0) return operation.parameters6.xy;
                if (index == 1) return operation.parameters6.zw;
                if (index == 2) return operation.parameters7.xy;
                if (index == 3) return operation.parameters7.zw;
                if (index == 4) return operation.parameters8.xy;
                if (index == 5) return operation.parameters8.zw;
                if (index == 6) return operation.parameters9.xy;
                return operation.parameters9.zw;
            }

            float TextStudioPolygonEdgeDistance(
                float2 samplePoint,
                float2 first,
                float2 second)
            {
                float2 edge = second - first;
                float2 offset = samplePoint - first;
                float amount = saturate(
                    dot(offset, edge) / max(dot(edge, edge), .000001));
                float2 nearest = offset - edge * amount;
                return dot(nearest, nearest);
            }

            float TextStudioCustomPolygonDistance(
                TextStudio2DGpuOperation operation,
                float2 shapeLocal,
                float2 size,
                int pointCount)
            {
                pointCount = clamp(pointCount, 3, 8);
                float minimumDistance = 1e10;
                half signValue = 1;
                float2 previous = TextStudioCustomPolygonPoint(
                    operation,
                    pointCount - 1);
                [unroll]
                for (int index = 0; index < 8; index++)
                {
                    if (index >= pointCount)
                        break;
                    float2 current = TextStudioCustomPolygonPoint(
                        operation,
                        index);
                    minimumDistance = min(
                        minimumDistance,
                        TextStudioPolygonEdgeDistance(
                            shapeLocal,
                            previous,
                            current));
                    bool crosses = (current.y > shapeLocal.y) !=
                                   (previous.y > shapeLocal.y);
                    float intersection =
                        (previous.x - current.x) *
                        (shapeLocal.y - current.y) /
                        (previous.y - current.y + .000001) +
                        current.x;
                    if (crosses && shapeLocal.x < intersection)
                        signValue = -signValue;
                    previous = current;
                }
                return signValue * sqrt(max(0, minimumDistance)) *
                       min(size.x, size.y);
            }

            float2 TextStudioStarPoint(int index, half inset)
            {
                half angle = 1.570796326794897 -
                             index * .628318530717959;
                half radius = (index & 1) == 0 ? .5 : .5 * inset;
                return .5 + float2(cos(angle), sin(angle)) * radius;
            }

            float TextStudioStarDistance(
                float2 shapeLocal,
                float2 size,
                half inset)
            {
                inset = clamp(inset, .05, .95);
                float minimumDistance = 1e10;
                half signValue = 1;
                float2 previous = TextStudioStarPoint(9, inset);
                [unroll]
                for (int index = 0; index < 10; index++)
                {
                    float2 current = TextStudioStarPoint(index, inset);
                    minimumDistance = min(
                        minimumDistance,
                        TextStudioPolygonEdgeDistance(
                            shapeLocal,
                            previous,
                            current));
                    bool crosses = (current.y > shapeLocal.y) !=
                                   (previous.y > shapeLocal.y);
                    float intersection =
                        (previous.x - current.x) *
                        (shapeLocal.y - current.y) /
                        (previous.y - current.y + .000001) +
                        current.x;
                    if (crosses && shapeLocal.x < intersection)
                        signValue = -signValue;
                    previous = current;
                }
                return signValue * sqrt(max(0, minimumDistance)) *
                       min(size.x, size.y);
            }

            float TextStudioDecorationShapeDistance(
                TextStudio2DGpuOperation operation,
                float2 shapeLocal,
                float2 size,
                float emScale,
                bool bridge)
            {
                float radius = max(0, operation.parameters0.w) * emScale;
                if (bridge)
                {
                    return TextStudioRoundedRectDistance(
                        shapeLocal,
                        size,
                        min(radius, min(size.x, size.y) * .5));
                }

                int shape = (int)round(operation.parameters5.x);
                if (shape == 0)
                {
                    int shapeMetadata = max(
                        0,
                        (int)round(operation.parameters3.z));
                    int leftEnd = (shapeMetadata >> 4) & 7;
                    int rightEnd = (shapeMetadata >> 7) & 7;
                    float capSize = max(0, operation.parameters3.y) * emScale;
                    return TextStudioPlateDistance(
                        shapeLocal,
                        size,
                        radius,
                        capSize,
                        leftEnd,
                        rightEnd);
                }
                if (shape == 1)
                {
                    float4 radii = max(0, operation.parameters4) * emScale;
                    if (dot(radii, 1) <= .00001)
                        radii = radius.xxxx;
                    return TextStudioPerCornerRoundedRectDistance(
                        shapeLocal,
                        size,
                        radii);
                }
                if (shape == 2)
                    return TextStudioSquircleDistance(
                        shapeLocal,
                        size,
                        operation.parameters5.y);
                if (shape == 3)
                    return TextStudioRoundedRectDistance(
                        shapeLocal,
                        size,
                        min(size.x, size.y) * .5);
                if (shape == 4)
                    return TextStudioEllipseDistance(shapeLocal, size);
                if (shape >= 5 && shape <= 7)
                {
                    int sideCount = shape == 5 ? 3 : (shape == 6 ? 6 : 8);
                    return TextStudioRegularPolygonDistance(
                        shapeLocal,
                        size,
                        sideCount);
                }
                if (shape == 8)
                    return TextStudioStarDistance(
                        shapeLocal,
                        size,
                        operation.parameters5.w);
                return TextStudioCustomPolygonDistance(
                    operation,
                    shapeLocal,
                    size,
                    (int)round(operation.parameters12.w));
            }

            half TextStudioSoftShapeCoverage(float distance, float softness, float antiAlias)
            {
                return smoothstep(
                    max(softness, 0) + antiAlias,
                    -max(softness, 0) - antiAlias,
                    distance);
            }

            float TextStudioLineSegmentDistance(float2 linePoint, float start, float end, float halfThickness, int cap)
            {
                if (cap == 1)
                {
                    float nearest = clamp(linePoint.x, start, end);
                    return length(float2(linePoint.x - nearest, linePoint.y)) - halfThickness;
                }
                float extension = cap == 2 ? halfThickness : 0;
                float2 halfSize = float2(max(0, end - start) * .5 + extension, halfThickness);
                float2 center = float2((start + end) * .5, 0);
                float2 delta = abs(linePoint - center) - halfSize;
                return length(max(delta, 0)) + min(max(delta.x, delta.y), 0);
            }

            float TextStudioDecorationLineDistance(TextStudio2DGpuOperation operation, float2 shapeLocal, float2 size,
                float emScale, float patternCoordinate, float2 pixelUnits)
            {
                int pattern = (int)round(operation.parameters3.x);
                int units = (int)round(operation.parameters3.w);
                int flags = (int)round(operation.parameters5.w);
                int cap = (flags >> 2) & 3;
                float unitX = units == 1 ? pixelUnits.x : emScale;
                float unitY = units == 1 ? pixelUnits.y : emScale;
                float separation = max(0, operation.parameters5.y) * unitY;
                float amplitude = max(0, operation.parameters4.w) * unitY;
                float thickness = pattern == 1 ? max(.000001, (size.y - separation) * .5) :
                    pattern == 4 ? max(.000001, size.y - amplitude * 2) : max(.000001, size.y);
                float2 linePoint = float2(shapeLocal.x * size.x, (shapeLocal.y - .5) * size.y);
                float phase = operation.parameters5.z * unitX;
                float lineDistance = TextStudioLineSegmentDistance(linePoint, 0, size.x, thickness * .5, cap);
                if (pattern == 1)
                {
                    float center = (thickness + separation) * .5;
                    float first = TextStudioLineSegmentDistance(float2(linePoint.x, linePoint.y - center), 0, size.x, thickness * .5, cap);
                    float second = TextStudioLineSegmentDistance(float2(linePoint.x, linePoint.y + center), 0, size.x, thickness * .5, cap);
                    lineDistance = min(first, second);
                }
                else if (pattern == 2)
                {
                    float spacing = max(.000001, operation.parameters4.z * unitX);
                    float cycle = patternCoordinate + phase;
                    float nearest = (frac(cycle / spacing + .5) - .5) * spacing;
                    float dots = length(float2(nearest, linePoint.y)) - thickness * .5;
                    lineDistance = max(dots, max(-linePoint.x, linePoint.x - size.x));
                }
                else if (pattern == 3)
                {
                    float dash = max(.000001, operation.parameters4.x * unitX);
                    float gap = max(0, operation.parameters4.y * unitX);
                    float period = max(.000001, dash + gap);
                    float cycle = frac((patternCoordinate + phase) / period) * period;
                    float relative = cycle > dash + gap * .5 ? cycle - period : cycle;
                    float dashes = TextStudioLineSegmentDistance(float2(relative, linePoint.y), 0, dash, thickness * .5, cap);
                    lineDistance = max(dashes, max(-linePoint.x, linePoint.x - size.x));
                }
                else if (pattern == 4)
                {
                    float wavelength = max(.000001, operation.parameters5.x * unitX);
                    float angle = 6.283185307179586 * (patternCoordinate + phase) / wavelength;
                    float wave = amplitude * sin(angle);
                    float slope = amplitude * 6.283185307179586 / wavelength * cos(angle);
                    float waves = abs(linePoint.y - wave) / sqrt(1 + slope * slope) - thickness * .5;
                    lineDistance = max(waves, max(-linePoint.x, linePoint.x - size.x));
                }
                return lineDistance;
            }

            float TextStudioDecorationBoundDistance(TextStudio2DGpuOperation operation, float2 shapeLocal, float2 size,
                float emScale, bool bridge, float2 surfaceHandle, float patternCoordinate, float2 pixelUnits, float2 sourcePoint)
            {
                float distance = 0;
                if ((int)round(operation.parameters0.x) <= 2)
                {
                    distance = TextStudioDecorationLineDistance(operation, shapeLocal, size, emScale, patternCoordinate, pixelUnits);
                    if (surfaceHandle.x > .5) distance = max(distance, -TextStudioSurfaceDistanceAtPoint(sourcePoint, surfaceHandle));
                }
                else if (surfaceHandle.x > .5) distance = TextStudioSurfaceDistance(shapeLocal, surfaceHandle);
                else distance = TextStudioDecorationShapeDistance(operation, shapeLocal, size, emScale, bridge);
                return distance;
            }

            half4 TextStudioDecorationComponent(uint styleSlot, int operationIndex, float4 adjustmentMask, half4 color, half coverage, bool coverageOnly)
            {
                if (coverageOnly) return saturate(coverage);
                color = TextStudioApplyColorAdjustmentsForSelection(styleSlot, operationIndex, adjustmentMask, color);
                half alpha = saturate(color.a * coverage);
                return half4(color.rgb * alpha, alpha);
            }

            fixed4 TextStudioDecorationOver(fixed4 front, fixed4 back)
            {
                return front + back * (1 - front.a);
            }

            half4 TextStudioEvaluateSurfaceTreatments(TextStudioDecorationPixelInput input,
                uint styleSlot, int ownerIndex, TextStudio2DGpuOperation owner, int glyphIndex,
                bool bridge, float2 surfaceHandle, float4 adjustmentMask, bool coverageOnly, out half geometryCoverage)
            {
                geometryCoverage = 0;
                float2 size = max(abs(input.geometry.xy), .00001);
                float emScale = max(input.geometry.z, .00001);
                // Resolved per vertex for line owners with a dash, dot or wave pattern, and zero for every other owner.
                float patternCoordinate = input.lineCoordinate;
                float2 pixelUnits = TextStudioLinePixelUnits(float4(input.localPosition, 0, 1));
                bool maskedLine = owner.parameters0.x <= 2 && surfaceHandle.x > .5;
                // Canvas batching moves POSITION into Canvas space. Reconstruct ink coordinates from the retained line origin instead.
                float2 sourcePoint = maskedLine ? input.atlasRect.zw + input.shapeAndGlyph.xy * size : input.localPosition;
                float lineDistance = 0, inkDistance = 0, distance = 0;
                if (maskedLine)
                {
                    lineDistance = TextStudioDecorationLineDistance(owner, input.shapeAndGlyph.xy, size, emScale, patternCoordinate, pixelUnits);
                    inkDistance = -TextStudioSurfaceDistanceAtPoint(sourcePoint, surfaceHandle);
                    distance = max(lineDistance, inkDistance);
                }
                int first = (int)round(owner.parameters13.y);
                int count = (int)round(owner.parameters13.z);
                int last = min(count, TEXT_STUDIO_MAX_SURFACE_TREATMENTS) + first - 1;
                int operationBase = TextStudioStyleOperationBase(styleSlot);
                bool lit = false;
                [loop]
                for (int scan = last; scan >= first; scan--)
                {
                    TextStudio2DGpuOperation candidate = TextStudioLoadOperation(operationBase + scan);
                    int candidateKind = (int)round(candidate.layerMetadata.x);
                    lit = lit || (int)round(candidate.selectorMetadata.w) == ownerIndex + 1 && (candidateKind == 6 || candidateKind == 11);
                }
                // Every bound-distance sample (the centre, the owner's lighting gradient and each shifted shadow) goes
                // through this one call site. Each additional inlined shape distance cost about 20 seconds of compile time.
                float stepSize = max(.00001, min(size.x, size.y) * .001);
                float2 gradientSamples = 0;
                float shiftedDistances[TEXT_STUDIO_MAX_SURFACE_TREATMENTS];
                [loop]
                for (int request = 0; request < 3 + max(0, last - first + 1); request++)
                {
                    float2 shapeOffset = 0, sourceOffset = 0;
                    int treatmentIndex = first + request - 3;
                    if (request == 0 && maskedLine) continue;
                    if ((request == 1 || request == 2) && !lit) continue;
                    if (request == 1 || request == 2)
                    {
                        sourceOffset = request == 1 ? float2(stepSize, 0) : float2(0, stepSize);
                        shapeOffset = sourceOffset / size;
                    }
                    else if (request >= 3)
                    {
                        shiftedDistances[treatmentIndex - first] = 0;
                        TextStudio2DGpuOperation shadow = TextStudioLoadOperation(operationBase + treatmentIndex);
                        int shadowKind = (int)round(shadow.layerMetadata.x);
                        if ((int)round(shadow.selectorMetadata.w) != ownerIndex + 1 || shadowKind != 2 && shadowKind != 3) continue;
                        float2 offset = shadow.parameters0.xy * (shadow.parameters1.y > .5 ? pixelUnits : emScale.xx);
                        sourceOffset = -offset;
                        shapeOffset = -offset / size;
                    }
                    float sampled = TextStudioDecorationBoundDistance(owner, input.shapeAndGlyph.xy + shapeOffset,
                        size, emScale, bridge, surfaceHandle, patternCoordinate + sourceOffset.x, pixelUnits, sourcePoint + sourceOffset);
                    if (request == 0) distance = sampled;
                    else if (request == 1) gradientSamples.x = sampled;
                    else if (request == 2) gradientSamples.y = sampled;
                    else shiftedDistances[treatmentIndex - first] = sampled;
                }
                float antiAlias = max(fwidth(distance), .00001);
                float lineAntiAlias = max(fwidth(lineDistance), .00001), inkAntiAlias = max(fwidth(inkDistance), .00001);
                half shapeCoverage = maskedLine ? min(TextStudioSoftShapeCoverage(lineDistance, 0, lineAntiAlias),
                    TextStudioSoftShapeCoverage(inkDistance, 0, inkAntiAlias)) : TextStudioSoftShapeCoverage(distance, 0, antiAlias);
                float2 screenLocal = input.screenPosition.xy / max(input.screenPosition.w, .00001);
                TextStudioPaintDerivatives paintDerivatives = TextStudioBuildPaintDerivatives(
                    input.shapeAndGlyph.zw, input.textAndProgram.xy, input.localPosition, screenLocal, input.worldPosition);
                float shapeFootprint = max(length(ddx(input.shapeAndGlyph.xy)), length(ddy(input.shapeAndGlyph.xy)));
                float pixelScale = max(length(float2(ddx(distance), ddy(distance))), .000001);
                // Hero derivatives are in render-target pixels. Authored pixel widths refer to final output pixels.
                if (_TextStudio2DDecorationHero > .5)
                    pixelScale *= max(_TextStudio2DSurfacePixelUnit.z, .000001);
                // The lighting gradient belongs to the owner's shape, not to a treatment, so it is resolved once.
                float2 gradientSteps = gradientSamples - distance;
                half2 gradient = lit ? -gradientSteps / max(length(gradientSteps), .000001) : 0;
                half4 result = 0;
                [loop]
                for (int index = last; index >= first; index--)
                {
                    TextStudio2DGpuOperation treatment = TextStudioLoadOperation(operationBase + index);
                    if ((int)round(treatment.selectorMetadata.w) != ownerIndex + 1) continue;
                    int kind = (int)round(treatment.layerMetadata.x);
                    float units = kind == 11 ? 0 : kind == 0 ? treatment.parameters0.z : kind == 1 ? treatment.parameters1.w : treatment.parameters1.y;
                    float scale = units > .5 ? pixelScale : emScale;
                    half coverage = 0;
                    if (kind == 0)
                    {
                        coverage = TextStudioSoftShapeCoverage(distance - treatment.parameters0.x * scale, treatment.parameters0.y * scale, antiAlias);
                        if (maskedLine)
                            coverage = min(TextStudioSoftShapeCoverage(lineDistance - treatment.parameters0.x * scale, treatment.parameters0.y * scale, lineAntiAlias),
                                TextStudioSoftShapeCoverage(inkDistance - treatment.parameters0.x * scale, treatment.parameters0.y * scale, inkAntiAlias));
                        if ((int)round(owner.parameters0.x) == 4)
                            coverage *= 1 - TextStudioSoftShapeCoverage(distance + owner.parameters0.y * emScale, 0, antiAlias);
                    }
                    else if (kind == 1)
                    {
                        float width = max(0, treatment.parameters0.x) * scale;
                        coverage = width > .000001 ? TextStudioSoftShapeCoverage(
                            abs(distance - treatment.parameters0.y * width * .5) - width * .5, treatment.parameters0.z * scale, antiAlias) : 0;
                    }
                    else if (kind == 2 || kind == 3)
                    {
                        float shifted = shiftedDistances[index - first];
                        coverage = TextStudioSoftShapeCoverage(shifted - treatment.parameters0.w * scale, treatment.parameters0.z * scale, antiAlias);
                        if (kind == 2) coverage = shapeCoverage * (1 - coverage);
                    }
                    else if (kind == 4 || kind == 5)
                    {
                        float radius = max(0, treatment.parameters0.x) * scale;
                        float spread = max(0, treatment.parameters0.y) * scale;
                        coverage = radius + spread > .000001 ? 1 - smoothstep(spread,
                            spread + radius + antiAlias, max(0, kind == 4 ? -distance : distance)) : 0;
                        if (kind == 4) coverage *= shapeCoverage;
                    }
                    half4 paint;
                    if (kind == 6 || kind == 11)
                    {
                        float width = max(.000001, treatment.parameters0.x * (kind == 6 ? scale : emScale));
                        half relief = shapeCoverage * (1 - smoothstep(0, width + treatment.parameters0.y * scale, max(0, -distance)));
                        paint = TextStudioEvaluateMaterialLighting(kind, treatment, input.shapeAndGlyph.xy,
                            shapeCoverage, relief, max(0, -distance), width, gradient,
                            shapeFootprint,
                            half4(input.color.rgb, 1), coverage);
                    }
                    else
                    {
                        half distanceCoordinate = treatment.parameters2.x == 5 ? abs(distance) / emScale : max(0, -distance) / emScale;
                        paint = TextStudioEvaluateStylePaint(styleSlot, index, glyphIndex, treatment,
                            input.shapeAndGlyph.zw, input.textAndProgram.xy, input.localPosition,
                            screenLocal, input.worldPosition, distanceCoordinate, paintDerivatives);
                        if (treatment.layerMetadata.w > .5) paint.rgb *= input.color.rgb;
                        coverage *= saturate(treatment.layerMetadata.z);
                    }
                    half4 component = TextStudioDecorationComponent(styleSlot, index, adjustmentMask, paint, coverage, coverageOnly);
                    result = TextStudioBlendPremultiplied(result, component, (int)round(treatment.layerMetadata.y));
                    // Every blend mode composes alpha source-over, so geometric coverage accumulates without paint.
                    geometryCoverage = saturate(coverage) + geometryCoverage * (1 - saturate(coverage));
                }
                half ownerOpacity = saturate(owner.layerMetadata.z * input.color.a);
                geometryCoverage *= ownerOpacity;
                return result * ownerOpacity;
            }

            // alphaTest is the alpha-clip value of the returned colour, or 1 where the core returns without clipping. Callers discard
            // after every interpolant read: discarding here, before the code of other layer kinds, made the Adreno 619 Vulkan driver
            // fail to link these programs ("Out of index").
            fixed4 TextStudioDecorationFragmentCore(
                TextStudioDecorationPixelInput input, bool coverageOnly, out half geometryCoverage, out half alphaTest)
            {
                geometryCoverage = 0;
                alphaTest = 1;
                if (TEXT_STUDIO_STYLE_TABLE_ENABLED < .5)
                    return 0;

                uint styleSlot = (uint)max(0, round(input.textAndProgram.z));
                int operationIndex = max(0, (int)round(input.textAndProgram.w));
                if (styleSlot == 0)
                    return 0;

                TextStudio2DGpuStyleHeader header =
                    TextStudioLoadStyleHeader(styleSlot);
                int operationCount = max(0, (int)round(header.metadata.y));
                if ((int)round(header.metadata.x) !=
                        TEXT_STUDIO_STYLE_SCHEMA_VERSION ||
                    operationIndex >= operationCount)
                {
                    return 0;
                }

                TextStudio2DGpuOperation operation =
                    TextStudioLoadOperation((int)round(header.program.x) + operationIndex);
                int layerKind = (int)round(operation.layerMetadata.x);
                int executionPath = (int)round(operation.executionMetadata.x);
                if ((executionPath != 2 && layerKind != 8 && layerKind != 1) || (layerKind != 7 && layerKind != 8 && layerKind != 1))
                    return 0;

                if (_TextStudio2DDecorationHero > .5 && operationIndex != _TextStudio2DHeroOperationIndex) return 0;
                float packedGeometryKind = round(input.geometry.w);
                bool connectedSurface = layerKind == 8 && packedGeometryKind < 0;
                float2 surfaceHandle = connectedSurface ? input.atlasRect.xy : 0;
                float4 adjustmentMask = connectedSurface ? TextStudioSurfaceAdjustmentMask(surfaceHandle) : input.atlasRect;
                int glyphIndex = layerKind == 8
                    ? connectedSurface ? -(int)packedGeometryKind - 1 : max(0, (int)floor(packedGeometryKind * .5) - 1)
                    : max(0, (int)packedGeometryKind - 1);
                if (_TextStudio2DDecorationHero > .5 && layerKind != 8 && _TextStudio2DHeroGlyphFilter >= 0 &&
                    TextStudioSourceUnit(styleSlot, glyphIndex) != _TextStudio2DHeroGlyphFilter) return 0;
                bool bridge = layerKind == 8 &&
                              fmod(packedGeometryKind, 2.0) > .5;

                half coverage = 0;
                float2 paintLocal = input.shapeAndGlyph.zw;
                half distanceCoordinate = 0;
                half paintLighting = 1;
                if (layerKind == 8)
                {
                    half4 surface = TextStudioEvaluateSurfaceTreatments(input, styleSlot, operationIndex, operation,
                        glyphIndex, bridge, surfaceHandle, adjustmentMask, coverageOnly, geometryCoverage);
                    UNITY_BRANCH
                    if (UNITY_UI_CLIP_RECT)
                    {
                        half clipping = UnityGet2DClipping(input.localPosition, _ClipRect);
                        surface *= clipping;
                        geometryCoverage *= clipping;
                    }
                    alphaTest = surface.a - .001;
                    return surface;
                }
                if (layerKind == 1)
                {
                    float2 contourPoint = TextStudioSurfaceMetricPoint(TextStudioSurfacePoint(input.shapeAndGlyph.xy, input.atlasRect.xy), input.atlasRect.xy);
                    float antiAlias = max(.000001, .5 * (length(ddx(contourPoint)) + length(ddy(contourPoint))));
                    float distance = TextStudioSurfaceDistanceAtPoint(contourPoint, input.atlasRect.xy);
                    coverage = 1 - smoothstep(-antiAlias, max(0, input.geometry.z) + antiAlias, distance);
                    distanceCoordinate = distance;
                }
                else
                {
                    TextStudio2DDepthFieldMapping mapping =
                        (TextStudio2DDepthFieldMapping)0;
                    mapping.atlasOrigin = input.atlasRect.xy;
                    mapping.atlasAxisX = float2(
                        input.atlasRect.z - input.atlasRect.x,
                        0);
                    mapping.atlasAxisY = float2(
                        0,
                        input.atlasRect.w - input.atlasRect.y);
                    float2 atlasUv = TextStudioDepthAtlasFromLocal(
                        mapping,
                        input.shapeAndGlyph.xy);
                    mapping.atlasDx = ddx(atlasUv);
                    mapping.atlasDy = ddy(atlasUv);
                    half bold = step(.5, input.geometry.z);
                    half weight =
                        (lerp(_WeightNormal, _WeightBold, bold) / 4.0 +
                         _FaceDilate) * _ScaleRatioA * .5;
                    mapping.threshold = .5 - weight;
                    mapping.gradientScale = max(_GradientScale, 1.0);
                    mapping.pixelsPerEm = max(1, input.geometry.x);
                    mapping.prepared = TextStudioGetPreparedField(styleSlot, glyphIndex);
                    float2 atlasMinimum = min(
                        input.atlasRect.xy,
                        input.atlasRect.zw);
                    float2 atlasMaximum = max(
                        input.atlasRect.xy,
                        input.atlasRect.zw);
                    float2 textureSize = max(
                        float2(_TextureWidth, _TextureHeight),
                        1.0);
                    float2 atlasInset = .5 / textureSize;
                    float2 safePadding = max(
                        0,
                        mapping.gradientScale - 2.0) / textureSize;
                    mapping.atlasLower = max(
                        atlasInset,
                        atlasMinimum - safePadding);
                    mapping.atlasUpper = min(
                        1.0 - atlasInset,
                        atlasMaximum + safePadding);
                    half depthProgress;
                    coverage = TextStudioAdaptiveDepthCoverage(
                        mapping,
                        input.shapeAndGlyph.xy,
                        TextStudioDepthEmOffset(mapping, operation.parameters0.xy),
                        operation.parameters0.z * length(operation.parameters0.xy),
                        operation.parameters0.w,
                        operation.parameters1.x * mapping.pixelsPerEm,
                        operation.parameters4.x * mapping.pixelsPerEm,
                        operation.parameters1.y,
                        operation.executionMetadata.y,
                        paintLocal,
                        depthProgress);
                    distanceCoordinate = depthProgress;
                    paintLighting = TextStudioDepthLighting(
                        mapping,
                        paintLocal,
                        depthProgress,
                        operation.parameters3.x,
                        operation.parameters3.y,
                        operation.parameters3.z,
                        operation.parameters3.w,
                        operation.parameters2.w);
                    if (operation.parameters1.z > .5)
                    {
                        paintLocal = float2(.5, 1.0 - depthProgress);
                        operation.paintMetadata.y = 0;
                    }
                }

                fixed4 paint = TextStudioEvaluateStylePaint(
                    styleSlot,
                    operationIndex,
                    glyphIndex,
                    operation,
                    paintLocal,
                    input.textAndProgram.xy,
                    input.localPosition,
                    input.screenPosition.xy /
                        max(input.screenPosition.w, .00001),
                    input.worldPosition,
                    distanceCoordinate);
                paint.rgb *= paintLighting;
                half sourceAlpha = saturate(input.color.a);
                if (operation.layerMetadata.w > .5)
                    paint.rgb *= input.color.rgb;
                paint = TextStudioApplyColorAdjustments(styleSlot, operationIndex, glyphIndex, paint);
                if (coverageOnly) paint = 1;
                half layerAlpha = saturate(coverage * paint.a * operation.layerMetadata.z * sourceAlpha);
                fixed4 result = fixed4(paint.rgb * layerAlpha, layerAlpha);
                geometryCoverage = saturate(coverage * operation.layerMetadata.z * sourceAlpha);
                if (layerKind == 1)
                {
                    half writing = TextStudioWritingSourceCoverage(input.shapeAndGlyph.zw, input.atlasRect.z);
                    result *= writing;
                    geometryCoverage *= writing;
                }

                UNITY_BRANCH
                if (UNITY_UI_CLIP_RECT)
                {
                    half clipping = UnityGet2DClipping(input.localPosition, _ClipRect);
                    result *= clipping;
                    geometryCoverage *= clipping;
                }
                alphaTest = result.a - .001;
                return result;
            }

        #if defined(TEXT_STUDIO_DECORATION_LINE_SOLID)
            // Largest distance a fill treatment moves or softens an edge. Pixel units scale by the screen gradient of a
            // distance that changes at most one unit per unit, which twice the footprint bounds.
            float TextStudioLineFillReach(int operationBase, int ownerIndex, int first, int last, float emScale, float footprint)
            {
                float reach = 0;
                [loop]
                for (int index = last; index >= first; index--)
                {
                    TextStudio2DGpuOperation treatment = TextStudioLoadOperation(operationBase + index);
                    if ((int)round(treatment.selectorMetadata.w) != ownerIndex + 1) continue;
                    float scale = treatment.parameters0.z > .5 ? footprint * 2 : emScale;
                    reach = max(reach, (abs(treatment.parameters0.x) + max(0, treatment.parameters0.y)) * scale);
                }
                return reach;
            }

            // Lean program of retained lines whose treatments are normal fills of solid paint, in a style without colour
            // adjustments (TextStudio2DStyleDecorations.SupportsLineProgram). It repeats the arithmetic, order and precision
            // of TextStudioEvaluateSurfaceTreatments for exactly that case, so selected rows render the pixels of the general
            // program while compiling none of its shape, surface, lighting, shadow, depth and paint families.
            fixed4 TextStudioDecorationLineFragment(TextStudioDecorationPixelInput input)
            {
                int operationBase = (int)round(input.lineRecord.x);
                if (operationBase < 0) return 0;
                int ownerIndex = max(0, (int)round(input.textAndProgram.w));
                TextStudio2DGpuOperation owner = TextStudioLoadOperation(operationBase + ownerIndex);
                if ((int)round(owner.layerMetadata.x) != 8 || owner.parameters0.x > 2) return 0;
                float2 size = max(abs(input.geometry.xy), .00001);
                float emScale = max(input.geometry.z, .00001);
                // Only pixel-unit lines read the camera scale; the line distance ignores it otherwise.
                float2 pixelUnits = (int)round(owner.parameters3.w) == 1 ? TextStudioLinePixelUnits(float4(input.localPosition, 0, 1)) : 1;
                float lineDistance = TextStudioDecorationLineDistance(owner, input.shapeAndGlyph.xy, size, emScale, input.lineCoordinate, pixelUnits);
                int first = (int)round(owner.parameters13.y);
                int last = min((int)round(owner.parameters13.z), TEXT_STUDIO_MAX_SURFACE_TREATMENTS) + first - 1;
                bool maskedLine = input.lineRecord.z > -1.5;
                float2 sourcePoint = input.atlasRect.zw + input.shapeAndGlyph.xy * size;
                float footprint = length(ddx(sourcePoint)) + length(ddy(sourcePoint));
                float inkDistance = 0, distance = lineDistance;
                if (maskedLine)
                {
                    inkDistance = -1e6;
                    if (input.lineRecord.z > -.5)
                    {
                        // Edges after the band edges lie beyond this reach from a pixel whose window stays inside the band and
                        // never cross its row. The reach covers the antialiasing widths of the pixel and its neighbours, the fill
                        // treatments and the line's half height, so every distance that reaches the pixel's coverage is exact.
                        float reach = footprint * 4 + size.y * .5 + TextStudioLineFillReach(operationBase, ownerIndex, first, last, emScale, footprint);
                        int edges = sourcePoint.y - reach >= input.lineBand.x && sourcePoint.y + reach <= input.lineBand.y
                            ? (int)round(input.lineRecord.w) : (int)round(input.lineRecord.z);
                        inkDistance = -TextStudioSurfaceEdgeDistance(sourcePoint, (int)round(input.lineRecord.y), edges);
                    }
                    distance = max(lineDistance, inkDistance);
                }
                float antiAlias = max(fwidth(distance), .00001);
                float lineAntiAlias = max(fwidth(lineDistance), .00001), inkAntiAlias = max(fwidth(inkDistance), .00001);
                float pixelScale = max(length(float2(ddx(distance), ddy(distance))), .000001);
                half4 result = 0;
                [loop]
                for (int index = last; index >= first; index--)
                {
                    TextStudio2DGpuOperation treatment = TextStudioLoadOperation(operationBase + index);
                    if ((int)round(treatment.selectorMetadata.w) != ownerIndex + 1 || (int)round(treatment.layerMetadata.x) != 0) continue;
                    float scale = treatment.parameters0.z > .5 ? pixelScale : emScale;
                    half coverage = TextStudioSoftShapeCoverage(distance - treatment.parameters0.x * scale, treatment.parameters0.y * scale, antiAlias);
                    if (maskedLine)
                        coverage = min(TextStudioSoftShapeCoverage(lineDistance - treatment.parameters0.x * scale, treatment.parameters0.y * scale, lineAntiAlias),
                            TextStudioSoftShapeCoverage(inkDistance - treatment.parameters0.x * scale, treatment.parameters0.y * scale, inkAntiAlias));
                    half4 paint = treatment.color;
                    if (treatment.layerMetadata.w > .5) paint.rgb *= input.color.rgb;
                    coverage *= saturate(treatment.layerMetadata.z);
                    half alpha = saturate(paint.a * coverage);
                    result = TextStudioBlendPremultiplied(result, half4(paint.rgb * alpha, alpha), (int)round(treatment.layerMetadata.y));
                }
                half ownerOpacity = saturate(owner.layerMetadata.z * input.color.a);
                half4 surface = result * ownerOpacity;
                UNITY_BRANCH
                if (UNITY_UI_CLIP_RECT)
                {
                    half clipping = UnityGet2DClipping(input.localPosition, _ClipRect);
                    surface *= clipping;
                }
                UNITY_BRANCH
                if (UNITY_UI_ALPHACLIP) clip(surface.a - .001);
                return surface;
            }
        #endif

            fixed4 TextStudioDecorationFragment(TextStudioDecorationPixelInput input) : SV_Target
            {
            #if defined(TEXT_STUDIO_DECORATION_LINE_SOLID)
                return TextStudioDecorationLineFragment(input);
            #else
                half geometryCoverage;
                half alphaTest;
                fixed4 result = TextStudioDecorationFragmentCore(input, _TextStudio2DGeometryCoveragePass > .5, geometryCoverage, alphaTest);
                UNITY_BRANCH
                if (UNITY_UI_ALPHACLIP)
                    clip(alphaTest);
                return result;
            #endif
            }

            struct TextStudioConnectedSurfaceOutput
            {
                fixed4 color : SV_Target;
                float depth : SV_Depth;
            };

            // Raised by Hero around its connected-surface union draws and lowered after them.
            float _TextStudio2DConnectedSurfaceUnion;

            TextStudioDecorationPixelInput TextStudioConnectedSurfaceVertex(TextStudioDecorationVertexInput input)
            {
                TextStudioDecorationPixelInput output = TextStudioDecorationVertex(input);
                // Hero raises the flag around its union draws only. An overlay Canvas submits every pass of a retained
                // material regardless of LightMode, so the union quads collapse there and rasterize nothing.
                if (_TextStudio2DConnectedSurfaceUnion < .5)
                    output.vertex = float4(0, 0, 0, 1);
                return output;
            }

            TextStudioConnectedSurfaceOutput TextStudioConnectedSurfaceFragment(TextStudioDecorationPixelInput input)
            {
                // Resolve overlapping patches by geometric coverage. One evaluation returns both the painted
                // layer and its coverage; inlining the core twice doubled this pass's compile cost.
                half geometryCoverage;
                half alphaTest;
                TextStudioConnectedSurfaceOutput output;
                output.color = TextStudioDecorationFragmentCore(input, _TextStudio2DGeometryCoveragePass > .5, geometryCoverage, alphaTest);
                float coverage = saturate(geometryCoverage);
                UNITY_BRANCH
                if (UNITY_UI_ALPHACLIP)
                    clip(alphaTest);
                clip(coverage - .00001);
                #if defined(UNITY_REVERSED_Z)
                output.depth = coverage;
                #else
                output.depth = 1 - coverage;
                #endif
                return output;
            }

            fixed4 TextStudioDecorationShadowFragment(
                TextStudioDecorationPixelInput input) : SV_Target
            {
                fixed4 result = TextStudioDecorationFragment(input);
                clip(result.a - .001);
                return 0;
            }
        ENDCG

        Pass
        {
            Name "TEXTSTUDIO_STYLE_DECORATION"
            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            // Retained lines that qualify draw with the lean solid-line program (AppearanceGpuCost.md).
            #pragma multi_compile_local __ TEXT_STUDIO_DECORATION_LINE_SOLID
            #pragma vertex TextStudioDecorationVertex
            #pragma fragment TextStudioDecorationFragment
            #pragma dynamic_branch UNITY_UI_CLIP_RECT
            #pragma dynamic_branch UNITY_UI_ALPHACLIP
            ENDCG
        }

        Pass
        {
            Name "TEXTSTUDIO_DECORATION_SHADOW_CASTER"
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
            #pragma vertex TextStudioDecorationVertex
            #pragma fragment TextStudioDecorationShadowFragment
            #pragma multi_compile_shadowcaster
            #pragma dynamic_branch UNITY_UI_CLIP_RECT
            #pragma dynamic_branch UNITY_UI_ALPHACLIP
            ENDCG
        }
        Pass
        {
            Name "TEXTSTUDIO_CONNECTED_SURFACE_UNION"
            // Hero draws this pass by index. A LightMode outside every pipeline's defaults keeps Built-in renderers and
            // camera Canvases, which draw each untagged pass, from writing its depth over later layers.
            Tags { "LightMode"="TextStudioConnectedSurfaceUnion" }
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend Off

            CGPROGRAM
            #pragma target 3.5
            #pragma target 4.5 TEXT_STUDIO_BUFFER_TRANSPORT
            #pragma multi_compile TEXT_STUDIO_BUFFER_TRANSPORT TEXT_STUDIO_TEXTURE_TRANSPORT
            #pragma vertex TextStudioConnectedSurfaceVertex
            #pragma fragment TextStudioConnectedSurfaceFragment
            #pragma dynamic_branch UNITY_UI_CLIP_RECT
            #pragma dynamic_branch UNITY_UI_ALPHACLIP
            ENDCG
        }
    }
}
