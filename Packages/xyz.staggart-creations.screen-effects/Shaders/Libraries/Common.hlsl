// SC Screen Effects © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

//-------------
// Generic functions
//-------------

#define ANGLE2RAD_RCP 0.01745329251 //=> Pi / 180
#define EPSILON         1.0e-4

float rand(float n) { return frac(sin(n) * 13758.5453123 * 0.01); }

float rand(float2 n) { return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453); }

inline float mod(float2 a, float b)
{
	return (a.xy - (b * floor(a.xy / b))).x;
}

//v2.2.0, function now takes an angle in degrees
float2 RotateUV(float2 uv, float2 pivot, float angle)
{
	const float radians = ANGLE2RAD_RCP * angle;
	
	float cosine = cos(radians);
	float sine = sin(radians);
	
	const float2 rotator = (mul(uv - pivot, float2x2(cosine, -sine, sine, cosine)) + pivot);
	
	return saturate(rotator);
}

half LuminanceThreshold(half val, half threshold)
{
	half contrib = max(0.0, val - threshold);

	contrib /= max(val, 0.001);

	return val * contrib;
}

float max3(float3 color)
{
	return max(color.r, max(color.g, color.b));
}

half3 LuminanceThreshold(half3 color, half threshold)
{
	half br = max3(color);

	half contrib = max(0, br - threshold);

	contrib /= max(br, 0.001);

	return color * contrib;
}

float min3(float3 color)
{
	return min(color.r, min(color.g, color.b));
}

void AdjustContract(inout float3 color, float amount)
{
	//Assumes linear color space!
	float midpoint = (0.5);
	color = (color - midpoint) * (amount + 1.0) + midpoint;
}

//Edge feathering
float ScreenEdgeMask(float2 screenPos, float length)
{
	float lengthRcp = 1.0f/length;
	float2 t = Remap10(abs(screenPos.xy * 2.0 - 1.0), lengthRcp, lengthRcp);
	return Smoothstep01(t.x) * Smoothstep01(t.y);
}

float EdgeMask(float2 uv, float size, float smoothness)
{
	half2 d = abs(uv - float2(0.5, 0.5)) * size;
	//d = pow(saturate(d), 2); // Roundness
	return pow(saturate(dot(d, d)), smoothness);
}

half Vignette(float2 uv, float intensity, float roundness, float smoothness)
{
	float2 dist = abs(uv - float2(0.5, 0.5)) * intensity;

	dist.x *= roundness;
	return pow(saturate(1.0 - dot(dist, dist)), smoothness);
}

float RadialMask(float2 uv) 
{
	return length(float2(float2(0.5, 0.5) - frac(uv)));
}

float RadialMask(float2 uv, float t) 
{
	float falloff = RadialMask(uv);
	falloff = smoothstep(t, 1.0, falloff);

	return saturate(falloff);
}
	
float RadialMask(float2 uv, float size, float falloff)
{
	float2 center = uv * 2 - 1;
	float gradient = smoothstep(size, size + falloff, RadialMask(uv));
	return gradient;
}

float LinearMirroredMask(float2 uv, half angle, half size, half falloff, half offset) 
{
	uv = RotateUV(uv, float2(0.5, 0.5), angle);
	float2 center = uv * 2.0 - 1.0;
	float dist = saturate(abs(center.y -offset)) ;

	float mask = smoothstep(size , size + falloff, dist);

	return saturate(mask);
}

float2 CartesianToPolar(float2 uv)
{
	float2 polar = uv - float2(0.5, 0.5);
	float2 uv2 = polar;

	//Radial
	polar.x = length(polar) *0.01;
	//Angular
	polar.y = 0.5 + (atan2(uv2.x, uv2.y) / 6.283185);

	return polar;
}

// (returns 1.0 when orthographic)
float CheckPerspective(float x)
{
	return lerp(x, 1.0, unity_OrthoParams.w);
}

#define NEAR_PLANE _ProjectionParams.y
#define FAR_PLANE _ProjectionParams.z

float LinearDepthFade(float linearDepth, float start, float end, float invert, float enable)
{
	if(enable == 0.0) return 1.0;
	
	float rawDepth = (linearDepth * FAR_PLANE) - NEAR_PLANE;
	float eyeDepth = FAR_PLANE - ((_ZBufferParams.z * (1.0 - rawDepth) + _ZBufferParams.w) * _ProjectionParams.w);

	float perspDist = rawDepth;
	float orthoDist = eyeDepth;

	//Non-linear depth values
	float dist = lerp(perspDist, orthoDist, unity_OrthoParams.w);
	
	float fadeFactor = saturate((end - dist) / (end-start));

	//OpenGL + Vulkan
	#if !defined(UNITY_REVERSED_Z)
	fadeFactor = 1-fadeFactor;
	#endif
	
	if (invert == 1.0) fadeFactor = 1-fadeFactor;

	return fadeFactor;
}

//Having a world position, fading can be radial
float LinearDistanceFade(float3 worldPos, float start, float end, float invert, float enable)
{
	if(enable == 0.0) return 1.0;
	
	float pixelDist = length(_WorldSpaceCameraPos.xyz - worldPos.xyz);

	//Distance based scalar
	float fadeFactor = saturate((end - pixelDist ) / (end-start));

	if (invert == 1.0) fadeFactor = 1-fadeFactor;

	return fadeFactor;
}

float3 GetWorldPosition(float2 screenPos, float deviceDepth )
{
	return ComputeWorldSpacePosition(screenPos, deviceDepth, UNITY_MATRIX_I_VP);
}
