#ifndef TEXT_STUDIO_2D_VISUAL_MODIFIER_COMMON_INCLUDED
#define TEXT_STUDIO_2D_VISUAL_MODIFIER_COMMON_INCLUDED

#define TEXT_STUDIO_VISUAL_MODIFIER_PER_TEXT_CLOCK (1u << 0)

struct TextStudio2DGpuTextUniform
{
    float4 timeAndProgress;
    float4 policyAndGeneration;
};

#ifndef TEXT_STUDIO_SINGLE_STYLE_OPERATION
#if defined(TEXT_STUDIO_TEXTURE_TRANSPORT)
Texture2D<float4> _TextStudio2DTextUniformsData;
TextStudio2DGpuTextUniform TextStudioLoadTextUniform(uint slot)
{
    int texel = (int)slot * 2;
    TextStudio2DGpuTextUniform record;
    record.timeAndProgress = _TextStudio2DTextUniformsData.Load(int3(texel & 1023, texel >> 10, 0));
    texel++;
    record.policyAndGeneration = _TextStudio2DTextUniformsData.Load(int3(texel & 1023, texel >> 10, 0));
    return record;
}
#else
StructuredBuffer<TextStudio2DGpuTextUniform>
    _TextStudio2DTextUniforms;
TextStudio2DGpuTextUniform TextStudioLoadTextUniform(uint slot) { return _TextStudio2DTextUniforms[slot]; }
#endif
float _TextStudio2DVisualModifiersEnabled;
#endif

void TextStudioResolveVisualModifier(
    uint styleSlot,
    TextStudio2DGpuOperation operation,
    out float continuousTime,
    out half phaseOffset,
    out half speed,
    out int motionPolicy,
    out int reducedMotionBehavior,
    out half valid)
{
    continuousTime = _TextStudio2DHeroTime;
    phaseOffset = operation.parameters0.z;
    speed = operation.parameters0.y;
    motionPolicy = _TextStudio2DHeroMotionPolicy;
    reducedMotionBehavior = (int)round(operation.paintMetadata.w);
    valid = 1;

    #ifndef TEXT_STUDIO_SINGLE_STYLE_OPERATION
    valid = 0;
    if (_TextStudio2DVisualModifiersEnabled < .5 || styleSlot == 0)
        return;

    uint operationFlags = (uint)round(operation.executionMetadata.w);
    if ((operationFlags & TEXT_STUDIO_VISUAL_MODIFIER_PER_TEXT_CLOCK) == 0u)
        return;

    TextStudio2DGpuTextUniform textUniform =
        TextStudioLoadTextUniform(styleSlot);
    continuousTime = textUniform.timeAndProgress.x;
    motionPolicy = (int)round(textUniform.policyAndGeneration.x);
    valid = 1;
    #endif
}

#endif
