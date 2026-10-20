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

half CreateDebugSplit(float2 uv, float t = 0.5)
{
    float splitAngle = -5;
                
    //Create a mask that represents a vertical split at the middle at a 15 degree angle.
    float angleRad = radians(splitAngle);
    float2 centeredCoord = uv - 0.5;
                
    float cosAngle = cos(-angleRad);
    float sinAngle = sin(-angleRad);
    float2 rotatedCoord;
    rotatedCoord.x = centeredCoord.x * cosAngle - centeredCoord.y * sinAngle;
    rotatedCoord.y = centeredCoord.x * sinAngle + centeredCoord.y * cosAngle;
    
    half mask = 1-step(t-0.5, rotatedCoord.x * 0.9);
    
    return mask;
}

float4 DebugView(float2 uv, float4 screenColor, float3 color, half t)
{
    half mask = CreateDebugSplit(uv, t);
    return float4(lerp(screenColor.rgb, color.rgb, mask), screenColor.a);
}

float4 DebugEyeIndex()
{
    #if defined(UNITY_STEREO_INSTANCING_ENABLED)
    return float4(lerp(float3(1,0,0), float3(0,1,0), unity_StereoEyeIndex), 1.0);
    #else
    return 1;
    #endif
}