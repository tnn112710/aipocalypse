float4x4 CZY_Base_Camera_InverseViewProjection;
float4x4 CZY_Base_Camera_InverseProjection;
float4x4 CZY_Base_Camera_CameraToWorldMatrix;
float3 CZY_Base_Camera_WorldPos;
float CZY_Base_Camera_NearPlane;
float CZY_Base_Camera_FarPlane;
float2 CZY_Base_Camera_NearSize;
float2 CZY_Base_Camera_FarSize;

float CZY_Fog_Volumetric_MaxDistance;

float ExponentialDepth(float d) {
    return pow(abs(d), 2);
}

float InverseExponentialDepth(float d) {
    return pow(abs(d), 0.5);
}

float3 UVWToViewSpace(float3 uvw) {

    float viewDepth = ExponentialDepth(uvw.z);
    float2 remappedViewSpaceRadius = lerp(CZY_Base_Camera_NearSize, CZY_Base_Camera_FarSize, viewDepth);

    float2 viewXY = (uvw.xy * 2 - 1) * remappedViewSpaceRadius.xy;
    float viewZ = viewDepth * CZY_Fog_Volumetric_MaxDistance;

    return float3(viewXY, viewZ);
}

float3 ViewSpaceToUVW(float3 vsPosition) {
    float depth = InverseExponentialDepth(vsPosition.z / CZY_Fog_Volumetric_MaxDistance);
    float2 remappedViewSpaceRadius = lerp(CZY_Base_Camera_NearSize, CZY_Base_Camera_FarSize, vsPosition.z / CZY_Fog_Volumetric_MaxDistance);

    float2 uvwXY = (vsPosition.xy / remappedViewSpaceRadius.xy) * 0.5 + 0.5;

    return float3(uvwXY, depth);
}
float3 ViewSpaceToWorldSpace(float3 viewPos, float2 uv) {

    float3 rawRay = mul(CZY_Base_Camera_InverseProjection, float4(uv * 2 - 1, 0, 1)).xyz;
    rawRay = mul(CZY_Base_Camera_CameraToWorldMatrix, float4(rawRay, 0)).xyz;
    float rayLength = length(rawRay);

    viewPos.z = -viewPos.z;
    viewPos.xyz /= (rayLength + 1e-7);

    float3 worldPos = mul(CZY_Base_Camera_CameraToWorldMatrix, float4(viewPos.xyz, 1.0)).xyz;
    return worldPos;

}


float3 UVWToWorldSpace(float3 uvw)
{
    float3 viewSpace = UVWToViewSpace(uvw);
    float3 worldPos = ViewSpaceToWorldSpace(viewSpace, uvw.xy);
    return worldPos;
}

float3 VoxelToUVW(uint3 id, float3 resolution) {
    float3 uvw = (float3(id) + 0.5f) / resolution;
    return uvw;
}

float RemapValue(float value, float inMin, float inMax, float outMin, float outMax)
{
    float t = (value - inMin) / (inMax - inMin);
    return lerp(outMin, outMax, t);
}

float rand(float3 vec)
{
	float a = 12.9898;
	float b = 78.233;
	float c = 153.332;
	float d = 43758.5453;
	float dt= dot(vec, float3(a,b,c));
	float sn= dt % 3.14;
	return frac(sin(sn) * d);
}

float CZY_GameTime;
int GetID(uint3 id)
{
	return (rand(id) * 16 + ((CZY_GameTime) * 60.0)) % 16;
}

static const float3 hammersley[16] = {
    float3(0.0000f, 0.3333f, 0.0312f),
    float3(0.5000f, 0.6667f, 0.0938f),
    float3(0.2500f, 0.1111f, 0.1562f),
    float3(0.7500f, 0.4444f, 0.2188f),
    float3(0.1250f, 0.7778f, 0.2812f),
    float3(0.6250f, 0.2222f, 0.3438f),
    float3(0.3750f, 0.5556f, 0.4062f),
    float3(0.8750f, 0.8889f, 0.4688f),
    float3(0.0625f, 0.0370f, 0.5312f),
    float3(0.5625f, 0.3704f, 0.5938f),
    float3(0.3125f, 0.7037f, 0.6562f),
    float3(0.8125f, 0.1481f, 0.7188f),
    float3(0.1875f, 0.4815f, 0.7812f),
    float3(0.6875f, 0.8148f, 0.8438f),
    float3(0.4375f, 0.2593f, 0.9062f),
    float3(0.9375f, 0.5926f, 0.9688f)
};