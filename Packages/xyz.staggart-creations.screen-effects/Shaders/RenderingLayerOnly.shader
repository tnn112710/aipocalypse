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

Shader "SC Screen Effects/Write to Rendering Layer"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("ZTest", Int) = 6
		[MaterialEnum(Both,0,Front,1,Back,2)] _Cull("Render faces", Float) = 1
        
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 0
        _BaseMap("ClippingMask", 2D) = "white" {}
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest+2" "RenderPipeline" = "UniversalPipeline" }
        
        Cull [_Cull]
        ZTest [_ZTest]
		ZWrite Off
        
        HLSLINCLUDE
        #pragma target 4.5
        
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
        
        //GPU Instancing
        #pragma multi_compile_instancing
        #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
        ENDHLSL
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ColorMask 0 0

            HLSLPROGRAM
            
            #define SHADERPASS_FORWARD

            #include_with_pragmas "RenderingLayerWriting.hlsl"
            
            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
        
        //Required, as rendering layers are some times written during the normal pre-pass
        Pass
		{
			Name "DepthNormals"
			Tags{ "LightMode" = "DepthNormals" }
            
            ColorMask 0 0
            			
			HLSLPROGRAM

            #define SHADERPASS_NORMALS
            
            #include_with_pragmas "RenderingLayerWriting.hlsl"
            
            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
		}

        //Deferred rendering
		Pass
        {
            Name "GBuffer"
            Tags{ "LightMode" = "UniversalGBuffer" }
            
            ColorMask 0 0
            ColorMask 0 1
            ColorMask 0 2
            ColorMask 0 3
            ColorMask 0 4
            
            HLSLPROGRAM

            #define SHADERPASS_DEFERRED
            
            #include_with_pragmas "RenderingLayerWriting.hlsl"
            
            #pragma vertex Vert
            #pragma fragment Frag

            ENDHLSL
        }
    }
    
    FallBack "Hidden/InternalErrorShader"
}