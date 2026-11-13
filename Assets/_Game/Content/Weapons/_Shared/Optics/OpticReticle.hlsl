// An optic's lens: a faint glass tint with the reticle projected at infinity along the lens's +Z (the barrel), so the
// dot stays on the aim point however the eye sits behind the glass (no parallax). ViewDirOS: fragment to camera, in the
// lens's object space. SizeDegrees: the angle the whole reticle texture spans.
void OpticReticle_float(float3 ViewDirOS, UnityTexture2D Tex, float SizeDegrees, float4 ReticleColor, float4 LensTint,
    out float3 Color, out float Alpha)
{
    float3 ray = -normalize(ViewDirOS);
    float facing = step(0.01, ray.z);
    float halfTan = tan(radians(max(SizeDegrees, 0.01) * 0.5));
    float2 uv = ray.xy / max(ray.z, 0.01) / halfTan * 0.5 + 0.5;
    float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
    float reticle = SAMPLE_TEXTURE2D(Tex.tex, Tex.samplerstate, uv).a * facing * inside;
    Color = lerp(LensTint.rgb, ReticleColor.rgb, reticle);
    Alpha = max(LensTint.a, reticle * ReticleColor.a);
}

void OpticReticle_half(half3 ViewDirOS, UnityTexture2D Tex, half SizeDegrees, half4 ReticleColor, half4 LensTint,
    out half3 Color, out half Alpha)
{
    float3 color;
    float alpha;
    OpticReticle_float(ViewDirOS, Tex, SizeDegrees, ReticleColor, LensTint, color, alpha);
    Color = color;
    Alpha = alpha;
}
