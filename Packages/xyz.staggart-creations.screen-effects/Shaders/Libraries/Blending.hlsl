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

#ifndef BLENDING_INCLUDED
#define BLENDING_INCLUDED

float3 BlendAdditive(float3 a, float3 b) 
{
	return a + b;
}

float3 BlendExclusion(float3 a, float3 b)
{
	return a + b - 2.0 * a * b;
}

float3 BlendLighten(float3 a, float3 b)
{
	return max(a, b);
}

//single channel overlay
float BlendOverlay(float a, float b)
{
	return (b < 0.5) ? 2.0 * a * b : 1.0 - 2.0 * (1.0 - a) * (1.0 - b);
}

//RGB overlay
float3 BlendOverlay(float3 a, float3 b)
{
	float3 color;
	color.r = BlendOverlay(a.r, b.r);
	color.g = BlendOverlay(a.g, b.g);
	color.b = BlendOverlay(a.b, b.b);
	return color;
}

float3 BlendScreen(float3 a, float3 b)
{
	return a + b - a * b;
}

half AddSub(half a, half b)
{
	return (b < 0.5) ? a + (2.0 * b - 1.0) : a + 2.0 * (b - 0.5);
}

half3 AddSub(half3 a, half3 b)
{
	return (length(b) < 0.5) ? a + (2.0 * b - 1.0) : a + 2.0 * (b - 0.5);
}

float CalculateFoamWeight(float gradient, float input)
{
	gradient = saturate(1.0 - gradient);
	return smoothstep(gradient, gradient + 1.0, input);
}
#endif