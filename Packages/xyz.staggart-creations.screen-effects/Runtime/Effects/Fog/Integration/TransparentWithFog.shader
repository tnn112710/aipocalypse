//Example shader that shows how the fog effect can be integrated into a transparent material

Shader "Unlit/SCSE_TransparentWithFog"
{
    Properties
	{
    	[HDR] _BaseColor("Color", Color) = (1, 0, 0, 1)
		[Toggle] _Enable("Enable Fog", Float) = 1
	}
    	
    SubShader
    {
        Tags{ "RenderType" = "Transparent" "Queue" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Back
        Zwrite Off
        
        Pass
        {
            HLSLPROGRAM
            
            #pragma vertex vert
            #pragma fragment frag
 
            //Uncomment to disable features that definitely aren't used for a performance gain
            
            //#define DISABLE_FOG_GRADIENT 
            //#define DISABLE_FOG_SKYBOXCOLOR
            //#define DISABLE_FOG_HEIGHTNOISE
            //#define DISABLE_FOG_DIRECTIONAL_COLOR
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            //Source for the "ComputeFog" function and all its dependencies
            #include_with_pragmas "Packages/xyz.staggart-creations.screen-effects/Runtime/Effects/Fog/Fog.hlsl"
 
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
 
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 positionWS: TEXCOORD1;
                float2 screenPos : TEXCOORD2;
                				
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
            };
            
            bool _Enable;
            float4 _BaseColor;
 
            v2f vert (appdata v)
            {
                v2f o;
                
                UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = TransformObjectToHClip(v.vertex.xyz);
                o.positionWS = TransformObjectToWorld(v.vertex.xyz).xyz;
                o.screenPos = ComputeScreenPos(v.vertex);
                
                return o;
            }
 
            float4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                float4 color = _BaseColor.rgba;

				if(_Enable == false) return float4(color.rgb, 1.0);
                
                //Apply this function to the final color (after lighting)
                ApplyTransparencyFog(i.positionWS, i.screenPos, color.rgb);
                
                return color;
            }
            
            ENDHLSL
        }
    }
}