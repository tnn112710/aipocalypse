// Enemy eye glow (EnemyEyes): a camera-facing dot that keeps a minimum size on screen, lit only from the front.
// _EnemyEyeGlow is a global (0 by day, NightHighlight's eye brightness at night), so every eye shares one material.
#ifndef AIPOCALYPSE_ENEMY_EYE_GLOW
#define AIPOCALYPSE_ENEMY_EYE_GLOW

float _EnemyEyeGlow;

// Built-in Quad corners (-0.5..0.5 in xy) spread around the object's origin, facing the camera. The dot is Size metres
// wide up close and MinAngle × distance far away; it moves toward the camera by its own size so the head doesn't hide it.
void EyeBillboard_float(float3 PositionOS, float Size, float MinAngle, out float3 Out)
{
#ifdef SHADERGRAPH_PREVIEW
    Out = PositionOS;
#else
    float3 center = TransformObjectToWorld(float3(0, 0, 0));
    float3 toCamera = _WorldSpaceCameraPos - center;
    float distance = length(toCamera);
    float size = max(Size, distance * MinAngle);
    center += toCamera / max(distance, 0.001) * size;
    float3 right = UNITY_MATRIX_V[0].xyz;
    float3 up = UNITY_MATRIX_V[1].xyz;
    Out = TransformWorldToObject(center + (right * PositionOS.x + up * PositionOS.y) * size);
#endif
}

void EyeBillboard_half(half3 PositionOS, half Size, half MinAngle, out half3 Out)
{
    float3 o;
    EyeBillboard_float(PositionOS, Size, MinAngle, o);
    Out = o;
}

// Soft round dot × colour × the night global, faded out when the eye (object +Z) faces away from the camera.
void EyeGlow_float(float2 UV, float4 Color, out float3 Out)
{
    float r = saturate(1 - length(UV - 0.5) * 2);
    float dotShape = r * r;
#ifdef SHADERGRAPH_PREVIEW
    Out = Color.rgb * dotShape;
#else
    float3 forward = normalize(float3(UNITY_MATRIX_M._m02, UNITY_MATRIX_M._m12, UNITY_MATRIX_M._m22));
    float3 center = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    float facing = smoothstep(-0.15, 0.35, dot(forward, normalize(_WorldSpaceCameraPos - center)));
    Out = Color.rgb * (dotShape * facing * _EnemyEyeGlow);
#endif
}

void EyeGlow_half(half2 UV, half4 Color, out half3 Out)
{
    float3 o;
    EyeGlow_float(UV, Color, o);
    Out = o;
}

#endif
