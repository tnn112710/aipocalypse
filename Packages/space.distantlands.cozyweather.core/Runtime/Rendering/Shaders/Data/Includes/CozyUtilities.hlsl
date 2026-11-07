
#ifndef COZYUtilities
#define COZYUtilities

float EaseOut(float start, float end, float t) {
    end -= start;
    return - end * t * (t - 2.0) * start;
}

float3 BetterViewDirection(float2 screenPosition) {
    float2 centeredUV = float2(screenPosition * 2 - 1);
    float4 mulMatrixUV = mul(UNITY_MATRIX_I_P, float4(centeredUV.x, -centeredUV.y, 1, 1));
    float3 transformedVector = normalize(mul((float3x3)UNITY_MATRIX_I_V, (mulMatrixUV / mulMatrixUV.a).xyz));
    return normalize(transformedVector);
}

float3 RotateAboutAxis(float3 input, float3 axis, float rotation)
{
    float s = sin(rotation);
    float c = cos(rotation);
    float one_minus_c = 1.0 - c;

    axis = normalize(axis);

    float3x3 rot_mat = { one_minus_c * axis.x * axis.x + c, one_minus_c * axis.x * axis.y - axis.z * s, one_minus_c * axis.z * axis.x + axis.y * s,
        one_minus_c * axis.x * axis.y + axis.z * s, one_minus_c * axis.y * axis.y + c, one_minus_c * axis.y * axis.z - axis.x * s,
        one_minus_c * axis.z * axis.x - axis.y * s, one_minus_c * axis.y * axis.z + axis.x * s, one_minus_c * axis.z * axis.z + c
    };

    return mul(rot_mat, input);
}

float2 RotateUV(float2 uv, float2 center, float rotation)
{
    uv -= center;
    float s = sin(rotation);
    float c = cos(rotation);

    float2x2 rMatrix = float2x2(c, -s, s, c);
    rMatrix *= 0.5;
    rMatrix += 0.5;
    rMatrix = rMatrix * 2 - 1;

    uv.xy = mul(uv.xy, rMatrix);
    uv += center;

    return uv;
}

float RemapValue(float value, float inMin, float inMax, float outMin, float outMax)
{
    float t = (value - inMin) / (inMax - inMin);

    return lerp(outMin, outMax, t);
}

float3 RGBtoHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

float3 HSVtoRGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

float3 AdjustHSV(float3 input, float s, float v) 
{
    float3 hsv = RGBtoHSV(input);
    
    hsv.y = saturate(hsv.y + s); 
    hsv.z = max(0.0, hsv.z + v);
    
    return HSVtoRGB(hsv);
}

#endif