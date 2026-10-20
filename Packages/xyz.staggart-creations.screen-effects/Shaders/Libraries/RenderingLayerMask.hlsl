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

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareRenderingLayerTexture.hlsl"
uint effect_RenderingLayer;

//Not using the global variant, because the rendering layer texture may not be available, or the effect uses an invalid mask
//Additionally, if the rendering layer is set to 'Nothing' or 'Everything' then the keyword is also not enabled
#pragma multi_compile_local_fragment _ _LIGHT_LAYERS

#if _LIGHT_LAYERS && (!SHADER_API_GLES3 && !SHADER_API_GLCORE)
#define RENDERING_LAYER_MASK_ENABLED
#endif

uint RenderingLayerMask(float2 uv)
{
	#ifndef RENDERING_LAYER_MASK_ENABLED
	return 1.0;
	#else
	uint effectLayer = asuint(effect_RenderingLayer);
	
	//Special case: if effectLayer is "Everything" (0xFFFFFFFF), always return 1
	if (effectLayer == 0)
	{
		return 1.0;
	}
	
	uint2 coords = uint2(uv * _ScreenParams.xy);
	uint sceneRenderingLayer = LoadSceneRenderingLayer(coords);

	uint mask = (effectLayer & sceneRenderingLayer) != 0 ? 1.0 : 0.0;
	
	return mask;
	#endif
}

bool Masked(float2 uv)
{
	return RenderingLayerMask(uv) == 0.0;
}

#define SAMPLE_MASK Masked(SCREEN_COORDS)
#define SAMPLE_MASK_AT(uv) Masked(uv)
#define APPLY_MASK if(Masked(SCREEN_COORDS)) return screenColor;
#define APPLY_MASK_AT(uv) if(Masked(uv)) return screenColor;