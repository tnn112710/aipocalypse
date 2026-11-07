#ifndef COZYVFXVariables
#define COZYVFXVariables

TEXTURE2D(CZY_FXBlockDepthTex);
SAMPLER(samplerCZY_FXBlockDepthTex);
float CZY_DepthTextureHeight;
float CZY_RenderDistance;
float3 CZY_WindDirection;

void GetVFXProperties_float(out float renderDistance, out float3 windDirection) {
    renderDistance = CZY_RenderDistance;
    windDirection = CZY_WindDirection;
}

void GetWorldHeight_float(float3 position, out float height)
{
    float3 worldPos = TransformObjectToWorld(float3(0, 0, 0));
    float2 uv = (position - worldPos).xz;
    uv = (uv / CZY_RenderDistance + float2(1, 1)) / 2;

    height = worldPos.y + CZY_DepthTextureHeight * (CZY_FXBlockDepthTex.SampleLevel(samplerCZY_FXBlockDepthTex, uv, 0).r - 0.5) * 2;
}

#endif